using Atrufulgium.Voxel.Collections;
using Atrufulgium.Voxel.World;
using System.Collections.Generic;
using UnityEngine;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine.Profiling;

namespace Atrufulgium.Voxel.WorldRendering {
    public class RenderWorldBehaviour : MonoBehaviour {

        GameWorld world;
        RenderWorld renderWorld;
        ChunkMesher mesher;
        OcclusionCulling occlusionCulling;
        NativeParallelHashMap<ChunkKey, ChunkVisibility> occlusionData;
        new Transform transform;
        /// <summary>
        /// When requesting chunks, only update when we change position
        /// at the chunk-scale.
        /// </summary>
        ChunkKey previousCenter = ChunkKey.FromWorldPos(int.MaxValue);
        /// <summary>
        /// Whether the previous chunk request was limited by going over
        /// <see cref="MAXPERFRAME"/> requests.
        /// </summary>
        bool previousChunkRequestWasLimited = false;

        static Material voxelMat;

        readonly Dictionary<ChunkKey, Mesh> meshes = new();

        [Range(1,128)]
        public int RenderDistance = 16;
        int previousRenderDistance;
        public Camera mainCamera;

        const int MAXPERFRAME = 500;

        /// <summary>
        /// The normals each of the submeshes <see cref="ChunkMesher"/> gives
        /// us, in order.
        /// </summary>
        static readonly Vector3[] SubmeshNormals = {
            Vector3.left, Vector3.right,
            Vector3.down, Vector3.up,
            Vector3.back, Vector3.forward
        };

        private void Awake() {
            if (voxelMat == null)
                voxelMat = Resources.Load<Material>("Materials/Voxel");
            if (mainCamera == null) {
                mainCamera = Camera.main;
                Debug.LogWarning("No camera was given to a RenderWorldBehaviour. Defaulted to the main camera, but please fill the main camera field in.");
            }

            world = new();
            renderWorld = new RenderWorld(world) {
                RenderDistance = RenderDistance,
                CenterPoint = mainCamera.transform.position
            };
            mesher = new();
            occlusionData = new(100000, Allocator.Persistent);
            occlusionCulling = new(occlusionData);
            transform = GetComponent<Transform>();

            previousRenderDistance = RenderDistance;
        }

        private void Update() {
            float3 pos = mainCamera.transform.position;
            renderWorld.RenderDistance = RenderDistance;
            renderWorld.CenterPoint = pos;

            ChunkKey currentCenter = ChunkKey.FromWorldPos((int3)pos);

            if (previousChunkRequestWasLimited || currentCenter != previousCenter || RenderDistance > previousRenderDistance) {
                RequestMoreChunks(currentCenter);
            }

            previousCenter = currentCenter;
            previousRenderDistance = RenderDistance;

            for (int i = 0; i < MAXPERFRAME; i++) {
                if (renderWorld.TryGetDirtyChunk(out ChunkKey key, out RLEChunk chunk)) {
                    // If it's active already, put it at the end of the queue to
                    // try again later.
                    // This may only be needed if the race conditions are in our
                    // disadvantage (which they always are, of course).
                    if (ChunkMesher.JobExists(key) || OcclusionGraphBuilder.JobExists(key)) {
                        renderWorld.MarkDirty(key);
                    } else {
                        ChunkMesher.RunAsynchronously<ChunkMesher>(key, (chunk, 0));
                        OcclusionGraphBuilder.RunAsynchronously<OcclusionGraphBuilder>(key, chunk);
                    }
                }
            }

            foreach (var key in WorldGen.GetAllCompletedJobs())
                WorldGen.TryComplete(key, ref world);

            foreach(var key in ChunkMesher.GetAllCompletedJobs()) {
                if (!meshes.TryGetValue(key, out Mesh mesh)) {
                    mesh = new();
                    meshes.Add(key, mesh);
                }
                // This already overwrites the mesh if true and does nothing
                // when false.
                // Okay it's 100% true anyway.
                ChunkMesher.TryComplete(key, ref mesh);
            }
            foreach (var key in OcclusionGraphBuilder.GetAllCompletedJobs()) {
                ChunkVisibility visibility = default;
                OcclusionGraphBuilder.TryComplete(key, ref visibility);
                occlusionData.Add(key, visibility);
            }

            occlusionCulling.Occlude(mainCamera, out var visible);
            Profiler.BeginSample("Occlusion Processing");
            while (visible.Count > 0) {
                var key = visible.Dequeue();
                if (meshes.TryGetValue(key, out var mesh)) {
                    RenderParams renderParams = new() {
                        material = voxelMat,
                        receiveShadows = true,
                        shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On,
                        worldBounds = new((float3)key.Worldpos + 16, (float3)32)
                    };

                    float3 nearestPointInChunk = math.clamp(pos, key.Worldpos, key.Worldpos + 32);
                    float3 chunkDir = math.normalize(pos - nearestPointInChunk); // (NaN is fine)

                    for (int subindex = 0; subindex < 6; subindex++) {
                        // Don't render anything with no faces
                        if (mesh.GetSubMesh(subindex).indexCount == 0)
                            continue;

                        // DEBUGGING, WHAT I KNOW:
                        // (1) The layout of quads is correct and corresponds with
                        //     the six directions as commented.
                        // (2) The `nearestPointInChunk` calculation is correct.
                        // (3) `SubmeshNormals` is correct.
                        // (4) After the dot product, stuff is *incorrect*.

                        // Faces are visible if one of the following hold:
                        // (1) The chunk normal disagrees with the ray "chunk-
                        //     to-camera"
                        var d = math.dot(SubmeshNormals[subindex], chunkDir);

                        bool visibleFace = d < 0.001;
                        // (2) The chunk is AABB-aligned with the chunk the
                        //     camera is in (this is needed for wells etc as we
                        //     actually have a 32-wide range of faces instead of
                        //     a single point)
                        visibleFace |= math.csum((float3)(currentCenter.KeyValue == key.KeyValue)) > 1;

                        if (!visibleFace)
                            continue;

                        Graphics.RenderMesh(
                            renderParams,
                            mesh,
                            subindex,
                            Matrix4x4.Translate((float3)key.Worldpos)
                        );
                    }
                }
            }
            Profiler.EndSample();
        }

        void RequestMoreChunks(ChunkKey center) {
            int requested = 0;
            foreach (int3 add in Enumerators.EnumerateDiamondInfinite3D()) {
                // We need to render up to the corners. Reaching corners takes
                // thrice as long as the axes, and we also go over a bunch we
                // don't need.
                if (add.x > 3 * RenderDistance)
                    break;
                if (math.any(math.abs(add) > RenderDistance))
                    continue;

                ChunkKey requestKey = center + add;
                if (world.ChunkIsLoadedOrLoading(requestKey))
                    continue;

                if (requested < MAXPERFRAME) {
                    world.LoadChunk(requestKey);
                    requested++;
                } else {
                    break;
                }
            }
            previousChunkRequestWasLimited = requested == MAXPERFRAME;
        }

        private void OnDestroy() {
            world.Dispose();
            renderWorld.Dispose();
            mesher.Dispose();
            occlusionData.Dispose();
            occlusionCulling.Dispose();
            WorldGen.DisposeStatic();
            ChunkMesher.DisposeStatic();
            OcclusionGraphBuilder.DisposeStatic();
        }
    }
}
