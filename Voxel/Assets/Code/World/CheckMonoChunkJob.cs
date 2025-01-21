using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace Atrufulgium.Voxel.World {

    /// <summary>
    /// Outputs whether the chunk consists of only one material.
    /// </summary>
    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, OptimizeFor = OptimizeFor.Performance)]
    public struct CheckMonoChunkJob : IJob {

        [ReadOnly]
        public RawChunk chunk;

        [WriteOnly]
        public NativeReference<bool> isMonoChunk;

        public void Execute() {
            isMonoChunk.Value = CheckMonoChunk();
        }

        unsafe bool CheckMonoChunk() {
            // As the size is (2^n)^3 for n >= 2, this goes right.
            // We can SIMD 8 at a time.
            // (Could maybe 16 at a time with `double4` but don't know about
            //  the 256-register support.)
            uint4* arr = (uint4*) chunk.GetUnsafeUnderlyingReadOnlyPtr();
            ushort firstMat = (ushort)(arr[0].x & 0xffff);
            uint4 mats = firstMat;
            mats |= (mats << 16);

            int max = chunk.VoxelsPerAxis;
            max = max * max * max / 8;
            for (int i = 0; i < max; i++) {
                if (math.any(arr[i] != mats))
                    return false;
            }
            return true;
        }
    }
}