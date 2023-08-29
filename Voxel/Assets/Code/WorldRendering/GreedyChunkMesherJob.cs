using System;
using System.Runtime.CompilerServices;
using Unity.Burst;
using Unity.Burst.CompilerServices;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace Atrufulgium.Voxel.WorldRendering {

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
        [WriteOnly]
        internal NativeArray<Vertex> vertices;
        [WriteOnly]
        internal NativeReference<int> verticesLength;
        int vertCount;

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
        // This is an implicit hash table. See the two VertToIndex.. methods.
        internal NativeArray<VertToIndexEntry> vertToIndex;

        static readonly LayerMode[] renderDirections = new[] {
            LayerMode.XUp, LayerMode.YUp, LayerMode.ZUp,
            LayerMode.XDown, LayerMode.YDown, LayerMode.ZDown
        };

        // burst
        // like
        // pls
        int scale => _BurstScaleValue();
        [return: AssumeRange(1, 8)]
        int _BurstScaleValue() => _burstScaleValue;
        int _burstScaleValue;

        int max => _BurstMaxValue();
        [return: AssumeRange(4, 32)]
        int _BurstMaxValue() => _burstMaxValue;
        int _burstMaxValue;

        /// <summary>
        /// The highest layer in the current pass.
        /// </summary>
        int TopLayer => _BurstTopPlayerValue();
        [return: AssumeRange(24, 31)]
        int _BurstTopPlayerValue() => _burstTopLayerValue;
        int _burstTopLayerValue;

        int LoD;


        public void Execute() {
            _burstScaleValue = chunk.VoxelSize;
            _burstMaxValue = chunk.VoxelsPerAxis;
            _burstTopLayerValue = 32 - scale;
            vertCount = 0;
            quadCount = 0;
            LoD = chunk.LoD;

            // TODO: the viewdir part.
            for (int layer = 0; layer < Chunk.ChunkSize; layer += scale) {
                // Unfortunately need to pass currentLayerMode through all
                // calls. Otherwise Burst doesn't see the "oh i can do this
                // compile-time" trick.
                HandleLayer(layer, LayerMode.XUp);
                HandleLayer(layer, LayerMode.XDown);
                HandleLayer(layer, LayerMode.YUp);
                HandleLayer(layer, LayerMode.YDown);
                HandleLayer(layer, LayerMode.ZUp);
                HandleLayer(layer, LayerMode.ZDown);
            }
            verticesLength.Value = vertCount;
            quadsLength.Value = quadCount;
        }

        struct RectData {
            public uint rectStarts;
            public uint noRects;

            public RectData(uint rectStarts, uint noRects) {
                this.rectStarts = rectStarts;
                this.noRects = noRects;
            }
        }

        // Tells Burst to not zero-set stackallocs
        [SkipLocalsInit]
        // Required to make Burst compile-time ALL LayerToCoord calls.
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        unsafe void HandleLayer([AssumeRange(0, 31)] int layer, LayerMode currentLayerMode) {
            // Assuming 32 ChunkSize.
            // This is 256B of stack space at most.
            // "rectStarts" is set to 1 whenever a rectangle should start.
            // Slowly loses bits after initialisation.
            // "noRects" is set to 1 whenever a rectangle is illegal:
            // air, and covered spaces. This supersedes "rectStarts".
            // (And after initialisation, other rects.)

            // Note: data is counted from the MSB = 0.
            // This is because "reverseBits" is kinda tragic and I don't
            // really need it.
            RectData* rectData = stackalloc RectData[max];

            for (int y = 0; y < max; y++) {
                // Get the data over all x SIMD 4 at a time, and then handle
                // it SIMD 32 bits at a time.

                // Container of unrelated data so we can SIMD it.
                uint3 bits = default;
                ref uint covered = ref bits.x;
                ref uint changedMat = ref bits.y;
                ref uint airMat = ref bits.z;

                uint prevMat = ushort.MaxValue;
                for (int x = 0; x < max; x += 4) {
                    bits <<= 4;

                    int4 xvec = x + new int4(0, 1, 2, 3);
                    uint4 mats = GetChunk(xvec, y, layer, currentLayerMode);
                    bool4 covereds = IsCovered(xvec, y, layer, currentLayerMode);
                    uint4 prevMats = mats.wxyz;
                    prevMats.x = prevMat;
                    prevMat = mats.w;
                    // Update the three values with their respective conditions.
                    // The x,y,z,w components correspond to the blocks.
                    // (Note the MSB part that causes a reversed (8,4,2,1))
                    // We will want to make them instead correspond to the
                    // three variables in `bits` we care about.
                    uint4x3 updatesTransposed = new(
                        (uint4)covereds,                    // For "covered"
                        (uint4)(mats != prevMats),          // For "changedMat"
                        (uint4)(mats == 0)                  // For "airMat"
                    );
                    uint3x4 updates = math.transpose(updatesTransposed);
                    bits += 8 * updates.c0 + 4 * updates.c1 + 2 * updates.c2 + updates.c3;
                }

                uint changedCovered = covered ^ (covered >> 1);
                uint noRectsVal = airMat | covered;
                uint changedVal = changedMat | changedCovered;
                changedVal &= ~noRectsVal; // NoRects supersedes

                // We care about MSB, but if max < 32 it's not shifted enough
                changedVal <<= (32 - max);
                noRectsVal <<= (32 - max);

                rectData[y] = new(changedVal, noRectsVal);
            }

            for (int y = 0; y < max; y++) {
                while (rectData[y].rectStarts != 0) {
                    // If yet unhandled, grow as far as possible
                    // horizontally, and then vertically.
                    var rect = GrowRect(rectData, y, layer, currentLayerMode);
                    CreateQuad(rect, layer, currentLayerMode);
                }
            }
        }

        [SkipLocalsInit]
        unsafe void CreateQuad(RectMat rect, [AssumeRange(0, 31)] int layer, LayerMode currentLayerMode) {
            int2* corners = stackalloc int2[4];

            int4 minmax = new int4(rect.x, rect.y, rect.x, rect.y);
            minmax.zw += new int2(rect.width, rect.height);
            minmax *= scale;
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
            int4 quadIndices = default;
            int3* coords = stackalloc int3[4];
            for (int i = 0; i < 4; i++) {
                int2 corner = corners[i];
                coords[i] = LayerToCoord(corner.x, corner.y, layer, currentLayerMode);
            }

            for (int i = 0; i < 4; i++) {
                Vertex vert;

                vert = new(
                    coords[i],
                    rect.material
                );

                // Ignore everything that does not fit with the !=s below. It's
                // extremely hard to achieve in natural gameplay to hit this.
                if (Hint.Likely(!VertToIndexTryGetValue(vert, out var index))) {
                    index = vertCount;
                    if (Hint.Likely(index != ChunkMesher.MAXVERTICES)) {
                        vertices[index] = vert;
                        VertToIndexAdd(vert, (ushort)index);
                        vertCount++;
                    } else {
                        index = 0;
                    }
                }
                quadIndices[i] = index;
            }

            for (int i = 0; i < 4; i++) {
                if (Hint.Likely(quadCount != ChunkMesher.MAXQUADS)) {
                    quads[quadCount] = (ushort)quadIndices[i];
                    quadCount++;
                }
            }
        }

        /// <summary>
        /// Grows the rectangle greedily as much as it can, updating the
        /// <paramref name="rectStarts"/> and <paramref name="noRects"/>
        /// bitfields as needed. Returns the resulting rectangle.
        /// </summary>
        unsafe RectMat GrowRect(
            RectData* rectData,
            [AssumeRange(0, 31)] int y,
            [AssumeRange(0, 31)] int layer,
            LayerMode currentLayerMode
        ) {
            RectData yData = rectData[y];
            int x = FirstBinaryOne(yData.rectStarts);
            Hint.Assume(0 <= x && x < 32); // As otherwise the row is 0 and this is not called.
            // Since a rectangle is starting here already, disallow it from
            // further operations. It may become 0 now.
            uint xmask = AllOnesUpTo(x + 1);
            yData.rectStarts &= ~xmask;
            // May be 32 (actually max)
            int x2 = FirstBinaryOne(yData.rectStarts | (yData.noRects & ~xmask));
            // Represents [x, x + width), exclusive.
            int width = x2 - x;
            rectData[y] = yData;

            // Now that we know the width, see how far up we can go.
            // Do this by masking the bits we care about. If this mask doesn't
            // touch any changes or illegals, we _might_ be fine. We still
            // need to check the material is the same.
            // *Actually two masks, the first bit we care about may be a change.
            uint rectMask = AllOnesUpTo(x2) & ~AllOnesUpTo(x);
            uint smallRectMask = AllOnesUpTo(x2) & ~AllOnesUpTo(x + 1);
            ushort mat = GetChunk(x, y, layer, currentLayerMode);

            // While we're at it, mark the area as done.
            // We can do this with the same mask as before!
            // Also add rectangle starts to the side of the rectangle where
            // applicable, because otherwise it won't generate anymore.
            uint newRectMask = 0x8000_0000 >> x2;
            newRectMask &= AllOnesUpTo(max);

            int y2 = y + 1;
            for (; y2 < max; y2++) {
                RectData y2Data = rectData[y2];
                uint res = (rectMask & y2Data.noRects) | (smallRectMask & y2Data.rectStarts);
                if (res != 0)
                    break;

                ushort mat2 = GetChunk(x, y2, layer, currentLayerMode);
                if (mat != mat2)
                    break;

                // This row is valid, mark done.
                y2Data.rectStarts &= ~rectMask;
                y2Data.rectStarts |= newRectMask & ~y2Data.noRects;
                y2Data.noRects |= rectMask;
                rectData[y2] = y2Data;
            }

            int height = y2 - y;

            return new(
                (byte)x,
                (byte)y,
                (byte)width,
                (byte)height,
                mat
            );
        }

        /// <summary>
        /// <para>
        /// Returns the zero-indexed bit-position of the first set bit.
        /// The MSB is index 0, the LSB is index 31. Returns if 0.
        /// </para>
        /// <para>
        /// This is clamped to [0,<see cref="max"/>], as we need that in every
        /// relevant context.
        /// </para>
        /// </summary>
        [return: AssumeRange(0, 32)]
        int FirstBinaryOne(uint i)
            => math.min(math.select(math.lzcnt(i), 32, i == 0), max);

        /// <summary>
        /// <para>
        /// Returns a uint that, counting from the LSB, is set to <tt>1</tt>
        /// <paramref name="i"/> times, and then set to <tt>0</tt>
        /// 32-<paramref name="i"/> times.
        /// </para>
        /// <para>
        /// Valid inputs are 0..32.
        /// </para>
        /// </summary>
        /// <remarks>
        /// This is <i>excluding</i> index <paramref name="i"/> itself.
        /// As such, <code>FirstBinaryOne(AllOnesUpTo(i+1)) = i</code>.
        /// </remarks>
        uint AllOnesUpTo([AssumeRange(0, 32)] int i)
            => math.select(uint.MaxValue - (uint.MaxValue >> i), uint.MaxValue, i == 32);

        /// <summary>
        /// Whether the X-, Y-, or Z-direction is constant, and which side we
        /// are considering.
        /// </summary>
        enum LayerMode {
            XUp = 0,
            YUp = 1,
            ZUp = 2,
            XDown = 4,
            YDown = 5,
            ZDown = 6
        };

        int3 LayerToCoord(
            [AssumeRange(0, 32)] int x,
            [AssumeRange(0, 32)] int y,
            [AssumeRange(0, 31)] int layer,
            LayerMode currentLayerMode
        ) {
            // Layer is unnormalized to [0,max) and instead has gaps.
            // So do not involve max in this calculation as we live in [0,32).
            // Do note the "scale" to keep on the grid.
            if (currentLayerMode >= LayerMode.XDown) {
                layer = TopLayer - layer;
            }
            var mod = (int)currentLayerMode % 4;
            if (mod == 1)
                return new(y, layer, x);
            if (mod == 2)
                return new(layer, x, y);
            return new(x, y, layer);
        }

        int4x3 LayerToCoord(
            int4 x,
            int4 y,
            [AssumeRange(0, 31)] int layer,
            LayerMode currentLayerMode
        ) {
            if (currentLayerMode >= LayerMode.XDown) {
                layer = TopLayer - layer;
            }
            var mod = (int)currentLayerMode % 4;
            if (mod == 1)
                return new(y, layer, x);
            if (mod == 2)
                return new(layer, x, y);
            return new(x, y, layer);
        }

        ushort GetChunk(
            [AssumeRange(0, 31)] int x,
            [AssumeRange(0, 31)] int y,
            [AssumeRange(0, 31)] int layer,
            LayerMode currentLayerMode
        ) {
            int3 coord = LayerToCoord(x * scale, y * scale, layer, currentLayerMode);
            coord >>= LoD;
            return chunk.GetRaw(coord.x + max * (coord.y + max * coord.z));
        }

        uint4 GetChunk(
            int4 x,
            int4 y,
            [AssumeRange(0, 31)] int layer,
            LayerMode currentLayerMode
        ) {
            int4x3 coord = LayerToCoord(x * scale, y * scale, layer, currentLayerMode);
            coord >>= LoD;
            return chunk.GetRaw(coord.c0 + max * (coord.c1 + max * coord.c2));
        }

        /// <summary>
        /// Whether rects can do anything they want to as they're covered
        /// by something else.
        /// </summary>
        bool IsCovered(
            [AssumeRange(0, 31)] int x,
            [AssumeRange(0, 31)] int y,
            [AssumeRange(0, 31)] int layer,
            LayerMode currentLayerMode
        ) {
            if (Hint.Unlikely(layer == TopLayer))
                return false;
            return GetChunk(x, y, layer + scale, currentLayerMode) > 0;
        }

        bool4 IsCovered(
            int4 x,
            int4 y,
            [AssumeRange(0, 31)] int layer,
            LayerMode currentLayerMode
        ) {
            if (Hint.Unlikely(layer == TopLayer))
                return false;
            return GetChunk(x, y, layer + scale, currentLayerMode) > 0;
        }

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
        readonly struct RectMat : IEquatable<RectMat> {
            public byte x { get => _BurstXValue(); }
            public byte y { get => _BurstYValue(); }
            public byte width { get => _BurstWidthValue(); }
            public byte height { get => _BurstHeightValue(); }
            public readonly ushort material;

            // burst pls part II electric boogaloo
            [return: AssumeRange(0ul, 31ul)]
            byte _BurstXValue() => _burstXValue;
            [return: AssumeRange(0ul, 31ul)]
            byte _BurstYValue() => _burstYValue;
            [return: AssumeRange(0ul, 32ul)]
            byte _BurstWidthValue() => _burstWidthValue;
            [return: AssumeRange(0ul, 32ul)]
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

        // Note that at most four solid blocks can touch one vertex and still
        // contribute to the vertices list. This * 4 < array langth = 128k.
        const int BUCKET_SIZE = 32771;
        const int TABLE_MASK = ChunkMesher.TABLECAPACITY - 1;

        /// <summary>
        /// This does NOT check duplicates.
        /// </summary>
        void VertToIndexAdd(Vertex v, ushort index) {
            int tableIndex = (int)((uint)v.GetHashCode() % BUCKET_SIZE);
            // Except for exceptional cases, very quickly done.
            while (vertToIndex[tableIndex].IsInitialised()) {
                tableIndex = (tableIndex + BUCKET_SIZE) & TABLE_MASK;
            }
            vertToIndex[tableIndex] = new VertToIndexEntry(v, index);
        }

        bool VertToIndexTryGetValue(Vertex v, out int index) {
            int tableIndex = (int)((uint)v.GetHashCode() % BUCKET_SIZE);
            index = 0;
            while (true) {
                VertToIndexEntry entry = vertToIndex[tableIndex];
                if (Hint.Likely(!entry.IsInitialised()))
                    return false;
                if (Hint.Unlikely(entry.TryVertexMatches(v, out var uindex))) {
                    index = uindex;
                    return true;
                }
                tableIndex = (tableIndex + BUCKET_SIZE) & TABLE_MASK;
            }
        }

        internal readonly struct VertToIndexEntry {
            readonly Vertex vert;
            readonly ushort index;
            readonly bool initialised;
            public VertToIndexEntry(Vertex vert, ushort index) {
                this.vert = vert;
                this.index = index;
                initialised = true;
            }

            public bool TryVertexMatches(Vertex other, out ushort index) {
                index = 0;
                if (other == vert) {
                    index = this.index;
                    return true;
                }
                return false;
            }

            public bool IsInitialised() => initialised;

            public override int GetHashCode() => vert.GetHashCode();
        }
    }
}