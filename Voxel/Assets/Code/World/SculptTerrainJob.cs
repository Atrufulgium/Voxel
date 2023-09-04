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

        Random rng;

        unsafe public void Execute() {
            int max = chunk.VoxelsPerAxis;
            int3 basePos = key.Value.Worldpos;
            int voxelSize = chunk.VoxelSize;
            ushort* arr = chunk.GetUnsafeUnderlyingPtr();

            rng = random.Value;

            heightmapOffset = rng.NextFloat2(-99999, 99999);

            for (int z = 0; z < max; z++) {
                for (int y = 0; y < max; y++) {
                    for (int x = 0; x < max; x++) {
                        int index = x + max * (y + max * z);
                        ushort mat = GetMaterial(basePos + new int3(x, y, z) * voxelSize);
                        arr[index] = mat;
                    }
                }
            }
        }

        ushort GetMaterial(int3 pos) {
            int height = (int)(noise.snoise((float2)pos.xz * 0.02f + heightmapOffset) * 20);
            if (pos.y < height)
                return 3;
            return 0;
        }
    }
}