using System;

namespace Atrufulgium.Voxels.Base {
    /// <summary> Eight boolean values in one byte. </summary>
    public struct PackedBits {
        public byte Byte { get; private set; }

        /// <summary> Returns the value of the bit at an index [0,7]. </summary>
        public int GetBitAsInt(int index) => (Byte >> index) & 1;
        /// <summary> Returns the value of the bit at an index [0,7]. </summary>
        public bool GetBit(int index) => GetBitAsInt(index) == 1;

        /// <summary> Set the value of the bit at an index [0,7]. </summary>
        public void SetBit(int index, int value) {
            value = value != 0 ? 1 : 0;
            Byte = (byte)((Byte & ~(1 << index)) | (value << index));
        }
        /// <summary> Set the value of the bit at an index [0,7]. </summary>
        public void SetBit(int index, bool value) {
            int nval = value ? 1 : 0;
            Byte = (byte)((Byte & ~(1 << index)) | (nval << index));
        }

        /// <summary> Copy another instance of PackedBits. </summary>
        public PackedBits(PackedBits other) => Byte = other.Byte;
        /// <summary> Create a value from scratch. </summary>
        /// <remarks> Recommended to use binary formatting: 0b0101_1010. </remarks>
        public PackedBits(byte value) => Byte = value;

        public bool this[int i] { get => GetBit(i); set => SetBit(i, value); }

        public override bool Equals(object obj) => obj is PackedBits p && p.Byte == Byte;
        public override int GetHashCode() => Byte.GetHashCode();
        public override string ToString() => Convert.ToString(Byte, 2).PadLeft(8, '0');
    }
}