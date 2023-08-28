using Atrufulgium.Voxel.Collections;
using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;

namespace Atrufulgium.Voxel.WorldRendering {

    /// <summary>
    /// Provides both instance methods for one mesh job at a time, as well as
    /// static methods for handling many meshings asynchronously.
    /// </summary>
    public class ChunkMesher : IDisposable {

        public const int MAXVERTICES = ushort.MaxValue;
        // Every quad needs to be adjacent to air. The optimum is achieved
        // with well-placed 50% fill-rate. This has 32^3 * 0.5 * 6 quads.
        // However, this breaks down earlier at 28^3 due to the vertex limit.
        // So in effect, it's at most 28^3 * 0.5 * 6
        public const int MAXQUADS = 65856;

        NativeArray<Vertex> vertices = new(MAXVERTICES, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
        NativeArray<ushort> quads = new(MAXQUADS, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
        NativeReference<int> quadsLength = new(Allocator.Persistent);
        NativeParallelHashMap<Vertex, int> vertToIndex = new(MAXVERTICES, Allocator.Persistent);

        /// <summary>
        /// <para>
        /// Turns this chunk into meshes. This runs blocks the thread that
        /// calls this until it is completed.
        /// </para>
        /// <para>
        /// Any face whose normals would be opposite to <paramref name="viewDir"/>
        /// are culled at this step already. Pass the zero-vector to cull nothing.
        /// Note that to use this, you need to not only consider the camera
        /// direction, but also the object's transform.
        /// </para>
        /// </summary>
        /// <param name="chunk">
        /// The chunk to turn into a mesh. Of course, this takes into account
        /// the chunks LoD.
        /// </param>
        /// <param name="viewDir">
        /// Either a normalised vector representing the camera direction in the
        /// chunk's model space, or the zero vector. In the former case, all
        /// invisible faces gets culled, in the latter case no culling happens.
        /// A camera looking at the positive z direction has a viewDir (0,0,1).
        /// </param>
        public Mesh GetMeshSynchronously(Chunk chunk, float3 viewDir = default) {
            GetMeshAsynchonously(chunk, viewDir);
            handle.Complete();
            TryCompleteMeshAsynchronously(out Mesh mesh);
            return mesh;
        }

        bool async = false;
        JobHandle handle;
        GreedyChunkMesherJob meshJob;

        /// <summary>
        /// <para>
        /// Starts converting this chunk into a mesh. The completion can be
        /// polled with <see cref="TryCompleteMeshAsynchronously(out Mesh)"/>.
        /// </para>
        /// <para>
        /// Any face whose normals would be opposite to <paramref name="viewDir"/>
        /// are culled in this step already. Pass the zero-vector to cull nothing.
        /// Note that to use this, you need to not only consider the camera
        /// direction, but also the object's transform.
        /// </para>
        /// </summary>
        /// <inheritdoc cref="GetMeshSynchronously(Chunk, float3)"/>
        public void GetMeshAsynchonously(Chunk chunk, float3 viewDir = default) {
            if (async)
                throw new InvalidOperationException("Cannot use the same ChunkMesher instance for multiple meshing tasks. Use multiple instances. Try using the static ChunkMesher.GetMeshAsynchronously.");

            vertToIndex.Clear();
            meshJob = new GreedyChunkMesherJob {
                chunk = chunk.GetCopy(),
                viewDir = viewDir,
                vertices = vertices,
                quads = quads,
                quadsLength = quadsLength,
                vertToIndex = vertToIndex
            };
            handle = meshJob.Schedule();
            async = true;
        }

        /// <summary>
        /// Polls whether the mesh construction has been completed. If so, puts
        /// the resulting mesh in the out param <paramref name="mesh"/>.
        /// </summary>
        /// <param name="oldMesh">
        /// The existing mesh to overwrite if the process is done.
        /// Heavily prefer to pass something to not generate garbage.
        /// </param>
        public bool TryCompleteMeshAsynchronously(out Mesh mesh, in Mesh oldMesh = null) {
            if (!async)
                throw new InvalidOperationException("Have not started any asynchonous meshing!");
            if (!handle.IsCompleted) {
                mesh = null;
                return false;
            }
            // I don't know *why* a handle.IsCompleted job needs a Complete()
            // call, but it does, so here we are.
            handle.Complete();
            // We were working on a copy.
            meshJob.chunk.Dispose();

            if (oldMesh == null) {
                mesh = new Mesh();
            } else {
                oldMesh.Clear();
                mesh = oldMesh;
            }

            int vertCount = meshJob.vertToIndex.Count();
            int quadCount = meshJob.quadsLength.Value;

            mesh.SetVertexBufferParams(vertCount, Vertex.Layout);
            // (Flag 15 supresses all messages)
            mesh.SetVertexBufferData(meshJob.vertices, 0, 0, vertCount, flags: (MeshUpdateFlags)15);

            mesh.SetIndexBufferParams(quadCount, IndexFormat.UInt16);
            mesh.SetIndexBufferData(meshJob.quads, 0, 0, quadCount, flags: (MeshUpdateFlags)15);

            mesh.subMeshCount = 1;
            // Do note: The docs (<=5.4 already though) note that quads are
            // often emulated. Is this still the case?
            mesh.SetSubMesh(0, new SubMeshDescriptor(0, quadCount, MeshTopology.Quads), flags: (MeshUpdateFlags)15);

            // Settings bounds sends an update mssage
            mesh.bounds = new(
                new(Chunk.ChunkSize / 2, Chunk.ChunkSize / 2, Chunk.ChunkSize / 2),
                new(Chunk.ChunkSize, Chunk.ChunkSize, Chunk.ChunkSize)
            );

            handle = default;
            async = false;
            return true;
        }

        public void Dispose() {
            vertices.Dispose();
            quads.Dispose();
            quadsLength.Dispose();
            vertToIndex.Dispose();
        }

        public static void DisposeStatic() {
            if (activeMeshers.Count > 0) {
                Debug.LogWarning($"There are still {activeMeshers.Count} active meshing jobs. Forcing them to finish before disposing them, but this might take a while!");

                Mesh placeholder = new();

                foreach ((var chunkKey, var chunkMesher) in Enumerators.EnumerateCopy(activeMeshers)) {
                    chunkMesher.handle.Complete();
                    ChunkMesher.TryCompleteMeshAsynchronously(chunkKey, out placeholder, in placeholder);
                }
            }

            while (idleMeshers.Count > 0) {
                idleMeshers.Pop().Dispose();
            }
        }

        static readonly Stack<ChunkMesher> idleMeshers = new();
        static readonly Dictionary<ChunkKey, ChunkMesher> activeMeshers = new();

        /// <inheritdoc cref="GetMeshAsynchonously(Chunk, float3)"/>
        /// <param name="key"> The unique identifier of this meshing job. </param>
        public static void GetMeshAsynchronously(ChunkKey key, Chunk chunk, float3 viewDir = default) {
            if (JobExists(key))
                throw new ArgumentException("The given key already has an associated job, so it cannot have a new one.");

            ChunkMesher mesher;
            if (idleMeshers.Count == 0) {
                mesher = new();
            } else {
                mesher = idleMeshers.Pop();
            }
            mesher.GetMeshAsynchonously(chunk, viewDir);
            activeMeshers.Add(key, mesher);
        }

        /// <inheritdoc cref="TryCompleteMeshAsynchronously(out Mesh, in Mesh)"/>
        /// <param name="key"> The unique identifier of this meshing job. </param>
        public static bool TryCompleteMeshAsynchronously(ChunkKey key, out Mesh mesh, in Mesh oldMesh = null) {
            if (!activeMeshers.TryGetValue(key, out ChunkMesher mesher))
                throw new ArgumentException($"There is no meshing job with ID {key}", nameof(key));

            if (mesher.TryCompleteMeshAsynchronously(out mesh, in oldMesh)) {
                activeMeshers.Remove(key);
                idleMeshers.Push(mesher);
                return true;
            } else {
                return false;
            }
        }

        /// <summary>
        /// Whether or not a given identifier already has an active job.
        /// </summary>
        public static bool JobExists(ChunkKey key)
            => activeMeshers.ContainsKey(key);

        /// <summary>
        /// Iterates through at most all mesh jobs that have been finished.
        /// </summary>
        /// <param name="maxCompletions">
        /// The maximum number of iterations to run.
        /// </param>
        public static IEnumerable<ChunkKey> GetAllCompletedJobs(int maxCompletions = int.MaxValue) {
            int completed = 0;
            foreach ((var key, var mesher) in Enumerators.EnumerateCopy(activeMeshers)) {
                if (mesher.handle.IsCompleted) {
                    yield return key;
                    completed++;
                    if (completed >= maxCompletions)
                        yield break;
                }
            }
        }
    }
}
