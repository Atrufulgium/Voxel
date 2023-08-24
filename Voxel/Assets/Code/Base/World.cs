using Atrufulgium.Voxel.Collections;
using System;
using System.Collections.Generic;
using Unity.Mathematics;

namespace Atrufulgium.Voxel.Base {
    /// <summary>
    /// Stores a collection of chunks.
    /// </summary>
    // TODO: Consider LoD in this class.
    public class World : IDisposable {

        static readonly Dictionary<int, World> knownWorlds = new();

        /// <summary>
        /// Creates a new world with a unique id.
        /// </summary>
        public World(int id) {
            if (knownWorlds.ContainsKey(id))
                throw new ArgumentException($"A world with id {id} already exists.", nameof(id));

            knownWorlds.Add(id, this);
        }

        /// <summary>
        /// All chunks stored in this world.
        /// </summary>
        readonly Dictionary<ChunkKey, Chunk> allChunks = new();
        /// <summary>
        /// All chunks that have been modified whose modifications have only
        /// been applied to the voxel array and not the world yet.
        /// </summary>
        readonly PriorityQueueSet<ChunkKey, float> dirtyChunks = new();

        /// <summary>
        /// Sets a position in this region to a voxel material.
        /// </summary>
        public void Set(int3 position, ushort material) {
            ChunkKey key = ChunkKey.FromWorldPos(position, out int3 chunkPos);
            if (allChunks.TryGetValue(key, out Chunk chunk)) {
                chunk[chunkPos] = material;
                // No need to reupdate the Dictionary as we're changing a
                // value of an underlying NativeCollection.
            } else {
                chunk = new(0);
                chunk[chunkPos] = material;
                allChunks.Add(key, chunk);
            }
            dirtyChunks.Enqueue(key, math.lengthsq(key.Worldpos));
        }

        /// <summary>
        /// Sets a chunk location to be a given chunk. The existing chunk will
        /// be forgotten, so do not access its native contents afterwards.
        /// </summary>
        public void SetChunk(ChunkKey key, Chunk chunk) {
            if (allChunks.TryGetValue(key, out Chunk oldChunk)) {
                oldChunk.Dispose();
                allChunks[key] = chunk;
            } else {
                allChunks.Add(key, chunk);
            }
            dirtyChunks.Enqueue(key, math.lengthsq(key.Worldpos));
        }

        /// <summary>
        /// Sets the chunk location to be a chunk made of a single material.
        /// </summary>
        public void SetChunk(ChunkKey key, ushort material) {
            if (!allChunks.TryGetValue(key, out Chunk chunk)) {
                chunk = new(0);
            }
            foreach (var (pos, _) in chunk) {
                chunk[pos] = material;
            }
            dirtyChunks.Enqueue(key, math.lengthsq(key.Worldpos));
        }

        /// <summary>
        /// Attempts to get the voxel value of a position in the world.
        /// </summary>
        public bool TryGet(int3 position, out ushort material) {
            ChunkKey key = ChunkKey.FromWorldPos(position, out int3 chunkPos);
            if (allChunks.TryGetValue(key, out Chunk chunk)) {
                material = chunk[chunkPos];
                return true;
            }
            material = 0;
            return false;
        }

        /// <summary>
        /// If there's still dirty chunk, returns one.
        /// </summary>
        public bool TryGetDirtyChunk(out ChunkKey key, out Chunk chunk) {
            if (dirtyChunks.TryDequeue(out key)) {
                chunk = allChunks[key];
                return true;
            }
            chunk = default;
            return false;
        }

        /// <summary>
        /// Marks a given chunk to be dirty without any reason.
        /// </summary>
        public void MarkDirty(ChunkKey key) {
            dirtyChunks.Enqueue(key, math.lengthsq(key.Worldpos));
        }

        /// <summary>
        /// Returns whether a given world ID exists.
        /// </summary>
        public static bool WorldExists(int id)
            => knownWorlds.ContainsKey(id);

        /// <summary>
        /// If the given world exists, returns it.
        /// </summary>
        public static bool TryGetWorld(int id, out World world)
            => knownWorlds.TryGetValue(id, out world);

        /// <summary>
        /// Removes an existing world.
        /// </summary>
        public static void RemoveWorld(int id) {
            if (!TryGetWorld(id, out World world))
                throw new ArgumentException($"World id {id} does not exist and cannot be removed.");

            knownWorlds.Remove(id);
            world.Dispose();
        }

        public void Dispose() {
            foreach(var (_, chunk) in allChunks) {
                chunk.Dispose();
            }
        }
    }
}
