using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Mathematics;

namespace Atrufulgium.Voxel.WorldRendering {

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
            => math.all(value == other.value);

        public override bool Equals(object obj)
            => obj is ChunkKey other && Equals(other);

        public override int GetHashCode()
            => value.GetHashCode();

        public override string ToString()
            => value.ToString();
    }
}
