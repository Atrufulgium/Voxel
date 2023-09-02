using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;

namespace Atrufulgium.Voxel.WorldRendering {
    /// <summary>
    /// Given the results of <see cref="BitFloodfillJob"/>, actually creates
    /// the <see cref="ChunkVisibility"/>.
    /// </summary>
    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, OptimizeFor = OptimizeFor.Performance)]
    public struct OcclussionFloodfillPostJob : IJob {

        [ReadOnly]
        public NativeArray<uint> resultXPos;

        [ReadOnly]
        public NativeArray<uint> resultYPos;

        [ReadOnly]
        public NativeArray<uint> resultZPos;

        [ReadOnly]
        public NativeArray<uint> resultXNeg;

        [ReadOnly]
        public NativeArray<uint> resultYNeg;

        [ReadOnly]
        public NativeArray<uint> resultZNeg;

        [ReadOnly]
        public NativeReference<int> maxIndex;

        [WriteOnly]
        public NativeReference<ChunkVisibility> seen;

        public unsafe void Execute() {
            ChunkVisibility seen = HandleFace(resultXPos, ChunkFace.XPos);
            seen |= HandleFace(resultYPos, ChunkFace.YPos);
            seen |= HandleFace(resultZPos, ChunkFace.ZPos);
            seen |= HandleFace(resultXNeg, ChunkFace.XNeg);
            seen |= HandleFace(resultYNeg, ChunkFace.YNeg);
            seen |= HandleFace(resultZNeg, ChunkFace.ZNeg);

            this.seen.Value = seen;
        }

        unsafe ChunkVisibility HandleFace(NativeArray<uint> result, ChunkFace startingFace) {
            int maxZ = maxIndex.Value;
            int maxY = maxZ / 4;
            uint right = 1u << (maxZ - 1);
            ChunkVisibility seen = ChunkVisibility.None;
            // This can easily be done four at a time.
            uint4* result4 = (uint4*)result.GetUnsafeReadOnlyPtr();
            for (int z = 0; z < maxZ; z++) {
                for (int y = 0; y < maxY; y++) {
                    uint4 val = result4[y + maxY * z];
                    // These case distinctions are annoying, but it's what I
                    // get for doing 128 bits at a time.
                    if (math.any((val & 1) > 0))
                        seen.SetVisible(startingFace, ChunkFace.XNeg, true);
                    if (math.any((val & right) > 0))
                        seen.SetVisible(startingFace, ChunkFace.XPos, true);
                    if (y == 0 && val.x > 0)
                        seen.SetVisible(startingFace, ChunkFace.YNeg, true);
                    if (y == maxY - 1 && val.w > 0)
                        seen.SetVisible(startingFace, ChunkFace.YPos, true);
                    if (z == 0 && math.any(val > 0))
                        seen.SetVisible(startingFace, ChunkFace.ZNeg, true);
                    if (z == maxZ - 1 && math.any(val > 0))
                        seen.SetVisible(startingFace, ChunkFace.ZPos, true);
                }
            }
            return seen;
        }
    }
}
