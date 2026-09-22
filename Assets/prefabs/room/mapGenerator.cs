using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace prefabs.room
{
    /// <summary>
    /// Handles procedural generation of the dungeon layout.
    /// Ensures deterministic generation using a fixed seed and validates room placements via physics checks.
    /// </summary>
    public class MapGenerator : MonoBehaviour
    {
        [Header("Room Prefabs")]
        [Tooltip("List of standard rooms used for the main path.")]
        public List<GameObject> roomPrefabs;

        [Tooltip("List of alcove rooms used for branching paths.")]
        public List<GameObject> alcoveRoomPrefabs;
        
        [Space]
        [Header("Special Rooms")]
        [Tooltip("The starting safe room prefab.")]
        public GameObject entrySafeRoomPrefab;

        [Tooltip("The final exit safe room prefab.")]
        public GameObject exitSafeRoomPrefab;
        
        [Space]
        [Header("Generation Settings")]
        [SerializeField] 
        [Tooltip("Maximum number of main-path rooms to generate before placing the exit.")]
        private int maxRooms;

        [SerializeField]
        [Tooltip("Seed used for deterministic random generation.")]
        private int generationSeed = 42;

        [SerializeField] 
        [Tooltip("The current available open sockets for room connection.")]
        private List<roomSocket> globalAvailableSockets = new List<roomSocket>();
        
        [Space]
        [Header("Doors")]
        [Tooltip("Prefab spawned at successfully connected room sockets.")]
        public GameObject doorOpenablePrefab;

        [Tooltip("Prefab spawned at blocked or dead-end sockets.")]
        public GameObject doorLockedPrefab;

        [Space]
        [Header("Debugging")]
        [SerializeField] 
        [Tooltip("Toggle visual and console debugging. Disable in production for better performance.")]
        private bool enableDebug = true;

        // Internal State
        private readonly List<roomSocket> _branchingSockets = new List<roomSocket>();
        private System.Random _rng;

#if UNITY_EDITOR
        // Debug state caches for Gizmos
        private readonly List<Bounds> _debugPlacedBounds = new List<Bounds>();
        private readonly List<Bounds> _debugFailedBounds = new List<Bounds>();
#endif

        private void Awake()
        {
            // Fail-Safe Asserts
            Debug.Assert(entrySafeRoomPrefab != null, "<color=red><b>[MapGenerator]</b></color> Entry Safe Room Prefab is missing!");
            Debug.Assert(exitSafeRoomPrefab != null, "<color=red><b>[MapGenerator]</b></color> Exit Safe Room Prefab is missing!");
            Debug.Assert(roomPrefabs != null && roomPrefabs.Count > 0, "<color=red><b>[MapGenerator]</b></color> Room Prefabs list is empty!");
            Debug.Assert(maxRooms > 0, "<color=red><b>[MapGenerator]</b></color> maxRooms must be greater than 0!");
        }

        private void Start()
        {
            if (globalAvailableSockets.Count == 0)
            {
                Debug.LogError("<color=red><b>[MapGenerator]</b></color> No initial roomSocket assigned in globalAvailableSockets. Aborting generation.");
                return;
            }

            GenerateDungeon();
        }

        /// <summary>
        /// Generates the entire dungeon layout deterministically.
        /// Processes the main path first, then attempts to populate remaining sockets with alcoves or locked doors.
        /// </summary>
        public void GenerateDungeon()
        {
            _rng = new System.Random(generationSeed);
            int currentRooms = 0;
            _branchingSockets.Clear();
            
#if UNITY_EDITOR
            _debugPlacedBounds.Clear();
            _debugFailedBounds.Clear();
#endif

            if (enableDebug) Debug.Log($"<color=cyan><b>[MapGenerator]</b></color> Starting generation with seed <b>{generationSeed}</b>...");
            
            // Generate starting room
            TryAddMainRoom(entrySafeRoomPrefab);

            // 1. Generate normal main-path rooms (Linear generation)
            while (currentRooms < maxRooms - 1 && globalAvailableSockets.Count > 0)
            {
                GameObject randomRoom = roomPrefabs[_rng.Next(0, roomPrefabs.Count)];
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
                if (_rng.NextDouble() > 0.5 && alcoveRoomPrefabs.Count > 0)
                {
                    GameObject randomAlcoveRoom = alcoveRoomPrefabs[_rng.Next(0, alcoveRoomPrefabs.Count)];
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

            if (enableDebug) Debug.Log($"<color=green><b>[MapGenerator]</b></color> Dungeon complete with <b>{currentRooms}</b> main rooms generated.");
        }

        /// <summary>
        /// Attempts to append a specific room to the current main path.
        /// </summary>
        /// <param name="roomPrefab">The room to attach.</param>
        /// <returns>True if the room found a valid socket and fit without overlapping.</returns>
        private bool TryAddMainRoom(GameObject roomPrefab)
        {
            var shuffledSockets = globalAvailableSockets.OrderBy(x => _rng.Next()).ToList();

            foreach (var outSocket in shuffledSockets)
            {
                if (outSocket == null) continue;

                if (TryAddSpecificRoomToSocket(roomPrefab, outSocket, isMainRoute: true))
                {
                    return true;
                }
            }
            
            if (enableDebug) Debug.LogWarning($"<color=orange><b>[MapGenerator]</b></color> Failed to place main room <b>{roomPrefab.name}</b> on any available sockets.");
            return false;
        }

        /// <summary>
        /// Attempts to attach a specific room prefab to an available socket.
        /// Performs localized bounds calculation and a CheckBox to prevent environmental overlaps.
        /// </summary>
        /// <param name="roomPrefab">The room prefab to instantiate.</param>
        /// <param name="outSocket">The available socket to connect to.</param>
        /// <param name="isMainRoute">Flag determining if this is the critical path or an optional alcove.</param>
        /// <returns>True if the room was successfully placed without overlapping existing geometry.</returns>
        private bool TryAddSpecificRoomToSocket(GameObject roomPrefab, roomSocket outSocket, bool isMainRoute)
        {
            var newRoom = Instantiate(roomPrefab, new Vector3(0, -40, 0), Quaternion.identity);
            var newRoomSockets = newRoom.GetComponentsInChildren<roomSocket>().ToList();

            if (newRoomSockets.Count == 0)
            {
                Destroy(newRoom);
                return false;
            }

            newRoomSockets = newRoomSockets.OrderBy(x => _rng.Next()).ToList();

            // 1. Move to origin BEFORE doing bounds calculations. 
            // This ensures Collider.bounds gives us a perfect local axis-aligned box.
            newRoom.transform.position = Vector3.zero;
            newRoom.transform.rotation = Quaternion.identity;
            Physics.SyncTransforms();

            // 2. Grab ALL colliders.
            Collider[] allColliders = newRoom.GetComponentsInChildren<Collider>();
            if (allColliders.Length == 0)
            {
                if (enableDebug) Debug.LogWarning($"<color=orange><b>[MapGenerator]</b></color> Room <b>{roomPrefab.name}</b> has no colliders! Overlap check will fail.");
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

#if UNITY_EDITOR
                Bounds testBounds = new Bounds(worldCenter, shrunkenExtents * 2f);
#endif

                if (isOverlapping)
                {   
#if UNITY_EDITOR
                    if (enableDebug) _debugFailedBounds.Add(testBounds);
#endif
                    continue; 
                }
                
                // --- SUCCESSFUL PLACEMENT ---
#if UNITY_EDITOR
                if (enableDebug) _debugPlacedBounds.Add(testBounds);
#endif
                if (enableDebug) Debug.Log($"<color=cyan><b>[MapGenerator]</b></color> Successfully attached <b>{roomPrefab.name}</b>.");

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

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            if (!enableDebug) return;

            // Draw boundaries of successfully placed rooms
            Gizmos.color = new Color(0f, 1f, 0f, 0.2f);
            foreach (var bounds in _debugPlacedBounds)
            {
                Gizmos.DrawWireCube(bounds.center, bounds.size);
            }

            // Draw boundaries where placement failed due to overlap
            Gizmos.color = new Color(1f, 0f, 0f, 0.4f);
            foreach (var bounds in _debugFailedBounds)
            {
                Gizmos.DrawWireCube(bounds.center, bounds.size);
            }
            
            // Visualize available global sockets
            if (globalAvailableSockets != null)
            {
                foreach (var socket in globalAvailableSockets)
                {
                    if (socket != null)
                    {
                        Gizmos.color = Color.cyan;
                        Gizmos.DrawSphere(socket.transform.position, 0.3f);
                        
                        Gizmos.color = Color.magenta;
                        Gizmos.DrawRay(socket.transform.position, socket.transform.forward * 2f);
                    }
                }
            }
        }
#endif
    }
}