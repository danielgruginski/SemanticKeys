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
            var root = new AdvancedDropdownItem("Semantic Keys");

            // --- 0. Add "None" Option ---
            // Name shows "None", but Value and Guid are string.Empty
            var noneItem = new SemanticKeyItem("None", string.Empty, string.Empty, string.Empty)
            {
                icon = (Texture2D)EditorGUIUtility.IconContent("d_Refresh").image
            };
            root.AddChild(noneItem);

            // Removed the separator "----" item to prevent selection bugs.

            // --- 1. Create Domain Option ---
            if (string.IsNullOrEmpty(_filterDomain))
            {
                // Pass nulls for command items, handled in HandleCreation
                var createDomainItem = new SemanticKeyItem(" + Create New Domain", null, null, null)
                {
                    IsCreationCommand = true,
                    CreationType = SemanticKeyItem.CreationCommandType.CreateDomain
                };
                createDomainItem.icon = (Texture2D)EditorGUIUtility.IconContent("CreateAddNew").image;
                root.AddChild(createDomainItem);
            }

            // --- 2. Populate Lists ---
            var guids = AssetDatabase.FindAssets("t:KeyDomain");
            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var domain = AssetDatabase.LoadAssetAtPath<KeyDomain>(path);
                if (domain == null) continue;

                if (!string.IsNullOrEmpty(_filterDomain) &&
                    !domain.DomainName.Equals(_filterDomain, System.StringComparison.OrdinalIgnoreCase)) continue;

                var domainGroup = new AdvancedDropdownItem(domain.DomainName);
                domainGroup.icon = (Texture2D)EditorGUIUtility.IconContent("ScriptableObject Icon").image;

                var createKeyItem = new SemanticKeyItem(" + Add Key", null, null, domain.Guid)
                {
                    IsCreationCommand = true,
                    CreationType = SemanticKeyItem.CreationCommandType.CreateKey,
                    DomainAsset = domain
                };
                createKeyItem.icon = (Texture2D)EditorGUIUtility.IconContent("Toolbar Plus").image;
                domainGroup.AddChild(createKeyItem);

                foreach (var key in domain.Keys)
                {
                    // Pass Name as both Name (UI) and Value (Data)
                    var item = new SemanticKeyItem(key.Name, key.Name, key.Guid, domain.Guid);
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

            // Use keyName for value, temp guid (real one generated inside AddKey)
            // Ideally we'd fetch the real guid but for UI update 'temp' is fine as long as we reload or user re-selects later
            // For immediate correctness, we can try to find the key we just added:
            var addedKey = System.Linq.Enumerable.FirstOrDefault(domain.Keys, k => k.Name == keyName);
            string realGuid = addedKey != null ? addedKey.Guid : "temp";

            var newItem = new SemanticKeyItem(keyName, keyName, realGuid, domain.Guid);
            OnItemSelected?.Invoke(newItem);
        }
    }
}