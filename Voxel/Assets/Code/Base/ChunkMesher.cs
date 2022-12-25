using Atrufulgium.Voxel.Collections;
using System;
using System.Collections.Generic;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;

namespace Atrufulgium.Voxel.Base {

    /// <summary>
    /// Provides both instance methods for one mesh job at a time, as well as
    /// static methods for handling many meshings asynchronously.
    /// </summary>
    public class ChunkMesher : IDisposable {

        NativeList<Vertex> vertices = new(2 * (ushort.MaxValue + 1), Allocator.Persistent);
        NativeList<ushort> quads = new(ushort.MaxValue + 1, Allocator.Persistent);
        NativeParallelHashMap<Vertex, int> vertToIndex = new(ushort.MaxValue + 1, Allocator.Persistent);
        NativeParallelHashMap<RectMaterialTuple, RectMaterialTuple> rects = new(64, Allocator.Persistent);

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
        // TODO: proper impl and tl;dr of https://doi.org/10.1137/0402027
        // Note that we also have a "don't care" region in nearly all planes as
        // we don't care what covered voxels do. Taking into account OPT in
        // this case seems nearly impossible.
        public Mesh GetMeshSynchronously(Chunk chunk, float3 viewDir = default) {
            GetMeshAsynchonously(chunk, viewDir);
            handle.Complete();
            TryCompleteMeshAsynchronously(out Mesh mesh);
            return mesh;
        }

        bool async = false;
        JobHandle handle;
        ChunkMesherJob meshJob;

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
                throw new InvalidOperationException("Cannot use the same ChunkMesher instance for multiple meshing tasks. Use multiple instances.");

            vertices.Clear();
            vertToIndex.Clear();
            quads.Clear();
            meshJob = new ChunkMesherJob {
                // Work on a copy to not have issues with race conditions.
                // However, we need to also dispose this copy of course!
                chunk = chunk.GetCopy(),
                viewDir = viewDir,
                vertices = vertices,
                quads = quads,
                vertToIndex = vertToIndex,
                rects = rects
            };
            handle = meshJob.Schedule();
            async = true;
        }

        /// <summary>
        /// Polls whether the mesh construction has been completed. If so, puts
        /// the resulting mesh in the out param <paramref name="mesh"/>.
        /// </summary>
        public bool TryCompleteMeshAsynchronously(out Mesh mesh) {
            if (!async)
                throw new ArgumentException("Have not started any asynchonous meshing!");
            if (!handle.IsCompleted) {
                mesh = null;
                return false;
            }
            // I don't know *why* a handle.IsCompleted job needs a Complete()
            // call, but it does, so here we are.
            handle.Complete();
            // We were working on a copy.
            meshJob.chunk.Dispose();

            mesh = new Mesh();
            mesh.SetVertexBufferParams(meshJob.vertices.Length, Vertex.Layout);
            // (Flag 15 supresses all messages)
            mesh.SetVertexBufferData(meshJob.vertices.AsArray(), 0, 0, meshJob.vertices.Length, flags: (MeshUpdateFlags)15);

            mesh.SetIndexBufferParams(meshJob.quads.Length, IndexFormat.UInt16);
            mesh.SetIndexBufferData(meshJob.quads.AsArray(), 0, 0, meshJob.quads.Length, flags: (MeshUpdateFlags)15);

            mesh.subMeshCount = 1;
            // Do note: The docs (<=5.4 already though) note that quads are
            // often emulated. Is this still the case?
            mesh.SetSubMesh(0, new SubMeshDescriptor(0, meshJob.quads.Length, MeshTopology.Quads), flags: (MeshUpdateFlags)15);

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
            vertToIndex.Dispose();
            rects.Dispose();
        }

        public static void DisposeStatic() {
            bool dangerous = activeMeshers.Count > 0;

            while (idleMeshers.Count > 0) {
                idleMeshers.Pop().Dispose();
            }
            foreach((var key, var chunkMesher) in Enumerators.EnumerateCopy(activeMeshers)) {
                chunkMesher.Dispose();
            }

            if (dangerous)
                throw new InvalidOperationException("There were still active jobs. Disposed them, but everything will probably go wrong!");
        }

        static Stack<ChunkMesher> idleMeshers = new();
        static Dictionary<ChunkKey, ChunkMesher> activeMeshers = new();

        /// <inheritdoc cref="GetMeshAsynchonously(Chunk, float3)"/>
        /// <param name="key"> The unique identifier of this meshing job. </param>
        public static void GetMeshAsynchronously(ChunkKey key, Chunk chunk, float3 viewDir = default) {
            ChunkMesher mesher;
            if (idleMeshers.Count == 0) {
                mesher = new();
            } else {
                mesher = idleMeshers.Pop();
            }
            mesher.GetMeshAsynchonously(chunk, viewDir);
            activeMeshers.Add(key, mesher);
        }

        /// <inheritdoc cref="TryCompleteMeshAsynchronously(out Mesh)"/>
        /// <param name="key"> The unique identifier of this meshing job. </param>
        public static bool TryCompleteMeshAsynchronously(ChunkKey key, out Mesh mesh) {
            if (!activeMeshers.TryGetValue(key, out ChunkMesher mesher))
                throw new ArgumentException($"There is no meshing job with ID {key}", nameof(key));

            if (mesher.TryCompleteMeshAsynchronously(out mesh)) {
                // A bit awkward, but both this and
                /// <see cref="GetAllCompletedMeshes(int)"/>
                // have these two lines.
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
        public static IEnumerable<(ChunkKey key, Mesh mesh)> GetAllCompletedMeshes(int maxCompletions = int.MaxValue) {
            int completed = 0;
            foreach ((var key, var mesher) in Enumerators.EnumerateCopy(activeMeshers)) {
                if (mesher.TryCompleteMeshAsynchronously(out Mesh mesh)) {
                    // A bit awkward, but both this and
                    /// <see cref="TryCompleteMeshAsynchronously(ChunkKey, out Mesh)"/>
                    // have these two lines.
                    activeMeshers.Remove(key);
                    idleMeshers.Push(mesher);
                    yield return (key, mesh);

                    completed++;
                    if (completed >= maxCompletions)
                        yield break;
                }
            }
        }
    }

    [BurstCompile(CompileSynchronously = true)]
    struct ChunkMesherJob : IJob {

        /// <summary>
        /// The chunk to turn into a mesh. Of course, this takes into account
        /// the chunks LoD.
        /// </summary>
        [ReadOnly]
        internal Chunk chunk;
        /// <summary>
        /// Either a normalised vector representing the camera direction in the
        /// chunk's model space, or the zero vector. In the former case, all
        /// invisible faces gets culled, in the latter case no culling happens.
        /// A camera looking at the positive z direction has a viewDir (0,0,1).
        /// </summary>
        [ReadOnly]
        internal float3 viewDir;

        /// <summary>
        /// All verts in the current GetMesh call.
        /// </summary>
        [WriteOnly]
        internal NativeList<Vertex> vertices;
        /// <summary>
        /// I'd call it "tris" if my topology wasn't quads. The indices of the
        /// four corners of quads inside the vertices list in the current
        /// GetMesh call.
        /// </summary>
        /// <remarks>
        /// ushorts are *not* sufficient. You can construct a 28x28x28 3d
        /// checkerboard pattern of "air / non-air" with no two diagonally
        /// touching non-air blocks of the same material. However, this
        /// requires 11k well-placed blocks (in a "place two break one" way)
        /// out of the maximum of 16k blocks that can induce 6 verts.
        /// Anyone who achieves that *deserves* the broken physics and
        /// graphics they desire.
        /// </remarks>
        [WriteOnly]
        internal NativeList<ushort> quads;

        /// <summary>
        /// A conversion from vertex to index inside the vertices list in the
        /// current GetMesh call.
        /// </summary>
        internal NativeParallelHashMap<Vertex, int> vertToIndex;
        /// <summary>
        /// The rects of the current GetMeshFromDirection layer.
        /// </summary>
        // This would be so much better with an interval tree.
        // I *will* implement those someday for RLE, so I guess,
        // TODO: replace with interval tree.
        internal NativeParallelHashMap<RectMaterialTuple, RectMaterialTuple> rects;

        /// <summary>
        /// In order to keep <see cref="vertices"/> write-only, keep track of
        /// its size separately. Ew.
        /// </summary>
        int vertexCount;

        static readonly LayerMode[] renderDirections = new[] { LayerMode.X, LayerMode.Y, LayerMode.Z };
        static readonly bool[] allBools = new[] { true, false };

        // TODO: proper impl and tl;dr of https://doi.org/10.1137/0402027
        // Note that we also have a "don't care" region in nearly all planes as
        // we don't care what covered voxels do. Taking into account OPT in
        // this case seems nearly impossible.
        public void Execute() {
            vertexCount = 0;
            for (int i = 0; i < 3; i++) {
                for (int ii = 0; ii < 2; ii++) {
                    var layerMode = renderDirections[i];
                    var backside = allBools[ii];
                    // TODO: This is probably incorrect for the same reason as on
                    // the GPU side - it doesn't take into account the perspective
                    // transformation.
                    int3 normal = LayerToCoord(0, 0, 1, layerMode);
                    if (!backside)
                        normal *= -1;
                    if (math.dot(viewDir, normal) >= 0)
                        GetMeshFromDirection(layerMode, backside);
                }
            }
        }

        private unsafe void GetMeshFromDirection(
            LayerMode layerMode,
            bool backside
        ) {
            int voxelSize = chunk.VoxelSize;
            int x, y, layer;
            for (layer = 0; layer < Chunk.ChunkSize; layer += voxelSize) {
                // Considering a single XY-plane, first partition into vertical
                // rectangles. Rectangles are allowed to go under other voxels
                // in another layer. Grow those rectangles up if possible.
                rects.Clear();
                for (y = 0; y < Chunk.ChunkSize; y += voxelSize) {
                    RectInt current = default;
                    ushort currentMat = 0;
                    for (x = 0; x < Chunk.ChunkSize + 1; x += voxelSize) {
                        // We're at the end of the chunk and can't do anything
                        // but add a possible WIP rect.
                        bool final = x == Chunk.ChunkSize;
                        // We're different and need to change what we're doing.
                        bool different = false;
                        ushort mat = 0;
                        if (!final) {
                            mat = chunk[LayerToCoord(x, y, layer, layerMode)];
                            different = mat != currentMat;
                        }
                        // We're covered by the previous layer and can do whatever
                        bool covered;
                        if (backside) {
                            covered = !final && layer > 0 && chunk[LayerToCoord(x, y, layer - voxelSize, layerMode)] != 0;
                        } else {
                            covered = !final && layer + voxelSize < Chunk.ChunkSize && chunk[LayerToCoord(x, y, layer + voxelSize, layerMode)] != 0;
                        }

                        // Commit the previous on differences. This automatically
                        // commits changes on x=32 as that's always "air".
                        if ((different && !covered || final) && currentMat != 0) {
                            // We're no longer available.
                            current.width = x - current.xMin;
                            var key = new RectMaterialTuple(current, currentMat);
                            if (rects.ContainsKey(key)) {
                                // As noted below, the key's equality is not
                                // transitive. This is an ugly hack, but this
                                // code *needs* it to be somewhat decent still.
                                // So we need to actually pop the (key,value)-
                                // pair and insert one that *looks* the same.
                                // Yes, this is massive dict-abuse.
                                var old = rects[key].rect;
                                rects.Remove(key);
                                old.height = current.yMin + voxelSize - old.yMin;
                                key.rect = old;
                            }
                            rects.Add(key, key);
                            current = default;
                            currentMat = 0;
                        }
                        // We're different and not air, so we start a new rect.
                        if (different && !covered && currentMat == 0) {
                            current = new(x, y, 0, voxelSize);
                            currentMat = mat;
                        }
                    } // for x
                } // for y

                // Then turn those rectangles into quads.
                foreach (var kvpair in rects) {
                    var rect = kvpair.Value.rect;
                    var mat = kvpair.Value.mat;
                    int2* corners = stackalloc int2[4];
                    if (backside) {
                        corners[0] = new(rect.xMin, rect.yMin);
                        corners[1] = new(rect.xMin, rect.yMax);
                        corners[2] = new(rect.xMax, rect.yMax);
                        corners[3] = new(rect.xMax, rect.yMin);
                    } else {
                        corners[0] = new(rect.xMax, rect.yMin);
                        corners[1] = new(rect.xMax, rect.yMax);
                        corners[2] = new(rect.xMin, rect.yMax);
                        corners[3] = new(rect.xMin, rect.yMin);
                    }

                    for (int ci = 0; ci < 4; ci++) {
                        int2 corner = corners[ci];
                        Vertex vert;

                        int z = layer;
                        if (!backside) {
                            z += voxelSize;
                        }
                        vert = new(LayerToCoord(corner.x, corner.y, z, layerMode), mat);

                        if (!vertToIndex.TryGetValue(vert, out int index)) {
                            index = vertexCount;
                            vertexCount++;
                            vertices.Add(vert);
                        }
                        quads.Add((ushort)index);
                    }
                } // for rect
            } // for layer
        }

        /// <summary>
        /// Whether the X-, Y-, or Z-direction is constant.
        /// </summary>
        enum LayerMode { X, Y, Z };

        private int3 LayerToCoord(int x, int y, int layer, LayerMode layerMode) {
            if (layerMode == LayerMode.X)
                return new(x, y, layer);
            else if (layerMode == LayerMode.Y)
                return new(y, layer, x);
            return new(layer, x, y);
        }
    }

    /// <summary>
    /// <para>
    /// The comparer only cares about (xMin, width), and the two being
    /// vertically apart by at most one. Also, disregards any different
    /// two material rects.
    /// </para>
    /// <para>
    /// Note!!! This equality is NOT transitive! This breaks a bunch of
    /// shit you usually wouldn't think about.
    /// </para>
    /// </summary>
    internal struct RectMaterialTuple : IEquatable<RectMaterialTuple> {
        public RectInt rect;
        public ushort mat;

        public RectMaterialTuple(RectInt rect, ushort mat) {
            this.rect = rect;
            this.mat = mat;
        }

        bool IEquatable<RectMaterialTuple>.Equals(RectMaterialTuple other)
            => mat == other.mat
            && rect.xMin == other.rect.xMin && rect.width == other.rect.width
            && (rect.yMin == other.rect.yMax || rect.yMax == other.rect.yMin);

        public override bool Equals(object obj)
            => obj is RectMaterialTuple other && ((IEquatable<RectMaterialTuple>)this).Equals(other);

        public override int GetHashCode()
            => rect.xMax + 33 * rect.width + 33 * 33 * mat;
    }

    /// <summary>
    /// Turns a (x,y,z,material) into a single uint, by storing in the
    /// first three factors 33 the coordinate positions, and in the
    /// remaining [0,119513]-range the material.
    /// </summary>
    internal struct Vertex : IEquatable<Vertex> {
        public uint data;

        /// <summary>
        /// The `pos` vector should actually be integer with values between
        /// 0 and 32 inclusive. The material should be [0,119513].
        /// </summary>
        public Vertex(float3 pos, ushort material) {
            data = (uint)pos.x
                + 33u * (uint)pos.y
                + 33u * 33u * (uint)pos.z
                + 33u * 33u * 33u * material;
        }

        bool IEquatable<Vertex>.Equals(Vertex other)
            => data == other.data;

        public override int GetHashCode()
            => (int)data;

        internal static readonly VertexAttributeDescriptor[] Layout = new VertexAttributeDescriptor[] {
            new VertexAttributeDescriptor(VertexAttribute.BlendIndices, VertexAttributeFormat.UInt32, 1)
        };
    }
}
