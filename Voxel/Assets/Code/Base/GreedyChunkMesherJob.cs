using System;
using Unity.Burst;
using Unity.Burst.CompilerServices;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace Atrufulgium.Voxel.Base {

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE1006:Naming Styles", Justification = "Burst!")]
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
        /// <remarks>
        /// Vertex count can be read from <see cref="vertToIndex"/>'s Count().
        /// </remarks>
        [WriteOnly]
        internal NativeArray<Vertex> vertices;

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
        internal NativeArray<ushort> quads;
        /// <summary>
        /// The part [0,quadCount) part of <see cref="quads"/> is filled with
        /// sensible data, the rest is old or garbage.
        /// </summary>
        /// <remarks>
        /// Don't read from this inside the job.
        /// </remarks>
        [WriteOnly]
        internal NativeReference<int> quadsLength;
        int quadCount;

        /// <summary>
        /// <para>
        /// A conversion from vertex to index inside the vertices list in the
        /// current GetMesh call.
        /// </para>
        /// <para>
        /// This is needed to weld together vertices of the same material.
        /// </para>
        /// </summary>
        // Can't whip up a datastructure that beats this comfortably in an
        // afternoon. Let's leave it at this, how disappointing that may be.
        internal NativeParallelHashMap<Vertex, int> vertToIndex;

        static readonly LayerMode[] renderDirections = new[] {
            LayerMode.XUp, LayerMode.YUp, LayerMode.ZUp,
            LayerMode.XDown, LayerMode.YDown, LayerMode.ZDown
        };

        LayerMode currentLayerMode;

        // burst
        // like
        // pls
        int scale => _BurstScaleValue();
        [return:AssumeRange(0,31)]
        int _BurstScaleValue() => _burstScaleValue;
        int _burstScaleValue;

        int max => _BurstMaxValue();
        [return:AssumeRange(0,31)]
        int _BurstMaxValue() => _burstMaxValue;
        int _burstMaxValue;

        /// <summary>
        /// The highest layer in the current pass.
        /// </summary>
        int TopLayer => _BurstTopPlayerValue();
        [return:AssumeRange(0,31)]
        int _BurstTopPlayerValue() => _burstTopLayerValue;
        int _burstTopLayerValue;


        public void Execute() {
            _burstScaleValue = chunk.VoxelSize;
            _burstMaxValue = chunk.VoxelsPerAxis;
            _burstTopLayerValue = 32 - scale;
            quadCount = 0;

            for (int i = 0; i < renderDirections.Length; i++) {
                currentLayerMode = renderDirections[i];
                // TODO: the viewdir part.
                for (int layer = 0; layer < Chunk.ChunkSize; layer += scale) {
                    HandleLayer(layer);
                }
            }
            quadsLength.Value = quadCount;
        }

        [SkipLocalsInit] // Tells Burst to not zero-set stackallocs
        unsafe void HandleLayer([AssumeRange(0,31)] int layer) {
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
                    if (Hint.Unlikely(!GetHandled(handled, x, y))) {
                        var rect = GrowRect(handled, x, y, layer);
                        CreateQuad(rect, layer);
                    }
                }
            }
        }

        [SkipLocalsInit]
        unsafe void CreateQuad(RectMat rect, [AssumeRange(0,31)] int layer) {
            int2* corners = stackalloc int2[4];

            int4 minmax = new(rect.x, rect.y, rect.x, rect.y);
            minmax.zw += new int2(rect.width, rect.height);
            // The order matters depending on which side of course.
            corners[0] = minmax.zy; //new(rect.x + rect.width, rect.y);
            corners[1] = minmax.zw; //new(rect.x + rect.width, rect.y + rect.height);
            corners[2] = minmax.xw; //new(rect.x, rect.y + rect.height);
            corners[3] = minmax.xy; //new(rect.x, rect.y);
            if (currentLayerMode < LayerMode.XDown) {
                // Want the voxels to have volume, so shift up.
                layer += scale;
            } else {
                // Make it so that corners[0] <-> corners[3] and corners[1] <-> corners[2]
                // Luckily int2 and int4 have predictable layout lol
                int4* hacky = (int4*)corners;
                int4 temp = hacky[0];
                hacky[0] = hacky[1].zwxy;
                hacky[1] = temp.zwxy;
            }

            // Register verts if they don't exist, and add to the quads list.
            for (int i = 0; i < 4; i++) {
                int2 corner = corners[i];
                Vertex vert;

                vert = new(
                    LayerToCoord(corner.x * scale, corner.y * scale, layer),
                    rect.material
                );

                // Ignore everything that does not fit. It's extremely hard to
                // achieve in natural gameplay to hit this.
                if (Hint.Likely(!vertToIndex.TryGetValue(vert, out var index))) {
                    index = vertToIndex.Count();
                    if (Hint.Likely(index != ChunkMesher.MAXVERTICES)) {
                        vertices[index] = vert;
                        vertToIndex.Add(vert, (ushort)index);
                    } else {
                        index = 0;
                    }
                }

                if (Hint.Likely(quadCount != ChunkMesher.MAXQUADS)) {
                    quads[quadCount] = (ushort)index;
                    quadCount++;
                }
            }
        }

        /// <summary>
        /// Grows the rectangle greedily as much as it can, updating the
        /// <paramref name="handled"/> bitfields as needed. Returns the
        /// resulting rectangle.
        /// </summary>
        unsafe RectMat GrowRect(BitField32* handled,
            [AssumeRange(0,31)] int x,
            [AssumeRange(0,31)] int y,
            [AssumeRange(0,31)] int layer
        ) {
            int x2 = x;
            int mat = GetChunk(x, y, layer);
            for (; x2 < max; x2++) {
                bool isHandled = GetHandled(handled, x2, y);
                if (isHandled) {
                    // Don't overlap rects or air/covereds
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

            return new(
                (byte)x,
                (byte)y,
                (byte)width,
                (byte)height,
                (ushort)mat
            );
        }

        /// <summary>
        /// Whether rects can do anything they want to as they're covered
        /// by something else.
        /// </summary>
        bool IsCovered(
            [AssumeRange(0,31)] int x,
            [AssumeRange(0,31)] int y,
            [AssumeRange(0,31)] int layer
        ) {
            if (Hint.Unlikely(layer == TopLayer))
                return false;
            return GetChunk(x, y, layer + scale) > 0;
        }

        unsafe bool GetHandled(
            BitField32* handled,
            [AssumeRange(0,31)] int x,
            [AssumeRange(0,31)] int y
        ) {
            int unrolledIndex = y * max + x;
            return handled[unrolledIndex / 32].IsSet(unrolledIndex % 32);
        }

        unsafe void SetHandled(
            BitField32* handled,
            [AssumeRange(0,31)] int x,
            [AssumeRange(0,31)] int y,
            bool value
        ) {
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

        int3 LayerToCoord(
            [AssumeRange(0,32)] int x,
            [AssumeRange(0,32)] int y,
            [AssumeRange(0,31)] int layer
        ) {
            Hint.Assume(currentLayerMode >= LayerMode.XUp && currentLayerMode <= LayerMode.ZDown);
            // Layer is unnormalized to [0,max) and instead has gaps.
            // So do not involve max in this calculation as we live in [0,32).
            // Do note the "scale" to keep on the grid.
            if (currentLayerMode >= LayerMode.XDown) {
                layer = TopLayer - layer;
            }
            int3 ret = new(x, y, layer);
            var mod = (int)currentLayerMode % 3;
            if (mod == 1)
                ret = ret.yzx;
            if (mod == 2)
                ret = ret.zxy;
            return ret;
        }

        ushort GetChunk(
            [AssumeRange(0,31)] int x,
            [AssumeRange(0,31)] int y,
            [AssumeRange(0,31)] int layer
        ) => chunk[LayerToCoord(x * scale, y * scale, layer)];

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
        readonly struct RectMat : IEquatable<RectMat> {
            public byte x { get => _BurstXValue(); }
            public byte y { get => _BurstYValue(); }
            public byte width { get => _BurstWidthValue(); }
            public byte height { get => _BurstHeightValue(); }
            public readonly ushort material;

            // burst pls part II electric boogaloo
            [return: AssumeRange(0ul,31ul)]
            byte _BurstXValue() => _burstXValue;
            [return: AssumeRange(0ul,31ul)]
            byte _BurstYValue() => _burstYValue;
            [return: AssumeRange(0ul,32ul)]
            byte _BurstWidthValue() => _burstWidthValue;
            [return: AssumeRange(0ul,32ul)]
            byte _BurstHeightValue() => _burstHeightValue;
            readonly byte _burstXValue;
            readonly byte _burstYValue;
            readonly byte _burstWidthValue;
            readonly byte _burstHeightValue;

            public RectMat(byte x, byte y, byte width, byte height, ushort material) {
                _burstXValue = x;
                _burstYValue = y;
                _burstWidthValue = width;
                _burstHeightValue = height;
                this.material = material;
            }

            public bool Equals(RectMat other)
                => x == other.x
                && y == other.y
                && width == other.width
                && height == other.height;
        }
    }
}