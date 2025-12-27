using NUnit.Framework;
using SemanticKeys.Editor;

namespace SemanticKeys.Tests
{
    public class SemanticKeyItemTests
    {
        [Test]
        public void Constructor_SetsPropertiesCorrectly()
        {
            string name = "MyKey";
            string value = "MyKey";
            string guid = "guid_123";
            string domainGuid = "domain_456";

            var item = new SemanticKeyItem(name, value, guid, domainGuid);

            Assert.AreEqual(name, item.name); // Inherited from AdvancedDropdownItem
            Assert.AreEqual(value, item.Value);
            Assert.AreEqual(guid, item.Guid);
            Assert.AreEqual(domainGuid, item.DomainGuid);

            // Default state checks
            Assert.IsFalse(item.IsCreationCommand);
            Assert.AreEqual(SemanticKeyItem.CreationCommandType.None, item.CreationType);
        }

        [Test]
        public void CreationCommand_CanBeConfigured()
        {
            var item = new SemanticKeyItem("Create", "Create", "", "")
            {
                IsCreationCommand = true,
                CreationType = SemanticKeyItem.CreationCommandType.CreateDomain
            };

            Assert.IsTrue(item.IsCreationCommand);
            Assert.AreEqual(SemanticKeyItem.CreationCommandType.CreateDomain, item.CreationType);
        }
    }
}