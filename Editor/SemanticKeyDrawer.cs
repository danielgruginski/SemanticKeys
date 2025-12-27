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
            var domainGuidProp = property.FindPropertyRelative("_domainGuid");

            // Draw Label
            position = EditorGUI.PrefixLabel(position, GUIUtility.GetControlID(FocusType.Passive), label);

            // Calculate Style
            var currentName = valueProp.stringValue;
            if (string.IsNullOrEmpty(currentName))
            {
                currentName = "Select Key...";
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

                var dropdown = new SemanticKeyDropdown(new UnityEditor.IMGUI.Controls.AdvancedDropdownState(), filterDomain);
                dropdown.OnItemSelected += (item) =>
                {
                    // Apply changes
                    guidProp.stringValue = item.Guid;
                    valueProp.stringValue = item.name;
                    domainGuidProp.stringValue = item.DomainGuid;

                    property.serializedObject.ApplyModifiedProperties();
                };
                dropdown.Show(position);
            }

            EditorGUI.EndProperty();
        }
    }
}