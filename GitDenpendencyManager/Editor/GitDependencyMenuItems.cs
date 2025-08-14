using UnityEditor;

namespace MonoFSM.Utility.Editor
{
    /// <summary>
    /// Git Dependencies 相關的選單項目
    /// 統一管理所有 MenuItem 功能
    /// </summary>
    public static class GitDependencyMenuItems
    {
        // 依據 MonoFSM CLAUDE.md 的 MenuItem 統一規範
        private const string MENU_ROOT = "Tools/MonoFSM/Git Dependencies/";
        
        [MenuItem(MENU_ROOT + "開啟管理視窗", false, 200)]
        private static void OpenManagementWindow()
        {
            GitDependencyWindow.ShowWindow();
        }

        [MenuItem("Window/Package Management/Convert Package to Submodule", false)]
        private static void ConvertPackageToSubmoduleWindow()
        {
            var window = GitDependencyWindow.ShowWindow();
            window.SetActiveTab(2);
            
        }

        [MenuItem("Window/Package Management/Git Dependency Installer", false)]
        private static void OpenPackageManagementWindow()
        {
            GitDependencyWindow.ShowWindow();
        }

        // 設定選單
        [MenuItem(MENU_ROOT + "設定/啟用自動檢查", false, 301)]
        private static void ToggleAutoCheck()
        {
            var currentState = GitDependencyManager.GetAutoInstallEnabled();
            GitDependencyManager.SetAutoInstallEnabled(!currentState);

            var newState = !currentState ? "啟用" : "停用";
            EditorUtility.DisplayDialog("設定更新", $"自動檢查已{newState}。", "確定");
        }

        [MenuItem(MENU_ROOT + "設定/啟用自動檢查", true)]
        private static bool ValidateToggleAutoCheck()
        {
            var enabled = GitDependencyManager.GetAutoInstallEnabled();
            Menu.SetChecked("Tools/MonoFSM/Dependencies/設定/啟用自動檢查", enabled);
            return true;
        }

        // 幫助選單
        // [MenuItem(MENU_ROOT + "幫助/關於 Git Dependencies", false, 401)]
        // private static void ShowAbout()
        // {
        //     var about =
        //         "MonoFSM Git Dependencies 管理器\n\n"
        //         + "功能特色:\n"
        //         + "• 自動檢查和安裝 Git Dependencies\n"
        //         + "• 視覺化管理界面\n"
        //         + "• 批量更新本地 packages\n"
        //         + "• 詳細的依賴報告\n\n"
        //         + "開發者: Red Candle Games\n"
        //         + "版本: 1.0.0";
        //
        //     EditorUtility.DisplayDialog("關於 Git Dependencies", about, "確定");
        // }

        [MenuItem(MENU_ROOT + "幫助/使用說明", false, 402)]
        private static void ShowHelp()
        {
            var help =
                "MonoFSM Git Dependencies 使用說明\n\n"
                + "1. 檢查依賴:\n"
                + "   使用 '檢查 Git Dependencies' 來掃描依賴狀態\n\n"
                + "2. 安裝依賴:\n"
                + "   使用 '安裝所有缺失 Dependencies' 來自動安裝\n\n"
                + "3. 管理界面:\n"
                + "   使用 '開啟管理視窗' 來開啟視覺化管理界面\n\n"
                + "4. 更新 Packages:\n"
                + "   使用 '更新本地 Package Dependencies' 來更新所有本地 packages\n\n"
                + "5. 自動檢查:\n"
                + "   在設定中可開啟/關閉自動檢查功能\n\n"
                + "注意事項:\n"
                + "• 建議在安裝完成後重新啟動 Unity\n"
                + "• 如遇問題請查看 Console 日誌";

            EditorUtility.DisplayDialog("使用說明", help, "確定");
        }
    }
}
