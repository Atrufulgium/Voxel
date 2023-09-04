using NUnit.Framework;

namespace Atrufulgium.Voxel.Collections.Tests {
    public class EnumeratorsTests {
        [Test]
        public void TestDiamond2D() {
            // Here Y goes down so flip vertically
            int[,] expected = new int[7, 7] {
                {  0, 0, 0,22, 0, 0, 0 },
                {  0, 0,21,11,23, 0, 0 },
                {  0,20,10, 4,12,24, 0 },
                { 19, 9, 3, 0, 1, 5,13 },
                {  0,18, 8, 2, 6,14, 0 },
                {  0, 0,17, 7,15, 0, 0 },
                {  0, 0, 0,16, 0, 0, 0 }
            };
            int[,] actual = new int[7, 7];
            int iter = 0;
            foreach (var pos in Enumerators.EnumerateDiamondInfinite2D(center: 3)) {
                if (pos.x == 7)
                    break;
                // This indexing is kinda flipped because you first select the row
                actual[pos.y, pos.x] = iter;
                iter++;
            }
            Assert.AreEqual(expected, actual);
        }

        [Test]
        public void TestDiamond3D() {
            // Order: X↓↓ Y ↓ Z →.
            int[] expected = new int[5 * 5 * 5] {
                0, 0, 0, 0, 0,
                0, 0, 0, 0, 0,
                0, 0, 24, 0, 0,
                0, 0, 0, 0, 0,
                0, 0, 0, 0, 0,

                0, 0, 0, 0, 0,
                0, 0,22, 0, 0,
                0,23, 6,21, 0,
                0, 0,20, 0, 0,
                0, 0, 0, 0, 0,

                0, 0,16, 0, 0,
                0,17, 4,15, 0,
               18, 5, 0, 3,14,
                0,19, 2,13, 0,
                0, 0,12, 0, 0,

                0, 0, 0, 0, 0,
                0, 0,10, 0, 0,
                0,11, 1, 9, 0,
                0, 0,8, 0, 0,
                0, 0, 0, 0, 0,

                0, 0, 0, 0, 0,
                0, 0, 0, 0, 0,
                0, 0, 7, 0, 0,
                0, 0, 0, 0, 0,
                0, 0, 0, 0, 0,
            };
            int[] actual = new int[5 * 5 * 5];
            int iter = 0;
            foreach(var pos in Enumerators.EnumerateDiamondInfinite3D(center: 2)) {
                if (pos.x > 4)
                    break;
                actual[pos.z + 5 * (pos.y + 5 * pos.x)] = iter;
                iter++;
            }
            Assert.AreEqual(expected, actual);
        }
    }
}
