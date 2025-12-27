using NUnit.Framework;
using SemanticKeys;
using UnityEngine;
using System.Linq;

namespace SemanticKeys.Tests
{
    public class KeyDomainTests
    {
        private KeyDomain _domain;

        [SetUp]
        public void Setup()
        {
            // Create a fresh instance for every test to ensure isolation
            _domain = ScriptableObject.CreateInstance<KeyDomain>();
        }

        [TearDown]
        public void Teardown()
        {
            Object.DestroyImmediate(_domain);
        }

        [Test]
        public void AddKey_NewName_IncrementsCount()
        {
            _domain.AddKey("Strength");
            Assert.AreEqual(1, _domain.Keys.Count());

            _domain.AddKey("Agility");
            Assert.AreEqual(2, _domain.Keys.Count());
        }

        [Test]
        public void AddKey_DuplicateName_ReturnsExisting_DoesNotIncrement()
        {
            var key1 = _domain.AddKey("Strength");
            var key2 = _domain.AddKey("Strength"); // Exact match
            var key3 = _domain.AddKey("strength"); // Case-insensitive match

            Assert.AreEqual(1, _domain.Keys.Count());
            Assert.AreSame(key1, key2);
            Assert.AreSame(key1, key3);
        }

        [Test]
        public void TryGetKeyByGuid_ValidGuid_ReturnsTrueAndName()
        {
            var keyDef = _domain.AddKey("Intelligence");
            string validGuid = keyDef.Guid;

            bool found = _domain.TryGetKeyByGuid(validGuid, out string name);

            Assert.IsTrue(found);
            Assert.AreEqual("Intelligence", name);
        }

        [Test]
        public void TryGetKeyByGuid_InvalidGuid_ReturnsFalse()
        {
            _domain.AddKey("Intelligence");

            bool found = _domain.TryGetKeyByGuid("invalid-guid-123", out string name);

            Assert.IsFalse(found);
            Assert.IsNull(name);
        }

        [Test]
        public void KeyDefinition_AutoGeneratesGuid_OnCreation()
        {
            var keyDef = new KeyDomain.KeyDefinition("TestKey");

            Assert.IsNotNull(keyDef.Guid);
            Assert.IsNotEmpty(keyDef.Guid);
            Assert.AreEqual("TestKey", keyDef.Name);
        }

        [Test]
        public void KeyDefinition_ValidateGuid_FixesEmptyGuid()
        {
            // Simulate a broken key (e.g. from bad serialization)
            var keyDef = new KeyDomain.KeyDefinition("BrokenKey");

            // Reflection is needed to set private field _guid to null/empty for testing
            var field = typeof(KeyDomain.KeyDefinition).GetField("_guid", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            field.SetValue(keyDef, "");

            Assert.IsEmpty(keyDef.Guid);

            // Run validation
            keyDef.ValidateGuid();

            Assert.IsNotEmpty(keyDef.Guid);
        }
    }
}