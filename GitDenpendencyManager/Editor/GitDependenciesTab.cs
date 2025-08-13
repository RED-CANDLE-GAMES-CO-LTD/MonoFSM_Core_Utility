using System.Collections.Generic;
using System.Linq;
using MonoFSM.Core;
using UnityEditor;
using UnityEngine;

namespace MonoFSM.Utility.Editor
{
    /// <summary>
    /// Git Dependencies 安裝管理 Tab
    /// 負責顯示和管理 Git Dependencies 的安裝狀態
    /// </summary>
    public class GitDependenciesTab
    {
        private Vector2 scrollPosition;
        private GitDependencyInstaller.DependencyCheckResult checkResult;
        private bool isChecking = false;
        private bool showInstalledDependencies = true;
        private bool showMissingDependencies = true;
        private string searchFilter = "";
        private string currentPackageJsonPath = "";

        // GUI Styles
        private GUIStyle headerStyle;
        private GUIStyle installedStyle;
        private GUIStyle missingStyle;
        private bool stylesInitialized = false;

        public void DrawGUI(string selectedPackageJsonPath)
        {
            // 更新當前路徑
            currentPackageJsonPath = selectedPackageJsonPath;

            InitializeStyles();

            DrawHeader();
            DrawToolbar(selectedPackageJsonPath);
            DrawSearchFilter();
            DrawDependenciesList();
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

            installedStyle = new GUIStyle(EditorStyles.helpBox)
            {
                normal = { textColor = new Color(0.2f, 0.8f, 0.2f) },
            };

            missingStyle = new GUIStyle(EditorStyles.helpBox)
            {
                normal = { textColor = new Color(0.8f, 0.2f, 0.2f) },
            };

            stylesInitialized = true;
        }

        private void DrawHeader()
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label("Git Dependencies 檢查", headerStyle);
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();

            if (checkResult != null)
            {
                var statusText = checkResult.allDependenciesInstalled
                    ? "✓ 所有依賴已安裝"
                    : $"⚠ {checkResult.missingDependencies.Count} 個依賴缺失";

                var statusColor = checkResult.allDependenciesInstalled ? Color.green : Color.yellow;

                var originalColor = GUI.color;
                GUI.color = statusColor;
                GUILayout.Label(statusText, EditorStyles.boldLabel);
                GUI.color = originalColor;
            }

            GUILayout.Space(5);
        }

        private void DrawToolbar(string selectedPackageJsonPath)
        {
            GUILayout.BeginHorizontal(EditorStyles.toolbar);

            if (checkResult == null)
            {
                if (GUILayout.Button("🔍檢查中...", EditorStyles.toolbarButton))
                {
                    RefreshDependencies(selectedPackageJsonPath);
                }
            }
            else if (GUILayout.Button("🔄重新檢查", EditorStyles.toolbarButton))
            {
                RefreshDependencies(selectedPackageJsonPath);
            }

            GUILayout.FlexibleSpace();

            // 顯眼的安裝按鈕
            var hasUninstalledDeps = checkResult != null && !checkResult.allDependenciesInstalled;
            GUI.enabled = hasUninstalledDeps;

            var installButtonStyle = EditorStyles.toolbarButton;
            if (hasUninstalledDeps)
            {
                installButtonStyle.normal.textColor = Color.white;
                var originalColor = GUI.backgroundColor;
                GUI.backgroundColor = new Color(0.2f, 0.7f, 0.2f); // 綠色背景

                if (
                    GUILayout.Button(
                        $"🔧 安裝所有缺失依賴 ({checkResult.missingDependencies.Count})",
                        installButtonStyle,
                        GUILayout.Height(25)
                    )
                )
                {
                    InstallMissingDependencies(selectedPackageJsonPath);
                }

                GUI.backgroundColor = originalColor;
            }
            else
            {
                if (GUILayout.Button("✅ 所有依賴已安裝", installButtonStyle, GUILayout.Height(25)))
                {
                    InstallMissingDependencies(selectedPackageJsonPath);
                }
            }
            GUI.enabled = true;

            GUILayout.Space(10);

            if (GUILayout.Button("生成報告", EditorStyles.toolbarButton))
            {
                GitDependencyManager.GenerateDependencyReport();
            }

            if (isChecking)
            {
                GUILayout.Label("檢查中...", EditorStyles.toolbarButton);
            }

            GUILayout.EndHorizontal();
        }

        private void DrawSearchFilter()
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label("搜尋:", GUILayout.Width(40));
            searchFilter = GUILayout.TextField(searchFilter);

            if (GUILayout.Button("清除", GUILayout.Width(50)))
            {
                searchFilter = "";
                GUI.FocusControl(null);
            }
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            showInstalledDependencies = GUILayout.Toggle(showInstalledDependencies, "顯示已安裝");
            showMissingDependencies = GUILayout.Toggle(showMissingDependencies, "顯示缺失");
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();

            GUILayout.Space(5);
        }

        private void DrawDependenciesList()
        {
            if (checkResult == null)
            {
                if (isChecking)
                {
                    GUILayout.Label("正在自動檢查依賴...");
                }
                else
                {
                    GUILayout.Label("選擇 Package 後將自動檢查依賴。");
                }
                return;
            }

            if (checkResult.gitDependencies.Count == 0)
            {
                GUILayout.Label("沒有找到 Git Dependencies。");
                return;
            }

            scrollPosition = GUILayout.BeginScrollView(scrollPosition);

            var filteredDependencies = GetFilteredDependencies();

            foreach (var dependency in filteredDependencies)
            {
                DrawDependencyItem(dependency);
            }

            GUILayout.EndScrollView();
        }

        private List<GitDependencyInstaller.GitDependencyInfo> GetFilteredDependencies()
        {
            var filtered = checkResult.gitDependencies.AsEnumerable();

            // 狀態過濾
            if (!showInstalledDependencies)
                filtered = filtered.Where(d => !d.isInstalled);
            if (!showMissingDependencies)
                filtered = filtered.Where(d => d.isInstalled);

            // 搜尋過濾
            if (!string.IsNullOrEmpty(searchFilter))
            {
                var filter = searchFilter.ToLower();
                filtered = filtered.Where(d =>
                    d.packageName.ToLower().Contains(filter) || d.gitUrl.ToLower().Contains(filter)
                );
            }

            return filtered.OrderBy(d => d.isInstalled ? 0 : 1).ThenBy(d => d.packageName).ToList();
        }

        private void DrawDependencyItem(GitDependencyInstaller.GitDependencyInfo dependency)
        {
            var style = dependency.isInstalled ? installedStyle : missingStyle;

            GUILayout.BeginVertical(style);

            // 標題行
            GUILayout.BeginHorizontal();

            var statusIcon = dependency.isInstalled ? "✓" : "✗";
            var statusColor = dependency.isInstalled ? Color.green : Color.red;

            var originalColor = GUI.color;
            GUI.color = statusColor;
            GUILayout.Label(statusIcon, GUILayout.Width(20));
            GUI.color = originalColor;

            GUILayout.Label(dependency.packageName, EditorStyles.boldLabel);

            GUILayout.FlexibleSpace();

            if (!dependency.isInstalled)
            {
                if (GUILayout.Button("安裝", GUILayout.Width(60)))
                {
                    InstallSingleDependency(dependency);
                }
            }
            else if (!string.IsNullOrEmpty(dependency.installedVersion))
            {
                GUILayout.Label($"v{dependency.installedVersion}", EditorStyles.miniLabel);
            }

            GUILayout.EndHorizontal();

            // URL 行
            GUILayout.BeginHorizontal();
            GUILayout.Label("URL:", GUILayout.Width(30));

            if (GUILayout.Button(dependency.gitUrl, EditorStyles.linkLabel))
            {
                // 複製 URL 到剪貼簿
                EditorGUIUtility.systemCopyBuffer = dependency.gitUrl;
                Debug.Log($"已複製到剪貼簿: {dependency.gitUrl}");
            }

            GUILayout.EndHorizontal();

            GUILayout.EndVertical();
            GUILayout.Space(5);
        }

        private void DrawFooter()
        {
            GUILayout.FlexibleSpace();

            GUILayout.BeginVertical(EditorStyles.helpBox);
            GUILayout.Label("提示:", EditorStyles.boldLabel);
            GUILayout.Label("• 點擊 URL 可複製到剪貼簿");
            GUILayout.Label("• 建議在安裝完成後重新啟動 Unity Editor");
            GUILayout.Label("• 如遇到問題，請查看 Console 的詳細日誌");
            GUILayout.EndVertical();
        }

        public void ResetState()
        {
            checkResult = null;
            isChecking = false;
            searchFilter = "";
        }

        /// <summary>
        /// 自動檢查依賴 - 當選擇新package時調用
        /// </summary>
        public void AutoCheckDependencies(string selectedPackageJsonPath)
        {
            if (!string.IsNullOrEmpty(selectedPackageJsonPath))
            {
                RefreshDependencies(selectedPackageJsonPath);
            }
        }

        private void RefreshDependencies(string selectedPackageJsonPath)
        {
            if (string.IsNullOrEmpty(selectedPackageJsonPath))
            {
                Debug.LogWarning("[GitDependenciesTab] 沒有選擇 package.json");
                return;
            }

            isChecking = true;

            // 使用 EditorApplication.delayCall 避免在 OnGUI 中執行耗時操作
            EditorApplication.delayCall += () =>
            {
                checkResult = GitDependencyInstaller.CheckGitDependencies(selectedPackageJsonPath);
                isChecking = false;
            };
        }

        private void InstallMissingDependencies(string selectedPackageJsonPath)
        {
            if (checkResult?.missingDependencies?.Count > 0)
            {
                var message =
                    $"確定要安裝 {checkResult.missingDependencies.Count} 個缺失的依賴嗎？\n\n"
                    + "這可能需要一些時間，請耐心等候。";

                if (EditorUtility.DisplayDialog("確認安裝", message, "確定", "取消"))
                {
                    GitDependencyInstaller.InstallMissingGitDependencies(checkResult);
                    RefreshDependencies(selectedPackageJsonPath);
                }
            }
        }

        private void InstallSingleDependency(GitDependencyInstaller.GitDependencyInfo dependency)
        {
            // 使用共用的安裝判定邏輯
            if (!GitDependencyInstaller.ShouldInstallDependency(dependency))
            {
                return;
            }

            var isGit = GitDependencyInstaller.IsGitUrl(dependency.gitUrl);
            var message = $"確定要安裝 '{dependency.packageName}' 嗎？\n\nURL: {dependency.gitUrl}";
            if (!isGit)
                message = $"確定要安裝 \n{dependency.packageName}@{dependency.installedVersion}";

            if (EditorUtility.DisplayDialog("確認安裝", message, "確定", "取消"))
            {
                // 使用共用的核心安裝方法
                GitDependencyInstaller.InstallSingleDependencyCore(
                    dependency,
                    OnSingleInstallComplete,
                    showProgress: false
                );
            }
        }

        /// <summary>
        /// 單個安裝完成的回調方法
        /// </summary>
        private void OnSingleInstallComplete(
            bool success,
            string packageName,
            string errorMessage = null
        )
        {
            if (success)
            {
                EditorUtility.DisplayDialog("安裝成功", $"'{packageName}' 已成功安裝！", "確定");
            }
            else
            {
                EditorUtility.DisplayDialog(
                    "安裝失敗",
                    $"'{packageName}' 安裝失敗。\n\n錯誤訊息: {errorMessage ?? "請查看 Console 獲取詳細訊息。"}",
                    "確定"
                );
            }

            // 重新整理依賴列表
            RefreshDependencies(currentPackageJsonPath);
        }
    }
}
