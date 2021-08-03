using UnityEngine;
using UnityEngine.Rendering;

namespace Atrufulgium.Voxels.Rendering {

    public class CameraRenderer {

        ScriptableRenderContext context;
        Camera camera;
        CommandBuffer buffer = new CommandBuffer() { name = "Render Camera" };
        CullingResults cullingResults;

        static ShaderTagId unlitShaderTagId = new ShaderTagId("SRPDefaultUnlit");
        static Shader drawVoxelShader = Shader.Find("Voxel/DrawVoxels");
        static Material drawVoxelMaterial = new Material(drawVoxelShader);

        static int voxelCountNameID = Shader.PropertyToID("voxelCount");
        static int paletteBufferNameID = Shader.PropertyToID("palette");
        static int voxelDataBufferNameID = Shader.PropertyToID("voxelData");

        ComputeBuffer paletteBuffer = new ComputeBuffer(16, 4 * sizeof(float));
        ComputeBuffer voxelDataBuffer = new ComputeBuffer(16 * 16 * 16, 4 * sizeof(byte));

        public void Render(ScriptableRenderContext context, Camera camera) {
            this.context = context;
            this.camera = camera;

            if (!Cull())
                return;

            Setup();
            DrawVisibleGeometry();
            DrawGizmos();
            Submit();
        }

        private void Setup() {
            context.SetupCameraProperties(camera);
            buffer.ClearRenderTarget(clearDepth: true, clearColor: true, Color.clear);
            buffer.BeginSample("Rendering");
            ExecuteCommandBuffer();
        }

        private void Submit() {
            buffer.EndSample("Rendering");
            ExecuteCommandBuffer();
            context.Submit();
        }

        private void ExecuteCommandBuffer() {
            context.ExecuteCommandBuffer(buffer);
            buffer.Clear();
        }

        private bool Cull() {
            if (camera.TryGetCullingParameters(out ScriptableCullingParameters p)) {
                cullingResults = context.Cull(ref p);
                return true;
            }
            return false;
        }

        private void DrawVisibleGeometry() {
            DrawVoxels();
            context.DrawSkybox(camera);
        }

        private void DrawVoxels() {
            foreach (VoxelRenderer renderer in VoxelManager.renderers) {
                paletteBuffer.SetData(renderer.palette);
                voxelDataBuffer.SetData(renderer.model);
                buffer.SetGlobalBuffer(paletteBufferNameID, paletteBuffer);
                buffer.SetGlobalBuffer(voxelDataBufferNameID, voxelDataBuffer);
                buffer.SetGlobalInt(voxelCountNameID, renderer.model.Length);
                buffer.DrawMesh(
                    VoxelManager.GetVoxelHelperMesh(renderer.model.Length),
                    renderer.transform.localToWorldMatrix,
                    drawVoxelMaterial
                );
                ExecuteCommandBuffer();
            }
        }

        private void DrawGizmos() {
#if UNITY_EDITOR
            if (UnityEditor.Handles.ShouldRenderGizmos()) {
                context.DrawGizmos(camera, GizmoSubset.PreImageEffects);
                context.DrawGizmos(camera, GizmoSubset.PostImageEffects);
            }
#endif // UNITY_EDITOR
        }
    }
}
