using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace Atrufulgium.Voxel.World {

    /// <summary>
    /// Sculpts the basic terrain of the world.
    /// </summary>
    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, OptimizeFor = OptimizeFor.Performance)]
    public struct SculptTerrainJob : IJob {

        [ReadOnly]
        public NativeReference<ChunkKey> key;

        [ReadOnly]
        public NativeReference<Random> random;

        public Chunk chunk;

        float2 heightmapOffset;
        float3 cavesOffset;

        Random rng;

        unsafe public void Execute() {
            int max = chunk.VoxelsPerAxis;
            int3 basePos = key.Value.Worldpos;
            int voxelSize = chunk.VoxelSize;
            ushort* arr = chunk.GetUnsafeUnderlyingPtr();

            rng = random.Value;

            heightmapOffset = rng.NextFloat2(-99999, 99999);
            cavesOffset = rng.NextFloat3(-99999, 99999);

            for (int z = 0; z < max; z++) {
                for (int x = 0; x < max; x++) {
                    int height = (int)(noise.snoise((basePos.xz + new float2(x, z) * voxelSize) * 0.02f + heightmapOffset) * 20) + 20;
                    for (int y = 0; y < max; y++) {
                        int index = x + max * (y + max * z);
                        ushort mat = GetMaterial(basePos + new int3(x, y, z) * voxelSize, height);
                        arr[index] = mat;
                    }
                }
            }
        }

        ushort GetMaterial(int3 pos, int height) {
            ushort mat = 0;
            if (pos.y < height)
                mat = 3;
            noise.snoise((float3)pos * 0.003f + cavesOffset, out float3 gradient);
            gradient *= math.clamp(height * 0.05f, 0.5f, 1f);
            int3 largeGradient = (int3)(math.abs(gradient) > 0.7f);
            if (math.lengthsq(largeGradient) == 1)
                mat = 0;
            return mat;
        }
    }
}