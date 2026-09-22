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

    newRoomSockets = newRoomSockets.OrderBy(x => Guid.NewGuid()).ToList();

    // 1. Move to origin BEFORE doing bounds calculations. 
    // This ensures Collider.bounds gives us a perfect local axis-aligned box.
    newRoom.transform.position = Vector3.zero;
    newRoom.transform.rotation = Quaternion.identity;
    Physics.SyncTransforms();

    // 2. Grab ALL colliders.
    Collider[] allColliders = newRoom.GetComponentsInChildren<Collider>();
    if (allColliders.Length == 0)
    {
        Debug.LogWarning($"Room {roomPrefab.name} has no colliders! Overlap check will fail.");
        Destroy(newRoom);
        return false;
    }

    // 3. Calculate compound bounds using all active colliders
    Bounds totalBounds = allColliders[0].bounds;
    for (int i = 1; i < allColliders.Length; i++)
    {
        totalBounds.Encapsulate(allColliders[i].bounds);
    }

    // Cache the true local center and the physical extents based on the colliders
    Vector3 localCenter = newRoom.transform.InverseTransformPoint(totalBounds.center);
    Vector3 shrunkenExtents = totalBounds.extents * 0.95f;

    // 4. NOW disable all colliders so the room doesn't detect itself
    foreach (var col in allColliders)
    {
        col.enabled = false;
    }

    foreach (var inSocket in newRoomSockets)
    {
        newRoom.transform.position = Vector3.zero;
        newRoom.transform.rotation = Quaternion.identity;

        newRoom.transform.rotation = Quaternion.LookRotation(-outSocket.transform.forward, outSocket.transform.up) * Quaternion.Inverse(inSocket.transform.localRotation);
        newRoom.transform.position += outSocket.transform.position - inSocket.transform.position;
        
        Physics.SyncTransforms();
        
        // 5. Calculate where the center of our compound bounding box is in world space now
        Vector3 worldCenter = newRoom.transform.TransformPoint(localCenter);
        
        bool isOverlapping = Physics.CheckBox(
            worldCenter,
            shrunkenExtents,
            newRoom.transform.rotation,
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
            foreach (var leftoverSocket in globalAvailableSockets)
            {
                if (leftoverSocket != outSocket && leftoverSocket != null)
                {
                    _branchingSockets.Add(leftoverSocket);
                }
            }
            globalAvailableSockets = newRoomSockets;
        }
        else
        {
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
        
        // 6. Re-enable all colliders for future rooms to detect!
        foreach (var col in allColliders)
        {
            col.enabled = true;
        }
        
        return true;
    }
    
    Destroy(newRoom);
    return false;
}
    }
}