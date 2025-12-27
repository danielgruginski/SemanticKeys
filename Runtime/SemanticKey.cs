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
        /// <summary>
        /// Represents an empty/unassigned key.
        /// </summary>
        public static readonly SemanticKey None = new SemanticKey();

        // The immutable identity of this key.
        [SerializeField] private string _guid;

        // The cached string value. We verify this against the GUID in Editor,
        // but use this directly at runtime for O(1) performance.
        [SerializeField] private string _value;

        // Optional: Store the Domain ID if you want to enforce strict typing
        [SerializeField] private string _domainGuid;

        public string Value => _value ?? string.Empty;
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
        // Changed to return string.Empty if null to prevent NullReferenceExceptions in legacy code
        public static implicit operator string(SemanticKey key) => key._value ?? string.Empty;

        public bool Equals(SemanticKey other)
        {
            // We treat null and empty as the same identity (None)
            var thisGuid = string.IsNullOrEmpty(_guid) ? string.Empty : _guid;
            var otherGuid = string.IsNullOrEmpty(other._guid) ? string.Empty : other._guid;

            return thisGuid == otherGuid;
        }

        public override bool Equals(object obj) => obj is SemanticKey other && Equals(other);

        public override int GetHashCode()
        {
            // Must match Equals logic: treat null same as empty
            return string.IsNullOrEmpty(_guid) ? 0 : _guid.GetHashCode();
        }

        public static bool operator ==(SemanticKey left, SemanticKey right) => left.Equals(right);
        public static bool operator !=(SemanticKey left, SemanticKey right) => !left.Equals(right);

        public override string ToString() => _value ?? string.Empty;
    }
}