using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;

namespace Atrufulgium.Voxel.Base {

    /// <summary>
    /// Represents a 32x32x32 cube of voxels.
    /// The voxel materials are ushorts.
    /// </summary>
    // This current state is of course very naive.
    // Cache-friendlier would be a massive array that can be walked (with pre-
    // determined parts for each LoD size and also octtrees or something) to
    // not have a bazillion cache misses.
    // For that, see also System.ArraySegment<T>.
    // More specifically, note https://0fps.net/2012/01/14/an-analysis-of-minecraft-like-engines/
    // Use virtual chunks and from the comments also z-curves.
    public readonly struct Chunk : IEnumerable<(int3, ushort)> {
        /// <summary>
        /// A value [0,5] representing how much detail this chunk has.       <br/>
        /// A value of 0 represents a 32x32x32 cube with 1x1x1-sized voxels. <br/>
        /// A value of 1 represents a 16x16x16 cube with 2x2x2-sized voxels. <br/>
        /// A value of 5 represents a 1x1x1 cube with 32x32x32-sized voxels.
        /// </summary>
        public readonly int LoD;
        public int VoxelsPerAxis => 32 >> LoD;
        public int VoxelSize => 1 << LoD;
        public readonly ushort[] voxels;

        static Unity.Mathematics.Random rng = new(230);

        /// <summary>
        /// Creates a new empty chunk with specified LoD.
        /// </summary>
        public Chunk(int LoD) {
            if (LoD is < 0 or > 5)
                throw new ArgumentException("Only levels of detail 0--5 are allowed.", nameof(LoD));

            this.LoD = LoD;
            int size = 32 >> LoD;
            voxels = new ushort[size*size*size];
        }

        /// <summary>
        /// Gets and sets voxels in [0,32)^3. If the level of detail is not 0,
        /// it reads/affects the larger voxels.
        /// </summary>
        public ushort this[int3 coord] { 
            get => voxels[CoordToIndexMorton3(coord)];
            set => voxels[CoordToIndexMorton3(coord)] = value;
        }

        /// <summary>
        /// <para>
        /// Returns a new chunk with a worse detail level than currently.
        /// </para>
        /// <para>
        /// If the new LoD is worse(=higher), it will set the larger voxels to
        /// be (in stochastic expectation) the most common material.
        /// </para>
        /// <para>
        /// If the new LoD is better(=lower), it will upscale exactly as-is.
        /// (Todo: Interpolate (same-type)?)
        /// </para>
        /// </summary>
        /// <param name="LoD"> The new LoD to consider. </param>
        public Chunk WithLoD(int newLoD) {
            Chunk newChunk = new(newLoD);
            int oldVoxelSize = VoxelSize;
            int newVoxelSize = newChunk.VoxelSize;

            if (newLoD <= LoD) {
                // More detail
                foreach((int3 coord, ushort material) in this) {
                    int3 cellMax = new(oldVoxelSize, oldVoxelSize, oldVoxelSize);
                    foreach(int3 offset in Enumerators.EnumerateVolume(max: cellMax, step: newVoxelSize)) {
                        newChunk[coord + offset] = material;
                    }
                }
                return newChunk;
            } else {
                // Less detail
                foreach ((int3 coord, ushort _) in newChunk) {
                    // Sample three random voxels in the part that gets downscaled.
                    ushort material = MajorityVote(
                        this[rng.NextInt3(newVoxelSize) + coord],
                        this[rng.NextInt3(newVoxelSize) + coord],
                        this[rng.NextInt3(newVoxelSize) + coord]
                    );
                    newChunk[coord] = material;
                }
                return newChunk;
            }
        }

        // These are brute-force checked over all (LoD,pos)'s.
        public int CoordToIndexMorton3(int3 coord) {
            coord >>= LoD;
            // Note that Morton does coords in order {0,1}^3, {0,..,3}^3,
            // {0,..,7}^3, etc. As such, it is justified to use Morton also
            // for the higher LoDs.
            return Morton3(coord);
        }

        public int3 IndexToCoordMorton3(int index) {
            int3 coord = UnMorton3(index);
            return coord << LoD;
        }

        /// <summary>
        /// <para>
        /// Turns this chunk into meshes.
        /// </para>
        /// <para>
        /// Any face whose normals would be opposite to <paramref name="viewDir"/>
        /// are culled at this step already. Pass the zero-vector to cull nothing.
        /// </para>
        /// </summary>
        // TODO: proper tl;dr of https://doi.org/10.1137/0402027
        // Note that we also have a "don't care" region in nearly all planes as
        // we don't care what covered voxels do. Taking into account OPT in
        // this case seems nearly impossible.
        public Mesh GetMesh(float3 viewDir) {
            // lmao the worst of the worst but hey, testing.
            // Yes I'm not even collapsing verts.
            List<Vertex> vertices = new();
            Dictionary<Vertex, int> vertToIndex = new();
            // Ushorts are fine - there are at most 33*33*33 vertices.
            // Update: These are not fine, because there may be multiple materials.
            List<ushort> quads = new();

            // For now assuming only air vs non-air for simplicity, and doing just one axis one-sided.
            int voxelSize = VoxelSize;
            int x, y, z;
            Dictionary<RectInt, RectInt> rects = new(new RectHorizontalOnlyComparer());
            
            for (z = 0; z < 32; z += voxelSize) {
                // Considering a single XY-plane, first partition into vertical
                // rectangles. Rectangles are allowed to go under other voxels
                // in another layer.
                rects.Clear();
                for (y = 0; y < 32; y += voxelSize) {
                    RectInt current = default;
                    for (x = 0; x < 32; x+= voxelSize) {
                        // We're not air
                        bool air = this[new(x, y, z)] == 0;
                        // We're covered by the previous layer
                        bool covered = z > 0 && this[new(x, y, z - voxelSize)] != 0;

                        if ((!air && !covered) && current.height == 0) {
                            // We're newly available.
                            current = new(x, y, 0, VoxelSize);
                        } else if (air && !covered && current.height != 0) {
                            // We're no longer available.
                            current.width = x - current.xMin;
                            if (rects.ContainsKey(current)) {
                                var old = rects[current];
                                old.height = current.yMin + voxelSize - old.yMin;
                                rects[current] = old;
                            } else {
                                rects.Add(current, current);
                            }
                            current = default;
                        }
                    }
                    if (current.height != 0) {
                        // We're no longer available due to chunk edge.
                        current.width = 32 - current.xMin;
                        if (rects.ContainsKey(current)) {
                            var old = rects[current];
                            old.height += voxelSize;
                            rects[current] = old;
                        } else {
                            rects.Add(current, current);
                        }
                    }
                }
                foreach(var (_, rect) in rects) {
                    foreach (int2 corner in Enumerators.EnumerateCorners(rect)) {
                        Vertex vert = new(new(corner, z), 1);
                        if (!vertToIndex.TryGetValue(vert, out int index)) {
                            index = vertices.Count;
                            vertices.Add(vert);
                        }
                        quads.Add((ushort)index);
                    }
                }
            }

            var mesh = new Mesh();
            mesh.SetVertexBufferParams(vertices.Count, Layout);
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
        /// Iterates over all voxels in this chunk and returns a
        /// (position, material)-tuple at each iteration. The position
        /// refers to the smallest corner of each voxel.
        /// </summary>
        public IEnumerator<(int3, ushort)> GetEnumerator() {
            foreach (int3 coord in Enumerators.EnumerateVolume(
                max: new int3(32, 32, 32), step: VoxelSize
            )) {
                yield return (coord, this[coord]);
            }
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        /// <summary>
        /// Returns a most common value of three values.
        /// </summary>
        private ushort MajorityVote(ushort a, ushort b, ushort c) {
            // This account for the (a,a,a) and (a,a,c) cases.
            if (a == b)
                return a;
            // This accounts for the (a,c,c), (c,b,c), and (a,b,c) cases.
            return c;
        }

        // Note: You'll never update these functions, also they don't need to
        // be tested anymore because I've run a brute force check on [0,32]^3
        // that they're eachother's inverses.
        /// <summary>
        /// Interleaves the bit patterns 00xxxxxx, 00yyyyyy, 00zzzzzz into a
        /// new number ..xyzxyz. All arguments must fit in 6 bits and this is
        /// not checked.
        /// </summary>
        private static int Morton3(int3 v) {
            // Each line, copy v over and mask to turn e.g. 0000abcd into
            // abcdabcd and then mask into ab0000cd. Then b,d are correct.
            // Really, just write it out on paper, that's easier to grasp.
            v = (v | (v << 8)) & 0x300F;
            v = (v | (v << 4)) & 0x30C3;
            v = (v | (v << 2)) & 0x9249;
            return v.x + 2 * v.y + 4 * v.z;
        }

        /// <summary>
        /// Given an interleaved bit pattern ..xyzxyz, restores 6-bit patterns
        /// 00xxxxxx, 00yyyyyy, 00zzzzzz back in their respective components.
        /// </summary>
        private static int3 UnMorton3(int x) {
            int3 v = new int3(x, x / 2, x / 4) & 0x9249;
            v = (v | (v >> 2)) & 0x30C3;
            v = (v | (v >> 4)) & 0x300F;
            v = (v | (v >> 8)) &   0x3F;
            return v;
        }

        /// <summary>
        /// A comparer only cares about (xMin, width).
        /// </summary>
        struct RectHorizontalOnlyComparer : IEqualityComparer<RectInt> {
            bool IEqualityComparer<RectInt>.Equals(RectInt x, RectInt y)
                => x.xMin == y.xMin && x.width == y.width;
            int IEqualityComparer<RectInt>.GetHashCode(RectInt obj)
                => (obj.xMin, obj.width).GetHashCode();
        }
    }
}
