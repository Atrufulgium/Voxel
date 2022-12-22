using System;
using System.Collections.Generic;
using UnityEngine;
using Unity.Mathematics;
using UnityEngine.Rendering;

namespace Atrufulgium.Voxel.Base {
    public class ChunkMesher {

        /// <summary>
        /// All verts in the current GetMesh call.
        /// </summary>
        readonly List<Vertex> vertices = new(ushort.MaxValue + 1);
        /// <summary>
        /// A conversion from vertex to index inside the vertices list in the
        /// current GetMesh call.
        /// </summary>
        readonly Dictionary<Vertex, int> vertToIndex = new(ushort.MaxValue + 1);
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
        readonly List<ushort> quads = new();
        /// <summary>
        /// The rects of the current GetMeshFromDirection layer.
        /// </summary>
        // This would be so much better with an interval tree.
        // I *will* implement those someday for RLE, so I guess,
        // TODO: replace with interval tree.
        readonly Dictionary<(RectInt, ushort), (RectInt, ushort)> rects = new(new RectHorizontalOnlyComparer());

        /// <summary>
        /// <para>
        /// Turns this chunk into meshes.
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
        public Mesh GetMesh(Chunk chunk, float3 viewDir = default) {
            vertices.Clear();
            vertToIndex.Clear();
            quads.Clear();
            rects.Clear();
            foreach ((var layerToCoord, var backside) in renderDirections) {
                // TODO: This is probably incorrect for the same reason as on
                // the GPU side - it doesn't take into account the perspective
                // transformation.
                int3 normal = layerToCoord(0, 0, 1);
                if (!backside)
                    normal *= -1;
                if (math.dot(viewDir, normal) >= 0)
                    GetMeshFromDirection(chunk, layerToCoord, backside);
            }

            var mesh = new Mesh();
            mesh.SetVertexBufferParams(vertices.Count, Layout);
            // (Flag 15 supresses all messages)
            mesh.SetVertexBufferData(vertices, 0, 0, vertices.Count, flags: (MeshUpdateFlags)15);

            mesh.SetIndexBufferParams(quads.Count, IndexFormat.UInt16);
            mesh.SetIndexBufferData(quads, 0, 0, quads.Count, flags: (MeshUpdateFlags)15);

            mesh.subMeshCount = 1;
            // Do note: The docs (<=5.4 already though) note that quads are
            // often emulated. Is this still the case?
            mesh.SetSubMesh(0, new SubMeshDescriptor(0, quads.Count, MeshTopology.Quads), flags: (MeshUpdateFlags)15);

            mesh.bounds = new(new(16, 16, 16), new(32, 32, 32));

            return mesh;
        }

        private static int3 LayerToCoordX(int x, int y, int layer) => new(x, y, layer);
        private static int3 LayerToCoordY(int x, int y, int layer) => new(y, layer, x);
        private static int3 LayerToCoordZ(int x, int y, int layer) => new(layer, x, y);

        private static readonly IEnumerable<(Func<int, int, int, int3>, bool)> renderDirections = Enumerators.EnumerateTuple(
            new Func<int, int, int, int3>[] { LayerToCoordX, LayerToCoordY, LayerToCoordZ },
            new[] { true, false }
        );

        private void GetMeshFromDirection(
            Chunk chunk,
            Func<int, int, int, int3> layerToCoord,
            bool backside
        ) {
            int voxelSize = chunk.VoxelSize;
            int x, y, layer;
            for (layer = 0; layer < 32; layer += voxelSize) {
                // Considering a single XY-plane, first partition into vertical
                // rectangles. Rectangles are allowed to go under other voxels
                // in another layer. Grow those rectangles up if possible.
                rects.Clear();
                for (y = 0; y < 32; y += voxelSize) {
                    RectInt current = default;
                    ushort currentMat = 0;
                    for (x = 0; x < 33; x += voxelSize) {
                        // We're at the end of the chunk and can't do anything
                        // but add a possible WIP rect.
                        bool final = x == 32;
                        // We're different and need to change what we're doing.
                        bool different = false;
                        ushort mat = 0;
                        if (!final) {
                            mat = chunk[layerToCoord(x, y, layer)];
                            different = mat != currentMat;
                        }
                        // We're covered by the previous layer and can do whatever
                        bool covered;
                        if (backside)
                            covered = !final && layer > 0 && chunk[layerToCoord(x, y, layer - voxelSize)] != 0;
                        else
                            covered = !final && layer + voxelSize < 32 && chunk[layerToCoord(x, y, layer + voxelSize)] != 0;

                        // Commit the previous on differences. This automatically
                        // commits changes on x=32 as that's always air.
                        if ((different && !covered || final) && currentMat != 0) {
                            // We're no longer available.
                            current.width = x - current.xMin;
                            var key = (current, currentMat);
                            if (rects.ContainsKey(key)) {
                                // As noted below, the key's equality is not
                                // transitive. This is an ugly hack, but this
                                // code *needs* it to be somewhat decent still.
                                // So we need to actually pop the (key,value)-
                                // pair and insert one that *looks* the same.
                                // Yes, this is massive dict-abuse.
                                var (old, _) = rects[key];
                                rects.Remove(key);
                                old.height = current.yMin + voxelSize - old.yMin;
                                key.current = old;
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
                foreach (var (_, (rect, mat)) in rects) {
                    IEnumerable<int2> corners;
                    if (backside)
                        corners = Enumerators.EnumerateCornersClockwise(rect);
                    else
                        corners = Enumerators.EnumerateCornersCounterclockwise(rect);

                    foreach (int2 corner in corners) {
                        Vertex vert;
                        if (backside)
                            vert = new(layerToCoord(corner.x, corner.y, layer), mat);
                        else
                            vert = new(layerToCoord(corner.x, corner.y, layer + voxelSize), mat);
                        if (!vertToIndex.TryGetValue(vert, out int index)) {
                            index = vertices.Count;
                            vertices.Add(vert);
                        }
                        quads.Add((ushort)index);
                    }
                } // for rect
            } // for layer
        }

        /// <summary>
        /// Turns a (x,y,z,material) into a single uint, by storing in the
        /// first three factors 33 the coordinate positions, and in the
        /// remaining [0,119513]-range the material.
        /// </summary>
        struct Vertex : IEquatable<Vertex> {
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
        }
        static readonly VertexAttributeDescriptor[] Layout = new VertexAttributeDescriptor[] {
            new VertexAttributeDescriptor(VertexAttribute.BlendIndices, VertexAttributeFormat.UInt32, 1)
        };

        /// <summary>
        /// <para>
        /// A comparer only cares about (xMin, width), and the two being
        /// vertically apart by at most one. Also, disregards any different
        /// two material rects.
        /// </para>
        /// <para>
        /// Note!!! This equality is NOT transitive! This breaks a bunch of
        /// shit you usually wouldn't think about.
        /// </para>
        /// </summary>
        struct RectHorizontalOnlyComparer : IEqualityComparer<(RectInt, ushort)> {
            bool IEqualityComparer<(RectInt, ushort)>.Equals((RectInt, ushort) x, (RectInt, ushort) y)
                => x.Item2 == y.Item2
                && x.Item1.xMin == y.Item1.xMin && x.Item1.width == y.Item1.width
                && (x.Item1.yMin == y.Item1.yMax || x.Item1.yMax == y.Item1.yMin);
            int IEqualityComparer<(RectInt, ushort)>.GetHashCode((RectInt, ushort) obj)
                => (obj.Item1.xMin, obj.Item1.width, obj.Item2).GetHashCode();
        }
    }
}
