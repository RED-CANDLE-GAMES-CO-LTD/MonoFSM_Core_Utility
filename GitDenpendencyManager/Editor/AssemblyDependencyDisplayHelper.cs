using System;
using System.Collections.Generic;
using MonoFSM.Core;
using UnityEditor;
using UnityEngine;

namespace MonoFSM.Utility.Editor
{
    /// <summary>
    /// Assembly Dependency 顯示輔助工具
    /// 提供各種 UI 顯示元件和格式化功能
    /// </summary>
    public class AssemblyDependencyDisplayHelper
    {
        private GUIStyle badgeStyle;
        private GUIStyle progressBarStyle;
        private bool stylesInitialized = false;

        private void InitializeStyles()
        {
            if (stylesInitialized)
                return;

            badgeStyle = new GUIStyle(EditorStyles.miniButton)
            {
                fontSize = 10,
                padding = new RectOffset(8, 8, 2, 2),
                margin = new RectOffset(2, 2, 2, 2),
            };

            progressBarStyle = new GUIStyle(EditorStyles.helpBox)
            {
                padding = new RectOffset(4, 4, 2, 2),
            };

            stylesInitialized = true;
        }

        /// <summary>
        /// 繪製狀態標籤
        /// </summary>
        public void DrawStatusBadge(string label, int count, Color color)
        {
            InitializeStyles();

            var originalColor = GUI.backgroundColor;
            GUI.backgroundColor = color;

            GUILayout.BeginHorizontal();
            GUILayout.Label($"{label}: ", GUILayout.Width(120));

            if (GUILayout.Button($"{count}", badgeStyle, GUILayout.Width(40)))
            {
                // 點擊時可以做一些動作，例如聚焦到該項目
            }

            GUILayout.EndHorizontal();
            GUI.backgroundColor = originalColor;
        }

        /// <summary>
        /// 繪製缺失依賴項目
        /// </summary>
        public void DrawMissingDependencyItem(
            AssemblyDependencyAnalyzer.ReferencedPackageInfo missing,
            Dictionary<string, string> gitUrlInputs,
            Action<AssemblyDependencyAnalyzer.ReferencedPackageInfo> onUpdateCallback
        )
        {
            GUILayout.BeginHorizontal();

            // 狀態標記
            string statusIcon = GetStatusIcon(missing);
            var statusColor = GetStatusColor(missing);

            var originalColor = GUI.color;
            GUI.color = statusColor;
            GUILayout.Label(statusIcon, GUILayout.Width(30));
            GUI.color = originalColor;

            // Package 資訊
            GUILayout.BeginVertical();

            GUILayout.BeginHorizontal();
            GUILayout.Label(missing.packageName, EditorStyles.boldLabel, GUILayout.Width(180));

            if (!string.IsNullOrEmpty(missing.assemblyName))
            {
                GUILayout.Label(
                    $"({missing.assemblyName})",
                    EditorStyles.miniLabel,
                    GUILayout.Width(120)
                );
            }
            GUILayout.EndHorizontal();

            // 安裝狀態詳細資訊
            DrawInstallationDetails(missing);

            GUILayout.EndVertical();

            // Git URL 狀態或輸入框
            DrawGitUrlSection(missing, gitUrlInputs, onUpdateCallback);

            GUILayout.EndHorizontal();

            // 如果是 local package，顯示提示
            if (
                missing.isLocalPackage
                && (string.IsNullOrEmpty(missing.gitUrl) || !missing.hasGitUrl)
            )
            {
                GUILayout.BeginHorizontal();
                GUILayout.Space(30);
                GUILayout.Label(
                    "💡 提示：如果不提供 Git URL，此 package 需要手動安裝為 local package",
                    EditorStyles.miniLabel
                );
                GUILayout.EndHorizontal();
            }

            GUILayout.Space(3);
        }

        /// <summary>
        /// 繪製已存在依賴項目
        /// </summary>
        public void DrawExistingDependencyItem(
            AssemblyDependencyAnalyzer.ReferencedPackageInfo existing
        )
        {
            GUILayout.BeginHorizontal();

            GUILayout.Label("✅", GUILayout.Width(30));
            GUILayout.Label(existing.packageName, GUILayout.Width(200));

            if (!string.IsNullOrEmpty(existing.assemblyName))
            {
                GUILayout.Label(
                    $"({existing.assemblyName})",
                    EditorStyles.miniLabel,
                    GUILayout.Width(120)
                );
            }

            GUILayout.FlexibleSpace();

            if (!string.IsNullOrEmpty(existing.gitUrl))
            {
                if (GUILayout.Button("📋", GUILayout.Width(25)))
                {
                    EditorGUIUtility.systemCopyBuffer = existing.gitUrl;
                    Debug.Log($"已複製 Git URL: {existing.gitUrl}");
                }

                if (
                    GUILayout.Button(
                        existing.gitUrl,
                        EditorStyles.linkLabel,
                        GUILayout.MaxWidth(300)
                    )
                )
                {
                    Application.OpenURL(existing.gitUrl);
                }
            }
            else
            {
                GUILayout.Label("(Registry Package)", EditorStyles.miniLabel);
            }

            GUILayout.EndHorizontal();
        }

        /// <summary>
        /// 繪製 Assembly 詳細項目
        /// </summary>
        public void DrawAssemblyDetailItem(
            AssemblyDependencyAnalyzer.AssemblyDependencyInfo assembly
        )
        {
            GUILayout.BeginVertical(EditorStyles.helpBox);

            GUILayout.BeginHorizontal();
            var statusIcon = assembly.hasExternalReferences ? "↗️" : "○";
            var statusColor = assembly.hasExternalReferences ? Color.yellow : Color.green;

            var originalColor = GUI.color;
            GUI.color = statusColor;
            GUILayout.Label(statusIcon, GUILayout.Width(20));
            GUI.color = originalColor;

            GUILayout.Label(assembly.assemblyName, EditorStyles.boldLabel);
            GUILayout.FlexibleSpace();
            GUILayout.Label($"{assembly.referencedGUIDs.Count} refs", EditorStyles.miniLabel);

            // 添加路徑資訊
            if (GUILayout.Button("📁", GUILayout.Width(25)))
            {
                EditorGUIUtility.PingObject(
                    AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(assembly.assemblyPath)
                );
            }

            GUILayout.EndHorizontal();

            if (assembly.hasExternalReferences && assembly.referencedPackages.Count > 0)
            {
                GUILayout.Space(5);
                foreach (var refPackage in assembly.referencedPackages)
                {
                    GUILayout.BeginHorizontal();
                    GUILayout.Space(20);

                    // 依賴狀態圖示
                    var depIcon = refPackage.isLocalPackage ? "🟡" : "🟢";
                    GUILayout.Label(depIcon, GUILayout.Width(20));

                    GUILayout.Label($"→ {refPackage.packageName}", EditorStyles.miniLabel);

                    if (!string.IsNullOrEmpty(refPackage.assemblyName))
                    {
                        GUILayout.Label($"({refPackage.assemblyName})", EditorStyles.miniLabel);
                    }

                    GUILayout.EndHorizontal();
                }
            }

            GUILayout.EndVertical();
            GUILayout.Space(2);
        }

        private void DrawInstallationDetails(
            AssemblyDependencyAnalyzer.ReferencedPackageInfo package
        )
        {
            GUILayout.BeginHorizontal();

            if (package.isLocalPackage)
            {
                GUILayout.Label("📁 Local Package", EditorStyles.miniLabel);
            }
            else
            {
                GUILayout.Label("🌐 Remote Package", EditorStyles.miniLabel);
            }

            if (!string.IsNullOrEmpty(package.packagePath))
            {
                GUILayout.Label($"路徑: {package.packagePath}", EditorStyles.miniLabel);
            }

            GUILayout.EndHorizontal();
        }

        private void DrawGitUrlSection(
            AssemblyDependencyAnalyzer.ReferencedPackageInfo missing,
            Dictionary<string, string> gitUrlInputs,
            Action<AssemblyDependencyAnalyzer.ReferencedPackageInfo> onUpdateCallback
        )
        {
            GUILayout.BeginVertical(GUILayout.Width(350));

            if (!string.IsNullOrEmpty(missing.gitUrl) && missing.hasGitUrl)
            {
                // 已有 Git URL，顯示為只讀
                GUILayout.Label("Git URL:", EditorStyles.miniLabel);
                GUI.enabled = false;
                GUILayout.TextField(missing.gitUrl);
                GUI.enabled = true;

                // 添加按鈕
                if (GUILayout.Button("添加到 package.json", GUILayout.Height(20)))
                {
                    onUpdateCallback?.Invoke(missing);
                }
            }
            else
            {
                // 需要輸入 Git URL
                GUILayout.Label("請輸入 Git URL:", EditorStyles.miniLabel);

                if (!gitUrlInputs.ContainsKey(missing.packageName))
                {
                    gitUrlInputs[missing.packageName] = "";
                }

                gitUrlInputs[missing.packageName] = GUILayout.TextField(
                    gitUrlInputs[missing.packageName]
                );

                // 添加按鈕，只有在有輸入時才啟用
                GUI.enabled = !string.IsNullOrWhiteSpace(gitUrlInputs[missing.packageName]);
                if (GUILayout.Button("添加到 package.json", GUILayout.Height(20)))
                {
                    missing.gitUrl = gitUrlInputs[missing.packageName];
                    missing.hasGitUrl = IsGitUrl(gitUrlInputs[missing.packageName]);
                    onUpdateCallback?.Invoke(missing);
                }
                GUI.enabled = true;
            }

            GUILayout.EndVertical();
        }

        private string GetStatusIcon(AssemblyDependencyAnalyzer.ReferencedPackageInfo package)
        {
            if (!string.IsNullOrEmpty(package.gitUrl) && package.hasGitUrl)
                return "🟢";
            if (package.isLocalPackage)
                return "🟡";
            return "🔴";
        }

        private Color GetStatusColor(AssemblyDependencyAnalyzer.ReferencedPackageInfo package)
        {
            if (!string.IsNullOrEmpty(package.gitUrl) && package.hasGitUrl)
                return Color.green;
            if (package.isLocalPackage)
                return Color.yellow;
            return Color.red;
        }

        private bool IsGitUrl(string url)
        {
            if (string.IsNullOrEmpty(url))
                return false;

            return url.StartsWith("https://github.com/")
                || url.StartsWith("git@github.com:")
                || url.StartsWith("git://")
                || url.Contains(".git");
        }
    }
}
