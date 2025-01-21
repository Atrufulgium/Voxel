using Unity.Mathematics;
using NUnit.Framework;

namespace Atrufulgium.Voxel.World.Tests {
    public class RLEChunkTests {
        [Test]
        public void TestManyRandomWrites() {
            RLEChunk chunk = new(0);
            RawChunk reference = new(0);
            Random rng = new(230);

            for (int i = 0; i < 999999; i++) {
                int3 pos = rng.NextInt3(32);
                ushort mat = (ushort)rng.NextInt(4);

                chunk.Set(pos, mat);
                reference[pos] = mat;
            }
            CollectionAssert.AreEqual(reference, chunk);

            chunk.Dispose();
            reference.Dispose();
        }

        /// <summary>
        /// Test a basic case, like "aaX" or "[ab", to use the notation in the
        /// code over there.
        /// <br/>
        /// The <paramref name="startMat"/> represent what three materials to
        /// insert either as the first three, or last three voxels, depending
        /// on <paramref name="end"/>. After that, <paramref name="index"/> is
        /// a number 0, 1, or 2, specifying what index of startMat to
        /// override with material 0.
        /// </summary>
        void TestBasicCase(int3 startMat, int index, bool end) {
            RawChunk reference = new(0);
            int linIndex = end ? 32765 : 0;
            for (int i = 0; i < 3; i++) {
                reference[RLEChunk.Vectorize(linIndex + i)] = (ushort)startMat[i];
            }

            RLEChunk chunk = new(reference);

            var pos = RLEChunk.Vectorize(linIndex + index);
            chunk.Set(pos, 0);
            reference[pos] = 0;

            CollectionAssert.AreEqual(reference, chunk);

            chunk.Dispose();
            reference.Dispose();
        }

        // In these tests, we have material correspondences X = 0, A = 1, B = 2.
        [Test] // aaa
        public void TestBasicCaseAAA() => TestBasicCase(new(1, 1, 1), 1, false);
        [Test] // aab
        public void TestBasicCaseAAB() => TestBasicCase(new(1, 1, 2), 1, false);
        [Test] // baa
        public void TestBasicCaseBAA() => TestBasicCase(new(2, 1, 1), 1, false);
        [Test] // [aa
        public void TestBasicCase_AA() => TestBasicCase(new(1, 1, 1), 0, false);
        [Test] // aa]
        public void TestBasicCaseAA_() => TestBasicCase(new(1, 1, 1), 2, true);
        [Test] // aba
        public void TestBasicCaseABA() => TestBasicCase(new(1, 2, 1), 1, false);
        [Test] // [ab
        public void TestBasicCase_AB() => TestBasicCase(new(1, 2, 2), 0, false);
        [Test] // ab]
        public void TestBasicCaseAB_() => TestBasicCase(new(1, 1, 2), 2, true);
        [Test] // aaX
        public void TestBasicCaseAAX() => TestBasicCase(new(1, 1, 0), 1, false);
        [Test] // Xaa
        public void TestBasicCaseXAA() => TestBasicCase(new(0, 1, 1), 1, false);
        [Test] // abX
        public void TestBasicCaseABX() => TestBasicCase(new(1, 2, 0), 1, false);
        [Test] // Xab
        public void TestBasicCaseXAB() => TestBasicCase(new(0, 1, 2), 1, false);
        [Test] // [aX
        public void TestBasicCase_AX() => TestBasicCase(new(1, 0, 0), 0, false);
        [Test] // Xa]
        public void TestBasicCaseXA_() => TestBasicCase(new(0, 0, 1), 2, true);
        [Test] // XaX
        public void TestBasicCaseXAX() => TestBasicCase(new(0, 1, 0), 1, false);
    }
}