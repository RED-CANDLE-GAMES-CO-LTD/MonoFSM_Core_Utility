// using System.Collections.Generic;
// using System.Linq;
// using UnityEditor;
// using UnityEngine;
//
// namespace MonoFSM.Utility.Editor
// {
//     public class PackageToSubmoduleWindow : EditorWindow
//     {
//         private Vector2 scrollPosition;
//         private List<GitPackageInfo> gitPackages = new();
//         private bool[] selectedPackages;
//         private string submoduleRootPath = "Submodules";
//
//         [MenuItem("Tools/Package To Submodule")]
//         public static void ShowStandaloneWindow()
//         {
//             var window = GetWindow<PackageToSubmoduleWindow>("Package To Submodule");
//             window.RefreshPackageList();
//             window.Show();
//         }
//
//         private void OnGUI()
//         {
//             EditorGUILayout.LabelField("Git Package To Submodule Converter", EditorStyles.boldLabel);
//             EditorGUILayout.Space();
//
//             // 設定區域
//             EditorGUILayout.LabelField("Settings", EditorStyles.boldLabel);
//             submoduleRootPath = EditorGUILayout.TextField("Submodule Root Path", submoduleRootPath);
//             EditorGUILayout.Space();
//
//             // 操作按鈕
//             EditorGUILayout.BeginHorizontal();
//             if (GUILayout.Button("Refresh Package List")) RefreshPackageList();
//             if (GUILayout.Button("Convert Selected")) ConvertSelectedPackages();
//             EditorGUILayout.EndHorizontal();
//             EditorGUILayout.Space();
//
//             // Package 列表
//             EditorGUILayout.LabelField("Git Packages Found", EditorStyles.boldLabel);
//
//             if (gitPackages.Count == 0)
//             {
//                 EditorGUILayout.HelpBox("No git packages found in manifest.json", MessageType.Info);
//                 return;
//             }
//
//             scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);
//
//             for (var i = 0; i < gitPackages.Count; i++)
//             {
//                 var package = gitPackages[i];
//
//                 EditorGUILayout.BeginVertical(GUI.skin.box);
//
//                 // 選擇框和包名
//                 EditorGUILayout.BeginHorizontal();
//                 if (selectedPackages != null && i < selectedPackages.Length)
//                     selectedPackages[i] = EditorGUILayout.Toggle(selectedPackages[i], GUILayout.Width(20));
//                 EditorGUILayout.LabelField(package.packageName, EditorStyles.boldLabel);
//                 EditorGUILayout.EndHorizontal();
//
//                 // 詳細資訊
//                 EditorGUILayout.LabelField("Git URL:", package.gitUrl, EditorStyles.miniLabel);
//                 if (!string.IsNullOrEmpty(package.gitPath))
//                     EditorGUILayout.LabelField("Path:", package.gitPath, EditorStyles.miniLabel);
//                 EditorGUILayout.LabelField("Target Submodule Path:",
//                     $"{submoduleRootPath}/{package.GetSubmoduleName()}", EditorStyles.miniLabel);
//
//                 EditorGUILayout.EndVertical();
//                 EditorGUILayout.Space();
//             }
//
//             EditorGUILayout.EndScrollView();
//
//             // 統計資訊
//             var selectedCount = selectedPackages?.Count(x => x) ?? 0;
//             EditorGUILayout.LabelField($"Selected: {selectedCount}/{gitPackages.Count}");
//         }
//
//         private void RefreshPackageList()
//         {
//             gitPackages = PackageToSubmoduleConverter.GetGitPackages();
//             selectedPackages = new bool[gitPackages.Count];
//         }
//
//         private void ConvertSelectedPackages()
//         {
//             if (selectedPackages == null) return;
//
//             var packagesToConvert = new List<GitPackageInfo>();
//             for (var i = 0; i < selectedPackages.Length && i < gitPackages.Count; i++)
//                 if (selectedPackages[i])
//                     packagesToConvert.Add(gitPackages[i]);
//
//             if (packagesToConvert.Count == 0)
//             {
//                 EditorUtility.DisplayDialog("No Selection", "請選擇要轉換的package", "OK");
//                 return;
//             }
//
//             var confirm = EditorUtility.DisplayDialog(
//                 "確認轉換",
//                 $"將轉換 {packagesToConvert.Count} 個git package為submodule。\n此操作會修改manifest.json和添加git submodule。\n是否繼續？",
//                 "確認",
//                 "取消"
//             );
//
//             if (confirm)
//             {
//                 var converter = new PackageToSubmoduleConverter(submoduleRootPath);
//                 foreach (var package in packagesToConvert) converter.ConvertToSubmodule(package);
//
//                 RefreshPackageList();
//                 EditorUtility.DisplayDialog("完成", "轉換完成！請檢查git狀態。", "OK");
//             }
//         }
//     }
// }

