using System.IO;
using System.Linq;
using MonoFSM.Core;
using UnityEditor;
using UnityEngine;

namespace MonoFSM.Utility.Editor
{
    /// <summary>
    /// Git Dependencies 相關的選單項目
    /// 統一管理所有 MenuItem 功能
    /// </summary>
    public static class GitDependencyMenuItems
    {
        // 依據 MonoFSM CLAUDE.md 的 MenuItem 統一規範
        private const string MENU_ROOT = "Tools/MonoFSM/Dependencies/";

        // [MenuItem(MENU_ROOT + "檢查 Git Dependencies", false, 101)]
        // private static void CheckGitDependencies()
        // {
        //     Debug.Log("[GitDependencyMenuItems] 開始檢查 Git Dependencies...");
        //     var result = GitDependencyInstaller.CheckGitDependencies();
        //
        //     var message =
        //         $"Git Dependencies 檢查完成：\n\n"
        //         + $"總計: {result.gitDependencies.Count}\n"
        //         + $"已安裝: {result.installedDependencies.Count}\n"
        //         + $"缺失: {result.missingDependencies.Count}";
        //
        //     if (result.allDependenciesInstalled)
        //     {
        //         EditorUtility.DisplayDialog(
        //             "檢查完成",
        //             message + "\n\n✓ 所有依賴已正確安裝！",
        //             "確定"
        //         );
        //     }
        //     else
        //     {
        //         message += $"\n\n缺失的依賴:\n• {string.Join("\n• ", result.missingDependencies)}";
        //
        //         if (
        //             EditorUtility.DisplayDialog(
        //                 "檢查完成",
        //                 message + "\n\n是否要現在安裝缺失的依賴？",
        //                 "安裝",
        //                 "稍後"
        //             )
        //         )
        //         {
        //             GitDependencyInstaller.InstallMissingGitDependencies(
        //         }
        //     }
        // }

        // [MenuItem(MENU_ROOT + "安裝所有缺失 Dependencies", false, 102)]
        // private static void InstallMissingDependencies()
        // {
        //     Debug.Log("[GitDependencyMenuItems] 開始安裝缺失的 Git Dependencies...");
        //     GitDependencyInstaller.InstallMissingGitDependencies();
        // }

        // [MenuItem(MENU_ROOT + "安裝所有缺失 Dependencies", true)]
        // private static bool ValidateInstallMissingDependencies()
        // {
        //     // 只有當有缺失依賴時才啟用選單
        //     var result = GitDependencyInstaller.GetLastCheckResult();
        //     return result.missingDependencies != null && result.missingDependencies.Count > 0;
        // }

        // [MenuItem(MENU_ROOT + "更新本地 Package Dependencies", false, 103)]
        // private static void UpdateLocalPackageDependencies()
        // {
        //     Debug.Log("[GitDependencyMenuItems] 開始更新本地 Package Dependencies...");
        //
        //     if (
        //         EditorUtility.DisplayDialog(
        //             "更新本地 Package Dependencies",
        //             "這將會檢查所有本地 packages 並自動添加缺失的 git dependencies。\n\n繼續嗎？",
        //             "確定",
        //             "取消"
        //         )
        //     )
        //     {
        //         GitDependencyManager.UpdateAllLocalPackageDependencies();
        //         EditorUtility.DisplayDialog(
        //             "更新完成",
        //             "本地 Package Dependencies 更新完成！",
        //             "確定"
        //         );
        //     }
        // }

        // [MenuItem(MENU_ROOT + "分析 Assembly Dependencies", false, 104)]
        // private static void AnalyzeAssemblyDependencies()
        // {
        //     var packageJsonPath = EditorUtility.OpenFilePanel(
        //         "選擇要分析的 package.json",
        //         Application.dataPath,
        //         "json"
        //     );
        //
        //     if (
        //         !string.IsNullOrEmpty(packageJsonPath)
        //         && Path.GetFileName(packageJsonPath) == "package.json"
        //     )
        //     {
        //         Debug.Log(
        //             $"[GitDependencyMenuItems] 開始分析 Assembly Dependencies: {packageJsonPath}"
        //         );
        //
        //         var result = AssemblyDependencyAnalyzer.AnalyzePackageDependencies(packageJsonPath);
        //
        //         var message =
        //             $"Assembly Dependencies 分析完成：\n\n"
        //             + $"Package: {result.targetPackageName}\n"
        //             + $"總計 Assemblies: {result.totalAssemblies}\n"
        //             + $"有外部引用: {result.externalReferences}\n"
        //             + $"缺失 Dependencies: {result.missingDependencies.Count}\n"
        //             + $"已存在 Dependencies: {result.existingDependencies.Count}";
        //
        //         if (result.missingDependencies.Count > 0)
        //         {
        //             message +=
        //                 $"\n\n缺失的 Dependencies:\n• {string.Join("\n• ", result.missingDependencies.Select(d => d.packageName))}";
        //
        //             if (
        //                 EditorUtility.DisplayDialog(
        //                     "分析完成",
        //                     message + "\n\n是否要打開管理視窗進行詳細操作？",
        //                     "打開管理視窗",
        //                     "關閉"
        //                 )
        //             )
        //             {
        //                 var window = GitDependencyWindow.ShowWindow();
        //                 // 切換到 Assembly Analysis tab 並設定選中的 package
        //                 EditorApplication.delayCall += () =>
        //                 {
        //                     var windowInstance = EditorWindow.GetWindow<GitDependencyWindow>();
        //                     if (windowInstance != null)
        //                     {
        //                         // 使用反射設定私有字段（或者可以改為公開方法）
        //                         var field = typeof(GitDependencyWindow).GetField(
        //                             "currentTab",
        //                             System.Reflection.BindingFlags.NonPublic
        //                                 | System.Reflection.BindingFlags.Instance
        //                         );
        //                         field?.SetValue(windowInstance, 1); // Assembly Analysis tab
        //
        //                         var pathField = typeof(GitDependencyWindow).GetField(
        //                             "selectedPackageJsonPath",
        //                             System.Reflection.BindingFlags.NonPublic
        //                                 | System.Reflection.BindingFlags.Instance
        //                         );
        //                         pathField?.SetValue(windowInstance, packageJsonPath);
        //
        //                         var resultField = typeof(GitDependencyWindow).GetField(
        //                             "assemblyAnalysisResult",
        //                             System.Reflection.BindingFlags.NonPublic
        //                                 | System.Reflection.BindingFlags.Instance
        //                         );
        //                         resultField?.SetValue(windowInstance, result);
        //
        //                         windowInstance.Repaint();
        //                     }
        //                 };
        //             }
        //         }
        //         else
        //         {
        //             EditorUtility.DisplayDialog(
        //                 "分析完成",
        //                 message + "\n\n✓ 所有必要依賴都已存在！",
        //                 "確定"
        //             );
        //         }
        //     }
        //     else if (!string.IsNullOrEmpty(packageJsonPath))
        //     {
        //         EditorUtility.DisplayDialog("錯誤", "請選擇有效的 package.json 檔案。", "確定");
        //     }
        // }

        // [MenuItem(MENU_ROOT + "重置依賴檢查狀態", false, 151)]
        // private static void ResetCheckStatus()
        // {
        //     GitDependencyManager.ResetCheckStatus();
        //     EditorUtility.DisplayDialog(
        //         "重置完成",
        //         "依賴檢查狀態已重置。\n下次啟動 Unity 時將重新檢查依賴。",
        //         "確定"
        //     );
        // }
        //
        // [MenuItem(MENU_ROOT + "生成依賴報告", false, 152)]
        // private static void GenerateDependencyReport()
        // {
        //     GitDependencyManager.GenerateDependencyReport();
        // }

        [MenuItem(MENU_ROOT + "開啟管理視窗", false, 200)]
        private static void OpenManagementWindow()
        {
            GitDependencyWindow.ShowWindow();
        }

        [MenuItem("Window/Package Management/Git Dependency Installer", false, 200)]
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

        // 偵錯選單 (只在 Development Build 中顯示)
        // [MenuItem(MENU_ROOT + "偵錯/清除所有快取", false, 501)]
        // private static void ClearAllCaches()
        // {
        //     GitDependencyInstaller.ClearCache();
        //     GitDependencyManager.ResetCheckStatus();
        //     PackageHelper.ClearCache();
        //
        //     Debug.Log("[GitDependencyMenuItems] 所有快取已清除");
        //     EditorUtility.DisplayDialog(
        //         "快取清除",
        //         "所有 Git Dependencies 相關快取已清除。",
        //         "確定"
        //     );
        // }
        //
        // [MenuItem(MENU_ROOT + "偵錯/清除所有快取", true)]
        // private static bool ValidateClearAllCaches()
        // {
        //     // 只在 Development Build 或 Debug 模式下顯示
        //     return Debug.isDebugBuild || EditorPrefs.GetBool("MonoFSM_DebugMode", false);
        // }

        // [MenuItem(MENU_ROOT + "偵錯/強制重新檢查", false, 502)]
        // private static void ForceRecheck()
        // {
        //     Debug.Log("[GitDependencyMenuItems] 強制重新檢查 Git Dependencies...");
        //     GitDependencyInstaller.ClearCache();
        //     GitDependencyManager.CheckAndPromptForInstallation();
        // }
        //
        // [MenuItem(MENU_ROOT + "偵錯/強制重新檢查", true)]
        // private static bool ValidateForceRecheck()
        // {
        //     return Debug.isDebugBuild || EditorPrefs.GetBool("MonoFSM_DebugMode", false);
        // }
        //
        // [MenuItem(MENU_ROOT + "偵錯/顯示快取資訊", false, 503)]
        // private static void ShowCacheInfo()
        // {
        //     var result = GitDependencyInstaller.GetLastCheckResult();
        //     var lastCheck = EditorPrefs.GetString(
        //         "MonoFSM_GitDependencies_LastChecked",
        //         "從未檢查"
        //     );
        //     var autoInstall = GitDependencyManager.GetAutoInstallEnabled();
        //
        //     var info =
        //         "Git Dependencies 快取資訊:\n\n"
        //         + $"上次檢查: {lastCheck}\n"
        //         + $"自動檢查: {(autoInstall ? "啟用" : "停用")}\n"
        //         + $"快取的依賴數量: {result.gitDependencies?.Count ?? 0}\n"
        //         + $"已安裝依賴: {result.installedDependencies?.Count ?? 0}\n"
        //         + $"缺失依賴: {result.missingDependencies?.Count ?? 0}";
        //
        //     Debug.Log($"[GitDependencyMenuItems] {info}");
        //     EditorUtility.DisplayDialog("快取資訊", info, "確定");
        // }
        //
        // [MenuItem(MENU_ROOT + "偵錯/顯示快取資訊", true)]
        // private static bool ValidateShowCacheInfo()
        // {
        //     return Debug.isDebugBuild || EditorPrefs.GetBool("MonoFSM_DebugMode", false);
        // }
    }
}
