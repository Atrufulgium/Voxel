using System;
using Unity.Mathematics;

namespace Atrufulgium.Voxel.World {
    /// <summary>
    /// An indexer for chunks in the <see cref="RenderWorld"/>.
    /// Construct it via either of <see cref="FromWorldPos(int3)"/> or
    /// <see cref="FromKey(int3)"/>.
    /// </summary>
    public readonly struct ChunkKey : IEquatable<ChunkKey> {
        readonly int3 value;
        public int3 KeyValue => value;
        public int3 Worldpos => value << Chunk.ChunkExponent;

        private ChunkKey(int3 value)
            => this.value = value;

        /// <summary>
        /// Constructs a new key corresponding to a position in the world.
        /// </summary>
        public static ChunkKey FromWorldPos(int3 pos)
            => new(pos >> Chunk.ChunkExponent);

        /// <summary>
        /// <inheritdoc cref="FromWorldPos(int3)"/>
        /// <para>
        /// Also gives the local chunk coordinates.
        /// </para>
        /// </summary>
        public static ChunkKey FromWorldPos(int3 pos, out int3 chunkPos) {
            chunkPos = pos & (Chunk.ChunkSize - 1);
            return FromWorldPos(pos);
        }

        /// <summary>
        /// Constructs a new key corresponding to the value of another key.
        /// </summary>
        public static ChunkKey FromKey(int3 key)
            => new(key);

        public bool Equals(ChunkKey other)
            => this == other;

        public override bool Equals(object obj)
            => obj is ChunkKey other && this == other;

        public static bool operator ==(ChunkKey a, ChunkKey b)
            => math.all(a.value == b.value);

        public static bool operator !=(ChunkKey a, ChunkKey b)
            => !(a == b);

        public override int GetHashCode()
            => value.GetHashCode();

        public override string ToString()
            => value.ToString();

        /// <summary>
        /// Offsets a given key by a number of chunks.
        /// </summary>
        public static ChunkKey operator +(ChunkKey key, int3 chunkOffset)
            => FromKey(key.KeyValue + chunkOffset);
    }
}
