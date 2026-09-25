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
        // Serialized so the domain keeps its GUID across sessions (it is written into keys and generated code).
        [SerializeField] private string _guid;
        [SerializeField] private List<KeyDefinition> _keys = new List<KeyDefinition>();

        // Runtime Cache
        private Dictionary<string, KeyDefinition> _guidLookup;

        public string DomainName => _domainName;
        public IEnumerable<KeyDefinition> Keys => _keys;
        public string Guid => _guid;

        private void OnEnable()
        {
            EnsureGuid();
            if (string.IsNullOrEmpty(_domainName)) _domainName = name;
            RebuildLookup();
        }

        private void EnsureGuid()
        {
            if (!string.IsNullOrEmpty(_guid)) return;
            _guid = System.Guid.NewGuid().ToString();
#if UNITY_EDITOR
            // Domains saved before the GUID was serialized get one now; mark them dirty so it is saved.
            EditorUtility.SetDirty(this);
#endif
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
            EnsureGuid();
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
            string oldFilePath = GetGeneratedFilePath(settings, oldClassName);
            if (File.Exists(oldFilePath)) AssetDatabase.DeleteAsset(oldFilePath);

            GenerateCode();
            AssetDatabase.SaveAssets();
            Debug.Log($"[SemanticKeys] Renamed Domain '{oldName}' -> '{newName}'");
        }

        [ContextMenu("Generate Static Class")]
        public void GenerateCode()
        {
            string className = SanitizeClassName(_domainName);
            if (className.Length == 0)
            {
                Debug.LogError($"[SemanticKeys] Cannot generate a class for domain '{_domainName}': its name has no letters or digits.");
                return;
            }

            var settings = SemanticKeysSettings.GetOrCreateSettings();
            string folderPath = settings.GeneratedCodePath;
            string filePath = GetGeneratedFilePath(settings, className);
            string code = GenerateCodeText(settings.GeneratedNamespace);

            try
            {
                if (!Directory.Exists(folderPath)) Directory.CreateDirectory(folderPath);

                // Leave an up-to-date file alone, so regenerating doesn't trigger a recompile.
                if (File.Exists(filePath) && File.ReadAllText(filePath) == code) return;

                File.WriteAllText(filePath, code);
                AssetDatabase.Refresh();
            }
            catch (Exception e) { Debug.LogError($"[SemanticKeys] Generation failed: {e.Message}"); }
        }

        /// <summary>
        /// Regenerates the static class if it was generated before, so it never keeps renamed or deleted keys.
        /// Returns false, and generates nothing, if the class has no file in the Generated Code Path.
        /// </summary>
        public bool UpdateGeneratedCode()
        {
            string className = SanitizeClassName(_domainName);
            if (className.Length == 0) return false;

            var settings = SemanticKeysSettings.GetOrCreateSettings();
            if (!File.Exists(GetGeneratedFilePath(settings, className))) return false;

            GenerateCode();
            return true;
        }

        /// <summary>
        /// The source of the static class. Names are made valid identifiers (see <see cref="SanitizeVariableName"/>);
        /// a name taken by an earlier key, or by the class itself, gets a numeric suffix.
        /// </summary>
        internal string GenerateCodeText(string targetNamespace)
        {
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
            sb.AppendLine($"    public static class {EscapeKeyword(className)}");
            sb.AppendLine("    {");

            // A member can't have the name of its class (CS0542).
            HashSet<string> usedNames = new HashSet<string> { className };
            foreach (var key in _keys)
            {
                if (key == null) continue;
                string variableName = SanitizeVariableName(key.Name);
                if (usedNames.Contains(variableName))
                {
                    int index = 1;
                    while (usedNames.Contains($"{variableName}_{index}")) index++;
                    variableName = $"{variableName}_{index}";
                }
                usedNames.Add(variableName);
                sb.AppendLine($"        public static readonly SemanticKey {EscapeKeyword(variableName)} = new SemanticKey({ToStringLiteral(key.Guid)}, {ToStringLiteral(key.Name)}, {ToStringLiteral(_guid)});");
            }
            sb.AppendLine("    }");
            sb.AppendLine("}");
            return sb.ToString();
        }

        private static string GetGeneratedFilePath(SemanticKeysSettings settings, string className) =>
            Path.Combine(settings.GeneratedCodePath, $"{className}.cs").Replace("\\", "/");

        /// <summary>
        /// The class name for a domain: letters, digits and underscores only, with a leading '_' if it starts
        /// with a digit. Empty if the name has none of these characters.
        /// </summary>
        internal static string SanitizeClassName(string input)
        {
            string temp = Regex.Replace(input ?? string.Empty, @"[^a-zA-Z0-9_]", "");
            if (temp.Length > 0 && char.IsDigit(temp[0])) temp = "_" + temp;
            return temp;
        }

        /// <summary>
        /// The field name for a key: dots and spaces become '_', other characters that aren't letters, digits or
        /// underscores are removed, and a leading digit gets a '_'. A name with nothing left becomes "Key".
        /// </summary>
        internal static string SanitizeVariableName(string input)
        {
            string temp = Regex.Replace((input ?? string.Empty).Replace(".", "_").Replace(" ", "_"), @"[^a-zA-Z0-9_]", "");
            if (temp.Length == 0) return "Key";
            if (char.IsDigit(temp[0])) temp = "_" + temp;
            return temp;
        }

        private static readonly HashSet<string> CSharpKeywords = new HashSet<string>
        {
            "abstract", "as", "base", "bool", "break", "byte", "case", "catch", "char", "checked", "class", "const",
            "continue", "decimal", "default", "delegate", "do", "double", "else", "enum", "event", "explicit", "extern",
            "false", "finally", "fixed", "float", "for", "foreach", "goto", "if", "implicit", "in", "int", "interface",
            "internal", "is", "lock", "long", "namespace", "new", "null", "object", "operator", "out", "override",
            "params", "private", "protected", "public", "readonly", "ref", "return", "sbyte", "sealed", "short",
            "sizeof", "stackalloc", "static", "string", "struct", "switch", "this", "throw", "true", "try", "typeof",
            "uint", "ulong", "unchecked", "unsafe", "ushort", "using", "virtual", "void", "volatile", "while",
            "__arglist", "__makeref", "__reftype", "__refvalue"
        };

        /// <summary>Prefixes C# keywords with '@' (e.g. a key named "default" becomes the field <c>@default</c>).</summary>
        internal static string EscapeKeyword(string identifier) =>
            CSharpKeywords.Contains(identifier) ? "@" + identifier : identifier;

        /// <summary>A C# string literal for <paramref name="value"/>: quotes, backslashes and control characters are escaped.</summary>
        internal static string ToStringLiteral(string value)
        {
            var sb = new System.Text.StringBuilder("\"");
            foreach (char c in value ?? string.Empty)
            {
                switch (c)
                {
                    case '"': sb.Append("\\\""); break;
                    case '\\': sb.Append("\\\\"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    default:
                        // Other control characters and the Unicode line separators can't appear in a literal as-is.
                        if (char.IsControl(c) || c == '\u2028' || c == '\u2029') sb.Append("\\u").Append(((int)c).ToString("x4"));
                        else sb.Append(c);
                        break;
                }
            }
            return sb.Append('"').ToString();
        }
#endif
    }
}