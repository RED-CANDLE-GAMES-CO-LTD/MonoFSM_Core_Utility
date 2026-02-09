using UnityEditor;
using UnityEditor.PackageManager;
using UnityEditor.PackageManager.Requests;
using UnityEngine;

namespace MonoFSM.Core
{
    /// <summary>
    /// Git 依賴安裝的共用工具方法
    /// </summary>
    public static partial class GitDependencyInstaller
    {
        /// <summary>
        /// 安裝結果回調
        /// </summary>
        public delegate void InstallCallback(
            bool success,
            string packageName,
            string errorMessage = null
        );

        /// <summary>
        /// 安裝單個依賴的核心方法
        /// </summary>
        /// <param name="dependency">依賴資訊</param>
        /// <param name="callback">安裝完成回調</param>
        /// <param name="showProgress">是否顯示進度</param>
        /// <param name="progressTitle">進度標題</param>
        public static void InstallSingleDependencyCore(
            GitDependencyInfo dependency,
            InstallCallback callback = null,
            bool showProgress = false,
            string progressTitle = "安裝依賴"
        )
        {
            var isGit = IsGitUrl(dependency.gitUrl);
            var identifier = GetPackageIdentifier(dependency, isGit);

            Debug.Log(
                $"[GitDependencyInstaller] 正在安裝: {dependency.packageName} - {identifier}"
            );

            var addRequest = Client.Add(identifier);

            if (showProgress)
            {
                WaitForInstallationWithProgress(addRequest, dependency, callback, progressTitle);
            }
            else
            {
                EditorApplication.delayCall += () =>
                    WaitForInstallationAsync(addRequest, dependency, callback);
            }
        }

        /// <summary>
        /// 取得套件識別符
        /// </summary>
        public static string GetPackageIdentifier(GitDependencyInfo dependency, bool isGit)
        {
            return isGit
                ? dependency.gitUrl
                : $"{dependency.packageName}@{dependency.targetVersion}";
        }

        /// <summary>
        /// 檢查依賴是否需要安裝（共用的安裝判定邏輯）
        /// </summary>
        public static bool ShouldInstallDependency(GitDependencyInfo dependency)
        {
            // 如果已經標記為已安裝，不需要安裝
            if (dependency.isInstalled)
            {
                Debug.Log($"[GitDependencyInstaller] {dependency.packageName} 已安裝，跳過");
                return false;
            }

            // 檢查 URL 有效性
            if (string.IsNullOrEmpty(dependency.gitUrl))
            {
                Debug.LogWarning(
                    $"[GitDependencyInstaller] {dependency.packageName} 的 URL 為空，跳過安裝"
                );
                return false;
            }

            // 對於 Git URL，檢查格式是否正確
            var isGit = IsGitUrl(dependency.gitUrl);
            if (isGit)
            {
                if (!IsValidGitUrl(dependency.gitUrl))
                {
                    Debug.LogWarning(
                        $"[GitDependencyInstaller] {dependency.packageName} 的 Git URL 格式不正確: {dependency.gitUrl}"
                    );
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// 驗證 Git URL 格式
        /// </summary>
        private static bool IsValidGitUrl(string gitUrl)
        {
            if (string.IsNullOrEmpty(gitUrl))
                return false;

            // 基本的 Git URL 格式檢查
            return gitUrl.StartsWith("https://github.com/")
                || gitUrl.StartsWith("git@github.com:")
                || gitUrl.StartsWith("git://")
                || gitUrl.Contains(".git");
        }

        /// <summary>
        /// 異步等待安裝完成
        /// </summary>
        private static void WaitForInstallationAsync(
            AddRequest request,
            GitDependencyInfo dependency,
            InstallCallback callback
        )
        {
            if (!request.IsCompleted)
            {
                EditorApplication.delayCall += () =>
                    WaitForInstallationAsync(request, dependency, callback);
                return;
            }

            HandleInstallationResult(request, dependency, callback);
        }

        /// <summary>
        /// 帶進度條的同步等待安裝完成
        /// </summary>
        private static void WaitForInstallationWithProgress(
            AddRequest request,
            GitDependencyInfo dependency,
            InstallCallback callback,
            string progressTitle
        )
        {
            while (!request.IsCompleted)
            {
                System.Threading.Thread.Sleep(100);

                // 允許用戶取消
                if (
                    EditorUtility.DisplayCancelableProgressBar(
                        progressTitle,
                        $"正在安裝: {dependency.packageName}...",
                        0.5f
                    )
                )
                {
                    Debug.Log(
                        $"[GitDependencyInstaller] 用戶取消了 {dependency.packageName} 的安裝"
                    );
                    EditorUtility.ClearProgressBar();
                    callback?.Invoke(false, dependency.packageName, "用戶取消安裝");
                    return;
                }
            }

            EditorUtility.ClearProgressBar();
            HandleInstallationResult(request, dependency, callback);
        }

        /// <summary>
        /// 處理安裝結果
        /// </summary>
        private static void HandleInstallationResult(
            AddRequest request,
            GitDependencyInfo dependency,
            InstallCallback callback
        )
        {
            if (request.Status == StatusCode.Success)
            {
                Debug.Log($"[GitDependencyInstaller] 成功安裝: {dependency.packageName}");
                callback?.Invoke(true, dependency.packageName);
            }
            else
            {
                var errorMessage = request.Error?.message ?? "未知錯誤";
                Debug.LogError(
                    $"[GitDependencyInstaller] 安裝失敗: {dependency.packageName} - {errorMessage}"
                );
                callback?.Invoke(false, dependency.packageName, errorMessage);
            }
        }

        /// <summary>
        /// 同步安裝單個依賴（用於批量安裝場景）
        /// </summary>
        /// <param name="dependency">依賴資訊</param>
        /// <returns>安裝是否成功</returns>
        public static bool InstallSingleDependencySync(GitDependencyInfo dependency)
        {
            if (!ShouldInstallDependency(dependency))
            {
                return false;
            }

            var isGit = IsGitUrl(dependency.gitUrl);
            var identifier = GetPackageIdentifier(dependency, isGit);

            Debug.Log(
                $"[GitDependencyInstaller] 正在安裝: {dependency.packageName} - {identifier}"
            );

            var addRequest = Client.Add(identifier);
            while (!addRequest.IsCompleted)
            {
                System.Threading.Thread.Sleep(50);
            }

            if (addRequest.Status == StatusCode.Success)
            {
                Debug.Log($"[GitDependencyInstaller] 成功安裝: {dependency.packageName}");
                return true;
            }
            else
            {
                var errorMessage = addRequest.Error?.message ?? "未知錯誤";
                Debug.LogError(
                    $"[GitDependencyInstaller] 安裝失敗: {dependency.packageName} - {errorMessage}"
                );
                return false;
            }
        }
    }
}
