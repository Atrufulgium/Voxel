﻿using System.Collections.Generic;
using UnityEngine;
using Unity.Mathematics;

namespace Atrufulgium.Voxel.Base {
    public class WorldBehaviour : MonoBehaviour {

        World world;
        ChunkMesher mesher;
        new Transform transform;

        Unity.Mathematics.Random rng;

        static Material voxelMat;

        Dictionary<ChunkKey, MeshFilter> meshes = new();

        private void Awake() {
            if (voxelMat == null)
                voxelMat = Resources.Load<Material>("Materials/Voxel");

            if (World.WorldExists(0)) {
                World.RemoveWorld(0);
            }
            world = new World(0);
            mesher = new();
            transform = GetComponent<Transform>();

            rng = new(230000);

            Generate();
        }

        int frame = 0;

        private void Update() {
            frame++;

            int3 center = rng.NextInt3(-200, 200);
            center.y /= 20;
            ushort mat = (ushort)rng.NextInt(0, 4);
            for (int i = 0; i < 200; i++) {
                world.Set(center + rng.NextInt3(-4, 4), mat);
            }

            for (int i = 0; i < 10; i++) {
                if (world.TryGetDirtyChunk(out ChunkKey key, out Chunk chunk)) {
                    // If it's active already, put it at the end of the queue to
                    // try again later.
                    // This may only be needed if the race conditions are in our
                    // disadvantage (which they always are, of course).
                    if (ChunkMesher.JobExists(key)) {
                        world.MarkDirty(key);
                    } else {
                        ChunkMesher.GetMeshAsynchronously(key, chunk, 0);
                    }
                }
            }

            foreach(var key in ChunkMesher.GetAllCompletedJobs()) {
                if (!meshes.TryGetValue(key, out MeshFilter filter))
                    filter = CreateChunkMesh(key);
                Mesh oldMesh = filter.mesh;
                if (ChunkMesher.TryCompleteMeshAsynchronously(key, out Mesh newMesh, in oldMesh)) {
                    filter.mesh = newMesh;
                }
            }
        }

        private void OnDestroy() {
            world.Dispose();
            mesher.Dispose();
            ChunkMesher.DisposeStatic();
        }

        private MeshFilter CreateChunkMesh(ChunkKey key) {
            GameObject newObject = new("(Chunk)", typeof(MeshFilter), typeof(MeshRenderer));
            newObject.transform.SetParent(transform);
            newObject.transform.position = (float3)key.Worldpos;
            MeshFilter filter = newObject.GetComponent<MeshFilter>();
            newObject.GetComponent<MeshRenderer>().material = voxelMat;
            meshes.Add(key, filter);
            return filter;
        }

        private void Generate() {
            int radius = 300;
            for (int x = -radius; x < radius; x++) {
                int bound = (int)math.sqrt(radius*radius - x * x);
                for (int z = -bound; z < bound; z++) {
                    float height
                        =  1 * Mathf.PerlinNoise(x /  1f, z /  1f)
                        +  2 * Mathf.PerlinNoise(x /  2f, z /  2f)
                        +  4 * Mathf.PerlinNoise(x /  4f, z /  4f)
                        +  8 * Mathf.PerlinNoise(x /  8f, z /  8f)
                        + 16 * Mathf.PerlinNoise(x / 16f, z / 16f);
                    for (int y = -5; y < height - 10; y++)
                        world.Set(new(x, y, z), 1);
                }
            }
        }
    }
}