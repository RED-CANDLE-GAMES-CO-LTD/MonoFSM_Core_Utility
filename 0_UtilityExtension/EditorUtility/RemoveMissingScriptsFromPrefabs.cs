#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace MonoFSM.Core
{
    public static class RemoveMissingScriptsFromPrefabs
    {
        [MenuItem("Tools/MonoFSM/Remove Missing Scripts From Current Prefab")]
        private static void RemoveFromCurrentPrefab()
        {
            var prefabStage = PrefabStageUtility.GetCurrentPrefabStage();
            if (prefabStage == null)
            {
                Debug.LogWarning("[RemoveMissingScripts] Not in Prefab Stage.");
                return;
            }

            var root = prefabStage.prefabContentsRoot;
            var transforms = root.GetComponentsInChildren<Transform>(true);
            var totalRemoved = 0;

            foreach (var t in transforms)
            {
                var count = GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject);
                if (count > 0)
                {
                    totalRemoved += count;
                    GameObjectUtility.RemoveMonoBehavioursWithMissingScript(t.gameObject);
                    Debug.Log($"[RemoveMissingScripts] Removed {count} from: {t.name}", t.gameObject);
                }
            }

            if (totalRemoved > 0)
            {
                EditorSceneManager.MarkSceneDirty(prefabStage.scene);
                Debug.Log($"[RemoveMissingScripts] Done. Removed {totalRemoved} missing scripts. Remember to save the prefab.");
            }
            else
            {
                Debug.Log("[RemoveMissingScripts] No missing scripts found in current prefab.");
            }
        }

        [MenuItem("Tools/MonoFSM/Remove Missing Scripts From Current Prefab", true)]
        private static bool RemoveFromCurrentPrefabValidation()
        {
            return PrefabStageUtility.GetCurrentPrefabStage() != null;
        }
    }
}
#endif
