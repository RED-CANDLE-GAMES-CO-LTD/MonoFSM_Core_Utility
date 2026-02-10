using System.Collections.Generic;
using System.IO;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
#endif

namespace MonoFSM.Core
{
#if UNITY_EDITOR
    public static class AnimatorControllerUtility
    {
        [MenuItem("CONTEXT/Animator/Duplicate Animator Override Controller")]
        public static void GenerateAnimatorOverrideController(MenuCommand command)
        {
            var animator = command.context as Animator;
            if (animator == null)
            {
                Debug.LogError("Can't find Animator");
                return;
            }

            var prefabPath = "";
            var prefabStage = PrefabStageUtility.GetCurrentPrefabStage();
            if (prefabStage)
                prefabPath = prefabStage.assetPath;
            else
                prefabPath =
                    AssetDatabase.GetAssetPath(PrefabUtility.GetCorrespondingObjectFromSource(animator.gameObject));

            Undo.RecordObject(animator, "Generate Variant");

            var folderPath = Path.GetDirectoryName(prefabPath);
            // var newAssetPath = Path.Combine(folderPath, animator.gameObject.name + ".overrideController");
            if (animator.runtimeAnimatorController != null)
            {
                if (animator.runtimeAnimatorController is AnimatorOverrideController)
                {
                    // 已經是 OverrideController，複製一份
                    var originalAssetPath = AssetDatabase.GetAssetPath(animator.runtimeAnimatorController);
                    var originalAssetName = Path.GetFileName(originalAssetPath);
                    var newAssetPath = Path.Combine(folderPath, "Copied " + originalAssetName);
                    Debug.Log(newAssetPath);
                    AssetDatabase.CopyAsset(originalAssetPath, newAssetPath);
                    var newOverrideController = AssetDatabase.LoadAssetAtPath<AnimatorOverrideController>(newAssetPath);
                    CopyAllOverrideClipsToControllerFolder(newOverrideController);
                    animator.runtimeAnimatorController = newOverrideController;
                }
                else
                {
                    // 是普通 AnimatorController，建立新的 OverrideController 指向它
                    var sourceController = animator.runtimeAnimatorController as AnimatorController;
                    var newAssetPath = Path.Combine(folderPath, animator.gameObject.name + ".overrideController");
                    Debug.Log($"Creating new OverrideController at: {newAssetPath}");
                    var newOverrideController = new AnimatorOverrideController(sourceController);
                    AssetDatabase.CreateAsset(newOverrideController, newAssetPath);
                    animator.runtimeAnimatorController = AssetDatabase.LoadAssetAtPath<AnimatorOverrideController>(newAssetPath);
                }

                animator.SetDirty();
                AssetDatabase.SaveAssets();
            }
            // Undo.FlushUndoRecordObjects();
        }

        //copy all override clips to the same folder of the override controller
        private static void CopyAllOverrideClipsToControllerFolder(AnimatorOverrideController overrideController)
        {
            var folderPath = Path.GetDirectoryName(AssetDatabase.GetAssetPath(overrideController));
            var overrides = new List<KeyValuePair<AnimationClip, AnimationClip>>(overrideController.overridesCount);
            overrideController.GetOverrides(overrides);
            for (var i = 0; i < overrides.Count; ++i)
            {
                if (!overrides[i].Value) continue; //有override的clip

                var clipPath = AssetDatabase.GetAssetPath(overrides[i].Value);
                var clipFolder = Path.GetDirectoryName(clipPath);
                if (folderPath != clipFolder)
                {
                    AssetDatabase.CopyAsset(clipPath, Path.Combine(folderPath, Path.GetFileName(clipPath)));
                    var newClip =
                        AssetDatabase.LoadAssetAtPath<AnimationClip>(Path.Combine(folderPath,
                            Path.GetFileName(clipPath)));
                    overrideController[overrides[i].Key] = newClip;
                }
            }
        }


        //generate a new animator controller and assign it to the animator
        [MenuItem("CONTEXT/Animator/Create Or Copy AnimatorController")] //給prefab用的
        public static void CreateOrCopyAnimatorController(MenuCommand command)
        {
            var animator = command.context as Animator;
            if (animator == null)
            {
                Debug.LogError("Can't find Animator");
                return;
            }

            CreateAnimatorControllerForAnimatorOfCurrentPrefab(animator);

            // Undo.FlushUndoRecordObjects();
        }

        /// <summary>
        /// 核心方法：為Animator創建或複製AnimatorController
        /// 參考GenerateAnimatorOverrideController和CreateOrCopyAnimatorController的成功模式
        /// </summary>
        private static AnimatorController CreateAnimatorControllerCore(Animator animator, bool useGenericPath = false, string customFolderPath = null)
        {
            if (animator == null)
            {
                Debug.LogError("Animator is null");
                return null;
            }

            // 參考GenerateAnimatorOverrideController的路徑獲取方式
            string prefabPath;
            var prefabStage = PrefabStageUtility.GetCurrentPrefabStage();
            
            if (useGenericPath && !string.IsNullOrEmpty(customFolderPath))
            {
                // 使用自定義路徑
                prefabPath = Path.Combine(customFolderPath, "dummy.prefab"); // 需要一個dummy檔名給GetDirectoryName用
            }
            else if (prefabStage)
            {
                prefabPath = prefabStage.assetPath;
            }
            else
            {
                prefabPath = AssetDatabase.GetAssetPath(PrefabUtility.GetCorrespondingObjectFromSource(animator.gameObject));
                if (string.IsNullOrEmpty(prefabPath))
                {
                    EditorUtility.DisplayDialog("Animator Controller Creation Error",
                        "The Animator is not part of a prefab. Please ensure the Animator is attached to a prefab instance.",
                        "OK");
                    return null;
                }
            }

            Undo.RecordObject(animator, "Create Animator Controller");

            var folderPath = Path.GetDirectoryName(prefabPath);
            // 使用Prefab根物件的名稱，而不是Animator GameObject的名稱
            var prefabRootName = prefabStage
                ? prefabStage.prefabContentsRoot.name
                : Path.GetFileNameWithoutExtension(prefabPath);
            var controllerName = prefabRootName;
            var controllerPath = Path.Combine(folderPath, controllerName + ".controller");
            
            AnimatorController newController;
            
            // 檢查路徑上是否已經存在同名的controller
            if (File.Exists(controllerPath))
            {
                Debug.Log($"Found existing controller at: {controllerPath}, using it directly");
                newController = AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
            }
            else
            {
                Debug.Log($"Creating controller at: {controllerPath}");
                
                // 如果已有controller，複製它；否則創建新的
                if (animator.runtimeAnimatorController != null)
                {
                    var originalPath = AssetDatabase.GetAssetPath(animator.runtimeAnimatorController);
                    Debug.Log($"Copying existing controller from: {originalPath}");
                    AssetDatabase.CopyAsset(originalPath, controllerPath);
                    newController = AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
                }
                else
                {
                    Debug.Log("Creating new controller");
                    newController = AnimatorController.CreateAnimatorControllerAtPath(controllerPath);
                }
            }

            if (newController == null)
            {
                Debug.LogError("Failed to create or load AnimatorController");
                return null;
            }

            // 參考GenerateAnimatorOverrideController的assignment方式
            animator.runtimeAnimatorController = newController;
            animator.SetDirty();
            AssetDatabase.SaveAssets();

            Debug.Log($"✓ Successfully created and assigned controller: {newController.name}");
            return newController;
        }

        /// <summary>
        /// 為指定的Animator創建AnimatorController（在Prefab Stage中使用）
        /// </summary>
        public static AnimatorController CreateAnimatorControllerForAnimatorOfCurrentPrefab(Animator animator)
        {
            return CreateAnimatorControllerCore(animator, useGenericPath: false);
        }

        /// <summary>
        /// 為指定的Animator創建AnimatorController（在Prefab Stage中使用）
        /// 與CreateAnimatorControllerForAnimatorOfCurrentPrefab功能相同，保留作為alias
        /// </summary>
        public static AnimatorController CreateAnimatorControllerForAnimator(Animator animator)
        {
            return CreateAnimatorControllerForAnimatorOfCurrentPrefab(animator);
        }

        /// <summary>
        /// 為Animator創建AnimatorController（通用方法，不依賴Prefab Stage）
        /// </summary>
        public static AnimatorController CreateAnimatorControllerForAnimatorGeneric(Animator animator, string folderPath = null)
        {
            return CreateAnimatorControllerCore(animator, useGenericPath: true, customFolderPath: folderPath);
        }

        /// <summary>
        /// 創建AnimatorController的快捷方法
        /// </summary>
        public static AnimatorController CreateAnimatorControllerAt(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                Debug.LogError("Path is null or empty");
                return null;
            }

            return AnimatorController.CreateAnimatorControllerAtPath(path);
        }

        /// <summary>
        /// 為Animator Controller添加狀態（預留方法）
        /// </summary>
        public static void AddStateToAnimatorController(Animator animator, string stateName)
        {
            // TODO: 實現添加狀態的邏輯
            // var animatorController = animator.runtimeAnimatorController as AnimatorController;
            // if (animatorController == null)
            // {
            //     Debug.LogError("Animator Controller is null");
            //     return;
            // }
            //
            // var newState = animatorController.AddMotion(new AnimationClip(), 0);
            // newState.name = stateName;
        }
    }
#endif
}