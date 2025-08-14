using UnityEngine;
using UnityEditor;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace MonoFSM.Utility.Editor
{
    [Serializable]
    public class GitPackageInfo
    {
        public string packageName;
        public string gitUrl;
        public string gitPath;
        public string originalDependencyValue;

        public string GetRepositoryUrl()
        {
            // 移除git URL中的?path=部分
            var match = Regex.Match(gitUrl, @"^([^?]+)");
            return match.Success ? match.Groups[1].Value : gitUrl;
        }

        public string GetSubmoduleName()
        {
            // 從package name或repository URL提取合適的submodule名稱
            if (!string.IsNullOrEmpty(packageName))
            {
                return packageName.Replace("com.", "").Replace(".", "_");
            }

            // 從URL提取repository名稱
            var match = Regex.Match(GetRepositoryUrl(), @"([^/]+)\.git$");
            if (match.Success)
            {
                return match.Groups[1].Value;
            }

            // 後備方案
            return Path.GetFileName(GetRepositoryUrl().TrimEnd('/'));
        }
    }

    public class PackageToSubmoduleConverter
    {
        private string submoduleRootPath;
        private string projectRoot;
        private string manifestPath;

        public PackageToSubmoduleConverter(string submoduleRootPath = "Submodules")
        {
            this.submoduleRootPath = submoduleRootPath;
            this.projectRoot = Path.GetDirectoryName(Application.dataPath);
            this.manifestPath = Path.Combine(projectRoot, "Packages", "manifest.json");
        }

        public static List<GitPackageInfo> GetGitPackages()
        {
            var packages = new List<GitPackageInfo>();
            var manifestPath = Path.Combine(Path.GetDirectoryName(Application.dataPath), "Packages", "manifest.json");

            if (!File.Exists(manifestPath))
            {
                UnityEngine.Debug.LogError("找不到manifest.json文件: " + manifestPath);
                return packages;
            }

            try
            {
                var manifestText = File.ReadAllText(manifestPath);
                var manifest = JObject.Parse(manifestText);
                var dependencies = manifest["dependencies"] as JObject;

                if (dependencies == null) return packages;

                foreach (var dependency in dependencies)
                {
                    var packageName = dependency.Key;
                    var dependencyValue = dependency.Value.ToString();

                    // 檢查是否為git URL
                    if (IsGitUrl(dependencyValue))
                    {
                        var gitInfo = ParseGitUrl(dependencyValue);
                        packages.Add(new GitPackageInfo
                        {
                            packageName = packageName,
                            gitUrl = gitInfo.url,
                            gitPath = gitInfo.path,
                            originalDependencyValue = dependencyValue
                        });
                    }
                }
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogError("解析manifest.json時發生錯誤: " + e.Message);
            }

            return packages;
        }

        private static bool IsGitUrl(string url)
        {
            return url.StartsWith("https://github.com/") || 
                   url.StartsWith("git@github.com:") ||
                   url.StartsWith("https://gitlab.com/") ||
                   url.Contains(".git");
        }

        private static (string url, string path) ParseGitUrl(string gitUrl)
        {
            // 解析 "https://github.com/user/repo.git?path=subfolder" 格式
            var match = Regex.Match(gitUrl, @"^([^?]+)(?:\?path=(.+))?");
            if (match.Success)
            {
                return (match.Groups[1].Value, match.Groups[2].Value);
            }
            
            return (gitUrl, string.Empty);
        }

        public bool ConvertToSubmodule(GitPackageInfo package)
        {
            try
            {
                UnityEngine.Debug.Log($"開始轉換 {package.packageName} 到 submodule...");

                // 1. 創建submodule目錄
                var submodulePath = Path.Combine(projectRoot, submoduleRootPath, package.GetSubmoduleName());
                if (!Directory.Exists(Path.GetDirectoryName(submodulePath)))
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(submodulePath));
                }

                // 2. 添加git submodule
                var repoUrl = package.GetRepositoryUrl();
                var relativePath = Path.Combine(submoduleRootPath, package.GetSubmoduleName()).Replace("\\", "/");
                
                if (!AddGitSubmodule(repoUrl, relativePath))
                {
                    UnityEngine.Debug.LogError($"添加git submodule失敗: {package.packageName}");
                    return false;
                }

                // 3. 更新manifest.json
                if (!UpdateManifestJson(package, relativePath))
                {
                    UnityEngine.Debug.LogError($"更新manifest.json失敗: {package.packageName}");
                    return false;
                }

                UnityEngine.Debug.Log($"成功轉換 {package.packageName} 到 submodule!");
                return true;
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogError($"轉換 {package.packageName} 時發生錯誤: {e.Message}");
                return false;
            }
        }

        private bool AddGitSubmodule(string repoUrl, string relativePath)
        {
            try
            {
                var startInfo = new ProcessStartInfo
                {
                    FileName = "git",
                    Arguments = $"submodule add {repoUrl} {relativePath}",
                    WorkingDirectory = projectRoot,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using (var process = Process.Start(startInfo))
                {
                    process.WaitForExit();
                    var output = process.StandardOutput.ReadToEnd();
                    var error = process.StandardError.ReadToEnd();

                    if (process.ExitCode == 0)
                    {
                        UnityEngine.Debug.Log($"Git submodule添加成功: {output}");
                        return true;
                    }
                    else
                    {
                        UnityEngine.Debug.LogError($"Git submodule添加失敗: {error}");
                        return false;
                    }
                }
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogError($"執行git命令時發生錯誤: {e.Message}");
                return false;
            }
        }

        private bool UpdateManifestJson(GitPackageInfo package, string relativePath)
        {
            try
            {
                var manifestText = File.ReadAllText(manifestPath);
                var manifest = JObject.Parse(manifestText);
                var dependencies = manifest["dependencies"] as JObject;

                if (dependencies == null) return false;

                // 計算新的file路徑
                string newDependencyValue;
                if (!string.IsNullOrEmpty(package.gitPath))
                {
                    // 如果原來有子路徑，保持子路徑結構
                    newDependencyValue = $"file:../{relativePath}/{package.gitPath}";
                }
                else
                {
                    // 沒有子路徑，直接指向submodule根目錄
                    newDependencyValue = $"file:../{relativePath}";
                }

                // 更新dependency
                dependencies[package.packageName] = newDependencyValue;

                // 寫回文件（保持格式化）
                var json = manifest.ToString(Formatting.Indented);
                File.WriteAllText(manifestPath, json);

                UnityEngine.Debug.Log($"已更新 {package.packageName} 到: {newDependencyValue}");
                return true;
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogError($"更新manifest.json時發生錯誤: {e.Message}");
                return false;
            }
        }
    }
}