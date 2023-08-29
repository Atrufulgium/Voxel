using System;
using Unity.Mathematics;
using UnityEngine.Rendering;

namespace Atrufulgium.Voxel.WorldRendering {

    /// <summary>
    /// Turns a (x,y,z,material) into a single uint, by storing in the
    /// first three factors 33 the coordinate positions, and in the
    /// remaining [0,119513]-range the material.
    /// </summary>
    /// <remarks>
    /// This struct is mirrored on the GPU.
    /// </remarks>
    internal struct Vertex : IEquatable<Vertex> {
        public uint data;

        /// <summary>
        /// The `pos` vector should actually be integer with values between
        /// 0 and 32 inclusive. The material should be [0,119513].
        /// </summary>
        public Vertex(float3 pos, ushort material) : this((int3)pos, material) { }

        public Vertex(int3 pos, ushort material) {
            data = (uint)pos.x
                + 33u * (uint)pos.y
                + 33u * 33u * (uint)pos.z
                + 33u * 33u * 33u * material;
        }

        public override bool Equals(object obj)
            => obj is Vertex vertex && data == vertex.data;

        bool IEquatable<Vertex>.Equals(Vertex other)
            => data == other.data;

        public override int GetHashCode()
            => (int)data;

        public static bool operator ==(Vertex a, Vertex b) => a.data == b.data;
        public static bool operator !=(Vertex a, Vertex b) => !(a == b);


        internal static readonly VertexAttributeDescriptor[] Layout = new VertexAttributeDescriptor[] {
            new VertexAttributeDescriptor(VertexAttribute.BlendIndices, VertexAttributeFormat.UInt32, 1)
        };
    }
}
