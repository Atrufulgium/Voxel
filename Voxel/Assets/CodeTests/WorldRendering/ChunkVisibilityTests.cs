using NUnit.Framework;
using System;

namespace Atrufulgium.Voxel.WorldRendering.Tests {
    // This is almost copied verbatim from my linqpad test file but it
    // makes sense to put into the repo also.
    public class ChunkVisibilityTests {
        [Test]
        public void TestAll() {
            // Casually exhausting all 2^(6 choose 2) = 16k options.
            // O(2^n^2) is my new favourite complexity.

            // The test fail is at the end. This is because Assert.AreEqual is
            // surprisingly slow. As in putting it inside the loop makes the
            // runtime of this test go from 0.03s to 10s.
            bool failed = false;
            string failReason = "";
            for (int i = 0; i < 2 << 15 && !failed; i++) {
                ChunkVisibility cv = new();
                // If bit ii of i is set, set visible, otherwise set invisible
                for (int ii = 0; ii < 15; ii++) {
                    bool visible = (i & (1 << i)) > 0;
                    cv.SetVisible(validPairs[ii].a, validPairs[ii].b, visible);
                }
                for (int ii = 0; ii < 15 && !failed; ii++) {
                    bool expected = (i & (1 << i)) > 0;
                    bool actual = cv.GetVisible(validPairs[ii].a, validPairs[ii].b);
                    if (expected != actual) {
                        failed = true;
                        // (This is powerset notation: bit `i` determines whether
                        //  element `i` of `validPairs` shows up.)
                        failReason = "First mistake and aborted; at inclusions " + Convert.ToString(i, 2).PadLeft(15);
                    }
                }
            }
            Assert.AreEqual(false, failed, failReason);
        }

        public static (ChunkFace a, ChunkFace b)[] validPairs = new[] {
            (ChunkFace.XPos, ChunkFace.XNeg),
            (ChunkFace.XPos, ChunkFace.YPos),
            (ChunkFace.XPos, ChunkFace.YNeg),
            (ChunkFace.XPos, ChunkFace.ZPos),
            (ChunkFace.XPos, ChunkFace.ZNeg),
            (ChunkFace.XNeg, ChunkFace.YPos),
            (ChunkFace.XNeg, ChunkFace.YNeg),
            (ChunkFace.XNeg, ChunkFace.ZPos),
            (ChunkFace.XNeg, ChunkFace.ZNeg),
            (ChunkFace.YPos, ChunkFace.YNeg),
            (ChunkFace.YPos, ChunkFace.ZPos),
            (ChunkFace.YPos, ChunkFace.ZNeg),
            (ChunkFace.YNeg, ChunkFace.ZPos),
            (ChunkFace.YNeg, ChunkFace.ZNeg),
            (ChunkFace.ZPos, ChunkFace.ZNeg)
        };

        [Test]
        public void TestIsFullyVisible() {
            // Just a sanity check
            ChunkVisibility cv = ChunkVisibility.None;
            foreach (var face in ChunkVisibility.AllChunkFaces())
                Assert.AreEqual(true, cv.IsFullyInvisible(face), $"Full invis: Failed face {face}");

            cv.SetVisible(ChunkFace.YPos, ChunkFace.ZNeg, true);
            foreach (var face in ChunkVisibility.AllChunkFaces())
                Assert.AreEqual(face != ChunkFace.YPos && face != ChunkFace.ZNeg, cv.IsFullyInvisible(face), $"Part invis: Failed face {face}");

            cv = ChunkVisibility.All;
            foreach (var face in ChunkVisibility.AllChunkFaces())
                Assert.AreEqual(false, cv.IsFullyInvisible(face));
        }
    }
}