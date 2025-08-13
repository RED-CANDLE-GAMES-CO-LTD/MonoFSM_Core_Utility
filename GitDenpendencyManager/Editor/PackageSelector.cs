using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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

        public string GetSelectedPackagePath() => selectedPackageJsonPath;

        public void DrawGUI()
        {
            GUILayout.BeginVertical(EditorStyles.helpBox);
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
                        packageOptions.Add($"{package.displayName} ({package.name})");
                        packagePaths.Add(packageJsonPath);
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
    }
}
