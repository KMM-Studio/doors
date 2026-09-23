using Unity.Mathematics;
using UnityEngine;

namespace prefabs.room.scripts
{
    // --- Unmanaged Structs (Safe for C# Jobs) ---
    public struct JobRoomDefinition
    {
        public int prefabID;
        public float3 localCenter;
        public float3 localExtents;
        public int socketStartIndex;
        public int socketCount;
    }

    public struct JobSocketData
    {
        public float3 localPosition;
        public quaternion localRotation;
    }

    public struct PlacedRoom
    {
        public int prefabID;
        public float3 worldPosition;
        public quaternion worldRotation;
        public float3 worldCenter;
        public float3 worldExtents;
    }

    // --- Managed Wrapper (For Main Thread Streaming) ---
    public class VirtualRoom
    {
        public PlacedRoom data;
        public GameObject instance;
        public bool isLoaded;
    }
}