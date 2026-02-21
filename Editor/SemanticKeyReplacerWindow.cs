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
    /// Uses raw file replacement to prevent Unity Editor freezes caused by recursive deserialization.
    /// </summary>
    public class SemanticKeyReplacerWindow : EditorWindow
    {
        [MenuItem("Tools/SemanticKeys/Key Replacer Tool")]
        public static void Open()
        {
            GetWindow<SemanticKeyReplacerWindow>("Key Replacer");
        }

        private ReplacerContainer _container;
        private SerializedObject _serializedContainer;
        private Vector2 _logScroll;
        private List<string> _log = new List<string>();

        private void OnEnable()
        {
            _container = ScriptableObject.CreateInstance<ReplacerContainer>();
            _serializedContainer = new SerializedObject(_container);
        }

        private void OnDisable()
        {
            if (_container != null) DestroyImmediate(_container);
        }

        private void OnGUI()
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Global Semantic Key Replacer", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("This tool modifies .prefab, .unity, and .asset files directly as text to avoid memory freezes.", MessageType.Info);
            EditorGUILayout.Space();

            _serializedContainer.Update();

            EditorGUILayout.PropertyField(_serializedContainer.FindProperty("From"));
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("⬇ Replace With ⬇", EditorStyles.centeredGreyMiniLabel);
            EditorGUILayout.Space();
            EditorGUILayout.PropertyField(_serializedContainer.FindProperty("To"));

            _serializedContainer.ApplyModifiedProperties();

            EditorGUILayout.Space(20);

            bool canExecute = _container.From.IsValid && _container.To.IsValid && _container.From.Guid != _container.To.Guid;

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
            var fromProp = _serializedContainer.FindProperty("From");
            var toProp = _serializedContainer.FindProperty("To");

            string fromGuid = fromProp.FindPropertyRelative("_guid").stringValue;
            string toGuid = toProp.FindPropertyRelative("_guid").stringValue;

            // One initial confirmation for project-wide modification
            if (EditorUtility.DisplayDialog("Confirm Replacement",
                $"This will replace all references of the selected key project-wide.\n\n" +
                "KeyDomain assets will be skipped. Back up your project first.",
                "Execute", "Cancel"))
            {
                PerformRawReplacement(fromGuid, toGuid);

                // Automatically synchronize cached names after GUID swap to maintain data integrity
                SemanticKeyReferenceUpdater.UpdateAllReferences();

                GUIUtility.ExitGUI();
            }
        }

        private void PerformRawReplacement(string fromGuid, string toGuid)
        {
            _log.Clear();
            _log.Add($"[Start] Searching for GUID: {fromGuid}");

            var domainGuids = new HashSet<string>(AssetDatabase.FindAssets("t:KeyDomain"));
            string[] allAssetGuids = AssetDatabase.FindAssets("t:Prefab t:Scene t:ScriptableObject");
            int updatedCount = 0;

            // CRITICAL: Prevent scene reloads and auto-imports mid-process
            AssetDatabase.StartAssetEditing();

            try
            {
                for (int i = 0; i < allAssetGuids.Length; i++)
                {
                    string assetGuid = allAssetGuids[i];

                    if (domainGuids.Contains(assetGuid))
                    {
                        continue;
                    }

                    string path = AssetDatabase.GUIDToAssetPath(assetGuid);
                    float progress = (float)i / allAssetGuids.Length;

                    if (EditorUtility.DisplayCancelableProgressBar("Replacing SemanticKeys", $"Scanning: {path}", progress))
                    {
                        _log.Add("[Cancelled] Operation aborted by user.");
                        break;
                    }

                    if (string.IsNullOrEmpty(path) || !File.Exists(path)) continue;

                    string content = File.ReadAllText(path);

                    if (content.Contains(fromGuid))
                    {
                        content = content.Replace(fromGuid, toGuid);
                        File.WriteAllText(path, content);

                        updatedCount++;
                        _log.Add($"[Updated] {path}");
                    }
                }

                AssetDatabase.SaveAssets();
                _log.Add($"[Finished] Updated {updatedCount} files.");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[KeyReplacer] Critical Error: {e.Message}");
                _log.Add($"[Error] {e.Message}");
            }
            finally
            {
                EditorUtility.ClearProgressBar();
                // Resume normal Unity asset tracking and trigger single re-import
                AssetDatabase.StopAssetEditing();
                AssetDatabase.Refresh();
            }
        }

        private class ReplacerContainer : ScriptableObject
        {
            public SemanticKey From;
            public SemanticKey To;
        }
    }
}