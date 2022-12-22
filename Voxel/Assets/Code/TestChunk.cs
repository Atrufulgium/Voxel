using Atrufulgium.Voxel.Base;
using Unity.Mathematics;
using UnityEngine;

namespace Atrufulgium.Voxel {
    [RequireComponent(typeof(MeshFilter))]
    internal class TestChunk : MonoBehaviour {
        Chunk baseChunk;
        MeshFilter meshFilter;
        ChunkMesher mesher;

        private void Awake() {
            meshFilter = GetComponent<MeshFilter>();
            mesher = new();

            baseChunk = new(0);
            foreach((int3 pos, ushort _) in baseChunk) {
                //int val = (pos.x - 16) * (pos.x - 16) + (pos.z - 16) * (pos.z - 16);
                //if (val < 450 - 16*pos.y)
                //    baseChunk[pos] = (ushort)(val > 400 - 16 * pos.y ? 1 : 2);
                float val = math.lengthsq(pos - 16);
                float val2 = math.lengthsq(pos.xy - 16);
                if (val is < 250 && val2 > 10)
                    baseChunk[pos] = (ushort) ((pos.x & 2) == 0 ? 1 : 2);
            }
        }

        [Range(0, 5)]
        public int LoD;

        private void Update() {
            LoD += 1;
            LoD %= 5;
            if (LoD == 0)
                LoD = 1;
            for (int i = 0; i < 10; i++) {
                using Chunk c = baseChunk.WithLoD(LoD);
                meshFilter.mesh = mesher.GetMesh(c);
            }

        }

        private void OnValidate() {
            if (!baseChunk.voxels.IsCreated)
                Awake();

            using Chunk c = baseChunk.WithLoD(LoD);
            meshFilter.mesh = mesher.GetMesh(c);
        }

        private void OnDestroy() {
            baseChunk.Dispose();
            mesher.Dispose();
        }
    }
}
