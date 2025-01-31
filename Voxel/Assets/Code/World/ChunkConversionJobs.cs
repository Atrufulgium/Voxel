using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;

namespace Atrufulgium.Voxel.World {

    /// <summary>
    /// Converts a <see cref="RLEChunk"/> to a <see cref="RawChunk"/>.
    /// <br/>
    /// The caller of this job is responsible for disposing <see cref="decompressed"/>.
    /// </summary>
    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, OptimizeFor = OptimizeFor.Performance)]
    public struct DecompressChunkJob : IJob {

        [ReadOnly]
        public RLEChunk compressed;

        [WriteOnly]
        public RawChunk decompressed;

        public void Execute() {
            decompressed.SetFromRLEChunk(compressed);
        }
    }

    /// <summary>
    /// Converts a <see cref="RawChunk"/> to a <see cref="RLEChunk"/>.
    /// <br/>
    /// The caller of this job is responsible for disposing <see cref="compressed"/>.
    /// </summary>
    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, OptimizeFor = OptimizeFor.Performance)]
    public struct CompressChunkJob : IJob {

        [ReadOnly]
        public RawChunk decompressed;

        [WriteOnly]
        public RLEChunk compressed;

        public void Execute() {
            compressed.SetFromRawChunk(decompressed);
        }
    }
}