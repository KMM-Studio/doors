using System.Collections.Generic;
using UnityEngine;

namespace prefabs.room.scripts
{
    /// <summary>
    /// Defines spatial and pooling data for a procedural room segment.
    /// Handles automatic bounds calculation and socket registration for room generation.
    /// </summary>
    public class RoomData : MonoBehaviour
    {
        [Header("Pooling Settings")]
        [Tooltip("The number of room instances to prewarm in the object pool.")]
        public int prewarmCount = 5;

        [Space(10)]
        [Header("Baked Data (Auto-Fills in Editor)")]
        [Tooltip("The calculated local center of the room based on its colliders.")]
        public Vector3 localCenter;
        
        [Tooltip("The calculated local extents of the room's trigger bounds.")]
        public Vector3 localExtents;
        
        [Tooltip("List of all connection sockets available in this room.")]
        public List<SocketData> sockets = new List<SocketData>();

#if UNITY_EDITOR
        [Space(15)]
        [Header("Debug & Visualization")]
        [Tooltip("Toggle visual and console debugging for this room.")]
        [SerializeField] private bool enableDebug = true;
        
        [Tooltip("Color of the room's bounds gizmo in the Scene view.")]
        [SerializeField] private Color boundsColor = new Color(0.2f, 0.8f, 1f, 0.5f);
        
        [Tooltip("Color of the socket gizmos in the Scene view.")]
        [SerializeField] private Color socketColor = new Color(1f, 0.5f, 0f, 0.8f);
#endif

        /// <summary>
        /// Represents a connection point (socket) where other rooms can be attached.
        /// </summary>
        [System.Serializable]
        public struct SocketData
        {
            [Tooltip("Local position of the socket relative to the room's pivot.")]
            public Vector3 localPosition;
            
            [Tooltip("Local rotation of the socket relative to the room's pivot.")]
            public Quaternion localRotation;
        }

        /// <summary>
        /// Validates critical component references and data initialization on startup.
        /// </summary>
        private void Awake()
        {
            // Fail-Safe Asserts
            Debug.Assert(sockets != null, "<color=red><b>[RoomData]</b></color> Sockets list is null! Check inspector serialization.", this);
            Debug.Assert(sockets.Count > 0, $"<color=orange><b>[RoomData]</b></color> Room '{gameObject.name}' has no baked sockets. Procedural generation may fail.", this);
            
            BoxCollider triggerCol = GetComponent<BoxCollider>();
            Debug.Assert(triggerCol != null, $"<color=red><b>[RoomData]</b></color> Missing BoxCollider trigger on '{gameObject.name}'. Did you forget to run 'Bake Room Data'?", this);
            
            if (triggerCol != null)
            {
                Debug.Assert(triggerCol.isTrigger, $"<color=red><b>[RoomData]</b></color> BoxCollider on '{gameObject.name}' must be set to 'isTrigger' for room detection.", this);
            }
        }

#if UNITY_EDITOR
        /// <summary>
        /// Automatically calculates room bounds and registers child sockets.
        /// Injects a BoxCollider trigger to represent the room's spatial volume.
        /// </summary>
        [ContextMenu("Bake Room Data")]
        public void BakeData()
        {
            sockets.Clear();
            var childSockets = GetComponentsInChildren<RoomSocket>();
            
            if (enableDebug)
            {
                Debug.Log($"<color=cyan><b>[RoomData]</b></color> Starting bake for <b>{gameObject.name}</b>. Found {childSockets.Length} sockets.");
            }

            foreach (var s in childSockets)
            {
                sockets.Add(new SocketData
                {
                    localPosition = transform.InverseTransformPoint(s.transform.position),
                    localRotation = Quaternion.Inverse(transform.rotation) * s.transform.rotation
                });
            }

            Collider[] allColliders = GetComponentsInChildren<Collider>();
            bool boundsInitialized = false;
            Bounds totalBounds = new Bounds();

            int parsedColliders = 0;
            foreach (var col in allColliders)
            {
                if (col.isTrigger) continue;

                parsedColliders++;
                if (!boundsInitialized)
                {
                    totalBounds = col.bounds;
                    boundsInitialized = true;
                }
                else
                {
                    totalBounds.Encapsulate(col.bounds);
                }
            }
        
            if (boundsInitialized)
            {
                localCenter = transform.InverseTransformPoint(totalBounds.center);
                localExtents = totalBounds.extents * 0.95f;

                BoxCollider triggerCol = GetComponent<BoxCollider>();
                if (triggerCol == null)
                {
                    triggerCol = gameObject.AddComponent<BoxCollider>();
                    if (enableDebug) Debug.Log($"<color=yellow><b>[RoomData]</b></color> Added missing BoxCollider trigger to <b>{gameObject.name}</b>.");
                }
            
                triggerCol.isTrigger = true;
                triggerCol.center = localCenter;
                triggerCol.size = localExtents * 1.8f;

                if (enableDebug)
                {
                    Debug.Log($"<color=green><b>[RoomData]</b></color> Bake complete for <b>{gameObject.name}</b>. " +
                              $"Parsed {parsedColliders} colliders. Center: {localCenter}, Size: {localExtents}");
                }
            }
            else if (enableDebug)
            {
                Debug.LogWarning($"<color=red><b>[RoomData]</b></color> Bake failed for <b>{gameObject.name}</b>! No valid non-trigger colliders found to calculate bounds.", this);
            }

            UnityEditor.EditorUtility.SetDirty(this);
        }

        /// <summary>
        /// Visualizes room boundaries, sockets, and edge case alerts in the Scene view.
        /// </summary>
        private void OnDrawGizmosSelected()
        {
            if (!enableDebug) return;

            Gizmos.matrix = transform.localToWorldMatrix;
            
            // Edge Case Visualization: Warn if bounds failed to generate or are zero
            if (localExtents == Vector3.zero || localExtents.sqrMagnitude < 0.1f)
            {
                Gizmos.color = Color.red;
                Gizmos.DrawWireCube(localCenter, Vector3.one * 2f); // Noticeable error box
                UnityEditor.Handles.Label(transform.position + Vector3.up * 2f, "WARNING: Room Bounds Missing or Zero!");
            }
            else
            {
                Gizmos.color = boundsColor;
                // Draw the bounds as they apply to the BoxCollider (size = localExtents)
                Gizmos.DrawWireCube(localCenter, localExtents * 1.8f);
            }

            // Draw Sockets
            Gizmos.color = socketColor;
            foreach (var socket in sockets)
            {
                // Core socket position
                Gizmos.DrawSphere(socket.localPosition, 0.25f);
                
                // Forward direction (where the next room connects)
                Vector3 forward = socket.localRotation * Vector3.forward;
                Gizmos.DrawRay(socket.localPosition, forward * 1.5f);
                
                // Upward tick (ensures rotation isn't twisting the next room)
                Vector3 up = socket.localRotation * Vector3.up;
                Gizmos.DrawRay(socket.localPosition + (forward * 1.5f), up * 0.5f);
            }
        }
#endif
    }
}