using System.Linq;
using UnityEditor;
using UnityEngine;

namespace SemanticKeys.Editor
{
    [CustomPropertyDrawer(typeof(SemanticKey))]
    public class SemanticKeyDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);

            // Locate properties
            var guidProp = property.FindPropertyRelative("_guid");
            var valueProp = property.FindPropertyRelative("_value");

            // Draw Label
            position = EditorGUI.PrefixLabel(position, GUIUtility.GetControlID(FocusType.Passive), label);

            // Calculate Style
            var currentName = valueProp.stringValue;
            // Treat empty string as "None" for UI display
            if (string.IsNullOrEmpty(currentName))
            {
                currentName = "None";
            }
            // The selected objects hold different keys: show Unity's mixed-value dash
            if (guidProp.hasMultipleDifferentValues)
            {
                currentName = "\u2014";
            }

            var style = EditorStyles.popup;
            var buttonContent = new GUIContent(currentName);

            // Draw the Button
            if (GUI.Button(position, buttonContent, style))
            {
                // Resolve filter attribute if present
                string filterDomain = null;
                var attributes = fieldInfo.GetCustomAttributes(typeof(SemanticKeyFilterAttribute), true);
                if (attributes.Length > 0)
                {
                    filterDomain = ((SemanticKeyFilterAttribute)attributes[0]).DomainName;
                }

                // The selection arrives after this OnGUI call (for "+ Add Key", after another window closes),
                // when 'property' may no longer be valid. Keep its objects and path, and find it again then.
                var targets = property.serializedObject.targetObjects;
                var propertyPath = property.propertyPath;

                var dropdown = new SemanticKeyDropdown(new UnityEditor.IMGUI.Controls.AdvancedDropdownState(), filterDomain);
                dropdown.OnItemSelected += (item) => ApplySelection(targets, propertyPath, item);
                dropdown.Show(position);
            }

            EditorGUI.EndProperty();
        }

        private static void ApplySelection(Object[] targets, string propertyPath, SemanticKeyItem item)
        {
            var liveTargets = targets.Where(t => t != null).ToArray();
            if (liveTargets.Length == 0) return;

            using (var serializedObject = new SerializedObject(liveTargets))
            {
                var property = serializedObject.FindProperty(propertyPath);
                if (property == null) return;

                // Apply changes (to every selected object)
                property.FindPropertyRelative("_guid").stringValue = item.Guid;
                property.FindPropertyRelative("_value").stringValue = item.Value;
                property.FindPropertyRelative("_domainGuid").stringValue = item.DomainGuid;

                serializedObject.ApplyModifiedProperties();
            }
        }
    }
}