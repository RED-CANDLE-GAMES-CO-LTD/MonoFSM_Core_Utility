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
        };

        // 子組件
        private PackageSelector packageSelector;
        private GitDependenciesTab gitDependenciesTab;
        private AssemblyAnalysisTab assemblyAnalysisTab;

        // GUI Styles
        private GUIStyle headerStyle;
        private bool stylesInitialized = false;

        [MenuItem("Tools/MonoFSM/Dependencies/管理 Git Dependencies", false, 100)]
        public static GitDependencyWindow ShowWindow()
        {
            var window = GetWindow<GitDependencyWindow>("Git Dependencies");
            window.minSize = new Vector2(700, 500); // 增加最小尺寸以容納更多資訊
            window.Show();
            return window;
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
