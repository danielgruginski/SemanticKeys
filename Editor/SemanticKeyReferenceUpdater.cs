using UnityEditor;
using UnityEngine;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace SemanticKeys.Editor
{
    /// <summary>
    /// Solves the "Stale Cache" issue by synchronizing cached string values with KeyDomains.
    /// Optimized with raw pre-scanning to prevent Editor freezes during project-wide updates.
    /// </summary>
    public static class SemanticKeyReferenceUpdater
    {
        [MenuItem("Tools/SemanticKeys/Update All References")]
        public static void UpdateAllReferences()
        {
            // 1. Load all Domains into a fast lookup dictionary
            var guidToNameMap = new Dictionary<string, string>();
            var domains = AssetDatabase.FindAssets("t:KeyDomain");

            foreach (var guid in domains)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var domain = AssetDatabase.LoadAssetAtPath<KeyDomain>(path);
                if (domain == null) continue;

                foreach (var key in domain.Keys)
                {
                    if (!guidToNameMap.ContainsKey(key.Guid))
                    {
                        guidToNameMap.Add(key.Guid, key.Name);
                    }
                }
            }

            if (guidToNameMap.Count == 0)
            {
                Debug.Log("[SemanticKeys] No keys found in any domain. Skipping update.");
                return;
            }

            int fixedCounter = 0;
            var guidsToSync = guidToNameMap.Keys.ToList();

            // --- PASS 1: PROJECT ASSETS ---
            var assetGuids = AssetDatabase.FindAssets("t:Prefab t:ScriptableObject");

            try
            {
                for (int i = 0; i < assetGuids.Length; i++)
                {
                    string path = AssetDatabase.GUIDToAssetPath(assetGuids[i]);

                    if (i % 50 == 0)
                    {
                        float progress = (float)i / assetGuids.Length;
                        if (EditorUtility.DisplayCancelableProgressBar("Semantic Keys", $"Scanning Assets: {path}", progress))
                        {
                            break;
                        }
                    }

                    if (string.IsNullOrEmpty(path) || !File.Exists(path)) continue;

                    // Performance Optimization: Check raw text before using SerializedObject.
                    // This prevents Unity from loading thousands of unrelated assets into memory.
                    string rawContent = File.ReadAllText(path);
                    bool likelyNeedsUpdate = guidsToSync.Any(g => rawContent.Contains(g));

                    if (likelyNeedsUpdate)
                    {
                        var assets = AssetDatabase.LoadAllAssetsAtPath(path);
                        foreach (var asset in assets)
                        {
                            if (asset == null) continue;
                            var so = new SerializedObject(asset);
                            if (ScanAndFix(so, guidToNameMap, path))
                            {
                                fixedCounter++;
                            }
                        }
                    }
                }

                // --- PASS 2: SCENE OBJECTS ---
                EditorUtility.DisplayProgressBar("Semantic Keys", "Scanning Scene Objects...", 0.95f);

                var sceneObjects = Resources.FindObjectsOfTypeAll<MonoBehaviour>()
                    .Cast<Object>()
                    .Concat(Resources.FindObjectsOfTypeAll<ScriptableObject>().Cast<Object>());

                foreach (var obj in sceneObjects)
                {
                    if (EditorUtility.IsPersistent(obj)) continue;
                    if (obj.hideFlags == HideFlags.NotEditable || obj.hideFlags == HideFlags.HideAndDontSave) continue;

                    var so = new SerializedObject(obj);
                    if (ScanAndFix(so, guidToNameMap, $"Scene Object: {obj.name}"))
                    {
                        fixedCounter++;
                    }
                }

                if (fixedCounter > 0)
                {
                    AssetDatabase.SaveAssets();
                    if (!Application.isPlaying)
                    {
                        UnityEditor.SceneManagement.EditorSceneManager.MarkAllScenesDirty();
                    }
                    EditorUtility.DisplayDialog("Semantic Keys", $"Update Complete.\nFixed {fixedCounter} stale references.", "OK");
                }
                else
                {
                    Debug.Log("[SemanticKeys] All references are already up to date.");
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }

        private static bool ScanAndFix(SerializedObject so, Dictionary<string, string> guidToNameMap, string contextPath)
        {
            var prop = so.GetIterator();
            bool changed = false;

            while (prop.Next(true))
            {
                if (prop.type == "SemanticKey")
                {
                    var guidProp = prop.FindPropertyRelative("_guid");
                    var valueProp = prop.FindPropertyRelative("_value");

                    if (guidProp != null && valueProp != null)
                    {
                        string currentGuid = guidProp.stringValue;
                        string currentValue = valueProp.stringValue;

                        if (guidToNameMap.TryGetValue(currentGuid, out string correctName))
                        {
                            if (currentValue != correctName)
                            {
                                valueProp.stringValue = correctName;
                                changed = true;
                                Debug.Log($"[SemanticKeys] Syncing '{currentValue}' -> '{correctName}' in {contextPath}");
                            }
                        }
                    }
                }
            }

            if (changed)
            {
                so.ApplyModifiedProperties();
            }
            return changed;
        }
    }
}