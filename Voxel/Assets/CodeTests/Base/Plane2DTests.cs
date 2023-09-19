using NUnit.Framework;
using Unity.Mathematics;

namespace Atrufulgium.Voxel.Base.Tests {
    public class Plane2DTests {
        // This test is representative enough of everything as all code is
        // basis-independent.
        [Test]
        public void TestHorizontal() {
            var plane = Plane2D.FromHorizontal(posY: 3);
            Assert.AreEqual(true, plane.IsValid);
            Assert.AreEqual(new float2(2, 3), plane.Project(new(2, -1)));
            Assert.AreEqual(3, plane.DistanceSigned(new(10, 6)));
            Assert.AreEqual(-3, plane.DistanceSigned(new(23, 0)));
            var plane2 = Plane2D.FromVertical(posX: 8);
            Assert.AreEqual(true, plane.TryIntersectWith(plane2, out var pos));
            Assert.AreEqual(new float2(8, 3), pos);
            plane2 = Plane2D.FromHorizontal(posY: 4);
            Assert.AreEqual(false, plane.TryIntersectWith(plane2, out _));
            Assert.AreEqual(true, plane.TryIntersectWith(plane, out pos));
            Assert.AreEqual(true, plane.ContainsPoint(pos));
        }
    }
}
