using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace Atrufulgium.Voxel.Base {
    internal interface IGreedyMeshJob : IJob {
        /// <summary>
        /// The chunk to turn into a mesh. Of course, this takes into account
        /// the chunks LoD.
        /// </summary>
        public Chunk Chunk { get; set; }
        /// <summary>
        /// Either a normalised vector representing the camera direction in the
        /// chunk's model space, or the zero vector. In the former case, all
        /// invisible faces gets culled, in the latter case no culling happens.
        /// A camera looking at the positive z direction has a viewDir (0,0,1).
        /// </summary>
        public float3 ViewDir { get; set; }

        /// <summary>
        /// All verts in the current GetMesh call.
        /// </summary>
        public NativeList<Vertex> Vertices { get; set; }
        /// <summary>
        /// I'd call it "tris" if my topology wasn't quads. The indices of the
        /// four corners of quads inside the vertices list in the current
        /// GetMesh call.
        /// </summary>
        /// <remarks>
        /// ushorts are *not* sufficient. You can construct a 28x28x28 3d
        /// checkerboard pattern of "air / non-air" with no two diagonally
        /// touching non-air blocks of the same material. However, this
        /// requires 11k well-placed blocks (in a "place two break one" way)
        /// out of the maximum of 16k blocks that can induce 6 verts.
        /// Anyone who achieves that *deserves* the broken physics and
        /// graphics they desire.
        /// </remarks>
        public NativeList<ushort> Quads { get; set; }

        /// <summary>
        /// <para>
        /// A conversion from vertex to index inside the vertices list in the
        /// current GetMesh call.
        /// </para>
        /// <para>
        /// This is needed to weld together vertices of the same material.
        /// </para>
        /// </summary>
        public NativeParallelHashMap<Vertex, int> VertToIndex { get; set; }
    }
}
