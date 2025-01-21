using Atrufulgium.Voxel.Base;
using Atrufulgium.Voxel.World;
using Unity.Collections;

namespace Atrufulgium.Voxel.WorldRendering {
    /// <summary>
    /// Creates the graph edges
    /// <a href="https://tomcc.github.io/2014/08/31/visibility-1.html">this</a>
    /// algorithm needs.
    /// </summary>
    // This actually is a bit wasteful as KeyedJobManager assumes every next
    // job depends on the previous, so while #2 -- #7 can be run in parallel,
    // they won't.
    // This is not actually a problem as having many jobs fills up the workers
    // nicely anyway, and there's also other jobs going on. It only increases
    // latency a bit, but given how fast this code is, that's insignificant.
    public class OcclusionGraphBuilder : KeyedJobManager<
        /* key */ ChunkKey,
        /* job */ OcclussionFloodfillPrepJob,
        /* job */ BitFloodfillJob,
        /* job */ BitFloodfillJob,
        /* job */ BitFloodfillJob,
        /* job */ BitFloodfillJob,
        /* job */ BitFloodfillJob,
        /* job */ BitFloodfillJob,
        /* job */ OcclussionFloodfillPostJob,
        /* in  */ RawChunk,
        /* out */ ChunkVisibility
    > {
        // We have six floodfill jobs as each face startes from, well, a
        // different face with floodfilling.
        // This causes us to have an annoying six floodfill arrays
        // `arena{ChunkFace}` to deal with everywhere.
        // However, the alternative is branching at the job-level. Doing this
        // is kinda obnoxious with "raw" jobs, and very annoying to implement
        // as a feature inside KeyedJobManager<>. So in the end, it's like
        // this.

        RawChunk chunkCopy;
        NativeArray<uint> allowsFloodfill;
        NativeArray<uint> arenaXPos;
        NativeArray<uint> arenaYPos;
        NativeArray<uint> arenaZPos;
        NativeArray<uint> arenaXNeg;
        NativeArray<uint> arenaYNeg;
        NativeArray<uint> arenaZNeg;
        NativeReference<int> maxIndex;
        NativeReference<ChunkVisibility> seen;

        public override void Setup(
            RawChunk chunk,
            out OcclussionFloodfillPrepJob job1,
            out BitFloodfillJob job2,
            out BitFloodfillJob job3,
            out BitFloodfillJob job4,
            out BitFloodfillJob job5,
            out BitFloodfillJob job6,
            out BitFloodfillJob job7,
            out OcclussionFloodfillPostJob job8
        ) {
            if (!chunk.IsCreated)
                throw new System.ArgumentException("The given chunk does not exist.");
            
            int maxIndex = chunk.VoxelsPerAxis;

            // When reusing, dispose and recreate everything when LoD changes
            // as nearly everything depends on `maxIndex`.
            bool reinit = Reused && chunkCopy.LoD != chunk.LoD;
            if (reinit) {
                DisposeThis();
            }

            if (!Reused || reinit) {
                // This is like 100kB per instance at most. Very spammable.
                chunkCopy = chunk.GetCopy();
                allowsFloodfill = new(maxIndex * maxIndex, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
                arenaXPos = new(maxIndex * maxIndex, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
                arenaYPos = new(maxIndex * maxIndex, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
                arenaZPos = new(maxIndex * maxIndex, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
                arenaXNeg = new(maxIndex * maxIndex, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
                arenaYNeg = new(maxIndex * maxIndex, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
                arenaZNeg = new(maxIndex * maxIndex, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
                this.maxIndex = new(maxIndex, Allocator.Persistent);
                seen = new(Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
            }
            if (Reused && !reinit) {
                chunkCopy.FromRawArray(chunk);
            }

            job1 = new() {
                chunk = chunkCopy,
                allowsFloodfill = allowsFloodfill,
                startingPositionsXPos = arenaXPos,
                startingPositionsYPos = arenaYPos,
                startingPositionsZPos = arenaZPos,
                startingPositionsXNeg = arenaXNeg,
                startingPositionsYNeg = arenaYNeg,
                startingPositionsZNeg = arenaZNeg
            };

            job2 = new() {
                allowsFloodfill = allowsFloodfill,
                arena = arenaXPos,
                maxIndex = this.maxIndex
            };

            job3 = new() {
                allowsFloodfill = allowsFloodfill,
                arena = arenaYPos,
                maxIndex = this.maxIndex
            };

            job4 = new() {
                allowsFloodfill = allowsFloodfill,
                arena = arenaZPos,
                maxIndex = this.maxIndex
            };

            job5 = new() {
                allowsFloodfill = allowsFloodfill,
                arena = arenaXNeg,
                maxIndex = this.maxIndex
            };

            job6 = new() {
                allowsFloodfill = allowsFloodfill,
                arena = arenaYNeg,
                maxIndex = this.maxIndex
            };

            job7 = new() {
                allowsFloodfill = allowsFloodfill,
                arena = arenaZNeg,
                maxIndex = this.maxIndex
            };

            job8 = new() {
                resultXPos = arenaXPos,
                resultYPos = arenaYPos,
                resultZPos = arenaZPos,
                resultXNeg = arenaXNeg,
                resultYNeg = arenaYNeg,
                resultZNeg = arenaZNeg,
                maxIndex = this.maxIndex,
                seen = seen
            };
        }

        public override void PostProcess(
            ref ChunkVisibility result,
            in OcclussionFloodfillPrepJob job1,
            in BitFloodfillJob job2,
            in BitFloodfillJob job3,
            in BitFloodfillJob job4,
            in BitFloodfillJob job5,
            in BitFloodfillJob job6,
            in BitFloodfillJob job7,
            in OcclussionFloodfillPostJob job8
        ) {
            result = job8.seen.Value;
        }

        public override void Dispose() {
            DisposeThis();
            base.Dispose();
        }

        void DisposeThis() {
            chunkCopy.Dispose();
            maxIndex.Dispose();
            allowsFloodfill.Dispose();
            arenaXPos.Dispose();
            arenaYPos.Dispose();
            arenaZPos.Dispose();
            arenaXNeg.Dispose();
            arenaYNeg.Dispose();
            arenaZNeg.Dispose();
            seen.Dispose();
        }
    }
}
