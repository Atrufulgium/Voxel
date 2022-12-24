using System.Collections.Generic;
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

            if (!World.TryGetWorld(0, out world))
                world = new World(0);
            mesher = new();
            transform = GetComponent<Transform>();

            rng = new(230000);

            Generate();
        }

        private void Update() {
            int3 center = rng.NextInt3(-200, 200);
            center.y /= 20;
            ushort mat = (ushort)rng.NextInt(0, 4);
            for (int i = 0; i < 200; i++) {
                world.Set(center + rng.NextInt3(-4, 4), mat);
            }

            for (int i = 0; i < 3; i++) {
                if (world.TryGetDirtyChunk(out ChunkKey key, out Chunk chunk)) {
                    if (!meshes.TryGetValue(key, out MeshFilter filter))
                        filter = CreateChunkMesh(key);
                    filter.mesh = mesher.GetMesh(chunk);
                }
            }
        }

        private void OnDestroy() {
            world.Dispose();
            mesher.Dispose();
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
            int radius = 100;
            for (int x = -radius; x < radius; x++) {
                int bound = (int)math.sqrt(radius*radius - x * x);
                for (int z = -bound; z < bound; z++) {
                    world.Set(new(x, -10, z), 1);
                }
            }
        }
    }
}
