using Atrufulgium.Voxel.Base;
using Atrufulgium.Voxel.World;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;

namespace Atrufulgium.Voxel.WorldRendering {

    /// <summary>
    /// Provides both instance methods for one mesh job at a time, as well as
    /// static methods for handling many meshings asynchronously.
    /// <br/>
    /// The resulting mesh has 6 submeshes. Each of these submeshes face only
    /// one direction. In order, these normals are:
    /// <c>(-1, 0, 0,)</c>, <c>(1, 0, 0)</c>, <c>(0, -1, 0)</c>, etc.
    /// </summary>
    public class ChunkMesher : KeyedJobManager<
        /* key */ ChunkKey,
        /* job */ DecompressChunkJob,
        /* job */ GreedyChunkMesherJob,
        /* in  */ (RLEChunk chunk, float3 viewDir),
        /* out */ Mesh
    > {

        static int MemoryFootprint = 0;

        public const int MAXVERTICES = ushort.MaxValue;
        // Every quad needs to be adjacent to air. The optimum is achieved
        // with well-placed 50% fill-rate. This has 32^3 * 0.5 * 6 quads.
        // However, this breaks down earlier at 28^3 due to the vertex limit.
        // So in effect, it's at most 28^3 * 0.5 * 6
        public const int MAXQUADS = 65856;

        // We're already ruining the cache anyway. Factor *2 to keep occupancy
        // below 50% to keep insertion clean.
        public const int TABLECAPACITY = MAXVERTICES * 2;

        NativeArray<Vertex> vertices = new(MAXVERTICES, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
        NativeReference<int> verticesLength = new(Allocator.Persistent);
        NativeArray<ushort> quads = new(MAXQUADS, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
        NativeArray<int> quadsLengths = new(6, Allocator.Persistent);
        NativeArray<GreedyChunkMesherJob.VertToIndexEntry> vertToIndex = new(TABLECAPACITY, Allocator.Persistent);

        public override void Setup(
            (RLEChunk chunk, float3 viewDir) input,
            out DecompressChunkJob job1,
            out GreedyChunkMesherJob job2
        ) {
            RLEChunk compressed = input.chunk.GetCopy();
            RawChunk decompressed = new(0);
            var viewDir = input.viewDir;

            job1 = new DecompressChunkJob {
                compressed = compressed,
                decompressed = decompressed
            };

            job2 = new GreedyChunkMesherJob {
                chunk = decompressed,
                viewDir = viewDir,
                vertices = vertices,
                verticesLength = verticesLength,
                quads = quads,
                quadsLength = quadsLengths,
                vertToIndex = vertToIndex
            };
        }

        public override void PostProcess(
            ref Mesh result,
            in DecompressChunkJob job1,
            in GreedyChunkMesherJob job2
        ) {
            if (result == null) {
                result = new Mesh();
            } else {
                result.Clear();
            }

            if (result.subMeshCount != 6)
                result.subMeshCount = 6;

            int vertCount = job2.verticesLength.Value;
            var quadCounts = job2.quadsLength;
            int quadCount = quadCounts[5];

            // Get rid of temps
            job1.compressed.Dispose();
            job1.decompressed.Dispose();

            // Unfortunately we actually need to clear the entire table for
            // next time as the entries are basically distributed randomly.
            // For small vertCounts we can be a little tricksier.
            if (vertCount < 1000) {
                // Needs to be done backwards.
                for (int i = vertCount - 1; i >= 0; i--) {
                    GreedyChunkMesherJob.VertToIndexRemove(vertices[i], vertToIndex);
                }
            } else {
                vertToIndex.Clear();
            }
            
            // Back to actually doing output.
            result.SetVertexBufferParams(vertCount, Vertex.Layout);
            // (Flag 15 supresses all messages)
            result.SetVertexBufferData(job2.vertices, 0, 0, vertCount, flags: (MeshUpdateFlags)15);

            int memoryDelta = vertCount * sizeof(uint);
            memoryDelta += quadCount * sizeof(uint);
            MemoryFootprint += memoryDelta;
            Debug.Log($"Mesh Memory: {MemoryFootprint} bytes (+{memoryDelta})");

            result.SetIndexBufferParams(quadCount, IndexFormat.UInt16);
            result.SetIndexBufferData(job2.quads, 0, 0, quadCount, flags: (MeshUpdateFlags)15);

            var bounds = new Bounds(
                new(RawChunk.ChunkSize / 2, RawChunk.ChunkSize / 2, RawChunk.ChunkSize / 2),
                new(RawChunk.ChunkSize, RawChunk.ChunkSize, RawChunk.ChunkSize)
            );

            result.bounds = bounds;

            // Do note: The docs (<=5.4 already though) note that quads are
            // often emulated. Is this still the case?
            for (int i = 0; i < 6; i++) {
                int start = (i == 0) ? 0 : quadCounts[i - 1];
                int count = quadCounts[i] - start;
                result.SetSubMesh(
                    i,
                    new SubMeshDescriptor(start, count, MeshTopology.Quads) { bounds=bounds },
                    flags: (MeshUpdateFlags)15
                );
            }
        }

        public override void Dispose() {
            vertices.Dispose();
            verticesLength.Dispose();
            quads.Dispose();
            quadsLengths.Dispose();
            vertToIndex.Dispose();
            base.Dispose();
        }
    }
}
