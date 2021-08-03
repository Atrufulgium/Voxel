using UnityEngine;
using UnityEngine.Rendering;

namespace Atrufulgium.Voxels.Rendering {

    public class VoxelRenderPipeline : RenderPipeline {

        CameraRenderer cameraRenderer = new CameraRenderer();

        protected override void Render(ScriptableRenderContext context, Camera[] cameras) {
            foreach (Camera camera in cameras) {
                cameraRenderer.Render(context, camera);
            }
        }
    }
}
