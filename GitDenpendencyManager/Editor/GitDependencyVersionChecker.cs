using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MonoFSM.Core;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEngine;

namespace MonoFSM.Utility.Editor
{
    /// <summary>
    ///     版本問題項目
    /// </summary>
    [Serializable]
    public class VersionIssueItem
    {
        public string packageName;
        public string issueType; // "GitUpdate", "VersionMismatch"
        public string currentVersion;
        public string targetVersion;
        public string description;
        public string packagePath;

        public VersionIssueItem(string packageName, string issueType, string currentVersion, string targetVersion,
            string description, string packagePath = "")
        {
            this.packageName = packageName;
            this.issueType = issueType;
            this.currentVersion = currentVersion;
            this.targetVersion = targetVersion;
            this.description = description;
            this.packagePath = packagePath;
        }
    }

    /// <summary>
    ///     版本檢查結果
    /// </summary>
    [Serializable]
    public class VersionCheckResult
    {
        public List<VersionIssueItem> issues = new();
        public int gitUpdateCount;
        public int versionMismatchCount;

        public int TotalIssues => issues.Count;
        public bool HasIssues => issues.Count > 0;
    }

    //FIXME: 會有這個需要嗎...
    /// <summary>
    ///     Git Dependencies 版本檢查器 - 背景定期檢查版本更新
    /// </summary>
    [InitializeOnLoad]
    public static class GitDependencyVersionChecker
    {
        private static bool isBackgroundCheckEnabled = true;
        private static double nextCheckTime;
        private static readonly double checkIntervalMinutes = 30; // 30分鐘檢查一次

        static GitDependencyVersionChecker()
        {
            // 從EditorPrefs讀取設定
            isBackgroundCheckEnabled = EditorPrefs.GetBool("GitDependency.BackgroundCheckEnabled", false);

            if (isBackgroundCheckEnabled) StartBackgroundCheck();
        }

        /// <summary>
        ///     啟動背景檢查
        /// </summary>
        public static void StartBackgroundCheck()
        {
            if (!isBackgroundCheckEnabled)
                return;

            EditorApplication.update += BackgroundUpdateCheck;
            nextCheckTime = EditorApplication.timeSinceStartup + checkIntervalMinutes * 60;

            Debug.Log($"[GitDependencyVersionChecker] 背景版本檢查已啟動，將每 {checkIntervalMinutes} 分鐘檢查一次");
        }

        /// <summary>
        ///     停止背景檢查
        /// </summary>
        public static void StopBackgroundCheck()
        {
            EditorApplication.update -= BackgroundUpdateCheck;
            Debug.Log("[GitDependencyVersionChecker] 背景版本檢查已停止");
        }

        /// <summary>
        ///     立即執行版本檢查（測試用）
        /// </summary>
        [MenuItem("Tools/MonoFSM/立即檢查版本問題")]
        public static void PerformManualVersionCheck()
        {
            Debug.Log("[GitDependencyVersionChecker] 手動觸發版本檢查...");
            PerformVersionCheck();
        }

        /// <summary>
        ///     設定背景檢查是否啟用
        /// </summary>
        public static void SetBackgroundCheckEnabled(bool enabled)
        {
            isBackgroundCheckEnabled = enabled;
            EditorPrefs.SetBool("GitDependency.BackgroundCheckEnabled", enabled);

            if (enabled)
                StartBackgroundCheck();
            else
                StopBackgroundCheck();
        }

        /// <summary>
        ///     獲取背景檢查是否啟用
        /// </summary>
        public static bool IsBackgroundCheckEnabled()
        {
            return isBackgroundCheckEnabled;
        }

        /// <summary>
        ///     設定檢查間隔（分鐘）
        /// </summary>
        public static void SetCheckInterval(double intervalMinutes)
        {
            // 這裡可以擴展為可配置的間隔時間
            Debug.Log($"[GitDependencyVersionChecker] 檢查間隔設定為 {intervalMinutes} 分鐘");
        }

        /// <summary>
        ///     背景更新檢查
        /// </summary>
        private static void BackgroundUpdateCheck()
        {
            if (!isBackgroundCheckEnabled)
            {
                StopBackgroundCheck();
                return;
            }

            // 檢查是否到了檢查時間
            if (EditorApplication.timeSinceStartup >= nextCheckTime)
            {
                PerformVersionCheck();
                nextCheckTime = EditorApplication.timeSinceStartup + checkIntervalMinutes * 60;
            }
        }

        /// <summary>
        ///     執行版本檢查 - 檢查 Git Dependencies 和所有 Local Packages
        /// </summary>
        private static void PerformVersionCheck()
        {
            try
            {
                Debug.Log("[GitDependencyVersionChecker] 開始全面版本檢查...");

                var result = PerformDetailedVersionCheck();

                if (result.HasIssues)
                {
                    Debug.Log("[GitDependencyVersionChecker] 發現版本問題：" +
                              $"Git 更新: {result.gitUpdateCount}, Local Package 版本不匹配: {result.versionMismatchCount}");

                    ShowDetailedUpdateNotification(result);
                }
                else
                {
                    Debug.Log("[GitDependencyVersionChecker] 所有依賴和 local packages 都是最新狀態");
                    EditorUtility.DisplayDialog("版本檢查結果", "所有依賴和 local packages 都是最新狀態", "確定");
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[GitDependencyVersionChecker] 版本檢查發生錯誤: {ex.Message}");
            }
        }

        /// <summary>
        ///     執行詳細的版本檢查，返回具體的問題清單
        /// </summary>
        public static VersionCheckResult PerformDetailedVersionCheck()
        {
            var result = new VersionCheckResult();

            try
            {
                // 1. 檢查主專案的 Git Dependencies
                var mainResult = GitDependencyInstaller.CheckGitDependencies();
                if (mainResult?.gitDependencies != null)
                    foreach (var dep in mainResult.gitDependencies)
                        if (dep.HasVersionUpdate())
                        {
                            result.issues.Add(new VersionIssueItem(
                                dep.packageName,
                                "GitUpdate",
                                dep.installedVersion,
                                dep.targetVersion,
                                $"Git 套件 {dep.packageName} 有更新版本可用"
                            ));
                            result.gitUpdateCount++;
                        }

                // 2. 檢查所有 Local Packages 的版本不匹配
                var localPackageIssues = CheckAllLocalPackagesVersionMismatchDetailed();
                result.issues.AddRange(localPackageIssues);
                result.versionMismatchCount = localPackageIssues.Count;

                Debug.Log($"[GitDependencyVersionChecker] 檢查完成，發現 {result.TotalIssues} 個問題");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[GitDependencyVersionChecker] 詳細版本檢查發生錯誤: {ex.Message}");
            }

            return result;
        }

        /// <summary>
        ///     檢查所有 Local Packages 的版本不匹配情況 - 返回詳細問題清單
        /// </summary>
        private static List<VersionIssueItem> CheckAllLocalPackagesVersionMismatchDetailed()
        {
            var issues = new List<VersionIssueItem>();

            try
            {
                // 尋找所有 local packages (在 Packages 資料夾或 Submodules 中)
                var localPackagePaths = FindAllLocalPackages();

                foreach (var packagePath in localPackagePaths)
                    try
                    {
                        var analysisResult = AssemblyDependencyAnalyzer.AnalyzePackageDependencies(packagePath);

                        if (analysisResult.versionMismatchDependencies.Count > 0)
                        {
                            var packageName = Path.GetFileName(Path.GetDirectoryName(packagePath));

                            foreach (var mismatch in analysisResult.versionMismatchDependencies)
                                issues.Add(new VersionIssueItem(
                                    mismatch.packageName,
                                    "VersionMismatch",
                                    mismatch.versionInPackageJson,
                                    mismatch.versionInPackageManager,
                                    $"套件 {packageName} 中的 {mismatch.packageName} 版本不匹配 ({mismatch.versionInPackageJson} → {mismatch.versionInPackageManager})",
                                    packagePath
                                ));
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.LogError($"[GitDependencyVersionChecker] 分析 package {packagePath} 時發生錯誤: {ex.Message}");
                    }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[GitDependencyVersionChecker] 檢查 local packages 時發生錯誤: {ex.Message}");
            }

            return issues;
        }

        /// <summary>
        ///     檢查所有 Local Packages 的版本不匹配情況 - 只返回數量（向後相容）
        /// </summary>
        private static int CheckAllLocalPackagesVersionMismatch()
        {
            var mismatchCount = 0;

            try
            {
                // 尋找所有 local packages (在 Packages 資料夾或 Submodules 中)
                var localPackagePaths = FindAllLocalPackages();

                foreach (var packagePath in localPackagePaths)
                    try
                    {
                        var analysisResult = AssemblyDependencyAnalyzer.AnalyzePackageDependencies(packagePath);

                        if (analysisResult.versionMismatchDependencies.Count > 0)
                        {
                            mismatchCount += analysisResult.versionMismatchDependencies.Count;
                            Debug.Log($"[GitDependencyVersionChecker] {analysisResult.targetPackageName}: " +
                                      $"發現 {analysisResult.versionMismatchDependencies.Count} 個版本不匹配");
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.LogWarning($"[GitDependencyVersionChecker] 分析 {packagePath} 時發生錯誤: {ex.Message}");
                    }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[GitDependencyVersionChecker] 檢查 local packages 時發生錯誤: {ex.Message}");
            }

            return mismatchCount;
        }

        /// <summary>
        ///     尋找所有 Local Packages
        /// </summary>
        private static List<string> FindAllLocalPackages()
        {
            var packagePaths = new List<string>();

            try
            {
                // 使用 PackageHelper 取得所有已安裝的本地套件
                var allPackages = PackageHelper.GetAllPackages();
                var localPackages = allPackages.Where(p => p.source == PackageSource.Local);

                foreach (var package in localPackages)
                    if (!string.IsNullOrEmpty(package.resolvedPath))
                    {
                        var packageJsonPath = Path.Combine(package.resolvedPath, "package.json");
                        if (File.Exists(packageJsonPath)) packagePaths.Add(packageJsonPath);
                    }

                // 額外檢查 Submodules 資料夾中可能未被 Package Manager 識別的 packages
                var submodulesDir = Path.Combine(Application.dataPath, "../Submodules");
                if (Directory.Exists(submodulesDir))
                {
                    var submodulePackageJsonFiles =
                        Directory.GetFiles(submodulesDir, "package.json", SearchOption.AllDirectories);
                    packagePaths.AddRange(submodulePackageJsonFiles);
                }

                // 去重
                return packagePaths.Distinct().Where(p => File.Exists(p)).ToList();
            }
            catch (Exception ex)
            {
                Debug.LogError($"[GitDependencyVersionChecker] 取得本地套件時發生錯誤: {ex.Message}");
                return new List<string>();
            }
        }

        /// <summary>
        ///     顯示詳細的更新通知和修復選項
        /// </summary>
        private static void ShowDetailedUpdateNotification(VersionCheckResult result)
        {
            var message = $"發現 {result.TotalIssues} 個版本問題：\n\n";

            // 列出前5個問題
            var displayCount = Mathf.Min(5, result.issues.Count);
            for (var i = 0; i < displayCount; i++)
            {
                var issue = result.issues[i];
                message += $"• {issue.packageName} - {issue.description}\n";
            }

            if (result.issues.Count > 5) message += $"... 還有 {result.issues.Count - 5} 個問題\n\n";

            message += "\n選擇處理方式：";

            var choice = EditorUtility.DisplayDialogComplex(
                "版本問題檢測",
                message,
                "一鍵修復全部", // 0
                "打開管理器", // 1
                "稍後處理" // 2
            );

            switch (choice)
            {
                case 0: // 一鍵修復
                    PerformOneClickFix(result);
                    break;
                case 1: // 打開管理器
                    var window = GitDependencyWindow.ShowWindow();
                    window.SetActiveTab(0);
                    break;
                case 2: // 稍後處理
                default:
                    break;
            }
        }

        /// <summary>
        ///     執行一鍵修復
        /// </summary>
        private static void PerformOneClickFix(VersionCheckResult result)
        {
            try
            {
                Debug.Log("[GitDependencyVersionChecker] 開始一鍵修復...");

                var fixedCount = 0;
                var errors = new List<string>();

                foreach (var issue in result.issues)
                    try
                    {
                        switch (issue.issueType)
                        {
                            case "GitUpdate":
                                // 對於 Git 更新，觸發完整的依賴檢查和安裝
                                Debug.Log($"[GitDependencyVersionChecker] 更新 Git 依賴: {issue.packageName}");
                                var checkResult = GitDependencyInstaller.CheckGitDependencies();
                                if (checkResult != null)
                                    GitDependencyInstaller.InstallMissingGitDependencies(checkResult);
                                fixedCount++;
                                break;

                            case "VersionMismatch":
                                // 對於版本不匹配，更新 package.json 中的版本
                                Debug.Log($"[GitDependencyVersionChecker] 修復版本不匹配: {issue.packageName}");
                                FixVersionMismatch(issue);
                                fixedCount++;
                                break;
                        }
                    }
                    catch (Exception ex)
                    {
                        var errorMsg = $"修復 {issue.packageName} 時發生錯誤: {ex.Message}";
                        errors.Add(errorMsg);
                        Debug.LogError($"[GitDependencyVersionChecker] {errorMsg}");
                    }

                // 顯示修復結果
                var resultMessage = $"一鍵修復完成！\n\n成功修復: {fixedCount} 個問題";
                if (errors.Count > 0)
                {
                    resultMessage += $"\n失敗: {errors.Count} 個問題\n\n錯誤詳情:\n";
                    resultMessage += string.Join("\n", errors.Take(3));
                    if (errors.Count > 3)
                        resultMessage += $"\n... 還有 {errors.Count - 3} 個錯誤";
                }

                EditorUtility.DisplayDialog("修復結果", resultMessage, "確定");

                // 重新檢查
                if (fixedCount > 0)
                {
                    Debug.Log("[GitDependencyVersionChecker] 修復完成後重新檢查版本狀態...");
                    EditorApplication.delayCall += () => PerformVersionCheck();
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[GitDependencyVersionChecker] 一鍵修復發生錯誤: {ex.Message}");
                EditorUtility.DisplayDialog("修復失敗", $"一鍵修復過程中發生錯誤：\n{ex.Message}", "確定");
            }
        }

        /// <summary>
        ///     修復版本不匹配問題
        /// </summary>
        private static void FixVersionMismatch(VersionIssueItem issue)
        {
            if (string.IsNullOrEmpty(issue.packagePath))
                return;

            try
            {
                Debug.Log(
                    $"[GitDependencyVersionChecker] 正在修復版本不匹配: {issue.packageName} ({issue.currentVersion} → {issue.targetVersion})");

                // 讀取並更新 package.json 中的 Git URL 版本號
                GitDependencyInstaller.UpdatePackageJsonDependencies(
                    issue.packagePath,
                    new List<GitDependencyInstaller.GitDependencyInfo>
                    {
                        new(issue.packageName, "") // 空的 gitUrl，讓方法自動從現有 URL 中更新版本號
                        {
                            targetVersion = issue.targetVersion
                        }
                    }
                );

                Debug.Log($"[GitDependencyVersionChecker] 已更新 {issue.packageName} 的版本至 {issue.targetVersion}");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[GitDependencyVersionChecker] 修復版本不匹配失敗 ({issue.packageName}): {ex.Message}");
                throw;
            }
        }

        /// <summary>
        ///     顯示綜合更新通知
        /// </summary>
        private static void ShowComprehensiveUpdateNotification(int gitUpdatesCount, int localPackagesMismatchCount)
        {
            string message;
            string title;

            if (gitUpdatesCount > 0 && localPackagesMismatchCount > 0)
            {
                title = "發現版本問題";
                message = "發現版本問題：\n\n" +
                          $"• {gitUpdatesCount} 個 Git 依賴有更新\n" +
                          $"• {localPackagesMismatchCount} 個 Local Package 版本不匹配\n\n" +
                          "是否打開 Git Dependencies 管理器處理？";
            }
            else if (gitUpdatesCount > 0)
            {
                title = "Git 依賴更新通知";
                message = $"發現 {gitUpdatesCount} 個 Git 依賴有更新！\n是否打開 Git Dependencies 管理器？";
            }
            else
            {
                title = "Local Package 版本問題";
                message = $"發現 {localPackagesMismatchCount} 個 Local Package 版本不匹配！\n" +
                          "這些 packages 的 package.json 中記錄的依賴版本與實際安裝版本不一致。\n\n" +
                          "是否打開 Assembly Analysis 管理器處理？";
            }

            if (EditorUtility.DisplayDialog(title, message, "打開管理器", "稍後處理"))
            {
                var window = GitDependencyWindow.ShowWindow();

                // 根據問題類型選擇適當的 tab
                if (localPackagesMismatchCount > 0)
                    window.SetActiveTab(1); // Assembly Analysis tab
                else
                    window.SetActiveTab(0); // Git Dependencies tab
            }
        }

        /// <summary>
        ///     立即執行版本檢查（手動觸發）
        /// </summary>
        [MenuItem("Tools/MonoFSM/Dependencies/立即檢查版本更新", false, 200)]
        public static void ManualVersionCheck()
        {
            Debug.Log("[GitDependencyVersionChecker] 手動觸發版本檢查");
            PerformVersionCheck();
        }

        /// <summary>
        ///     開啟/關閉背景檢查設定
        /// </summary>
        [MenuItem("Tools/MonoFSM/Dependencies/背景版本檢查設定", false, 201)]
        public static void ShowBackgroundCheckSettings()
        {
            var currentEnabled = IsBackgroundCheckEnabled();
            var newEnabled = EditorUtility.DisplayDialog(
                "背景版本檢查設定",
                $"目前狀態: {(currentEnabled ? "已啟用" : "已停用")}\n\n" +
                $"檢查間隔: {checkIntervalMinutes} 分鐘\n\n" +
                "是否要改變設定？",
                currentEnabled ? "停用背景檢查" : "啟用背景檢查",
                "取消"
            );

            if (newEnabled)
            {
                SetBackgroundCheckEnabled(!currentEnabled);
                EditorUtility.DisplayDialog(
                    "設定已更改",
                    $"背景版本檢查已{(!currentEnabled ? "啟用" : "停用")}",
                    "確定"
                );
            }
        }
    }
}