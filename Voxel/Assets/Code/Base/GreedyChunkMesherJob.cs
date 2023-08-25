using System;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace Atrufulgium.Voxel.Base {

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, OptimizeFor = OptimizeFor.Performance)]
    struct GreedyChunkMesherJob : IJob {

        /// <summary>
        /// The chunk to turn into a mesh. Of course, this takes into account
        /// the chunks LoD.
        /// </summary>
        [ReadOnly]
        internal Chunk chunk;

        /// <summary>
        /// Either a normalised vector representing the camera direction in the
        /// chunk's model space, or the zero vector. In the former case, all
        /// invisible faces gets culled, in the latter case no culling happens.
        /// A camera looking at the positive z direction has a viewDir (0,0,1).
        /// </summary>
        [ReadOnly]
        internal float3 viewDir;

        /// <summary>
        /// All verts in the current GetMesh call.
        /// </summary>
        [WriteOnly]
        internal NativeList<Vertex> vertices;

        /// <summary>
        /// I'd call it "tris" if my topology wasn't quads. The indices of the
        /// four corners of quads inside the vertices list in the current
        /// GetMesh call.
        /// </summary>
        /// <remarks>
        /// ushorts are *not* sufficient. You can construct a 28x28x28 3d
        /// checkerboard pattern of "air / non-air" with no two diagonally
        /// touching non-air blocks of the same material. However, this
        /// requires 11k well-placed blocks (in a "place two break one" way)
        /// out of the maximum of 16k blocks that can induce 6 verts.
        /// Anyone who achieves that *deserves* the broken physics and
        /// graphics they desire.
        /// </remarks>
        [WriteOnly]
        internal NativeList<ushort> quads;

        /// <summary>
        /// <para>
        /// A conversion from vertex to index inside the vertices list in the
        /// current GetMesh call.
        /// </para>
        /// <para>
        /// This is needed to weld together vertices of the same material.
        /// </para>
        /// </summary>
        internal NativeParallelHashMap<Vertex, int> vertToIndex;

        static readonly LayerMode[] renderDirections = new[] {
            LayerMode.XUp, LayerMode.YUp, LayerMode.ZUp,
            LayerMode.XDown, LayerMode.YDown, LayerMode.ZDown
        };

        LayerMode currentLayerMode;
        int scale;
        int max;
        /// <summary>
        /// The highest layer in the current pass.
        /// </summary>
        int TopLayer => 32 - scale;

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
            for (int y = 0; y < max; y++) {
                for (int x = 0; x < max; x++) {
                    bool init = GetChunk(x, y, layer) == 0;
                    init |= IsCovered(x, y, layer);
                    SetHandled(handled, x, y, init);
                }
            }

            for (int y = 0; y < max; y++) {
                for (int x = 0; x < max; x++) {
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
            if (layer >= TopLayer)
                return false;
            return GetChunk(x, y, layer + scale) > 0;
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
            // Layer is unnormalized to [0,max) and instead has gaps.
            // So do not involve max in this calculation as we live in [0,32).
            // Do note the "scale" to keep on the grid.
            if (currentLayerMode >= LayerMode.XDown) {
                layer = TopLayer - layer;
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