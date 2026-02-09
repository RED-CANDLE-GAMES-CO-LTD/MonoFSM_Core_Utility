using System;
using System.Collections.Generic;
using System.IO;
using MonoFSM.Core;
using UnityEditor;
using UnityEngine;

namespace MonoFSM.Utility.Editor
{
    /// <summary>
    /// Package 選擇器組件
    /// 負責顯示和管理可用的 Package 選項
    /// </summary>
    public class PackageSelector
    {
        public event Action<string> OnPackageChanged;

        private string[] availablePackageOptions;
        private string[] availablePackagePaths;
        private int selectedPackageIndex = 0;
        private string selectedPackageJsonPath = "";
        private bool _filterGitDependencies = true; // 新增：是否只顯示包含 gitDependencies 的 packages

        public string GetSelectedPackagePath() => selectedPackageJsonPath;

        public void DrawGUI()
        {
            GUILayout.BeginVertical(EditorStyles.helpBox);

            // 新增 checkbox 控制項
            GUILayout.BeginHorizontal();
            var newFilterState = EditorGUILayout.ToggleLeft("只顯示包含 gitDependencies 的 packages", _filterGitDependencies);
            if (newFilterState != _filterGitDependencies)
            {
                _filterGitDependencies = newFilterState;
                RefreshPackageOptions(); // 當篩選狀態改變時重新整理選項
            }

            GUILayout.EndHorizontal();
            
            GUILayout.BeginHorizontal();

            GUILayout.Label("選擇 Package:", GUILayout.Width(100));

            // 重新整理按鈕
            if (GUILayout.Button("🔄", GUILayout.Width(25)))
            {
                RefreshPackageOptions();
            }

            // 下拉選單
            if (availablePackageOptions != null && availablePackageOptions.Length > 0)
            {
                var newIndex = EditorGUILayout.Popup(selectedPackageIndex, availablePackageOptions);
                if (newIndex != selectedPackageIndex)
                {
                    selectedPackageIndex = newIndex;
                    selectedPackageJsonPath = availablePackagePaths[selectedPackageIndex];
                    OnPackageChanged?.Invoke(selectedPackageJsonPath);
                }
            }
            else
            {
                GUILayout.Label("沒有可用的 packages", EditorStyles.centeredGreyMiniLabel);
            }

            GUILayout.EndHorizontal();

            // 顯示選中的路徑
            if (!string.IsNullOrEmpty(selectedPackageJsonPath))
            {
                GUILayout.Label($"路徑: {selectedPackageJsonPath}", EditorStyles.miniLabel);

                // // 顯示 package.json 檔案的 Object field
                // if (File.Exists(selectedPackageJsonPath))
                // {
                //     // 將絕對路徑轉換為相對於專案的路徑
                //     var projectPath = Application.dataPath.Replace("/Assets", "");
                //     var relativePath = selectedPackageJsonPath.Replace(projectPath + "/", "");
                //
                //     var packageJsonAsset = AssetDatabase.LoadAssetAtPath<TextAsset>(relativePath);
                //
                //     GUILayout.BeginHorizontal();
                //     GUILayout.Label("Package.json:", GUILayout.Width(100));
                //     EditorGUILayout.ObjectField(packageJsonAsset, typeof(TextAsset), false);
                //     GUILayout.EndHorizontal();
                // }
            }

            GUILayout.EndVertical();
            GUILayout.Space(10);
        }

        public void RefreshPackageOptions()
        {
            var packageOptions = new List<string>();
            var packagePaths = new List<string>();

            try
            {
                // 取得所有 packages
                var allPackages = PackageHelper.GetAllPackages();

                foreach (var package in allPackages)
                {
                    // 過濾 Unity 內部 packages
                    if (IsUnityBuiltInPackage(package.name))
                        continue;

                    string packageJsonPath = null;

                    //TODO:太多不重要的package被撈出來了? 想要編輯時怎麼辦呢？ 要帶有gitDependency的package才需要被列出嗎？
                    if (package.source == UnityEditor.PackageManager.PackageSource.Local)
                    {
                        // 本地 package
                        var packageFullPath = PackageHelper.GetPackageFullPath(
                            $"Packages/{package.name}"
                        );
                        if (!string.IsNullOrEmpty(packageFullPath))
                        {
                            packageJsonPath = Path.Combine(packageFullPath, "package.json");
                        }
                    }
                    else
                    {
                        // Git 或 Registry packages
                        if (!string.IsNullOrEmpty(package.resolvedPath))
                        {
                            packageJsonPath = Path.Combine(package.resolvedPath, "package.json");
                        }
                    }

                    if (!string.IsNullOrEmpty(packageJsonPath) && File.Exists(packageJsonPath))
                    {
                        // 如果啟用了 gitDependencies 篩選，則檢查當前 package 是否包含 gitDependencies
                        if (!_filterGitDependencies || PackageHasGitDependencies(package))
                        {
                            packageOptions.Add($"{package.displayName} ({package.name})");
                            packagePaths.Add(packageJsonPath);
                        }
                    }
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"[PackageSelector] 取得 package 選項時發生錯誤: {ex.Message}");
            }

            availablePackageOptions = packageOptions.ToArray();
            availablePackagePaths = packagePaths.ToArray();

            // 確保選擇的索引有效
            if (selectedPackageIndex >= availablePackageOptions.Length)
            {
                selectedPackageIndex = 0;
            }

            // 更新選中的路徑
            if (
                availablePackagePaths.Length > 0
                && selectedPackageIndex < availablePackagePaths.Length
            )
            {
                selectedPackageJsonPath = availablePackagePaths[selectedPackageIndex];
            }
        }

        /// <summary>
        /// 檢查是否為 Unity 內建 package
        /// </summary>
        private bool IsUnityBuiltInPackage(string packageName)
        {
            return packageName.StartsWith("com.unity.modules.")
                || packageName.StartsWith("com.unity.")
                || packageName == "";
        }

        /// <summary>
        /// 檢查 package 是否包含 gitDependencies
        /// </summary>
        private bool PackageHasGitDependencies(UnityEditor.PackageManager.PackageInfo package)
        {
            // 檢查 package.json 中是否有 gitDependencies
            var packageJsonPath = Path.Combine(package.resolvedPath, "package.json");
            if (File.Exists(packageJsonPath))
            {
                var jsonText = File.ReadAllText(packageJsonPath);
                return jsonText.Contains("\"gitDependencies\"");
            }

            return false;
        }
    }
}
