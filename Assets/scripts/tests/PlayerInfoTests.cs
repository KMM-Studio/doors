/*using NUnit.Framework;
using UnityEngine;
using prefabs.player;

namespace prefabs.player.Tests
{
    public class PlayerInfoTests
    {
        private GameObject _playerGo;
        private PlayerInfo _playerInfo;

        [SetUp]
        public void SetUp()
        {
            _playerGo = new GameObject("PlayerInfoMock");
            _playerInfo = _playerGo.AddComponent<PlayerInfo>();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_playerGo);
        }

        [Test]
        public void Awake_InitializesHealthAndArraySize()
        {
            // Act
            _playerInfo.maxHealth = 150;
            _playerInfo.SendMessage("Awake");

            // Assert
            Assert.AreEqual(150, _playerInfo.currentHealth, "Current health should default to max health on Awake if it is 0.");
            Assert.IsNotNull(_playerInfo.inventory, "Inventory array should be initialized.");
            
            int expectedCount = (int)ItemType.COUNT_DO_NOT_USE_IN_INSPECTOR_OR_YOU_WILL_BE_PART_OF_NEXT_LAY_OFF_WAVE;
            Assert.AreEqual(expectedCount, _playerInfo.inventory.Length, "Inventory array size should match the ItemType enum count.");
            Assert.IsNull(_playerInfo.inventory[(int)ItemType.Primary], "Primary slot should be empty initially.");
        }

        [Test]
        public void HasAnyItem_ReturnsCorrectBoolean()
        {
            _playerInfo.SendMessage("Awake");

            // Assert Empty
            Assert.IsFalse(_playerInfo.HasAnyItem(), "Should return false when all slots are null.");

            // Act - Add Item
            GameObject mockItem = new GameObject("MockItem");
            Item item = mockItem.AddComponent<Item>();
            item.itemType = ItemType.Primary;
            _playerInfo.inventory[(int)ItemType.Primary] = item;

            // Assert Populated
            Assert.IsTrue(_playerInfo.HasAnyItem(), "Should return true when at least one slot is populated.");

            Object.DestroyImmediate(mockItem);
        }

        [Test]
        public void ChangeItemTypeNext_LoopsCorrectlyThroughSlots()
        {
            _playerInfo.SendMessage("Awake");
            
            // Add items to primary and secondary so HasAnyItem() and slot switching work
            GameObject item1 = new GameObject("Item1");
            item1.AddComponent<Item>().itemType = ItemType.Primary;
            _playerInfo.inventory[(int)ItemType.Primary] = item1.AddComponent<Item>();

            GameObject item2 = new GameObject("Item2");
            item2.AddComponent<Item>().itemType = ItemType.Secondary;
            _playerInfo.inventory[(int)ItemType.Secondary] = item2.AddComponent<Item>();

            _playerInfo.currentItemType = ItemType.Primary;

            // Act
            _playerInfo.ChangeItemTypeNext();

            // Assert
            Assert.AreEqual(ItemType.Secondary, _playerInfo.currentItemType, "Should transition to the next occupied slot.");

            Object.DestroyImmediate(item1);
            Object.DestroyImmediate(item2);
        }

        [Test]
        public void ChangeItemTypePrevious_LoopsBackwardsCorrectly()
        {
            _playerInfo.SendMessage("Awake");
            
            int lastIndex = (int)ItemType.COUNT_DO_NOT_USE_IN_INSPECTOR_OR_YOU_WILL_BE_PART_OF_NEXT_LAY_OFF_WAVE - 1;
            ItemType lastEnum = (ItemType)lastIndex;

            GameObject itemPrimary = new GameObject("Primary");
            itemPrimary.AddComponent<Item>().itemType = ItemType.Primary;
            _playerInfo.inventory[(int)ItemType.Primary] = itemPrimary.AddComponent<Item>();

            GameObject itemLast = new GameObject("Last");
            itemLast.AddComponent<Item>().itemType = lastEnum;
            _playerInfo.inventory[lastIndex] = itemLast.AddComponent<Item>();

            // Start at Primary (0)
            _playerInfo.currentItemType = ItemType.Primary;

            // Act (Going previous from 0 should wrap around to the last occupied slot)
            _playerInfo.ChangeItemTypePrevious();

            // Assert
            Assert.AreEqual(lastEnum, _playerInfo.currentItemType, "Should wrap around backwards to the highest valid populated slot.");

            Object.DestroyImmediate(itemPrimary);
            Object.DestroyImmediate(itemLast);
        }

        [Test]
        public void SelectOccupiedSlot_FindsNextItem_AndSkipsEmpty()
        {
            _playerInfo.SendMessage("Awake");
            
            GameObject primaryGo = new GameObject("Primary");
            primaryGo.AddComponent<Item>().itemType = ItemType.Primary;
            
            GameObject utilsGo = new GameObject("Utils");
            utilsGo.AddComponent<Item>().itemType = ItemType.Utils;

            // Put items in Primary and Utils. Leave Secondary and Melee null.
            _playerInfo.inventory[(int)ItemType.Primary] = primaryGo.AddComponent<Item>();
            _playerInfo.inventory[(int)ItemType.Secondary] = null;
            _playerInfo.inventory[(int)ItemType.Melee] = null;
            _playerInfo.inventory[(int)ItemType.Utils] = utilsGo.AddComponent<Item>();

            _playerInfo.currentItemType = ItemType.Primary;

            // Act (Direction 1 = Forward)
            bool found = _playerInfo.SelectOccupiedSlot(1);

            // Assert
            Assert.IsTrue(found, "Should successfully find an occupied slot.");
            Assert.AreEqual(ItemType.Utils, _playerInfo.currentItemType, "Should skip Secondary and Melee, landing on Utils.");

            Object.DestroyImmediate(primaryGo);
            Object.DestroyImmediate(utilsGo);
        }

        [Test]
        public void RemoveItemFromInventory_ClearsSlot_AndAutoSwitches()
        {
            _playerInfo.SendMessage("Awake");
            
            GameObject primaryGo = new GameObject("Primary");
            primaryGo.AddComponent<Item>().itemType = ItemType.Primary;
            
            GameObject secondaryGo = new GameObject("Secondary");
            secondaryGo.AddComponent<Item>().itemType = ItemType.Secondary;

            _playerInfo.inventory[(int)ItemType.Primary] = primaryGo.AddComponent<Item>();
            _playerInfo.inventory[(int)ItemType.Secondary] = secondaryGo.AddComponent<Item>();

            _playerInfo.currentItemType = ItemType.Primary;
            _playerInfo.UpdateItemTags();

            // Act
            _playerInfo.RemoveItemFromInventory(ItemType.Primary, selectNextOccupied: true);

            // Assert
            Assert.IsNull(_playerInfo.inventory[(int)ItemType.Primary], "Primary slot should be cleared.");
            Assert.AreEqual(ItemType.Secondary, _playerInfo.currentItemType, "Should automatically switch to the next occupied slot (Secondary).");
            Assert.IsNotNull(_playerInfo.currentItem, "currentItemTags should be updated to the secondary item.");

            Object.DestroyImmediate(primaryGo);
            Object.DestroyImmediate(secondaryGo);
        }
    }
}*/