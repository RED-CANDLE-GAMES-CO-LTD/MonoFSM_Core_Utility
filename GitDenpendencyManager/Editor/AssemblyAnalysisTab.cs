using System.Collections.Generic;
using System.Linq;
using MonoFSM.Core;
using UnityEditor;
using UnityEngine;

namespace MonoFSM.Utility.Editor
{
    /// <summary>
    /// Assembly Dependency 分析 Tab - 改善版本
    /// 提供詳細的Assembly分析功能和更好的顯示介面
    /// </summary>
    public class AssemblyAnalysisTab
    {
        private AssemblyDependencyAnalyzer.AnalysisResult analysisResult;
        private bool isAnalyzing = false;
        private Dictionary<string, string> gitUrlInputs = new Dictionary<string, string>();
        private Vector2 scrollPosition;
        private AssemblyDependencyDisplayHelper displayHelper;

        // GUI Styles
        private GUIStyle headerStyle;
        private bool stylesInitialized = false;

        // 展開狀態管理
        private bool showExistingDependencies = false;
        private bool showAssemblyDetails = false;
        private bool showInstallationStatus = true;

        public AssemblyAnalysisTab()
        {
            displayHelper = new AssemblyDependencyDisplayHelper();
        }

        public void DrawGUI(string selectedPackageJsonPath)
        {
            InitializeStyles();

            DrawHeader(selectedPackageJsonPath);
            DrawAnalysisResults();
            DrawFooter();
        }

        private void InitializeStyles()
        {
            if (stylesInitialized)
                return;

            headerStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 14,
                margin = new RectOffset(0, 0, 10, 5),
            };

            stylesInitialized = true;
        }

        private void DrawHeader(string selectedPackageJsonPath)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label("Package.json補完：Assembly Dependency 分析器", headerStyle);

            GUILayout.FlexibleSpace();

            // 分析按鈕
            GUI.enabled = !string.IsNullOrEmpty(selectedPackageJsonPath) && !isAnalyzing;
            if (analysisResult == null && !isAnalyzing)
            {
                if (GUILayout.Button("分析中...", GUILayout.Width(80)))
                {
                    AnalyzeSelectedPackage(selectedPackageJsonPath);
                }
            }
            else if (GUILayout.Button("🔄重新分析", GUILayout.Width(80)))
            {
                AnalyzeSelectedPackage(selectedPackageJsonPath);
            }
            GUI.enabled = true;

            GUILayout.EndHorizontal();
            GUILayout.Space(5);

            GUILayout.Label(
                "🔄 自動分析 package 內的 asmdef 引用，協助更新 dependencies",
                EditorStyles.helpBox
            );
            GUILayout.Space(5);
        }

        private void DrawAnalysisResults()
        {
            if (isAnalyzing)
            {
                GUILayout.Label("正在分析中...", EditorStyles.centeredGreyMiniLabel);
                return;
            }

            if (analysisResult == null)
            {
                if (isAnalyzing)
                {
                    GUILayout.Label("正在自動分析中...", EditorStyles.centeredGreyMiniLabel);
                }
                else
                {
                    GUILayout.Label(
                        "選擇 Package 後將自動分析",
                        EditorStyles.centeredGreyMiniLabel
                    );
                }
                return;
            }

            scrollPosition = GUILayout.BeginScrollView(scrollPosition);

            // 結果摘要 - 更詳細的資訊
            DrawDetailedAnalysisSummary();

            GUILayout.Space(10);

            // 安裝狀態總覽
            DrawInstallationStatusOverview();

            GUILayout.Space(10);

            // 缺失的 Dependencies
            if (analysisResult.missingDependencies.Count > 0)
            {
                DrawMissingDependencies();
            }

            // 已存在的 Dependencies - 可摺疊
            DrawExistingDependencies();

            // Assembly 詳細資訊 - 可摺疊
            DrawAssemblyDetails();

            GUILayout.EndScrollView();
        }

        private void DrawDetailedAnalysisSummary()
        {
            GUILayout.BeginVertical(EditorStyles.helpBox);
            GUILayout.Label("📊 分析結果", EditorStyles.boldLabel);

            GUILayout.BeginHorizontal();
            GUILayout.BeginVertical(GUILayout.Width(200));
            GUILayout.Label($"Package: {analysisResult.targetPackageName}", EditorStyles.boldLabel);
            GUILayout.Label($"總計 Assemblies: {analysisResult.totalAssemblies}");
            GUILayout.Label($"有外部引用: {analysisResult.externalReferences}");
            GUILayout.EndVertical();

            // GUILayout.BeginVertical();
            // displayHelper.DrawStatusBadge("缺失 Dependencies", analysisResult.missingDependencies.Count,
            //     analysisResult.missingDependencies.Count > 0 ? Color.red : Color.green);
            // displayHelper.DrawStatusBadge("已存在 Dependencies", analysisResult.existingDependencies.Count, Color.blue);
            // displayHelper.DrawStatusBadge("需要 Git URL", analysisResult.needGitUrlDependencies.Count,
            //     analysisResult.needGitUrlDependencies.Count > 0 ? Color.yellow : Color.green);
            // GUILayout.EndVertical();

            GUILayout.EndHorizontal();
            GUILayout.EndVertical();
        }

        private void DrawInstallationStatusOverview()
        {
            showInstallationStatus = EditorGUILayout.Foldout(
                showInstallationStatus,
                "📋 外部引用狀態總覽",
                true
            );
            if (!showInstallationStatus)
                return;

            GUILayout.BeginVertical(EditorStyles.helpBox);

            var allDependencies = new List<AssemblyDependencyAnalyzer.ReferencedPackageInfo>();
            allDependencies.AddRange(analysisResult.missingDependencies);
            allDependencies.AddRange(analysisResult.existingDependencies);

            // 按狀態分類顯示外部引用
            GUILayout.Label("🔗 所有外部引用:", EditorStyles.boldLabel);

            foreach (var dependency in allDependencies.OrderBy(d => d.packageName))
            {
                GUILayout.BeginHorizontal();

                // 狀態圖示和顏色
                string statusIcon;
                string statusText;
                Color statusColor;

                if (analysisResult.existingDependencies.Contains(dependency))
                {
                    statusIcon = "✅";
                    statusText = "已登記 Registered";
                    statusColor = Color.green;
                }
                else if (dependency.isLocalPackage)
                {
                    statusIcon = "📁";
                    statusText = "本地 Package";
                    statusColor = Color.yellow;
                }
                else
                {
                    statusIcon = "❌";
                    statusText = "Missing in Package.json";
                    statusColor = Color.red;
                }

                var originalColor = GUI.color;
                GUI.color = statusColor;
                GUILayout.Label(statusIcon, GUILayout.Width(25));
                GUI.color = originalColor;

                GUILayout.Label(dependency.packageName, GUILayout.Width(200));
                GUILayout.Label($"[{statusText}]", EditorStyles.miniLabel, GUILayout.Width(80));

                // 顯示引用來源的Assembly
                if (!string.IsNullOrEmpty(dependency.assemblyName))
                {
                    GUILayout.Label(
                        $"← {dependency.assemblyName}",
                        EditorStyles.miniLabel,
                        GUILayout.Width(150)
                    );
                }

                GUILayout.FlexibleSpace();

                // Git URL 或動作按鈕
                if (!string.IsNullOrEmpty(dependency.gitUrl))
                {
                    if (GUILayout.Button("📋", GUILayout.Width(25)))
                    {
                        EditorGUIUtility.systemCopyBuffer = dependency.gitUrl;
                        Debug.Log($"已複製 Git URL: {dependency.gitUrl}");
                    }
                }
                else if (dependency.isLocalPackage)
                {
                    GUILayout.Label("(Local)", EditorStyles.miniLabel);
                }

                GUILayout.EndHorizontal();
            }

            GUILayout.EndVertical();
        }

        private void DrawMissingDependencies()
        {
            // GUILayout.Label("❌ 尚未加到Package.json的 Dependencies:", EditorStyles.boldLabel);
            //
            // // 說明
            // GUILayout.BeginVertical(EditorStyles.helpBox);
            // GUILayout.Label("說明：", EditorStyles.boldLabel);
            // GUILayout.Label("• 🟢 綠色：已從主專案 manifest.json 找到 Git URL，可直接添加");
            // GUILayout.Label("• 🟡 黃色：本地 package，可選擇提供 Git URL 或保持為 local package");
            // GUILayout.Label("• 🔴 紅色：需要手動提供 Git URL");
            // GUILayout.EndVertical();
            //
            // GUILayout.Space(5);

            GUILayout.BeginVertical(EditorStyles.helpBox);
            foreach (var missing in analysisResult.missingDependencies)
            {
                displayHelper.DrawMissingDependencyItem(
                    missing,
                    gitUrlInputs,
                    UpdateSinglePackageJson
                );
            }
            GUILayout.EndVertical();

            GUILayout.Space(10);
        }

        private void DrawExistingDependencies()
        {
            if (analysisResult.existingDependencies.Count == 0)
                return;

            showExistingDependencies = EditorGUILayout.Foldout(
                showExistingDependencies,
                $"✅ 已存在的 Dependencies ({analysisResult.existingDependencies.Count})",
                true
            );

            if (!showExistingDependencies)
                return;

            GUILayout.BeginVertical(EditorStyles.helpBox);
            foreach (var existing in analysisResult.existingDependencies)
            {
                displayHelper.DrawExistingDependencyItem(existing);
            }
            GUILayout.EndVertical();
            GUILayout.Space(10);
        }

        private void DrawAssemblyDetails()
        {
            if (analysisResult.assemblies.Count == 0)
                return;

            showAssemblyDetails = EditorGUILayout.Foldout(
                showAssemblyDetails,
                $"🔧 Assembly 詳細資訊 ({analysisResult.assemblies.Count})",
                true
            );

            if (!showAssemblyDetails)
                return;

            GUILayout.BeginVertical(EditorStyles.helpBox);
            foreach (var assembly in analysisResult.assemblies)
            {
                displayHelper.DrawAssemblyDetailItem(assembly);
            }
            GUILayout.EndVertical();
            GUILayout.Space(10);
        }

        private void DrawFooter()
        {
            GUILayout.FlexibleSpace();

            GUILayout.BeginVertical(EditorStyles.helpBox);
            GUILayout.Label("💡 Assembly Analysis 說明:", EditorStyles.boldLabel);
            GUILayout.Label("• ✅ = 已安裝且在 package.json 中");
            GUILayout.Label("• ❌ = 缺失依賴，需要安裝");
            GUILayout.Label("• 🟡 = 本地 package（可選擇提供 Git URL）");
            GUILayout.Label("• 📊 = 進度條顯示整體安裝狀態");
            GUILayout.EndVertical();
        }

        public void ResetState()
        {
            analysisResult = null;
            isAnalyzing = false;
            gitUrlInputs.Clear();
        }

        /// <summary>
        /// 自動分析 - 當選擇新package時調用
        /// </summary>
        public void AutoAnalyze(string selectedPackageJsonPath)
        {
            if (!string.IsNullOrEmpty(selectedPackageJsonPath))
            {
                AnalyzeSelectedPackage(selectedPackageJsonPath);
            }
        }

        private void AnalyzeSelectedPackage(string selectedPackageJsonPath)
        {
            if (string.IsNullOrEmpty(selectedPackageJsonPath))
                return;

            isAnalyzing = true;

            EditorApplication.delayCall += () =>
            {
                analysisResult = AssemblyDependencyAnalyzer.AnalyzePackageDependencies(
                    selectedPackageJsonPath
                );
                isAnalyzing = false;
                gitUrlInputs.Clear(); // 清空之前的輸入
            };
        }

        private void UpdateSinglePackageJson(
            AssemblyDependencyAnalyzer.ReferencedPackageInfo package
        )
        {
            if (analysisResult == null)
                return;

            var gitUrl = !string.IsNullOrEmpty(package.gitUrl)
                ? package.gitUrl
                : (
                    gitUrlInputs.ContainsKey(package.packageName)
                        ? gitUrlInputs[package.packageName]
                        : ""
                );

            if (string.IsNullOrWhiteSpace(gitUrl))
            {
                EditorUtility.DisplayDialog("錯誤", "沒有提供 Git URL", "確定");
                return;
            }

            // 使用單一 package 更新方法
            AssemblyDependencyAnalyzer.UpdateSinglePackageJsonDependency(
                analysisResult,
                package.packageName,
                gitUrl
            );

            EditorUtility.DisplayDialog(
                "添加完成",
                $"已將 '{package.packageName}' 添加到 package.json！",
                "確定"
            );

            // 重新分析以更新狀態
            AnalyzeSelectedPackage(analysisResult.targetPackageJsonPath);
        }
    }
}
