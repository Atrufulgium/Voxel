using Atrufulgium.Voxel.World;
using NUnit.Framework;

namespace Atrufulgium.Voxel.WorldRendering.Tests {
    // These tests are for 4x4x4 chunks only (as you can still write those down
    // lol), but I've manually tested that the code keeps working under
    // different LoDs too.
    // In fact, nonzero LoD kind of _is_ the more difficult case.
    public class OcclusionGraphBuilderTests {

        [Test]
        public void TestEmpty() {
            ChunkVisibility actual = default;
            using RawChunk rawchunk
                = new RawChunk(3, new ushort[4 * 4 * 4])
                .WithLoD(0);
            using RLEChunk chunk = new(rawchunk);
            OcclusionGraphBuilder.RunSynchronously<OcclusionGraphBuilder>(chunk, ref actual);
            ChunkVisibility expected = ChunkVisibility.All;
            Assert.AreEqual(expected, actual);
        }

        [Test]
        public void TestFilled() {
            ChunkVisibility actual = default;
            using RawChunk rawchunk = new RawChunk(3, new ushort[] {
                1,1,1,1,
                1,1,1,1,
                1,1,1,1,
                1,1,1,1,

                1,1,1,1,
                1,1,1,1,
                1,1,1,1,
                1,1,1,1,

                1,1,1,1,
                1,1,1,1,
                1,1,1,1,
                1,1,1,1,

                1,1,1,1,
                1,1,1,1,
                1,1,1,1,
                1,1,1,1
            }).WithLoD(0);
            using RLEChunk chunk = new(rawchunk);
            OcclusionGraphBuilder.RunSynchronously<OcclusionGraphBuilder>(chunk, ref actual);
            ChunkVisibility expected = ChunkVisibility.None;
            Assert.AreEqual(expected, actual);
        }

        [Test]
        public void TestFlatXPos() {
            ChunkVisibility actual = default;
            // (Recall XYZ order here: X→ Y↓ Z↓↓)
            using RawChunk rawchunk = new RawChunk(3, new ushort[] {
                0,0,1,1,
                0,0,1,1,
                0,0,1,1,
                0,0,1,1,

                0,0,1,1,
                0,0,1,1,
                0,0,1,1,
                0,0,1,1,

                0,0,1,1,
                0,0,1,1,
                0,0,1,1,
                0,0,1,1,

                0,0,1,1,
                0,0,1,1,
                0,0,1,1,
                0,0,1,1
            }).WithLoD(0);
            using RLEChunk chunk = new(rawchunk);
            OcclusionGraphBuilder.RunSynchronously<OcclusionGraphBuilder>(chunk, ref actual);
            ChunkVisibility expected = ChunkVisibility.All;
            expected.SetVisible(ChunkFace.XPos, ChunkFace.XNeg, false);
            expected.SetVisible(ChunkFace.XPos, ChunkFace.YPos, false);
            expected.SetVisible(ChunkFace.XPos, ChunkFace.YNeg, false);
            expected.SetVisible(ChunkFace.XPos, ChunkFace.ZPos, false);
            expected.SetVisible(ChunkFace.XPos, ChunkFace.ZNeg, false);
            Assert.AreEqual(expected, actual);
        }

        [Test]
        public void TestFlatXNeg() {
            ChunkVisibility actual = default;
            using RawChunk rawchunk = new RawChunk(3, new ushort[] {
                1,1,0,0,
                1,1,0,0,
                1,1,0,0,
                1,1,0,0,
                
                1,1,0,0,
                1,1,0,0,
                1,1,0,0,
                1,1,0,0,

                1,1,0,0,
                1,1,0,0,
                1,1,0,0,
                1,1,0,0,

                1,1,0,0,
                1,1,0,0,
                1,1,0,0,
                1,1,0,0
            }).WithLoD(0);
            using RLEChunk chunk = new(rawchunk);
            OcclusionGraphBuilder.RunSynchronously<OcclusionGraphBuilder>(chunk, ref actual);
            ChunkVisibility expected = ChunkVisibility.All;
            expected.SetVisible(ChunkFace.XNeg, ChunkFace.XPos, false);
            expected.SetVisible(ChunkFace.XNeg, ChunkFace.YPos, false);
            expected.SetVisible(ChunkFace.XNeg, ChunkFace.YNeg, false);
            expected.SetVisible(ChunkFace.XNeg, ChunkFace.ZPos, false);
            expected.SetVisible(ChunkFace.XNeg, ChunkFace.ZNeg, false);
            Assert.AreEqual(expected, actual);
        }

        [Test]
        public void TestFlatYPos() {
            ChunkVisibility actual = default;
            using RawChunk rawchunk = new RawChunk(3, new ushort[] {
                0,0,0,0,
                0,0,0,0,
                1,1,1,1,
                1,1,1,1,

                0,0,0,0,
                0,0,0,0,
                1,1,1,1,
                1,1,1,1,

                0,0,0,0,
                0,0,0,0,
                1,1,1,1,
                1,1,1,1,

                0,0,0,0,
                0,0,0,0,
                1,1,1,1,
                1,1,1,1
            }).WithLoD(0);
            using RLEChunk chunk = new(rawchunk);
            OcclusionGraphBuilder.RunSynchronously<OcclusionGraphBuilder>(chunk, ref actual);
            ChunkVisibility expected = ChunkVisibility.All;
            expected.SetVisible(ChunkFace.YPos, ChunkFace.XPos, false);
            expected.SetVisible(ChunkFace.YPos, ChunkFace.XNeg, false);
            expected.SetVisible(ChunkFace.YPos, ChunkFace.YNeg, false);
            expected.SetVisible(ChunkFace.YPos, ChunkFace.ZPos, false);
            expected.SetVisible(ChunkFace.YPos, ChunkFace.ZNeg, false);
            Assert.AreEqual(expected, actual);
        }

        [Test]
        public void TestFlatYNeg() {
            ChunkVisibility actual = default;
            using RawChunk rawchunk = new RawChunk(3, new ushort[] {
                1,1,1,1,
                1,1,1,1,
                0,0,0,0,
                0,0,0,0,

                1,1,1,1,
                1,1,1,1,
                0,0,0,0,
                0,0,0,0,

                1,1,1,1,
                1,1,1,1,
                0,0,0,0,
                0,0,0,0,

                1,1,1,1,
                1,1,1,1,
                0,0,0,0,
                0,0,0,0
            }).WithLoD(0);
            using RLEChunk chunk = new(rawchunk);
            OcclusionGraphBuilder.RunSynchronously<OcclusionGraphBuilder>(chunk, ref actual);
            ChunkVisibility expected = ChunkVisibility.All;
            expected.SetVisible(ChunkFace.YNeg, ChunkFace.XPos, false);
            expected.SetVisible(ChunkFace.YNeg, ChunkFace.XNeg, false);
            expected.SetVisible(ChunkFace.YNeg, ChunkFace.YPos, false);
            expected.SetVisible(ChunkFace.YNeg, ChunkFace.ZPos, false);
            expected.SetVisible(ChunkFace.YNeg, ChunkFace.ZNeg, false);
            Assert.AreEqual(expected, actual);
        }

        [Test]
        public void TestFlatZPos() {
            ChunkVisibility actual = default;
            using RawChunk rawchunk = new RawChunk(3, new ushort[] {
                0,0,0,0,
                0,0,0,0,
                0,0,0,0,
                0,0,0,0,

                0,0,0,0,
                0,0,0,0,
                0,0,0,0,
                0,0,0,0,

                1,1,1,1,
                1,1,1,1,
                1,1,1,1,
                1,1,1,1,

                1,1,1,1,
                1,1,1,1,
                1,1,1,1,
                1,1,1,1
            }).WithLoD(0);
            using RLEChunk chunk = new(rawchunk);
            OcclusionGraphBuilder.RunSynchronously<OcclusionGraphBuilder>(chunk, ref actual);
            ChunkVisibility expected = ChunkVisibility.All;
            expected.SetVisible(ChunkFace.ZPos, ChunkFace.XPos, false);
            expected.SetVisible(ChunkFace.ZPos, ChunkFace.XNeg, false);
            expected.SetVisible(ChunkFace.ZPos, ChunkFace.YPos, false);
            expected.SetVisible(ChunkFace.ZPos, ChunkFace.YNeg, false);
            expected.SetVisible(ChunkFace.ZPos, ChunkFace.ZNeg, false);
            Assert.AreEqual(expected, actual);
        }

        [Test]
        public void TestFlatZNeg() {
            ChunkVisibility actual = default;
            using RawChunk rawchunk = new RawChunk(3, new ushort[] {
                1,1,1,1,
                1,1,1,1,
                1,1,1,1,
                1,1,1,1,

                1,1,1,1,
                1,1,1,1,
                1,1,1,1,
                1,1,1,1,

                0,0,0,0,
                0,0,0,0,
                0,0,0,0,
                0,0,0,0,

                0,0,0,0,
                0,0,0,0,
                0,0,0,0,
                0,0,0,0
            }).WithLoD(0);
            using RLEChunk chunk = new(rawchunk);
            OcclusionGraphBuilder.RunSynchronously<OcclusionGraphBuilder>(chunk, ref actual);
            ChunkVisibility expected = ChunkVisibility.All;
            expected.SetVisible(ChunkFace.ZNeg, ChunkFace.XPos, false);
            expected.SetVisible(ChunkFace.ZNeg, ChunkFace.XNeg, false);
            expected.SetVisible(ChunkFace.ZNeg, ChunkFace.YPos, false);
            expected.SetVisible(ChunkFace.ZNeg, ChunkFace.YNeg, false);
            expected.SetVisible(ChunkFace.ZNeg, ChunkFace.ZPos, false);
            Assert.AreEqual(expected, actual);
        }

        // Literally a ∐ shape in 3D
        [Test]
        public void TestCup() {
            ChunkVisibility actual = default;
            using RawChunk rawchunk = new RawChunk(3, new ushort[] {
                1,1,1,1,
                1,1,1,1,
                1,1,1,1,
                1,1,1,1,

                1,1,1,1,
                1,0,0,1,
                1,0,0,1,
                1,0,0,1,

                1,1,1,1,
                1,0,0,1,
                1,0,0,1,
                1,0,0,1,

                1,1,1,1,
                1,1,1,1,
                1,1,1,1,
                1,1,1,1
            }).WithLoD(0);
            using RLEChunk chunk = new(rawchunk);
            OcclusionGraphBuilder.RunSynchronously<OcclusionGraphBuilder>(chunk, ref actual);
            ChunkVisibility expected = ChunkVisibility.None;
            Assert.AreEqual(expected, actual);
        }

        [Test]
        public void TestCorners() {
            ChunkVisibility actual = default;
            using RawChunk rawchunk = new RawChunk(3, new ushort[] {
                0,1,1,0,
                1,1,1,1,
                1,1,1,1,
                0,1,1,0,

                1,1,1,1,
                1,1,1,1,
                1,1,1,1,
                1,1,1,1,

                1,1,1,1,
                1,1,1,1,
                1,1,1,1,
                1,1,1,1,

                0,1,1,0,
                1,1,1,1,
                1,1,1,1,
                0,1,1,0
            }).WithLoD(0);
            using RLEChunk chunk = new(rawchunk);
            OcclusionGraphBuilder.RunSynchronously<OcclusionGraphBuilder>(chunk, ref actual);
            ChunkVisibility expected = ChunkVisibility.All;
            expected.SetVisible(ChunkFace.XPos, ChunkFace.XNeg, false);
            expected.SetVisible(ChunkFace.YPos, ChunkFace.YNeg, false);
            expected.SetVisible(ChunkFace.ZPos, ChunkFace.ZNeg, false);
            Assert.AreEqual(expected, actual);
        }

        [Test]
        public void TestTunnels() {
            ChunkVisibility actual = default;
            using RawChunk rawchunk = new RawChunk(3, new ushort[] {
                1,1,1,1,
                1,1,1,1,
                1,0,1,1,
                1,1,1,1,

                1,1,1,1,
                1,1,1,1,
                0,0,1,1,
                1,0,1,1,

                1,1,0,1,
                1,1,0,0,
                1,1,0,1,
                1,1,0,1,

                1,1,1,1,
                1,1,1,1,
                1,1,1,1,
                1,1,1,1
            }).WithLoD(0);
            using RLEChunk chunk = new(rawchunk);
            OcclusionGraphBuilder.RunSynchronously<OcclusionGraphBuilder>(chunk, ref actual);
            ChunkVisibility expected = ChunkVisibility.None;
            expected.SetVisible(ChunkFace.XNeg, ChunkFace.YPos, true);
            expected.SetVisible(ChunkFace.XNeg, ChunkFace.ZNeg, true);
            expected.SetVisible(ChunkFace.YPos, ChunkFace.ZNeg, true);
            expected.SetVisible(ChunkFace.XPos, ChunkFace.YNeg, true);
            expected.SetVisible(ChunkFace.YNeg, ChunkFace.YPos, true);
            expected.SetVisible(ChunkFace.YPos, ChunkFace.XPos, true);
            Assert.AreEqual(expected, actual);
        }
    }
}