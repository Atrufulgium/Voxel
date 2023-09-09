using Atrufulgium.Voxel.Collections;
using Atrufulgium.Voxel.World;
using Unity.Burst;
using Unity.Burst.CompilerServices;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace Atrufulgium.Voxel.WorldRendering {
    // TODO: One way to improve this job:
    // Create a (very) low-res map that represents viewspace.
    // If a fragment of that map is *fully* blocked by a chunk, mark it as
    // ineligible. Once it's ineligible, any chunk that is contained in only
    // ineligible fragments gets ignored.
    // Most gains here are with nearby chunks blocking out a large part of
    // everything else behind.
    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, OptimizeFor = OptimizeFor.Performance)]
    public struct OcclusionCullingJob : IJob {
        /// <summary>
        /// All the data needed to construct the occlusion graph.
        /// </summary>
        [ReadOnly]
        public NativeParallelHashMap<ChunkKey, ChunkVisibility> occlusionData;
        /// <summary>
        /// The result of the occlusion calculations. Included ones are rendered.
        /// Clear before the job.
        /// </summary>
        [WriteOnly]
        public NativeParallelHashSet<ChunkKey> visible;
        /// <summary>
        /// All chunks that have yet to be processed on whether they are
        /// included or not.
        /// No need to clear before the job, clears itself.
        /// </summary>
        public NativeQueueSet<OcclusionCulling.ChunkKeyChunkFaceTuple> candidateChunks;

        /// <summary>
        /// <para>
        /// The eight corners of the frustrum far plane. They are given as
        /// follows (when viewing the frustrum from behind the camera):
        /// <code>
        ///     1-------2
        ///     |\     /|
        ///     | *---* |
        ///     | | C | |
        ///     | *---* |
        ///     |/     \|
        ///     0-------3
        /// </code>
        /// (We are ignoring the near plane.)
        /// </para>
        /// <para>
        /// Well, the specific order does not matter, as long as it's clockwise.
        /// </para>
        /// <para>
        /// See also <see cref="UnityEngine.Camera.CalculateFrustumCorners(UnityEngine.Rect, float, UnityEngine.Camera.MonoOrStereoscopicEye, UnityEngine.Vector3[])"/>
        /// </para>
        /// </summary>
        [ReadOnly]
        public NativeArray<float3> frustrumFarCorners;

        /// <summary>
        /// The current camera forward vector.
        /// </summary>
        [ReadOnly]
        public NativeReference<float3> cameraForward;
        /// <summary>
        /// The current camera's position.
        /// </summary>
        [ReadOnly]
        public NativeReference<float3> cameraPosition;

        float3 cameraPos;

        unsafe public void Execute() {
            cameraPos = cameraPosition.Value;
            // There's at most only one ignored face: backwards.
            // The minimum is three: look at a diagnoal for instance.
            // No [SkipLocalsInit] because we care about this zero-set.
            ChunkFace* allowedFaces = stackalloc ChunkFace[5];
            // How to move the key from the current to allowedFaces[i].
            int3* faceOffsets = stackalloc int3[5];
            // The opposite direction to allowedFaces[i].
            ChunkFace* oppositeFaces = stackalloc ChunkFace[5];
            float3 cameraForward = this.cameraForward.Value;
            // TODO: The current code is wrong as it doesn't account for >3. Problem for later.
            allowedFaces[0] = cameraForward.x >= 0 ? ChunkFace.XPos : ChunkFace.XNeg;
            oppositeFaces[0] = cameraForward.x >= 0 ? ChunkFace.XNeg : ChunkFace.XPos;
            faceOffsets[0] = cameraForward.x >= 0 ? new(1, 0, 0) : new(-1, 0, 0);
            allowedFaces[1] = cameraForward.y >= 0 ? ChunkFace.YPos : ChunkFace.YNeg;
            oppositeFaces[1] = cameraForward.y >= 0 ? ChunkFace.YNeg : ChunkFace.YPos;
            faceOffsets[1] = cameraForward.y >= 0 ? new(0, 1, 0) : new(0, -1, 0);
            allowedFaces[2] = cameraForward.z >= 0 ? ChunkFace.ZPos : ChunkFace.ZNeg;
            oppositeFaces[2] = cameraForward.z >= 0 ? ChunkFace.ZNeg : ChunkFace.ZPos;
            faceOffsets[2] = cameraForward.z >= 0 ? new(0, 0, 1) : new(0, 0, -1);

            // The initial chunk has no "from face", so do manually.
            ChunkKey key = ChunkKey.FromWorldPos((int3)cameraPos);
            visible.Add(key);
            for (int i = 0; i < 5; i++) {
                if (allowedFaces[i] == default)
                    break;
                candidateChunks.Enqueue(new(key + faceOffsets[i], oppositeFaces[i]));
            }

            // TODO: Note that we will revisit chunks from different directions.
            // Need to think about how to properly handle this because it can
            // grow to up to 3x the necessary work.
            // This 3x is common (e.g. air).
            ChunkFace fromFace = default;
            while (candidateChunks.Count > 0) {
                (key, fromFace) = candidateChunks.Dequeue();
                if (Hint.Unlikely(!occlusionData.TryGetValue(key, out var visibility)))
                    continue;
                visible.Add(key);
                for (int i = 0; i < 5; i++) {
                    if (i > 2 && allowedFaces[i] == default) {
                        break;
                    }
                    if (!visibility.GetVisible(fromFace, allowedFaces[i]))
                        continue;
                    if (Hint.Unlikely(!InFrustrum(key)))
                        continue;
                    candidateChunks.Enqueue(new(key + faceOffsets[i], oppositeFaces[i]));
                }
            }
        }

        /// <summary>
        /// Whether a given point is in the current camera's frustrum.
        /// (Including the bit between the near plane and camera.)
        /// </summary>
        bool InFrustrum(ChunkKey key) {
            // The eight corners of the AABB.
            // X and Y are the same in both.
            float4 x = key.Worldpos.x + new float4(32, 0, 32, 0);
            float4 y = key.Worldpos.y + new float4(32, 32, 0, 0);
            float4 z = key.Worldpos.z;
            float4 z2 = z + 32;

            // Far
            if (!HandlePlane(frustrumFarCorners[2], frustrumFarCorners[1], frustrumFarCorners[0], x, y, z, z2))
                return false;
            // Left
            if (!HandlePlane(frustrumFarCorners[0], frustrumFarCorners[1], cameraPos, x, y, z, z2))
                return false;
            // Bottom
            if (!HandlePlane(frustrumFarCorners[1], frustrumFarCorners[2], cameraPos, x, y, z, z2))
                return false;
            // Right
            if (!HandlePlane(frustrumFarCorners[2], frustrumFarCorners[3], cameraPos, x, y, z, z2))
                return false;
            // Top
            if (!HandlePlane(frustrumFarCorners[3], frustrumFarCorners[0], cameraPos, x, y, z, z2))
                return false;
            return true;
        }

        /// <summary>
        /// Given a base point <paramref name="p1"/> and two of its neighbours
        /// <paramref name="p2"/> and <paramref name="p3"/>, gets whether any
        /// point as determined by <paramref name="x"/>, <paramref name="y"/>,
        /// <paramref name="z"/> (or <paramref name="z2"/>) lies on the
        /// correct side of the plane.
        /// </summary>
        bool HandlePlane(float3 p1, float3 p2, float3 p3, float4 x, float4 y, float4 z, float4 z2) {
            float3 d1 = p2 - p1;
            float3 d2 = p3 - p1;
            float3 n = math.normalize(math.cross(d2, d1));
            return math.any(TestInHalfplane(p1, n, x, y, z))
                || math.any(TestInHalfplane(p1, n, x, y, z2));
        }

        /// <summary>
        /// For four points, checks whether they are at the side a given normal
        /// a given (infinite) plane directs to, or the opposite side.
        /// </summary>
        bool4 TestInHalfplane(float3 planePoint, float3 planeNormal, float4 x, float4 y, float4 z) {
            x -= planePoint.x;
            y -= planePoint.y;
            z -= planePoint.z;
            return (planeNormal.x * x + planeNormal.y * y + planeNormal.z * z) >= 0;
        }
    }
}
