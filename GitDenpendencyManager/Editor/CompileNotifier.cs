using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 監聽 Unity domain reload，在兩個時間點播放音效提醒：
///   ① afterAssemblyReload 立即觸發（reload 完成但 Unity 可能還在初始化）
///   ② afterAssemblyReload 後等待指定 frames 再觸發（Unity 基本穩定可用）
/// 設定路徑：Edit > Preferences > Compile Notifier
/// </summary>
[InitializeOnLoad]
public static class CompileNotifier
{
    private const string PrefEnabled        = "CompileNotifier_Enabled";
    private const string PrefClipGuid       = "CompileNotifier_ClipGUID";
    private const string PrefOnReloadImmed  = "CompileNotifier_OnReloadImmed";
    private const string PrefOnReloadDelay  = "CompileNotifier_OnReloadDelay";
    private const string PrefDelayFrames    = "CompileNotifier_DelayFrames";

    static CompileNotifier()
    {
        AssemblyReloadEvents.afterAssemblyReload += OnAfterAssemblyReload;
    }

    // ── 事件處理 ────────────────────────────────────────────────────

    private static void OnAfterAssemblyReload()
    {
        if (!EditorPrefs.GetBool(PrefEnabled, true)) return;

        // 時間點 1：立即播
        if (EditorPrefs.GetBool(PrefOnReloadImmed, true))
            PlayNotificationSound();

        // 時間點 2：delay frames 後播
        if (EditorPrefs.GetBool(PrefOnReloadDelay, true))
        {
            int frames = EditorPrefs.GetInt(PrefDelayFrames, 10);
            DelayFrames(frames, PlayNotificationSound);
        }
    }

    // ── 音效播放 ────────────────────────────────────────────────────

    private static void PlayNotificationSound()
    {
        var clipGuid = EditorPrefs.GetString(PrefClipGuid, "");
        if (!string.IsNullOrEmpty(clipGuid))
        {
            var path = AssetDatabase.GUIDToAssetPath(clipGuid);
            var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
            if (clip != null)
            {
                PlayEditorClip(clip);
                return;
            }
        }

        EditorApplication.Beep();
    }

    /// <summary>
    /// 透過 reflection 呼叫 Unity 內部的 AudioUtil.PlayPreviewClip
    /// </summary>
    private static void PlayEditorClip(AudioClip clip)
    {
        var audioUtil = Type.GetType("UnityEditor.AudioUtil, UnityEditor");
        if (audioUtil == null) return;

        // Unity 2020+ signature: PlayPreviewClip(AudioClip, int, bool)
        var method = audioUtil.GetMethod(
            "PlayPreviewClip",
            BindingFlags.Static | BindingFlags.Public,
            null,
            new Type[] { typeof(AudioClip), typeof(int), typeof(bool) },
            null);

        if (method != null)
        {
            method.Invoke(null, new object[] { clip, 0, false });
            return;
        }

        // fallback 舊版 signature
        var methodOld = audioUtil.GetMethod(
            "PlayPreviewClip",
            BindingFlags.Static | BindingFlags.Public,
            null,
            new Type[] { typeof(AudioClip) },
            null);

        methodOld?.Invoke(null, new object[] { clip });
    }

    /// <summary>
    /// 透過鏈式 delayCall 等待指定 frames 後執行 callback
    /// </summary>
    private static void DelayFrames(int frames, Action callback)
    {
        if (frames <= 0) { callback(); return; }
        EditorApplication.delayCall += () => DelayFrames(frames - 1, callback);
    }

    // ── Preferences UI ──────────────────────────────────────────────

    [SettingsProvider]
    private static SettingsProvider CreateSettingsProvider()
    {
        return new SettingsProvider("Preferences/Compile Notifier", SettingsScope.User)
        {
            label = "Compile Notifier",
            guiHandler = _ => DrawPreferencesGUI(),
            keywords = new HashSet<string> { "compile", "notify", "sound", "audio", "reload" }
        };
    }

    private static void DrawPreferencesGUI()
    {
        EditorGUILayout.Space(4);

        var enabled    = EditorPrefs.GetBool(PrefEnabled, true);
        var newEnabled = EditorGUILayout.Toggle("啟用編譯完成通知", enabled);
        if (newEnabled != enabled)
            EditorPrefs.SetBool(PrefEnabled, newEnabled);

        EditorGUILayout.Space(4);

        using (new EditorGUI.DisabledScope(!newEnabled))
        {
            // 音效 clip
            var guid    = EditorPrefs.GetString(PrefClipGuid, "");
            var path    = string.IsNullOrEmpty(guid) ? "" : AssetDatabase.GUIDToAssetPath(guid);
            var current = string.IsNullOrEmpty(path) ? null : AssetDatabase.LoadAssetAtPath<AudioClip>(path);

            var newClip = (AudioClip)EditorGUILayout.ObjectField("通知音效（空白則使用系統 Beep）", current, typeof(AudioClip), false);
            if (newClip != current)
            {
                var newGuid = newClip != null ? AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(newClip)) : "";
                EditorPrefs.SetString(PrefClipGuid, newGuid);
            }

            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("觸發時間點", EditorStyles.boldLabel);

            // 時間點 1：立即
            var onImmed    = EditorPrefs.GetBool(PrefOnReloadImmed, true);
            var newOnImmed = EditorGUILayout.Toggle(new GUIContent("① Reload 完成（立即）", "afterAssemblyReload 觸發時立即播放"), onImmed);
            if (newOnImmed != onImmed)
                EditorPrefs.SetBool(PrefOnReloadImmed, newOnImmed);

            // 時間點 2：delay
            var onDelay    = EditorPrefs.GetBool(PrefOnReloadDelay, true);
            var newOnDelay = EditorGUILayout.Toggle(new GUIContent("② Reload 完成（延遲後）", "afterAssemblyReload 後等待指定 frames 再播放\nUnity 完成 scene 反序列化等工作後"), onDelay);
            if (newOnDelay != onDelay)
                EditorPrefs.SetBool(PrefOnReloadDelay, newOnDelay);

            using (new EditorGUI.DisabledScope(!newOnDelay))
            {
                EditorGUI.indentLevel++;
                var delayFrames = EditorPrefs.GetInt(PrefDelayFrames, 10);
                var newDelay    = EditorGUILayout.IntSlider(new GUIContent("延遲 Frames", "建議 5~15，讓 Unity 完成 scene 反序列化"), delayFrames, 1, 30);
                if (newDelay != delayFrames)
                    EditorPrefs.SetInt(PrefDelayFrames, newDelay);
                EditorGUI.indentLevel--;
            }

            EditorGUILayout.Space(8);

            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("測試播放", GUILayout.Width(100)))
                    PlayNotificationSound();
            }
        }

        EditorGUILayout.Space(4);
        EditorGUILayout.HelpBox("① 提示可以去做別的事了，② 提示 Unity 已完全就緒。", MessageType.Info);
    }
}
