using UnityEditor;
using UnityEngine;
using SemanticKeys;
using System.Linq;
using System.Collections.Generic;

namespace SemanticKeys.Editor
{
    [CustomEditor(typeof(KeyDomain))]
    public class KeyDomainEditor : UnityEditor.Editor
    {
        private KeyDomain _domain;
        private string _editingGuid = null;
        private string _tempName = "";

        private void OnEnable() => _domain = (KeyDomain)target;

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            EditorGUILayout.Space();

            // --- Header & Rename ---
            EditorGUILayout.LabelField("Domain Settings", EditorStyles.boldLabel);
            EditorGUILayout.BeginHorizontal();
            GUI.enabled = false;
            EditorGUILayout.TextField("Domain Name", _domain.DomainName);
            GUI.enabled = true;
            if (GUILayout.Button("Rename", GUILayout.Width(60)))
            {
                SemanticKeyInputWindow.Open("Rename Domain", "New Name:", (n) => _domain.RenameDomain(n));
            }
            EditorGUILayout.EndHorizontal();

            // --- Actions ---
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Generate Static Class")) _domain.GenerateCode();
            if (GUILayout.Button("Update All References")) SemanticKeyReferenceUpdater.UpdateAllReferences();
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField($"Keys ({_domain.Keys.Count()})", EditorStyles.boldLabel);

            // --- Key List ---
            EditorGUILayout.BeginVertical("box");
            foreach (var key in _domain.Keys.ToList())
            {
                DrawKeyRow(key);
                EditorGUILayout.Space(2);
            }
            if (!_domain.Keys.Any()) EditorGUILayout.HelpBox("No keys found.", MessageType.Info);
            EditorGUILayout.EndVertical();

            // --- Add Key ---
            EditorGUILayout.Space(5);
            if (GUILayout.Button("+ Add New Key", GUILayout.Height(25)))
            {
                SemanticKeyInputWindow.Open("Add Key", "Name:", (n) => _domain.AddKey(n));
            }

            serializedObject.ApplyModifiedProperties();
        }

        private void DrawKeyRow(KeyDomain.KeyDefinition key)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.BeginHorizontal();

            if (_editingGuid == key.Guid)
            {
                // Edit Mode
                _tempName = EditorGUILayout.TextField(_tempName);
                GUI.backgroundColor = Color.green;
                if (GUILayout.Button("Save", GUILayout.Width(45)))
                {
                    if (_domain.RenameKey(key.Guid, _tempName))
                    {
                        _editingGuid = null;
                        if (EditorUtility.DisplayDialog("Key Renamed", "Update all references now?", "Yes", "No"))
                        {
                            SemanticKeyReferenceUpdater.UpdateAllReferences();
                        }
                    }
                }
                GUI.backgroundColor = Color.white;
                if (GUILayout.Button("Cancel", GUILayout.Width(55))) _editingGuid = null;
            }
            else
            {
                // View Mode
                EditorGUILayout.LabelField(key.Name, EditorStyles.boldLabel);

                if (GUILayout.Button("Edit", GUILayout.Width(40)))
                {
                    _editingGuid = key.Guid;
                    _tempName = key.Name;
                }

                // Find References Button
                if (GUILayout.Button(EditorGUIUtility.IconContent("d_ViewToolZoom"), GUILayout.Width(30)))
                {
                    FindReferences(key);
                }

                GUI.backgroundColor = new Color(1f, 0.6f, 0.6f);
                if (GUILayout.Button("X", GUILayout.Width(25)))
                {
                    DeleteKeyWithConfirmation(key);
                }
                GUI.backgroundColor = Color.white;
            }
            EditorGUILayout.EndHorizontal();

            // Read-only GUID
            GUI.enabled = false;
            EditorGUILayout.LabelField("GUID: " + key.Guid, EditorStyles.miniLabel);
            GUI.enabled = true;
            EditorGUILayout.EndVertical();
        }

        private void DeleteKeyWithConfirmation(KeyDomain.KeyDefinition key)
        {
            // 1. Scan for references to count them
            var guidsToScan = AssetDatabase.FindAssets("t:Prefab t:ScriptableObject");
            var foundAssetPaths = new List<string>();
            var foundSceneObjects = new List<Object>();
            int fieldCount = 0;

            try
            {
                EditorUtility.DisplayProgressBar("Scanning", $"Checking usages of '{key.Name}'...", 0);

                // Scan Assets
                for (int i = 0; i < guidsToScan.Length; i++)
                {
                    var path = AssetDatabase.GUIDToAssetPath(guidsToScan[i]);
                    var assets = AssetDatabase.LoadAllAssetsAtPath(path);
                    foreach (var asset in assets)
                    {
                        if (asset == null) continue;
                        var so = new SerializedObject(asset);
                        int countInAsset = CountReferences(so, key.Guid);
                        if (countInAsset > 0)
                        {
                            if (!foundAssetPaths.Contains(path)) foundAssetPaths.Add(path);
                            fieldCount += countInAsset;
                        }
                    }
                }

                // Scan Scene Objects
                var sceneObjects = Resources.FindObjectsOfTypeAll<MonoBehaviour>();
                var runtimeSOs = Resources.FindObjectsOfTypeAll<ScriptableObject>();
                var allSceneObjects = new List<Object>(sceneObjects);
                allSceneObjects.AddRange(runtimeSOs);

                foreach (var obj in allSceneObjects)
                {
                    if (obj == null || EditorUtility.IsPersistent(obj)) continue;
                    if (obj.hideFlags == HideFlags.NotEditable || obj.hideFlags == HideFlags.HideAndDontSave) continue;

                    var so = new SerializedObject(obj);
                    int countInObj = CountReferences(so, key.Guid);
                    if (countInObj > 0)
                    {
                        foundSceneObjects.Add(obj);
                        fieldCount += countInObj;
                    }
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }

            // 2. Confirmation Dialog
            string message = fieldCount > 0
                ? $"Found {fieldCount} references to '{key.Name}' in {foundAssetPaths.Count + foundSceneObjects.Count} objects.\n\n" +
                  "Deleting this key will reset these fields to 'None' (null)."
                : $"Are you sure you want to delete '{key.Name}'?";

            if (EditorUtility.DisplayDialog("Delete Key", message, "Delete and Reset", "Cancel"))
            {
                // 3. Reset References
                if (fieldCount > 0)
                {
                    try
                    {
                        EditorUtility.DisplayProgressBar("Deleting", "Resetting references...", 0.5f);
                        bool changed = false;

                        // Reset Assets
                        foreach (var path in foundAssetPaths)
                        {
                            var assets = AssetDatabase.LoadAllAssetsAtPath(path);
                            foreach (var asset in assets)
                            {
                                if (asset == null) continue;
                                var so = new SerializedObject(asset);
                                if (ResetInSerializedObject(so, key.Guid)) changed = true;
                            }
                        }

                        // Reset Scene Objects
                        foreach (var obj in foundSceneObjects)
                        {
                            if (obj == null) continue;
                            var so = new SerializedObject(obj);
                            if (ResetInSerializedObject(so, key.Guid)) changed = true;
                        }

                        if (changed)
                        {
                            AssetDatabase.SaveAssets();
                            if (!Application.isPlaying) UnityEditor.SceneManagement.EditorSceneManager.MarkAllScenesDirty();
                        }
                    }
                    finally
                    {
                        EditorUtility.ClearProgressBar();
                    }
                }

                // 4. Delete Key
                _domain.DeleteKey(key.Guid);
            }
        }

        private int CountReferences(SerializedObject so, string targetGuid)
        {
            int count = 0;
            var prop = so.GetIterator();
            while (prop.Next(true))
            {
                if (prop.type == "SemanticKey")
                {
                    var guidProp = prop.FindPropertyRelative("_guid");
                    if (guidProp != null && guidProp.stringValue == targetGuid)
                    {
                        count++;
                    }
                }
            }
            return count;
        }

        private bool ResetInSerializedObject(SerializedObject so, string targetGuid)
        {
            bool changed = false;
            var prop = so.GetIterator();
            while (prop.Next(true))
            {
                if (prop.type == "SemanticKey")
                {
                    var guidProp = prop.FindPropertyRelative("_guid");
                    if (guidProp != null && guidProp.stringValue == targetGuid)
                    {
                        var valueProp = prop.FindPropertyRelative("_value");
                        var domainProp = prop.FindPropertyRelative("_domainGuid");

                        guidProp.stringValue = string.Empty;
                        valueProp.stringValue = string.Empty;
                        if (domainProp != null) domainProp.stringValue = string.Empty;

                        changed = true;
                    }
                }
            }

            if (changed) so.ApplyModifiedProperties();
            return changed;
        }

        private void FindReferences(KeyDomain.KeyDefinition key)
        {
            // PASS 1: Scan Assets (Prefabs, ScriptableObjects) - Removed t:Scene to avoid crash
            var guidsToScan = AssetDatabase.FindAssets("t:Prefab t:ScriptableObject");
            var foundObjects = new List<Object>();

            try
            {
                // 1. Assets on Disk
                EditorUtility.DisplayProgressBar("Scanning", $"Finding usages of '{key.Name}' in Assets...", 0);
                for (int i = 0; i < guidsToScan.Length; i++)
                {
                    var path = AssetDatabase.GUIDToAssetPath(guidsToScan[i]);
                    var assets = AssetDatabase.LoadAllAssetsAtPath(path);
                    foreach (var asset in assets)
                    {
                        if (asset == null) continue;
                        var so = new SerializedObject(asset);
                        if (HasReference(so, key.Guid))
                        {
                            foundObjects.Add(asset);
                            break; // Found one instance in this asset, move to next file
                        }
                    }
                }

                // 2. Objects in Open Scenes
                EditorUtility.DisplayProgressBar("Scanning", $"Finding usages of '{key.Name}' in Scene...", 0.8f);
                var sceneObjects = Resources.FindObjectsOfTypeAll<MonoBehaviour>();
                var runtimeSOs = Resources.FindObjectsOfTypeAll<ScriptableObject>();

                var allSceneObjects = new List<Object>(sceneObjects);
                allSceneObjects.AddRange(runtimeSOs);

                foreach (var obj in allSceneObjects)
                {
                    if (obj == null) continue;
                    // Skip assets on disk (already handled) and internal objects
                    if (EditorUtility.IsPersistent(obj)) continue;
                    if (obj.hideFlags == HideFlags.NotEditable || obj.hideFlags == HideFlags.HideAndDontSave) continue;

                    var so = new SerializedObject(obj);
                    if (HasReference(so, key.Guid))
                    {
                        // Add the GameObject if it's a component, otherwise the object itself
                        foundObjects.Add(obj is Component c ? c.gameObject : obj);
                    }
                }
            }
            finally { EditorUtility.ClearProgressBar(); }

            if (foundObjects.Count > 0)
            {
                Selection.objects = foundObjects.ToArray();
                EditorGUIUtility.PingObject(foundObjects[0]);
                EditorUtility.DisplayDialog("Find References", $"Found {foundObjects.Count} usages.\nObjects selected.", "OK");
            }
            else
            {
                EditorUtility.DisplayDialog("Find References", "No usages found.", "OK");
            }
        }

        private bool HasReference(SerializedObject so, string targetGuid)
        {
            var prop = so.GetIterator();
            while (prop.Next(true))
            {
                if (prop.type == "SemanticKey")
                {
                    var guidProp = prop.FindPropertyRelative("_guid");
                    if (guidProp != null && guidProp.stringValue == targetGuid)
                    {
                        return true;
                    }
                }
            }
            return false;
        }
    }
}