using UnityEditor;
using UnityEngine;
using System;

namespace SemanticKeys.Editor
{
    public class SemanticKeyInputWindow : EditorWindow
    {
        private string _inputLabel;
        private string _inputText = "";
        private Action<string> _onConfirm;
        private bool _shouldClose = false;

        public static void Open(string title, string label, Action<string> onConfirm)
        {
            var window = ScriptableObject.CreateInstance<SemanticKeyInputWindow>();
            window.titleContent = new GUIContent(title);
            window._inputLabel = label;
            window._onConfirm = onConfirm;

            // Center the window on screen
            var size = new Vector2(300, 100);
            var center = new Rect((Screen.currentResolution.width - size.x) / 2, (Screen.currentResolution.height - size.y) / 2, size.x, size.y);
            window.position = center;
            window.ShowUtility(); // Show as a modal utility
        }

        private void OnGUI()
        {
            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField(_inputLabel, EditorStyles.boldLabel);

            GUI.SetNextControlName("InputField");
            _inputText = EditorGUILayout.TextField(_inputText);

            // Focus the field immediately
            EditorGUI.FocusTextInControl("InputField");

            EditorGUILayout.Space(10);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Cancel")) Close();
                if (GUILayout.Button("Create") || (Event.current.isKey && Event.current.keyCode == KeyCode.Return))
                {
                    if (!string.IsNullOrEmpty(_inputText))
                    {
                        _onConfirm?.Invoke(_inputText);
                        _shouldClose = true;
                    }
                }
            }

            if (_shouldClose) Close();
        }
    }
}