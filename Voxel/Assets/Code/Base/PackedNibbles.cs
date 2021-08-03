namespace Atrufulgium.Voxels.Base {
    /// <summary> Two [0-15] values in one byte. </summary>
    public struct PackedNibbles {
        public byte Byte { get; private set; }

        /// <summary>
        /// Gets or sets the lower half.
        /// This is a value [0,15]. Higher values wrap around.
        /// </summary>
        public int Lower {
            get {
                return Byte & 0x0F;
            }
            set {
                // Clear lower half
                Byte &= 0xF0;
                // Set lower half
                Byte |= (byte)(value & 0x0F);
            }
        }

        /// <summary>
        /// Gets or sets the upper half.
        /// This is a value [0,15]. Higher values wrap around.
        /// </summary>
        public int Upper {
            get {
                return (Byte & 0xF0) >> 4;
            }
            set {
                // Clear upper half
                Byte &= 0x0F;
                // Set upper half
                Byte |= (byte)((value & 0x0F) << 4);
            }
        }

        /// <summary> Copy another instance of PackedBits. </summary>
        public PackedNibbles(PackedNibbles other) => Byte = other.Byte;
        /// <summary> Create a value from scratch. </summary>
        /// <remarks> Recommended to use hex formatting: 0xF0. </remarks>
        public PackedNibbles(byte value) => Byte = value;

        public override bool Equals(object obj) => obj is PackedNibbles p && p.Byte == Byte;
        public override int GetHashCode() => Byte.GetHashCode();
        public override string ToString() => Byte.ToString("X2");
    }
}