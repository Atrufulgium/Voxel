using System;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace Atrufulgium.Voxel.Base {

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, OptimizeFor = OptimizeFor.Performance)]
    struct GreedyChunkMesherJob : IGreedyMeshJob {

        Chunk IGreedyMeshJob.Chunk { get => chunk; set => chunk = value; }
        [ReadOnly]
        Chunk chunk;

        float3 IGreedyMeshJob.ViewDir { get => viewDir; set => viewDir = value; }
        [ReadOnly]
        float3 viewDir;

        NativeList<Vertex> IGreedyMeshJob.Vertices { get => vertices; set => vertices = value; }
        [WriteOnly]
        NativeList<Vertex> vertices;

        NativeList<ushort> IGreedyMeshJob.Quads { get => quads; set => quads = value; }
        [WriteOnly]
        NativeList<ushort> quads;

        NativeParallelHashMap<Vertex, int> IGreedyMeshJob.VertToIndex { get => vertToIndex; set => vertToIndex = value; }
        NativeParallelHashMap<Vertex, int> vertToIndex;

        static readonly LayerMode[] renderDirections = new[] {
            LayerMode.XUp, LayerMode.YUp, LayerMode.ZUp,
            LayerMode.XDown, LayerMode.YDown, LayerMode.ZDown
        };

        LayerMode currentLayerMode;
        int scale;
        int max;

        public void Execute() {
            scale = chunk.VoxelSize;
            max = chunk.VoxelsPerAxis;

            for (int i = 0; i < renderDirections.Length; i++) {
                currentLayerMode = renderDirections[i];
                // TODO: the viewdir part.
                for (int layer = 0; layer < Chunk.ChunkSize; layer += chunk.VoxelSize) {
                    HandleLayer(layer);
                }
            }
        }

        unsafe void HandleLayer(int layer) {
            // Assuming 32 ChunkSize.
            // This is 128B of stack space at most.
            // This keeps track of every rect we've written in order to not
            // have overlapping rectangles (or way too many).
            BitField32* handled = stackalloc BitField32[(int)math.ceil(max * max / 32f)];
            // Init to false, except for air and covereds.
            for (int y = 0; y < max; y += 1) {
                for (int x = 0; x < max; x += 1) {
                    bool init = false;
                    init |= GetChunk(x, y, layer) == 0;
                    init |= IsCovered(x, y, layer);
                    SetHandled(handled, x, y, init);
                }
            }

            for (int y = 0; y < max; y += 1) {
                for (int x = 0; x < max; x += 1) {
                    // If yet unhandled, grow as far as possible
                    // horizontally, and then vertically.
                    if (!GetHandled(handled, x, y)) {
                        var rect = GrowRect(handled, x, y, layer);
                        CreateQuad(rect, layer);
                    }
                }
            }
        }

        unsafe void CreateQuad(RectMat rect, int layer) {
            int2* corners = stackalloc int2[4];

            // The order matters depending on which side of course.
            if (currentLayerMode < LayerMode.XDown) {
                corners[0] = new(rect.x + rect.width, rect.y);
                corners[1] = new(rect.x + rect.width, rect.y + rect.height);
                corners[2] = new(rect.x, rect.y + rect.height);
                corners[3] = new(rect.x, rect.y);
                // Want the voxels to have volume, so shift up.
                layer += scale;
            } else {
                corners[3] = new(rect.x + rect.width, rect.y);
                corners[2] = new(rect.x + rect.width, rect.y + rect.height);
                corners[1] = new(rect.x, rect.y + rect.height);
                corners[0] = new(rect.x, rect.y);
            }

            // Register verts if they don't exist, and add to the quads list.
            for (int i = 0; i < 4; i++) {
                int2 corner = corners[i];
                Vertex vert;

                vert = new(
                    LayerToCoord(corner.x * scale, corner.y * scale, layer),
                    rect.material
                );

                if (!vertToIndex.TryGetValue(vert, out int index)) {
                    index = vertToIndex.Count();
                    vertices.Add(vert);
                    vertToIndex.Add(vert, index);
                }

                quads.Add((ushort)index);
            }
        }

        /// <summary>
        /// Grows the rectangle greedily as much as it can, updating the
        /// <paramref name="handled"/> bitfields as needed. Returns the
        /// resulting rectangle.
        /// </summary>
        unsafe RectMat GrowRect(BitField32* handled, int x, int y, int layer) {
            int x2 = x;
            int mat = GetChunk(x, y, layer);
            for (; x2 < max; x2++) {
                bool isCovered = IsCovered(x2, y, layer);
                if (isCovered)
                    continue; // Covered allows anything.

                bool isHandled = GetHandled(handled, x2, y);
                if (isHandled) {
                    // Don't overlap rects
                    break;
                }

                int mat2 = GetChunk(x2, y, layer);
                if (mat != mat2) {
                    break;
                }
            }
            // Represents [x, x + width).
            int width = x2 - x;

            // Now that we know the width, see how far up we can go.
            int y2 = y;
            for (; y2 < max; y2++) {
                bool validRow = true;

                // Unfortunate near-copypasta of the above
                for (x2 = x; x2 < x + width; x2++) {
                    bool isCovered = IsCovered(x2, y2, layer);
                    if (isCovered)
                        continue;

                    bool isHandled = GetHandled(handled, x2, y2);
                    if (isHandled) {
                        validRow = false;
                        break;
                    }
                    int mat2 = GetChunk(x2, y2, layer);
                    if (mat != mat2) {
                        validRow = false;
                        break;
                    }
                }

                if (!validRow) {
                    break;
                }
            }
            int height = y2 - y;

            // Mark the area as done
            for (y2 = y; y2 < y + height; y2++) {
                for (x2 = x; x2 < x + width; x2++)
                    SetHandled(handled, x2, y2, true);
            }

            return new() {
                x = (byte)x,
                y = (byte)y,
                width = (byte)width,
                height = (byte)height,
                material = (byte)mat
            };
        }

        /// <summary>
        /// Whether rects can do anything they want to as they're covered
        /// by something else.
        /// </summary>
        bool IsCovered(int x, int y, int layer) {
            if (layer >= max - 1)
                return false;
            return GetChunk(x, y, layer + 1) > 0;
        }

        unsafe bool GetHandled(BitField32* handled, int x, int y) {
            int unrolledIndex = y * max + x;
            return handled[unrolledIndex / 32].IsSet(unrolledIndex % 32);
        }

        unsafe void SetHandled(BitField32* handled, int x, int y, bool value) {
            int unrolledIndex = y * max + x;
            handled[unrolledIndex / 32].SetBits(unrolledIndex % 32, value);
        }

        /// <summary>
        /// Whether the X-, Y-, or Z-direction is constant, and which side we
        /// are considering.
        /// </summary>
        enum LayerMode { 
            XUp = 0,
            YUp = 1,
            ZUp = 2,
            XDown = 3,
            YDown = 4,
            ZDown = 5
        };

        int3 LayerToCoord(int x, int y, int layer) {
            if (currentLayerMode >= LayerMode.XDown) {
                layer = (max - 1) - layer;
            }
            int3 ret = currentLayerMode switch {
                LayerMode.XUp or LayerMode.XDown => new(x, y, layer),
                LayerMode.YUp or LayerMode.YDown => new(y, layer, x),
                LayerMode.ZUp or LayerMode.ZDown => new(layer, x, y),
                _ => default
            };
            return ret;
        }

        ushort GetChunk(int x, int y, int layer)
            => chunk[LayerToCoord(x * scale, y * scale, layer)];

        /// <summary>
        /// <para>
        /// Contains the properties of a rect inside a layer in a chunk,
        /// together with its material. Equality ignores the material.
        /// </para>
        /// <para>
        /// Specifically, represents that <tt>[x,x+width)×[y,y+height)</tt>
        /// is made of <tt>material</tt>.
        /// </para>
        /// </summary>
        /// <remarks>
        /// Assumes <see cref="Chunk.ChunkSize"/> is at most 256.
        /// </remarks>
        struct RectMat : IEquatable<RectMat> {
            public byte x;
            public byte y;
            public byte width;
            public byte height;
            public ushort material;

            public bool Equals(RectMat other)
                => x == other.x
                && y == other.y
                && width == other.width
                && height == other.height;
        }
    }
}