using System;
using UnityEngine;

namespace SemanticKeys
{
    /// <summary>
    /// A wrapper for a string that is backed by a GUID-based definition.
    /// Eliminates magic strings by enforcing selection from a KeyDomain.
    /// </summary>
    [Serializable]
    public struct SemanticKey : IEquatable<SemanticKey>
    {
        // The immutable identity of this key.
        [SerializeField] private string _guid;

        // The cached string value. We verify this against the GUID in Editor,
        // but use this directly at runtime for O(1) performance.
        [SerializeField] private string _value;

        // Optional: Store the Domain ID if you want to enforce strict typing
        [SerializeField] private string _domainGuid;

        public string Value => _value;
        public string Guid => _guid;
        public bool IsValid => !string.IsNullOrEmpty(_guid) && !string.IsNullOrEmpty(_value);

        public SemanticKey(string guid, string value, string domainGuid)
        {
            _guid = guid;
            _value = value;
            _domainGuid = domainGuid;
        }

        // Implicit conversion allows drop-in replacement for string APIs
        // e.g., animator.SetTrigger(MySemanticKey);
        public static implicit operator string(SemanticKey key) => key._value;

        public bool Equals(SemanticKey other)
        {
            // We compare GUIDs for strict equality, not the string value
            // This handles the case where "Strength" was renamed to "Might" 
            // but the serialized object hasn't updated its cache yet.
            return _guid == other._guid;
        }

        public override bool Equals(object obj) => obj is SemanticKey other && Equals(other);
        public override int GetHashCode() => _guid != null ? _guid.GetHashCode() : 0;
        public static bool operator ==(SemanticKey left, SemanticKey right) => left.Equals(right);
        public static bool operator !=(SemanticKey left, SemanticKey right) => !left.Equals(right);

        public override string ToString() => _value;
    }
}