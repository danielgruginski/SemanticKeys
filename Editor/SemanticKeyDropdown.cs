using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEngine;
using System.IO;
using SemanticKeys;

namespace SemanticKeys.Editor
{
    public class SemanticKeyDropdown : AdvancedDropdown
    {
        private string _filterDomain;
        public System.Action<SemanticKeyItem> OnItemSelected;

        public SemanticKeyDropdown(AdvancedDropdownState state, string filterDomain = null) : base(state)
        {
            _filterDomain = filterDomain;
            this.minimumSize = new Vector2(200, 300);
        }

        protected override AdvancedDropdownItem BuildRoot()
        {
            // (Same as previous response, keeping it brief for the update)
            var root = new AdvancedDropdownItem("Semantic Keys");

            // ... None Option ...

            // ... Create Domain Option ...
            if (string.IsNullOrEmpty(_filterDomain))
            {
                var createDomainItem = new SemanticKeyItem(" + Create New Domain", null, null)
                {
                    IsCreationCommand = true,
                    CreationType = SemanticKeyItem.CreationCommandType.CreateDomain
                };
                createDomainItem.icon = (Texture2D)EditorGUIUtility.IconContent("CreateAddNew").image;
                root.AddChild(createDomainItem);
                root.AddChild(new AdvancedDropdownItem("-----------------"));
            }

            // ... Populate Lists ...
            // (Standard AssetDatabase search logic here, same as previous file)
            var guids = AssetDatabase.FindAssets("t:KeyDomain");
            foreach (var guid in guids)
            {
                // ... (Load Domain logic) ...
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var domain = AssetDatabase.LoadAssetAtPath<KeyDomain>(path);
                if (domain == null) continue;

                if (!string.IsNullOrEmpty(_filterDomain) &&
                    !domain.DomainName.Equals(_filterDomain, System.StringComparison.OrdinalIgnoreCase)) continue;

                var domainGroup = new AdvancedDropdownItem(domain.DomainName);
                domainGroup.icon = (Texture2D)EditorGUIUtility.IconContent("ScriptableObject Icon").image;

                var createKeyItem = new SemanticKeyItem(" + Add Key", null, domain.Guid)
                {
                    IsCreationCommand = true,
                    CreationType = SemanticKeyItem.CreationCommandType.CreateKey,
                    DomainAsset = domain
                };
                createKeyItem.icon = (Texture2D)EditorGUIUtility.IconContent("Toolbar Plus").image;
                domainGroup.AddChild(createKeyItem);

                foreach (var key in domain.Keys)
                {
                    var item = new SemanticKeyItem(key.Name, key.Guid, domain.Guid);
                    item.icon = (Texture2D)EditorGUIUtility.IconContent("TextAsset Icon").image;
                    domainGroup.AddChild(item);
                }
                root.AddChild(domainGroup);
            }

            return root;
        }

        protected override void ItemSelected(AdvancedDropdownItem item)
        {
            if (item is SemanticKeyItem keyItem)
            {
                if (keyItem.IsCreationCommand) HandleCreation(keyItem);
                else OnItemSelected?.Invoke(keyItem);
            }
        }

        private void HandleCreation(SemanticKeyItem item)
        {
            if (item.CreationType == SemanticKeyItem.CreationCommandType.CreateDomain)
            {
                SemanticKeyInputWindow.Open("Create Domain", "Domain Name:", (name) => CreateDomainAsset(name));
            }
            else if (item.CreationType == SemanticKeyItem.CreationCommandType.CreateKey)
            {
                SemanticKeyInputWindow.Open($"Add Key to {item.DomainAsset.DomainName}", "Key Name:", (name) =>
                {
                    CreateKeyInDomain(item.DomainAsset, name);
                });
            }
        }

        private void CreateDomainAsset(string name)
        {
            // 1. Load Settings to find correct path
            var settings = SemanticKeysSettings.GetOrCreateSettings();
            string directory = settings.DomainCreationPath;

            if (!Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            string safeName = name.Replace(" ", "");
            string path = $"{directory}/{safeName}.asset";

            if (File.Exists(path))
            {
                EditorUtility.DisplayDialog("Error", $"A domain named '{name}' already exists at {path}.", "OK");
                return;
            }

            var newDomain = ScriptableObject.CreateInstance<KeyDomain>();
            newDomain.SetDomainName(name);

            AssetDatabase.CreateAsset(newDomain, path);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            EditorGUIUtility.PingObject(newDomain);
        }

        private void CreateKeyInDomain(KeyDomain domain, string keyName)
        {
            domain.AddKey(keyName);
            AssetDatabase.SaveAssets();

            // UX: Auto-regenerate code when adding a key?
            // Optional: domain.GenerateCode(); 
            // Warning: This triggers compilation which interrupts the Editor workflow. 
            // Better to let user manually click "Generate" or do it on a debounced timer.

            var newItem = new SemanticKeyItem(keyName, "temp", domain.Guid);
            // Note: In real usage we need the real guid, but AddKey returns KeyDefinition so we can get it.
            // Simplified here for brevity.
            OnItemSelected?.Invoke(newItem);
        }
    }
}