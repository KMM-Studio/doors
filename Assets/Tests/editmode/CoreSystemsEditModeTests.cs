using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using prefabs.item;
using prefabs.player;
using prefabs.room.scripts;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace Tests.EditMode
{
    [TestFixture]
    public class InventoryAndItemPoolEditModeTests
    {
        private readonly List<Object> _createdObjects = new List<Object>();
        private GameObject _playerObj;
        private PlayerInventory _inventory;
        private GameObject _poolObj;
        private ItemPool _itemPool;

        [SetUp]
        public void SetUp()
        {
            _poolObj = new GameObject("TestItemPool");
            _itemPool = _poolObj.AddComponent<ItemPool>();
            InvokePrivateMethod(_itemPool, "Awake");
            _createdObjects.Add(_poolObj);

            _playerObj = new GameObject("TestPlayer");
            _inventory = _playerObj.AddComponent<PlayerInventory>();
            _createdObjects.Add(_playerObj);
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var obj in _createdObjects)
            {
                if (obj != null) Object.DestroyImmediate(obj);
            }
            _createdObjects.Clear();
        }

        [Test]
        public void TryToAddItem_EmptySlot_AddsAndEquipsItem()
        {
            var primaryData = CreateItemData("Rifle", ItemType.Primary);
            PlayerInventory changedPayload = null;
            void Handler(PlayerInventory inv) => changedPayload = inv;
            PlayerInventory.OnInventoryChanged += Handler;

            try
            {
                bool added = _inventory.TryToAddItem(primaryData);

                Assert.IsTrue(added);
                Assert.AreEqual(primaryData, _inventory.GetItem(ItemType.Primary));
                Assert.AreEqual(ItemType.Primary, _inventory.CurrentItemType);
                Assert.AreEqual(primaryData, _inventory.CurrentItem);
                Assert.AreEqual(_inventory, changedPayload);
            }
            finally
            {
                PlayerInventory.OnInventoryChanged -= Handler;
            }
        }

        [Test]
        public void TryToAddItem_OccupiedSlot_ReturnsFalseAndPreservesOriginal()
        {
            var firstPrimary = CreateItemData("RifleA", ItemType.Primary);
            var secondPrimary = CreateItemData("RifleB", ItemType.Primary);

            Assert.IsTrue(_inventory.TryToAddItem(firstPrimary));
            Assert.IsFalse(_inventory.TryToAddItem(secondPrimary));
            Assert.AreEqual(firstPrimary, _inventory.GetItem(ItemType.Primary));
        }

        [Test]
        public void CycleNextAndPrevious_SkipsEmptySlotsAndWrapsAround()
        {
            var primary = CreateItemData("Rifle", ItemType.Primary);
            var melee = CreateItemData("Knife", ItemType.Melee);

            _inventory.TryToAddItem(primary);
            _inventory.TryToAddItem(melee);

            Assert.AreEqual(ItemType.Primary, _inventory.CurrentItemType);

            // Secondary is empty, so CycleNext should jump directly to Melee
            _inventory.CycleNext();
            Assert.AreEqual(ItemType.Melee, _inventory.CurrentItemType);

            // Utils is empty, so CycleNext should wrap around to Primary
            _inventory.CycleNext();
            Assert.AreEqual(ItemType.Primary, _inventory.CurrentItemType);

            // CyclePrevious should wrap backward to Melee
            _inventory.CyclePrevious();
            Assert.AreEqual(ItemType.Melee, _inventory.CurrentItemType);
        }

        [Test]
        public void Item_PickupCooldown_BlocksPickupUntilExpired()
        {
            var itemData = CreateItemData("Pistol", ItemType.Secondary);
            var itemObj = new GameObject("PhysicalPistol");
            _createdObjects.Add(itemObj);
            var physicalItem = itemObj.AddComponent<Item>();
            physicalItem.itemData = itemData;

            physicalItem.ApplyPickupCooldown(2f);
            Assert.IsFalse(physicalItem.CanBePickedUp);
            Assert.IsFalse(physicalItem.TryToPickup(_inventory));
            Assert.IsNull(_inventory.GetItem(ItemType.Secondary));

            physicalItem.ApplyPickupCooldown(0f);
            Assert.IsTrue(physicalItem.CanBePickedUp);
            Assert.IsTrue(physicalItem.TryToPickup(_inventory));
            Assert.AreEqual(itemData, _inventory.GetItem(ItemType.Secondary));
            Assert.IsFalse(physicalItem.gameObject.activeSelf, "Picked up item should be deactivated in ItemPool.");
        }

        [Test]
        public void ItemPool_StoreAndSpawn_ReusesPooledInstanceWithCooldown()
        {
            var itemData = CreateItemData("Medkit", ItemType.Utils);
            var itemObj = new GameObject("PhysicalMedkit");
            _createdObjects.Add(itemObj);
            var physicalItem = itemObj.AddComponent<Item>();
            physicalItem.itemData = itemData;

            _itemPool.StoreInPool(physicalItem);
            Assert.IsFalse(physicalItem.gameObject.activeSelf);

            Vector3 dropPos = new Vector3(3f, 1f, -4f);
            Item spawned = _itemPool.SpawnFromPool(itemData, dropPos);

            Assert.AreSame(physicalItem, spawned, "ItemPool should reuse the parked instance instead of instantiating a new one.");
            Assert.IsTrue(spawned.gameObject.activeSelf);
            Assert.AreEqual(dropPos, spawned.transform.position);
            Assert.IsFalse(spawned.CanBePickedUp, "Spawned item from pool should have pickup cooldown applied.");
        }

        private ItemData CreateItemData(string itemName, ItemType type)
        {
            var data = ScriptableObject.CreateInstance<ItemData>();
            data.name = itemName;
            data.itemName = itemName;
            data.itemType = type;
            _createdObjects.Add(data);
            return data;
        }

        private static void InvokePrivateMethod(object target, string methodName)
        {
            var method = target.GetType().GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Instance);
            method?.Invoke(target, null);
        }
    }

    [TestFixture]
    public class PlayerStatsEditModeTests
    {
        private GameObject _playerObj;
        private PlayerStats _stats;

        [SetUp]
        public void SetUp()
        {
            _playerObj = new GameObject("PlayerStatsObject");
            _stats = _playerObj.AddComponent<PlayerStats>();
        }

        [TearDown]
        public void TearDown()
        {
            if (_playerObj != null) Object.DestroyImmediate(_playerObj);
        }

        [Test]
        public void Awake_InitializesHealthToMaxHealthWhenZero()
        {
            _stats.maxHealth = 120;
            _stats.currentHealth = 0;

            var awake = typeof(PlayerStats).GetMethod("Awake", BindingFlags.NonPublic | BindingFlags.Instance);
            awake?.Invoke(_stats, null);

            Assert.AreEqual(120, _stats.currentHealth);
        }

        [Test]
        public void Update_BroadcastsNormalizedHealthRatio()
        {
            _stats.maxHealth = 100;
            _stats.currentHealth = 65;

            float receivedRatio = -1f;
            void Handler(float ratio) => receivedRatio = ratio;
            PlayerStats.OnHealthChanged += Handler;

            try
            {
                var update = typeof(PlayerStats).GetMethod("Update", BindingFlags.NonPublic | BindingFlags.Instance);
                update?.Invoke(_stats, null);

                Assert.AreEqual(0.65f, receivedRatio, 0.0001f);
            }
            finally
            {
                PlayerStats.OnHealthChanged -= Handler;
            }
        }
    }

    [TestFixture]
    public class GenerateDungeonJobEditModeTests
    {
        [Test]
        public void Execute_SameSeed_ProducesIdenticalDeterministicLayouts()
        {
            using var runA = RunDungeonJob(seed: 1337u, totalRooms: 10);
            using var runB = RunDungeonJob(seed: 1337u, totalRooms: 10);

            Assert.AreEqual(10, runA.Length, "Job should generate all 10 requested rooms.");
            Assert.AreEqual(runA.Length, runB.Length);

            // Start room is prefab 0, final safe room is prefab 2
            Assert.AreEqual(0, runA[0].prefabID);
            Assert.AreEqual(2, runA[runA.Length - 1].prefabID);

            for (int i = 0; i < runA.Length; i++)
            {
                Assert.AreEqual(runA[i].prefabID, runB[i].prefabID, $"Prefab mismatch at room {i}");
                Assert.AreEqual(runA[i].worldPosition, runB[i].worldPosition, $"Position mismatch at room {i}");
                Assert.AreEqual(runA[i].worldRotation.value, runB[i].worldRotation.value, $"Rotation mismatch at room {i}");
                Assert.AreEqual(runA[i].usedSocketsMask, runB[i].usedSocketsMask, $"Socket mask mismatch at room {i}");
            }
        }

        private static NativeList<PlacedRoom> RunDungeonJob(uint seed, int totalRooms)
        {
            // Define 3 prefabs: 0 = Start (1 exit), 1 = Corridor (entry + exit), 2 = Final SafeRoom (1 entry)
            var roomDefs = new NativeArray<JobRoomDef>(3, Allocator.TempJob);
            var sockets = new NativeArray<JobSocket>(4, Allocator.TempJob);
            var bounds = new NativeArray<JobBound>(3, Allocator.TempJob);
            var roomCounts = new NativeArray<int>(3, Allocator.TempJob);
            var placedRooms = new NativeList<PlacedRoom>(Allocator.TempJob);

            try
            {
                // Prefab 0: Start Room
                roomDefs[0] = new JobRoomDef
                {
                    prefabID = 0,
                    localCenter = float3.zero,
                    localExtents = new float3(2f, 2f, 2f),
                    socketStartIndex = 0,
                    socketCount = 1,
                    boundStartIndex = 0,
                    boundCount = 1
                };
                sockets[0] = new JobSocket
                {
                    localPosition = new float3(0f, 0f, 2.5f),
                    localRotation = quaternion.identity
                };
                bounds[0] = new JobBound { localCenter = float3.zero, localExtents = new float3(2f, 2f, 2f) };

                // Prefab 1: Standard Room (2 opposite sockets)
                roomDefs[1] = new JobRoomDef
                {
                    prefabID = 1,
                    localCenter = float3.zero,
                    localExtents = new float3(2f, 2f, 2f),
                    socketStartIndex = 1,
                    socketCount = 2,
                    boundStartIndex = 1,
                    boundCount = 1
                };
                sockets[1] = new JobSocket
                {
                    localPosition = new float3(0f, 0f, -2.5f),
                    localRotation = quaternion.Euler(0f, math.PI, 0f)
                };
                sockets[2] = new JobSocket
                {
                    localPosition = new float3(0f, 0f, 2.5f),
                    localRotation = quaternion.identity
                };
                bounds[1] = new JobBound { localCenter = float3.zero, localExtents = new float3(2f, 2f, 2f) };

                // Prefab 2: Final Safe Room (1 entry socket)
                roomDefs[2] = new JobRoomDef
                {
                    prefabID = 2,
                    localCenter = float3.zero,
                    localExtents = new float3(2f, 2f, 2f),
                    socketStartIndex = 3,
                    socketCount = 1,
                    boundStartIndex = 2,
                    boundCount = 1
                };
                sockets[3] = new JobSocket
                {
                    localPosition = new float3(0f, 0f, -2.5f),
                    localRotation = quaternion.Euler(0f, math.PI, 0f)
                };
                bounds[2] = new JobBound { localCenter = float3.zero, localExtents = new float3(2f, 2f, 2f) };

                var job = new GenerateDungeonJob
                {
                    roomDefs = roomDefs,
                    sockets = sockets,
                    bounds = bounds,
                    totalRooms = totalRooms,
                    randomRoomsCount = 1,
                    finalRoomID = 2,
                    alcoveSpawnChance = 0f,
                    originPos = float3.zero,
                    originRot = quaternion.identity,
                    random = new Unity.Mathematics.Random(seed),
                    placedRooms = placedRooms,
                    roomCounts = roomCounts
                };

                job.Run();
                return placedRooms;
            }
            finally
            {
                roomDefs.Dispose();
                sockets.Dispose();
                bounds.Dispose();
                roomCounts.Dispose();
            }
        }
    }
}
