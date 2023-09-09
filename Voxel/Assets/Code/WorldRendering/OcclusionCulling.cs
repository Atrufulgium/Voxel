using Atrufulgium.Voxel.Collections;
using Atrufulgium.Voxel.World;
using System;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace Atrufulgium.Voxel.WorldRendering {
    public class OcclusionCulling : IDisposable {

        // Not our responsibility, needed for the job
        NativeParallelHashMap<ChunkKey, ChunkVisibility> occlusionData;
        // Our responsibility, used in the job
        NativeParallelHashSet<ChunkKey> visible = new(100000, Allocator.Persistent);
        NativeQueueSet<ChunkKeyChunkFaceTuple> candidateChunks = new(Allocator.Persistent);
        NativeArray<float3> frustrumFarCorners = new(4, Allocator.Persistent);
        Vector3[] frustrumFarCornersArray = new Vector3[4];
        NativeReference<float3> cameraForward = new(Allocator.Persistent);
        NativeReference<float3> cameraPosition = new(Allocator.Persistent);

        /// <summary>
        /// Constructs a culler that uses the visibility data provided in
        /// <paramref name="occlusionData"/>. The calling class is responsible
        /// for the disposal of that data.
        /// </summary>
        public OcclusionCulling(NativeParallelHashMap<ChunkKey, ChunkVisibility> occlusionData) {
            this.occlusionData = occlusionData;
        }

        /// <summary>
        /// Given a camera, goes through the occlusion data to see what chunks
        /// are visible, and returns that. The returned value is live and
        /// changes, so immediately use it. This class is responsibly for
        /// discarding the output.
        /// </summary>
        public void Occlude(Camera camera, out NativeParallelHashSet<ChunkKey> visible) {
            cameraPosition.Value = camera.transform.position;
            cameraForward.Value = camera.transform.forward;
            camera.CalculateFrustumCorners(
                new(0, 0, 1, 1),
                camera.farClipPlane,
                Camera.MonoOrStereoscopicEye.Mono,
                frustrumFarCornersArray
            );
            for (int i = 0; i < 4; i++)
                frustrumFarCorners[i] = camera.transform.TransformVector(frustrumFarCornersArray[i]);
            this.visible.Clear();

            //System.Diagnostics.Stopwatch sw = new();
            //sw.Start();
            OcclusionCullingJob job = new() {
                occlusionData = occlusionData,
                visible = this.visible,
                candidateChunks = candidateChunks,
                frustrumFarCorners = frustrumFarCorners,
                cameraForward = cameraForward,
                cameraPosition = cameraPosition
            };
            // TODO: Perhaps earlier in the frame to not block the main thread.
            // If I do it dynamically in OnPreCull though, that becomes harder.
            job.Run();
            visible = job.visible;
            //sw.Stop();
            //Debug.Log($"Occlusion took {1000f * sw.ElapsedTicks / System.Diagnostics.Stopwatch.Frequency} ms");
        }

        public void Dispose() {
            visible.Dispose();
            candidateChunks.Dispose();
            frustrumFarCorners.Dispose();
            cameraForward.Dispose();
            cameraPosition.Dispose();
        }

        /// <summary>
        /// Burst doesn't like tuples as keys as it tries to use managed
        /// methods. Read this as if it's (ChunkKey key, ChunkFace fromFace).
        /// </summary>
        public struct ChunkKeyChunkFaceTuple : IEquatable<ChunkKeyChunkFaceTuple> {
            public ChunkKey key;
            public ChunkFace fromFace;

            public ChunkKeyChunkFaceTuple(ChunkKey key, ChunkFace fromFace) {
                this.key = key;
                this.fromFace = fromFace;
            }

            public bool Equals(ChunkKeyChunkFaceTuple other)
                => key == other.key && fromFace == other.fromFace;

            public override int GetHashCode()
                => key.GetHashCode() ^ (int)fromFace;

            public void Deconstruct(out ChunkKey key, out ChunkFace fromFace) {
                key = this.key;
                fromFace = this.fromFace;
            }
        }
    }
}