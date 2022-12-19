using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;

namespace Atrufulgium.Voxel.Base {

    /// <summary>
    /// Represents a 32x32x32 cube of voxels.
    /// The voxel values are ushorts.
    /// </summary>
    // This current state is of course very naive.
    // Cache-friendlier would be a massive array that can be walked (with pre-
    // determined parts for each LoD size and also octtrees or something) to
    // not have a bazillion cache misses.
    // For that, see also System.ArraySegment<T>.
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
            get => voxels[CoordToIndex(coord)];
            set => voxels[CoordToIndex(coord)] = value;
        }

        /// <summary>
        /// <para>
        /// Returns a new chunk with a worse detail level than currently.
        /// </para>
        /// <para>
        /// If the new LoD is worse(=higher), it will set the larger voxels to
        /// be the first of the cell. (Todo: most common one?)
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
                // (You may want to pull `oldStepSize` into the above branch,
                //  but more sophisticated methods will use it here also.)
                foreach((int3 coord, ushort _) in newChunk)
                    newChunk[coord] = this[coord];
                return newChunk;
            }
        }

        // TODO: Test
        public int CoordToIndex(int3 coord) {
            coord >>= LoD;
            int inverseLod = 5 - LoD;
            coord.x <<= 2 * inverseLod;
            coord.y <<= inverseLod;
            return coord.x + coord.y + coord.z;
        }

        public int3 IndexToCoord(int index) {
            int inverseLod = 5 - LoD;
            int3 coord = new(
                index >> 2 * inverseLod,
                index >> inverseLod,
                index
            );
            coord %= (1 << inverseLod);
            return coord << LoD;
        }

        /// <summary>
        /// <para>
        /// Turns this chunk into meshes. In particular, each vertex:
        /// <list type="bullet">
        /// <item> Has <b>no</b> normals. Voxels are simple, these can be
        /// derived easily in shaders. </item>
        /// <item> Has (positive) uvs such that uvs mod one correspond to a
        /// single voxel. </item>
        /// <item> Uses vertex colours for the voxel value, divided by 32768.
        /// As the float mantissa is 23 bits, this is exact. </item>
        /// </list>
        /// The last two are compressed into one though, to unpack on the GPU.
        /// </para>
        /// <para>
        /// Any face whose normals would be opposite to <paramref name="viewDir"/>
        /// are culled at this step already.
        /// </para>
        /// </summary>
        public Mesh GetMesh(int3 viewDir) {
            // lmao the worst of the worst but hey, testing.
            // Yes I'm not even collapsing verts.
            List<Vertex> vertices = new();
            // Ushorts are fine - there are at most 33*33*33 vertices.
            List<ushort> quads = new();
            //temp ofc lol
            ushort[] cubeQuads = new ushort[] { 5, 7, 6, 4, 2, 6, 7, 3, 3, 7, 5, 1, 1, 5, 4, 0, 0, 4, 6, 2, 2, 3, 1, 0 };
            foreach((int3 coord, ushort material) in this) {
                if (material == 0)
                    continue;

                int index = vertices.Count;
                foreach (int3 corner in Enumerators.EnumerateVolume(new int3(2,2,2))) {
                    // TODO: uvs ofc
                    vertices.Add(new(coord + corner * VoxelSize, 0, material));
                }
                foreach (var tri in cubeQuads)
                    quads.Add((ushort)(tri + index));
            }

            var mesh = new Mesh();
            mesh.SetVertexBufferParams(vertices.Count, Layout);
            mesh.SetVertexBufferData(vertices, 0, 0, vertices.Count, flags: (MeshUpdateFlags)/*15*/0);

            mesh.SetIndexBufferParams(quads.Count, IndexFormat.UInt16);
            mesh.SetIndexBufferData(quads, 0, 0, quads.Count, flags: 0);

            mesh.subMeshCount = 1;
            // Do note: The docs (<=5.4 already though) note that quads are
            // often emulated. Is this still the case?
            mesh.SetSubMesh(0, new SubMeshDescriptor(0, quads.Count, MeshTopology.Quads), flags: 0);

            return mesh;
        }

        /// <summary>
        /// See the documentation of <see cref="GetMesh(int3)"/>
        /// </summary>
        /// <remarks>
        /// The uvs are integer, with each coord being one of {0, 1, .. 32}.
        /// This takes up 12 bits in total. The actual voxel value is a ushort,
        /// taking up 16 bits. This fits within the 32 bits of a single float,
        /// which can be unpacked on the GPU side. Packing format:
        /// <code>
        ///     msb    __YY YYYY __XX XXXX VVVV VVVV VVVV VVVV    lsb
        ///               uv-y      uv-x       voxel value
        /// </code>
        /// </remarks>
        [StructLayout(LayoutKind.Sequential)]
        struct Vertex {
            public float3 pos;
            public int color;

            public Vertex(float3 pos, int2 uv, ushort value) {
                this.pos = pos;
                color = value + (uv.x << 16) + (uv.y << 24);
            }
        }
        static VertexAttributeDescriptor[] Layout = new VertexAttributeDescriptor[] {
            new VertexAttributeDescriptor(VertexAttribute.Position, VertexAttributeFormat.Float32, 3),
            new VertexAttributeDescriptor(VertexAttribute.Color, VertexAttributeFormat.SInt32, 1)
        };

        /// <summary>
        /// Iterates over all voxels in this chunk and returns a
        /// (position, value)-tuple at each iteration. The position
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
    }
}
