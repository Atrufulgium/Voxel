using UnityEngine;

namespace Atrufulgium.Voxels.Rendering {
    [RequireComponent(typeof(MeshFilter))]
    [ExecuteInEditMode]
    public class SetMeshToSingleVertex : MonoBehaviour {

        Mesh oldMesh;
        MeshFilter mf;

        void Awake() {
            mf = GetComponent<MeshFilter>();
            MeshToPoint();
        }

#if UNITY_EDITOR
        [ContextMenu("Set mesh to single vertex")]
        public void MeshToPointInEditor() => MeshToPoint();
        [ContextMenu("Set mesh to original mesh")]
        public void RestoreMeshInEditor() => RestoreMesh();
#endif // UNITY_EDITOR

        void MeshToPoint() {
            oldMesh = mf.sharedMesh;
            mf.sharedMesh = VoxelManager.GetVoxelHelperMesh(1);
        }

        void RestoreMesh() {
            mf.sharedMesh = oldMesh;
        }
    }
}
