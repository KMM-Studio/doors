using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace prefabs.room
{
    public class MapGenerator : MonoBehaviour
    {
        public List<GameObject> roomPrefabs;
        public List<GameObject> alcoveRoomPrefabs;
        
        public GameObject entrySafeRoomPrefab;
        public GameObject exitSafeRoomPrefab;
        
        [SerializeField] private List<roomSocket> globalAvailableSockets = new List<roomSocket>();
        
        // We explicitly track bypassed sockets for alcoves
        private readonly List<roomSocket> _branchingSockets = new List<roomSocket>();
        
        [SerializeField] private int maxRooms;

        public GameObject doorOpenablePrefab;
        public GameObject doorLockedPrefab;

        private void Start()
        {
            if (globalAvailableSockets.Count == 0)
            {
                Debug.LogError("No initial roomSocket assigned.");
                return;
            }

            GenerateDungeon();
        }

        public void GenerateDungeon()
        {
            int currentRooms = 0;
            _branchingSockets.Clear();
            
            // Generate starting room
            TryAddMainRoom(entrySafeRoomPrefab);

            // 1. Generate normal main-path rooms (Linear generation)
            while (currentRooms < maxRooms - 1 && globalAvailableSockets.Count > 0)
            {
                GameObject randomRoom = roomPrefabs[UnityEngine.Random.Range(0, roomPrefabs.Count)];
                bool success = TryAddMainRoom(randomRoom);

                if (!success) break; 
                currentRooms++;
            }

            // 2. Guarantee the final room on the main path
            if (globalAvailableSockets.Count > 0)
            {
                TryAddMainRoom(exitSafeRoomPrefab);
            }
            
            // Add any leftover sockets from the very last room to the branching list
            _branchingSockets.AddRange(globalAvailableSockets.Where(s => s != null));
            globalAvailableSockets.Clear();

            // 3. Alcoves and Locked Doors
            foreach (var socket in _branchingSockets)
            {
                if (socket == null || socket.gameObject == null) continue;

                bool alcovePlaced = false;
                
                // 50% chance to attempt placing an alcove
                if (UnityEngine.Random.value > 0.5f && alcoveRoomPrefabs.Count > 0)
                {
                    GameObject randomAlcoveRoom = alcoveRoomPrefabs[UnityEngine.Random.Range(0, alcoveRoomPrefabs.Count)];
                    alcovePlaced = TryAddSpecificRoomToSocket(randomAlcoveRoom, socket, isMainRoute: false);
                }

                // If alcove placement failed or RNG rejected it -> lock the door
                if (!alcovePlaced)
                {
                    if (doorLockedPrefab != null)
                    {
                        Instantiate(doorLockedPrefab, socket.transform.position, socket.transform.rotation);
                    }
                    Destroy(socket.gameObject);
                }
            }

            Debug.Log($"Dungeon complete with {currentRooms} main rooms generated.");
        }

        private bool TryAddMainRoom(GameObject roomPrefab)
        {
            var shuffledSockets = globalAvailableSockets.OrderBy(x => Guid.NewGuid()).ToList();

            foreach (var outSocket in shuffledSockets)
            {
                if (outSocket == null) continue;

                if (TryAddSpecificRoomToSocket(roomPrefab, outSocket, isMainRoute: true))
                {
                    return true;
                }
            }
            return false;
        }

        private bool TryAddSpecificRoomToSocket(GameObject roomPrefab, roomSocket outSocket, bool isMainRoute)
        {
            var newRoom = Instantiate(roomPrefab, new Vector3(0, -40, 0), Quaternion.identity);
            var newRoomSockets = newRoom.GetComponentsInChildren<roomSocket>().ToList();

            if (newRoomSockets.Count == 0)
            {
                Destroy(newRoom);
                return false;
            }

            var newRoomCollider = newRoom.GetComponentInChildren<MeshCollider>(); 
            if (newRoomCollider != null) newRoomCollider.enabled = false;

            MeshFilter meshFilter = newRoom.GetComponentInChildren<MeshFilter>();
            Bounds localBounds = meshFilter.sharedMesh.bounds;
            Vector3 shrunkenExtents = Vector3.Scale(localBounds.extents, meshFilter.transform.lossyScale) * 0.95f;

            newRoomSockets = newRoomSockets.OrderBy(x => Guid.NewGuid()).ToList();

            foreach (var inSocket in newRoomSockets)
            {
                newRoom.transform.position = Vector3.zero;
                newRoom.transform.rotation = Quaternion.identity;

                newRoom.transform.rotation = Quaternion.LookRotation(-outSocket.transform.forward, outSocket.transform.up) * Quaternion.Inverse(inSocket.transform.localRotation);
                newRoom.transform.position += outSocket.transform.position - inSocket.transform.position;
                
                Physics.SyncTransforms();
                
                Vector3 worldCenter = meshFilter.transform.TransformPoint(localBounds.center);
                
                bool isOverlapping = Physics.CheckBox(
                    worldCenter,
                    shrunkenExtents,
                    meshFilter.transform.rotation,
                    LayerMask.GetMask("room"),
                    QueryTriggerInteraction.Collide
                );

                if (isOverlapping)
                {   
                    continue; 
                }
                
                // --- SUCCESSFUL PLACEMENT ---
                newRoomSockets.Remove(inSocket);

                if (isMainRoute)
                {
                    // Move the UNUSED sockets from the previous room into the branching list for alcoves
                    foreach (var leftoverSocket in globalAvailableSockets)
                    {
                        if (leftoverSocket != outSocket && leftoverSocket != null)
                        {
                            _branchingSockets.Add(leftoverSocket);
                        }
                    }

                    // Set the next linear step to ONLY use the new room's sockets
                    globalAvailableSockets = newRoomSockets;
                }
                else
                {
                    // This is an alcove. If it has extra sockets, cap them immediately so they don't bleed out.
                    foreach (var alcoveLeftover in newRoomSockets)
                    {
                        if (doorLockedPrefab != null)
                        {
                            Instantiate(doorLockedPrefab, alcoveLeftover.transform.position, alcoveLeftover.transform.rotation);
                        }
                        Destroy(alcoveLeftover.gameObject);
                    }
                }
                
                if (doorOpenablePrefab != null)
                {
                    Instantiate(doorOpenablePrefab, outSocket.transform.position, outSocket.transform.rotation);
                }
                
                Destroy(outSocket.gameObject);
                Destroy(inSocket.gameObject);
                
                if (newRoomCollider != null) newRoomCollider.enabled = true;
                
                return true;
            }
            
            Destroy(newRoom);
            return false;
        }
    }
}