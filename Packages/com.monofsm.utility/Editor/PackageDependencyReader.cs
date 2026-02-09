using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using MonoFSM.Core;
#if UNITY_EDITOR
using UnityEditor.PackageManager;
#endif

namespace MonoFSM.Utility.Editor
{
    /// <summary>
    /// 從 Package Manager 讀取依賴資訊的輔助類
    /// 利用 PackageHelper 的現有功能，提供自動填入功能
    /// </summary>
    public static class PackageDependencyReader
    {
        private static Dictionary<string, DependencyInfo> _cachedDependencies;
        private static bool _cacheInitialized;

        /// <summary>
        /// 依賴資訊結構
        /// </summary>
        public class DependencyInfo
        {
            public string PackageName { get; set; }
            public string Url { get; set; }
            public string Version { get; set; }
            public DependencyType Type { get; set; }
            public string RegistryName { get; set; }
            public string RegistryUrl { get; set; }
            public string Scope { get; set; }
            public PackageSource Source { get; set; }
            public string ResolvedPath { get; set; }
        }

        /// <summary>
        /// 依賴類型
        /// </summary>
        public enum DependencyType
        {
            Git,
            Registry,
            Local,
            Embedded,
            BuiltIn
        }

        /// <summary>
        /// 初始化並從 Package Manager 讀取所有依賴資訊
        /// </summary>
        public static void InitializeDependencyCache()
        {
            if (_cacheInitialized)
                return;

            _cachedDependencies = new Dictionary<string, DependencyInfo>();
            _cacheInitialized = true;
            try
            {
#if UNITY_EDITOR
                // 使用 PackageHelper 的現有功能獲取 package 列表
                var packages = GetAllPackages();

                foreach (var package in packages)
                {
                    var depInfo = CreateDependencyInfo(package);
                    _cachedDependencies[package.name] = depInfo;
                }


                Debug.Log($"[PackageDependencyReader] 已載入 {_cachedDependencies.Count} 個依賴項目");
#endif
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
            }
        }

#if UNITY_EDITOR
        /// <summary>
        /// 獲取所有 packages（利用 PackageHelper 的邏輯）
        /// </summary>
        private static List<PackageInfo> GetAllPackages()
        {
            var listRequest = Client.List(true, false);

            // 等待請求完成
            while (!listRequest.IsCompleted)
            {
                System.Threading.Thread.Sleep(10);
            }

            if (listRequest.Status == StatusCode.Success)
            {
                return listRequest.Result.ToList();
            }
            else
            {
                Debug.LogWarning($"[PackageDependencyReader] 無法取得套件清單: {listRequest.Error?.message}");
                return new List<PackageInfo>();
            }
        }

        /// <summary>
        /// 從 PackageInfo 創建 DependencyInfo
        /// </summary>
        private static DependencyInfo CreateDependencyInfo(PackageInfo package)
        {
            var info = new DependencyInfo
            {
                PackageName = package.name,
                Version = package.version,
                Source = package.source,
                ResolvedPath = package.resolvedPath
            };

            // Debug.Log("packageInfo:" + JsonUtility.ToJson(package));

            // 根據 source 類型設定依賴類型和 URL
            switch (package.source)
            {
                case PackageSource.Git:
                    info.Type = DependencyType.Git;
                    info.Url = package.packageId.Split("@")[1];
                    Debug.Log($"[PackageDependencyReader] Git package URL: {info.Url}");
                    // info.Url = package.repository.url; 
                    break;

                case PackageSource.Local:
                    info.Type = DependencyType.Local;
                    info.Url = $"file:{package.resolvedPath}";
                    break;

                case PackageSource.Registry:
                    info.Type = DependencyType.Registry;
                    // Registry packages 沒有直接的 URL，但可以從 registry 資訊推斷
                    break;

                case PackageSource.Embedded:
                    info.Type = DependencyType.Embedded;
                    break;

                case PackageSource.BuiltIn:
                    info.Type = DependencyType.BuiltIn;
                    break;

                default:
                    info.Type = DependencyType.Registry;
                    break;
            }

            return info;
        }
#endif

        /// <summary>
        /// 獲取指定 package 的依賴資訊
        /// </summary>
        public static DependencyInfo GetDependencyInfo(string packageName)
        {
            InitializeDependencyCache();
            return _cachedDependencies.TryGetValue(packageName, out var info) ? info : null;
        }

        /// <summary>
        /// 獲取所有已快取的依賴資訊
        /// </summary>
        public static Dictionary<string, DependencyInfo> GetAllDependencies()
        {
            InitializeDependencyCache();
            return new Dictionary<string, DependencyInfo>(_cachedDependencies);
        }

        /// <summary>
        /// 檢查是否有類似的 package name (模糊匹配)
        /// </summary>
        public static List<DependencyInfo> FindSimilarPackages(string packageName)
        {
            InitializeDependencyCache();
            var results = new List<DependencyInfo>();

            foreach (var kvp in _cachedDependencies)
            {
                var existingName = kvp.Key;
                var info = kvp.Value;

                // 精確匹配
                if (existingName == packageName)
                {
                    results.Insert(0, info); // 放在最前面
                    continue;
                }

                //有需要嗎...?
                // // 模糊匹配：檢查是否包含相似的部分
                // if (IsPackageNameSimilar(packageName, existingName))
                // {
                //     results.Add(info);
                // }
            }

            return results;
        }

        /// <summary>
        /// 清除快取，強制重新讀取
        /// </summary>
        public static void ClearCache()
        {
            _cacheInitialized = false;
            _cachedDependencies?.Clear();
        }

        /// <summary>
        /// 檢查兩個 package 名稱是否相似
        /// </summary>
        private static bool IsPackageNameSimilar(string target, string existing)
        {
            // 簡單的相似度檢查
            // 1. 檢查是否有共同的 namespace
            var targetParts = target.Split('.');
            var existingParts = existing.Split('.');

            if (targetParts.Length >= 2 && existingParts.Length >= 2)
            {
                // 檢查前兩個部分是否相同 (例如: com.unity.*)
                if (targetParts[0] == existingParts[0] && targetParts[1] == existingParts[1])
                {
                    return true;
                }
            }

            // 2. 檢查是否包含相同的關鍵字
            var targetKeywords = target.Split('.', '-', '_').Where(s => s.Length > 2).ToArray();
            var existingKeywords = existing.Split('.', '-', '_').Where(s => s.Length > 2).ToArray();

            foreach (var targetKeyword in targetKeywords)
            {
                foreach (var existingKeyword in existingKeywords)
                {
                    if (string.Equals(targetKeyword, existingKeyword, StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }
                }
            }

            return false;
        }
    }
}