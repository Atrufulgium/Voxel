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
    /// </summary>
    public class ChunkMesher : KeyedJobManager<
        /* key */ ChunkKey,
        /* job */ GreedyChunkMesherJob,
        /* in  */ (Chunk chunk, float3 viewDir),
        /* out */ Mesh
    > {

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
        NativeReference<int> quadsLength = new(Allocator.Persistent);
        NativeArray<GreedyChunkMesherJob.VertToIndexEntry> vertToIndex = new(TABLECAPACITY, Allocator.Persistent);

        public override void Setup((Chunk chunk, float3 viewDir) input, out GreedyChunkMesherJob job) {
            var chunk = input.chunk;
            var viewDir = input.viewDir;

            job = new GreedyChunkMesherJob {
                chunk = chunk.GetCopy(),
                viewDir = viewDir,
                vertices = vertices,
                verticesLength = verticesLength,
                quads = quads,
                quadsLength = quadsLength,
                vertToIndex = vertToIndex
            };
        }

        public override void PostProcess(ref Mesh result, in GreedyChunkMesherJob job) {
            if (result == null) {
                result = new Mesh();
            } else {
                result.Clear();
            }

            int vertCount = job.verticesLength.Value;
            int quadCount = job.quadsLength.Value;

            // Prepare next job already.
            job.chunk.Dispose();
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
            result.SetVertexBufferData(job.vertices, 0, 0, vertCount, flags: (MeshUpdateFlags)15);

            result.SetIndexBufferParams(quadCount, IndexFormat.UInt16);
            result.SetIndexBufferData(job.quads, 0, 0, quadCount, flags: (MeshUpdateFlags)15);

            result.subMeshCount = 1;
            // Do note: The docs (<=5.4 already though) note that quads are
            // often emulated. Is this still the case?
            result.SetSubMesh(0, new SubMeshDescriptor(0, quadCount, MeshTopology.Quads), flags: (MeshUpdateFlags)15);

            // Settings bounds sends an update mssage
            result.bounds = new(
                new(Chunk.ChunkSize / 2, Chunk.ChunkSize / 2, Chunk.ChunkSize / 2),
                new(Chunk.ChunkSize, Chunk.ChunkSize, Chunk.ChunkSize)
            );
        }

        public override void Dispose() {
            vertices.Dispose();
            verticesLength.Dispose();
            quads.Dispose();
            quadsLength.Dispose();
            vertToIndex.Dispose();
            base.Dispose();
        }
    }
}
