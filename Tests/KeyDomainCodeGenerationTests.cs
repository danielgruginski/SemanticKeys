using NUnit.Framework;
using SemanticKeys;
using UnityEngine;

namespace SemanticKeys.Tests
{
    public class KeyDomainCodeGenerationTests
    {
        private KeyDomain _domain;

        [SetUp]
        public void Setup()
        {
            _domain = ScriptableObject.CreateInstance<KeyDomain>();
            _domain.SetDomainName("Stats");
        }

        [TearDown]
        public void Teardown()
        {
            Object.DestroyImmediate(_domain);
        }

        [Test]
        public void GeneratedCode_DeclaresAFieldPerKey()
        {
            var strength = _domain.AddKey("Strength");

            string code = _domain.GenerateCodeText("Game.Constants");

            StringAssert.Contains("namespace Game.Constants", code);
            StringAssert.Contains("public static class Stats", code);
            StringAssert.Contains($"public static readonly SemanticKey Strength = new SemanticKey(\"{strength.Guid}\", \"Strength\", \"{_domain.Guid}\");", code);
        }

        [Test]
        public void GeneratedCode_EscapesQuotesAndBackslashes()
        {
            _domain.AddKey("Say \"Hi\" \\o/");

            string code = _domain.GenerateCodeText("Game.Constants");

            StringAssert.Contains("\"Say \\\"Hi\\\" \\\\o/\"", code);
        }

        [Test]
        public void GeneratedCode_KeywordNames_GetAtPrefix()
        {
            _domain.AddKey("default");
            _domain.AddKey("event");

            string code = _domain.GenerateCodeText("Game.Constants");

            StringAssert.Contains("SemanticKey @default = ", code);
            StringAssert.Contains("SemanticKey @event = ", code);
        }

        [Test]
        public void GeneratedCode_KeyNamedLikeTheClass_GetsSuffix()
        {
            // A member can't have the name of its enclosing class
            _domain.AddKey("Stats");

            string code = _domain.GenerateCodeText("Game.Constants");

            StringAssert.Contains("SemanticKey Stats_1 = ", code);
        }

        [Test]
        public void GeneratedCode_ClashingNames_GetSuffixes()
        {
            _domain.AddKey("Max HP");
            _domain.AddKey("Max.HP");

            string code = _domain.GenerateCodeText("Game.Constants");

            StringAssert.Contains("SemanticKey Max_HP = ", code);
            StringAssert.Contains("SemanticKey Max_HP_1 = ", code);
        }

        [TestCase("Strength", "Strength")]
        [TestCase("UI.Labels.Title", "UI_Labels_Title")]
        [TestCase("Critical Hit %", "Critical_Hit_")]
        [TestCase("2H Sword", "_2H_Sword")]
        [TestCase("%", "Key")]
        [TestCase("", "Key")]
        public void SanitizeVariableName_ReturnsValidIdentifier(string keyName, string expected)
        {
            Assert.AreEqual(expected, KeyDomain.SanitizeVariableName(keyName));
        }

        [TestCase("Player Stats", "PlayerStats")]
        [TestCase("UI.Labels", "UILabels")]
        [TestCase("3D Assets", "_3DAssets")]
        [TestCase("!!!", "")]
        public void SanitizeClassName_ReturnsValidIdentifierOrEmpty(string domainName, string expected)
        {
            Assert.AreEqual(expected, KeyDomain.SanitizeClassName(domainName));
        }

        [Test]
        public void ToStringLiteral_EscapesControlCharacters()
        {
            Assert.AreEqual("\"a\\nb\\tc\\u0001\"", KeyDomain.ToStringLiteral("a\nb\tc\u0001"));
            Assert.AreEqual("\"\"", KeyDomain.ToStringLiteral(null));
        }
    }
}