using Atrufulgium.Voxel.Base;
using Atrufulgium.Voxel.Collections;
using Atrufulgium.Voxel.World;
using Unity.Burst;
using Unity.Burst.CompilerServices;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace Atrufulgium.Voxel.WorldRendering {
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
        public NativeQueueSet<ChunkKey> visible;
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
        /// The current camera left vector.
        /// </summary>
        [ReadOnly]
        public NativeReference<float3> cameraRight;
        /// <summary>
        /// The current camera's position.
        /// </summary>
        [ReadOnly]
        public NativeReference<float3> cameraPosition;
        /// <summary>
        /// The current camera's <see cref="UnityEngine.Camera.worldToCameraMatrix"/>.
        /// </summary>
        [ReadOnly]
        public NativeReference<float4x4> worldToCameraMatrix;
        /// <summary>
        /// The current camera's <see cref="UnityEngine.Camera.projectionMatrix"/>.
        /// </summary>
        [ReadOnly]
        public NativeReference<float4x4> projectionMatrix;

        float3 camPos;
        float3 camForward;
        float3 camRight;
        float4x4 VP;

        public void Execute() {
            camPos = cameraPosition.Value;
            camForward = cameraForward.Value;
            camRight = cameraRight.Value;
            VP = math.mul(projectionMatrix.Value, worldToCameraMatrix.Value);
            // We really don't need to handle all of them.
            HandleOctant(true, true, true);
            HandleOctant(true, true, false);
            HandleOctant(true, false, true);
            HandleOctant(true, false, false);
            HandleOctant(false, true, true);
            HandleOctant(false, true, false);
            HandleOctant(false, false, true);
            HandleOctant(false, false, false);
        }

        unsafe void HandleOctant(bool xPos, bool yPos, bool zPos) {
            // No [SkipLocalsInit] because we care about this zero-set.
            // What directions we may leave chunks from.
            ChunkFace* leaveFaces = stackalloc ChunkFace[3];
            // How to move the key from the current to allowedFaces[i].
            int3* faceOffsets = stackalloc int3[3];
            // The opposite direction to allowedFaces[i].
            ChunkFace* enterFaces = stackalloc ChunkFace[3];
            leaveFaces[0] = xPos ? ChunkFace.XPos : ChunkFace.XNeg;
            enterFaces[0] = xPos ? ChunkFace.XNeg : ChunkFace.XPos;
            faceOffsets[0] = xPos ? new(1, 0, 0) : new(-1, 0, 0);
            leaveFaces[1] = yPos ? ChunkFace.YPos : ChunkFace.YNeg;
            enterFaces[1] = yPos ? ChunkFace.YNeg : ChunkFace.YPos;
            faceOffsets[1] = yPos ? new(0, 1, 0) : new(0, -1, 0);
            leaveFaces[2] = zPos ? ChunkFace.ZPos : ChunkFace.ZNeg;
            enterFaces[2] = zPos ? ChunkFace.ZNeg : ChunkFace.ZPos;
            faceOffsets[2] = zPos ? new(0, 0, 1) : new(0, 0, -1);

            // The initial chunk has no "from face", so do manually.
            ChunkKey key = ChunkKey.FromWorldPos((int3)camPos);
            visible.Enqueue(key);
            for (int i = 0; i < 3; i++) {
                candidateChunks.Enqueue(new(key + faceOffsets[i], enterFaces[i]));
            }

            // Divide up screenspace into 64x64. Whenever a fragment is fully
            // covered by a chunk, mark it.
            // Whenever we would schedule something fully in marked fragments,
            // don't.
            ulong* screenspaceBlocked = stackalloc ulong[64];

            // TODO: Note that we will revisit chunks from different directions.
            // Need to think about how to properly handle this because it can
            // grow to up to 3x the necessary work.
            // This 3x is common (e.g. air).
            ChunkFace fromFace = default;
            while (candidateChunks.Count > 0) {
                (key, fromFace) = candidateChunks.Dequeue();
                if (Hint.Unlikely(!occlusionData.TryGetValue(key, out var visibility)))
                    continue;
                visible.Enqueue(key);
                for (int i = 0; i < 3; i++) {
                    if (!visibility.GetVisible(fromFace, leaveFaces[i])) {
                        continue;
                    }
                    var newKey = key + faceOffsets[i];
                    if (Hint.Unlikely(!InFrustrum(newKey))) {
                        continue;
                    }
                    if (Hint.Unlikely(TestMarked(screenspaceBlocked, newKey))) {
                        continue;
                    }

                    candidateChunks.Enqueue(new(newKey, enterFaces[i]));
                }

                // Check over all faces whether they are visible. If not, mark.
                // "Visible" can be reduced to "visible according to our three
                // directions", and then "all faces" can be reduced to "all
                // leave faces" (as any face can see itself).
                for (int i = 0; i < 3; i++) {
                    var face = leaveFaces[i];
                    if (!visibility.GetVisible(face, enterFaces[0])
                        && !visibility.GetVisible(face, enterFaces[1])
                        && !visibility.GetVisible(face, enterFaces[2]))
                        Mark(screenspaceBlocked, key, face);
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
            if (!HandlePlane(frustrumFarCorners[0], frustrumFarCorners[1], camPos, x, y, z, z2))
                return false;
            // Bottom
            if (!HandlePlane(frustrumFarCorners[1], frustrumFarCorners[2], camPos, x, y, z, z2))
                return false;
            // Right
            if (!HandlePlane(frustrumFarCorners[2], frustrumFarCorners[3], camPos, x, y, z, z2))
                return false;
            // Top
            if (!HandlePlane(frustrumFarCorners[3], frustrumFarCorners[0], camPos, x, y, z, z2))
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

        /// <summary>
        /// Inside 64x64 bit map <paramref name="map"/>, mark all fragments
        /// fully contained inside the given chunk face in screenspace.
        /// </summary>
        [SkipLocalsInit]
        unsafe void Mark(ulong* map, ChunkKey key, ChunkFace face) {
            GetCorners(key, face, out var xs, out var ys);
            float2* corners = stackalloc float2[4];
            float2 center = default;
            for (int i = 0; i < 4; i++) {
                corners[i] = new(xs[i], ys[i]);
                center += corners[i];
            }
            center *= 0.25f;
            Plane2D* planes = stackalloc Plane2D[4];
            for (int i = 0; i < 4; i++) {
                int ii = (i + 1) % 4;
                planes[i] = Plane2D.FromFacing(corners[i], corners[ii], facing: center);
            }
            // Now actually mark the stuff.
            // The lines will usually look something like \ /\ / and we do not
            // want to mark stuff containing the lines. So we need to mark
            // between ceil(second intersection) and floor(third intersection).
            // Oh and of course no need to do anything below/above extremes.
            int minY = math.max(0, (int)math.ceil(math.cmin(ys)));
            int maxY = math.min(64, (int)math.floor(math.cmax(ys)));
            float4 xIntersections = default;
            for (int y = minY; y < maxY; y++) {
                for (int i = 0; i < 4; i++)
                    if (Hint.Likely(planes[i].TryIntersectWith(Plane2D.FromHorizontal(y + 0.5f), out var inter)))
                        xIntersections[i] = inter.x;
                    else
                        xIntersections[i] = float.PositiveInfinity;
                // Sort our vector such that indices 1 and 2 contain the values
                // we are interested in. Take into account 0 up to 2 infinities.
                // >0 infinities is very rare though.
                WeirdSort(ref xIntersections);
                int minX = math.clamp((int)math.ceil(xIntersections[1]), 0, 64);
                int maxX = math.clamp((int)math.floor(xIntersections[2]), 0, 64);
                ulong mask = BitMath.AllOnesIntervalHigh64(minX, maxX);
                ulong res = map[y] | mask;
                // It is incredibly more likely for a `..101..` to appear from
                // holes between quads that should be filled, than anything
                // else. Also, those quads are very coarse -- entire chunkfaces.
                // Furthermore, when checking we're checking large-ish radii
                // for nearly all relevant chunks -- the chance that (1) this
                // gives a false positive and (2) this false positive
                // _matters_, is small.
                // So fill in any such `..101..` holes to `..111..`.
                res |= (res << 1) & (res >> 1);
                if (y > 1 && y < 63)
                    res |= map[y-1] & map[y+1];
                map[y] = res;
            }

            // Leaving this here to uncomment because it's just _satisfying_
            // to watch the mask do its thing.
            //string s = "";
            //for (int i = 63; i >= 0; i--) {
            //    ulong val = map[i];
            //    s += System.Convert.ToString((long)val, 2).PadLeft(64, '0') + "\n";
            //}
            //UnityEngine.Debug.Log(s);
        }

        // The weird sort we need above.
        void WeirdSort(ref float4 vector) {
            ref float a = ref vector.x;
            ref float b = ref vector.y;
            ref float c = ref vector.z;
            ref float d = ref vector.w;
            if (a > c) Swap(ref a, ref c);
            if (b > d) Swap(ref b, ref d);
            if (a > b) Swap(ref a, ref b);
            if (c > d) Swap(ref c, ref d);
            if (b > c) Swap(ref b, ref c);
            // The two-infinity case needs special handling
            if (Hint.Unlikely(c == float.PositiveInfinity)) {
                Swap(ref b, ref c);
                Swap(ref a, ref b);
            }
        }

        // No not using (a,b) = (b,a) because burst can't handle that.
        void Swap(ref float a, ref float b) {
            float temp = b;
            b = a;
            a = temp;
        }

        /// <summary>
        /// Test whether a given chunk is fully contained inside marked
        /// fragments in the 64x64 bit map <paramref name="map"/>.
        /// </summary>
        unsafe bool TestMarked(ulong* map, ChunkKey key) {
            // Approximate the chunk by a sphere of radius sqrt(3 * 16^2).
            // Spheres do not actually get mapped to spheres by projection
            // matrices, but ellipsoids. As a fix, add a fudge factor of +1 but
            // this is pretty wrong near the edges of the screen.
            // Probably not really a problem though given our 64x64 resolution.
            // Just like in Mark() (actually GetCorners()), we know that the
            // key lies in the frustrum and at the correct side of the camera.
            float4 center = new(key.Worldpos + 16, 1);
            float4 border = center + new float4(camRight, 0) * (27.71f + 1);
            border = math.mul(VP, border);
            center = math.mul(VP, center);
            border.xy /= border.w;
            center.xy /= center.w;
            border.xy = (border.xy + 1) * 0.5f;
            center.xy = (center.xy + 1) * 0.5f;
            border.xy *= 64;
            center.xy *= 64;
            float2 center2D = center.xy;
            float radius = math.distance(center.xy, border.xy);
            // Simple heuristic: large radii are processed first and very
            // unlikely to be contained in something even larger before.
            // The number itself is pretty arbitrary.
            if (radius > 24)
                return false;
            // This time we want to check including the chunk boundary
            // fragments, but since we're massively overestimating with the
            // sphere we can still ceil/floor instead of going floor/ceil.
            int minY = math.max(0, (int)math.floor(center.y - radius));
            int maxY = math.min(64, (int)math.ceil(center.y + radius));
            bool didAnything = false;
            for (int y = minY; y < maxY; y++) {
                // Note that this may result in negative input sometimes.
                float yDelta = y - center.y;
                float xBorderOffsetSq = radius * radius - yDelta * yDelta;
                if (Hint.Unlikely(xBorderOffsetSq < 0))
                    continue;
                float xBorderOffset = math.sqrt(xBorderOffsetSq);
                int minX = math.clamp((int)math.floor(center.x - xBorderOffset), 0, 64);
                int maxX = math.clamp((int)math.ceil(center.x + xBorderOffset), 0, 64);
                ulong check = BitMath.AllOnesIntervalHigh64(minX, maxX);

                if (check == 0)
                    continue;
                ulong mask = map[y];
                didAnything = true;
                if ((mask | check) != mask)
                    return false;
            }

            return didAnything;
        }

        /// <summary>
        /// Converts the corners of a chunkface to screenspace as [0,1].
        /// These values are unclamped; they could be below 0 or above 1.
        /// The only guarantee is that adjacent indices mod 4 are adjacent.
        /// </summary>
        unsafe void GetCorners(ChunkKey key, ChunkFace face, out float4 x, out float4 y) {
            // First create the four corners in world space.
            // Take into account winding order. All we require is "not an X".
            // As such, make [0] the base, [1] and [3] two offset vectors, and [2] their sum.
            float4 worldX = 0;
            float4 worldY = 0;
            float4 worldZ = 0;
            float3 offset = key.Worldpos - camPos;
            bool farFace = face == ChunkFace.XPos || face == ChunkFace.YPos || face == ChunkFace.ZPos;
            if (face == ChunkFace.XPos || face == ChunkFace.XNeg) {
                // Basis vectors (0,1,0), (0,0,1)
                worldY[1] = 32;
                worldZ[3] = 32;
                if (farFace)
                    offset.x += 32;
            } else if (face == ChunkFace.YPos || face == ChunkFace.YNeg) {
                // Basis vectors (1,0,0), (0,0,1)
                worldX[1] = 32;
                worldZ[3] = 32;
                if (farFace)
                    offset.y += 32;
            } else {
                // Basis vectors (1,0,0), (0,1,0)
                worldX[1] = 32;
                worldY[3] = 32;
                if (farFace)
                    offset.z += 32;
            }
            worldX[2] = worldX[1] + worldX[3];
            worldY[2] = worldY[1] + worldY[3];
            worldZ[2] = worldZ[1] + worldZ[3];
            worldX += offset.x;
            worldY += offset.y;
            worldZ += offset.z;

            // Now project away the cameraForward direction.
            // Note that we don't care about z as by assumption all faces are
            // in front of the camera and we're in the frustrum.
            // Don't forget perspective projection though.
            float4 w = default;
            x = VP.c0.x * worldX + VP.c1.x * worldY + VP.c2.x * worldZ + VP.c3.x;
            y = VP.c0.y * worldX + VP.c1.y * worldY + VP.c2.y * worldZ + VP.c3.y;
            w = VP.c0.w * worldX + VP.c1.w * worldY + VP.c2.w * worldZ + VP.c3.w;
            x /= w;
            y /= w;
            x = (x + 1) * 0.5f;
            y = (y + 1) * 0.5f;
            // Rescale to a screenspace of [0,64)^2
            x *= 64;
            y *= 64;
        }
    }
}
