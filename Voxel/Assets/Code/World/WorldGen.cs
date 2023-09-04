using Atrufulgium.Voxel.Base;
using Unity.Collections;
using Unity.Mathematics;

namespace Atrufulgium.Voxel.World {
    public class WorldGen : KeyedJobManager<
        /* key */ ChunkKey,
        /* job */ SculptTerrainJob,
        /* job */ CheckMonoChunkJob,
        /* in  */ (ChunkKey key, uint seed),
        /* out */ GameWorld
    > {
        NativeReference<ChunkKey> key = new(Allocator.Persistent);
        NativeReference<Random> rng = new(new(1), Allocator.Persistent);
        Chunk chunk = default;
        NativeReference<bool> isOnlyAir = new(Allocator.Persistent);

        unsafe public override void Setup((ChunkKey key, uint seed) input, out SculptTerrainJob job1, out CheckMonoChunkJob job2) {
            if (!Reused) {
                chunk = new(1);
            } else {
                chunk.Clear();
            }

            key.Value = input.key;
            if (input.seed == 0)
                input.seed = 0x12345678;
            rng.Value = new(input.seed);

            job1 = new() {
                key = key,
                chunk = chunk,
                random = rng
            };

            job2 = new() {
                chunk = chunk,
                isMonoChunk = isOnlyAir
            };
        }

        public override void PostProcess(ref GameWorld world, in SculptTerrainJob job1, in CheckMonoChunkJob job2) {
            // Mono chunks can be super low LoD, they're one material only.
            // (This will usually be air)
            // The lowest we support is 4 voxels per direction.
            if (!job2.isMonoChunk.Value)
                world.SetChunk(job1.key.Value, job1.chunk.GetCopy());
            else
                world.SetChunk(job1.key.Value, job1.chunk.WithLoD(3));
        }

        public override void Dispose() {
            key.Dispose();
            rng.Dispose();
            chunk.Dispose();
            isOnlyAir.Dispose();
            base.Dispose();
        }
    }
}