using Atrufulgium.Voxels.Base;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Atrufulgium.Voxels.Rendering {
    public static class VoxelManager {
        public static HashSet<VoxelRenderer> renderers = new HashSet<VoxelRenderer>();

        /// <summary>
        /// Rendering a voxel model of N voxels requires a helper mesh
        /// of N vertices (with no regard to their properties) for the
        /// geometry shader. This method returns that mesh.
        /// </summary>
        public static Mesh GetVoxelHelperMesh(int voxelCount) {
            // Not caching anything, just testing now.
            return new Mesh {
                vertices = new Vector3[voxelCount],
                triangles = new int[0],
                // Manually specify non-trivial bounds -- otherwise it gets culled.
                bounds = new Bounds(Vector3.zero, Vector3.one / 100f)
            };
        }
    }
}