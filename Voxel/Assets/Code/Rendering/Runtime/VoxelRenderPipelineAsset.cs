using UnityEngine;
using UnityEngine.Rendering;

namespace Atrufulgium.Voxels.Rendering {

    [CreateAssetMenu(menuName = "Voxel Render Pipeline")]
    public class VoxelRenderPipelineAsset : RenderPipelineAsset {

        protected override RenderPipeline CreatePipeline() {
            return new VoxelRenderPipeline();
        }
    }
}
