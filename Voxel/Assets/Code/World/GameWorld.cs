using System;
using System.Collections.Generic;
using Unity.Mathematics;

namespace Atrufulgium.Voxel.World {
    /// <summary>
    /// This class contains all data of the current world.
    /// </summary>
    public class GameWorld : IDisposable {

        readonly Dictionary<ChunkKey, Chunk> loadedChunks = new();

        /// <summary>
        /// This event is called whenever chunk information has been changed.
        /// This includes the initial load.
        /// </summary>
        public event ChunkUpdatedEventHandler ChunkUpdated;
        public delegate void ChunkUpdatedEventHandler(object sender, ChunkUpdatedEventArgs e);
        void OnChunkUpdated(ChunkKey k, Chunk c) {
            ChunkUpdated?.Invoke(this, new() { Chunk = c, Key = k });
        }

        /// <summary>
        /// <para>
        /// Loads a chunk for later use. When this is loaded it automatically
        /// fires <see cref="ChunkUpdated"/>. You can also try to get it with
        /// <see cref="TryGetChunk(ChunkKey, out Chunk, int)"/>.
        /// </para>
        /// <para>
        /// This (silently) does nothing when the chunk already exists.
        /// </para>
        /// </summary>
        public void LoadChunk(ChunkKey key) {
            if (ChunkIsLoadedOrLoading(key))
                return;
            WorldGen.RunAsynchronously<WorldGen>(key, (key, 0));
        }

        /// <summary>
        /// Creates or overwrites an existing chunk with a given chunk.
        /// </summary>
        public void SetChunk(ChunkKey key, Chunk chunk) {
            if (loadedChunks.ContainsKey(key)) {
                loadedChunks[key].Dispose();
                loadedChunks[key] = chunk;
            } else {
                loadedChunks.Add(key, chunk);
            }
            OnChunkUpdated(key, chunk);
        }

        /// <summary>
        /// If a chunk is loaded, returns true and returns its contents (with
        /// an optional given LoD). Otherwise, returns false.
        /// </summary>
        /// <param name="LoD">
        /// The optional LoD of the returned chunk. May be <tt>-1</tt> to leave
        /// the existing LoD as-is.
        /// </param>
        public bool TryGetChunk(ChunkKey key, out Chunk value, int LoD = -1) {
            if (loadedChunks.TryGetValue(key, out Chunk loadedChunk)) {
                if (LoD == -1 || loadedChunk.LoD == LoD)
                    value = loadedChunk;
                else
                    value = loadedChunk.WithLoD(LoD);
                return true;
            } else {
                value = default;
                return false;
            }
        }

        public bool ChunkIsLoaded(ChunkKey key)
            => loadedChunks.ContainsKey(key);

        public bool ChunkIsLoadedOrLoading(ChunkKey key)
            => loadedChunks.ContainsKey(key) || WorldGen.JobExists(key);

        /// <summary>
        /// Sets a single voxel in the world.
        /// </summary>
        /// <exception cref="ChunkNotLoadedException"></exception>
        public void Set(int3 position, ushort material) {
            ChunkKey key = ChunkKey.FromWorldPos(position, out int3 chunkPos);
            if (!loadedChunks.TryGetValue(key, out Chunk chunk)) {
                throw new ChunkNotLoadedException(key);
            }
            chunk[chunkPos] = material;
            OnChunkUpdated(key, chunk);
        }

        /// <summary>
        /// <para>
        /// Attempts to get the voxel value of a position in the world.
        /// </para>
        /// <para>
        /// If the chunk does not exist, it returns <tt>false</tt> and outputs
        /// air. Depending on your application, this may be fine.
        /// </para>
        /// </summary>
        public bool TryGet(int3 position, out ushort material) {
            ChunkKey key = ChunkKey.FromWorldPos(position, out int3 chunkPos);
            if (loadedChunks.TryGetValue(key, out Chunk chunk)) {
                material = chunk[chunkPos];
                return true;
            }
            material = 0;
            return false;
        }

        public void Dispose() {
            foreach (var (_, chunk) in loadedChunks)
                chunk.Dispose();
        }
    }

    public class ChunkUpdatedEventArgs : EventArgs {
        public Chunk Chunk { get; set; }
        public ChunkKey Key { get; set; }
    }
}