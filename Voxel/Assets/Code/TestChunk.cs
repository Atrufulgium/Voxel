﻿using Atrufulgium.Voxel.Base;
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
                if (val < 450 - 16*pos.y)
                    baseChunk[pos] = (ushort)(val > 400 - 16 * pos.y ? 10 : 21);
            }
        }

        [Range(0, 5)]
        public int LoD;

        private void OnValidate() {
            if (baseChunk.voxels == null)
                Awake();

            meshFilter.mesh = baseChunk.WithLoD(LoD).GetMesh(0);
        }
    }
}