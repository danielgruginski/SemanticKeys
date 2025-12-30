using UnityEditor;
using UnityEngine;
using SemanticKeys;
using System.Collections.Generic;

namespace SemanticKeys.Editor
{
    public class SemanticKeyReplacerWindow : EditorWindow
    {
        [MenuItem("Tools/SemanticKeys/Key Replacer Tool")]
        public static void Open()
        {
            GetWindow<SemanticKeyReplacerWindow>("Key Replacer");
        }

        // We use a temporary ScriptableObject just to leverage the PropertyDrawer 
        // because drawing SemanticKey manually is complex without SerializedProperties.
        private ReplacerContainer _container;
        private SerializedObject _serializedContainer;

        private void OnEnable()
        {
            _container = ScriptableObject.CreateInstance<ReplacerContainer>();
            _serializedContainer = new SerializedObject(_container);
        }

        private void OnDisable()
        {
            DestroyImmediate(_container);
        }

        private void OnGUI()
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Replace Semantic Key References", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("This tool scans the entire project and replaces all occurrences of the 'From' key with the 'To' key.", MessageType.Info);
            EditorGUILayout.Space();

            _serializedContainer.Update();

            EditorGUILayout.PropertyField(_serializedContainer.FindProperty("From"));
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("⬇ Replace With ⬇", EditorStyles.centeredGreyMiniLabel);
            EditorGUILayout.Space();
            EditorGUILayout.PropertyField(_serializedContainer.FindProperty("To"));

            _serializedContainer.ApplyModifiedProperties();

            EditorGUILayout.Space(20);

            if (!_container.From.IsValid)
            {
                EditorGUILayout.HelpBox("Select a source key.", MessageType.Warning);
                GUI.enabled = false;
            }
            else if (!_container.To.IsValid)
            {
                EditorGUILayout.HelpBox("Select a target key.", MessageType.Warning);
                GUI.enabled = false;
            }

            if (GUILayout.Button("Replace All", GUILayout.Height(40)))
            {
                if (EditorUtility.DisplayDialog("Confirm Replace",
                    $"Are you sure you want to replace ALL references of '{_container.From.Value}' with '{_container.To.Value}'?\nThis cannot be easily undone.",
                    "Yes, Replace", "Cancel"))
                {
                    // Fetch the domain GUID safely via serialization since it might not be public on the struct
                    var toProp = _serializedContainer.FindProperty("To");
                    string toDomainGuid = toProp.FindPropertyRelative("_domainGuid")?.stringValue ?? "";

                    ReplaceReferences(_container.From, _container.To, toDomainGuid);
                }
            }
            GUI.enabled = true;
        }

        private void ReplaceReferences(SemanticKey from, SemanticKey to, string toDomainGuid)
        {
            // PASS 1: Scan Assets (Prefabs, ScriptableObjects) - Removed t:Scene
            var guidsToScan = AssetDatabase.FindAssets("t:Prefab t:ScriptableObject");
            int count = 0;

            try
            {
                for (int i = 0; i < guidsToScan.Length; i++)
                {
                    var path = AssetDatabase.GUIDToAssetPath(guidsToScan[i]);
                    EditorUtility.DisplayProgressBar("Replacing Keys", $"Scanning Assets: {path}", (float)i / guidsToScan.Length);

                    var assets = AssetDatabase.LoadAllAssetsAtPath(path);
                    foreach (var asset in assets)
                    {
                        if (asset == null) continue;

                        var so = new SerializedObject(asset);
                        if (ReplaceInSerializedObject(so, from, to, toDomainGuid))
                        {
                            count++;
                        }
                    }
                }

                // PASS 2: Scan Open Scene Objects
                EditorUtility.DisplayProgressBar("Replacing Keys", "Scanning Scene Objects...", 1.0f);

                var allObjects = new List<Object>();
                allObjects.AddRange(Resources.FindObjectsOfTypeAll<MonoBehaviour>());
                allObjects.AddRange(Resources.FindObjectsOfTypeAll<ScriptableObject>());

                foreach (var obj in allObjects)
                {
                    // Skip assets on disk (handled in Pass 1) and internal engine objects
                    if (EditorUtility.IsPersistent(obj)) continue;
                    if (obj.hideFlags == HideFlags.NotEditable || obj.hideFlags == HideFlags.HideAndDontSave) continue;

                    // Skip the tool's own internal container so we don't modify the "From" field during execution
                    if (obj == _container) continue;

                    var so = new SerializedObject(obj);
                    if (ReplaceInSerializedObject(so, from, to, toDomainGuid))
                    {
                        count++;
                    }
                }

                if (count > 0)
                {
                    AssetDatabase.SaveAssets();
                    if (!Application.isPlaying)
                    {
                        UnityEditor.SceneManagement.EditorSceneManager.MarkAllScenesDirty();
                    }
                    EditorUtility.DisplayDialog("Complete", $"Replaced {count} references.", "OK");
                }
                else
                {
                    EditorUtility.DisplayDialog("Complete", "No matching references found to replace.", "OK");
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }

        private bool ReplaceInSerializedObject(SerializedObject so, SemanticKey from, SemanticKey to, string toDomainGuid)
        {
            var prop = so.GetIterator();
            bool changed = false;

            while (prop.Next(true))
            {
                if (prop.type == "SemanticKey")
                {
                    var guidProp = prop.FindPropertyRelative("_guid");

                    // Match by GUID (Identity)
                    if (guidProp != null && guidProp.stringValue == from.Guid)
                    {
                        var valueProp = prop.FindPropertyRelative("_value");
                        var domainProp = prop.FindPropertyRelative("_domainGuid");

                        // Swap
                        guidProp.stringValue = to.Guid;
                        valueProp.stringValue = to.Value;
                        if (domainProp != null)
                        {
                            domainProp.stringValue = toDomainGuid;
                        }

                        changed = true;
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

        // Internal container to use SerializedObject system
        private class ReplacerContainer : ScriptableObject
        {
            public SemanticKey From;
            public SemanticKey To;
        }
    }
}