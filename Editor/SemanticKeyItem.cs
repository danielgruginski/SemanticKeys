using UnityEditor.IMGUI.Controls;
using UnityEngine;

namespace SemanticKeys.Editor
{
    /// <summary>
    /// A wrapper class required by Unity's AdvancedDropdown API.
    /// </summary>
    public class SemanticKeyItem : AdvancedDropdownItem
    {
        public enum CreationCommandType
        {
            None,
            CreateDomain,
            CreateKey
        }

        public string Guid { get; }
        public string Value { get; } // New: Separates UI Name from Data Value
        public string DomainGuid { get; }

        public bool IsCreationCommand { get; set; }
        public CreationCommandType CreationType { get; set; }
        public KeyDomain DomainAsset { get; set; }

        // Updated Constructor to take 'value'
        public SemanticKeyItem(string name, string value, string keyGuid, string domainGuid) : base(name)
        {
            Value = value;
            Guid = keyGuid;
            DomainGuid = domainGuid;
            IsCreationCommand = false;
            CreationType = CreationCommandType.None;
        }
    }
}