using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;

namespace prefabs.room.scripts
{
    // =========================================================================
    // 1. UNMANAGED DATA STRUCTS (Job-Safe)
    // =========================================================================
    
    public struct PlacedRoom
    {
        public int prefabID;
        public float3 worldPosition;
        public quaternion worldRotation;
        public float3 worldCenter;
        public float3 worldExtents;
        public int entrySocketIndex; 
        public uint usedSocketsMask; // Bitmask replaces HashSet for Job safety (Max 32 sockets)
    }

    public struct JobRoomDef
    {
        public int prefabID;
        public float3 localCenter;
        public float3 localExtents;
        public int socketStartIndex;
        public int socketCount;
    }

    public struct JobSocket
    {
        public float3 localPosition;
        public quaternion localRotation;
    }

    // --- Managed Wrapper (For Main Thread Streaming) ---
    public class VirtualRoom
    {
        public PlacedRoom data;
        public GameObject instance;
        public bool isLoaded;
    }
}