
using System.Collections;
using System.Collections.Generic;
using prefabs.room.scripts;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace prefabs.room
{
    public class LinearDungeonManager : MonoBehaviour
    {
        public static LinearDungeonManager Instance;

        [Header("Generation Settings")]
        public Transform originTransform; 
        public int desiredRoomCount = 10; 
    
        [Header("Prefabs")]
        public RoomData startRoomPrefab;  
        public RoomData[] randomRoomPrefabs;
        public RoomData[] alcovePrefabs; 
        public RoomData finalRoomPrefab;  

        [Header("Alcove Settings")]
        [Range(0f, 1f)]
        public float alcoveSpawnChance = 0.5f;

        [Header("Streaming Settings")]
        public int lookAhead = 3;
        public int lookBehind = 3;
        
        [Space]
        [Header("Debug & Visualization")]
        public bool enableDebug = true;

        private readonly List<RoomData> _allPrefabs = new List<RoomData>();
        private readonly List<VirtualRoom> _virtualDungeon = new List<VirtualRoom>();
        private readonly Dictionary<int, Queue<GameObject>> _roomPools = new Dictionary<int, Queue<GameObject>>();
        private int _currentRoomIndex = -1;
        private const string LogPrefix = "<color=cyan><b>[JobDungeonManager]</b></color>";

        private void Awake()
        {
            Instance = this;
            _allPrefabs.Add(startRoomPrefab);           
            _allPrefabs.AddRange(randomRoomPrefabs);
            if (alcovePrefabs != null) _allPrefabs.AddRange(alcovePrefabs);
            _allPrefabs.Add(finalRoomPrefab);
        }

        private void Start()
        {
            StartCoroutine(MasterGenerationSequence());
        }

        private IEnumerator MasterGenerationSequence()
        {
            _virtualDungeon.Clear();

            // 1. Convert Unity data to Unmanaged Native Arrays
            NativeArray<JobRoomDef> nativeRoomDefs = new NativeArray<JobRoomDef>(_allPrefabs.Count, Allocator.TempJob);
            
            // Count total sockets to size the flat array
            int totalSocketCount = 0;
            foreach (var prefab in _allPrefabs) totalSocketCount += prefab.sockets.Count;
            NativeArray<JobSocket> nativeSockets = new NativeArray<JobSocket>(totalSocketCount, Allocator.TempJob);
            
            int socketWriterIndex = 0;
            for (int i = 0; i < _allPrefabs.Count; i++)
            {
                nativeRoomDefs[i] = new JobRoomDef
                {
                    prefabID = i,
                    localCenter = (float3)_allPrefabs[i].localCenter,
                    localExtents = (float3)_allPrefabs[i].localExtents,
                    socketStartIndex = socketWriterIndex,
                    socketCount = _allPrefabs[i].sockets.Count
                };

                foreach (var socket in _allPrefabs[i].sockets)
                {
                    nativeSockets[socketWriterIndex++] = new JobSocket
                    {
                        localPosition = (float3)socket.localPosition,
                        localRotation = (quaternion)socket.localRotation
                    };
                }
            }

            NativeList<PlacedRoom> nativePlacedRooms = new NativeList<PlacedRoom>(Allocator.TempJob);
            NativeArray<int> nativeRoomCounts = new NativeArray<int>(_allPrefabs.Count, Allocator.TempJob);

            // 2. Schedule the Burst Job
            var job = new GenerateDungeonJob
            {
                roomDefs = nativeRoomDefs,
                sockets = nativeSockets,
                totalRooms = desiredRoomCount + 2,
                randomRoomsCount = randomRoomPrefabs.Length,
                finalRoomID = _allPrefabs.Count - 1,
                alcoveSpawnChance = alcovePrefabs != null && alcovePrefabs.Length > 0 ? alcoveSpawnChance : 0f,
                originPos = (float3)originTransform.position,
                originRot = (quaternion)originTransform.rotation,
                
                // Initialize Random with a random seed!
                random = new Unity.Mathematics.Random((uint)UnityEngine.Random.Range(1, 100000)), 
                
                placedRooms = nativePlacedRooms,
                roomCounts = nativeRoomCounts
            };

            JobHandle handle = job.Schedule();

            // 3. Wait without freezing the Main Thread!
            yield return new WaitUntil(() => handle.IsCompleted);
            handle.Complete();

            // 4. Extract data back to Managed code
            int[] actualRoomCounts = new int[_allPrefabs.Count];
            nativeRoomCounts.CopyTo(actualRoomCounts);

            foreach (var placedRoom in nativePlacedRooms)
            {
                _virtualDungeon.Add(new VirtualRoom { data = placedRoom, isLoaded = false });
            }

            // 5. Cleanup Unmanaged Memory immediately to prevent memory leaks
            nativeRoomDefs.Dispose();
            nativeSockets.Dispose();
            nativePlacedRooms.Dispose();
            nativeRoomCounts.Dispose();

            // 6. Prewarm the standard Unity GameObjects over a few frames
            yield return StartCoroutine(PrewarmPoolsCoroutine(actualRoomCounts));

            SetCurrentRoom(0);
            if (enableDebug) Debug.Log($"{LogPrefix} Burst Generation complete!");
        }

        private IEnumerator PrewarmPoolsCoroutine(int[] actualRoomCounts)
        {
            _roomPools.Clear();
            for (int i = 0; i < _allPrefabs.Count; i++)
            {
                _roomPools[i] = new Queue<GameObject>();
                int targetPoolSize = math.min(_allPrefabs[i].prewarmCount, actualRoomCounts[i]);

                for (int p = 0; p < targetPoolSize; p++)
                {
                    GameObject roomInst = Instantiate(_allPrefabs[i].gameObject);
                    roomInst.SetActive(false);
                    _roomPools[i].Enqueue(roomInst);
                    if (p % 3 == 0) yield return null; 
                }
            }
        }

        private void SetCurrentRoom(int index)
        {
            if (_currentRoomIndex != index)
            {
                _currentRoomIndex = index;
                ShiftRoomWindow();
            }
        }

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

        private GameObject GetRoomFromPool(PlacedRoom roomData)
        {
            Queue<GameObject> pool = _roomPools[roomData.prefabID];
            GameObject roomInstance;

            if (pool.Count > 0)
            {
                roomInstance = pool.Dequeue();
                roomInstance.transform.position = (Vector3)roomData.worldPosition;
                roomInstance.transform.rotation = (Quaternion)roomData.worldRotation;
                roomInstance.SetActive(true);
            }
            else
            {
                roomInstance = Instantiate(_allPrefabs[roomData.prefabID].gameObject, 
                    (Vector3)roomData.worldPosition, (Quaternion)roomData.worldRotation);
            }

            return roomInstance;
        }

        private void ReturnRoomToPool(int prefabID, GameObject roomInstance)
        {
            if (roomInstance.TryGetComponent(out RoomTrigger trigger))
                trigger.OnPlayerEnteredRoom -= SetCurrentRoom;

            roomInstance.SetActive(false);
            _roomPools[prefabID].Enqueue(roomInstance);
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            if (!enableDebug || _virtualDungeon == null) return;

            foreach (var vRoom in _virtualDungeon)
            {
                Gizmos.color = vRoom.isLoaded ? Color.green : Color.gray;
                Gizmos.matrix = Matrix4x4.TRS((Vector3)vRoom.data.worldPosition, (Quaternion)vRoom.data.worldRotation, Vector3.one);
                Vector3 center = (Vector3)(vRoom.data.worldCenter - vRoom.data.worldPosition);
                Vector3 size = (Vector3)(vRoom.data.worldExtents * 2f);
                Gizmos.DrawWireCube(center, size);
            }
            Gizmos.matrix = Matrix4x4.identity;
        }
#endif
    }
}
