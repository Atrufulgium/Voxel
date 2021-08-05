using Atrufulgium.Voxels.Base;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
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
            Mesh m = new Mesh {
                vertices = new Vector3[voxelCount]
            };
            m.SetIndices(Enumerable.Range(0, voxelCount).ToArray(), MeshTopology.Points, 0);
            return m;
        }
    }
}