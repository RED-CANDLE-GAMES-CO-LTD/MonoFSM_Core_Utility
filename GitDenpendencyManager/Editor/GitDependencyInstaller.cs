using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using MonoFSM.Utility.Editor;
using Newtonsoft.Json;
using UnityEngine;
using PackageInfo = UnityEditor.PackageManager.PackageInfo;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.PackageManager;
using System.IO;
using Newtonsoft.Json.Linq;
#endif

namespace MonoFSM.Core
{
    /// <summary>
    /// Git Dependency 安裝器 - 檢查和安裝 package.json 中的 git dependencies
    /// 使用 #if UNITY_EDITOR 模式，可在 Runtime assembly 中提供 Editor 功能
    /// </summary>
    public static partial class GitDependencyInstaller
    {
        /// <summary>
        /// Git 依賴資訊
        /// </summary>
        [Serializable]
        public class GitDependencyInfo
        {
            public string packageName;
            public string gitUrl;
            public bool isInstalled;
            public string installedVersion;
            public string targetVersion;

            public GitDependencyInfo(string name, string url)
            {
                packageName = name;
                gitUrl = url;
                isInstalled = false;
                installedVersion = "";
                targetVersion = "";
            }

            /// <summary>
            ///     檢查是否有可用的版本更新
            /// </summary>
            public bool HasVersionUpdate()
            {
                if (!isInstalled || string.IsNullOrEmpty(installedVersion) || string.IsNullOrEmpty(targetVersion))
                    return false;

                // 如果 targetVersion 是 "latest"，假設總是有更新
                if (targetVersion == "latest")
                    return true;

                return CompareVersions(targetVersion, installedVersion) > 0;
            }

            /// <summary>
            ///     比較兩個版本號，返回 1 表示 version1 > version2，0 表示相等，-1 表示 version1 < version2
            /// </summary>
            private static int CompareVersions(string version1, string version2)
            {
                if (string.IsNullOrEmpty(version1) || string.IsNullOrEmpty(version2))
                    return 0;

                // 移除可能的 "v" 前綴
                version1 = version1.TrimStart('v');
                version2 = version2.TrimStart('v');

                try
                {
                    var v1Parts = version1.Split('.').Select(int.Parse).ToArray();
                    var v2Parts = version2.Split('.').Select(int.Parse).ToArray();

                    var maxLength = Math.Max(v1Parts.Length, v2Parts.Length);
                    for (var i = 0; i < maxLength; i++)
                    {
                        var v1Value = i < v1Parts.Length ? v1Parts[i] : 0;
                        var v2Value = i < v2Parts.Length ? v2Parts[i] : 0;

                        if (v1Value > v2Value)
                            return 1;
                        if (v1Value < v2Value)
                            return -1;
                    }

                    return 0;
                }
                catch
                {
                    // 如果版本號格式不標準，使用字串比較
                    return string.Compare(version1, version2, StringComparison.Ordinal);
                }
            }
        }

        /// <summary>
        /// 依賴檢查結果
        /// </summary>
        [Serializable]
        public class DependencyCheckResult
        {
            public List<GitDependencyInfo> gitDependencies = new List<GitDependencyInfo>();
            public List<string> missingDependencies = new List<string>();
            public List<string> installedDependencies = new List<string>();
            public bool allDependenciesInstalled = false;
        }

#if UNITY_EDITOR
        private static DependencyCheckResult s_lastCheckResult;
        private static bool s_isChecking = false;

        /// <summary>
        /// 檢查所有 git dependencies 狀態
        /// </summary>
        /// <summary>
        /// 檢查 Git Dependencies（主專案 manifest.json）
        /// </summary>
        public static DependencyCheckResult CheckGitDependencies()
        {
            var manifestPath = Path.Combine(Application.dataPath, "../Packages/manifest.json");
            return CheckGitDependencies(manifestPath);
        }

        /// <summary>
        /// 檢查指定 package.json 的 Git Dependencies
        /// </summary>
        public static DependencyCheckResult CheckGitDependencies(string packageJsonPath)
        {
            if (s_isChecking)
            {
                Debug.LogWarning("[GitDependencyInstaller] 正在檢查中，請稍後...");
                return s_lastCheckResult ?? new DependencyCheckResult();
            }

            s_isChecking = true;
            var result = new DependencyCheckResult();

            try
            {
                // 讀取指定的 package.json
                if (!File.Exists(packageJsonPath))
                {
                    Debug.LogError(
                        $"[GitDependencyInstaller] 找不到 package.json: {packageJsonPath}"
                    );
                    return result;
                }

                var packageText = File.ReadAllText(packageJsonPath);
                var packageJson = JObject.Parse(packageText);
                var dependencies = packageJson["gitDependencies"] as JObject;

                if (dependencies == null)
                {
                    Debug.LogWarning(
                        $"[GitDependencyInstaller] {Path.GetFileName(packageJsonPath)} 中沒有找到 dependencies"
                    );
                    return result;
                }

                // 取得已安裝的套件列表
                var listRequest = Client.List(true, false);
                while (!listRequest.IsCompleted)
                {
                    Thread.Sleep(10);
                }

                var installedPackages =
                    new Dictionary<string, PackageInfo>();
                if (listRequest.Status == StatusCode.Success)
                {
                    foreach (var package in listRequest.Result)
                    {
                        installedPackages[package.name] = package;
                    }
                }

                // 檢查並同步 scopedRegistries
                ManifestManager.SyncScopedRegistriesFromPackageJson(packageJson, packageJsonPath);

                // 分析 git dependencies
                foreach (var dependency in dependencies)
                {
                    var packageName = dependency.Key;
                    var packageUrl = dependency.Value.ToString();

                    // 檢查是否為 git URL
                    // if (IsGitUrl(packageUrl))
                    // {
                    Debug.Log(
                        $"[GitDependencyInstaller] 檢查Git依賴: {packageName} - URL: {packageUrl}"
                    );

                    var gitInfo = new GitDependencyInfo(packageName, packageUrl);

                    // 檢查是否已安裝
                    if (installedPackages.ContainsKey(packageName))
                    {
                        var installedPackage = installedPackages[packageName];
                        gitInfo.isInstalled = true;
                        gitInfo.installedVersion = installedPackage.version;
                        gitInfo.targetVersion = ExtractVersionFromGitUrl(packageUrl);
                        result.installedDependencies.Add(packageName);
                    }
                    else
                    {
                        result.missingDependencies.Add(packageName);
                    }

                    result.gitDependencies.Add(gitInfo);
                    // }
                }

                result.allDependenciesInstalled = result.missingDependencies.Count == 0;

                Debug.Log(
                    $"[GitDependencyInstaller] 檢查完成 - 總計: {result.gitDependencies.Count}, 已安裝: {result.installedDependencies.Count}, 缺失: {result.missingDependencies.Count}"
                );

                // 如果有同步 scopedRegistries，提供額外的日誌
                var packageScopedRegistries = packageJson["scopedRegistries"] as JArray;
                if (packageScopedRegistries != null && packageScopedRegistries.Count > 0)
                {
                    Debug.Log(
                        $"[GitDependencyInstaller] 已檢查並同步 {packageScopedRegistries.Count} 個 scopedRegistries 到主專案"
                    );
                }
            }
            catch (Exception ex)
            {
                Debug.LogError(
                    $"[GitDependencyInstaller] 檢查 git dependencies 時發生錯誤: {ex.Message}"
                );
            }
            finally
            {
                s_isChecking = false;
                s_lastCheckResult = result;
            }

            return result;
        }

        /// <summary>
        /// 安裝所有缺失的 git dependencies
        /// </summary>
        public static void InstallMissingGitDependencies(DependencyCheckResult checkResult)
        {
            // 使用共用的安裝判定邏輯過濾需要安裝的依賴
            var dependenciesToInstall = checkResult
                .gitDependencies.Where(d => !d.isInstalled && ShouldInstallDependency(d))
                .ToList();

            if (dependenciesToInstall.Count == 0)
            {
                Debug.Log("[GitDependencyInstaller] 所有 git dependencies 已安裝完成或無需安裝");
                return;
            }

            Debug.Log(
                $"[GitDependencyInstaller] 開始安裝 {dependenciesToInstall.Count} 個缺失的 git dependencies"
            );

            var installCount = 0;
            var failedCount = 0;

            foreach (var gitDep in dependenciesToInstall)
            {
                // 使用完全共用的同步安裝方法
                if (InstallSingleDependencySync(gitDep))
                {
                    installCount++;
                }
                else
                {
                    failedCount++;
                }
            }

            Debug.Log(
                $"[GitDependencyInstaller] 安裝完成 - 成功: {installCount}, 失敗: {failedCount}, 總計: {dependenciesToInstall.Count}"
            );

            // 清除快取並重新整理 AssetDatabase
            ClearCache();
            AssetDatabase.Refresh();
        }

        /// <summary>
        /// 更新指定 package.json 中的 git dependencies
        /// </summary>
        public static void UpdatePackageJsonDependencies(
            string packageJsonPath,
            List<GitDependencyInfo> gitDependencies
        )
        {
            if (!File.Exists(packageJsonPath))
            {
                Debug.LogError($"[GitDependencyInstaller] 找不到 package.json: {packageJsonPath}");
                return;
            }

            try
            {
                var packageText = File.ReadAllText(packageJsonPath);
                var packageJson = JObject.Parse(packageText);

                var dependencies = packageJson["gitDependencies"] as JObject;
                if (dependencies == null)
                {
                    packageJson["gitDependencies"] = dependencies = new JObject();
                }

                var addedCount = 0;
                var updatedCount = 0;
                
                foreach (var gitDep in gitDependencies)
                {
                    var newUrl = gitDep.gitUrl;
                    
                    // 如果 gitUrl 是空的，但有 targetVersion，嘗試更新現有 URL 的版本號
                    if (string.IsNullOrEmpty(newUrl) && !string.IsNullOrEmpty(gitDep.targetVersion))
                    {
                        if (dependencies.ContainsKey(gitDep.packageName))
                        {
                            var existingUrl = dependencies[gitDep.packageName]?.ToString();
                            if (!string.IsNullOrEmpty(existingUrl))
                            {
                                newUrl = UpdateGitUrlVersion(existingUrl, gitDep.targetVersion);
                            }
                        }
                    }
                    
                    if (!string.IsNullOrEmpty(newUrl))
                    {
                        if (dependencies.ContainsKey(gitDep.packageName))
                        {
                            var oldUrl = dependencies[gitDep.packageName]?.ToString();
                            if (oldUrl != newUrl)
                            {
                                dependencies[gitDep.packageName] = newUrl;
                                updatedCount++;
                                Debug.Log(
                                    $"[GitDependencyInstaller] 已更新 package.json 中的 {gitDep.packageName}: {oldUrl} → {newUrl}"
                                );
                            }
                        }
                        else
                        {
                            dependencies[gitDep.packageName] = newUrl;
                            addedCount++;
                            Debug.Log(
                                $"[GitDependencyInstaller] 已添加到 package.json: {gitDep.packageName} = {newUrl}"
                            );
                        }
                    }
                }

                var totalChanges = addedCount + updatedCount;
                if (totalChanges > 0)
                {
                    File.WriteAllText(
                        packageJsonPath,
                        packageJson.ToString(Formatting.Indented)
                    );
                    Debug.Log(
                        $"[GitDependencyInstaller] 已更新 package.json，添加了 {addedCount} 個、更新了 {updatedCount} 個 dependencies"
                    );
                }
                else
                {
                    Debug.Log("[GitDependencyInstaller] package.json 已是最新狀態");
                }
            }
            catch (Exception ex)
            {
                Debug.LogError(
                    $"[GitDependencyInstaller] 更新 package.json 時發生錯誤: {ex.Message}"
                );
            }
        }

        /// <summary>
        /// 判斷是否為 Git URL
        /// </summary>
        public static bool IsGitUrl(string url)
        {
            if (string.IsNullOrEmpty(url))
                return false;

            return url.StartsWith("https://github.com/")
                || url.StartsWith("git@github.com:")
                || url.StartsWith("git://")
                || url.Contains(".git");
        }

        /// <summary>
        /// 從 Git URL 中提取版本資訊
        /// </summary>
        private static string ExtractVersionFromGitUrl(string gitUrl)
        {
            // 嘗試提取 #v 或 #
            var hashIndex = gitUrl.IndexOf('#');
            if (hashIndex > 0 && hashIndex < gitUrl.Length - 1)
            {
                return gitUrl.Substring(hashIndex + 1);
            }

            return "latest";
        }
        
        /// <summary>
        /// 更新 Git URL 中的版本號
        /// </summary>
        private static string UpdateGitUrlVersion(string gitUrl, string newVersion)
        {
            if (string.IsNullOrEmpty(gitUrl) || string.IsNullOrEmpty(newVersion))
                return gitUrl;
                
            // 移除現有的版本號（如果有的話）
            var hashIndex = gitUrl.IndexOf('#');
            var baseUrl = hashIndex > 0 ? gitUrl.Substring(0, hashIndex) : gitUrl;
            
            // 添加新版本號
            return $"{baseUrl}#{newVersion}";
        }

        /// <summary>
        /// 取得最後一次檢查結果
        /// </summary>
        public static DependencyCheckResult GetLastCheckResult()
        {
            return s_lastCheckResult ?? new DependencyCheckResult();
        }

        /// <summary>
        /// 清除快取
        /// </summary>
        public static void ClearCache()
        {
            s_lastCheckResult = null;
            s_isChecking = false;
        }


#else
        // Runtime 版本 - 提供基本的狀態查詢
        public static DependencyCheckResult CheckGitDependencies()
        {
            Debug.LogWarning("[GitDependencyInstaller] Runtime 模式下無法檢查 git dependencies");
            return new DependencyCheckResult();
        }

        public static void InstallMissingGitDependencies()
        {
            Debug.LogWarning("[GitDependencyInstaller] Runtime 模式下無法安裝 git dependencies");
        }

        public static void UpdatePackageJsonDependencies(
            string packageJsonPath,
            List<GitDependencyInfo> gitDependencies
        )
        {
            Debug.LogWarning("[GitDependencyInstaller] Runtime 模式下無法更新 package.json");
        }

        public static DependencyCheckResult GetLastCheckResult()
        {
            return new DependencyCheckResult();
        }

        public static void ClearCache() { }
#endif
    }
}
