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
        [ReadOnly] public NativeArray<JobBound> bounds;

        public int totalRooms;
        public int randomRoomsCount;
        public int finalRoomID;
        public float alcoveSpawnChance;

        public float3 originPos;
        public quaternion originRot;

        public Random random;

        public NativeList<PlacedRoom> placedRooms;
        public NativeArray<int> roomCounts;

        public void Execute()
        {
            GenerateMainPath();
            GenerateAlcoves();
        }

        private void GenerateMainPath()
        {
            var maxAttemptsPerRoom = 500; // If it fails 500 times, it's clearly boxed in.
            var i = 0;

            // Use a while loop instead of a for loop so we can step backward (i--)
            while (i < totalRooms)
            {
                var roomSuccessfullyPlaced = false;
                var failedAttempts = 0;
                var isFinalRoom = i == totalRooms - 1;

                while (!roomSuccessfullyPlaced && failedAttempts < maxAttemptsPerRoom)
                {
                    var prefabID = PickNextRoomID(i, isFinalRoom, failedAttempts);

                    if (TryCalculateRoomPlacement(i, prefabID, isFinalRoom, out var newRoom, out var usedExitIndex))
                    {
                        // SAVE the connection index so we can undo it later if we need to backtrack 
                        newRoom.parentExitSocketIndex = usedExitIndex;

                        if (i > 0)
                        {
                            // Update the previous room's bitmask to mark the exit as used
                            var prevRoom = placedRooms[i - 1];
                            prevRoom.usedSocketsMask |= 1u << usedExitIndex;
                            placedRooms[i - 1] = prevRoom;
                        }

                        placedRooms.Add(newRoom);
                        roomCounts[prefabID]++;
                        roomSuccessfullyPlaced = true;
                    }
                    else
                    {
                        failedAttempts++;
                    }
                }

                // ==========================================
                // BACKTRACKING LOGIC
                // ==========================================
                if (!roomSuccessfullyPlaced)
                {
                    // If we can't even place the first room, abort completely.
                    if (i == 0) return;

                    // Step backward!
                    i--;

                    // Get the bad room that boxed us in
                    var badRoom = placedRooms[i];
                    roomCounts[badRoom.prefabID]--; // Remove it from the counts

                    // Un-mark the socket on its parent room so it can be used again
                    if (i > 0)
                    {
                        var parentRoom = placedRooms[i - 1];
                        // Bitwise trick: Turn OFF the bit using AND (&) and NOT (~)
                        parentRoom.usedSocketsMask &= ~(1u << badRoom.parentExitSocketIndex);
                        placedRooms[i - 1] = parentRoom;
                    }

                    // Erase the bad room from the physical dungeon array
                    placedRooms.RemoveAt(i);

                    // The outer while loop will now continue. Because we did i--, 
                    // it will immediately attempt to regenerate this step with a fresh start!
                }
                else
                {
                    // Success! Move forward to the next room.
                    i++;
                }
            }
        }

        private void GenerateAlcoves()
        {
            if (alcoveSpawnChance <= 0f) return;

            // Assume alcoves start after random rooms and before the final room
            var alcoveStartIndex = 1 + randomRoomsCount;
            var alcoveCount = roomDefs.Length - 1 - alcoveStartIndex; // All rooms minus start, random, and final

            if (alcoveCount <= 0) return;

            var mainPathCount = placedRooms.Length;

            for (var i = 0; i < mainPathCount; i++)
            {
                var mainRoom = placedRooms[i];
                var mainDef = roomDefs[mainRoom.prefabID];

                for (var s = 0; s < mainDef.socketCount; s++)
                {
                    // Check bitmask: if this socket is used, skip
                    if ((mainRoom.usedSocketsMask & (1u << s)) != 0) continue;

                    if (random.NextFloat() > alcoveSpawnChance) continue;

                    var alcovePrefabID = random.NextInt(alcoveStartIndex, alcoveStartIndex + alcoveCount);

                    if (TryCalculateAlcovePlacement(mainRoom, s, alcovePrefabID, out var newAlcove))
                    {
                        placedRooms.Add(newAlcove);
                        roomCounts[alcovePrefabID]++;

                        // Mark door as used
                        mainRoom.usedSocketsMask |= 1u << s;
                        placedRooms[i] = mainRoom;
                    }
                }
            }
        }

        private int PickNextRoomID(int currentIndex, bool isFinalRoom, int failedAttempts)
        {
            if (currentIndex == 0) return 0;
            if (isFinalRoom) return finalRoomID;

            var prefabID = random.NextInt(1, 1 + randomRoomsCount);

            if (randomRoomsCount > 1 && failedAttempts < 20)
            {
                var prevPrefabID = placedRooms[currentIndex - 1].prefabID;
                while (prefabID == prevPrefabID) prefabID = random.NextInt(1, 1 + randomRoomsCount);
            }

            return prefabID;
        }

        private bool TryCalculateRoomPlacement(int roomIndex, int prefabID, bool isFinalRoom, out PlacedRoom newRoom,
            out int exitSocketUsedOnPrevRoom)
        {
            newRoom = default;
            exitSocketUsedOnPrevRoom = 0;

            var prefab = roomDefs[prefabID];
            float3 roomPos;
            quaternion roomRot;
            var entryIndex = 0;

            if (roomIndex == 0)
            {
                roomPos = originPos;
                roomRot = originRot;
            }
            else
            {
                var prevRoom = placedRooms[roomIndex - 1];
                var prevPrefab = roomDefs[prevRoom.prefabID];

                if (prevPrefab.socketCount > 1)
                {
                    exitSocketUsedOnPrevRoom = random.NextInt(0, prevPrefab.socketCount);
                    while (exitSocketUsedOnPrevRoom == prevRoom.entrySocketIndex)
                        exitSocketUsedOnPrevRoom = random.NextInt(0, prevPrefab.socketCount);
                }

                var exitSocket = sockets[prevPrefab.socketStartIndex + exitSocketUsedOnPrevRoom];
                var currentSocketPos =
                    prevRoom.worldPosition + math.mul(prevRoom.worldRotation, exitSocket.localPosition);
                var currentSocketRot = math.mul(prevRoom.worldRotation, exitSocket.localRotation);

                if (!isFinalRoom && prefab.socketCount > 1) entryIndex = random.NextInt(0, prefab.socketCount);

                var entrySocket = sockets[prefab.socketStartIndex + entryIndex];
                var targetRotFlipped = math.mul(currentSocketRot, quaternion.Euler(0f, math.PI, 0f));
                roomRot = math.mul(targetRotFlipped, math.inverse(entrySocket.localRotation));

                var rotatedEntryOffset = math.mul(roomRot, entrySocket.localPosition);
                roomPos = currentSocketPos - rotatedEntryOffset;
            }

            var worldCenter = roomPos + math.mul(roomRot, prefab.localCenter);
            var shrunkenExtents = prefab.localExtents * 0.95f;

            for (var j = 0; j < placedRooms.Length; j++)
            {
                var placed = placedRooms[j];

                // PHASE 1: Coarse Check (The giant bounding boxes)
                var coarseOverlap = CheckObbIntersection(
                    worldCenter, shrunkenExtents, roomRot,
                    placed.worldCenter, placed.worldExtents * 0.95f, placed.worldRotation
                );

                if (coarseOverlap)
                    // PHASE 2: Fine Check (Compound shapes)
                    if (CheckCompoundIntersection(prefab, roomPos, roomRot, placed))
                        return false; // Actually blocked
            }

            newRoom = new PlacedRoom
            {
                prefabID = prefabID,
                worldPosition = roomPos,
                worldRotation = roomRot,
                worldCenter = worldCenter,
                worldExtents = prefab.localExtents,
                entrySocketIndex = entryIndex,
                usedSocketsMask = 1u << entryIndex
            };
            return true;
        }

        private bool CheckCompoundIntersection(JobRoomDef roomA, float3 posA, quaternion rotA, PlacedRoom roomB)
        {
            var prefabB = roomDefs[roomB.prefabID];

            for (var i = 0; i < roomA.boundCount; i++)
            {
                var boundA = bounds[roomA.boundStartIndex + i];
                var worldCenterA = posA + math.mul(rotA, boundA.localCenter);

                for (var j = 0; j < prefabB.boundCount; j++)
                {
                    // Assuming PlacedRoom tracks its prefab's bound info
                    var boundB = bounds[prefabB.boundStartIndex + j];
                    var worldCenterB = roomB.worldPosition + math.mul(roomB.worldRotation, boundB.localCenter);

                    // Use your existing SAT math[cite: 3]
                    if (CheckObbIntersection(
                            worldCenterA, boundA.localExtents, rotA,
                            worldCenterB, boundB.localExtents, roomB.worldRotation))
                        return true;
                }
            }

            return false;
        }

        private bool TryCalculateAlcovePlacement(PlacedRoom mainRoom, int socketIndex, int alcovePrefabID,
            out PlacedRoom newAlcove)
        {
            newAlcove = default;
            var mainPrefab = roomDefs[mainRoom.prefabID];
            var alcovePrefab = roomDefs[alcovePrefabID];

            var exitSocket = sockets[mainPrefab.socketStartIndex + socketIndex];
            var alcoveSocketPos = mainRoom.worldPosition + math.mul(mainRoom.worldRotation, exitSocket.localPosition);
            var alcoveSocketRot = math.mul(mainRoom.worldRotation, exitSocket.localRotation);

            var entrySocket = sockets[alcovePrefab.socketStartIndex + 0];
            var targetRotFlipped = math.mul(alcoveSocketRot, quaternion.Euler(0f, math.PI, 0f));
            var alcoveRot = math.mul(targetRotFlipped, math.inverse(entrySocket.localRotation));

            var rotatedEntryOffset = math.mul(alcoveRot, entrySocket.localPosition);
            var alcovePos = alcoveSocketPos - rotatedEntryOffset;

            var alcoveCenter = alcovePos + math.mul(alcoveRot, alcovePrefab.localCenter);
            var alcoveExtents = alcovePrefab.localExtents * 0.95f;

            for (var j = 0; j < placedRooms.Length; j++)
            {
                var placed = placedRooms[j];
                if (CheckObbIntersection(alcoveCenter, alcoveExtents, alcoveRot, placed.worldCenter,
                        placed.worldExtents * 0.95f, placed.worldRotation)) return false;
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

        private static bool CheckObbIntersection(float3 centerA, float3 extentsA, quaternion rotA, float3 centerB,
            float3 extentsB, quaternion rotB)
        {
            var epsilon = 1e-4f;

            var aX = math.mul(rotA, new float3(1, 0, 0));
            var aY = math.mul(rotA, new float3(0, 1, 0));
            var aZ = math.mul(rotA, new float3(0, 0, 1));
            var bX = math.mul(rotB, new float3(1, 0, 0));
            var bY = math.mul(rotB, new float3(0, 1, 0));
            var bZ = math.mul(rotB, new float3(0, 0, 1));

            var t = centerB - centerA;
            var tx = math.dot(t, aX);
            var ty = math.dot(t, aY);
            var tz = math.dot(t, aZ);

            var r00 = math.dot(aX, bX);
            var r01 = math.dot(aX, bY);
            var r02 = math.dot(aX, bZ);
            var r10 = math.dot(aY, bX);
            var r11 = math.dot(aY, bY);
            var r12 = math.dot(aY, bZ);
            var r20 = math.dot(aZ, bX);
            var r21 = math.dot(aZ, bY);
            var r22 = math.dot(aZ, bZ);

            var ar00 = math.abs(r00) + epsilon;
            var ar01 = math.abs(r01) + epsilon;
            var ar02 = math.abs(r02) + epsilon;
            var ar10 = math.abs(r10) + epsilon;
            var ar11 = math.abs(r11) + epsilon;
            var ar12 = math.abs(r12) + epsilon;
            var ar20 = math.abs(r20) + epsilon;
            var ar21 = math.abs(r21) + epsilon;
            var ar22 = math.abs(r22) + epsilon;

            float ra, rb;

            ra = extentsA.x;
            rb = extentsB.x * ar00 + extentsB.y * ar01 + extentsB.z * ar02;
            if (math.abs(tx) > ra + rb) return false;
            ra = extentsA.y;
            rb = extentsB.x * ar10 + extentsB.y * ar11 + extentsB.z * ar12;
            if (math.abs(ty) > ra + rb) return false;
            ra = extentsA.z;
            rb = extentsB.x * ar20 + extentsB.y * ar21 + extentsB.z * ar22;
            if (math.abs(tz) > ra + rb) return false;

            ra = extentsA.x * ar00 + extentsA.y * ar10 + extentsA.z * ar20;
            rb = extentsB.x;
            if (math.abs(tx * r00 + ty * r10 + tz * r20) > ra + rb) return false;
            ra = extentsA.x * ar01 + extentsA.y * ar11 + extentsA.z * ar21;
            rb = extentsB.y;
            if (math.abs(tx * r01 + ty * r11 + tz * r21) > ra + rb) return false;
            ra = extentsA.x * ar02 + extentsA.y * ar12 + extentsA.z * ar22;
            rb = extentsB.z;
            if (math.abs(tx * r02 + ty * r12 + tz * r22) > ra + rb) return false;

            ra = extentsA.y * ar20 + extentsA.z * ar10;
            rb = extentsB.y * ar02 + extentsB.z * ar01;
            if (math.abs(tz * r10 - ty * r20) > ra + rb) return false;
            ra = extentsA.y * ar21 + extentsA.z * ar11;
            rb = extentsB.x * ar02 + extentsB.z * ar00;
            if (math.abs(tz * r11 - ty * r21) > ra + rb) return false;
            ra = extentsA.y * ar22 + extentsA.z * ar12;
            rb = extentsB.x * ar01 + extentsB.y * ar00;
            if (math.abs(tz * r12 - ty * r22) > ra + rb) return false;
            ra = extentsA.x * ar20 + extentsA.z * ar00;
            rb = extentsB.y * ar12 + extentsB.z * ar11;
            if (math.abs(tx * r20 - tz * r00) > ra + rb) return false;
            ra = extentsA.x * ar21 + extentsA.z * ar01;
            rb = extentsB.x * ar12 + extentsB.z * ar10;
            if (math.abs(tx * r21 - tz * r01) > ra + rb) return false;
            ra = extentsA.x * ar22 + extentsA.z * ar02;
            rb = extentsB.x * ar11 + extentsB.y * ar10;
            if (math.abs(tx * r22 - tz * r02) > ra + rb) return false;
            ra = extentsA.x * ar10 + extentsA.y * ar00;
            rb = extentsB.y * ar22 + extentsB.z * ar21;
            if (math.abs(ty * r00 - tx * r10) > ra + rb) return false;
            ra = extentsA.x * ar11 + extentsA.y * ar01;
            rb = extentsB.x * ar22 + extentsB.z * ar20;
            if (math.abs(ty * r01 - tx * r11) > ra + rb) return false;
            ra = extentsA.x * ar12 + extentsA.y * ar02;
            rb = extentsB.x * ar21 + extentsB.y * ar20;
            if (math.abs(ty * r02 - tx * r12) > ra + rb) return false;

            return true;
        }
    }
}