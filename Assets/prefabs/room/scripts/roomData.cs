using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace prefabs.room.scripts
{
    /// <summary>
    ///     Defines spatial and pooling data for a procedural room segment.
    ///     Handles automatic bounds calculation and socket registration for room generation.
    /// </summary>
    [RequireComponent(typeof(RoomTrigger))]
    public class RoomData : MonoBehaviour
    {
        [Header("Pooling Settings")] [Tooltip("The number of room instances to prewarm in the object pool.")]
        public int prewarmCount = 5;

        [Space(10)]
        [Header("Auto-Voxelization")]
        [Tooltip("Uncheck for simple rectangular rooms. Check for L-shaped rooms or stairs to enable greedy meshing.")]
        public bool isComplexShape;

        [Tooltip("The standardized block size (X, Y, Z).")]
        public Vector3 blockSize = new(2f, 2.5f, 2f);

        [Tooltip("Sampling multiplier. 2 = samples with blocks half the size, then merges them.")] [Range(1, 4)]
        public int oversampleFactor = 1;

        [Space(10)]
        [Header("Baked Data (Auto-Fills in Editor)")]
        [Tooltip("The calculated local center of the room based on its colliders.")]
        public Vector3 localCenter;

        [Tooltip("The calculated local extents of the room's trigger bounds.")]
        public Vector3 localExtents;

        [Tooltip("List of all connection sockets available in this room.")]
        public List<SocketData> sockets = new();

        [Header("Compound Bounds")] public List<Bounds> subBounds = new();

        private void Awake()
        {
            Debug.Assert(sockets != null,
                "<color=red><b>[RoomData]</b></color> Sockets list is null! Check inspector serialization.", this);
            Debug.Assert(sockets.Count > 0,
                $"<color=orange><b>[RoomData]</b></color> Room '{gameObject.name}' has no baked sockets. Procedural generation may fail.",
                this);

            var triggerCol = GetComponent<BoxCollider>();
            Debug.Assert(triggerCol != null,
                $"<color=red><b>[RoomData]</b></color> Missing BoxCollider trigger on '{gameObject.name}'. Did you forget to run 'Bake Room Data'?",
                this);

            if (triggerCol != null)
                Debug.Assert(triggerCol.isTrigger,
                    $"<color=red><b>[RoomData]</b></color> BoxCollider on '{gameObject.name}' must be set to 'isTrigger' for room detection.",
                    this);
        }

        [Serializable]
        public struct SocketData
        {
            public Vector3 localPosition;
            public Quaternion localRotation;
        }

#if UNITY_EDITOR
        [Space(15)] [Header("Debug & Visualization")] [SerializeField]
        private bool enableDebug = true;

        [SerializeField] private Color boundsColor = new(0.2f, 0.8f, 1f, 0.5f);
        [SerializeField] private Color socketColor = new(1f, 0.5f, 0f, 0.8f);

        [ContextMenu("Bake Room Data (Auto-Grid)")]
        public void BakeData()
        {
            sockets.Clear();
            subBounds.Clear();

            var childSockets = GetComponentsInChildren<RoomSocket>();
            foreach (var s in childSockets)
                sockets.Add(new SocketData
                {
                    localPosition = transform.InverseTransformPoint(s.transform.position),
                    localRotation = Quaternion.Inverse(transform.rotation) * s.transform.rotation
                });

            var allColliders = GetComponentsInChildren<Collider>();
            var totalBounds = new Bounds(transform.position, Vector3.zero);
            var boundsInitialized = false;

            foreach (var col in allColliders)
            {
                if (col.isTrigger) continue;

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
                if (!isComplexShape)
                {
                    // FAST PATH: Single bounding box for simple rooms
                    var center = transform.InverseTransformPoint(totalBounds.center);
                    var shrunkenSize = totalBounds.size * 0.90f;
                    subBounds.Add(new Bounds(center, shrunkenSize));
                }
                else
                {
                    // COMPLEX PATH: Oversampled Greedy Meshing
                    var effectiveBlockSize = blockSize / oversampleFactor;

                    var rawMin = transform.InverseTransformPoint(totalBounds.min);
                    var rawMax = transform.InverseTransformPoint(totalBounds.max);

                    var localMin = new Vector3(
                        (float)Math.Round(rawMin.x, 2),
                        (float)Math.Round(rawMin.y, 2),
                        (float)Math.Round(rawMin.z, 2)
                    );

                    var localMax = new Vector3(
                        (float)Math.Round(rawMax.x, 2),
                        (float)Math.Round(rawMax.y, 2),
                        (float)Math.Round(rawMax.z, 2)
                    );

                    var startX = Mathf.Floor(localMin.x / effectiveBlockSize.x) * effectiveBlockSize.x;
                    var endX = Mathf.Ceil(localMax.x / effectiveBlockSize.x) * effectiveBlockSize.x;
                    var startY = Mathf.Floor(localMin.y / effectiveBlockSize.y) * effectiveBlockSize.y;
                    var endY = Mathf.Ceil(localMax.y / effectiveBlockSize.y) * effectiveBlockSize.y;
                    var startZ = Mathf.Floor(localMin.z / effectiveBlockSize.z) * effectiveBlockSize.z;
                    var endZ = Mathf.Ceil(localMax.z / effectiveBlockSize.z) * effectiveBlockSize.z;

                    var hitBuffer = new Collider[20];
                    var rawBlocks = new List<Bounds>();

                    for (var x = startX; x < endX; x += effectiveBlockSize.x)
                    for (var y = startY; y < endY; y += effectiveBlockSize.y)
                    for (var z = startZ; z < endZ; z += effectiveBlockSize.z)
                    {
                        var localBlockCenter = new Vector3(
                            x + effectiveBlockSize.x * 0.5f,
                            y + effectiveBlockSize.y * 0.5f,
                            z + effectiveBlockSize.z * 0.5f
                        );
                        var worldBlockCenter = transform.TransformPoint(localBlockCenter);

                        // 50% width on X/Z to prevent wall bleed, 100% on Y to catch floors
                        var testSize = new Vector3(
                            effectiveBlockSize.x * 0.95f,
                            effectiveBlockSize.y * 1.0f,
                            effectiveBlockSize.z * 0.95f
                        );
                        var halfExtents = testSize * 0.5f;

                        var hitCount = Physics.OverlapBoxNonAlloc(worldBlockCenter, halfExtents, hitBuffer,
                            Quaternion.identity);
                        var isOccupied = false;

                        for (var i = 0; i < hitCount; i++)
                        {
                            var hit = hitBuffer[i];
                            if (hit.isTrigger) continue;

                            if (hit.transform.IsChildOf(transform))
                            {
                                isOccupied = true;
                                break;
                            }
                        }

                        if (isOccupied)
                            // Save at 100% scale so edges touch perfectly for the merger
                            rawBlocks.Add(new Bounds(localBlockCenter, effectiveBlockSize));
                    }

                    // Run the Greedy Meshing optimizer
                    var optimizedBlocks = OptimizeBoundsList(rawBlocks);

                    // Apply the 0.90f Burst shrink factor to the finalized, giant blocks
                    foreach (var block in optimizedBlocks) subBounds.Add(new Bounds(block.center, block.size * 0.90f));
                }

                // Update the master trigger bounds for the coarse check
                localCenter = transform.InverseTransformPoint(totalBounds.center);
                localExtents = totalBounds.extents * 0.95f;

                var triggerCol = GetComponent<BoxCollider>();
                if (triggerCol == null) triggerCol = gameObject.AddComponent<BoxCollider>();

                triggerCol.isTrigger = true;
                triggerCol.center = localCenter;
                triggerCol.size = localExtents * 1.8f;
            }

            EditorUtility.SetDirty(this);
        }

        // --- GREEDY MESHING HELPERS ---
        private List<Bounds> OptimizeBoundsList(List<Bounds> inputBlocks)
        {
            var blocks = new List<Bounds>(inputBlocks);
            blocks = MergeAlongAxis(blocks, 0); // X-Axis
            blocks = MergeAlongAxis(blocks, 2); // Z-Axis
            blocks = MergeAlongAxis(blocks, 1); // Y-Axis
            return blocks;
        }

        private List<Bounds> MergeAlongAxis(List<Bounds> blocks, int axis)
        {
            var merged = true;
            while (merged)
            {
                merged = false;
                for (var i = 0; i < blocks.Count; i++)
                {
                    for (var j = i + 1; j < blocks.Count; j++)
                        if (CanMerge(blocks[i], blocks[j], axis))
                        {
                            var combined = blocks[i];
                            combined.Encapsulate(blocks[j]);

                            blocks.RemoveAt(j);
                            blocks.RemoveAt(i);
                            blocks.Add(combined);

                            merged = true;
                            break;
                        }

                    if (merged) break;
                }
            }

            return blocks;
        }

        private bool CanMerge(Bounds a, Bounds b, int axis)
        {
            var eps = 0.05f;

            var alignX = Mathf.Abs(a.center.x - b.center.x) < eps && Mathf.Abs(a.size.x - b.size.x) < eps;
            var alignY = Mathf.Abs(a.center.y - b.center.y) < eps && Mathf.Abs(a.size.y - b.size.y) < eps;
            var alignZ = Mathf.Abs(a.center.z - b.center.z) < eps && Mathf.Abs(a.size.z - b.size.z) < eps;

            if (axis == 0)
                return alignY && alignZ && (Mathf.Abs(a.max.x - b.min.x) < eps || Mathf.Abs(b.max.x - a.min.x) < eps);
            if (axis == 1)
                return alignX && alignZ && (Mathf.Abs(a.max.y - b.min.y) < eps || Mathf.Abs(b.max.y - a.min.y) < eps);
            if (axis == 2)
                return alignX && alignY && (Mathf.Abs(a.max.z - b.min.z) < eps || Mathf.Abs(b.max.z - a.min.z) < eps);

            return false;
        }

        private void OnDrawGizmosSelected()
        {
            if (!enableDebug) return;

            Gizmos.matrix = transform.localToWorldMatrix;

            if (localExtents == Vector3.zero || localExtents.sqrMagnitude < 0.1f)
            {
                Gizmos.color = Color.red;
                Gizmos.DrawWireCube(localCenter, Vector3.one * 2f);
                Handles.Label(transform.position + Vector3.up * 2f, "WARNING: Room Bounds Missing or Zero!");
            }
            else
            {
                Gizmos.color = boundsColor;
                Gizmos.DrawWireCube(localCenter, localExtents * 1.8f);
            }

            Gizmos.color = socketColor;
            foreach (var socket in sockets)
            {
                Gizmos.DrawSphere(socket.localPosition, 0.25f);
                var forward = socket.localRotation * Vector3.forward;
                Gizmos.DrawRay(socket.localPosition, forward * 1.5f);
                var up = socket.localRotation * Vector3.up;
                Gizmos.DrawRay(socket.localPosition + forward * 1.5f, up * 0.5f);
            }

            Gizmos.color = new Color(1f, 1f, 0f, 0.4f);
            foreach (var bound in subBounds) Gizmos.DrawWireCube(bound.center, bound.extents * 2f);
        }
#endif
    }
}