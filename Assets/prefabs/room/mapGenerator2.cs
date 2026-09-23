using System;
using System.Collections;
using System.Collections.Generic;
using prefabs.room.scripts;
using Unity.Mathematics;
using UnityEngine;

// Note: CLAUDE.md recommends using Game.<System> for namespaces (e.g., Game.Rooms). 
// Kept as prefabs.room to prevent breaking existing Unity Editor script references.
namespace prefabs.room
{
    /// <summary>
    /// Manages the deterministic procedural generation, pooling, and streaming
    /// of a linear sequence of rooms ending in a boss/final room.
    /// </summary>
    public class LinearDungeonManager : MonoBehaviour
    {
        public static LinearDungeonManager Instance;

        #region Serialized Fields

        [Header("Generation Settings")]
        public Transform originTransform; // Just an empty GameObject where the dungeon begins
        public int randomRoomCount = 10; 
    
        [Header("Prefabs")]
        public RoomData startRoomPrefab;  // Will be spawned at Index 0
        public RoomData[] randomRoomPrefabs;
        public RoomData finalRoomPrefab;  // Will be spawned at the end

        [Header("Streaming Settings")]
        public int lookAhead = 3;
        public int lookBehind = 3;
        

        [Space]
        [Header("Debug & Visualization")]
        [Tooltip("Toggle visual gizmos and rich console debugging. Disable for production performance.")]
        public bool enableDebug = true;

        #endregion

        #region Internal State

        private readonly List<RoomData> _allPrefabs = new List<RoomData>();
        private int _finalRoomPrefabID; 

        private readonly List<VirtualRoom> _virtualDungeon = new List<VirtualRoom>();
        private readonly Dictionary<int, Queue<GameObject>> _roomPools = new Dictionary<int, Queue<GameObject>>();
        private int _currentRoomIndex = -1;

        // Debug state
        private const string LogPrefix = "<color=cyan><b>[LinearDungeonManager]</b></color>";
        private readonly List<Vector3> _debugErrorLocations = new List<Vector3>();
        private readonly List<Ray> _debugSocketRays = new List<Ray>();

        #endregion

        /// <summary>
        /// Initializes the singleton instance and maps prefabs to standardized IDs.
        /// </summary>
        private void Awake()
        {
            Instance = this;
            
            ValidateDependencies();

            // Map all prefabs into a single list so prefabIDs stay perfectly synchronized
            _allPrefabs.Add(startRoomPrefab);           
            _allPrefabs.AddRange(randomRoomPrefabs);
            _allPrefabs.Add(finalRoomPrefab);
            _finalRoomPrefabID = _allPrefabs.Count - 1; // The boss room is always the last ID
            
            if (enableDebug)
            {
                Debug.Log($"{LogPrefix} Awake complete. Registered <color=yellow>{_allPrefabs.Count}</color> total room prefabs.");
            }
        }

        /// <summary>
        /// Validates editor assignments and begins the dungeon generation process.
        /// </summary>
        private void Start()
        {
            if (startRoomPrefab == null)
            {
                Debug.LogError($"{LogPrefix} <color=red><b>Starting Socket is not assigned!</b></color> Aborting generation.");
                return;
            }

            // Kick off the generation when the game starts
            StartCoroutine(GenerateAndPrewarmCoroutine());
        }

        /// <summary>
        /// Validates required references to fail fast if the Inspector is misconfigured.
        /// </summary>
        private void ValidateDependencies()
        {
            Debug.Assert(randomRoomPrefabs != null && randomRoomPrefabs.Length > 0, 
                $"{LogPrefix} <color=red><b>randomRoomPrefabs</b></color> is unassigned or empty!");
            Debug.Assert(finalRoomPrefab != null, 
                $"{LogPrefix} <color=red><b>finalRoomPrefab</b></color> is unassigned!");
        }

        /// <summary>
        /// Coroutine that calculates the deterministic layout of the dungeon virtually, 
        /// followed by instantiating and pooling the necessary room instances.
        /// </summary>
        /// <returns>IEnumerator for Coroutine yielding.</returns>
        public IEnumerator GenerateAndPrewarmCoroutine()
    {
        _virtualDungeon.Clear();
        int[] actualRoomCounts = new int[_allPrefabs.Count];

        int totalRooms = randomRoomCount + 2; // +1 for Start, +1 for Final
        Vector3 currentSocketPos = originTransform.position;
        Quaternion currentSocketRot = originTransform.rotation;

        for (int i = 0; i < totalRooms; i++)
        {
            // 1. Pick the correct Prefab ID based on the loop index
            int prefabID;
            if (i == 0) prefabID = 0; // The Start Room
            else if (i == totalRooms - 1) prefabID = _allPrefabs.Count - 1; // The Final Room
            else prefabID = UnityEngine.Random.Range(1, _allPrefabs.Count - 1); // A Random Room

            RoomData prefab = _allPrefabs[prefabID];
            Vector3 roomPos;
            Quaternion roomRot;

            // 2. Position the room
            if (i == 0)
            {
                // The Start Room doesn't snap to anything, it sits exactly at the origin
                roomPos = originTransform.position;
                roomRot = originTransform.rotation;
            }
            else
            {
                // Normal Rooms snap to the previous socket
                var entrySocket = prefab.sockets[0];
                Quaternion targetRotFlipped = currentSocketRot * Quaternion.Euler(0, 180f, 0);
                roomRot = targetRotFlipped * Quaternion.Inverse(entrySocket.localRotation);
                Vector3 rotatedEntryOffset = roomRot * entrySocket.localPosition;
                roomPos = currentSocketPos - rotatedEntryOffset;
            }

            // 3. Save to virtual dungeon
            PlacedRoom newRoom = new PlacedRoom
            {
                prefabID = prefabID,
                worldPosition = roomPos,
                worldRotation = roomRot,
                worldCenter = roomPos + (roomRot * prefab.localCenter),
                worldExtents = prefab.localExtents 
            };
            
            _virtualDungeon.Add(new VirtualRoom { data = newRoom, isLoaded = false });
            actualRoomCounts[prefabID]++;

            // Setup Next Socket
            if (i < totalRooms - 1)
            {
                // If the room only has 1 socket (like a Starter Room), we must use index 0.
                // Otherwise, randomly pick any socket EXCEPT index 0 (which is the entrance we just came through).
                int exitIndex = (prefab.sockets.Count == 1) ? 0 : UnityEngine.Random.Range(1, prefab.sockets.Count);
                
                var exitSocket = prefab.sockets[exitIndex];
                
                currentSocketPos = roomPos + (roomRot * exitSocket.localPosition);
                currentSocketRot = roomRot * exitSocket.localRotation;
            }
        }

        // PREWARM POOLS
        _roomPools.Clear();
        for (int i = 0; i < _allPrefabs.Count; i++)
        {
            _roomPools[i] = new Queue<GameObject>();
            int targetPoolSize = Mathf.Min(_allPrefabs[i].prewarmCount, actualRoomCounts[i]);

            for (int p = 0; p < targetPoolSize; p++)
            {
                GameObject roomInst = Instantiate(_allPrefabs[i].gameObject);
                roomInst.SetActive(false);
                _roomPools[i].Enqueue(roomInst);
                if (p % 3 == 0) yield return null; 
            }
        }

        // LOAD INITIAL WINDOW
        SetCurrentRoom(0);
    }

        /// <summary>
        /// Updates the current room index and triggers a shift in the streamed room window.
        /// </summary>
        /// <param name="index">The new room index the player has entered.</param>
        private void SetCurrentRoom(int index)
        {
            if (_currentRoomIndex != index)
            {
                if (enableDebug) Debug.Log($"{LogPrefix} Player entered room <color=yellow><b>{index}</b></color>. Shifting window...");
                _currentRoomIndex = index;
                ShiftRoomWindow();
            }
        }

        /// <summary>
        /// Iterates through the virtual dungeon to load rooms within the active window 
        /// (lookAhead + lookBehind) and returns out-of-bounds rooms to the object pool.
        /// </summary>
        private void ShiftRoomWindow()
        {
            int windowStart = math.max(0, _currentRoomIndex - lookBehind);
            int windowEnd = math.min(_virtualDungeon.Count - 1, _currentRoomIndex + lookAhead);

            for (int i = 0; i < _virtualDungeon.Count; i++)
            {
                bool isInsideWindow = (i >= windowStart && i <= windowEnd);
                var vRoom = _virtualDungeon[i];

                if (isInsideWindow && !vRoom.isLoaded)
                {
                    vRoom.instance = GetRoomFromPool(vRoom.data);
                
                    if (vRoom.instance.TryGetComponent(out RoomTrigger trigger))
                    {
                        trigger.roomIndex = i;
                        trigger.OnPlayerEnteredRoom -= SetCurrentRoom; 
                        trigger.OnPlayerEnteredRoom += SetCurrentRoom;
                    }

                    vRoom.isLoaded = true;
                }
                else if (!isInsideWindow && vRoom.isLoaded)
                {
                    ReturnRoomToPool(vRoom.data.prefabID, vRoom.instance);
                    vRoom.instance = null;
                    vRoom.isLoaded = false;
                }
            }
        }

        /// <summary>
        /// Retrieves a room instance from the pool or instantiates a new one if the pool is empty.
        /// </summary>
        /// <param name="roomData">The virtual layout data dictating position and rotation.</param>
        /// <returns>An active GameObject representing the room.</returns>
        private GameObject GetRoomFromPool(PlacedRoom roomData)
        {
            Queue<GameObject> pool = _roomPools[roomData.prefabID];
            GameObject roomInstance;

            if (pool.Count > 0)
            {
                roomInstance = pool.Dequeue();
                roomInstance.transform.position = roomData.worldPosition;
                roomInstance.transform.rotation = roomData.worldRotation;
                roomInstance.SetActive(true);
            }
            else
            {
                if (enableDebug) Debug.LogWarning($"{LogPrefix} Pool exhaustion for prefabID <color=yellow>{roomData.prefabID}</color>. Instantiating dynamically.");
                roomInstance = Instantiate(_allPrefabs[roomData.prefabID].gameObject, roomData.worldPosition, roomData.worldRotation);
            }

            return roomInstance;
        }

        /// <summary>
        /// Cleans up event subscriptions and returns an active room back into the inactive pool.
        /// </summary>
        /// <param name="prefabID">The internal ID of the prefab type.</param>
        /// <param name="roomInstance">The physical GameObject being returned.</param>
        private void ReturnRoomToPool(int prefabID, GameObject roomInstance)
        {
            if (roomInstance.TryGetComponent(out RoomTrigger trigger))
            {
                trigger.OnPlayerEnteredRoom -= SetCurrentRoom;
            }

            roomInstance.SetActive(false);
            _roomPools[prefabID].Enqueue(roomInstance);
        }

#if UNITY_EDITOR
        /// <summary>
        /// Visually represents the spatial logic of the virtual dungeon in the Unity Editor Scene View.
        /// Only draws when the GameObject is selected to reduce visual clutter.
        /// </summary>
        private void OnDrawGizmosSelected()
        {
            if (!enableDebug) return;

            // 1. Draw Virtual Room Bounds
            if (_virtualDungeon != null)
            {
                for (int i = 0; i < _virtualDungeon.Count; i++)
                {
                    var vRoom = _virtualDungeon[i];
                    Gizmos.color = vRoom.isLoaded ? Color.green : Color.gray;
                    
                    // Draw bounding wire cube
                    Gizmos.matrix = Matrix4x4.TRS(vRoom.data.worldPosition, vRoom.data.worldRotation, Vector3.one);
                    
                    // Assuming localExtents represents half-extents (size / 2)
                    Gizmos.DrawWireCube(vRoom.data.worldCenter - vRoom.data.worldPosition, vRoom.data.worldExtents * 2f);
                }
            }
            Gizmos.matrix = Matrix4x4.identity;

            // 2. Draw Sockets Connections
            Gizmos.color = Color.cyan;
            foreach (var ray in _debugSocketRays)
            {
                Gizmos.DrawSphere(ray.origin, 0.5f);
                Gizmos.DrawRay(ray.origin, ray.direction * 2f);
            }

            // 3. Draw Edge Case Error Locations
            if (_debugErrorLocations != null && _debugErrorLocations.Count > 0)
            {
                Gizmos.color = Color.red;
                foreach (var pos in _debugErrorLocations)
                {
                    Gizmos.DrawWireSphere(pos, 2f);
                    Gizmos.DrawIcon(pos, "console.erroricon", true);
                }
            }
        }
#endif
    }
}