using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace MonoFSM.Utility.Editor
{
    /// <summary>
    /// Git Dependencies 管理視窗 - 重構版本
    /// 提供視覺化的依賴管理界面，包含 Assembly Dependency 分析功能
    /// </summary>
    public class GitDependencyWindow : EditorWindow
    {
        private int currentTab = 0;
        private readonly string[] tabNames =
        {
            "安裝 Git Dependencies",
            "Assembly Analysis & Update Package.json",
            "Convert Package to Submodule"
        };

        // 子組件
        private PackageSelector packageSelector;
        private GitDependenciesTab gitDependenciesTab;
        private AssemblyAnalysisTab assemblyAnalysisTab;

        // PackageToSubmodule相關
        private List<GitPackageInfo> gitPackages;
        private bool[] selectedPackages;
        private string submoduleRootPath = "Submodules";
        private Vector2 packageScrollPosition;

        // GUI Styles
        private GUIStyle headerStyle;
        private bool stylesInitialized = false;

        // [MenuItem("Tools/MonoFSM/Dependencies/Git Dependencies Installer", false, 100)]
        public static GitDependencyWindow ShowWindow()
        {
            var window = GetWindow<GitDependencyWindow>("Git Dependencies");
            window.minSize = new Vector2(700, 500); // 增加最小尺寸以容納更多資訊
            window.Show();
            return window;
        }

        public void SetActiveTab(int tabIndex)
        {
            if (tabIndex >= 0 && tabIndex < tabNames.Length) currentTab = tabIndex;
        }

        private void OnEnable()
        {
            InitializeComponents();
        }

        private void InitializeComponents()
        {
            // 初始化子組件
            packageSelector = new PackageSelector();
            packageSelector.OnPackageChanged += OnPackageSelectionChanged;

            gitDependenciesTab = new GitDependenciesTab();
            assemblyAnalysisTab = new AssemblyAnalysisTab();

            // 初始化PackageToSubmodule
            RefreshPackageList();

            // 初始化Package選項
            packageSelector.RefreshPackageOptions();

            // 如果有選中的package，執行初次檢查
            var initialPackagePath = packageSelector.GetSelectedPackagePath();
            if (!string.IsNullOrEmpty(initialPackagePath))
            {
                EditorApplication.delayCall += () =>
                {
                    gitDependenciesTab?.AutoCheckDependencies(initialPackagePath);
                    assemblyAnalysisTab?.AutoAnalyze(initialPackagePath);
                };
            }
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

        private void OnGUI()
        {
            InitializeStyles();

            DrawHeader();
            packageSelector?.DrawGUI();
            DrawTabs();

            var selectedPackagePath = packageSelector?.GetSelectedPackagePath();

            switch (currentTab)
            {
                case 0:
                    gitDependenciesTab?.DrawGUI(selectedPackagePath);
                    break;
                case 1:
                    assemblyAnalysisTab?.DrawGUI(selectedPackagePath);
                    break;
                case 2:
                    DrawPackageToSubmoduleTab();
                    break;
            }
        }

        private void DrawHeader()
        {
            GUILayout.Space(10);
            GUILayout.Label("MonoFSM Git Dependencies 管理器", headerStyle);
            GUILayout.Space(10);
        }

        private void DrawTabs()
        {
            currentTab = GUILayout.Toolbar(currentTab, tabNames);
            GUILayout.Space(10);
        }

        private void OnPackageSelectionChanged(string newPackagePath)
        {
            // 當Package選擇改變時，清除兩個Tab的狀態
            gitDependenciesTab?.ResetState();
            assemblyAnalysisTab?.ResetState();

            // 自動執行首次檢查/分析
            if (!string.IsNullOrEmpty(newPackagePath))
            {
                // 延遲執行以避免在GUI事件中執行
                EditorApplication.delayCall += () =>
                {
                    gitDependenciesTab?.AutoCheckDependencies(newPackagePath);
                    assemblyAnalysisTab?.AutoAnalyze(newPackagePath);
                };
            }
        }

        private void RefreshPackageList()
        {
            gitPackages = PackageToSubmoduleConverter.GetGitPackages();
            selectedPackages = new bool[gitPackages?.Count ?? 0];
        }

        private void DrawPackageToSubmoduleTab()
        {
            EditorGUILayout.LabelField("Git Package To Submodule Converter", EditorStyles.boldLabel);
            EditorGUILayout.Space();

            // 設定區域
            EditorGUILayout.LabelField("Settings", EditorStyles.boldLabel);
            submoduleRootPath = EditorGUILayout.TextField("Submodule Root Path", submoduleRootPath);
            EditorGUILayout.Space();

            // 操作按鈕
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Refresh Package List")) RefreshPackageList();
            if (GUILayout.Button("Convert Selected")) ConvertSelectedPackages();
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.Space();

            // Package 列表
            EditorGUILayout.LabelField("Git Packages Found", EditorStyles.boldLabel);

            if (gitPackages == null || gitPackages.Count == 0)
            {
                EditorGUILayout.HelpBox("No git packages found in manifest.json", MessageType.Info);
                return;
            }

            packageScrollPosition = EditorGUILayout.BeginScrollView(packageScrollPosition);

            for (var i = 0; i < gitPackages.Count; i++)
            {
                var package = gitPackages[i];

                EditorGUILayout.BeginVertical(GUI.skin.box);

                // 選擇框和包名
                EditorGUILayout.BeginHorizontal();
                if (selectedPackages != null && i < selectedPackages.Length)
                    selectedPackages[i] = EditorGUILayout.Toggle(selectedPackages[i], GUILayout.Width(20));
                EditorGUILayout.LabelField(package.packageName, EditorStyles.boldLabel);
                EditorGUILayout.EndHorizontal();

                // 詳細資訊
                EditorGUILayout.LabelField("Git URL:", package.gitUrl, EditorStyles.miniLabel);
                if (!string.IsNullOrEmpty(package.gitPath))
                    EditorGUILayout.LabelField("Path:", package.gitPath, EditorStyles.miniLabel);
                EditorGUILayout.LabelField("Target Submodule Path:",
                    $"{submoduleRootPath}/{package.GetSubmoduleName()}", EditorStyles.miniLabel);

                EditorGUILayout.EndVertical();
                EditorGUILayout.Space();
            }

            EditorGUILayout.EndScrollView();

            // 統計資訊
            var selectedCount = selectedPackages?.Count(x => x) ?? 0;
            EditorGUILayout.LabelField($"Selected: {selectedCount}/{gitPackages.Count}");
        }

        private void ConvertSelectedPackages()
        {
            if (selectedPackages == null || gitPackages == null) return;

            var packagesToConvert = new List<GitPackageInfo>();
            for (var i = 0; i < selectedPackages.Length && i < gitPackages.Count; i++)
                if (selectedPackages[i])
                    packagesToConvert.Add(gitPackages[i]);

            if (packagesToConvert.Count == 0)
            {
                EditorUtility.DisplayDialog("No Selection", "請選擇要轉換的package", "OK");
                return;
            }

            var confirm = EditorUtility.DisplayDialog(
                "確認轉換",
                $"將轉換 {packagesToConvert.Count} 個git package為submodule。\n此操作會修改manifest.json和添加git submodule。\n是否繼續？",
                "確認",
                "取消"
            );

            if (confirm)
            {
                var converter = new PackageToSubmoduleConverter(submoduleRootPath);
                foreach (var package in packagesToConvert) converter.ConvertToSubmodule(package);

                RefreshPackageList();
                EditorUtility.DisplayDialog("完成", "轉換完成！請檢查git狀態。", "OK");
            }
        }

        private void OnDestroy()
        {
            // 清理事件
            if (packageSelector != null)
            {
                packageSelector.OnPackageChanged -= OnPackageSelectionChanged;
            }
        }
    }
}
