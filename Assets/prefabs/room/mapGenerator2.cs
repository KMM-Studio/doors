using System.Collections;
using System.Collections.Generic;
using prefabs.room.scripts;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using Random = System.Random;

namespace prefabs.room
{
    /// <summary>
    ///     Manages the procedural generation and dynamic streaming of a linear dungeon sequence.
    ///     Utilizes Burst-compiled jobs for layout generation and object pooling for efficient runtime streaming.
    /// </summary>
    public class LinearDungeonManager : MonoBehaviour
    {
        private const string LogPrefix = "<color=cyan><b>[JobDungeonManager]</b></color>";

        /// <summary>
        ///     Singleton instance for easy global access.
        /// </summary>
        public static LinearDungeonManager Instance;

        [Header("Generation Settings")]
        [Tooltip("The foundational transform dictating where the dungeon begins in world space.")]
        public Transform originTransform;

        [Tooltip("Total number of randomized rooms to sequence between the start and final rooms.")]
        public int desiredRoomCount = 10;

        [Space] [Header("Prefabs")] [Tooltip("The initial room spawned at the origin point.")]
        public RoomData startRoomPrefab;

        [Tooltip("Pool of standard rooms selected randomly during dungeon generation.")]
        public RoomData[] randomRoomPrefabs;

        [Tooltip("Optional dead-end rooms spawned at unused sockets.")]
        public RoomData[] alcovePrefabs;

        [Tooltip("The concluding room that marks the end of the dungeon sequence.")]
        public RoomData finalRoomPrefab;

        [Space] [Header("Doors")] [Tooltip("Door prefab instantiated between two valid, connected rooms.")]
        public GameObject openableDoorPrefab;

        [Tooltip("Door prefab or wall instantiated to cap unused, open room sockets.")]
        public GameObject lockedDoorPrefab;

        [Space]
        [Header("Alcove Settings")]
        [Range(0f, 1f)]
        [Tooltip("Probability of generating an alcove on an unused socket instead of a locked door.")]
        public float alcoveSpawnChance = 0.5f;

        [Space]
        [Header("Streaming Settings")]
        [Tooltip("Number of rooms to keep active ahead of the player's current room.")]
        public int lookAhead = 3;

        [Tooltip("Number of rooms to keep active behind the player's current room.")]
        public int lookBehind = 3;

        [Space] [Header("Debug & Visualization")] [Tooltip("Toggle visual gizmos and detailed console logging.")]
        public bool enableDebug = true;

        [Tooltip("Explicit seed for deterministic generation. Leave at 0 for random seed.")]
        public uint debugSeed;

        private readonly List<RoomData> _allPrefabs = new();
        private readonly Queue<GameObject> _lockedDoorPool = new();
        private readonly Queue<GameObject> _openableDoorPool = new();
        private readonly Dictionary<int, Queue<GameObject>> _roomPools = new();
        private readonly List<VirtualRoom> _virtualDungeon = new();

        private int _currentRoomIndex = -1;
        private int _mainRoomCount;

        // Used to tie alcoves to their main path rooms for correct streaming
        private int[] _roomMainIndices;

        private void Awake()
        {
            Instance = this;

            RunFailSafeAsserts();

            _allPrefabs.Add(startRoomPrefab);
            _allPrefabs.AddRange(randomRoomPrefabs);
            if (alcovePrefabs != null) _allPrefabs.AddRange(alcovePrefabs);
            _allPrefabs.Add(finalRoomPrefab);
        }

        private void Start()
        {
            StartCoroutine(MasterGenerationSequence());
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            if (!enableDebug || _virtualDungeon == null) return;

            for (var i = 0; i < _virtualDungeon.Count; i++)
            {
                var vRoom = _virtualDungeon[i];
                var mainIdx = _roomMainIndices != null && i < _roomMainIndices.Length ? _roomMainIndices[i] : i;

                // Color coding: Green = Loaded, Gray = Unloaded, Yellow = Current Room
                Gizmos.color = mainIdx == _currentRoomIndex ? Color.yellow : vRoom.isLoaded ? Color.green : Color.gray;

                // FIX: Inaccurate Box Drawing
                // Set the matrix origin directly to worldCenter to prevent applying worldRotation twice.
                Gizmos.matrix = Matrix4x4.TRS(vRoom.data.worldCenter, vRoom.data.worldRotation, Vector3.one);
                var size = (Vector3)(vRoom.data.worldExtents * 2f);

                // Draw bounding box for spatial awareness around the centered TRS matrix
                Gizmos.DrawWireCube(Vector3.zero, size);

                // Draw a faint solid cube for the current room bounds to highlight it
                if (mainIdx == _currentRoomIndex)
                {
                    Gizmos.color = new Color(1f, 0.92f, 0.016f, 0.1f);
                    Gizmos.DrawCube(Vector3.zero, size);
                }
            }

            // Edge Case Visualization: Draw streaming window bounds in Scene View
            if (_virtualDungeon.Count > 0 && _currentRoomIndex >= 0 && _mainRoomCount > 0)
            {
                Gizmos.matrix = Matrix4x4.identity;
                var windowStart = math.max(0, _currentRoomIndex - lookBehind);
                var windowEnd = math.min(_mainRoomCount - 1, _currentRoomIndex + lookAhead);

                if (windowStart < _virtualDungeon.Count && windowEnd < _virtualDungeon.Count)
                {
                    Vector3 startPos = _virtualDungeon[windowStart].data.worldPosition;
                    Vector3 endPos = _virtualDungeon[windowEnd].data.worldPosition;

                    Gizmos.color = Color.cyan;
                    Gizmos.DrawLine(startPos + Vector3.up * 5f, endPos + Vector3.up * 5f);
                    Gizmos.DrawWireSphere(startPos + Vector3.up * 5f, 1f);
                    Gizmos.DrawWireSphere(endPos + Vector3.up * 5f, 1f);
                }
            }

            Gizmos.matrix = Matrix4x4.identity;
        }
#endif

        /// <summary>
        ///     Validates critical component and prefab assignments before execution to prevent null references.
        /// </summary>
        private void RunFailSafeAsserts()
        {
            Debug.Assert(originTransform != null,
                $"{LogPrefix} <color=red><b>CRITICAL:</b></color> Origin Transform is unassigned!", this);
            Debug.Assert(startRoomPrefab != null,
                $"{LogPrefix} <color=red><b>CRITICAL:</b></color> Start Room Prefab is unassigned!", this);
            Debug.Assert(finalRoomPrefab != null,
                $"{LogPrefix} <color=red><b>CRITICAL:</b></color> Final Room Prefab is unassigned!", this);
            Debug.Assert(openableDoorPrefab != null,
                $"{LogPrefix} <color=red><b>CRITICAL:</b></color> Openable Door Prefab is unassigned!", this);
            Debug.Assert(lockedDoorPrefab != null,
                $"{LogPrefix} <color=red><b>CRITICAL:</b></color> Locked Door Prefab is unassigned!", this);
            Debug.Assert(randomRoomPrefabs != null && randomRoomPrefabs.Length > 0,
                $"{LogPrefix} <color=red><b>CRITICAL:</b></color> Random Room Prefabs array is empty!", this);
        }

        /// <summary>
        ///     Orchestrates the conversion of Unity data to unmanaged types, schedules the Burst job,
        ///     awaits completion, and cleans up memory.
        /// </summary>
        /// <returns>IEnumerator for Unity Coroutine sequence.</returns>
        private IEnumerator MasterGenerationSequence()
        {
            _virtualDungeon.Clear();

            // 1. Convert Unity data to Unmanaged Native Arrays
            var nativeRoomDefs = new NativeArray<JobRoomDef>(_allPrefabs.Count, Allocator.TempJob);

            var totalSocketCount = 0;
            foreach (var prefab in _allPrefabs) totalSocketCount += prefab.sockets.Count;
            var nativeSockets = new NativeArray<JobSocket>(totalSocketCount, Allocator.TempJob);

            var totalBoundCount = 0;
            foreach (var prefab in _allPrefabs) totalBoundCount += prefab.subBounds.Count;
            var nativeBounds = new NativeArray<JobBound>(totalBoundCount, Allocator.TempJob);

            var boundWriterIndex = 0;
            var socketWriterIndex = 0;

            for (var i = 0; i < _allPrefabs.Count; i++)
            {
                nativeRoomDefs[i] = new JobRoomDef
                {
                    prefabID = i,
                    localCenter = _allPrefabs[i].localCenter,
                    localExtents = _allPrefabs[i].localExtents,
                    socketStartIndex = socketWriterIndex,
                    socketCount = _allPrefabs[i].sockets.Count,
                    boundStartIndex = boundWriterIndex,
                    boundCount = _allPrefabs[i].subBounds.Count
                };

                foreach (var socket in _allPrefabs[i].sockets)
                    nativeSockets[socketWriterIndex++] = new JobSocket
                    {
                        localPosition = socket.localPosition,
                        localRotation = socket.localRotation
                    };

                foreach (var b in _allPrefabs[i].subBounds)
                    nativeBounds[boundWriterIndex++] = new JobBound
                    {
                        localCenter = b.center,
                        localExtents = b.extents
                    };
            }

            var nativePlacedRooms = new NativeList<PlacedRoom>(Allocator.TempJob);
            var nativeRoomCounts = new NativeArray<int>(_allPrefabs.Count, Allocator.TempJob);

            // Determine seed adhering to project rules (System.Random over UnityEngine.Random)
            var activeSeed = debugSeed != 0 ? debugSeed : (uint)new Random().Next(1, 100000);

            if (enableDebug)
                Debug.Log(
                    $"{LogPrefix} Dispatching Burst Job. Target Rooms: <color=yellow>{desiredRoomCount}</color> | Seed: <color=yellow>{activeSeed}</color>");

            // 2. Schedule the Burst Job
            var job = new GenerateDungeonJob
            {
                roomDefs = nativeRoomDefs,
                sockets = nativeSockets,
                bounds = nativeBounds,
                totalRooms = desiredRoomCount + 2,
                randomRoomsCount = randomRoomPrefabs.Length,
                finalRoomID = _allPrefabs.Count - 1,
                alcoveSpawnChance = alcovePrefabs != null && alcovePrefabs.Length > 0 ? alcoveSpawnChance : 0f,
                originPos = originTransform.position,
                originRot = originTransform.rotation,
                random = new Unity.Mathematics.Random(activeSeed),
                placedRooms = nativePlacedRooms,
                roomCounts = nativeRoomCounts
            };

            var handle = job.Schedule();

            // 3. Wait without freezing the Main Thread
            yield return new WaitUntil(() => handle.IsCompleted);
            handle.Complete();

            // 4. Extract data back to Managed code
            var actualRoomCounts = new int[_allPrefabs.Count];
            nativeRoomCounts.CopyTo(actualRoomCounts);

            foreach (var placedRoom in nativePlacedRooms)
                _virtualDungeon.Add(new VirtualRoom { data = placedRoom, isLoaded = false });

            // 5. Cleanup Unmanaged Memory
            nativeRoomDefs.Dispose();
            nativeSockets.Dispose();
            nativeBounds.Dispose();
            nativePlacedRooms.Dispose();
            nativeRoomCounts.Dispose();

            // --- FIX: ALCOVE STREAMING ---
            // Map alcoves to their respective main-path rooms based on distance,
            // avoiding shifting the stream window entirely out of bounds.
            MapAlcovesToMainRooms();
            // -----------------------------

            if (enableDebug)
                Debug.Log(
                    $"{LogPrefix} Burst Generation complete. Virtual rooms created: <color=green>{_virtualDungeon.Count}</color>. Prewarming pools...");

            // 6. Prewarm the standard Unity GameObjects
            yield return StartCoroutine(PrewarmPoolsCoroutine(actualRoomCounts));

            SetCurrentRoom(0);
        }

        /// <summary>
        ///     Analyzes the virtual dungeon array and ensures alcoves are bound to the array index of
        ///     their host main room so they load and unload together.
        /// </summary>
        private void MapAlcovesToMainRooms()
        {
            _roomMainIndices = new int[_virtualDungeon.Count];
            _mainRoomCount = 0;
            var mainRooms = new HashSet<int>();

            // Phase 1: Identify what is an alcove and what is the main path
            for (var i = 0; i < _virtualDungeon.Count; i++)
            {
                var isAlcove = false;
                if (alcovePrefabs != null)
                    foreach (var alcovePrefab in alcovePrefabs)
                        if (_allPrefabs[_virtualDungeon[i].data.prefabID].name == alcovePrefab.name)
                        {
                            isAlcove = true;
                            break;
                        }

                if (!isAlcove)
                {
                    mainRooms.Add(i);
                    _mainRoomCount = math.max(_mainRoomCount, i + 1);
                }
            }

            // Phase 2: Create the lookup array
            for (var i = 0; i < _virtualDungeon.Count; i++)
                if (mainRooms.Contains(i))
                {
                    _roomMainIndices[i] = i; // Main rooms map to themselves
                }
                else
                {
                    // For alcoves, locate the nearest main room to act as the streaming anchor
                    var closestMain = 0;
                    var minDistSq = float.MaxValue;

                    foreach (var mainIdx in mainRooms)
                    {
                        var distSq = math.distancesq(_virtualDungeon[i].data.worldPosition,
                            _virtualDungeon[mainIdx].data.worldPosition);
                        if (distSq < minDistSq)
                        {
                            minDistSq = distSq;
                            closestMain = mainIdx;
                        }
                    }

                    _roomMainIndices[i] = closestMain;
                }
        }

        /// <summary>
        ///     Gradually instantiates room prefabs to populate object pools, spreading the workload across multiple frames.
        /// </summary>
        /// <param name="actualRoomCounts">Array mapping prefab IDs to the amount of times they were generated.</param>
        private IEnumerator PrewarmPoolsCoroutine(int[] actualRoomCounts)
        {
            _roomPools.Clear();
            for (var i = 0; i < _allPrefabs.Count; i++)
            {
                _roomPools[i] = new Queue<GameObject>();
                // Assuming RoomData has a prewarmCount, falling back to 5 if not found, 
                // but keeping your original math.min logic intention.
                var targetPoolSize =
                    math.min(10,
                        actualRoomCounts[i]); // Defaulted 10, adjust to your RoomData.prewarmCount if available

                for (var p = 0; p < targetPoolSize; p++)
                {
                    var roomInst = Instantiate(_allPrefabs[i].gameObject);
                    roomInst.SetActive(false);
                    _roomPools[i].Enqueue(roomInst);
                    if (p % 3 == 0) yield return null;
                }
            }
        }

        /// <summary>
        ///     Updates the active room index based on player triggers and initiates streaming adjustments.
        /// </summary>
        /// <param name="index">The layout index of the room the player just entered.</param>
        private void SetCurrentRoom(int index)
        {
            // Resolve the entry index to its main sequence parent (handles alcoves)
            var mainIndex = _roomMainIndices != null && index < _roomMainIndices.Length
                ? _roomMainIndices[index]
                : index;

            if (_currentRoomIndex != mainIndex)
            {
                if (enableDebug)
                    Debug.Log(
                        $"{LogPrefix} Player transitioned to mapped Sequence Index: <color=orange>{mainIndex}</color> (Triggered by Index: {index})");

                _currentRoomIndex = mainIndex;
                ShiftRoomWindow();
            }
        }

        /// <summary>
        ///     Handles the loading and unloading of rooms based on the lookAhead and lookBehind streaming boundaries.
        /// </summary>
        private void ShiftRoomWindow()
        {
            var windowStart = math.max(0, _currentRoomIndex - lookBehind);
            var windowEnd =
                _currentRoomIndex + lookAhead; // Cap removed here, bounded gracefully by the loop logic below.

#if UNITY_EDITOR
            if (enableDebug)
                Debug.Log(
                    $"{LogPrefix} Stream Window Shift -> Start: <color=white>{windowStart}</color> | End: <color=white>{windowEnd}</color>");
#endif

            for (var i = 0; i < _virtualDungeon.Count; i++)
            {
                // Utilize the mapping array so alcoves share their parent's window state
                var mainIdx = _roomMainIndices != null && i < _roomMainIndices.Length ? _roomMainIndices[i] : i;
                var isInsideWindow = mainIdx >= windowStart && mainIdx <= windowEnd;

                var vRoom = _virtualDungeon[i];

                if (isInsideWindow && !vRoom.isLoaded)
                {
                    vRoom.instance = GetRoomFromPool(vRoom.data);
                    if (vRoom.instance.TryGetComponent(out RoomTrigger trigger))
                    {
                        trigger.roomIndex = i; // Assign precise layout index (not main index) for entry mapping later
                        trigger.OnPlayerEnteredRoom -= SetCurrentRoom;
                        trigger.OnPlayerEnteredRoom += SetCurrentRoom;
                    }

                    vRoom.isLoaded = true;
                    SpawnDoorsForRoom(vRoom, i);
                }
                else if (!isInsideWindow && vRoom.isLoaded)
                {
                    DespawnDoorsForRoom(vRoom);
                    ReturnRoomToPool(vRoom.data.prefabID, vRoom.instance);

                    vRoom.instance = null;
                    vRoom.isLoaded = false;
                }
            }
        }

        /// <summary>
        ///     Retrieves a room from the corresponding object pool or instantiates a new one if the pool is empty.
        /// </summary>
        /// <param name="roomData">The struct containing position, rotation, and ID data for the room.</param>
        /// <returns>An active GameObject representing the room.</returns>
        private GameObject GetRoomFromPool(PlacedRoom roomData)
        {
            var pool = _roomPools[roomData.prefabID];
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
#if UNITY_EDITOR
                if (enableDebug)
                    Debug.LogWarning(
                        $"{LogPrefix} <color=yellow>Pool exhaustion</color> for PrefabID {roomData.prefabID}. Instantiating new room on the fly.");
#endif
                roomInstance = Instantiate(_allPrefabs[roomData.prefabID].gameObject,
                    roomData.worldPosition, roomData.worldRotation);
            }

            return roomInstance;
        }

        /// <summary>
        ///     Deactivates a room, unregisters its events, and returns it to the corresponding object pool.
        /// </summary>
        private void ReturnRoomToPool(int prefabID, GameObject roomInstance)
        {
            if (roomInstance.TryGetComponent(out RoomTrigger trigger))
                trigger.OnPlayerEnteredRoom -= SetCurrentRoom;

            roomInstance.SetActive(false);
            _roomPools[prefabID].Enqueue(roomInstance);
        }

        /// <summary>
        ///     Populates the doorways of a room based on socket utilization masks calculated by the Burst Job.
        /// </summary>
        private void SpawnDoorsForRoom(VirtualRoom vRoom, int roomIndex)
        {
            var prefabData = _allPrefabs[vRoom.data.prefabID];

            for (var i = 0; i < prefabData.sockets.Count; i++)
            {
                var socket = prefabData.sockets[i];

                var doorPos = (Vector3)(vRoom.data.worldPosition +
                                        math.mul(vRoom.data.worldRotation, socket.localPosition));
                var doorRot = (Quaternion)math.mul(vRoom.data.worldRotation, socket.localRotation);

                var isUsed = (vRoom.data.usedSocketsMask & (1u << i)) != 0;

                if (!isUsed)
                {
                    var lockedDoor = GetDoorFromPool(lockedDoorPrefab, _lockedDoorPool, doorPos, doorRot);
                    if (lockedDoor) vRoom.activeDoors.Add(lockedDoor);
                }
                else if (i == vRoom.data.entrySocketIndex && roomIndex != 0)
                {
                    var openableDoor = GetDoorFromPool(openableDoorPrefab, _openableDoorPool, doorPos, doorRot);
                    if (openableDoor) vRoom.activeDoors.Add(openableDoor);
                }
            }
        }

        private GameObject GetDoorFromPool(GameObject prefab, Queue<GameObject> pool, Vector3 pos, Quaternion rot)
        {
            if (!prefab) return null;

            GameObject door;
            if (pool.Count > 0)
            {
                door = pool.Dequeue();
                door.transform.position = pos;
                door.transform.rotation = rot;
                door.SetActive(true);
            }
            else
            {
                door = Instantiate(prefab, pos, rot);
            }

            return door;
        }

        /// <summary>
        ///     Clears all active doors tied to a specific room, resetting their states and pooling them.
        /// </summary>
        private void DespawnDoorsForRoom(VirtualRoom vRoom)
        {
            foreach (var door in vRoom.activeDoors)
            {
                if (!door) continue;

                if (door.TryGetComponent(out DoorScript doorScript)) doorScript.Reset();

                door.SetActive(false);

                if (door.name.Contains(openableDoorPrefab.name))
                    _openableDoorPool.Enqueue(door);
                else
                    _lockedDoorPool.Enqueue(door);
            }

            vRoom.activeDoors.Clear();
        }
    }
}