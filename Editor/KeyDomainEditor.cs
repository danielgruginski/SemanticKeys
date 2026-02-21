using UnityEditor;
using UnityEngine;
using SemanticKeys;
using System.Linq;
using System.Collections.Generic;
using System.IO;

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
                EditorGUILayout.LabelField(key.Name, EditorStyles.boldLabel);

                if (GUILayout.Button("Edit", GUILayout.Width(40)))
                {
                    _editingGuid = key.Guid;
                    _tempName = key.Name;
                }

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

            GUI.enabled = false;
            EditorGUILayout.LabelField("GUID: " + key.Guid, EditorStyles.miniLabel);
            GUI.enabled = true;
            EditorGUILayout.EndVertical();
        }

        private void DeleteKeyWithConfirmation(KeyDomain.KeyDefinition key)
        {
            var results = PerformOptimizationScan(key.Guid);
            int fieldCount = results.TotalRefCount;

            string message = fieldCount > 0
                ? $"Found {fieldCount} references to '{key.Name}' in {results.Assets.Count + results.SceneObjects.Count} objects.\n\n" +
                  "Deleting this key will reset these fields to 'None' (null)."
                : $"Are you sure you want to delete '{key.Name}'?";

            if (EditorUtility.DisplayDialog("Delete Key", message, "Delete and Reset", "Cancel"))
            {
                if (fieldCount > 0)
                {
                    try
                    {
                        EditorUtility.DisplayProgressBar("Deleting", "Resetting references...", 0.5f);

                        foreach (var path in results.Assets)
                        {
                            var assets = AssetDatabase.LoadAllAssetsAtPath(path);
                            foreach (var asset in assets)
                            {
                                if (asset == null) continue;
                                var so = new SerializedObject(asset);
                                ResetInSerializedObject(so, key.Guid);
                            }
                        }

                        foreach (var obj in results.SceneObjects)
                        {
                            var so = new SerializedObject(obj);
                            ResetInSerializedObject(so, key.Guid);
                        }

                        AssetDatabase.SaveAssets();
                        if (!Application.isPlaying) UnityEditor.SceneManagement.EditorSceneManager.MarkAllScenesDirty();
                    }
                    finally { EditorUtility.ClearProgressBar(); }
                }

                _domain.DeleteKey(key.Guid);
            }
        }

        private void FindReferences(KeyDomain.KeyDefinition key)
        {
            var results = PerformOptimizationScan(key.Guid);
            var foundObjects = new List<Object>();

            foreach (var path in results.Assets)
            {
                var asset = AssetDatabase.LoadMainAssetAtPath(path);
                if (asset != null) foundObjects.Add(asset);
            }
            foundObjects.AddRange(results.SceneObjects);

            if (foundObjects.Count > 0)
            {
                Selection.objects = foundObjects.ToArray();
                EditorGUIUtility.PingObject(foundObjects[0]);
                EditorUtility.DisplayDialog("Find References", $"Found {results.TotalRefCount} usages across {foundObjects.Count} objects.\nObjects selected.", "OK");
            }
            else
            {
                EditorUtility.DisplayDialog("Find References", "No usages found.", "OK");
            }
        }

        private ScanResults PerformOptimizationScan(string targetGuid)
        {
            var results = new ScanResults();
            string domainPath = AssetDatabase.GetAssetPath(_domain);

            // 1. Project Scan (Raw Text Pre-filter)
            var guidsToScan = AssetDatabase.FindAssets("t:Prefab t:ScriptableObject t:Scene");

            try
            {
                for (int i = 0; i < guidsToScan.Length; i++)
                {
                    string path = AssetDatabase.GUIDToAssetPath(guidsToScan[i]);
                    if (path == domainPath) continue; // Skip self

                    if (i % 50 == 0)
                        EditorUtility.DisplayProgressBar("Scanning", $"Pre-filtering Assets: {path}", (float)i / guidsToScan.Length);

                    if (!File.Exists(path)) continue;

                    // Optimization: Read raw text to check for GUID presence
                    string content = File.ReadAllText(path);
                    if (content.Contains(targetGuid))
                    {
                        results.Assets.Add(path);
                        // Count occurrences in raw text as an estimate
                        results.TotalRefCount += System.Text.RegularExpressions.Regex.Matches(content, targetGuid).Count;
                    }
                }

                // 2. Scene Scan (SerializedObject is unavoidable for hierarchy, but we limit it to loaded objects)
                EditorUtility.DisplayProgressBar("Scanning", "Scanning Scene Objects...", 0.9f);
                var sceneObjects = Resources.FindObjectsOfTypeAll<MonoBehaviour>()
                    .Cast<Object>()
                    .Concat(Resources.FindObjectsOfTypeAll<ScriptableObject>().Cast<Object>());

                foreach (var obj in sceneObjects)
                {
                    if (obj == null || EditorUtility.IsPersistent(obj)) continue;
                    if (obj.hideFlags == HideFlags.NotEditable || obj.hideFlags == HideFlags.HideAndDontSave) continue;

                    var so = new SerializedObject(obj);
                    int count = CountReferences(so, targetGuid);
                    if (count > 0)
                    {
                        results.SceneObjects.Add(obj is Component c ? c.gameObject : obj);
                        results.TotalRefCount += count;
                    }
                }
            }
            finally { EditorUtility.ClearProgressBar(); }

            return results;
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
                    if (guidProp != null && guidProp.stringValue == targetGuid) count++;
                }
            }
            return count;
        }

        private void ResetInSerializedObject(SerializedObject so, string targetGuid)
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
                        guidProp.stringValue = string.Empty;
                        var valProp = prop.FindPropertyRelative("_value");
                        if (valProp != null) valProp.stringValue = string.Empty;
                        var domProp = prop.FindPropertyRelative("_domainGuid");
                        if (domProp != null) domProp.stringValue = string.Empty;
                        changed = true;
                    }
                }
            }
            if (changed) so.ApplyModifiedProperties();
        }

        private class ScanResults
        {
            public List<string> Assets = new List<string>();
            public List<Object> SceneObjects = new List<Object>();
            public int TotalRefCount = 0;
        }
    }
}