using System;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;

#if UNITY_EDITOR
using UnityEditor;
using System.IO;
using System.Text.RegularExpressions;
#endif

namespace SemanticKeys
{
    [CreateAssetMenu(fileName = "NewKeyDomain", menuName = "SemanticKeys/Key Domain")]
    public class KeyDomain : ScriptableObject, ISerializationCallbackReceiver
    {
        [Serializable]
        public class KeyDefinition
        {
            [SerializeField] private string _name;
            [SerializeField] private string _guid;
            [TextArea][SerializeField] private string _description;

            public string Name => _name;
            public string Guid => _guid;
            public string Description => _description;

            public KeyDefinition(string name)
            {
                _name = name;
                _guid = System.Guid.NewGuid().ToString();
            }

            public void ValidateGuid() { if (string.IsNullOrEmpty(_guid)) _guid = System.Guid.NewGuid().ToString(); }
            public void RegenerateGuid() { _guid = System.Guid.NewGuid().ToString(); }

            // Internal setters for Editor mutation
            public void SetName(string name) => _name = name;
        }

        [SerializeField] private string _domainName;
        [SerializeField] private List<KeyDefinition> _keys = new List<KeyDefinition>();

        // Runtime Cache
        private Dictionary<string, KeyDefinition> _guidLookup;

        public string DomainName => _domainName;
        public IEnumerable<KeyDefinition> Keys => _keys;
        public string Guid { get; private set; }

        private void OnEnable()
        {
            if (string.IsNullOrEmpty(Guid)) Guid = System.Guid.NewGuid().ToString();
            if (string.IsNullOrEmpty(_domainName)) _domainName = name;
            RebuildLookup();
        }

        public void OnBeforeSerialize() { }
        public void OnAfterDeserialize() { RebuildLookup(); }

        private void RebuildLookup()
        {
            if (_keys == null) return;
            _guidLookup = new Dictionary<string, KeyDefinition>(_keys.Count);
            foreach (var key in _keys)
            {
                if (key != null && !string.IsNullOrEmpty(key.Guid) && !_guidLookup.ContainsKey(key.Guid))
                {
                    _guidLookup.Add(key.Guid, key);
                }
            }
        }

        /// <summary>
        /// O(1) Lookup.
        /// </summary>
        public bool TryGetKeyByGuid(string guid, out string keyName)
        {
            if (_guidLookup == null) RebuildLookup();

            if (_guidLookup.TryGetValue(guid, out var def))
            {
                keyName = def.Name;
                return true;
            }
            keyName = null;
            return false;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (string.IsNullOrEmpty(Guid)) Guid = System.Guid.NewGuid().ToString();
            if (_keys != null)
            {
                var seenGuids = new HashSet<string>();
                foreach (var key in _keys)
                {
                    if (key == null) continue;
                    key.ValidateGuid();
                    if (seenGuids.Contains(key.Guid)) key.RegenerateGuid();
                    seenGuids.Add(key.Guid);
                }
            }
        }

        public void SetDomainName(string name)
        {
            _domainName = name;
            EditorUtility.SetDirty(this);
        }

        public KeyDefinition AddKey(string name)
        {
            var existing = _keys.FirstOrDefault(k => k.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
            if (existing != null) return existing;

            Undo.RecordObject(this, $"Add Key '{name}'");
            var newKey = new KeyDefinition(name);
            _keys.Add(newKey);
            EditorUtility.SetDirty(this);
            RebuildLookup();
            return newKey;
        }

        public bool RenameKey(string guid, string newName)
        {
            var key = _keys.FirstOrDefault(k => k.Guid == guid);
            if (key == null) return false;

            if (_keys.Any(k => k.Guid != guid && k.Name.Equals(newName, StringComparison.OrdinalIgnoreCase)))
            {
                Debug.LogWarning($"[SemanticKeys] Key '{newName}' already exists.");
                return false;
            }

            Undo.RecordObject(this, $"Rename Key '{key.Name}' to '{newName}'");
            key.SetName(newName);
            EditorUtility.SetDirty(this);
            RebuildLookup();
            return true;
        }

        public void DeleteKey(string guid)
        {
            var key = _keys.FirstOrDefault(k => k.Guid == guid);
            if (key == null) return;

            Undo.RecordObject(this, $"Delete Key '{key.Name}'");
            _keys.Remove(key);
            EditorUtility.SetDirty(this);
            RebuildLookup();
        }

        public void RenameDomain(string newName)
        {
            if (string.IsNullOrEmpty(newName) || _domainName.Equals(newName, StringComparison.Ordinal)) return;

            string oldName = _domainName;
            string oldClassName = SanitizeClassName(oldName);

            _domainName = newName;
            EditorUtility.SetDirty(this);

            string assetPath = AssetDatabase.GetAssetPath(this);
            if (!string.IsNullOrEmpty(assetPath))
            {
                string newFileName = newName.Replace(" ", "");
                AssetDatabase.RenameAsset(assetPath, newFileName);
            }

            // Cleanup old code
            var settings = SemanticKeysSettings.GetOrCreateSettings();
            string oldFilePath = Path.Combine(settings.GeneratedCodePath, $"{oldClassName}.cs").Replace("\\", "/");
            if (File.Exists(oldFilePath)) AssetDatabase.DeleteAsset(oldFilePath);

            GenerateCode();
            AssetDatabase.SaveAssets();
            Debug.Log($"[SemanticKeys] Renamed Domain '{oldName}' -> '{newName}'");
        }

        [ContextMenu("Generate Static Class")]
        public void GenerateCode()
        {
            var settings = SemanticKeysSettings.GetOrCreateSettings();
            string folderPath = settings.GeneratedCodePath;
            string targetNamespace = settings.GeneratedNamespace;

            if (!Directory.Exists(folderPath)) Directory.CreateDirectory(folderPath);

            string className = SanitizeClassName(_domainName);
            var sb = new System.Text.StringBuilder();

            sb.AppendLine("// <auto-generated>");
            sb.AppendLine($"// Generated by SemanticKeys.");
            sb.AppendLine("// </auto-generated>");
            sb.AppendLine("");
            sb.AppendLine($"namespace {targetNamespace}");
            sb.AppendLine("{");
            sb.AppendLine($"    using SemanticKeys;");
            sb.AppendLine("");
            sb.AppendLine($"    public static class {className}");
            sb.AppendLine("    {");

            HashSet<string> usedNames = new HashSet<string>();
            foreach (var key in _keys)
            {
                string variableName = SanitizeVariableName(key.Name);
                if (usedNames.Contains(variableName))
                {
                    int index = 1;
                    while (usedNames.Contains($"{variableName}_{index}")) index++;
                    variableName = $"{variableName}_{index}";
                }
                usedNames.Add(variableName);
                sb.AppendLine($"        public static readonly SemanticKey {variableName} = new SemanticKey(\"{key.Guid}\", \"{key.Name}\", \"{this.Guid}\");");
            }
            sb.AppendLine("    }");
            sb.AppendLine("}");

            try
            {
                File.WriteAllText(Path.Combine(folderPath, $"{className}.cs"), sb.ToString());
                AssetDatabase.Refresh();
            }
            catch (Exception e) { Debug.LogError($"[SemanticKeys] Generation failed: {e.Message}"); }
        }

        private string SanitizeClassName(string input) => Regex.Replace(input, @"[^a-zA-Z0-9_]", "");

        private string SanitizeVariableName(string input)
        {
            string temp = Regex.Replace(input.Replace(".", "_").Replace(" ", "_"), @"[^a-zA-Z0-9_]", "");
            if (temp.Length > 0 && char.IsDigit(temp[0])) temp = "_" + temp;
            return temp;
        }
#endif
    }
}