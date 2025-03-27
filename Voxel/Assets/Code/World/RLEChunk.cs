using Atrufulgium.Voxel.Base;
using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Mathematics;

namespace Atrufulgium.Voxel.World {
    /// <summary>
    /// Represents a compressed 32x32x32 cube of voxels.
    /// The voxel materials are ushorts.
    /// <br/>
    /// Never use the parameterless constructor.
    /// </summary>
    /// <remarks>
    /// When doing few edits, you can use this chunk type directly. If your
    /// edits replace material but don't change the structure of the chunk,
    /// you can also use this directly.
    /// <br/>
    /// Otherwise, if you do many arbitrary modifications, you might want to
    /// first convert the chunk to a <see cref="RawChunk"/>, apply your
    /// changes, and then convert back.
    /// <br/>
    /// Like <see cref="RawChunk"/>, this is just a native reference, so you
    /// can pass around instances of this struct as if it's an object.
    /// </remarks>
    public struct RLEChunk : IEnumerable<(int3, ushort)>, IDisposable {

        /// <inheritdoc cref="RawChunk.LoD"/>
        /// <remarks>
        /// Unlike <see cref="RawChunk"/>, this LoD value may change if you
        /// for instance overwrite data with <see cref="CopyFrom(RLEChunk)"/>.
        /// </remarks>
        public int LoD { get; private set; }
        public int VoxelsPerAxis => ChunkSize >> LoD;
        public int VoxelSize => 1 << LoD;

        /// <summary>
        /// A list of compressed chunk data. We first enumerate x, then y,
        /// then z. When created, the capacity of this list should be a little
        /// bigger (~40%?) to account for minor changes.
        /// </summary>
        // Invariant: There's always a (material: *, start: 0)-entry, nothing
        // starts at VoxelsPerAxis^3 or later, and no start is used twice.
        NativeList<RLEEntry> voxels;

        /// <inheritdoc cref="RawChunk.ChunkExponent"/>
        public const int ChunkExponent = RawChunk.ChunkExponent;
        /// <inheritdoc cref="RawChunk.ChunkSize"/>
        public const int ChunkSize = RawChunk.ChunkSize;

        /// <summary>
        /// Whether the underlying NativeList exists.
        /// (It will exist if you properly use the constructor and don't run
        ///  out of memory.)
        /// </summary>
        public bool IsCreated => voxels.IsCreated;

        /// <summary>
        /// Creates a new chunk filled with <paramref name="material"/> with a
        /// specified LoD.
        /// </summary>
        public RLEChunk(int LoD, ushort material = 0, int capacity = 1) {
            if (LoD is < 0 or > ChunkExponent)
                throw new ArgumentException($"Only levels of detail 0--{ChunkExponent} are allowed.", nameof(LoD));

            this.LoD = LoD;
            voxels = new(capacity, Allocator.Persistent);
            voxels.Add(new(material, 0));
            // TODO: We're allowing <4 here, but RawChunk doesn't.
        }

        /// <summary>
        /// Turns a <see cref="RawChunk"/> into a new <see cref="RLEChunk"/>.
        /// <br/>
        /// For the inverse operation, see the <see cref="RawChunk(RLEChunk)"/>
        /// constructor.
        /// <br/>
        /// For the operation that writes into an existing RLEChunk, see
        /// <see cref="SetFromRawChunk(RawChunk)"/>.
        /// </summary>
        public unsafe RLEChunk(RawChunk chunk) {
            if (!chunk.IsCreated)
                throw new ArgumentException("Tried to compress a non-existent chunk!");

            LoD = chunk.LoD;
            voxels = new(1, Allocator.Persistent);
            FillRawChunkData(chunk);
        }

        /// <summary>
        /// Compresses a <see cref="RawChunk"/>'s data into this RLEChunk.
        /// <br/>
        /// To compress into a new chunk, use the <see cref="RLEChunk(RawChunk)"/>
        /// constructor.
        /// </summary>
        public void SetFromRawChunk(RawChunk chunk) {
            if (!chunk.IsCreated)
                throw new ArgumentException("Tried to compress a non-existent chunk!");
            if (!IsCreated)
                throw new InvalidOperationException("Tried to compress into a non-existent chunk!");

            FillRawChunkData(chunk);
        }

        /// <summary>
        /// Clears and fills the internal <see cref="voxels"/> array based on
        /// a <see cref="RawChunk"/>.
        /// </summary>
        private unsafe void FillRawChunkData(RawChunk chunk) {
            LoD = chunk.LoD;

            // Count the number of transitions in the array before creating a
            // list (as otherwise we'd just resize the thing a bazillion times)
            var ptr = chunk.GetUnsafeUnderlyingReadOnlyPtr();
            int entries = 1; // the ambient material is already an entry
            for (int i = 1; i < VoxelsPerAxis * VoxelsPerAxis * VoxelsPerAxis; i++) {
                if (*(ptr + i) != *(ptr + i - 1)) // every change adds an entry
                    entries++;
            }

            int capacity = (int)math.round(entries * 1.4f);
            if (capacity < 1)
                capacity = 1;

            voxels.Clear();
            voxels.SetCapacity(capacity);

            // Actually write all values.
            voxels.Add(new(*ptr, 0));
            for (int i = 1; i < VoxelsPerAxis * VoxelsPerAxis * VoxelsPerAxis; i++) {
                ushort currMat = *(ptr + i);
                ushort prevMat = *(ptr + i - 1);
                if (currMat != prevMat) {
                    voxels.Add(new(currMat, i));
                }
            }
        }

        public ushort this[int x, int y, int z] {
            get => this[new int3(x, y, z)];
            set => this[new int3(x, y, z)] = value;
        }

        public ushort this[int3 position] {
            get => Get(position);
            set => Set(position, value);
        }

        /// <summary>
        /// Returns the voxel at a specific location in a chunk.
        /// This is efficient.
        /// </summary>
        internal ushort Get(int3 position) {
            if (!IsCreated)
                throw new InvalidOperationException("Tried to get a non-existent chunk!");

            return voxels[GetRunIndex(position)].material;
        }

        /// <summary>
        /// Sets a single voxel to a material in the chunk. This is not
        /// particularly efficient -- if you need to set many voxels, first
        /// <see cref="Decompress"/> your chunk and then apply your changes.
        /// </summary>
        internal void Set(int3 position, ushort material) {
            if (!IsCreated)
                throw new InvalidOperationException("Tried to set a non-existent chunk!");

            ushort linearized = Linearize(position);
            // There's a few possibilities for the linearized index:
            // If the material at `position` is already `material`, we don't
            // have to do anything. For the other cases, consult the following
            // chart. We are putting an X into the middle column each time, and
            // [ and ] denote the starts and ends of the RLE respectively.
            //
            //  +2    +1    0     -1    -2
            // aaa   aab   aba   abX   XaX
            //       baa   [ab   Xab
            //       [aa   ab]   [aX
            //       aa]   aaX   Xa]
            //             Xaa
            //
            // The number at the top denotes how many RLE entries we add/remove
            // when setting the middle to X. Here, a and b are "other"
            // materials than `material`.
            // This collection of possibilities is exhaustive.
            // If I don't want useless memcopies by canceling additions and
            // removals, this gets a stupid amount of branches though...

            int i = GetRunIndex(linearized);
            var run = voxels[i];
            int length = GetRunLength(run, i);

            // Special case: same material
            if (run.material == material)
                return;

            // Special case: Start of the RLE
            // This is either +1, 0, or -1
            RLEEntry nextRun;
            if (linearized == 0) {
                if (length > 1) {
                    // +1 case "[aa"
                    voxels.Insert(0, new(material, 0));
                    voxels[1] = new(run.material, 1);
                    return;
                }
                // length == 1, accessing the next one is safe
                nextRun = voxels[i + 1];
                if (nextRun.material != material) {
                    // 0 case "[ab"
                    voxels[0] = new(material, 0);
                    return;
                }
                // -1 case "[aX"
                voxels[0] = new(material, 0);
                voxels.RemoveAt(1);
                return;
            }

            // We now have a well-defined "previous" entry.
            int previ = i;
            var prevRun = run;
            if (linearized - 1 < run.start) {
                previ = i - 1;
                prevRun = voxels[previ];
            }
            
            // Special case: End of the RLE
            // Again, either +1, 0, or -1
            if (linearized == VoxelsPerAxis * VoxelsPerAxis * VoxelsPerAxis - 1) {
                if (i == previ) {
                    // +1 case "aa]"
                    voxels.Add(new(material, linearized));
                    return;
                }
                // Now the second-to-last and last voxels differ.
                if (prevRun.material != material) {
                    // 0 case "ab]"
                    voxels[i] = new(material, run.start);
                    return;
                }
                // -1 case "Xa]"
                voxels.RemoveAt(i);
                return;
            }

            // We now have a well-defined "next" entry.
            int nexti = i;
            nextRun = run;
            if (linearized + 1 >= run.start + length) {
                nexti = i + 1;
                nextRun = voxels[nexti];
            }

            // We can now go through all other cases.
            // yeah this code is ugly AF but it has literally no reason to
            // change in the future, so just exhaustively test everything and
            // then that's that
            if (previ == i && i == nexti) {
                // case "aaa": insert two slots for the swaps into "aXa"
                voxels.InserTwo(i + 1, new(material, linearized), new(run.material, linearized + 1));
                return;
            }
            if (previ == i && i != nexti) {
                if (nextRun.material != material) {
                    // case "aab": insert one slot to create "aXb"
                    voxels.Insert(i + 1, new(material, linearized));
                    return;
                }
                // case "aaX": move next run's start back by one to create "aXX"
                voxels[nexti] = new(material, linearized);
                return;
            }
            if (previ != i && i == nexti) {
                if (prevRun.material != material) {
                    // case "baa"
                    voxels.Insert(i, new(material, linearized));
                    voxels[i + 1] = new(run.material, linearized + 1);
                    return;
                }
                // case "Xaa"
                voxels[i] = new(run.material, linearized + 1);
                return;
            }
            // Now we're either aba (abc), abX, Xab, or XaX: all three runs are
            // different.
            if (prevRun.material == material) {
                if (nextRun.material == material) {
                    // -2 case "XaX", where we can just remove the a and X starts
                    voxels.RemoveRange(i, 2);
                    return;
                } else {
                    // -1 case "Xab", where we can just remove the a start
                    voxels.RemoveAt(i);
                    return;
                }
            } else {
                if (nextRun.material == material) {
                    // -1 case "abX", change the b run and remove the now-duplicate X
                    voxels[i] = new(material, linearized);
                    voxels.RemoveAt(i + 1);
                    return;
                } else {
                    // 0 case "aba" (or abc), change the b run
                    voxels[i] = new(material, linearized);
                    return;
                }
            }
        }

        /// <summary>
        /// Replaces all <paramref name="from"/> material to <paramref name="to"/>
        /// material in the chunk. This is efficient.
        /// </summary>
        public unsafe void ReplaceAll(ushort from, ushort to) {
            // Pointers are safe in this context as the list reference cannot
            // change in the meantime. This holds true in all unsafe methods.
            var ptr = voxels.GetUnsafeTypedPtr();
            for (int i = 0; i < voxels.Length; i++) {
                if ((ptr + i)->material == from) {
                    (ptr + i)->material = to;
                }
            }
        }

        /// <summary>
        /// Clears this chunk with air.
        /// </summary>
        public void Clear() {
            voxels.Clear();
            voxels.Add(new RLEEntry(0, 0));
        }

        /// <summary>
        /// Reads the RLEChunk into a decompressed <see cref="VoxelsPerAxis"/>^3 materials list.
        /// </summary>
        // stackalloc'ing up to 6% of the usual stack limit is a terrible idea...
        // letsgooo
        internal unsafe void DecompressIntoMaterials(ref Span<ushort> arr) {
            if (arr.Length != VoxelsPerAxis * VoxelsPerAxis * VoxelsPerAxis) {
                throw new ArgumentException($"The given array must be exactly {VoxelsPerAxis}^3.");
            }
            var ptr = voxels.GetUnsafeTypedReadOnlyPtr();
            int arri = 0;
            for (int i = 0; i < voxels.Length; i++) {
                var entry = *(ptr + i);
                int count = GetRunLength(entry, i);

                for (int ii = 0; ii < count; ii++) {
                    arr[arri] = entry.material;
                    arri++;
                }
            }
            if (arri != VoxelsPerAxis * VoxelsPerAxis * VoxelsPerAxis) {
                throw new InvalidOperationException("Encountered malformed RLEChunk with incomplete data.");
            }
        }

        /// <summary>
        /// Creates a deep copy of the chunk, including a copy of the voxel data.
        /// </summary>
        public RLEChunk GetCopy() {
            RLEChunk copy = new(LoD, 0, voxels.Capacity);
            copy.voxels.CopyFrom(voxels);
            return copy;
        }

        /// <summary>
        /// Overwrites the current chunk with a deep copy of another chunk's data.
        /// </summary>
        public void CopyFrom(RLEChunk other) {
            LoD = other.LoD;
            voxels.Clear();
            voxels.CopyFrom(other.voxels);
        }

        public void Dispose() => voxels.Dispose();

        // TODO: Struct enumerator? This might get called a ton.
        // Doing yield also forces safe code, while pointering is so much better.
        public IEnumerator<(int3, ushort)> GetEnumerator() {
            ushort mat;
            int linearIndex = 0;
            for (int voxelIndex = 0; voxelIndex < voxels.Length; voxelIndex++) {
                var entry = voxels[voxelIndex];
                int count = GetRunLength(entry, voxelIndex);
                mat = entry.material;

                for (int i = 0; i < count; i++) {
                    yield return (Vectorize(linearIndex++), mat);
                }
            }
            if (linearIndex != VoxelsPerAxis * VoxelsPerAxis * VoxelsPerAxis) {
                throw new InvalidOperationException("Encountered malformed RLEChunk with incomplete data.");
            }
        }

        /// <summary>
        /// Gets the index to the run that contains the voxel at `position`.
        /// This is efficient.
        /// </summary>
        int GetRunIndex(ushort linPos) {
            // checked the code and wow they're doing the same as c# it's a miracle
            int i = voxels.BinarySearch(new(0, linPos));
            if (i >= 0) {
                return i;
            } else {
                // (it returns the complement of the *next* entry, but we need
                //  the previous one)
                i = ~i;
                return i-1;
            }
        }
        /// <inheritdoc cref="GetRunIndex(ushort)"/>
        int GetRunIndex(int3 position) => GetRunIndex(Linearize(position));

        /// <summary>
        /// Calculates the length of a run. The <paramref name="entry"/> must
        /// match the <paramref name="entryIndex"/>.
        /// </summary>
        int GetRunLength(RLEEntry entry, int entryIndex) {
            if (entryIndex == voxels.Length - 1) {
                return VoxelsPerAxis * VoxelsPerAxis * VoxelsPerAxis - entry.start;
            }
            return voxels[entryIndex + 1].start - entry.start;
        }

        internal ushort Linearize(int3 index)
            => (ushort)math.dot(
                index,
                new(1, VoxelsPerAxis, VoxelsPerAxis * VoxelsPerAxis)
            );
        internal int3 Vectorize(int index)
            => new int3(
                index,
                index / VoxelsPerAxis,
                index / (VoxelsPerAxis * VoxelsPerAxis)
            ) % VoxelsPerAxis;

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        private struct RLEEntry : IEquatable<RLEEntry>, IComparable<RLEEntry> {
            /// <summary>
            /// What material this entry is made of.
            /// </summary>
            public ushort material;
            /// <summary>
            /// What index this run starts at. The end of this run is implicit:
            /// if there's a next entry, it will be up to that entry's start,
            /// minus one. Otherwise, it will end at 32^3.
            /// </summary>
            public ushort start;

            public RLEEntry(ushort material, ushort start) {
                this.material = material;
                this.start = start;
            }

            public RLEEntry(ushort material, int start) {
                this.material = material;
                this.start = (ushort)start;
            }

            // only for binary search-on-start reasons
            public int CompareTo(RLEEntry other)
                => start.CompareTo(other.start);

            public override bool Equals(object obj) {
                return obj is RLEEntry entry && Equals(entry);
            }

            public bool Equals(RLEEntry other) {
                return material == other.material &&
                       start == other.start;
            }

            public override int GetHashCode() {
                return HashCode.Combine(material, start);
            }

            public static bool operator ==(RLEEntry left, RLEEntry right) {
                return left.Equals(right);
            }

            public static bool operator !=(RLEEntry left, RLEEntry right) {
                return !(left == right);
            }

            public override string ToString()
                => $"Material {material} from {start}";
        }
    }
}
