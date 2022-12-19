using Atrufulgium.Voxel.Base;
using Unity.Mathematics;
using UnityEngine;

namespace Atrufulgium.Voxel {
    [RequireComponent(typeof(MeshFilter))]
    internal class TestChunk : MonoBehaviour {
        Chunk baseChunk;
        MeshFilter meshFilter;

        private void Awake() {
            meshFilter = GetComponent<MeshFilter>();

            baseChunk = new(0);
            foreach((int3 pos, ushort _) in baseChunk) {
                int val = (pos.x - 16) * (pos.x - 16) + (pos.z - 16) * (pos.z - 16);
                if (val < 450 - 16*pos.y && val > 400 - 16 * pos.y)
                    baseChunk[pos] = 1;
            }
        }

        [Range(0, 5)]
        public int LoD;

        private void OnValidate() {
            if (Application.isPlaying)
                meshFilter.mesh = baseChunk.WithLoD(LoD).GetMesh(0);
        }
    }
}
