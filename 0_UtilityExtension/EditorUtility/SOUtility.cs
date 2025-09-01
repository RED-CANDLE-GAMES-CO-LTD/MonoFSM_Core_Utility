using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace MonoFSM.Core.Editor.Utility
{
    /// <summary>
    ///     ScriptableObject 搜尋和管理的通用工具類
    ///     提供統一的 ScriptableObject 資產搜尋、過濾和驗證功能
    /// </summary>
    public static class SOUtility
    {
        /// <summary>
        ///     獲取過濾後的 ScriptableObject 資產
        /// </summary>
        /// <typeparam name="T">ScriptableObject 類型</typeparam>
        /// <param name="searchFilter">Unity AssetDatabase 搜尋過濾器，如果為 null 則使用預設過濾器</param>
        /// <param name="validator">自定義驗證函數，如果為 null 則不進行額外驗證</param>
        /// <returns>符合條件的資產列表</returns>
        public static List<T> GetFilteredAssets<T>(string searchFilter = null,
            Func<T, bool> validator = null) where T : ScriptableObject
        {
            var sw = Stopwatch.StartNew();

            // 如果沒有提供搜尋過濾器，使用類型名稱作為預設過濾器
            if (string.IsNullOrEmpty(searchFilter)) searchFilter = $"t:{typeof(T).Name}";

            var guids = AssetDatabase.FindAssets(searchFilter);
            var results = new List<T>(guids.Length);

            Debug.Log(
                $"[ScriptableObjectUtility] GetFilteredAssets: Found {guids.Length} assets matching filter: {searchFilter}");

            for (var i = 0; i < guids.Length; i++)
            {
                var path = AssetDatabase.GUIDToAssetPath(guids[i]);
                var asset = AssetDatabase.LoadAssetAtPath<T>(path);

                if (asset != null)
                    // 如果有提供驗證函數，則進行額外驗證
                    if (validator == null || validator(asset))
                        results.Add(asset);
            }

            sw.Stop();
            Debug.Log(
                $"[ScriptableObjectUtility] GetFilteredAssets: Found {results.Count} valid assets in {sw.ElapsedMilliseconds} ms.");

            return results;
        }

        /// <summary>
        ///     根據名稱搜尋 ScriptableObject 資產
        /// </summary>
        /// <typeparam name="T">ScriptableObject 類型</typeparam>
        /// <param name="name">要搜尋的名稱</param>
        /// <param name="exactMatch">是否進行精確匹配，false 表示模糊搜尋</param>
        /// <returns>符合條件的資產列表</returns>
        public static List<T> FindAssetsByName<T>(string name, bool exactMatch = false)
            where T : ScriptableObject
        {
            if (string.IsNullOrEmpty(name))
                return new List<T>();

            // 建立搜尋過濾器
            var searchFilter = exactMatch
                ? $"{name} t:{typeof(T).Name}"
                : $"t:{typeof(T).Name}";

            var validator = exactMatch
                ? new Func<T, bool>(asset =>
                    asset.name.Equals(name, StringComparison.OrdinalIgnoreCase))
                : new Func<T, bool>(asset =>
                    asset.name.IndexOf(name, StringComparison.OrdinalIgnoreCase) >= 0);

            return GetFilteredAssets(searchFilter, validator);
        }

        /// <summary>
        ///     根據精確名稱找到第一個匹配的 ScriptableObject 資產
        /// </summary>
        /// <typeparam name="T">ScriptableObject 類型</typeparam>
        /// <param name="exactName">精確的名稱</param>
        /// <returns>找到的資產，如果沒找到則返回 null</returns>
        public static T FindAssetByExactName<T>(string exactName) where T : ScriptableObject
        {
            return FindAssetsByName<T>(exactName, true).FirstOrDefault();
        }

        /// <summary>
        ///     獲取指定類型的所有 ScriptableObject 資產
        /// </summary>
        /// <typeparam name="T">ScriptableObject 類型</typeparam>
        /// <returns>所有該類型的資產</returns>
        public static IEnumerable<T> GetAllAssetsOfType<T>() where T : ScriptableObject
        {
            return GetFilteredAssets<T>();
        }

        /// <summary>
        ///     獲取指定類型的所有 ScriptableObject 資產，按名稱分組
        /// </summary>
        /// <typeparam name="T">ScriptableObject 類型</typeparam>
        /// <returns>按名稱分組的資產字典</returns>
        public static Dictionary<string, List<T>> GetAssetsGroupedByName<T>()
            where T : ScriptableObject
        {
            return GetAllAssetsOfType<T>()
                .GroupBy(asset => asset.name)
                .ToDictionary(group => group.Key, group => group.ToList());
        }

        /// <summary>
        ///     檢查指定名稱的 ScriptableObject 是否存在
        /// </summary>
        /// <typeparam name="T">ScriptableObject 類型</typeparam>
        /// <param name="name">要檢查的名稱</param>
        /// <returns>是否存在</returns>
        public static bool AssetExistsByName<T>(string name) where T : ScriptableObject
        {
            return FindAssetByExactName<T>(name) != null;
        }
    }
}
