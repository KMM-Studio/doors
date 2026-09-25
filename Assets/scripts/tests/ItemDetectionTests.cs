/*using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using prefabs.player;

namespace prefabs.player.Tests
{
    public class ItemDetectionTests
    {
        private GameObject _playerGo;
        private ItemDetection _itemDetection;
        private PlayerInfo _playerInfo;
        private GameObject _dropPointGo;

        [SetUp]
        public void SetUp()
        {
            // 1. Create a mock player object
            _playerGo = new GameObject("PlayerMock");
            _itemDetection = _playerGo.AddComponent<ItemDetection>();
            _playerInfo = _playerGo.AddComponent<PlayerInfo>();

            // 2. Create a mock drop point
            _dropPointGo = new GameObject("DropPointMock");
            _dropPointGo.transform.position = new Vector3(5f, 0f, 5f);

            // 3. Assign dependencies
            _itemDetection.playerInfo = _playerInfo;
            _itemDetection.dropPoint = _dropPointGo.transform;

            // Ensure the inventory dictionary is initialized for the test environment
            // (Assumes PlayerInfo.inventory is a dictionary. If it's a custom class, adjust accordingly)
            if (_playerInfo.inventory == null)
            {
                // Note: Replace 'int' or 'object' with your actual itemType Enum/Type if it complains
                // _playerInfo.inventory = new Dictionary<YourItemTypeEnum, ItemTags>();
            }
        }

        [TearDown]
        public void TearDown()
        {
            // Clean up the hierarchy after every test
            Object.DestroyImmediate(_playerGo);
            Object.DestroyImmediate(_dropPointGo);
        }

        [Test]
        public void PickUpItem_WhenItemIsNull_ReturnsFalse()
        {
            // Act
            bool result = _itemDetection.PickUpItem(null);

            // Assert
            Assert.IsFalse(result, "PickUpItem should return false when passed a null ItemTags.");
        }

        [Test]
        public void PickUpItem_WhenInventorySlotEmpty_StoresItemAndReturnsTrue()
        {
            // Arrange
            GameObject itemGo = new GameObject("TestItem");
            Item item = itemGo.AddComponent<Item>();
            
            // Act
            bool result = _itemDetection.PickUpItem(item);

            // Assert
            Assert.IsTrue(result, "PickUpItem should succeed when the slot is empty.");
            Assert.IsFalse(itemGo.activeSelf, "Item should be deactivated upon pickup.");
            Assert.AreEqual(_itemDetection.transform, itemGo.transform.parent, "Item should be parented to the player.");
            
            Object.DestroyImmediate(itemGo);
        }

        [Test]
        public void PickUpItem_WhenInventorySlotOccupied_ReturnsFalse()
        {
            // Arrange
            GameObject firstItemGo = new GameObject("FirstItem");
            Item firstItem = firstItemGo.AddComponent<Item>();
            
            GameObject secondItemGo = new GameObject("SecondItem");
            Item secondItem = secondItemGo.AddComponent<Item>();
            
            // Assume both have the SAME itemType. (You may need to explicitly set itemTag.itemType here depending on your enum)
            
            // Act
            _itemDetection.PickUpItem(firstItem); // First one should succeed
            bool secondResult = _itemDetection.PickUpItem(secondItem); // Second one should fail

            // Assert
            Assert.IsFalse(secondResult, "PickUpItem should fail if the slot is already occupied.");
            
            Object.DestroyImmediate(firstItemGo);
            Object.DestroyImmediate(secondItemGo);
        }

        [Test]
        public void DropItem_DetachesFromPlayer_AndMovesToDropPoint()
        {
            // Arrange
            GameObject itemGo = new GameObject("TestItem");
            Item item = itemGo.AddComponent<Item>();
            
            // Simulate item being held by player
            itemGo.transform.SetParent(_itemDetection.transform);
            itemGo.SetActive(false);

            // Act
            _itemDetection.DropItem(item);

            // Assert
            Assert.IsNull(itemGo.transform.parent, "Item should have no parent after being dropped.");
            Assert.AreEqual(_dropPointGo.transform.position, itemGo.transform.position, "Item should be moved to the drop point.");
            Assert.IsTrue(itemGo.activeSelf, "Item should be re-activated in the world.");
            Assert.AreEqual(_playerInfo.maxPickupCooldown, item.pickupBlockTimer, "Pickup cooldown should be applied.");

            Object.DestroyImmediate(itemGo);
        }

        [Test]
        public void SwapItemTags_EquipsNewItem_AndDropsOldItem()
        {
            // Arrange
            GameObject oldItemGo = new GameObject("OldItem");
            Item oldItem = oldItemGo.AddComponent<Item>();
            
            GameObject newItemGo = new GameObject("NewItem");
            Item newItem = newItemGo.AddComponent<Item>();

            // Pick up the first item normally
            _itemDetection.PickUpItem(oldItem);

            // Act
            _itemDetection.SwapItemTags(newItem);

            // Assert (New Item State)
            Assert.IsFalse(newItemGo.activeSelf, "New item should be hidden/stowed.");
            Assert.AreEqual(_itemDetection.transform, newItemGo.transform.parent, "New item should be parented to player.");

            // Assert (Old Item State)
            Assert.IsNull(oldItemGo.transform.parent, "Old item should be un-parented (dropped).");
            Assert.AreEqual(_dropPointGo.transform.position, oldItemGo.transform.position, "Old item should be at the drop point.");
            Assert.IsTrue(oldItemGo.activeSelf, "Old item should be active in the world.");

            Object.DestroyImmediate(oldItemGo);
            Object.DestroyImmediate(newItemGo);
        }
    }
}*/