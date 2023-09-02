using System.Collections.Generic;
using Unity.Mathematics;

namespace Atrufulgium.Voxel.WorldRendering {
    // We want every pair to result in a unique power of two.
    // You can simply see the columns aligning in only two spots.
    // We don't particularly care about the numbers themselves,
    // only this property.
    // Completely unrelated trivia: In 4D, with an additional ana and kata
    // giving WPos and WNeg, it would still fit in a uint. In 5D however, it
    // won't. 4D bestD.
    public enum ChunkFace : uint {
        XPos = 0b000000000011111,
        XNeg = 0b000000111100001,
        YPos = 0b000111000100010,
        YNeg = 0b011001001000100,
        ZPos = 0b101010010001000,
        ZNeg = 0b110100100010000
    }

    /// <summary>
    /// This struct handles the question: "could face A be visible from face
    /// B?". This relation is symmetric of course. However, it is not
    /// transitive. If A could see B and B could see C, it doesn't mean that A
    /// could see C.
    /// </summary>
    public struct ChunkVisibility {
        // If bit ChunkFaceA & ChunkFaceB is unset, it represents that those
        // are maybe visible from one-another. Otherwise, it is impossible to
        // view one from the other.
        // If any bit in ChunkFaceA is unset, it then means that it is visible
        // from somewhere else.
        // Yes we're wasting a full 17 bits here but we're putting this in a
        // dictionary. Cache coherency is not relevant. Also, works nicer
        // with uint4s.
        uint value;

        /// <summary>
        /// Gives a ChunkVisibility where everything sees everything.
        /// </summary>
        public static ChunkVisibility All => default;
        /// <summary>
        /// Gives a ChunkVisibility where everything sees nothing else.
        /// </summary>
        public static ChunkVisibility None => new() { value = 0b111111111111111 };

        /// <summary>
        /// Sets whether two faces are visible from one-another.
        /// </summary>
        public void SetVisible(ChunkFace a, ChunkFace b, bool visible) {
            if (a == b)
                return;

            uint mask = (uint)a & (uint)b;
            if (visible) {
                value &= ~mask;
            } else {
                value |= mask;
            }
        }

        public void SetAllInvisible()
            => value = 0b111111111111111;

        public void SetAllVisible()
            => value = 0;

        /// <summary>
        /// Gets whether two faces are visible from one-another.
        /// (This defaults to true for all pairs, until set otherwise.)
        /// </summary>
        public bool GetVisible(ChunkFace a, ChunkFace b) {
            if (a == b)
                return true;

            uint mask = (uint)a & (uint)b;
            return (value & mask) == 0;
        }

        /// <summary>
        /// SIMD version of <see cref="GetVisible(ChunkFace, ChunkFace)"/>.
        /// </summary>
        /// <remarks>
        /// This only gives well-formed results when all components of
        /// <paramref name="chunkFaceA"/> and <paramref name="chunkFaceB"/> are
        /// one of <see cref="ChunkVisibility"/>'s named values.
        /// </remarks>
        public bool4 GetVisible(uint4 chunkFaceA, uint4 chunkFaceB) {
            uint4 mask = chunkFaceA & chunkFaceB;
            return (value & mask) == 0;
        }

        /// <inheritdoc cref="GetVisible(uint4, uint4)"/>
        public bool3 GetVisible(uint3 chunkFaceA, uint3 chunkFaceB) {
            uint3 mask = chunkFaceA & chunkFaceB;
            return (value & mask) == 0;
        }

        /// <inheritdoc cref="GetVisible(uint4, uint4)"/>
        public bool2 GetVisible(uint2 chunkFaceA, uint2 chunkFaceB) {
            uint2 mask = chunkFaceA & chunkFaceB;
            return (value & mask) == 0;
        }

        /// <summary>
        /// Converts this visibility to a vector ( ... ) of either included or
        /// excluded pairs. These pairs are written as two of XxYyZz in that
        /// order, and whether we do inclusion or exclusion is written before
        /// the vector.
        /// </summary>
        /// <returns></returns>
        public override string ToString() {
            static char FaceToString(ChunkFace face) => face switch {
                ChunkFace.XPos => 'X',
                ChunkFace.XNeg => 'x',
                ChunkFace.YPos => 'Y',
                ChunkFace.YNeg => 'y',
                ChunkFace.ZPos => 'Z',
                ChunkFace.ZNeg => 'z',
                _ => '$'
            };

            int included = 0;
            foreach (var f1 in AllChunkFaces())
                foreach (var f2 in AllChunkFaces())
                    if (f1 < f2 && GetVisible(f1, f2))
                        included++;

            bool isIncludedVector = included < 9;
            System.Text.StringBuilder sb = new();
            if (isIncludedVector)
                sb.Append("Included: (");
            else
                sb.Append("Excluded: (");
            foreach(var f1 in AllChunkFaces()) {
                foreach(var f2 in AllChunkFaces()) {
                    if (f1 >= f2)
                        continue;
                    if (GetVisible(f1, f2) ^ isIncludedVector)
                        continue;
                    sb.Append(FaceToString(f1));
                    sb.Append(FaceToString(f2));
                    sb.Append(", ");
                }
            }
            sb.Append(")");
            sb.Replace(", )", ")");
            return sb.ToString();
        }

        // This is a little awkward with the binary operators defined vs the
        // internal representation, but it makes semantic sense.
        /// <summary>
        /// If face A can be seen from face B in <i>either</i> of <paramref name="a"/>
        /// or <paramref name="b"/>, then the same holds for the return value.
        /// </summary>
        public static ChunkVisibility CombineVisible(ChunkVisibility a, ChunkVisibility b)
            => new() { value = a.value & b.value };

        /// <inheritdoc cref="CombineVisible(ChunkVisibility, ChunkVisibility)"/>
        public static ChunkVisibility operator |(ChunkVisibility a, ChunkVisibility b)
            => CombineVisible(a, b);

        /// <summary>
        /// Only if face A can be seen from face B in both of <paramref name="a"/>
        /// and <paramref name="b"/>, then the same holds for the return value.
        /// </summary>
        public static ChunkVisibility CombineInvisible(ChunkVisibility a, ChunkVisibility b)
            => new() { value = a.value | b.value };

        /// <inheritdoc cref="CombineInvisible(ChunkVisibility, ChunkVisibility)"/>
        public static ChunkVisibility operator &(ChunkVisibility a, ChunkVisibility b)
            => CombineInvisible(a, b);

        public static IEnumerable<ChunkFace> AllChunkFaces() {
            yield return ChunkFace.XPos;
            yield return ChunkFace.XNeg;
            yield return ChunkFace.YPos;
            yield return ChunkFace.YNeg;
            yield return ChunkFace.ZPos;
            yield return ChunkFace.ZNeg;
        }
    }
}
