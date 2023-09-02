using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace Atrufulgium.Voxel.WorldRendering {

    /// <summary>
    /// Converts a chunk into a format the <see cref="BitFloodfillJob"/> can
    /// handle. See that class for more docs.
    /// </summary>
    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, OptimizeFor = OptimizeFor.Performance)]
    public struct OcclussionFloodfillPrepJob : IJob {

        [ReadOnly]
        public Chunk chunk;

        [WriteOnly]
        public NativeArray<uint> allowsFloodfill;

        [WriteOnly]
        public NativeArray<uint> startingPositionsXPos;
        [WriteOnly]
        public NativeArray<uint> startingPositionsYPos;
        [WriteOnly]
        public NativeArray<uint> startingPositionsZPos;
        [WriteOnly]
        public NativeArray<uint> startingPositionsXNeg;
        [WriteOnly]
        public NativeArray<uint> startingPositionsYNeg;
        [WriteOnly]
        public NativeArray<uint> startingPositionsZNeg;

        public void Execute() {
            int max = chunk.VoxelsPerAxis;

            for (int z = 0; z < max; z++) {
                for (int y = 0; y < max; y++) {
                    // All the `startingPositions{ChunkFace}` are compactified,
                    // so the writeIndex to those and the index we read from
                    // in the chunk differ.
                    int writeIndex = y + max * z;
                    uint val = 0;
                    uint3 startPosLow = 0;
                    uint3 startPosHigh = 0;
                    ref uint startPosXPos = ref startPosHigh.x;
                    ref uint startPosYPos = ref startPosHigh.y;
                    ref uint startPosZPos = ref startPosHigh.z;
                    ref uint startPosXNeg = ref startPosLow.x;
                    ref uint startPosYNeg = ref startPosLow.y;
                    ref uint startPosZNeg = ref startPosLow.z;
                    // Watch out with the whole MSB/LSB part when shifting;
                    // start from the top.
                    for (int x = max - 1; x >= 0; x--) {
                        val <<= 1;
                        startPosLow <<= 1;
                        startPosHigh <<= 1;
                        int readIndex = x + max * writeIndex;
                        ushort mat = chunk.GetRaw(readIndex);
                        val |= mat == 0 ? 1u : 0u;
                        // Also mark the starting positions: all air blocks in
                        // the side of the chunk at a certain face.
                        if (mat == 0) {
                            int3 vec = new(x, y, z);
                            startPosLow |= (uint3)(vec == 0);
                            startPosHigh |= (uint3)(vec == max - 1);
                        }
                    }
                    allowsFloodfill[writeIndex] = val;
                    startingPositionsXPos[writeIndex] = startPosXPos;
                    startingPositionsYPos[writeIndex] = startPosYPos;
                    startingPositionsZPos[writeIndex] = startPosZPos;
                    startingPositionsXNeg[writeIndex] = startPosXNeg;
                    startingPositionsYNeg[writeIndex] = startPosYNeg;
                    startingPositionsZNeg[writeIndex] = startPosZNeg;
                }
            }
        }
    }
}
