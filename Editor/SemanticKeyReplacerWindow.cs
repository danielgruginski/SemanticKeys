using UnityEditor;
using UnityEngine;
using SemanticKeys;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace SemanticKeys.Editor
{
    /// <summary>
    /// Refactoring utility that swaps Semantic Key references project-wide.
    /// Uses raw file replacement for assets and SerializedObject for scene objects to prevent crashes.
    /// </summary>
    public class SemanticKeyReplacerWindow : EditorWindow
    {
        [MenuItem("Tools/SemanticKeys/Key Replacer Tool")]
        public static void Open()
        {
            GetWindow<SemanticKeyReplacerWindow>("Key Replacer");
        }

        // Serializing fields directly in the window ensures they survive domain reloads and scene refreshes.
        [SerializeField] private SemanticKey _from;
        [SerializeField] private SemanticKey _to;

        private SerializedObject _serializedObject;
        private Vector2 _logScroll;
        private List<string> _log = new List<string>();

        private void OnEnable()
        {
            _serializedObject = new SerializedObject(this);
        }

        private void OnGUI()
        {
            // Ensure the SerializedObject is re-linked if lost during a domain reload or project refresh.
            if (_serializedObject == null || _serializedObject.targetObject == null)
            {
                _serializedObject = new SerializedObject(this);
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Global Semantic Key Replacer", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Modifies Assets (.prefab, .asset) as raw text. Modifies Scene Objects via SerializedObject to prevent crashes.", MessageType.Info);
            EditorGUILayout.Space();

            _serializedObject.Update();

            EditorGUILayout.PropertyField(_serializedObject.FindProperty("_from"), new GUIContent("From"));
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("⬇ Replace With ⬇", EditorStyles.centeredGreyMiniLabel);
            EditorGUILayout.Space();
            EditorGUILayout.PropertyField(_serializedObject.FindProperty("_to"), new GUIContent("To"));

            _serializedObject.ApplyModifiedProperties();

            EditorGUILayout.Space(20);

            bool canExecute = _from.IsValid && _to.IsValid && _from.Guid != _to.Guid;

            if (!canExecute)
            {
                EditorGUILayout.HelpBox("Select two different valid keys to begin.", MessageType.None);
                GUI.enabled = false;
            }

            if (GUILayout.Button("Execute Global Replacement", GUILayout.Height(40)))
            {
                ExecuteReplacement();
            }
            GUI.enabled = true;

            DrawLog();
        }

        private void DrawLog()
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Session Log", EditorStyles.miniBoldLabel);
            _logScroll = EditorGUILayout.BeginScrollView(_logScroll, GUI.skin.box, GUILayout.Height(150));
            if (_log.Count == 0) EditorGUILayout.LabelField("Ready.", EditorStyles.miniLabel);
            foreach (var line in _log)
            {
                EditorGUILayout.LabelField(line, EditorStyles.miniLabel);
            }
            EditorGUILayout.EndScrollView();
            if (_log.Count > 0 && GUILayout.Button("Clear Log", GUILayout.Width(70))) _log.Clear();
        }

        private void ExecuteReplacement()
        {
            string fromGuid = _from.Guid;
            string toGuid = _to.Guid;
            string toValue = _to.Value;

            // Extract the domain GUID from the 'to' key property accurately via SerializedProperty
            var toProp = _serializedObject.FindProperty("_to");
            string toDomain = toProp.FindPropertyRelative("_domainGuid").stringValue;

            if (EditorUtility.DisplayDialog("Confirm Replacement",
                $"Replace all references of '{_from.Value}' with '{_to.Value}' project-wide?\n\n" +
                "This will modify assets on disk and objects in the current scene.",
                "Execute", "Cancel"))
            {
                PerformHybridReplacement(fromGuid, toGuid, toValue, toDomain);

                // Finalize by syncing names to maintain identity-value consistency
                SemanticKeyReferenceUpdater.UpdateAllReferences();

                GUIUtility.ExitGUI();
            }
        }

        private void PerformHybridReplacement(string fromGuid, string toGuid, string toValue, string toDomain)
        {
            _log.Clear();
            _log.Add($"[Start] Swapping GUID: {fromGuid} -> {toGuid}");

            var domainGuids = new HashSet<string>(AssetDatabase.FindAssets("t:KeyDomain"));

            // PASS 1: Raw File Replacement for Assets (Explicitly excluding .unity files)
            string[] assetGuids = AssetDatabase.FindAssets("t:Prefab t:ScriptableObject");
            int updatedFiles = 0;

            AssetDatabase.StartAssetEditing();
            try
            {
                for (int i = 0; i < assetGuids.Length; i++)
                {
                    string guid = assetGuids[i];
                    if (domainGuids.Contains(guid)) continue;

                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    if (string.IsNullOrEmpty(path) || !File.Exists(path)) continue;

                    if (EditorUtility.DisplayCancelableProgressBar("Replacing (Assets)", path, (float)i / assetGuids.Length)) break;

                    string content = File.ReadAllText(path);
                    if (content.Contains(fromGuid))
                    {
                        content = content.Replace(fromGuid, toGuid);
                        File.WriteAllText(path, content);
                        updatedFiles++;
                        _log.Add($"[Asset Updated] {path}");
                    }
                }
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
            }

            // PASS 2: Object-based Replacement for Scene Objects
            // This prevents "Scene modified" popups and crashes by using Unity's official API for in-memory objects.
            int updatedSceneObjects = 0;

            var sceneObjects = Resources.FindObjectsOfTypeAll<MonoBehaviour>()
                .Cast<Object>()
                .Concat(Resources.FindObjectsOfTypeAll<ScriptableObject>().Cast<Object>());

            foreach (var obj in sceneObjects)
            {
                if (EditorUtility.IsPersistent(obj)) continue;
                if (obj.hideFlags == HideFlags.NotEditable || obj.hideFlags == HideFlags.HideAndDontSave) continue;
                if (obj == this) continue;

                var so = new SerializedObject(obj);
                if (ApplyReplacementToSO(so, fromGuid, toGuid, toValue, toDomain))
                {
                    updatedSceneObjects++;
                    _log.Add($"[Scene Object Updated] {obj.name}");
                }
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            EditorUtility.ClearProgressBar();

            _log.Add($"[Finished] Updated {updatedFiles} assets and {updatedSceneObjects} scene objects.");
        }

        private bool ApplyReplacementToSO(SerializedObject so, string fromGuid, string toGuid, string toVal, string toDom)
        {
            bool changed = false;
            var prop = so.GetIterator();

            while (prop.Next(true))
            {
                if (prop.type == "SemanticKey")
                {
                    var guidProp = prop.FindPropertyRelative("_guid");
                    if (guidProp != null && guidProp.stringValue == fromGuid)
                    {
                        var valueProp = prop.FindPropertyRelative("_value");
                        var domainProp = prop.FindPropertyRelative("_domainGuid");

                        guidProp.stringValue = toGuid;
                        if (valueProp != null) valueProp.stringValue = toVal;
                        if (domainProp != null) domainProp.stringValue = toDom;

                        changed = true;
                    }
                }
            }

            if (changed) so.ApplyModifiedProperties();
            return changed;
        }
    }
}