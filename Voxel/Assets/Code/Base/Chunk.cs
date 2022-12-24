using Atrufulgium.Voxel.Collections;
using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Mathematics;

namespace Atrufulgium.Voxel.Base {

    /// <summary>
    /// Represents a 32x32x32 cube of voxels.
    /// The voxel materials are ushorts.
    /// </summary>
    /// <remarks>
    /// While this is technically a struct, the only thing you have write
    /// access to is an unmanaged array, so feel free to just pass this around
    /// as if this were a reference.
    /// </remarks>
    // This current state is of course very naive.
    // Cache-friendlier would be a massive array that can be walked (with pre-
    // determined parts for each LoD size and also octtrees or something) to
    // not have a bazillion cache misses.
    // For that, see also System.ArraySegment<T>.
    // More specifically, note https://0fps.net/2012/01/14/an-analysis-of-minecraft-like-engines/
    // Use virtual chunks and from the comments also z-curves.
    public struct Chunk : IEnumerable<(int3, ushort)>, IDisposable {
        /// <summary>
        /// A value [0,5] representing how much detail this chunk has.       <br/>
        /// A value of 0 represents a 32x32x32 cube with 1x1x1-sized voxels. <br/>
        /// A value of 1 represents a 16x16x16 cube with 2x2x2-sized voxels. <br/>
        /// A value of 5 represents a 1x1x1 cube with 32x32x32-sized voxels.
        /// </summary>
        public readonly int LoD;
        public int VoxelsPerAxis => ChunkSize >> LoD;
        public int VoxelSize => 1 << LoD;
        NativeArray<ushort> voxels;

        /// <summary>
        /// Whether the underlying NativeArray exists.
        /// </summary>
        public bool IsCreated => voxels.IsCreated;

        /// <summary>
        /// The chunks have width 2**ChunkExponent.
        /// This may be at most 5.
        /// </summary>
        public const int ChunkExponent = 5;
        /// <summary>
        /// The chunk width.
        /// </summary>
        public const int ChunkSize = 1 << ChunkExponent;

        /// <summary>
        /// Creates a new empty chunk with specified LoD.
        /// </summary>
        public Chunk(int LoD) {
            if (LoD is < 0 or > ChunkExponent)
                throw new ArgumentException($"Only levels of detail 0--{ChunkExponent} are allowed.", nameof(LoD));

            this.LoD = LoD;
            int size = ChunkSize >> LoD;
            voxels = new(size*size*size, Allocator.Persistent);
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
        /// be the one in the smaller voxel in its center.
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
                    newChunk[coord] = this[coord + (newVoxelSize >> 1)];
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
        /// Iterates over all voxels in this chunk and returns a
        /// (position, material)-tuple at each iteration. The position
        /// refers to the smallest corner of each voxel.
        /// </summary>
        public IEnumerator<(int3, ushort)> GetEnumerator() {
            foreach (int3 coord in Enumerators.EnumerateVolume(
                max: new int3(ChunkSize, ChunkSize, ChunkSize), step: VoxelSize
            )) {
                yield return (coord, this[coord]);
            }
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

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

        public void Dispose() {
            voxels.Dispose();
        }
    }
}
