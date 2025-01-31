using Atrufulgium.Voxel.Base;
using Unity.Collections;
using Unity.Mathematics;

namespace Atrufulgium.Voxel.World {
    public class WorldGen : KeyedJobManager<
        /* key */ ChunkKey,
        /* job */ SculptTerrainJob,
        /* job */ CompressChunkJob,
        /* in  */ (ChunkKey key, uint seed),
        /* out */ GameWorld
    > {
        NativeReference<ChunkKey> key = new(Allocator.Persistent);
        NativeReference<Random> rng = new(new(1), Allocator.Persistent);
        RawChunk chunk = default;
        RLEChunk compressed = default;
        NativeReference<bool> isOnlyAir = new(Allocator.Persistent);

        unsafe public override void Setup(
            (ChunkKey key, uint seed) input,
            out SculptTerrainJob job1,
            out CompressChunkJob job2
        ) {
            if (!Reused) {
                chunk = new(0);
                compressed = new(0);
            } else {
                chunk.Clear();
                compressed.Clear();
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
                decompressed = chunk,
                compressed = compressed
            };
        }

        public override void PostProcess(
            ref GameWorld world,
            in SculptTerrainJob job1,
            in CompressChunkJob job2
        ) {
            world.SetChunk(job1.key.Value, job2.compressed.GetCopy());
        }

        public override void Dispose() {
            key.Dispose();
            rng.Dispose();
            chunk.Dispose();
            compressed.Dispose();
            isOnlyAir.Dispose();
            base.Dispose();
        }
    }
}