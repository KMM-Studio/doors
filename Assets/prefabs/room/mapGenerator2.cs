using System;
using System.Collections;
using System.Collections.Generic;
using prefabs.room.scripts;
using Unity.Mathematics;
using UnityEngine;

namespace prefabs.room
{
    public class LinearDungeonManager : MonoBehaviour
    {
        public static LinearDungeonManager Instance;

        #region Serialized Fields

        [Header("Generation Settings")]
        public Transform originTransform; 
        public int desiredRoomCount = 10; 
    
        [Header("Prefabs")]
        public RoomData startRoomPrefab;  
        public RoomData[] randomRoomPrefabs;
        public RoomData[] alcovePrefabs;
        public RoomData finalRoomPrefab;  
        
        [Header("Generation Settings")]
        [Range(0f, 1f)]
        public float alcoveSpawnChance = 0.5f;

        [Header("Streaming Settings")]
        public int lookAhead = 3;
        public int lookBehind = 3;
        
        [Space]
        [Header("Debug & Visualization")]
        public bool enableDebug = true;

        #endregion

        #region Internal State

        private readonly List<RoomData> _allPrefabs = new List<RoomData>();
        private int _finalRoomPrefabID; 

        private readonly List<VirtualRoom> _virtualDungeon = new List<VirtualRoom>();
        private readonly Dictionary<int, Queue<GameObject>> _roomPools = new Dictionary<int, Queue<GameObject>>();
        private int _currentRoomIndex = -1;

        private const string LogPrefix = "<color=cyan><b>[LinearDungeonManager]</b></color>";
        private readonly List<Vector3> _debugErrorLocations = new List<Vector3>();
        private readonly List<Ray> _debugSocketRays = new List<Ray>();

        #endregion

        private void Awake()
        {
            Instance = this;
            ValidateDependencies();

            _allPrefabs.Add(startRoomPrefab);           
            _allPrefabs.AddRange(randomRoomPrefabs);
            _allPrefabs.Add(finalRoomPrefab);
            _finalRoomPrefabID = _allPrefabs.Count - 1; 
        }

        private void Start()
        {
            if (startRoomPrefab == null) return;
            StartCoroutine(GenerateAndPrewarmCoroutine());
        }

        private void ValidateDependencies()
        {
            Debug.Assert(randomRoomPrefabs != null && randomRoomPrefabs.Length > 0, 
                $"{LogPrefix} <color=red><b>randomRoomPrefabs</b></color> is unassigned!");
            Debug.Assert(finalRoomPrefab != null, 
                $"{LogPrefix} <color=red><b>finalRoomPrefab</b></color> is unassigned!");
        }

        /// <summary>
        /// Pure Unity.Mathematics OBB Overlap Check. 100% Burst & Job System Safe.
        /// </summary>
        private static bool CheckOBBIntersection(float3 centerA, float3 extentsA, quaternion rotA, 
                                                 float3 centerB, float3 extentsB, quaternion rotB)
        {
            float EPSILON = 1e-4f;

            // math.mul replaces quaternion * vector
            float3 aX = math.mul(rotA, new float3(1, 0, 0)); 
            float3 aY = math.mul(rotA, new float3(0, 1, 0)); 
            float3 aZ = math.mul(rotA, new float3(0, 0, 1));

            float3 bX = math.mul(rotB, new float3(1, 0, 0)); 
            float3 bY = math.mul(rotB, new float3(0, 1, 0)); 
            float3 bZ = math.mul(rotB, new float3(0, 0, 1));
            
            float3 t = centerB - centerA;

            float tx = math.dot(t, aX); float ty = math.dot(t, aY); float tz = math.dot(t, aZ);

            float r00 = math.dot(aX, bX); float r01 = math.dot(aX, bY); float r02 = math.dot(aX, bZ);
            float r10 = math.dot(aY, bX); float r11 = math.dot(aY, bY); float r12 = math.dot(aY, bZ);
            float r20 = math.dot(aZ, bX); float r21 = math.dot(aZ, bY); float r22 = math.dot(aZ, bZ);

            float ar00 = math.abs(r00) + EPSILON; float ar01 = math.abs(r01) + EPSILON; float ar02 = math.abs(r02) + EPSILON;
            float ar10 = math.abs(r10) + EPSILON; float ar11 = math.abs(r11) + EPSILON; float ar12 = math.abs(r12) + EPSILON;
            float ar20 = math.abs(r20) + EPSILON; float ar21 = math.abs(r21) + EPSILON; float ar22 = math.abs(r22) + EPSILON;

            float ra, rb;

            ra = extentsA.x; rb = extentsB.x * ar00 + extentsB.y * ar01 + extentsB.z * ar02; if (math.abs(tx) > ra + rb) return false;
            ra = extentsA.y; rb = extentsB.x * ar10 + extentsB.y * ar11 + extentsB.z * ar12; if (math.abs(ty) > ra + rb) return false;
            ra = extentsA.z; rb = extentsB.x * ar20 + extentsB.y * ar21 + extentsB.z * ar22; if (math.abs(tz) > ra + rb) return false;

            ra = extentsA.x * ar00 + extentsA.y * ar10 + extentsA.z * ar20; rb = extentsB.x; if (math.abs(tx * r00 + ty * r10 + tz * r20) > ra + rb) return false;
            ra = extentsA.x * ar01 + extentsA.y * ar11 + extentsA.z * ar21; rb = extentsB.y; if (math.abs(tx * r01 + ty * r11 + tz * r21) > ra + rb) return false;
            ra = extentsA.x * ar02 + extentsA.y * ar12 + extentsA.z * ar22; rb = extentsB.z; if (math.abs(tx * r02 + ty * r12 + tz * r22) > ra + rb) return false;

            ra = extentsA.y * ar20 + extentsA.z * ar10; rb = extentsB.y * ar02 + extentsB.z * ar01; if (math.abs(tz * r10 - ty * r20) > ra + rb) return false;
            ra = extentsA.y * ar21 + extentsA.z * ar11; rb = extentsB.x * ar02 + extentsB.z * ar00; if (math.abs(tz * r11 - ty * r21) > ra + rb) return false;
            ra = extentsA.y * ar22 + extentsA.z * ar12; rb = extentsB.x * ar01 + extentsB.y * ar00; if (math.abs(tz * r12 - ty * r22) > ra + rb) return false;
            ra = extentsA.x * ar20 + extentsA.z * ar00; rb = extentsB.y * ar12 + extentsB.z * ar11; if (math.abs(tx * r20 - tz * r00) > ra + rb) return false;
            ra = extentsA.x * ar21 + extentsA.z * ar01; rb = extentsB.x * ar12 + extentsB.z * ar10; if (math.abs(tx * r21 - tz * r01) > ra + rb) return false;
            ra = extentsA.x * ar22 + extentsA.z * ar02; rb = extentsB.x * ar11 + extentsB.y * ar10; if (math.abs(tx * r22 - tz * r02) > ra + rb) return false;
            ra = extentsA.x * ar10 + extentsA.y * ar00; rb = extentsB.y * ar22 + extentsB.z * ar21; if (math.abs(ty * r00 - tx * r10) > ra + rb) return false;
            ra = extentsA.x * ar11 + extentsA.y * ar01; rb = extentsB.x * ar22 + extentsB.z * ar20; if (math.abs(ty * r01 - tx * r11) > ra + rb) return false;
            ra = extentsA.x * ar12 + extentsA.y * ar02; rb = extentsB.x * ar21 + extentsB.y * ar20; if (math.abs(ty * r02 - tx * r12) > ra + rb) return false;

            return true; 
        }

        public IEnumerator GenerateAndPrewarmCoroutine()
        {
            _virtualDungeon.Clear();
            int[] actualRoomCounts = new int[_allPrefabs.Count];

            int totalRooms = desiredRoomCount + 2; 

            for (int i = 0; i < totalRooms; i++)
            {
                bool roomSuccessfullyPlaced = false;
                int failedAttempts = 0;

                while (!roomSuccessfullyPlaced)
                {
                    // 1. Pick the correct Prefab ID based on the loop index
                    int prefabID;
                    if (i == 0) 
                    {
                        prefabID = 0; // The Start Room
                    }
                    else if (i == totalRooms - 1) 
                    {
                        prefabID = _allPrefabs.Count - 1; // The Final Room
                    }
                    else 
                    {
                        prefabID = UnityEngine.Random.Range(1, _allPrefabs.Count - 1); // A Random Room

                        // PREVENT DUPLICATES IN A ROW (Soft Constraint)
                        // Only apply this rule if we have at least 2 different random rooms,
                        // AND we haven't been struggling to place this room (e.g., less than 20 failed attempts).
                        int relaxationThreshold = 20; 
    
                        if (randomRoomPrefabs.Length > 1 && failedAttempts < relaxationThreshold)
                        {
                            int prevPrefabID = _virtualDungeon[i - 1].data.prefabID;
        
                            // Keep re-rolling until we get a room that is different from the previous one
                            while (prefabID == prevPrefabID)
                            {
                                prefabID = UnityEngine.Random.Range(1, _allPrefabs.Count - 1);
                            }
                        }
                    }

                    RoomData prefab = _allPrefabs[prefabID];
                    float3 roomPos;
                    quaternion roomRot;
                    int entryIndex = 0;
                    int exitIndex = 0;
                    
                    if (i == 0)
                    {
                        // Bridge to Unity Engine: Cast Transform properties to Unity.Mathematics types
                        roomPos = (float3)originTransform.position;
                        roomRot = (quaternion)originTransform.rotation;
                    }
                    else
                    {
                        
                        // ----------------------------------------------------------
                        // 1. CHOOSE THE EXIT DOOR ON THE PREVIOUS ROOM
                        // ----------------------------------------------------------
                        PlacedRoom prevRoom = _virtualDungeon[i - 1].data;
                        RoomData prevPrefab = _allPrefabs[prevRoom.prefabID];

                        
                        if (prevPrefab.sockets.Count > 1)
                        {
                            // Keep randomly picking a door until we pick one that IS NOT the door we just entered through!
                            exitIndex = UnityEngine.Random.Range(0, prevPrefab.sockets.Count);
                            while (exitIndex == prevRoom.entrySocketIndex)
                            {
                                exitIndex = UnityEngine.Random.Range(0, prevPrefab.sockets.Count);
                            }
                        }
                        var exitSocket = prevPrefab.sockets[exitIndex];

                        float3 currentSocketPos = prevRoom.worldPosition + math.mul(prevRoom.worldRotation, (float3)exitSocket.localPosition);
                        quaternion currentSocketRot = math.mul(prevRoom.worldRotation, (quaternion)exitSocket.localRotation);

                        // ----------------------------------------------------------
                        // 2. CHOOSE THE ENTRY DOOR ON THE NEW ROOM
                        // ----------------------------------------------------------
                        
                        // If this is a normal room (not the final boss room), pick ANY socket as the entry
                        if (i < totalRooms - 1 && prefab.sockets.Count > 1) 
                        {
                            entryIndex = UnityEngine.Random.Range(0, prefab.sockets.Count);
                        }
                        var entrySocket = prefab.sockets[entryIndex];

                        // Rotate and position as normal...
                        quaternion targetRotFlipped = math.mul(currentSocketRot, quaternion.Euler(0f, math.PI, 0f));
                        roomRot = math.mul(targetRotFlipped, math.inverse((quaternion)entrySocket.localRotation));
                        
                        float3 rotatedEntryOffset = math.mul(roomRot, (float3)entrySocket.localPosition);
                        roomPos = currentSocketPos - rotatedEntryOffset;
                    }

                    // Overlap check preparation
                    float3 worldCenter = roomPos + math.mul(roomRot, (float3)prefab.localCenter);
                    float3 shrunkenExtents = (float3)prefab.localExtents * 0.95f; 
                    
                    bool isOverlapping = false;
                    
                    for (int j = 0; j < _virtualDungeon.Count; j++)
                    {
                        PlacedRoom placed = _virtualDungeon[j].data;
                        float3 placedShrunkenExtents = placed.worldExtents * 0.95f;

                        if (CheckOBBIntersection(worldCenter, shrunkenExtents, roomRot, 
                                                 placed.worldCenter, placedShrunkenExtents, placed.worldRotation))
                        {
                            isOverlapping = true;
                            break;
                        }
                    }

                    if (!isOverlapping)
                    {   
                        if (i > 0)
                        {
                            _virtualDungeon[i - 1].data.usedSockets.Add(exitIndex);
                        }
                        
                        HashSet<int> newRoomUsedSockets = new HashSet<int> { entryIndex };

                        PlacedRoom newRoom = new PlacedRoom
                        {
                            prefabID = prefabID,
                            worldPosition = roomPos,
                            worldRotation = roomRot,
                            worldCenter = worldCenter, 
                            worldExtents = (float3)prefab.localExtents,
                            entrySocketIndex = entryIndex,
                            usedSockets = newRoomUsedSockets,
                        };
                        
                        _virtualDungeon.Add(new VirtualRoom { data = newRoom, isLoaded = false });
                        actualRoomCounts[prefabID]++;
                        
                        roomSuccessfullyPlaced = true;
                    }
                    else
                    {
                        failedAttempts++;
                        if (failedAttempts % 50 == 0) yield return null;
                    }
                }
            }

            // PREWARM POOLS
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

            SetCurrentRoom(0);
        }

        private void SetCurrentRoom(int index)
        {
            if (_currentRoomIndex != index)
            {
                if (enableDebug) Debug.Log($"{LogPrefix} Player entered room <color=yellow><b>{index}</b></color>.");
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
                
                // Bridge to Unity Engine: Cast Unity.Mathematics back to Unity Types for Transforms
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
            {
                trigger.OnPlayerEnteredRoom -= SetCurrentRoom;
            }

            roomInstance.SetActive(false);
            _roomPools[prefabID].Enqueue(roomInstance);
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            if (!enableDebug) return;

            if (_virtualDungeon != null)
            {
                for (int i = 0; i < _virtualDungeon.Count; i++)
                {
                    var vRoom = _virtualDungeon[i];
                    Gizmos.color = vRoom.isLoaded ? Color.green : Color.gray;
                    
                    // Cast Unity.Mathematics to Unity Engine types for Gizmos
                    Gizmos.matrix = Matrix4x4.TRS((Vector3)vRoom.data.worldPosition, (Quaternion)vRoom.data.worldRotation, Vector3.one);
                    
                    Vector3 center = (Vector3)(vRoom.data.worldCenter - vRoom.data.worldPosition);
                    Vector3 size = (Vector3)(vRoom.data.worldExtents * 2f);
                    
                    Gizmos.DrawWireCube(center, size);
                }
            }
            Gizmos.matrix = Matrix4x4.identity;
        }
#endif
    }
}