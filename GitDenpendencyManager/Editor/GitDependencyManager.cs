using System.Collections.Generic;
using System.IO;
using System.Linq;
using MonoFSM.Core;
using UnityEditor;
using UnityEngine;

namespace MonoFSM.Utility.Editor
{
    /// <summary>
    /// Git Dependency 管理器 - Editor 專用進階功能
    /// 提供自動檢查、批量安裝、UI 整合等功能
    /// </summary>
    [InitializeOnLoad]
    public static class GitDependencyManager
    {
        private const string DEPENDENCIES_CHECKED_KEY = "MonoFSM_GitDependencies_LastChecked";
        private const string AUTO_INSTALL_KEY = "MonoFSM_GitDependencies_AutoInstall";

        // 預設的關鍵 Git Dependencies（MonoFSM 相關）
        // public static readonly Dictionary<string, string> CriticalGitDependencies = new Dictionary<
        //     string,
        //     string
        // >
        // {
        //     {
        //         "com.cysharp.unitask",
        //         "https://github.com/Cysharp/UniTask.git?path=src/UniTask/Assets/Plugins/UniTask"
        //     },
        //     {
        //         "com.cysharp.zstring",
        //         "https://github.com/Cysharp/ZString.git?path=src/ZString.Unity/Assets/Scripts/ZString"
        //     },
        //     {
        //         "com.github-glitchenzo.nugetforunity",
        //         "https://github.com/GlitchEnzo/NuGetForUnity.git?path=/src/NuGetForUnity"
        //     },
        // };

        static GitDependencyManager()
        {
            EditorApplication.delayCall += OnEditorStartup;
        }

        private static void OnEditorStartup()
        {
            // 檢查是否需要自動檢查 dependencies
            if (ShouldAutoCheckDependencies())
            {
                CheckAndPromptForInstallation();
            }
        }

        /// <summary>
        /// 檢查是否需要自動檢查 dependencies
        /// </summary>
        private static bool ShouldAutoCheckDependencies()
        {
            // 如果從未檢查過，或距離上次檢查超過一天
            var lastChecked = EditorPrefs.GetString(DEPENDENCIES_CHECKED_KEY, "");
            if (string.IsNullOrEmpty(lastChecked))
                return true;

            if (System.DateTime.TryParse(lastChecked, out var lastCheckDate))
            {
                return (System.DateTime.Now - lastCheckDate).TotalDays > 1;
            }

            return true;
        }

        /// <summary>
        /// 檢查並提示安裝
        /// </summary>
        public static void CheckAndPromptForInstallation()
        {
            var checkResult = GitDependencyInstaller.CheckGitDependencies();

            // 更新最後檢查時間
            EditorPrefs.SetString(DEPENDENCIES_CHECKED_KEY, System.DateTime.Now.ToString());

            if (checkResult.missingDependencies.Count > 0)
            {
                ShowInstallationDialog(checkResult);
            }
            else
            {
                Debug.Log("[GitDependencyManager] 所有 Git Dependencies 已正確安裝");
            }
        }

        /// <summary>
        /// 顯示安裝對話框
        /// </summary>
        private static void ShowInstallationDialog(
            GitDependencyInstaller.DependencyCheckResult checkResult
        )
        {
            var missingList = string.Join("\n• ", checkResult.missingDependencies);
            var message =
                $"MonoFSM 檢測到以下 Git Dependencies 尚未安裝：\n\n• {missingList}\n\n"
                + "這些套件對 MonoFSM 的正常運作非常重要。\n\n"
                + "是否要現在自動安裝？";

            if (
                EditorUtility.DisplayDialog(
                    "MonoFSM Git Dependencies",
                    message,
                    "自動安裝",
                    "稍後手動安裝"
                )
            )
            {
                InstallMissingDependenciesWithProgress(checkResult);
            }
            else
            {
                // 顯示手動安裝指南
                ShowManualInstallationGuide(checkResult);
            }
        }

        /// <summary>
        /// 帶進度條的安裝流程
        /// </summary>
        private static void InstallMissingDependenciesWithProgress(
            GitDependencyInstaller.DependencyCheckResult checkResult
        )
        {
            var missingDeps = checkResult.gitDependencies.Where(d => !d.isInstalled).ToList();
            var totalCount = missingDeps.Count;
            var installedCount = 0;

            try
            {
                for (int i = 0; i < missingDeps.Count; i++)
                {
                    var dep = missingDeps[i];
                    var progress = (float)i / totalCount;

                    EditorUtility.DisplayProgressBar(
                        "安裝 Git Dependencies",
                        $"正在安裝: {dep.packageName}...",
                        progress
                    );

                    Debug.Log($"[GitDependencyManager] 正在安裝: {dep.packageName}");

                    var addRequest = UnityEditor.PackageManager.Client.Add(dep.gitUrl);
                    while (!addRequest.IsCompleted)
                    {
                        System.Threading.Thread.Sleep(100);
                        if (
                            EditorUtility.DisplayCancelableProgressBar(
                                "安裝 Git Dependencies",
                                $"正在安裝: {dep.packageName}...",
                                progress
                            )
                        )
                        {
                            Debug.Log("[GitDependencyManager] 用戶取消了安裝流程");
                            break;
                        }
                    }

                    if (addRequest.Status == UnityEditor.PackageManager.StatusCode.Success)
                    {
                        installedCount++;
                        Debug.Log($"[GitDependencyManager] 成功安裝: {dep.packageName}");
                    }
                    else
                    {
                        Debug.LogError(
                            $"[GitDependencyManager] 安裝失敗: {dep.packageName} - {addRequest.Error?.message}"
                        );
                    }
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }

            // 顯示安裝結果
            var resultMessage =
                $"Git Dependencies 安裝完成！\n\n" + $"成功安裝: {installedCount}/{totalCount}\n\n";

            if (installedCount == totalCount)
            {
                resultMessage += "所有依賴已成功安裝，MonoFSM 已準備就緒！";
                EditorUtility.DisplayDialog("安裝成功", resultMessage, "確定");
            }
            else
            {
                resultMessage += "部分依賴安裝失敗，請檢查 Console 查看詳細錯誤訊息。";
                EditorUtility.DisplayDialog("安裝部分成功", resultMessage, "確定");
            }

            // 重新整理 AssetDatabase
            AssetDatabase.Refresh();
        }

        /// <summary>
        /// 顯示手動安裝指南
        /// </summary>
        private static void ShowManualInstallationGuide(
            GitDependencyInstaller.DependencyCheckResult checkResult
        )
        {
            var guide = "MonoFSM Git Dependencies 手動安裝指南：\n\n";
            guide += "1. 開啟 Window > Package Manager\n";
            guide += "2. 點擊左上角的 + 按鈕\n";
            guide += "3. 選擇 'Add package from git URL'\n";
            guide += "4. 依序輸入以下 Git URLs：\n\n";

            foreach (var dep in checkResult.gitDependencies.Where(d => !d.isInstalled))
            {
                guide += $"• {dep.packageName}:\n  {dep.gitUrl}\n\n";
            }

            guide += "5. 等待 Unity 安裝完成\n";
            guide += "6. 重新啟動 Unity Editor（建議）";

            EditorUtility.DisplayDialog("手動安裝指南", guide, "了解");
        }

        /// <summary>
        /// 更新所有本地 packages 的 dependencies
        /// </summary>
        public static void UpdateAllLocalPackageDependencies()
        {
            var localPackages = PackageHelper.GetLocalPackagePaths();
            var gitDependencies = GitDependencyInstaller.CheckGitDependencies();
            var updatedCount = 0;

            foreach (var packagePath in localPackages)
            {
                var packageJsonPath = Path.Combine(
                    PackageHelper.GetPackageFullPath(packagePath),
                    "package.json"
                );

                if (File.Exists(packageJsonPath))
                {
                    var beforeText = File.ReadAllText(packageJsonPath);
                    GitDependencyInstaller.UpdatePackageJsonDependencies(
                        packageJsonPath,
                        gitDependencies.gitDependencies
                    );
                    var afterText = File.Exists(packageJsonPath)
                        ? File.ReadAllText(packageJsonPath)
                        : "";

                    if (beforeText != afterText)
                    {
                        updatedCount++;
                    }
                }
            }

            if (updatedCount > 0)
            {
                Debug.Log(
                    $"[GitDependencyManager] 已更新 {updatedCount} 個本地 package 的 dependencies"
                );
                AssetDatabase.Refresh();
            }
            else
            {
                Debug.Log("[GitDependencyManager] 所有本地 packages 的 dependencies 已是最新狀態");
            }
        }

        /// <summary>
        /// 重置檢查狀態（強制重新檢查）
        /// </summary>
        public static void ResetCheckStatus()
        {
            EditorPrefs.DeleteKey(DEPENDENCIES_CHECKED_KEY);
            GitDependencyInstaller.ClearCache();
            Debug.Log("[GitDependencyManager] 已重置檢查狀態，下次啟動將重新檢查");
        }

        /// <summary>
        /// 取得自動安裝設定
        /// </summary>
        public static bool GetAutoInstallEnabled()
        {
            return EditorPrefs.GetBool(AUTO_INSTALL_KEY, true);
        }

        /// <summary>
        /// 設定自動安裝
        /// </summary>
        public static void SetAutoInstallEnabled(bool enabled)
        {
            EditorPrefs.SetBool(AUTO_INSTALL_KEY, enabled);
        }

        /// <summary>
        /// 生成依賴報告
        /// </summary>
        public static void GenerateDependencyReport()
        {
            var checkResult = GitDependencyInstaller.CheckGitDependencies();
            var report = "=== MonoFSM Git Dependencies 報告 ===\n\n";

            report += $"檢查時間: {System.DateTime.Now:yyyy-MM-dd HH:mm:ss}\n";
            report += $"總計 Git Dependencies: {checkResult.gitDependencies.Count}\n";
            report += $"已安裝: {checkResult.installedDependencies.Count}\n";
            report += $"缺失: {checkResult.missingDependencies.Count}\n\n";

            report += "=== 詳細列表 ===\n";
            foreach (var dep in checkResult.gitDependencies)
            {
                var status = dep.isInstalled ? "✓ 已安裝" : "✗ 缺失";
                report += $"{status} {dep.packageName}\n";
                report += $"  URL: {dep.gitUrl}\n";
                if (dep.isInstalled)
                {
                    report += $"  版本: {dep.installedVersion}\n";
                }
                report += "\n";
            }

            Debug.Log(report);

            // 選擇性寫入檔案
            if (
                EditorUtility.DisplayDialog(
                    "依賴報告",
                    "依賴報告已輸出到 Console。\n是否要同時寫入到檔案？",
                    "寫入檔案",
                    "只顯示在 Console"
                )
            )
            {
                var reportPath = Path.Combine(
                    Application.dataPath,
                    "../MonoFSM_Dependencies_Report.txt"
                );
                File.WriteAllText(reportPath, report);
                Debug.Log($"依賴報告已寫入: {reportPath}");
                EditorUtility.RevealInFinder(reportPath);
            }
        }
    }
}
