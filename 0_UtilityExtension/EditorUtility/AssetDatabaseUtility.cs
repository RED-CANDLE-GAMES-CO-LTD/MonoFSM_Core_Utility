using System;
using System.Diagnostics;
using System.IO;
using UnityEngine;
using Debug = UnityEngine.Debug;
using Object = UnityEngine.Object;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
#endif

namespace MonoFSM.Core
{
    public static class AssetDatabaseUtility
    {
#if UNITY_EDITOR

        public delegate T AssetCreateDelegate<out T>(string prefabPath)
            where T : UnityEngine.Object;

        //把目標asset複製到prefab所在的資料夾
        public static T CopyAssetOrCreateToPrefabFolder<T>(
            T oriAsset,
            string assetExtension,
            AssetCreateDelegate<T> customAssetCreationMethod
        )
            where T : UnityEngine.Object
        {
            var prefabStage = PrefabStageUtility.GetCurrentPrefabStage();
            if (prefabStage == null)
            {
                Debug.LogError("Not in prefab stage");
                return null;
            }

            var prefabPath = PrefabStageUtility.GetCurrentPrefabStage().assetPath;
            var prefabFolderPath = Path.GetDirectoryName(prefabPath);

            if (oriAsset == null)
            {
                //create new asset
                if (customAssetCreationMethod != null)
                {
                    Debug.Log("Create new asset");

                    var fileName = Path.GetFileName(prefabPath);
                    //remove extension
                    fileName = fileName.Substring(
                        0,
                        fileName.Length - Path.GetExtension(fileName).Length
                    );
                    var createFilePath = prefabFolderPath + "/" + fileName + assetExtension;

                    //從外部傳進來，特殊的create法
                    var obj = customAssetCreationMethod.Invoke(createFilePath);
                    if (obj != null && string.IsNullOrEmpty(AssetDatabase.GetAssetPath(obj)))
                        AssetDatabase.CreateAsset(obj, createFilePath);
                    return obj;
                }

                return null;
            }

            var originalPath = AssetDatabase.GetAssetPath(oriAsset);

            if (Path.GetDirectoryName(originalPath) == prefabFolderPath)
            {
                Debug.LogError("Same Folder, Move Prefab to another folder");
                return null;
            }

            //extension of asset
            var extension = Path.GetExtension(originalPath);
            var newFilePath = prefabFolderPath + "/" + oriAsset.name + " Copied " + extension;
            Debug.Log("Copy Asset to:" + newFilePath);
            AssetDatabase.CopyAsset(originalPath, newFilePath);
            var newAsset = AssetDatabase.LoadAssetAtPath<T>(newFilePath);
            //生asset沒有得undo唷
            // Undo.RegisterCreatedObjectUndo(newAsset, "Copy Asset");
            return newAsset;
        }

        private static void CreateFolderIfNotExist(string folderPath)
        {
            if (!System.IO.Directory.Exists(folderPath))
            {
                System.IO.Directory.CreateDirectory(folderPath);
            }
        }

        public static T CreateAsset<T>(string folderPath, string fileName)
            where T : ScriptableObject
        {
            UnityEditor.EditorUtility.ClearProgressBar();
            UnityEditor.EditorUtility.DisplayProgressBar("CreateAsset", fileName, 0.5f);
            CreateFolderIfNotExist(folderPath);

            var data = AssetDatabase.LoadAssetAtPath<T>(folderPath + "/" + fileName + ".asset");
            if (data != null)
            {
                Debug.LogWarning("data already exist");
                UnityEditor.EditorUtility.ClearProgressBar();
                return data;
            }

            var asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, folderPath + "/" + fileName + ".asset");
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            UnityEditor.EditorUtility.ClearProgressBar();
            return asset;
        }
#endif

        [Conditional("UNITY_EDITOR")]
        public static void SetDirty(this Object obj)
        {
#if UNITY_EDITOR
            UnityEditor.EditorUtility.SetDirty(obj);
#endif
        }

        //"Resources/Configs"


#if UNITY_EDITOR
        public static string GetAssetGUID(this Object obj)
        {
            AssetDatabase.TryGetGUIDAndLocalFileIdentifier(obj, out var guid, out long localId);
            return guid;
        }

        //單純給任何scriptable object用
        public static ScriptableObject CreateScriptableObject(
            this Type type,
            string fileRelativePath
        )
        {
            CreateAssetFolderIfParentNotExist(fileRelativePath);
            //check if file exist
            var asset = AssetDatabase.LoadAssetAtPath<ScriptableObject>(
                "Assets/" + fileRelativePath
            );
            if (asset != null)
            {
                Debug.Log("File already exist, linked" + fileRelativePath);
                return asset;
            }

            asset = ScriptableObject.CreateInstance(type);
            if (asset == null)
            {
                Debug.LogError($"Failed to create ScriptableObject of type {type}");
                return null;
            }
            AssetDatabase.CreateAsset(asset, "Assets/" + fileRelativePath);
            //[]: 這個不call OK 嗎？
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            return asset;
        }

        /// <summary>
        /// 建立 ScriptableObject 到指定的路徑（支援 Assets 和 Package）
        /// </summary>
        /// <param name="type">ScriptableObject 型別</param>
        /// <param name="targetPath">目標路徑 ("Assets" 或 "Packages/package-name")</param>
        /// <param name="fileRelativePath">相對於目標路徑的檔案路徑</param>
        /// <param name="fileName"></param>
        /// <returns>建立的 ScriptableObject</returns>
        public static ScriptableObject CreateScriptableObjectAt(
            this Type type,
            string targetPath,
            string fileRelativePath,
            string fileName
        )
        {
            // 組合完整路徑
            string fullPath;
            if (targetPath == "Assets")
            {
                fullPath = "Assets/" + fileRelativePath;
                CreateAssetFolderIfParentNotExist(fileRelativePath);
            }
            else if (targetPath.StartsWith("Packages/"))
            {
                fullPath = targetPath + "/" + fileRelativePath;
                CreatePackageFolderIfParentNotExist(targetPath, fileRelativePath);
            }
            else
            {
                Debug.LogError($"不支援的目標路徑: {targetPath}");
                return null;
            }

            fullPath += "/" + fileName + ".asset";

            // 檢查檔案是否已存在
            var asset = AssetDatabase.LoadAssetAtPath<ScriptableObject>(fullPath);
            if (asset != null)
            {
                Debug.Log($"檔案已存在，連結現有檔案: {fullPath}");
                return asset;
            }

            // 建立新的 ScriptableObject
            asset = ScriptableObject.CreateInstance(type);
            if (asset == null)
            {
                Debug.LogError($"Failed to ScriptableObject.CreateInstance of type {type}");
                return null;
            }

            Debug.Log(type);

            AssetDatabase.CreateAsset(asset, fullPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"已建立 ScriptableObject: {fullPath}");
            return asset;
        }

        [Conditional("UNITY_EDITOR")]
        private static void CreateAssetFolderIfParentNotExist(string fileRelativePath)
        {
            // var folderRelativePath = fileRelativePath.FolderPath();
            CreateFolderAtPathRecursive("Assets", fileRelativePath);
        }

        /// <summary>
        /// 在 Package 內建立資料夾（如果父資料夾不存在）
        /// </summary>
        [Conditional("UNITY_EDITOR")]
        private static void CreatePackageFolderIfParentNotExist(
            string packagePath,
            string fileRelativePath
        )
        {
            // var folderRelativePath = fileRelativePath.FolderPath();
            CreateFolderAtPathRecursive(packagePath, fileRelativePath);
        }

        /// <summary>
        /// 通用的資料夾創建方法，支援 Assets 和 Package 路徑
        /// </summary>
        [Conditional("UNITY_EDITOR")]
        private static void CreateFolderAtPathRecursive(string basePath, string folderPath)
        {
            // 如果 folderPath 為空，直接返回
            if (string.IsNullOrEmpty(folderPath))
                return;

            var folders = folderPath.Split('/');
            var currentPath = basePath;

            for (var i = 0; i < folders.Length; i++)
            {
                var folder = folders[i];

                if (!string.IsNullOrEmpty(folder))
                {
                    var newPath = currentPath + "/" + folder;

                    // 檢查資料夾是否存在，不存在就創建
                    if (!AssetDatabase.IsValidFolder(newPath))
                        AssetDatabase.CreateFolder(currentPath, folder);

                    currentPath = newPath;
                }
            }
        }

        /// <summary>
        /// [已棄用] 使用 CreateFolderAtPathRecursive 替代
        /// </summary>
        [Conditional("UNITY_EDITOR")]
        [Obsolete("Use CreateFolderAtPathRecursive instead")]
        private static void CreateAssetFolderAtPathRecursive(string folderPath)
        {
            CreateFolderAtPathRecursive("Assets", folderPath);
        }

        /// <summary>
        /// [已棄用] 使用 CreateFolderAtPathRecursive 替代
        /// </summary>
        [Conditional("UNITY_EDITOR")]
        [Obsolete("Use CreateFolderAtPathRecursive instead")]
        private static void CreatePackageFolderAtPathRecursive(
            string packagePath,
            string folderPath
        )
        {
            CreateFolderAtPathRecursive(packagePath, folderPath);
        }
#endif
    }
}
