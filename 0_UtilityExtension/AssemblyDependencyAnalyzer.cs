using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
#if UNITY_EDITOR
using UnityEditor;
using Newtonsoft.Json.Linq;
#endif

namespace MonoFSM.Core
{
    /// <summary>
    /// Assembly Definition 依賴分析器
    /// 分析 package 內的 asmdef 引用，並自動更新 package.json dependencies
    /// </summary>
    public static class AssemblyDependencyAnalyzer
    {
        // Static cache for GUID mappings
        private static Dictionary<string, string> s_guidToPackageMap;
        private static Dictionary<string, string> s_guidToAsmdefNameMap;
        private static bool s_guidMappingCacheValid;

        // Additional performance caches
        private static Dictionary<string, string> s_manifestCache;
        private static DateTime s_manifestCacheTime;
        private static readonly TimeSpan CacheValidDuration = TimeSpan.FromMinutes(5); // Cache 有效期 5 分鐘

        // Package path and local package caches
        private static List<string> s_localPackagesCache;
        private static DateTime s_localPackagesCacheTime;
        private static Dictionary<string, string> s_packagePathCache =
            new Dictionary<string, string>();
        private static Dictionary<string, bool> s_isLocalPackageCache =
            new Dictionary<string, bool>();
        private static Dictionary<string, string> s_assemblyNameCache =
            new Dictionary<string, string>();

        /// <summary>
        /// Assembly 依賴資訊
        /// </summary>
        [System.Serializable]
        public class AssemblyDependencyInfo
        {
            public string assemblyName;
            public string assemblyPath;
            public string packageName;
            public string packagePath;
            public List<string> referencedGUIDs = new List<string>();
            public List<ReferencedPackageInfo> referencedPackages =
                new List<ReferencedPackageInfo>();
            public bool hasExternalReferences;

            public AssemblyDependencyInfo(string name, string path)
            {
                assemblyName = name;
                assemblyPath = path;
                packageName = "";
                packagePath = "";
                hasExternalReferences = false;
            }
        }

        /// <summary>
        /// 被引用的 Package 資訊
        /// </summary>
        [System.Serializable]
        public class ReferencedPackageInfo
        {
            public string packageName;
            public string packagePath;
            public string gitUrl;
            public bool isLocalPackage;
            public bool hasGitUrl;
            public string assemblyName; // 被引用的 assembly 名稱

            public ReferencedPackageInfo(string name)
            {
                packageName = name;
                packagePath = "";
                gitUrl = "";
                isLocalPackage = false;
                hasGitUrl = false;
                assemblyName = "";
            }
        }

        /// <summary>
        /// 分析結果
        /// </summary>
        [System.Serializable]
        public class AnalysisResult
        {
            public string targetPackageJsonPath;
            public string targetPackageName;
            public List<AssemblyDependencyInfo> assemblies = new List<AssemblyDependencyInfo>();
            public List<ReferencedPackageInfo> missingDependencies =
                new List<ReferencedPackageInfo>();
            public List<ReferencedPackageInfo> existingDependencies =
                new List<ReferencedPackageInfo>();
            public List<ReferencedPackageInfo> needGitUrlDependencies =
                new List<ReferencedPackageInfo>();
            public int totalAssemblies;
            public int externalReferences;

            public AnalysisResult(string packageJsonPath)
            {
                targetPackageJsonPath = packageJsonPath;
                targetPackageName = GetPackageNameFromPath(packageJsonPath);
                totalAssemblies = 0;
                externalReferences = 0;
            }

            private string GetPackageNameFromPath(string packageJsonPath)
            {
                try
                {
                    // 嘗試從 package.json 檔案中讀取 name
                    if (File.Exists(packageJsonPath))
                    {
                        var packageText = File.ReadAllText(packageJsonPath);
                        var packageJson = JObject.Parse(packageText);
                        var name = packageJson["name"]?.ToString();
                        if (!string.IsNullOrEmpty(name))
                        {
                            return name;
                        }
                    }

                    // 備用：從目錄名稱取得
                    var packageDir = Path.GetDirectoryName(packageJsonPath);
                    return Path.GetFileName(packageDir);
                }
                catch
                {
                    return "Unknown";
                }
            }
        }

#if UNITY_EDITOR
        /// <summary>
        /// 分析指定 package.json 的 Assembly Dependencies
        /// </summary>
        public static AnalysisResult AnalyzePackageDependencies(string packageJsonPath)
        {
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            var result = new AnalysisResult(packageJsonPath);

            if (!File.Exists(packageJsonPath))
            {
                UnityEngine.Debug.LogError(
                    $"[AssemblyDependencyAnalyzer] package.json 不存在: {packageJsonPath}"
                );
                return result;
            }

            try
            {
                // 計時：讀取 package.json
                var jsonReadTime = System.Diagnostics.Stopwatch.StartNew();
                var packageJson = JObject.Parse(File.ReadAllText(packageJsonPath));
                result.targetPackageName = packageJson["name"]?.ToString() ?? "Unknown";
                jsonReadTime.Stop();

                // 計時：尋找 asmdef 檔案
                var findFilesTime = System.Diagnostics.Stopwatch.StartNew();
                var packageDir = Path.GetDirectoryName(packageJsonPath);
                var asmdefFiles = Directory.GetFiles(
                    packageDir,
                    "*.asmdef",
                    SearchOption.AllDirectories
                );
                result.totalAssemblies = asmdefFiles.Length;
                findFilesTime.Stop();

                // 計時：建立 GUID 映射
                var mappingTime = System.Diagnostics.Stopwatch.StartNew();
                BuildGuidToPackageMaps();
                mappingTime.Stop();

                // 計時：取得現有依賴
                var dependenciesTime = System.Diagnostics.Stopwatch.StartNew();
                var existingDependencies = GetExistingDependencies(packageJson);
                dependenciesTime.Stop();

                UnityEngine.Debug.Log(
                    $"[AssemblyDependencyAnalyzer] 分析 {result.targetPackageName}，找到 {asmdefFiles.Length} 個 asmdef 檔案\n"
                        + $"效能統計 - 讀取JSON: {jsonReadTime.ElapsedMilliseconds}ms, "
                        + $"尋找檔案: {findFilesTime.ElapsedMilliseconds}ms, "
                        + $"GUID映射: {mappingTime.ElapsedMilliseconds}ms, "
                        + $"現有依賴: {dependenciesTime.ElapsedMilliseconds}ms"
                );

                // 計時：分析所有 asmdef
                var analysisTime = System.Diagnostics.Stopwatch.StartNew();
                foreach (var asmdefPath in asmdefFiles)
                {
                    var singleAssemblyTime = System.Diagnostics.Stopwatch.StartNew();
                    var assemblyInfo = AnalyzeAssemblyDefinition(
                        asmdefPath,
                        result.targetPackageName
                    );
                    result.assemblies.Add(assemblyInfo);
                    singleAssemblyTime.Stop();

                    UnityEngine.Debug.Log(
                        $"[AssemblyDependencyAnalyzer] {assemblyInfo.assemblyName}: "
                            + $"引用數={assemblyInfo.referencedGUIDs.Count}, 外部引用={assemblyInfo.hasExternalReferences}, "
                            + $"外部package數={assemblyInfo.referencedPackages.Count}, 耗時={singleAssemblyTime.ElapsedMilliseconds}ms"
                    );

                    if (assemblyInfo.hasExternalReferences)
                    {
                        result.externalReferences++;

                        // 檢查引用的 packages
                        foreach (var refPackage in assemblyInfo.referencedPackages)
                        {
                            if (
                                refPackage.packageName != result.targetPackageName
                                && !IsUnityBuiltInPackage(refPackage.packageName)
                                && refPackage.packageName != "Assets"
                            ) // 跳過 Assets，無法作為 package 安裝
                            {
                                // 檢查是否已存在於 dependencies 中
                                if (existingDependencies.ContainsKey(refPackage.packageName))
                                {
                                    refPackage.gitUrl = existingDependencies[
                                        refPackage.packageName
                                    ];
                                    refPackage.hasGitUrl = IsGitUrl(refPackage.gitUrl);

                                    if (
                                        !result.existingDependencies.Any(d =>
                                            d.packageName == refPackage.packageName
                                        )
                                    )
                                    {
                                        result.existingDependencies.Add(refPackage);
                                    }
                                }
                                else
                                {
                                    // 新的依賴 - 嘗試從主專案 manifest.json 取得 Git URL
                                    var manifestGitUrl = TryGetGitUrlFromManifest(
                                        refPackage.packageName
                                    );
                                    if (!string.IsNullOrEmpty(manifestGitUrl))
                                    {
                                        refPackage.gitUrl = manifestGitUrl;
                                        refPackage.hasGitUrl = IsGitUrl(manifestGitUrl);
                                    }

                                    if (
                                        !result.missingDependencies.Any(d =>
                                            d.packageName == refPackage.packageName
                                        )
                                    )
                                    {
                                        result.missingDependencies.Add(refPackage);

                                        // 所有缺失的 dependencies 都需要 Git URL（除非已從 manifest 找到）
                                        if (
                                            string.IsNullOrEmpty(refPackage.gitUrl)
                                            || !refPackage.hasGitUrl
                                        )
                                        {
                                            result.needGitUrlDependencies.Add(refPackage);
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
                analysisTime.Stop();

                stopwatch.Stop();
                UnityEngine.Debug.Log(
                    $"[AssemblyDependencyAnalyzer] 分析完成 - 缺失依賴: {result.missingDependencies.Count}, 需要 Git URL: {result.needGitUrlDependencies.Count}\n"
                        + $"總耗時: {stopwatch.ElapsedMilliseconds}ms (Assembly分析: {analysisTime.ElapsedMilliseconds}ms)"
                );
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                UnityEngine.Debug.LogError(
                    $"[AssemblyDependencyAnalyzer] 分析失敗: {ex.Message} (總耗時: {stopwatch.ElapsedMilliseconds}ms)"
                );
            }

            return result;
        }

        /// <summary>
        /// 分析單個 Assembly Definition
        /// </summary>
        private static AssemblyDependencyInfo AnalyzeAssemblyDefinition(
            string asmdefPath,
            string targetPackageName
        )
        {
            var totalStopwatch = System.Diagnostics.Stopwatch.StartNew();
            var asmdefName = Path.GetFileNameWithoutExtension(asmdefPath);
            var assemblyInfo = new AssemblyDependencyInfo(asmdefName, asmdefPath);

            try
            {
                // 計時：檔案讀取和JSON解析
                var fileReadStopwatch = System.Diagnostics.Stopwatch.StartNew();
                var asmdefJson = JObject.Parse(File.ReadAllText(asmdefPath));
                var references = asmdefJson["references"] as JArray;
                fileReadStopwatch.Stop();

                if (references != null)
                {
                    var referenceCount = references.Count;
                    UnityEngine.Debug.Log(
                        $"[AssemblyDependencyAnalyzer] {asmdefName} 有 {referenceCount} 個引用，檔案讀取耗時: {fileReadStopwatch.ElapsedMilliseconds}ms"
                    );

                    var guidProcessingTime = System.Diagnostics.Stopwatch.StartNew();
                    var packagePathLookupTime = 0L;
                    var isLocalPackageTime = 0L;
                    var getAssemblyNameTime = 0L;

                    foreach (var reference in references)
                    {
                        var singleGuidStopwatch = System.Diagnostics.Stopwatch.StartNew();

                        var rawGuid = reference.ToString();
                        // 移除 "GUID:" 前綴，只保留實際的 GUID
                        var guid = rawGuid.StartsWith("GUID:") ? rawGuid.Substring(5) : rawGuid;
                        assemblyInfo.referencedGUIDs.Add(rawGuid); // 保存原始格式用於記錄
                        var asmdefName_fromGuid = s_guidToAsmdefNameMap?.GetValueOrDefault(
                            guid,
                            "Unknown"
                        );

                        if (s_guidToPackageMap != null && s_guidToPackageMap.ContainsKey(guid))
                        {
                            var packageName = s_guidToPackageMap[guid];

                            var packagePathStopwatch = System.Diagnostics.Stopwatch.StartNew();
                            var packagePath = GetPackagePathByName(packageName);
                            packagePathStopwatch.Stop();
                            packagePathLookupTime += packagePathStopwatch.ElapsedMilliseconds;

                            // 檢查是否為外部引用（不在目標 package 內）
                            if (
                                packageName != targetPackageName
                                && !string.IsNullOrEmpty(packageName)
                            )
                            {
                                assemblyInfo.hasExternalReferences = true;

                                var isLocalStopwatch = System.Diagnostics.Stopwatch.StartNew();
                                var isLocalPackage = IsLocalPackage(packageName);
                                isLocalStopwatch.Stop();
                                isLocalPackageTime += isLocalStopwatch.ElapsedMilliseconds;

                                var getNameStopwatch = System.Diagnostics.Stopwatch.StartNew();
                                var assemblyName =
                                    asmdefName_fromGuid ?? GetAssemblyNameByGuid(guid);
                                getNameStopwatch.Stop();
                                getAssemblyNameTime += getNameStopwatch.ElapsedMilliseconds;

                                var refPackageInfo = new ReferencedPackageInfo(packageName)
                                {
                                    packagePath = packagePath,
                                    isLocalPackage = isLocalPackage,
                                    assemblyName = assemblyName,
                                };

                                assemblyInfo.referencedPackages.Add(refPackageInfo);
                            }
                        }

                        singleGuidStopwatch.Stop();
                        if (singleGuidStopwatch.ElapsedMilliseconds > 50) // 只記錄超過50ms的GUID處理
                        {
                            UnityEngine.Debug.LogWarning(
                                $"[AssemblyDependencyAnalyzer] GUID {guid} 處理耗時異常: {singleGuidStopwatch.ElapsedMilliseconds}ms"
                            );
                        }
                    }
                    guidProcessingTime.Stop();

                    UnityEngine.Debug.Log(
                        $"[AssemblyDependencyAnalyzer] {asmdefName} GUID處理詳細耗時 - "
                            + $"總計: {guidProcessingTime.ElapsedMilliseconds}ms, "
                            + $"PackagePath查詢: {packagePathLookupTime}ms, "
                            + $"IsLocalPackage: {isLocalPackageTime}ms, "
                            + $"GetAssemblyName: {getAssemblyNameTime}ms"
                    );
                }
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogError(
                    $"[AssemblyDependencyAnalyzer] 分析 {asmdefPath} 失敗: {ex.Message}"
                );
            }

            totalStopwatch.Stop();
            UnityEngine.Debug.Log(
                $"[AssemblyDependencyAnalyzer] {asmdefName} 總分析耗時: {totalStopwatch.ElapsedMilliseconds}ms"
            );
            return assemblyInfo;
        }

        /// <summary>
        /// 清除所有快取
        /// </summary>
        public static void ClearAllCaches()
        {
            s_guidToPackageMap = null;
            s_guidToAsmdefNameMap = null;
            s_guidMappingCacheValid = false;
            s_manifestCache = null;
            s_manifestCacheTime = DateTime.MinValue;
            s_localPackagesCache = null;
            s_localPackagesCacheTime = DateTime.MinValue;
            s_packagePathCache.Clear();
            s_isLocalPackageCache.Clear();
            s_assemblyNameCache.Clear();
        }

        /// <summary>
        /// 清除 GUID 映射快取（向後相容）
        /// </summary>
        public static void ClearGuidMappingCache()
        {
            ClearAllCaches();
        }

        /// <summary>
        /// 建立 GUID 到 Package 名稱的映射（Static 版本）
        /// </summary>
        private static void BuildGuidToPackageMaps()
        {
            // 如果快取已經有效，就不重建
            if (
                s_guidMappingCacheValid
                && s_guidToPackageMap != null
                && s_guidToAsmdefNameMap != null
            )
                return;

            s_guidToPackageMap = new Dictionary<string, string>();
            s_guidToAsmdefNameMap = new Dictionary<string, string>();

            try
            {
                // 使用擴充的 PackageHelper 取得所有 packages
                var allPackages = PackageHelper.GetAllPackages();
                UnityEngine.Debug.Log(
                    $"[AssemblyDependencyAnalyzer] 找到 {allPackages.Count} 個 packages"
                );

                foreach (var package in allPackages)
                {
                    UnityEngine.Debug.Log(
                        $"[AssemblyDependencyAnalyzer] 處理 package: {package.name} (source: {package.source})"
                    );

                    // 取得 package 的完整路徑
                    string packageFullPath = null;
                    if (package.source == UnityEditor.PackageManager.PackageSource.Local)
                    {
                        // 本地 package
                        packageFullPath = PackageHelper.GetPackageFullPath(
                            $"Packages/{package.name}"
                        );
                    }
                    else
                    {
                        // Git 或 Registry packages，使用 resolvedPath
                        packageFullPath = package.resolvedPath;
                    }

                    if (!string.IsNullOrEmpty(packageFullPath) && Directory.Exists(packageFullPath))
                    {
                        // 搜尋該 package 內的所有 asmdef 檔案
                        var asmdefFiles = Directory.GetFiles(
                            packageFullPath,
                            "*.asmdef",
                            SearchOption.AllDirectories
                        );

                        foreach (var asmdefFile in asmdefFiles)
                        {
                            var metaFile = asmdefFile + ".meta";
                            if (File.Exists(metaFile))
                            {
                                var guid = ExtractGuidFromMetaFile(metaFile);
                                if (!string.IsNullOrEmpty(guid))
                                {
                                    s_guidToPackageMap[guid] = package.name;
                                    s_guidToAsmdefNameMap[guid] = Path.GetFileNameWithoutExtension(
                                        asmdefFile
                                    );
                                    // Debug.Log($"[AssemblyDependencyAnalyzer] 映射 package: {guid} -> {package.name} ({Path.GetFileName(asmdefFile)})");
                                }
                            }
                        }
                    }
                    else
                    {
                        UnityEngine.Debug.LogWarning(
                            $"[AssemblyDependencyAnalyzer] 無法存取 package 路徑: {package.name} -> {packageFullPath}"
                        );
                    }
                }
                // 2. 處理 Asset的
                var allAsmdefGuids = AssetDatabase.FindAssets("t:AssemblyDefinitionAsset");
                UnityEngine.Debug.Log(
                    "[AssemblyDependencyAnalyzer] 找到Asset中 asmdef GUIDs: "
                        + allAsmdefGuids.Length
                );
                foreach (var guid in allAsmdefGuids)
                {
                    // 如果已經在 packages 中處理過，就跳過
                    if (s_guidToPackageMap.ContainsKey(guid))
                        continue;

                    var asmdefPath = AssetDatabase.GUIDToAssetPath(guid);
                    s_guidToPackageMap[guid] = "Assets"; // 預設為主專案
                    s_guidToAsmdefNameMap[guid] = Path.GetFileNameWithoutExtension(asmdefPath);
                }

                UnityEngine.Debug.Log(
                    $"[AssemblyDependencyAnalyzer] 建立了 {s_guidToPackageMap.Count} 個 GUID 到 Package 的映射"
                );
                s_guidMappingCacheValid = true;
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogError(
                    $"[AssemblyDependencyAnalyzer] 建立 GUID 映射失敗: {ex.Message}"
                );
            }
        }

        /// <summary>
        /// 從 .meta 檔案中提取 GUID
        /// </summary>
        private static string ExtractGuidFromMetaFile(string metaFilePath)
        {
            try
            {
                var lines = File.ReadAllLines(metaFilePath);
                foreach (var line in lines)
                {
                    if (line.StartsWith("guid: "))
                    {
                        return line.Substring(6).Trim();
                    }
                }
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogWarning(
                    $"[AssemblyDependencyAnalyzer] 無法讀取 meta 檔案 {metaFilePath}: {ex.Message}"
                );
            }

            return null;
        }

        /// <summary>
        /// 從 Asset 路徑中取得 Package 名稱
        /// </summary>
        private static string GetPackageNameFromAssetPath(string assetPath)
        {
            if (assetPath.StartsWith("Packages/"))
            {
                var parts = assetPath.Split('/');
                if (parts.Length >= 2)
                {
                    return parts[1]; // Packages/com.example.package/...
                }
            }
            else if (assetPath.StartsWith("Assets/"))
            {
                return ""; // 主專案
            }

            return "";
        }

        /// <summary>
        /// 取得現有的 dependencies
        /// </summary>
        private static Dictionary<string, string> GetExistingDependencies(JObject packageJson)
        {
            var dependencies = new Dictionary<string, string>();

            var depsObject = packageJson["dependencies"] as JObject;
            if (depsObject != null)
            {
                foreach (var dep in depsObject)
                {
                    dependencies[dep.Key] = dep.Value.ToString();
                }
            }

            return dependencies;
        }

        /// <summary>
        /// 取得或快取本地 packages 列表
        /// </summary>
        private static List<string> GetCachedLocalPackages()
        {
            var currentTime = DateTime.Now;

            // 檢查快取是否有效（5分鐘有效期）
            if (
                s_localPackagesCache != null
                && s_localPackagesCacheTime != DateTime.MinValue
                && (currentTime - s_localPackagesCacheTime) < CacheValidDuration
            )
            {
                return s_localPackagesCache;
            }

            // 重新載入並快取
            s_localPackagesCache = PackageHelper.GetLocalPackagePaths();
            s_localPackagesCacheTime = currentTime;
            UnityEngine.Debug.Log(
                $"[AssemblyDependencyAnalyzer] 已快取 {s_localPackagesCache.Count} 個 local packages"
            );

            return s_localPackagesCache;
        }

        /// <summary>
        /// 根據 package 名稱取得路徑（快取版本）
        /// </summary>
        private static string GetPackagePathByName(string packageName)
        {
            // 先檢查快取
            if (s_packagePathCache.TryGetValue(packageName, out var cachedPath))
            {
                return cachedPath;
            }

            var stopwatch = System.Diagnostics.Stopwatch.StartNew();

            // 使用快取的本地 packages
            var localPackages = GetCachedLocalPackages();
            var targetPath = $"Packages/{packageName}";
            var result = "";

            if (localPackages.Contains(targetPath))
            {
                result = PackageHelper.GetPackageFullPath(targetPath);
            }

            // 快取結果
            s_packagePathCache[packageName] = result;

            stopwatch.Stop();
            if (stopwatch.ElapsedMilliseconds > 10) // 只記錄超過10ms的查詢
            {
                UnityEngine.Debug.Log(
                    $"[AssemblyDependencyAnalyzer] GetPackagePathByName({packageName}) 耗時: {stopwatch.ElapsedMilliseconds}ms"
                );
            }

            return result;
        }

        /// <summary>
        /// 檢查是否為本地 package（快取版本）
        /// </summary>
        private static bool IsLocalPackage(string packageName)
        {
            // 先檢查快取
            if (s_isLocalPackageCache.TryGetValue(packageName, out var cachedResult))
            {
                return cachedResult;
            }

            // 使用快取的本地 packages
            var localPackages = GetCachedLocalPackages();
            var result = localPackages.Any(p => p.EndsWith(packageName));

            // 快取結果
            s_isLocalPackageCache[packageName] = result;
            return result;
        }

        /// <summary>
        /// 檢查是否為 Unity 內建 package
        /// </summary>
        private static bool IsUnityBuiltInPackage(string packageName)
        {
            return packageName.StartsWith("com.unity.modules.")
                || packageName.StartsWith("com.unity.")
                || packageName == "";
        }

        /// <summary>
        /// 根據 GUID 取得 Assembly 名稱（快取版本）
        /// </summary>
        private static string GetAssemblyNameByGuid(string guid)
        {
            // 先檢查快取
            if (s_assemblyNameCache.TryGetValue(guid, out var cachedName))
            {
                return cachedName;
            }

            var result = "Unknown";
            var assetPath = AssetDatabase.GUIDToAssetPath(guid);
            if (!string.IsNullOrEmpty(assetPath))
            {
                try
                {
                    var asmdefJson = JObject.Parse(File.ReadAllText(assetPath));
                    result =
                        asmdefJson["name"]?.ToString()
                        ?? Path.GetFileNameWithoutExtension(assetPath);
                }
                catch
                {
                    result = Path.GetFileNameWithoutExtension(assetPath);
                }
            }

            // 快取結果
            s_assemblyNameCache[guid] = result;
            return result;
        }

        /// <summary>
        /// 檢查是否為 Git URL
        /// </summary>
        private static bool IsGitUrl(string url)
        {
            if (string.IsNullOrEmpty(url))
                return false;

            return url.StartsWith("https://github.com/")
                || url.StartsWith("git@github.com:")
                || url.StartsWith("git://")
                || url.Contains(".git");
        }

        /// <summary>
        /// 取得或快取主專案 manifest.json 內容
        /// </summary>
        private static Dictionary<string, string> GetCachedManifestDependencies()
        {
            var manifestPath = Path.Combine(
                UnityEngine.Application.dataPath,
                "../Packages/manifest.json"
            );
            if (!File.Exists(manifestPath))
                return new Dictionary<string, string>();

            var manifestInfo = new FileInfo(manifestPath);
            var currentTime = DateTime.Now;

            // 檢查快取是否有效
            if (
                s_manifestCache != null
                && s_manifestCacheTime != DateTime.MinValue
                && (currentTime - s_manifestCacheTime) < CacheValidDuration
                && manifestInfo.LastWriteTime <= s_manifestCacheTime
            )
            {
                return s_manifestCache;
            }

            // 重新讀取並快取
            try
            {
                var manifestText = File.ReadAllText(manifestPath);
                var manifestJson = JObject.Parse(manifestText);
                var dependencies = manifestJson["dependencies"] as JObject;

                s_manifestCache = new Dictionary<string, string>();
                if (dependencies != null)
                {
                    foreach (var dep in dependencies)
                    {
                        s_manifestCache[dep.Key] = dep.Value.ToString();
                    }
                }

                s_manifestCacheTime = currentTime;
                UnityEngine.Debug.Log(
                    $"[AssemblyDependencyAnalyzer] 已快取 manifest.json，包含 {s_manifestCache.Count} 個 dependencies"
                );
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogWarning(
                    $"[AssemblyDependencyAnalyzer] 讀取 manifest.json 時發生錯誤: {ex.Message}"
                );
                s_manifestCache = new Dictionary<string, string>();
            }

            return s_manifestCache;
        }

        /// <summary>
        /// 嘗試從主專案 manifest.json 取得 Git URL（使用快取版本）
        /// </summary>
        private static string TryGetGitUrlFromManifest(string packageName)
        {
            var manifestDependencies = GetCachedManifestDependencies();

            if (manifestDependencies.ContainsKey(packageName))
            {
                var url = manifestDependencies[packageName];

                // 忽略 file: 開頭的 local package URLs
                if (url.StartsWith("file:"))
                {
                    UnityEngine.Debug.Log(
                        $"[AssemblyDependencyAnalyzer] 忽略 local package URL: {packageName} -> {url}"
                    );
                    return null;
                }

                // 只返回 Git URLs
                if (IsGitUrl(url))
                {
                    UnityEngine.Debug.Log(
                        $"[AssemblyDependencyAnalyzer] 從 manifest.json 找到 Git URL: {packageName} -> {url}"
                    );
                    return url;
                }
            }

            return null;
        }

        /// <summary>
        /// 自動更新 package.json dependencies（批量更新版本，保留向後相容）
        /// </summary>
        public static void UpdatePackageJsonDependencies(
            AnalysisResult analysisResult,
            Dictionary<string, string> gitUrlMappings = null
        )
        {
            if (analysisResult.missingDependencies.Count == 0)
            {
                UnityEngine.Debug.Log(
                    "[AssemblyDependencyAnalyzer] 沒有缺失的 dependencies 需要更新"
                );
                return;
            }

            try
            {
                var packageJson = JObject.Parse(
                    File.ReadAllText(analysisResult.targetPackageJsonPath)
                );
                var dependencies = packageJson["dependencies"] as JObject;
                if (dependencies == null)
                {
                    packageJson["dependencies"] = dependencies = new JObject();
                }

                var addedCount = 0;
                foreach (var missingDep in analysisResult.missingDependencies)
                {
                    if (!dependencies.ContainsKey(missingDep.packageName))
                    {
                        string dependencyUrl;

                        // 嘗試從 Git URL 映射中取得
                        if (
                            gitUrlMappings != null
                            && gitUrlMappings.ContainsKey(missingDep.packageName)
                        )
                        {
                            dependencyUrl = gitUrlMappings[missingDep.packageName];
                        }
                        else if (!string.IsNullOrEmpty(missingDep.gitUrl))
                        {
                            dependencyUrl = missingDep.gitUrl;
                        }
                        else
                        {
                            // 使用版本號或本地路徑
                            dependencyUrl = missingDep.isLocalPackage
                                ? $"file:../{missingDep.packageName}"
                                : "1.0.0"; // 預設版本
                        }

                        dependencies[missingDep.packageName] = dependencyUrl;
                        addedCount++;
                        UnityEngine.Debug.Log(
                            $"[AssemblyDependencyAnalyzer] 已添加 dependency: {missingDep.packageName} -> {dependencyUrl}"
                        );
                    }
                }

                if (addedCount > 0)
                {
                    WritePackageJsonSafely(analysisResult.targetPackageJsonPath, packageJson);
                    UnityEngine.Debug.Log(
                        $"[AssemblyDependencyAnalyzer] 已更新 package.json，添加了 {addedCount} 個 dependencies"
                    );
                    AssetDatabase.Refresh();
                }
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogError(
                    $"[AssemblyDependencyAnalyzer] 更新 package.json 失敗: {ex.Message}"
                );
            }
        }

        /// <summary>
        /// 更新單一 package 到 package.json dependencies
        /// </summary>
        public static void UpdateSinglePackageJsonDependency(
            AnalysisResult analysisResult,
            string packageName,
            string gitUrl
        )
        {
            if (string.IsNullOrEmpty(packageName) || string.IsNullOrEmpty(gitUrl))
            {
                UnityEngine.Debug.LogError(
                    "[AssemblyDependencyAnalyzer] packageName 或 gitUrl 不能為空"
                );
                return;
            }

            try
            {
                var packageJson = JObject.Parse(
                    File.ReadAllText(analysisResult.targetPackageJsonPath)
                );
                var dependencies = packageJson["dependencies"] as JObject;
                if (dependencies == null)
                {
                    packageJson["dependencies"] = dependencies = new JObject();
                }

                // 只添加指定的單一 package
                if (!dependencies.ContainsKey(packageName))
                {
                    dependencies[packageName] = gitUrl;
                    WritePackageJsonSafely(analysisResult.targetPackageJsonPath, packageJson);
                    UnityEngine.Debug.Log(
                        $"[AssemblyDependencyAnalyzer] 已添加單一 dependency: {packageName} -> {gitUrl}"
                    );
                    AssetDatabase.Refresh();
                }
                else
                {
                    UnityEngine.Debug.LogWarning(
                        $"[AssemblyDependencyAnalyzer] {packageName} 已存在於 dependencies 中"
                    );
                }
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogError(
                    $"[AssemblyDependencyAnalyzer] 更新單一 package.json dependency 失敗: {ex.Message}"
                );
            }
        }

        /// <summary>
        /// 安全地寫入 package.json，確保格式正確且無多餘逗號
        /// </summary>
        private static void WritePackageJsonSafely(string filePath, JObject packageJson)
        {
            try
            {
                // 使用自定義格式化設定，確保不會產生多餘的逗號
                var jsonString = packageJson.ToString(Newtonsoft.Json.Formatting.Indented);

                // 額外檢查：移除可能的多餘逗號（在 } 或 ] 前的逗號）
                jsonString = System.Text.RegularExpressions.Regex.Replace(
                    jsonString,
                    @",(\s*[}\]])",
                    "$1"
                );

                File.WriteAllText(filePath, jsonString);
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogError(
                    $"[AssemblyDependencyAnalyzer] 寫入 package.json 時發生錯誤: {ex.Message}"
                );
                throw;
            }
        }

#else
        // Runtime 版本 - 只提供基本功能
        public static AnalysisResult AnalyzePackageDependencies(string packageJsonPath)
        {
            UnityEngine.Debug.LogWarning(
                "[AssemblyDependencyAnalyzer] Runtime 模式下無法分析 Assembly Dependencies"
            );
            return new AnalysisResult(packageJsonPath);
        }

        public static void UpdatePackageJsonDependencies(
            AnalysisResult analysisResult,
            Dictionary<string, string> gitUrlMappings = null
        )
        {
            UnityEngine.Debug.LogWarning(
                "[AssemblyDependencyAnalyzer] Runtime 模式下無法更新 package.json"
            );
        }
#endif
    }
}
