/*using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using prefabs.room;
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

            // Create Mock Prefabs
            _mockEntryRoom = CreateMockRoom("MockEntryRoom", 1);
            _mockExitRoom = CreateMockRoom("MockExitRoom", 0);
            _mockStandardRoom = CreateMockRoom("MockStandardRoom", 2); // 1 in, 1 out
            _mockAlcoveRoom = CreateMockRoom("MockAlcoveRoom", 0);
            _mockOpenDoor = new GameObject("MockOpenDoor");
            _mockLockedDoor = new GameObject("MockLockedDoor");
            
            _trackedObjects.AddRange(new[] { _mockEntryRoom, _mockExitRoom, _mockStandardRoom, _mockAlcoveRoom, _mockOpenDoor, _mockLockedDoor });

            // Initialize Generator Object (Disabled to prevent Awake from firing prematurely)
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

            // Setup Initial Global Socket
            _initialSocketObject = new GameObject("InitialGlobalSocket");
            var initialSocket = _initialSocketObject.AddComponent<roomSocket>();
            _trackedObjects.Add(_initialSocketObject);
            
            // Assign Private Fields via Reflection
            SetPrivateField(_mapGenerator, "maxRooms", 5);
            SetPrivateField(_mapGenerator, "generationSeed", 42);
            SetPrivateField(_mapGenerator, "enableDebug", false);
            SetPrivateField(_mapGenerator, "globalAvailableSockets", new List<roomSocket> { initialSocket });
        }

        [TearDown]
        public void TearDown()
        {
            // Clean up generator and predefined mocks
            if (_generatorObject != null) Object.DestroyImmediate(_generatorObject);
            
            foreach (var obj in _trackedObjects)
            {
                if (obj != null) Object.DestroyImmediate(obj);
            }

            // Clean up instantiated clones generated during the test
            var allSockets = Object.FindObjectsByType<roomSocket>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var socket in allSockets)
            {
                if (socket != null && socket.gameObject != null)
                {
                    Object.DestroyImmediate(socket.transform.root.gameObject);
                }
            }
        }

        [Test]
        public void Awake_MissingPrefabs_LogsAssertMessages()
        {
            // Arrange
            _mapGenerator.entrySafeRoomPrefab = null;
            _mapGenerator.exitSafeRoomPrefab = null;
            _mapGenerator.roomPrefabs = new List<GameObject>();
            SetPrivateField(_mapGenerator, "maxRooms", 0);

            // Expect Assertions
            LogAssert.Expect(LogType.Assert, "<color=red><b>[MapGenerator]</b></color> Entry Safe Room Prefab is missing!");
            LogAssert.Expect(LogType.Assert, "<color=red><b>[MapGenerator]</b></color> Exit Safe Room Prefab is missing!");
            LogAssert.Expect(LogType.Assert, "<color=red><b>[MapGenerator]</b></color> Room Prefabs list is empty!");
            LogAssert.Expect(LogType.Assert, "<color=red><b>[MapGenerator]</b></color> maxRooms must be greater than 0!");

            // Act
            _generatorObject.SetActive(true); // Triggers Awake()
            
            // Assert handled by LogAssert.Expect
        }

        [Test]
        public void Start_EmptyGlobalSockets_LogsErrorAndAborts()
        {
            // Arrange
            SetPrivateField(_mapGenerator, "globalAvailableSockets", new List<roomSocket>());
            LogAssert.Expect(LogType.Error, "<color=red><b>[MapGenerator]</b></color> No initial roomSocket assigned in globalAvailableSockets. Aborting generation.");

            // Act
            _generatorObject.SetActive(true); // Triggers Awake() and Start()

            // Assert
            // FindObjectsByType should only return the original prefabs, not instantiated clones, since generation aborted.
            var spawnedDoors = GameObject.Find("MockOpenDoor(Clone)");
            Assert.IsNull(spawnedDoors, "Dungeon generation should have aborted, but instantiated objects were found.");
        }

        [Test]
        public void GenerateDungeon_ValidSetup_ConsumesInitialSocket()
        {
            // Arrange
            _generatorObject.SetActive(true); 

            // Act
            _mapGenerator.GenerateDungeon();

            // Assert
            // The initial socket should be destroyed/consumed by the first room placement
            Assert.IsTrue(_initialSocketObject == null || !_initialSocketObject.activeInHierarchy, "The initial socket should be consumed and destroyed during generation.");
        }

        [TestCase(100)]
        [TestCase(999)]
        public void GenerateDungeon_Deterministic_IdenticalSeedsProduceSameBranchCounts(int seed)
        {
            // Arrange
            _generatorObject.SetActive(true);
            SetPrivateField(_mapGenerator, "generationSeed", seed);
            SetPrivateField(_mapGenerator, "enableDebug", true);

            // Act - First Generation
            _mapGenerator.GenerateDungeon();
            int firstRunSocketCount = Object.FindObjectsByType<roomSocket>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length;

            // Clean up generated clones manually for the second run
            var allSockets = Object.FindObjectsByType<roomSocket>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var socket in allSockets)
            {
                if (socket != null && socket.gameObject != null && !PrefabIsMock(socket.gameObject))
                {
                    Object.DestroyImmediate(socket.transform.root.gameObject);
                }
            }

            // Restore initial socket for second run
            var newInitialSocketObj = new GameObject("InitialGlobalSocket2");
            var newInitialSocket = newInitialSocketObj.AddComponent<roomSocket>();
            _trackedObjects.Add(newInitialSocketObj);
            SetPrivateField(_mapGenerator, "globalAvailableSockets", new List<roomSocket> { newInitialSocket });

            // Act - Second Generation (Same Seed)
            _mapGenerator.GenerateDungeon();
            int secondRunSocketCount = Object.FindObjectsByType<roomSocket>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length;

            // Assert
            Assert.AreEqual(firstRunSocketCount, secondRunSocketCount, "Deterministic generation failed. Identical seeds yielded different object counts.");
        }

        [Test]
        public void GenerateDungeon_OverlapCheck_FailsPlacementGracefully()
        {
            // Arrange
            _generatorObject.SetActive(true);
            
            // Create a giant collider on the standard room to force Physics.CheckBox to return true (overlap)
            var boxCollider = _mockStandardRoom.GetComponent<BoxCollider>();
            boxCollider.size = new Vector3(1000f, 1000f, 1000f);

            // Act
            _mapGenerator.GenerateDungeon();

            // Assert
            // Because the standard room will immediately overlap with the entry room, 
            // the main generation loop will break early.
            var spawnedStandardRooms = GameObject.Find("MockStandardRoom(Clone)");
            Assert.IsNull(spawnedStandardRooms, "Room should not have been instantiated because it overlapped with existing geometry.");
        }

        // --- Helper Methods ---
        
        private GameObject CreateMockRoom(string name, int socketCount)
        {
            GameObject room = new GameObject(name);
            room.layer = LayerMask.NameToLayer("room"); // Target script requires "room" layer for Physics checks
            
            // Add a collider for bounds calculation and physics checks
            var col = room.AddComponent<BoxCollider>();
            col.size = Vector3.one * 5f;

            for (int i = 0; i < socketCount; i++)
            {
                GameObject socketObj = new GameObject($"Socket_{i}");
                socketObj.transform.SetParent(room.transform);
                socketObj.transform.localPosition = new Vector3(0, 0, (i + 1) * 5f);
                socketObj.AddComponent<roomSocket>();
            }

            return room;
        }

        private void SetPrivateField(object target, string fieldName, object value)
        {
            FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
            field?.SetValue(target, value);
        }

        private bool PrefabIsMock(GameObject obj)
        {
            return _trackedObjects.Contains(obj);
        }
    }
}*/