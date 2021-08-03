using System;
using System.Runtime.InteropServices;

namespace Atrufulgium.Voxels.Base {
    /// <summary>
    /// A voxel as part of some object.
    /// Voxels on their own have no meaning. Their position, rotation,
    /// scale, and colors are determined by their parent object.
    /// </summary>
    [StructLayout(LayoutKind.Explicit)]
    public struct Voxel : IEquatable<Voxel> {
        /// <summary>
        /// Lower half: [0,15] offset X.
        /// Upper half: [0,15] offset Y.
        /// </summary>
        [FieldOffset(0)]
        PackedNibbles dataA;
        /// <summary>
        /// Lower half: [0,15] offset Z.
        /// Upper half: [0,15] color choice in the palette.
        /// </summary>
        [FieldOffset(1)]
        PackedNibbles dataB;
        /// <summary>
        /// 1: Render +X face on true.
        /// 2: Render -X face on true.
        /// 4: Render +Y face on true.
        /// 8: Render -Y face on true.
        /// 16: Render +Z face on true.
        /// 32: Render -Z face on true.
        /// </summary>
        [FieldOffset(2)]
        PackedBits faceflags;
        /// <summary>
        /// Lower half: (value + 1) is voxels per unit.
        /// 16: Half-transparent on true.
        /// 32: Emissive on true.
        /// 64: Casts shadows on true.
        /// 128: Receives shadows on true.
        /// </summary>
        [FieldOffset(3)]
        PackedBits graphicsflags;

        /// <summary>
        /// All four bytes of this struct as a single numeric type.
        /// For easier equality checking and such.
        /// </summary>
        [FieldOffset(0)]
        uint fullData;

        /// <summary>
        /// Gets or sets the x-offset of this voxel.
        /// This is a value [0,15]. Higher values wrap around.
        /// </summary>
        public int x { get => dataA.Lower; set => dataA.Lower = value; }

        /// <summary>
        /// Gets or sets the y-offset of this voxel.
        /// This is a value [0,15]. Higher values wrap around.
        /// </summary>
        public int y { get => dataA.Upper; set => dataA.Upper = value; }

        /// <summary>
        /// Gets or sets the z-offset of this voxel.
        /// This is a value [0,15]. Higher values wrap around.
        /// </summary>
        public int z { get => dataB.Lower; set => dataB.Lower = value; }

        /// <summary>
        /// Gets or sets the color (byte palette ID) of this voxel.
        /// This is a value [0,15]. Other values wrap around.
        /// </summary>
        public int ColorID { get => dataB.Upper; set => dataB.Upper = value; }

        /// <summary> Whether to render the +X-face of this voxel. </summary>
        public bool RenderFacePosX { get => faceflags[0]; set => faceflags[0] = value; }
        /// <summary> Whether to render the -X-face of this voxel. </summary>
        public bool RenderFaceNegX { get => faceflags[1]; set => faceflags[1] = value; }
        /// <summary> Whether to render the +Y-face of this voxel. </summary>
        public bool RenderFacePosY { get => faceflags[2]; set => faceflags[2] = value; }
        /// <summary> Whether to render the -Y-face of this voxel. </summary>
        public bool RenderFaceNegY { get => faceflags[3]; set => faceflags[3] = value; }
        /// <summary> Whether to render the +Z-face of this voxel. </summary>
        public bool RenderFacePosZ { get => faceflags[4]; set => faceflags[4] = value; }
        /// <summary> Whether to render the -Z-face of this voxel. </summary>
        public bool RenderFaceNegZ { get => faceflags[5]; set => faceflags[5] = value; }
        /// <summary> Whether this voxel is 50% transparent. </summary>
        public bool HalfTransparent { get => graphicsflags[0]; set => graphicsflags[0] = value; }
        /// <summary> Whether this voxel glows. </summary>
        public bool Emissive { get => graphicsflags[1]; set => graphicsflags[1] = value; }
        /// <summary> Whether this voxel casts shadows. </summary>
        public bool CastsShadows { get => graphicsflags[2]; set => graphicsflags[2] = value; }
        /// <summary> Whether this voxel receives shadows. </summary>
        public bool ReceivesShadows { get => graphicsflags[3]; set => graphicsflags[3] = value; }
        /// <summary>
        /// The size of this voxel, expressed as how many of this voxel
        /// would fit in a single unit cube on one axis.
        /// This is a value [1,16]. Other values wrap around.
        /// </summary>
        public int VoxelsPerUnit {
            get => (graphicsflags.Byte & 0x0F) + 1;
            set {
                value &= 0x0F;
                value -= 1;
                faceflags.SetBit(0, value & 1);
                faceflags.SetBit(1, value & 2);
                faceflags.SetBit(2, value & 4);
                faceflags.SetBit(3, value & 8);
            }
        }

        public override bool Equals(object obj) => obj is Voxel v && v.fullData == fullData;
        public bool Equals(Voxel other) => other.fullData == fullData;
        public override int GetHashCode() => fullData.GetHashCode();

        public static bool operator ==(Voxel a, Voxel b) => a.fullData == b.fullData;
        public static bool operator !=(Voxel a, Voxel b) => !(a == b);
    }
}