using System.Collections.Generic;
using UnityEngine;
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
    public static class GitDependencyInstaller
    {
        /// <summary>
        /// Git 依賴資訊
        /// </summary>
        [System.Serializable]
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
        }

        /// <summary>
        /// 依賴檢查結果
        /// </summary>
        [System.Serializable]
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
                var dependencies = packageJson["dependencies"] as JObject;

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
                    System.Threading.Thread.Sleep(10);
                }

                var installedPackages =
                    new Dictionary<string, UnityEditor.PackageManager.PackageInfo>();
                if (listRequest.Status == StatusCode.Success)
                {
                    foreach (var package in listRequest.Result)
                    {
                        installedPackages[package.name] = package;
                    }
                }

                // 分析 git dependencies
                foreach (var dependency in dependencies)
                {
                    var packageName = dependency.Key;
                    var packageUrl = dependency.Value.ToString();

                    // 檢查是否為 git URL
                    if (IsGitUrl(packageUrl))
                    {
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
                    }
                }

                result.allDependenciesInstalled = result.missingDependencies.Count == 0;

                Debug.Log(
                    $"[GitDependencyInstaller] 檢查完成 - 總計: {result.gitDependencies.Count}, 已安裝: {result.installedDependencies.Count}, 缺失: {result.missingDependencies.Count}"
                );
            }
            catch (System.Exception ex)
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
            // 清除快取以確保取得最新狀態
            // ClearCache();
            // var checkResult = CheckGitDependencies();
            // if (checkResult.missingDependencies.Count == 0)
            // {
            //     Debug.Log("[GitDependencyInstaller] 所有 git dependencies 已安裝完成");
            //     return;
            // }

            Debug.Log(
                $"[GitDependencyInstaller] 開始安裝 {checkResult.missingDependencies.Count} 個缺失的 git dependencies"
            );

            var installCount = 0;
            foreach (var gitDep in checkResult.gitDependencies)
            {
                if (!gitDep.isInstalled)
                {
                    Debug.Log(
                        $"[GitDependencyInstaller] 正在安裝: {gitDep.packageName} from {gitDep.gitUrl}"
                    );

                    var addRequest = Client.Add(gitDep.gitUrl);
                    while (!addRequest.IsCompleted)
                    {
                        System.Threading.Thread.Sleep(50);
                    }

                    if (addRequest.Status == StatusCode.Success)
                    {
                        Debug.Log($"[GitDependencyInstaller] 成功安裝: {gitDep.packageName}");
                        installCount++;
                    }
                    else
                    {
                        Debug.LogError(
                            $"[GitDependencyInstaller] 安裝失敗: {gitDep.packageName} - {addRequest.Error?.message}"
                        );
                    }
                }
            }

            Debug.Log(
                $"[GitDependencyInstaller] 安裝完成 - 成功: {installCount}/{checkResult.missingDependencies.Count}"
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

                var dependencies = packageJson["dependencies"] as JObject;
                if (dependencies == null)
                {
                    packageJson["dependencies"] = dependencies = new JObject();
                }

                var addedCount = 0;
                foreach (var gitDep in gitDependencies)
                {
                    if (!dependencies.ContainsKey(gitDep.packageName))
                    {
                        dependencies[gitDep.packageName] = gitDep.gitUrl;
                        addedCount++;
                        Debug.Log(
                            $"[GitDependencyInstaller] 已添加到 package.json: {gitDep.packageName}"
                        );
                    }
                }

                if (addedCount > 0)
                {
                    File.WriteAllText(
                        packageJsonPath,
                        packageJson.ToString(Newtonsoft.Json.Formatting.Indented)
                    );
                    Debug.Log(
                        $"[GitDependencyInstaller] 已更新 package.json，添加了 {addedCount} 個 dependencies"
                    );
                }
                else
                {
                    Debug.Log("[GitDependencyInstaller] package.json 已是最新狀態");
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogError(
                    $"[GitDependencyInstaller] 更新 package.json 時發生錯誤: {ex.Message}"
                );
            }
        }

        /// <summary>
        /// 判斷是否為 Git URL
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
