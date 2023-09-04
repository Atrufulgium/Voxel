using Atrufulgium.Voxel.World;
using System.Collections.Generic;
using UnityEngine;
using Unity.Mathematics;
using static UnityEditor.PlayerSettings;
using Atrufulgium.Voxel.Collections;

namespace Atrufulgium.Voxel.WorldRendering {
    public class RenderWorldBehaviour : MonoBehaviour {

        GameWorld world;
        RenderWorld renderWorld;
        ChunkMesher mesher;
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

        Dictionary<ChunkKey, MeshFilter> meshes = new();

        [Range(1,128)]
        public int RenderDistance = 16;
        int previousRenderDistance;
        public Camera mainCamera;

        const int MAXPERFRAME = 1000;

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
                if (renderWorld.TryGetDirtyChunk(out ChunkKey key, out Chunk chunk)) {
                    // If it's active already, put it at the end of the queue to
                    // try again later.
                    // This may only be needed if the race conditions are in our
                    // disadvantage (which they always are, of course).
                    if (ChunkMesher.JobExists(key)) {
                        renderWorld.MarkDirty(key);
                    } else {
                        ChunkMesher.RunAsynchronously<ChunkMesher>(key, (chunk, 0));
                    }
                }
            }

            foreach (var key in WorldGen.GetAllCompletedJobs())
                WorldGen.PollJobCompleted(key, ref world);

            foreach(var key in ChunkMesher.GetAllCompletedJobs()) {
                if (!meshes.TryGetValue(key, out MeshFilter filter))
                    filter = CreateChunkMesh(key);
                Mesh mesh = filter.mesh;
                // This already overwrites the mesh if true and does nothing
                // when false.
                if (ChunkMesher.PollJobCompleted(key, ref mesh)) {
                    // Enqueue for occlusion
                }
            }
            foreach (var key in OcclusionGraphBuilder.GetAllCompletedJobs()) {
                ChunkVisibility visibility = default;
                OcclusionGraphBuilder.PollJobCompleted(key, ref visibility);
            }
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
            mesher.Dispose();
            WorldGen.DisposeStatic();
            ChunkMesher.DisposeStatic();
            OcclusionGraphBuilder.DisposeStatic();
        }

        private MeshFilter CreateChunkMesh(ChunkKey key) {
            GameObject newObject = new("(Chunk)", typeof(MeshFilter), typeof(MeshRenderer));
            newObject.hideFlags = HideFlags.HideInHierarchy;
            newObject.transform.SetParent(transform);
            newObject.transform.position = (float3)key.Worldpos;
            MeshFilter filter = newObject.GetComponent<MeshFilter>();
            newObject.GetComponent<MeshRenderer>().material = voxelMat;
            meshes.Add(key, filter);
            return filter;
        }
    }
}
