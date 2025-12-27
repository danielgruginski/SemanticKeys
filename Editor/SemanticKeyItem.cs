using UnityEditor.IMGUI.Controls;
using UnityEngine;

namespace SemanticKeys.Editor
{
    /// <summary>
    /// A wrapper class required by Unity's AdvancedDropdown API.
    /// It carries the data from the Domain/Key into the UI list,
    /// and handles special "Command" items like "Create New Domain".
    /// </summary>
    public class SemanticKeyItem : AdvancedDropdownItem
    {
        public enum CreationCommandType
        {
            None,
            CreateDomain,
            CreateKey
        }

        // Data needed to construct the runtime SemanticKey struct
        public string Guid { get; }
        public string DomainGuid { get; }

        // Data needed for the "Create New..." logic
        public bool IsCreationCommand { get; set; }
        public CreationCommandType CreationType { get; set; }
        public KeyDomain DomainAsset { get; set; } // Reference needed so we know where to add the new key

        public SemanticKeyItem(string name, string keyGuid, string domainGuid) : base(name)
        {
            Guid = keyGuid;
            DomainGuid = domainGuid;
            IsCreationCommand = false;
            CreationType = CreationCommandType.None;
        }
    }
}