using Atrufulgium.Voxel.Base;
using Atrufulgium.Voxel.Collections;
using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;

namespace Atrufulgium.Voxel.World {

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
    // Use virtual chunks.
    // Don't bother with z-curves, does not seem worth it in this project
    // from measurement -- about 7% slower.
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
            if (VoxelsPerAxis < 4)
                throw new ArgumentException($"LoD may not be such that there are less than 4 voxels per axis.", nameof(LoD));
        }

        /// <summary>
        /// <para>
        /// Creates a new chunk with specified LoD and prefilled materials.
        /// </para>
        /// <para>
        /// The materials list must fit the chunk exactly, and is a flattened
        /// 3D list ordered as first X, then Y, then Z.
        /// </para>
        /// </summary>
        public Chunk(int LoD, IEnumerable<ushort> materials) : this(LoD) {
            FromRawArray(materials);
        }

        /// <summary>
        /// Gets and sets voxels in [0,32)^3. If the level of detail is not 0,
        /// it reads/affects the larger voxels.
        /// </summary>
        public ushort this[int x, int y, int z] {
            get => this[new int3(x, y, z)];
            set => this[new int3(x, y, z)] = value;
        }

        /// <inheritdoc cref="this[int, int, int]"/>
        public ushort this[int3 coord] { 
            get => voxels[CoordToIndex(coord)];
            set => voxels[CoordToIndex(coord)] = value;
        }

        /// <summary>
        /// <inheritdoc cref="this[int3]"/>
        /// <para>
        /// This is a version that handles four values at a time.
        /// </para>
        /// </summary>
        public int4 this[int4 x, int4 y, int4 z] {
            get {
                int4 index = CoordToIndex(x, y, z);
                int4 ret;
                ret.x = voxels[index.x];
                ret.y = voxels[index.y];
                ret.z = voxels[index.z];
                ret.w = voxels[index.w];
                return ret;
            }
            set {
                int4 index = CoordToIndex(x, y, z);
                voxels[index.x] = (ushort)value.x;
                voxels[index.y] = (ushort)value.y;
                voxels[index.z] = (ushort)value.z;
                voxels[index.w] = (ushort)value.w;
            }
        }

        /// <summary>
        /// <inheritdoc cref="this[int4, int4, int4]"/>
        /// <para>
        /// The columns of the matrix represent the "x", "y", and "z" parts.
        /// </para>
        /// </summary>
        public int4 this[int4x3 coords]
            => this[coords.c0, coords.c1, coords.c2];

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

        /// <summary>
        /// Clears this chunk with air.
        /// </summary>
        public void Clear() {
            voxels.Clear();
        }

        /// <summary>
        /// Creates a copy of the chunk, including a copy of the voxel data.
        /// </summary>
        public Chunk GetCopy() {
            Chunk copy = new(LoD);
            copy.voxels.CopyFrom(voxels);
            return copy;
        }

        int CoordToIndex(int3 coord) {
            coord >>= LoD;
            return coord.x + VoxelsPerAxis * (coord.y + VoxelsPerAxis * coord.z);
        }

        int4 CoordToIndex(int4 x, int4 y, int4 z) {
            x >>= LoD;
            y >>= LoD;
            z >>= LoD;
            return x + VoxelsPerAxis * (y + VoxelsPerAxis * z);
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

        /// <summary>
        /// Unsafe read access into the array. This allows bursted jobs to not
        /// recalculate <see cref="VoxelsPerAxis"/> and <see cref="VoxelSize"/>
        /// every time.
        /// </summary>
        public ushort GetRaw(int index) => voxels[index];
        /// <inheritdoc cref="GetRaw(int)"/>
        public uint4 GetRaw(int4 index) => new(voxels[index.x], voxels[index.y], voxels[index.z], voxels[index.w]);

        /// <summary>
        /// Unsafe write access to overwrite the entire array contents with new
        /// values.
        /// </summary>
        public void FromRawArray(IEnumerable<ushort> values) {
            int written = 0;
            foreach (ushort val in values) {
                if (written >= voxels.Length)
                    throw new ArgumentException($"There are too many values for this chunk. Expected {voxels.Length}, got more.", nameof(values));
                voxels[written] = val;
                written++;
            }
            if (written < voxels.Length)
                throw new ArgumentException($"There are too few values for this chunk. Expected {voxels.Length}, got {written}.", nameof(values));
        }
        /// <inheritdoc cref="FromRawArray(IEnumerable{ushort})"/>
        public void FromRawArray(Chunk values) {
            FromRawArray(values.voxels);
        }

        /// <summary>
        /// Returns a pointer to the underlying array.
        /// </summary>
        public unsafe ushort* GetUnsafeUnderlyingPtr()
            => (ushort*) voxels.GetUnsafePtr();

        /// <summary>
        /// Returns a read-only pointer to the underlying array.
        /// </summary>
        public unsafe ushort* GetUnsafeUnderlyingReadOnlyPtr()
            => (ushort*)voxels.GetUnsafeReadOnlyPtr();


        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        public void Dispose() {
            voxels.Dispose();
        }
    }
}
