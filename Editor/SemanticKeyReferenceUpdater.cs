using UnityEditor;
using UnityEngine;
using System.Collections.Generic;

namespace SemanticKeys.Editor
{
    /// <summary>
    /// Solves the "Stale Cache" issue. 
    /// Scans the entire project for SemanticKeys that have a valid GUID but an outdated String Value
    /// and updates them to match the Source of Truth (KeyDomain).
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

            int fixedCounter = 0;
            int scannedCounter = 0;

            // --- PASS 1: PROJECT ASSETS (Prefabs & SOs) ---
            var guidsToScan = AssetDatabase.FindAssets("t:Prefab t:ScriptableObject");

            try
            {
                // Scan Project Assets
                foreach (var assetGuid in guidsToScan)
                {
                    var path = AssetDatabase.GUIDToAssetPath(assetGuid);
                    scannedCounter++;
                    EditorUtility.DisplayProgressBar("Updating Semantic Keys", $"Scanning Assets: {path}...", (float)scannedCounter / (guidsToScan.Length * 2)); // *2 roughly for scene pass

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

                // --- PASS 2: OPEN SCENE OBJECTS ---
                // Find all MonoBehaviours in loaded scenes (excludes assets on disk)
                var sceneObjects = Resources.FindObjectsOfTypeAll<MonoBehaviour>();

                // Also scan ScriptableObjects that might be referenced in scene components but not saved as assets
                var runtimeSOs = Resources.FindObjectsOfTypeAll<ScriptableObject>();

                var allSceneObjects = new List<Object>(sceneObjects);
                allSceneObjects.AddRange(runtimeSOs);

                int sceneObjCount = 0;
                foreach (var obj in allSceneObjects)
                {
                    sceneObjCount++;
                    // Skip assets on disk (already handled) and internal Unity objects
                    if (EditorUtility.IsPersistent(obj)) continue;
                    if (obj.hideFlags == HideFlags.NotEditable || obj.hideFlags == HideFlags.HideAndDontSave) continue;

                    if (sceneObjCount % 50 == 0) // Update bar periodically
                        EditorUtility.DisplayProgressBar("Updating Semantic Keys", "Scanning Scene Objects...", 0.5f + ((float)sceneObjCount / allSceneObjects.Count * 0.5f));

                    var so = new SerializedObject(obj);
                    if (ScanAndFix(so, guidToNameMap, $"Scene Object: {obj.name}"))
                    {
                        fixedCounter++;
                    }
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }

            if (fixedCounter > 0)
            {
                AssetDatabase.SaveAssets();
                // If we modified scene objects, we need to mark the scene as dirty
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

        /// <summary>
        /// Helper to scan a SerializedObject and fix any SemanticKey properties.
        /// Returns true if any changes were made.
        /// </summary>
        private static bool ScanAndFix(SerializedObject so, Dictionary<string, string> guidToNameMap, string contextPath)
        {
            var prop = so.GetIterator();
            bool changed = false;

            // Iterate through every single property in the file
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
                                Debug.Log($"[SemanticKeys] Fixed stale key in {contextPath}: '{currentValue}' -> '{correctName}'");
                            }
                        }
                    }
                }
            }

            if (changed)
            {
                so.ApplyModifiedProperties();
                return true;
            }
            return false;
        }
    }
}