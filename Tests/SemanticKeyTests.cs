using NUnit.Framework;
using SemanticKeys;
using System.Collections.Generic;
using UnityEngine;

namespace SemanticKeys.Tests
{
    public class SemanticKeyTests
    {
        [Test]
        public void Equals_IdenticalKeys_ReturnsTrue()
        {
            var key1 = new SemanticKey("guid_1", "Health", "domain_1");
            var key2 = new SemanticKey("guid_1", "Health", "domain_1");

            Assert.IsTrue(key1.Equals(key2));
            Assert.IsTrue(key1 == key2);
        }

        [Test]
        public void Equals_DifferentGuid_ReturnsFalse()
        {
            var key1 = new SemanticKey("guid_1", "Health", "domain_1");
            var key2 = new SemanticKey("guid_2", "Health", "domain_1"); // Same name, diff GUID

            Assert.IsFalse(key1.Equals(key2));
            Assert.IsFalse(key1 == key2);
        }

        [Test]
        public void ImplicitString_ReturnsValue()
        {
            var key = new SemanticKey("guid_1", "Agility", "domain_1");
            string value = key;

            Assert.AreEqual("Agility", value);
        }

        [Test]
        public void ImplicitString_NullValue_ReturnsEmpty()
        {
            // Simulating a partially deserialized key where Value is missing
            var key = new SemanticKey("guid_1", null, "domain_1");
            string value = key;

            Assert.IsNotNull(value);
            Assert.AreEqual(string.Empty, value);
        }

        // --- THE DANGER ZONE TESTS ---
        // These tests highlight the exact problem likely breaking your system.

        [Test]
        public void SplitTruth_EqualityBasedOnGuid_ButStringBasedOnValue()
        {
            // Scenario: You renamed a key in the editor, but the runtime data hasn't updated perfectly
            var keyOldName = new SemanticKey("guid_100", "Health_Old", "domain_1");
            var keyNewName = new SemanticKey("guid_100", "Health_New", "domain_1");

            // They are "Equal" objects because GUIDs match
            Assert.IsTrue(keyOldName == keyNewName, "Keys should be equal via GUID");

            // BUT they produce different strings
            string str1 = keyOldName;
            string str2 = keyNewName;

            Assert.AreNotEqual(str1, str2, "String representations are different");

            // This causes Dictionary chaos:
            var dict = new Dictionary<string, int>();
            dict[keyNewName] = 10;

            // Looking up with the 'Old' key (which is technically Equal to the New Key) fails
            // because the Dictionary uses the string conversion, not the Struct Equality
            Assert.IsFalse(dict.ContainsKey(keyOldName));
        }

        [Test]
        public void EmptyGuid_TreatsAllAsNone()
        {
            // If you try to use SemanticKey simply as a string wrapper without generating GUIDs...
            var keyHealth = new SemanticKey(null, "Health", null);
            var keyMana = new SemanticKey(null, "Mana", null);

            // They are BOTH considered 'None' because GUID is missing
            Assert.AreEqual(SemanticKey.None, keyHealth);
            Assert.AreEqual(SemanticKey.None, keyMana);

            // Which means they are equal to each other!
            Assert.IsTrue(keyHealth == keyMana, "Without GUIDs, different keys are considered Equal!");
        }

        [Test]
        public void IsValid_ReturnsTrue_OnlyIfGuidAndValueExist()
        {
            var validKey = new SemanticKey("guid", "value", "domain");

            // Invalid states
            var missingGuid = new SemanticKey(null, "value", "domain");
            var missingValue = new SemanticKey("guid", null, "domain");
            var defaultKey = default(SemanticKey);

            Assert.IsTrue(validKey.IsValid);
            Assert.IsFalse(missingGuid.IsValid);
            // Assuming implementation requires Value to be non-empty for validity
            Assert.IsFalse(missingValue.IsValid);
            Assert.IsFalse(defaultKey.IsValid);
        }

        [Test]
        public void DefaultStruct_Equals_None()
        {
            // A default struct (all nulls) must behave exactly like SemanticKey.None
            var def = default(SemanticKey);

            Assert.AreEqual(SemanticKey.None, def);
            Assert.AreEqual(string.Empty, def.Value);
            Assert.AreEqual(string.Empty, (string)def);
        }

        [Test]
        public void GetHashCode_Consistency()
        {
            // If Equals() is true, GetHashCode() MUST be identical. 
            // This is mandatory for using SemanticKeys as Dictionary keys.
            var key1 = new SemanticKey("guid_A", "Val", "dom");
            var key2 = new SemanticKey("guid_A", "DiffVal", "dom"); // Equal via GUID

            Assert.IsTrue(key1.Equals(key2));
            Assert.AreEqual(key1.GetHashCode(), key2.GetHashCode(), "HashCodes must match if Equals() is true");

            // Verify None/Default hash codes
            var none = SemanticKey.None;
            var def = default(SemanticKey);
            Assert.AreEqual(none.GetHashCode(), def.GetHashCode());
        }

        [Test]
        public void ToString_ReturnsValue()
        {
            var key = new SemanticKey("guid", "MyString", "dom");
            Assert.AreEqual("MyString", key.ToString());

            Assert.AreEqual(string.Empty, SemanticKey.None.ToString());
        }
    }
}