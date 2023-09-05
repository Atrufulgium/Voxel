using Atrufulgium.Voxel.Collections;
using Atrufulgium.Voxel.World;
using System;
using System.Collections.Generic;
using Unity.Mathematics;

namespace Atrufulgium.Voxel.WorldRendering {
    /// <summary>
    /// Stores a collection of chunks. The difference with
    /// <see cref="GameWorld"/> is that this only contains the chunks
    /// that are relevant for rendering.
    /// </summary>
    public class RenderWorld : IDisposable {

        /// <summary>
        /// In order to properly order chunk rendering and requests for a
        /// large radius, a center point is needed.
        /// </summary>
        public float3 CenterPoint { get; set; }
        /// <summary>
        /// How many chunks in each direction to load.
        /// </summary>
        public int RenderDistance { get; set; }

        /// <summary>
        /// All chunks stored in this world. Note that all of these chunks are
        /// the live chunks, so no disposal necessary here.
        /// </summary>
        readonly Dictionary<ChunkKey, Chunk> allChunks = new();
        /// <summary>
        /// All chunks that have been modified whose modifications have only
        /// been applied to the voxel array and not the rendering yet.
        /// </summary>
        readonly PriorityQueueSet<ChunkKey, float> dirtyChunks = new();

        readonly GameWorld world;

        public RenderWorld(GameWorld world) {
            world.ChunkUpdated += HandleChunkUpdate;
            this.world = world;
        }

        void HandleChunkUpdate(object sender, ChunkUpdatedEventArgs e) {
            Chunk chunk = e.Chunk;
            ChunkKey key = e.Key;
            float dist = Distance(key);
            //if (dist > RenderDistance)
            //    return;

            if (allChunks.ContainsKey(key))
                allChunks[key] = chunk;
            else
                allChunks.Add(key, chunk);
            dirtyChunks.Enqueue(key, dist);
        }

        /// <summary>
        /// Removes a given chunk from rendering.
        /// </summary>
        public void RemoveChunk(ChunkKey key) {
            if (allChunks.ContainsKey(key))
                allChunks.Remove(key);
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
            dirtyChunks.Enqueue(key, 0);
        }

        /// <summary>
        /// The distance (in chunks) between a point and chunk center.
        /// </summary>
        float Distance(ChunkKey key) => math.distance(key.Worldpos + 16, CenterPoint) / 32;

        /// <summary>
        /// Iterate over all ChunkKeys of chunks being rendered.
        /// </summary>
        public IEnumerable<ChunkKey> RenderedChunks()
            => Enumerators.EnumerateCopy(allChunks.Keys);

        public void Dispose() {
            world.ChunkUpdated -= HandleChunkUpdate;
        }
    }
}
