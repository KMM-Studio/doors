using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace prefabs.room.scripts
{

            [BurstCompile]
    public struct GenerateDungeonJob : IJob
    {
        [ReadOnly] public NativeArray<JobRoomDef> roomDefs;
        [ReadOnly] public NativeArray<JobSocket> sockets;
        
        public int totalRooms;
        public int randomRoomsCount;
        public int finalRoomID;
        public float alcoveSpawnChance;
        
        public float3 originPos;
        public quaternion originRot;
        
        public Unity.Mathematics.Random random;

        public NativeList<PlacedRoom> placedRooms;
        public NativeArray<int> roomCounts;

        public void Execute()
        {
            GenerateMainPath();
            GenerateAlcoves();
        }

        private void GenerateMainPath()
        {
            for (int i = 0; i < totalRooms; i++)
            {
                bool roomSuccessfullyPlaced = false;
                int failedAttempts = 0;
                bool isFinalRoom = (i == totalRooms - 1);

                while (!roomSuccessfullyPlaced)
                {
                    int prefabID = PickNextRoomID(i, isFinalRoom, failedAttempts);

                    if (TryCalculateRoomPlacement(i, prefabID, isFinalRoom, out PlacedRoom newRoom, out int usedExitIndex))
                    {
                        if (i > 0)
                        {
                            // Update the previous room's bitmask to mark the exit as used
                            PlacedRoom prevRoom = placedRooms[i - 1];
                            prevRoom.usedSocketsMask |= (1u << usedExitIndex);
                            placedRooms[i - 1] = prevRoom;
                        }
                        
                        placedRooms.Add(newRoom);
                        roomCounts[prefabID]++;
                        roomSuccessfullyPlaced = true;
                    }
                    else
                    {
                        failedAttempts++;
                        // In a Job, we can't yield. We just spin. 
                        // If it fails 1000 times, force an abort to prevent infinite loop freezes.
                        if (failedAttempts > 1000) return; 
                    }
                }
            }
        }

        private void GenerateAlcoves()
        {
            if (alcoveSpawnChance <= 0f) return;
            
            // Assume alcoves start after random rooms and before the final room
            int alcoveStartIndex = 1 + randomRoomsCount;
            int alcoveCount = (roomDefs.Length - 1) - alcoveStartIndex; // All rooms minus start, random, and final
            
            if (alcoveCount <= 0) return;

            int mainPathCount = placedRooms.Length;

            for (int i = 0; i < mainPathCount; i++)
            {
                PlacedRoom mainRoom = placedRooms[i];
                JobRoomDef mainDef = roomDefs[mainRoom.prefabID];

                for (int s = 0; s < mainDef.socketCount; s++)
                {
                    // Check bitmask: if this socket is used, skip
                    if ((mainRoom.usedSocketsMask & (1u << s)) != 0) continue;
                    
                    if (random.NextFloat() > alcoveSpawnChance) continue;

                    int alcovePrefabID = random.NextInt(alcoveStartIndex, alcoveStartIndex + alcoveCount);

                    if (TryCalculateAlcovePlacement(mainRoom, s, alcovePrefabID, out PlacedRoom newAlcove))
                    {
                        placedRooms.Add(newAlcove);
                        roomCounts[alcovePrefabID]++;
                        
                        // Mark door as used
                        mainRoom.usedSocketsMask |= (1u << s);
                        placedRooms[i] = mainRoom;
                    }
                }
            }
        }

        private int PickNextRoomID(int currentIndex, bool isFinalRoom, int failedAttempts)
        {
            if (currentIndex == 0) return 0; 
            if (isFinalRoom) return finalRoomID; 

            int prefabID = random.NextInt(1, 1 + randomRoomsCount); 

            if (randomRoomsCount > 1 && failedAttempts < 20)
            {
                int prevPrefabID = placedRooms[currentIndex - 1].prefabID;
                while (prefabID == prevPrefabID)
                {
                    prefabID = random.NextInt(1, 1 + randomRoomsCount);
                }
            }
            return prefabID;
        }

        private bool TryCalculateRoomPlacement(int roomIndex, int prefabID, bool isFinalRoom, out PlacedRoom newRoom, out int exitSocketUsedOnPrevRoom)
        {
            newRoom = default;
            exitSocketUsedOnPrevRoom = 0;
            
            JobRoomDef prefab = roomDefs[prefabID];
            float3 roomPos;
            quaternion roomRot;
            int entryIndex = 0;

            if (roomIndex == 0)
            {
                roomPos = originPos;
                roomRot = originRot;
            }
            else
            {
                PlacedRoom prevRoom = placedRooms[roomIndex - 1];
                JobRoomDef prevPrefab = roomDefs[prevRoom.prefabID];

                if (prevPrefab.socketCount > 1)
                {
                    exitSocketUsedOnPrevRoom = random.NextInt(0, prevPrefab.socketCount);
                    while (exitSocketUsedOnPrevRoom == prevRoom.entrySocketIndex)
                        exitSocketUsedOnPrevRoom = random.NextInt(0, prevPrefab.socketCount);
                }
                
                JobSocket exitSocket = sockets[prevPrefab.socketStartIndex + exitSocketUsedOnPrevRoom];
                float3 currentSocketPos = prevRoom.worldPosition + math.mul(prevRoom.worldRotation, exitSocket.localPosition);
                quaternion currentSocketRot = math.mul(prevRoom.worldRotation, exitSocket.localRotation);

                if (!isFinalRoom && prefab.socketCount > 1) 
                {
                    entryIndex = random.NextInt(0, prefab.socketCount);
                }
                
                JobSocket entrySocket = sockets[prefab.socketStartIndex + entryIndex];
                quaternion targetRotFlipped = math.mul(currentSocketRot, quaternion.Euler(0f, math.PI, 0f));
                roomRot = math.mul(targetRotFlipped, math.inverse(entrySocket.localRotation));
                
                float3 rotatedEntryOffset = math.mul(roomRot, entrySocket.localPosition);
                roomPos = currentSocketPos - rotatedEntryOffset;
            }

            float3 worldCenter = roomPos + math.mul(roomRot, prefab.localCenter);
            float3 shrunkenExtents = prefab.localExtents * 0.95f; 
            
            for (int j = 0; j < placedRooms.Length; j++)
            {
                PlacedRoom placed = placedRooms[j];
                if (CheckObbIntersection(worldCenter, shrunkenExtents, roomRot, placed.worldCenter, placed.worldExtents * 0.95f, placed.worldRotation))
                {
                    return false; 
                }
            }

            newRoom = new PlacedRoom
            {
                prefabID = prefabID,
                worldPosition = roomPos,
                worldRotation = roomRot,
                worldCenter = worldCenter, 
                worldExtents = prefab.localExtents,
                entrySocketIndex = entryIndex,
                usedSocketsMask = (1u << entryIndex)
            };
            return true;
        }

        private bool TryCalculateAlcovePlacement(PlacedRoom mainRoom, int socketIndex, int alcovePrefabID, out PlacedRoom newAlcove)
        {
            newAlcove = default;
            JobRoomDef mainPrefab = roomDefs[mainRoom.prefabID];
            JobRoomDef alcovePrefab = roomDefs[alcovePrefabID];

            JobSocket exitSocket = sockets[mainPrefab.socketStartIndex + socketIndex];
            float3 alcoveSocketPos = mainRoom.worldPosition + math.mul(mainRoom.worldRotation, exitSocket.localPosition);
            quaternion alcoveSocketRot = math.mul(mainRoom.worldRotation, exitSocket.localRotation);

            JobSocket entrySocket = sockets[alcovePrefab.socketStartIndex + 0];
            quaternion targetRotFlipped = math.mul(alcoveSocketRot, quaternion.Euler(0f, math.PI, 0f));
            quaternion alcoveRot = math.mul(targetRotFlipped, math.inverse(entrySocket.localRotation));
            
            float3 rotatedEntryOffset = math.mul(alcoveRot, entrySocket.localPosition);
            float3 alcovePos = alcoveSocketPos - rotatedEntryOffset;

            float3 alcoveCenter = alcovePos + math.mul(alcoveRot, alcovePrefab.localCenter);
            float3 alcoveExtents = alcovePrefab.localExtents * 0.95f;
            
            for (int j = 0; j < placedRooms.Length; j++)
            {
                PlacedRoom placed = placedRooms[j];
                if (CheckObbIntersection(alcoveCenter, alcoveExtents, alcoveRot, placed.worldCenter, placed.worldExtents * 0.95f, placed.worldRotation))
                {
                    return false; 
                }
            }

            newAlcove = new PlacedRoom
            {
                prefabID = alcovePrefabID,
                worldPosition = alcovePos,
                worldRotation = alcoveRot,
                worldCenter = alcoveCenter,
                worldExtents = alcovePrefab.localExtents,
                entrySocketIndex = 0,
                usedSocketsMask = 1u << 0
            };
            return true;
        }

        private static bool CheckObbIntersection(float3 centerA, float3 extentsA, quaternion rotA, float3 centerB, float3 extentsB, quaternion rotB)
        {
            float epsilon = 1e-4f;

            float3 aX = math.mul(rotA, new float3(1, 0, 0)); float3 aY = math.mul(rotA, new float3(0, 1, 0)); float3 aZ = math.mul(rotA, new float3(0, 0, 1));
            float3 bX = math.mul(rotB, new float3(1, 0, 0)); float3 bY = math.mul(rotB, new float3(0, 1, 0)); float3 bZ = math.mul(rotB, new float3(0, 0, 1));
            
            float3 t = centerB - centerA;
            float tx = math.dot(t, aX); float ty = math.dot(t, aY); float tz = math.dot(t, aZ);

            float r00 = math.dot(aX, bX); float r01 = math.dot(aX, bY); float r02 = math.dot(aX, bZ);
            float r10 = math.dot(aY, bX); float r11 = math.dot(aY, bY); float r12 = math.dot(aY, bZ);
            float r20 = math.dot(aZ, bX); float r21 = math.dot(aZ, bY); float r22 = math.dot(aZ, bZ);

            float ar00 = math.abs(r00) + epsilon; float ar01 = math.abs(r01) + epsilon; float ar02 = math.abs(r02) + epsilon;
            float ar10 = math.abs(r10) + epsilon; float ar11 = math.abs(r11) + epsilon; float ar12 = math.abs(r12) + epsilon;
            float ar20 = math.abs(r20) + epsilon; float ar21 = math.abs(r21) + epsilon; float ar22 = math.abs(r22) + epsilon;

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
    }
}
