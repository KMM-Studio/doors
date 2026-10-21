using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using prefabs.room;
using prefabs.room.scripts;
using UnityEngine;
using UnityEngine.TestTools;

namespace Tests
{
    /// <summary>
    /// Test suite for MapGenerator.
    /// Verifies deterministic generation, object placement, and fail-safe log assertions.
    /// </summary>
    [TestFixture]
    public class MapGeneratorTests
    {
        private GameObject _generatorObject;
        private MapGenerator _mapGenerator;
        private GameObject _initialSocketObject;

        // Mock Prefabs
        private GameObject _mockEntryRoom;
        private GameObject _mockExitRoom;
        private GameObject _mockStandardRoom;
        private GameObject _mockAlcoveRoom;
        private GameObject _mockOpenDoor;
        private GameObject _mockLockedDoor;

        private List<GameObject> _trackedObjects;

        [SetUp]
        public void SetUp()
        {
            _trackedObjects = new List<GameObject>();

            // Create Mock Prefabs placed far away from origin so template colliders do not block placement near (0,0,0)
            _mockEntryRoom = CreateMockRoom("MockEntryRoom", 2, new Vector3(10000f, 10000f, 10000f));
            _mockExitRoom = CreateMockRoom("MockExitRoom", 1, new Vector3(10020f, 10000f, 10000f));
            _mockStandardRoom = CreateMockRoom("MockStandardRoom", 3, new Vector3(10040f, 10000f, 10000f));
            _mockAlcoveRoom = CreateMockRoom("MockAlcoveRoom", 1, new Vector3(10060f, 10000f, 10000f));
            _mockOpenDoor = new GameObject("MockOpenDoor");
            _mockLockedDoor = new GameObject("MockLockedDoor");

            _trackedObjects.AddRange(new[]
            {
                _mockEntryRoom, _mockExitRoom, _mockStandardRoom, _mockAlcoveRoom, _mockOpenDoor, _mockLockedDoor
            });

            // Initialize Generator Object (Disabled to prevent Awake/Start from firing prematurely)
            _generatorObject = new GameObject("MapGenerator");
            _generatorObject.SetActive(false);
            _mapGenerator = _generatorObject.AddComponent<MapGenerator>();

            // Assign Public Fields
            _mapGenerator.entrySafeRoomPrefab = _mockEntryRoom;
            _mapGenerator.exitSafeRoomPrefab = _mockExitRoom;
            _mapGenerator.roomPrefabs = new List<GameObject> { _mockStandardRoom };
            _mapGenerator.alcoveRoomPrefabs = new List<GameObject> { _mockAlcoveRoom };
            _mapGenerator.doorOpenablePrefab = _mockOpenDoor;
            _mapGenerator.doorLockedPrefab = _mockLockedDoor;

            // Setup Initial Global Socket at Origin
            _initialSocketObject = new GameObject("InitialGlobalSocket");
            var initialSocket = _initialSocketObject.AddComponent<RoomSocket>();
            _trackedObjects.Add(_initialSocketObject);

            // Assign Private Fields via Reflection
            SetPrivateField(_mapGenerator, "maxRooms", 5);
            SetPrivateField(_mapGenerator, "generationSeed", 42);
            SetPrivateField(_mapGenerator, "enableDebug", false);
            SetPrivateField(_mapGenerator, "globalAvailableSockets", new List<RoomSocket> { initialSocket });
        }

        [TearDown]
        public void TearDown()
        {
            if (_generatorObject != null) Object.DestroyImmediate(_generatorObject);

            foreach (var obj in _trackedObjects)
            {
                if (obj != null) Object.DestroyImmediate(obj);
            }

            // Clean up any instantiated room/door clones generated during the test
            CleanupGeneratedClones();
        }

        [Test]
        public void Awake_MissingPrefabs_LogsAssertMessages()
        {
            // Arrange
            _mapGenerator.entrySafeRoomPrefab = null;
            _mapGenerator.exitSafeRoomPrefab = null;
            _mapGenerator.roomPrefabs = new List<GameObject>();
            SetPrivateField(_mapGenerator, "maxRooms", 0);
            SetPrivateField(_mapGenerator, "globalAvailableSockets", new List<RoomSocket>());

            // Expect Assertions from Awake()
            LogAssert.Expect(LogType.Assert, "<color=red><b>[MapGenerator]</b></color> Entry Safe Room Prefab is missing!");
            LogAssert.Expect(LogType.Assert, "<color=red><b>[MapGenerator]</b></color> Exit Safe Room Prefab is missing!");
            LogAssert.Expect(LogType.Assert, "<color=red><b>[MapGenerator]</b></color> Room Prefabs list is empty!");
            LogAssert.Expect(LogType.Assert, "<color=red><b>[MapGenerator]</b></color> maxRooms must be greater than 0!");

            // Act
            _generatorObject.SetActive(true); // Triggers Awake() immediately
            Object.DestroyImmediate(_generatorObject); // Destroy before Start() runs on next frame
        }

        [UnityTest]
        public IEnumerator Start_EmptyGlobalSockets_LogsErrorAndAborts()
        {
            // Arrange
            SetPrivateField(_mapGenerator, "globalAvailableSockets", new List<RoomSocket>());
            LogAssert.Expect(LogType.Error,
                "<color=red><b>[MapGenerator]</b></color> No initial roomSocket assigned in globalAvailableSockets. Aborting generation.");

            // Act
            _generatorObject.SetActive(true); // Triggers Awake(), schedules Start()
            yield return null; // Wait 1 frame so Start() runs

            // Assert
            var spawnedDoors = GameObject.Find("MockOpenDoor(Clone)");
            Assert.IsNull(spawnedDoors,
                "Dungeon generation should have aborted, but instantiated objects were found.");
        }

        [UnityTest]
        public IEnumerator GenerateDungeon_ValidSetup_ConsumesInitialSocket()
        {
            // Arrange & Act
            _generatorObject.SetActive(true); // Awake() + Start() -> GenerateDungeon()
            yield return null; // Allow Start() and end-of-frame Object.Destroy() to complete

            // Assert
            Assert.IsTrue(_initialSocketObject == null || !_initialSocketObject.activeInHierarchy,
                "The initial socket should be consumed and destroyed during generation.");
            Assert.IsNotNull(GameObject.Find("MockEntryRoom(Clone)"),
                "The entry safe room should have been instantiated.");
        }

        [TestCase(100)]
        [TestCase(999)]
        public void GenerateDungeon_Deterministic_IdenticalSeedsProduceSameBranchCounts(int seed)
        {
            // Arrange
            SetPrivateField(_mapGenerator, "generationSeed", seed);
            SetPrivateField(_mapGenerator, "enableDebug", false);

            // Act - First Generation (keep _generatorObject inactive so Start() doesn't run a second time)
            _mapGenerator.GenerateDungeon();
            int firstRunDoorCount = CountCloneObjects("MockOpenDoor(Clone)") + CountCloneObjects("MockLockedDoor(Clone)");
            int firstRunRoomCount = CountCloneObjects("MockStandardRoom(Clone)") + CountCloneObjects("MockAlcoveRoom(Clone)");

            // Clean up generated clones manually before the second run
            CleanupGeneratedClones();

            // Restore initial socket for second run
            var newInitialSocketObj = new GameObject("InitialGlobalSocket2");
            var newInitialSocket = newInitialSocketObj.AddComponent<RoomSocket>();
            _trackedObjects.Add(newInitialSocketObj);
            SetPrivateField(_mapGenerator, "globalAvailableSockets", new List<RoomSocket> { newInitialSocket });

            // Act - Second Generation (Same Seed)
            _mapGenerator.GenerateDungeon();
            int secondRunDoorCount = CountCloneObjects("MockOpenDoor(Clone)") + CountCloneObjects("MockLockedDoor(Clone)");
            int secondRunRoomCount = CountCloneObjects("MockStandardRoom(Clone)") + CountCloneObjects("MockAlcoveRoom(Clone)");

            // Assert
            Assert.Greater(firstRunDoorCount, 0, "Generation should have spawned at least one door.");
            Assert.AreEqual(firstRunDoorCount, secondRunDoorCount,
                "Deterministic generation failed. Identical seeds yielded different door counts.");
            Assert.AreEqual(firstRunRoomCount, secondRunRoomCount,
                "Deterministic generation failed. Identical seeds yielded different room counts.");
        }

        [UnityTest]
        public IEnumerator GenerateDungeon_OverlapCheck_FailsPlacementGracefully()
        {
            // Arrange
            // Create a giant collider on the standard room to force Physics.CheckBox to return true (overlap)
            var boxCollider = _mockStandardRoom.GetComponent<BoxCollider>();
            boxCollider.size = new Vector3(1000f, 1000f, 1000f);

            // Act
            _generatorObject.SetActive(true); // Start() runs GenerateDungeon()
            yield return null; // Wait for end-of-frame Destroy(newRoom) on rejected overlapping room

            // Assert
            var spawnedStandardRooms = GameObject.Find("MockStandardRoom(Clone)");
            Assert.IsNull(spawnedStandardRooms,
                "Room should not have been instantiated because it overlapped with existing geometry.");
        }

        // --- Helper Methods ---

        private GameObject CreateMockRoom(string name, int socketCount, Vector3 templatePosition)
        {
            GameObject room = new GameObject(name);
            room.transform.position = templatePosition;
            int roomLayer = LayerMask.NameToLayer("room");
            room.layer = roomLayer >= 0 ? roomLayer : 0;

            // Add a collider for bounds calculation and physics checks (4x4x4 box inside +-2.5f sockets)
            var col = room.AddComponent<BoxCollider>();
            col.size = new Vector3(4f, 4f, 4f);

            // Outward-facing sockets on walls of the room so connected rooms do not overlap
            Vector3[] positions =
            {
                new Vector3(0f, 0f, -2.5f),
                new Vector3(0f, 0f, 2.5f),
                new Vector3(2.5f, 0f, 0f),
                new Vector3(-2.5f, 0f, 0f)
            };
            Quaternion[] rotations =
            {
                Quaternion.Euler(0f, 180f, 0f),
                Quaternion.identity,
                Quaternion.Euler(0f, 90f, 0f),
                Quaternion.Euler(0f, -90f, 0f)
            };

            for (int i = 0; i < socketCount && i < positions.Length; i++)
            {
                GameObject socketObj = new GameObject($"Socket_{i}");
                socketObj.transform.SetParent(room.transform, false);
                socketObj.transform.localPosition = positions[i];
                socketObj.transform.localRotation = rotations[i];
                socketObj.AddComponent<RoomSocket>();
            }

            return room;
        }

        private void CleanupGeneratedClones()
        {
            var allObjects = Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var obj in allObjects)
            {
                if (obj != null && obj.transform.parent == null && obj.name.EndsWith("(Clone)"))
                {
                    Object.DestroyImmediate(obj);
                }
            }
        }

        private int CountCloneObjects(string exactCloneName)
        {
            int count = 0;
            var allObjects = Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var obj in allObjects)
            {
                if (obj != null && obj.name == exactCloneName)
                {
                    count++;
                }
            }
            return count;
        }

        private void SetPrivateField(object target, string fieldName, object value)
        {
            FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
            field?.SetValue(target, value);
        }
    }
}
