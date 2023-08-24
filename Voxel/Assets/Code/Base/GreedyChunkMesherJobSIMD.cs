using System;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace Atrufulgium.Voxel.Base {

    /// <summary>
    /// SIMD mirror of <see cref="GreedyChunkMesherJob"/>.
    /// For documentation, browse that file. Here comments are only about
    /// the new stuff.
    /// </summary>
    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, OptimizeFor = OptimizeFor.Performance)]
    struct GreedyChunkMesherJobSIMD : IGreedyMeshJob {

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
                for (int layer = 0; layer < Chunk.ChunkSize; layer += chunk.VoxelSize) {
                    HandleLayerSIMD(layer);
                }
            }
        }

        unsafe void HandleLayerSIMD(int layer) {
            BitField32* handled = stackalloc BitField32[(int)math.ceil(max * max / 32f)];
            for (int y = 0; y < max; y += 1) {
                for (int x = 0; x < max; x += 4) {
                    int4 xvec = x + new int4(0,1,2,3);
                    bool4 init = GetChunkSIMD(xvec, y, layer) == 0;
                    init |= IsCoveredSIMD(xvec, y, layer);
                    SetHandledSIMD(handled, x, y, init);
                }
            }

            for (int y = 0; y < max; y += 1) {
                for (int x = 0; x < max; x += 1) {
                    if (!GetHandled(handled, x, y)) {
                        var rect = GrowRectSIMD(handled, x, y, layer);
                        CreateQuad(rect, layer);
                    }
                }
            }
        }

        unsafe void CreateQuad(RectMat rect, int layer) {
            int2* corners = stackalloc int2[4];
            if (currentLayerMode < LayerMode.XDown) {
                corners[0] = new(rect.x + rect.width, rect.y);
                corners[1] = new(rect.x + rect.width, rect.y + rect.height);
                corners[2] = new(rect.x, rect.y + rect.height);
                corners[3] = new(rect.x, rect.y);
                layer += scale;
            } else {
                corners[3] = new(rect.x + rect.width, rect.y);
                corners[2] = new(rect.x + rect.width, rect.y + rect.height);
                corners[1] = new(rect.x, rect.y + rect.height);
                corners[0] = new(rect.x, rect.y);
            }
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

        unsafe RectMat GrowRectSIMD(BitField32* handled, int x, int y, int layer) {
            int x2 = x;
            int mat = GetChunk(x, y, layer);
            for (; x2 < max; x2 += 4) {
                int4 x2vec = x2 + new int4(0,1,2,3);
                bool4 isCovered = IsCoveredSIMD(x2vec, y, layer);
                // Don't need after max, and isCovered is checked
                // with both, so hacky: mask the after part in isCovered.
                isCovered |= x2vec >= max;
                if (math.all(isCovered))
                    continue;
                bool4 isHandled = GetHandledSIMD(handled, x2, y);
                isHandled &= !isCovered;
                if (math.any(isHandled)) {
                    // Note that the problem can be any of the four.
                    // Current x2 assumes it's the first one.
                    if (!isHandled.x && isHandled.y)
                        x2++;
                    else if (isHandled.z)
                        x2 += 2;
                    else if (isHandled.w)
                        x2 += 3;
                    break;
                }
                int4 mat2 = GetChunkSIMD(x2vec, y, layer);
                bool4 wrongMat = mat != mat2;
                wrongMat &= !isCovered;
                if (math.any(wrongMat)) {
                    if (!wrongMat.x && wrongMat.y)
                        x2++;
                    else if (wrongMat.z)
                        x2 += 2;
                    else if (wrongMat.w)
                        x2 += 3;
                    break;
                }
            }
            // If everything succeeds, x2 might still go beyond `max` (e.g.
            // start at 1, go in 4-increments, end up at 33 instead of 32).
            x2 = math.min(x2, max);
            int width = x2 - x;
            int y2 = y;
            for (; y2 < max; y2++) {
                bool validRow = true;
                for (x2 = x; x2 < x + width; x2 += 4) {
                    int4 x2vec = x2 + new int4(0, 1, 2, 3);
                    bool4 isCovered = IsCoveredSIMD(x2vec, y2, layer);
                    isCovered |= x2vec >= x + width;
                    if (math.all(isCovered))
                        continue;
                    bool4 isHandled = GetHandledSIMD(handled, x2, y2);
                    isHandled &= !isCovered;
                    if (math.any(isHandled)) {
                        validRow = false;
                        break;
                    }
                    int4 mat2 = GetChunkSIMD(x2vec, y2, layer);
                    bool4 wrongMat = mat != mat2;
                    wrongMat &= !isCovered;
                    if (math.any(wrongMat)) {
                        validRow = false;
                        break;
                    }
                }

                if (!validRow) {
                    break;
                }
            }
            int height = y2 - y;
            for (y2 = y; y2 < y + height; y2++) {
                for (x2 = x; x2 < x + width; x2 += 4) {
                    int4 x2vec = x2 + new int4(0, 1, 2, 3);
                    bool4 isHandled = x2vec < x + width;
                    OrHandledSIMD(handled, x2, y2, isHandled);
                }
            }
            return new() {
                x = (byte)x,
                y = (byte)y,
                width = (byte)width,
                height = (byte)height,
                material = (byte)mat
            };
        }

        // These SIMD variants return bogus data when x >= max
        bool4 IsCoveredSIMD(int4 x, int4 y, int layer) {
            if (layer >= max - 1)
                return false;
            int4 bogus = (int4)(x >= max);
            x *= (1 - bogus);
            return GetChunkSIMD(x, y, layer + 1) > 0;
        }

        unsafe bool GetHandled(BitField32* handled, int x, int y) {
            int unrolledIndex = y * max + x;
            return handled[unrolledIndex / 32].IsSet(unrolledIndex % 32);
        }

        unsafe bool4 GetHandledSIMD(BitField32* handled, int x, int y) {
            int unrolledIndex = y * max + x;
            // Can only fast-path when it fits.
            if (x < max - 4) {
                uint val = handled[unrolledIndex / 32].GetBits(unrolledIndex % 32, 4);
                return (val & new uint4(1, 2, 4, 8)) >= 1;
            } else {
                bool4 ret = default;
                for (int i = 0; x + i < max; unrolledIndex++, i++) {
                    ret[i] = handled[unrolledIndex / 32].IsSet(unrolledIndex % 32);
                }
                return ret;
            }
        }

        unsafe void SetHandledSIMD(BitField32* handled, int x, int y, bool4 value) {
            int unrolledIndex = y * max + x;
            int modVal = unrolledIndex % 32;
            // Can only fast-path when it fits.
                int divVal = unrolledIndex / 32;
            if (modVal < max - 4) {
                uint mask = ~(15u << modVal);
                uint maskFilling = (uint)math.dot((int4)value, new int4(1, 2, 4, 8)) << modVal;
                handled[divVal].Value = (handled[divVal].Value & mask) + maskFilling;
            } else {
                for (int i = 0; x + i < max; unrolledIndex++, i++) {
                    // Not using modVal/divVal because ^
                    handled[unrolledIndex / 32].SetBits(unrolledIndex % 32, value[i]);
                }
            }
        }

        unsafe void OrHandledSIMD(BitField32* handled, int x, int y, bool4 value) {
            int unrolledIndex = y * max + x;
            int modVal = unrolledIndex % 32;
            if (modVal < max - 4) {
                int divVal = unrolledIndex / 32;
                uint maskFilling = (uint)math.dot((int4)value, new int4(1, 2, 4, 8)) << modVal;
                handled[divVal].Value |= maskFilling;
            } else {
                for (int i = 0; x + i < max; unrolledIndex++, i++) {
                    modVal = unrolledIndex % 32;
                    int divVal = unrolledIndex / 32;
                    handled[divVal].SetBits(modVal, value[i] || handled[divVal].IsSet(modVal));
                }
            }
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

        int4x3 LayerToCoordSIMD(int4 x, int4 y, int layer) {
            if (currentLayerMode >= LayerMode.XDown) {
                layer = (max - 1) - layer;
            }
            int4x3 ret = currentLayerMode switch {
                LayerMode.XUp or LayerMode.XDown => new(x, y, layer),
                LayerMode.YUp or LayerMode.YDown => new(y, layer, x),
                LayerMode.ZUp or LayerMode.ZDown => new(layer, x, y),
                _ => default
            };
            return ret;
        }

        ushort GetChunk(int x, int y, int layer)
            => chunk[LayerToCoord(x * scale, y * scale, layer)];

        int4 GetChunkSIMD(int4 x, int4 y, int layer) {
            int4 bogus = (int4)(x >= max);
            x *= (1 - bogus);
            return chunk[LayerToCoordSIMD(x, y, layer)];
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