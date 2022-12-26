using System;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;

namespace Atrufulgium.Voxel.Base {

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, OptimizeFor = OptimizeFor.Performance)]
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
        internal NativeParallelHashMap<RectMaterialLayerTuple, RectMaterialLayerTuple> rects;

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
            // Four layers at a time.
            // *Perhaps* the y-values can also be partially SIMD'd if careful.
            // For the x values this is unreasonable though.
            // As the SIMD can only handle "four at a time" though, this is
            // sufficient, as otherwise it would be 16 at a time if multiple.
            int x, y;
            int4 layers = new int4(0, 1, 2, 3) * voxelSize;
            for (; layers.w < Chunk.ChunkSize; layers += 4 * voxelSize) {
                // Considering a single XY-plane, first partition into vertical
                // rectangles. Rectangles are allowed to go under other voxels
                // in another layer. Grow those rectangles up if possible.
                rects.Clear();
                for (y = 0; y < Chunk.ChunkSize; y += voxelSize) {
                    RectInt* current = stackalloc RectInt[4];
                    int4 currentMat = 0;
                    for (x = 0; x < Chunk.ChunkSize + 1; x += voxelSize) {
                        // We're at the end of the chunk and can't do anything
                        // but add a possible WIP rect.
                        bool final = x == Chunk.ChunkSize;
                        // We're different and need to change what we're doing.
                        bool4 different = false;
                        int4 mat = 0;
                        if (!final) {
                            LayerToCoord(x, y, layers, layerMode, out int4 xOut, out int4 yOut, out int4 zOut);
                            mat = chunk[xOut, yOut, zOut];
                            different = mat != currentMat;
                        }
                        // We're covered by the previous layer and can do whatever
                        bool4 covered = false;
                        if (!final) {
                            bool4 check;
                            int4 xOut, yOut, zOut;
                            if (backside) {
                                LayerToCoord(x, y, layers - voxelSize, layerMode, out xOut, out yOut, out zOut);
                                check = layers > 0;
                            } else {
                                LayerToCoord(x, y, layers + voxelSize, layerMode, out xOut, out yOut, out zOut);
                                check = layers + voxelSize < Chunk.ChunkSize;
                            }
                            xOut *= (int4)check;
                            yOut *= (int4)check;
                            zOut *= (int4)check;
                            covered = check & chunk[xOut, yOut, zOut] != 0;
                        }

                        // This unfortunately needs to be sequential instead
                        // of SIMD because of branching.
                        for (int i = 0; i < 4; i++) {
                            // Commit the previous on differences. This automatically
                            // commits changes on x=32 as that's always "air".
                            if ((different[i] && !covered[i] || final) && currentMat[i] != 0) {
                                // We're no longer available.
                                current[i].width = x - current[i].xMin;
                                var key = new RectMaterialLayerTuple(current[i], (ushort)layers[i], (ushort)currentMat[i]);
                                if (rects.ContainsKey(key)) {
                                    // As noted below, the key's equality is not
                                    // transitive. This is an ugly hack, but this
                                    // code *needs* it to be somewhat decent still.
                                    // So we need to actually pop the (key,value)-
                                    // pair and insert one that *looks* the same.
                                    // Yes, this is massive dict-abuse.
                                    var old = rects[key].rect;
                                    rects.Remove(key);
                                    old.height = current[i].yMin + voxelSize - old.yMin;
                                    key.rect = old;
                                }
                                rects.Add(key, key);
                                current[i] = default;
                                currentMat[i] = 0;
                            }
                            // We're different and not air, so we start a new rect.
                            if (different[i] && !covered[i] && currentMat[i] == 0) {
                                current[i] = new(x, y, 0, voxelSize);
                                currentMat[i] = mat[i];
                            }
                        } // for i
                    } // for x
                } // for y

                // Then turn those rectangles into quads.
                foreach (var kvpair in rects) {
                    var rect = kvpair.Value.rect;
                    var currentLayer = kvpair.Value.layer;
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

                        int z = currentLayer;
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

        // A variant of the above for four coords.
        private void LayerToCoord(int x, int y, int4 layer, LayerMode layerMode, out int4 xOut, out int4 yOut, out int4 zOut) {
            if (layerMode == LayerMode.X) {
                xOut = x;
                yOut = y;
                zOut = layer;
            } else if (layerMode == LayerMode.Y) {
                xOut = y;
                yOut = layer;
                zOut = x;
            } else {
                xOut = layer;
                yOut = x;
                zOut = y;
            }
        }
    }

    /// <summary>
    /// <para>
    /// The comparer only cares about (xMin, width), and the two being
    /// vertically apart by at most one. Also, disregards any different
    /// two material rects. Also, two different layers are always different.
    /// </para>
    /// <para>
    /// Note!!! This equality is NOT transitive! This breaks a bunch of
    /// shit you usually wouldn't think about.
    /// </para>
    /// </summary>
    internal struct RectMaterialLayerTuple : IEquatable<RectMaterialLayerTuple> {
        public RectInt rect;
        public ushort layer;
        public ushort mat;

        public RectMaterialLayerTuple(RectInt rect, ushort layer, ushort mat) {
            this.rect = rect;
            this.layer = layer;
            this.mat = mat;
        }

        bool IEquatable<RectMaterialLayerTuple>.Equals(RectMaterialLayerTuple other)
            => layer == other.layer
            && mat == other.mat
            && rect.xMin == other.rect.xMin && rect.width == other.rect.width
            && (rect.yMin == other.rect.yMax || rect.yMax == other.rect.yMin);

        public override bool Equals(object obj)
            => obj is RectMaterialLayerTuple other && ((IEquatable<RectMaterialLayerTuple>)this).Equals(other);

        public override int GetHashCode()
            => rect.xMax + 33 * rect.width + 33 * 33 * layer + 33 * 33 * 33 * mat;
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
