using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

namespace MonoFSM.Utility.Editor
{
    /// <summary>
    /// 主專案 Packages/manifest.json 管理器
    /// 負責處理 scopedRegistries 和主專案的 dependencies
    /// </summary>
    public static class ManifestManager
    {
        private static readonly string ManifestPath = "Packages/manifest.json";

        /// <summary>
        /// 添加 scoped registry 到主專案和目標 package 的 manifest.json
        /// </summary>
        public static void AddScopedRegistry(
            string packageName,
            string registryName,
            string registryUrl,
            string scope,
            string version,
            string targetPackageJsonPath
        )
        {
            try
            {
                // 1. 更新主專案的 manifest.json
                UpdateMainProjectManifest(packageName, registryName, registryUrl, scope, version);

                // 2. 將 scopedRegistries 複製到目標 package.json
                AddScopedRegistryToPackageJson(
                    targetPackageJsonPath,
                    registryName,
                    registryUrl,
                    scope
                );

                Debug.Log(
                    $"[ManifestManager] 已完成 {packageName}@{version} 的 scoped registry 設定"
                );

                // 顯示成功訊息
                EditorUtility.DisplayDialog(
                    "Scoped Registry 設定完成",
                    $"已完成 {packageName}@{version} 的設定！\n\n"
                        + $"✅ 已添加到主專案 Packages/manifest.json\n"
                        + $"✅ 已複製 scopedRegistry 到 package.json\n\n"
                        + $"Registry: {registryName}\n"
                        + $"URL: {registryUrl}\n"
                        + $"Scope: {scope}\n\n"
                        + "Unity Package Manager 將自動重新載入。",
                    "確定"
                );
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[ManifestManager] 設定 scoped registry 時發生錯誤: {ex.Message}");
                EditorUtility.DisplayDialog(
                    "錯誤",
                    $"設定 scoped registry 失敗:\n{ex.Message}",
                    "確定"
                );
            }
        }

        /// <summary>
        /// 更新主專案的 Packages/manifest.json
        /// </summary>
        private static void UpdateMainProjectManifest(
            string packageName,
            string registryName,
            string registryUrl,
            string scope,
            string version
        )
        {
            if (!File.Exists(ManifestPath))
            {
                Debug.LogError($"[ManifestManager] 找不到 {ManifestPath}");
                return;
            }

            var manifestJson = JObject.Parse(File.ReadAllText(ManifestPath));

            // 添加到 dependencies
            var dependencies = manifestJson["dependencies"] as JObject;
            if (dependencies == null)
            {
                manifestJson["dependencies"] = dependencies = new JObject();
            }
            dependencies[packageName] = version;

            // 處理 scopedRegistries
            var scopedRegistries = manifestJson["scopedRegistries"] as JArray;
            if (scopedRegistries == null)
            {
                manifestJson["scopedRegistries"] = scopedRegistries = new JArray();
            }

            // 檢查是否已存在相同的 scoped registry
            JObject existingRegistry = null;
            foreach (var registry in scopedRegistries)
            {
                if (
                    registry["name"]?.ToString() == registryName
                    && registry["url"]?.ToString() == registryUrl
                )
                {
                    existingRegistry = registry as JObject;
                    break;
                }
            }

            if (existingRegistry == null)
            {
                // 創建新的 scoped registry
                var newRegistry = new JObject
                {
                    ["name"] = registryName,
                    ["url"] = registryUrl,
                    ["scopes"] = new JArray { scope },
                };
                scopedRegistries.Add(newRegistry);
                Debug.Log($"[ManifestManager] 已添加新的 scoped registry: {registryName}");
            }
            else
            {
                // 確保 scope 在現有 registry 中
                var existingScopes = existingRegistry["scopes"] as JArray;
                bool scopeExists = existingScopes.Any(s => s.ToString() == scope);

                if (!scopeExists)
                {
                    existingScopes.Add(scope);
                    Debug.Log($"[ManifestManager] 已將 scope {scope} 添加到現有 registry");
                }
            }

            // 寫回檔案
            File.WriteAllText(
                ManifestPath,
                manifestJson.ToString(Newtonsoft.Json.Formatting.Indented)
            );

            // 刷新 Package Manager
            AssetDatabase.Refresh();
        }

        /// <summary>
        /// 將 scopedRegistries 添加到目標 package.json
        /// </summary>
        private static void AddScopedRegistryToPackageJson(
            string packageJsonPath,
            string registryName,
            string registryUrl,
            string scope
        )
        {
            if (!File.Exists(packageJsonPath))
            {
                Debug.LogError($"[ManifestManager] 找不到目標 package.json: {packageJsonPath}");
                return;
            }

            var packageJson = JObject.Parse(File.ReadAllText(packageJsonPath));

            // 添加 scopedRegistries 到 package.json
            var scopedRegistries = packageJson["scopedRegistries"] as JArray;
            if (scopedRegistries == null)
            {
                packageJson["scopedRegistries"] = scopedRegistries = new JArray();
            }

            // 檢查是否已存在
            bool registryExists = scopedRegistries.Any(registry =>
                registry["name"]?.ToString() == registryName
                && registry["url"]?.ToString() == registryUrl
            );

            if (!registryExists)
            {
                var newRegistry = new JObject
                {
                    ["name"] = registryName,
                    ["url"] = registryUrl,
                    ["scopes"] = new JArray { scope },
                };
                scopedRegistries.Add(newRegistry);

                // 寫回檔案
                File.WriteAllText(
                    packageJsonPath,
                    packageJson.ToString(Newtonsoft.Json.Formatting.Indented)
                );
                Debug.Log(
                    $"[ManifestManager] 已將 scopedRegistry 複製到 package.json: {packageJsonPath}"
                );
            }
        }

        /// <summary>
        /// 檢查指定 scope 是否已在 manifest.json 中設定
        /// </summary>
        public static bool IsScopeRegistered(string scope)
        {
            try
            {
                if (!File.Exists(ManifestPath))
                    return false;

                var manifestJson = JObject.Parse(File.ReadAllText(ManifestPath));
                var scopedRegistries = manifestJson["scopedRegistries"] as JArray;

                if (scopedRegistries == null)
                    return false;

                foreach (var registry in scopedRegistries)
                {
                    var scopes = registry["scopes"] as JArray;
                    if (scopes != null)
                    {
                        foreach (var registeredScope in scopes)
                        {
                            if (registeredScope.ToString() == scope)
                                return true;
                        }
                    }
                }

                return false;
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"[ManifestManager] 檢查 scope 時發生錯誤: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 獲取 manifest.json 的路徑
        /// </summary>
        public static string GetManifestPath()
        {
            return ManifestPath;
        }
    }
}
