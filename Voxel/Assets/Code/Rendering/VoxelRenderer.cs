using Atrufulgium.Voxels.Base;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Atrufulgium.Voxels.Rendering {
    public class VoxelRenderer : MonoBehaviour {
        public Voxel[] model = new Voxel[] { new Voxel() { 
            x = 0, y = 0, z = 0,
            ColorID = 0,
            RenderFacePosX = true, RenderFaceNegX = true,
            RenderFacePosY = false, RenderFaceNegY = true,
            RenderFacePosZ = true, RenderFaceNegZ = true,
            VoxelsPerUnit = 1
        } };
        public Color[] palette = new Color[16];

        private void Awake() {
            VoxelManager.renderers.Add(this);
        }

        private void OnDestroy() {
            VoxelManager.renderers.Remove(this);
        }
    }
}