using System;

namespace Atrufulgium.Voxel.World {
    /// <summary>
    /// This exception is to be thrown whenever actions would affect unloaded
    /// chunks.
    /// </summary>
    public class ChunkNotLoadedException : InvalidOperationException {
        public ChunkNotLoadedException() : base() { }
        public ChunkNotLoadedException(string message) : base(message) { }
        public ChunkNotLoadedException(string message, Exception innerException) : base(message, innerException) { }
        public ChunkNotLoadedException(ChunkKey key) : base($"(Chunk {key}) Tried to access an unloaded chunk.") { }
        public ChunkNotLoadedException(ChunkKey key, string message) : base($"(Chunk {key}) {message}") { }
        public ChunkNotLoadedException(ChunkKey key, string message, Exception innerException) : base($"(Chunk {key}) {message}", innerException) { }
    }
}
