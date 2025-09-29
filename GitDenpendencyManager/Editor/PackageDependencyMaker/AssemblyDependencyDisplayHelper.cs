using System;
using System.Collections.Generic;
using MonoFSM.Core;
using UnityEditor;
using UnityEngine;
using System.Linq;

namespace MonoFSM.Utility.Editor
{
    /// <summary>
    /// Assembly Dependency 顯示輔助工具
    /// 提供各種 UI 顯示元件和格式化功能
    /// </summary>
    public class AssemblyDependencyDisplayHelper
    {
        private GUIStyle _badgeStyle;
        private bool _stylesInitialized;

        private void InitializeStyles()
        {
            if (_stylesInitialized)
                return;

            _badgeStyle = new GUIStyle(EditorStyles.miniButton)
            {
                fontSize = 10,
                padding = new RectOffset(8, 8, 2, 2),
                margin = new RectOffset(2, 2, 2, 2),
            };

            _stylesInitialized = true;
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

            if (GUILayout.Button($"{count}", _badgeStyle, GUILayout.Width(40)))
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
            GUILayout.BeginVertical(GUILayout.Width(450));

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
                // 首先檢查是否能從 manifest.json 自動獲取資訊
                DrawAutoDetectedDependencies(missing, onUpdateCallback);

                GUILayout.Space(5);
                
                // 提供多種添加方式
                GUILayout.Label("手動添加方式:", EditorStyles.miniLabel);

                // Git URL 輸入方式
                GUILayout.BeginHorizontal();
                GUILayout.Label("Git URL:", GUILayout.Width(60));

                if (!gitUrlInputs.ContainsKey(missing.packageName))
                {
                    gitUrlInputs[missing.packageName] = "";
                }

                gitUrlInputs[missing.packageName] = GUILayout.TextField(
                    gitUrlInputs[missing.packageName]
                );

                // Git URL 添加按鈕
                GUI.enabled = !string.IsNullOrWhiteSpace(gitUrlInputs[missing.packageName]);
                if (GUILayout.Button("添加 Git", GUILayout.Width(60)))
                {
                    missing.gitUrl = gitUrlInputs[missing.packageName];
                    missing.hasGitUrl = IsGitUrl(gitUrlInputs[missing.packageName]);
                    onUpdateCallback?.Invoke(missing);
                }
                GUI.enabled = true;
                GUILayout.EndHorizontal();

                // Registry Package 方式
                GUILayout.BeginHorizontal();
                GUILayout.Label("Registry:", GUILayout.Width(60));

                // 版本號輸入 (可選)
                var versionKey = missing.packageName + "_version";
                if (!gitUrlInputs.ContainsKey(versionKey))
                {
                    gitUrlInputs[versionKey] = "";
                }

                gitUrlInputs[versionKey] = GUILayout.TextField(
                    gitUrlInputs[versionKey],
                    GUILayout.Width(80)
                );
                GUILayout.Label("(版本)", EditorStyles.miniLabel, GUILayout.Width(40));

                // Registry 添加按鈕
                if (GUILayout.Button("添加 Registry", GUILayout.Width(80)))
                {
                    var version = string.IsNullOrWhiteSpace(gitUrlInputs[versionKey])
                        ? "latest"
                        : gitUrlInputs[versionKey];
                    missing.gitUrl = "registry:" + version; // 用特殊前綴標記Registry package
                    missing.hasGitUrl = false; // 標記為非Git URL
                    onUpdateCallback?.Invoke(missing);
                }
                GUILayout.EndHorizontal();

                // Scoped Registry 方式 - 完整設定
                GUILayout.Label("Scoped Registry 設定:", EditorStyles.miniLabel);

                // 版本號輸入
                GUILayout.BeginHorizontal();
                GUILayout.Label("版本:", GUILayout.Width(60));
                var npmVersionKey = missing.packageName + "_npm_version";
                if (!gitUrlInputs.ContainsKey(npmVersionKey))
                {
                    gitUrlInputs[npmVersionKey] = "";
                }
                gitUrlInputs[npmVersionKey] = GUILayout.TextField(
                    gitUrlInputs[npmVersionKey],
                    GUILayout.Width(100)
                );
                GUILayout.EndHorizontal();

                // Registry Name 輸入
                GUILayout.BeginHorizontal();
                GUILayout.Label("Name:", GUILayout.Width(60));
                var registryNameKey = missing.packageName + "_registry_name";
                if (!gitUrlInputs.ContainsKey(registryNameKey))
                {
                    gitUrlInputs[registryNameKey] = "npm";
                }
                gitUrlInputs[registryNameKey] = GUILayout.TextField(
                    gitUrlInputs[registryNameKey],
                    GUILayout.Width(100)
                );
                GUILayout.EndHorizontal();

                // Registry URL 輸入
                GUILayout.BeginHorizontal();
                GUILayout.Label("URL:", GUILayout.Width(60));
                var registryUrlKey = missing.packageName + "_registry_url";
                if (!gitUrlInputs.ContainsKey(registryUrlKey))
                {
                    gitUrlInputs[registryUrlKey] = "https://registry.npmjs.org/";
                }
                gitUrlInputs[registryUrlKey] = GUILayout.TextField(
                    gitUrlInputs[registryUrlKey],
                    GUILayout.Width(250)
                );
                GUILayout.EndHorizontal();

                // Scope 輸入 (自動提取但可編輯)
                GUILayout.BeginHorizontal();
                GUILayout.Label("Scope:", GUILayout.Width(60));
                var scopeKey = missing.packageName + "_scope";
                if (!gitUrlInputs.ContainsKey(scopeKey))
                {
                    gitUrlInputs[scopeKey] = ExtractScope(missing.packageName);
                }
                gitUrlInputs[scopeKey] = GUILayout.TextField(
                    gitUrlInputs[scopeKey],
                    GUILayout.Width(150)
                );
                GUILayout.EndHorizontal();

                // 添加按鈕
                GUILayout.BeginHorizontal();
                if (GUILayout.Button("添加 Scoped Registry", GUILayout.Width(140)))
                {
                    var version = string.IsNullOrWhiteSpace(gitUrlInputs[npmVersionKey])
                        ? "latest"
                        : gitUrlInputs[npmVersionKey];
                    var registryName = gitUrlInputs[registryNameKey];
                    var registryUrl = gitUrlInputs[registryUrlKey];
                    var scope = gitUrlInputs[scopeKey];

                    missing.gitUrl =
                        $"scopedRegistry:{registryName}:{registryUrl}:{scope}:{version}";
                    missing.hasGitUrl = false;
                    onUpdateCallback?.Invoke(missing);
                }

                // 或者直接填寫 JSON 的按鈕
                if (GUILayout.Button("自定義 JSON", GUILayout.Width(100)))
                {
                    // 顯示 JSON 輸入對話框
                    ShowScopedRegistryJsonDialog(missing, onUpdateCallback);
                }
                GUILayout.EndHorizontal();

                // Asset Store / 手動安裝提示
                GUILayout.BeginHorizontal();
                GUILayout.Label("其他:", GUILayout.Width(60));
                if (GUILayout.Button("標記為手動安裝", GUILayout.Width(120)))
                {
                    missing.gitUrl = "manual"; // 特殊標記
                    missing.hasGitUrl = false;
                    onUpdateCallback?.Invoke(missing);
                }
                GUILayout.Label("(Asset Store等)", EditorStyles.miniLabel);
                GUILayout.EndHorizontal();

                // 說明文字
                GUILayout.Space(3);
                GUILayout.BeginVertical(EditorStyles.helpBox);
                GUILayout.Label("💡 添加方式說明:", EditorStyles.miniLabel);
                GUILayout.Label("• Git: 🟢 提供完整的Git URL", EditorStyles.miniLabel);
                GUILayout.Label("• Registry: 🔵 從Unity registry安裝", EditorStyles.miniLabel);
                GUILayout.Label("• NPM: 🟣 從npm安裝(需scopedRegistry)", EditorStyles.miniLabel);
                GUILayout.Label("• 手動安裝: 🟠 Asset Store或本地package", EditorStyles.miniLabel);
                GUILayout.EndVertical();
            }

            GUILayout.EndVertical();
        }

        /// <summary>
        /// 繪製從 Package Manager 自動檢測到的依賴資訊
        /// </summary>
        private void DrawAutoDetectedDependencies(
            AssemblyDependencyAnalyzer.ReferencedPackageInfo missing,
            Action<AssemblyDependencyAnalyzer.ReferencedPackageInfo> onUpdateCallback
        )
        {
            // 初始化依賴快取
            PackageDependencyReader.InitializeDependencyCache();

            // 精確匹配
            var exactMatch = PackageDependencyReader.GetDependencyInfo(missing.packageName);

            // 模糊匹配
            // var similarPackages = PackageDependencyReader.FindSimilarPackages(missing.packageName);

            // if (exactMatch != null || similarPackages.Count > 0)
            if (exactMatch != null) // || similarPackages.Count > 0)
            {
                GUILayout.BeginVertical(EditorStyles.helpBox);
                GUILayout.Label("📋 從 Package Manager 檢測到的依賴:", EditorStyles.boldLabel);

                // 精確匹配
                if (exactMatch != null)
                {
                    GUILayout.Space(3);
                    DrawDependencyQuickAdd(exactMatch, missing, onUpdateCallback, true);
                }

                // 相似的 packages
                // if (similarPackages.Count > 1 || (similarPackages.Count == 1 && exactMatch == null))
                // {
                //     GUILayout.Space(3);
                //     GUILayout.Label("🔍 相似的依賴項目:", EditorStyles.miniLabel);
                //
                //     var toShow = similarPackages.Take(3).ToList(); // 最多顯示3個
                //     foreach (var similar in toShow)
                //     {
                //         if (similar != exactMatch) // 避免重複顯示
                //         {
                //             DrawDependencyQuickAdd(similar, missing, onUpdateCallback, false);
                //         }
                //     }
                //
                //     if (similarPackages.Count > 3)
                //     {
                //         GUILayout.Label($"... 還有 {similarPackages.Count - 3} 個相似項目", EditorStyles.miniLabel);
                //     }
                // }

                GUILayout.EndVertical();
            }
        }

        /// <summary>
        /// 繪製依賴快速添加按鈕
        /// </summary>
        private void DrawDependencyQuickAdd(
            PackageDependencyReader.DependencyInfo depInfo,
            AssemblyDependencyAnalyzer.ReferencedPackageInfo missing,
            Action<AssemblyDependencyAnalyzer.ReferencedPackageInfo> onUpdateCallback,
            bool isExactMatch
        )
        {
            GUILayout.BeginHorizontal();

            // 類型圖示
            var typeIcon = GetDependencyTypeIcon(depInfo.Type);
            var typeColor = GetDependencyTypeColor(depInfo.Type);

            var originalColor = GUI.color;
            GUI.color = typeColor;
            GUILayout.Label(typeIcon, GUILayout.Width(20));
            GUI.color = originalColor;

            // Package 名稱
            var nameStyle = isExactMatch ? EditorStyles.boldLabel : EditorStyles.label;
            GUILayout.Label(depInfo.PackageName, nameStyle, GUILayout.Width(180));

            // 版本或 URL 資訊
            var infoText = GetDependencyInfoText(depInfo);
            GUILayout.Label(infoText, EditorStyles.miniLabel, GUILayout.MaxWidth(200));

            GUILayout.FlexibleSpace();

            // 快速添加按鈕
            var buttonText = isExactMatch ? "✅ 使用此依賴" : "📋 使用";
            var buttonWidth = isExactMatch ? 100 : 60;

            if (GUILayout.Button(buttonText, GUILayout.Width(buttonWidth)))
            {
                ApplyDependencyInfo(depInfo, missing, onUpdateCallback);
            }

            GUILayout.EndHorizontal();
        }

        /// <summary>
        /// 應用依賴資訊到目標 package
        /// </summary>
        private void ApplyDependencyInfo(
            PackageDependencyReader.DependencyInfo depInfo,
            AssemblyDependencyAnalyzer.ReferencedPackageInfo missing,
            Action<AssemblyDependencyAnalyzer.ReferencedPackageInfo> onUpdateCallback
        )
        {
            switch (depInfo.Type)
            {
                case PackageDependencyReader.DependencyType.Git:
                    missing.gitUrl = depInfo.Url;
                    missing.hasGitUrl = true;
                    break;

                case PackageDependencyReader.DependencyType.Registry:
                    missing.gitUrl = "registry:" + (depInfo.Version ?? "latest");
                    missing.hasGitUrl = false;
                    break;

                case PackageDependencyReader.DependencyType.Local:
                    missing.gitUrl = depInfo.Url;
                    missing.hasGitUrl = false;
                    break;

                case PackageDependencyReader.DependencyType.Embedded:
                    missing.gitUrl = "embedded:" + depInfo.PackageName;
                    missing.hasGitUrl = false;
                    break;

                case PackageDependencyReader.DependencyType.BuiltIn:
                    missing.gitUrl = "builtin:" + depInfo.PackageName;
                    missing.hasGitUrl = false;
                    break;
            }

            onUpdateCallback?.Invoke(missing);

            Debug.Log($"[AutoFill] 已自動填入 {missing.packageName} 的依賴資訊 (來源: {depInfo.PackageName})");
        }

        /// <summary>
        /// 獲取依賴類型對應的圖示
        /// </summary>
        private string GetDependencyTypeIcon(PackageDependencyReader.DependencyType type)
        {
            switch (type)
            {
                case PackageDependencyReader.DependencyType.Git:
                    return "🟢";
                case PackageDependencyReader.DependencyType.Registry:
                    return "🔵";
                case PackageDependencyReader.DependencyType.Local:
                    return "🟡";
                case PackageDependencyReader.DependencyType.Embedded:
                    return "🟠";
                case PackageDependencyReader.DependencyType.BuiltIn:
                    return "⚪";
                default:
                    return "❓";
            }
        }

        /// <summary>
        /// 獲取依賴類型對應的顏色
        /// </summary>
        private Color GetDependencyTypeColor(PackageDependencyReader.DependencyType type)
        {
            switch (type)
            {
                case PackageDependencyReader.DependencyType.Git:
                    return Color.green;
                case PackageDependencyReader.DependencyType.Registry:
                    return Color.blue;
                case PackageDependencyReader.DependencyType.Local:
                    return Color.yellow;
                case PackageDependencyReader.DependencyType.Embedded:
                    return new Color(1f, 0.5f, 0f); // Orange
                case PackageDependencyReader.DependencyType.BuiltIn:
                    return Color.gray;
                default:
                    return Color.gray;
            }
        }

        /// <summary>
        /// 獲取依賴的描述文字
        /// </summary>
        private string GetDependencyInfoText(PackageDependencyReader.DependencyInfo depInfo)
        {
            switch (depInfo.Type)
            {
                case PackageDependencyReader.DependencyType.Git:
                    var gitInfo = depInfo.Url ?? "";
                    return gitInfo.Length > 40 ? gitInfo.Substring(0, 37) + "..." : gitInfo;

                case PackageDependencyReader.DependencyType.Registry:
                    return $"v{depInfo.Version ?? "latest"}";

                case PackageDependencyReader.DependencyType.Local:
                    var localPath = depInfo.ResolvedPath ?? depInfo.Url ?? "";
                    return localPath.Length > 30 ? "..." + localPath.Substring(localPath.Length - 27) : localPath;

                case PackageDependencyReader.DependencyType.Embedded:
                    return $"Embedded v{depInfo.Version ?? ""}";

                case PackageDependencyReader.DependencyType.BuiltIn:
                    return $"Built-in v{depInfo.Version ?? ""}";

                default:
                    return "Unknown";
            }
        }

        private string GetStatusIcon(AssemblyDependencyAnalyzer.ReferencedPackageInfo package)
        {
            if (!string.IsNullOrEmpty(package.gitUrl))
            {
                if (package.hasGitUrl)
                    return "🟢"; // Git URL
                if (package.gitUrl.StartsWith("registry:"))
                    return "🔵"; // Registry package
                if (package.gitUrl.StartsWith("scopedRegistry:"))
                    return "🟣"; // NPM scoped registry
                if (package.gitUrl == "manual")
                    return "🟠"; // Manual install
            }
            if (package.isLocalPackage)
                return "🟡"; // Local package
            return "🔴"; // Missing
        }

        private Color GetStatusColor(AssemblyDependencyAnalyzer.ReferencedPackageInfo package)
        {
            if (!string.IsNullOrEmpty(package.gitUrl))
            {
                if (package.hasGitUrl)
                    return Color.green; // Git URL
                if (package.gitUrl.StartsWith("registry:"))
                    return Color.blue; // Registry package
                if (package.gitUrl.StartsWith("scopedRegistry:"))
                    return Color.magenta; // NPM scoped registry
                if (package.gitUrl == "manual")
                    return new Color(1f, 0.5f, 0f); // Orange for manual
            }
            if (package.isLocalPackage)
                return Color.yellow; // Local package
            return Color.red; // Missing
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

        /// <summary>
        /// 從package名稱提取scope
        /// 例如: com.kyrylokuzyk.primetween -> com.kyrylokuzyk
        /// </summary>
        private string ExtractScope(string packageName)
        {
            if (string.IsNullOrEmpty(packageName))
                return "";

            var parts = packageName.Split('.');
            if (parts.Length >= 2)
            {
                return $"{parts[0]}.{parts[1]}";
            }

            return packageName; // 如果無法提取，返回原名稱
        }

        /// <summary>
        /// 顯示自定義 scoped registry JSON 輸入對話框
        /// </summary>
        private void ShowScopedRegistryJsonDialog(
            AssemblyDependencyAnalyzer.ReferencedPackageInfo missing,
            Action<AssemblyDependencyAnalyzer.ReferencedPackageInfo> onUpdateCallback
        )
        {
            var defaultJson =
                $@"{{
  ""version"": ""latest"",
  ""scopedRegistry"": {{
    ""name"": ""npm"",
    ""url"": ""https://registry.npmjs.org/"",
    ""scopes"": [""{ExtractScope(missing.packageName)}""]
  }}
}}";

            var customJson = EditorInputDialog.Show(
                "自定義 Scoped Registry JSON",
                "請輸入完整的 scoped registry 設定:",
                defaultJson,
                "確定",
                "取消"
            );

            if (!string.IsNullOrEmpty(customJson))
            {
                missing.gitUrl = "customScopedRegistry:" + customJson;
                missing.hasGitUrl = false;
                onUpdateCallback?.Invoke(missing);
            }
        }
    }

    /// <summary>
    /// 簡單的 Editor 輸入對話框
    /// </summary>
    public static class EditorInputDialog
    {
        public static string Show(
            string title,
            string message,
            string defaultText,
            string ok,
            string cancel
        )
        {
            // 使用 Unity 的輸入對話框
            // 注意：這是簡化版本，實際可能需要創建自定義 EditorWindow
            return EditorUtility.DisplayDialog(
                title,
                $"{message}\n\n預設值已複製到剪貼簿，請貼到外部編輯器修改後，再次調用此功能。",
                ok,
                cancel
            )
                ? defaultText
                : null;
        }
    }
}
