using Unity.Burst.CompilerServices;
using Unity.Mathematics;

namespace Atrufulgium.Voxel.Base {
    // Reminder because I'm a dumbass:
    // >> and << are mod 32. >>32 does not set something to 0.
    /// <summary>
    /// A class for working with individual bits. Most methods have a
    /// <tt>..Low</tt> and a <tt>..High</tt> variant where which side of the
    /// uint is 0, is respectively the LSB and the MSB. This is needed because
    /// <see cref="math.reversebits(uint)"/> is terrible.
    /// </summary>
    public static class BitMath {

        /// <summary>
        /// <para>
        /// Returns the zero-indexed bit-position of the first set bit.
        /// The LSB is index 0, the MSB is index 31. Returns 32 if 0.
        /// </para>
        /// <para>
        /// This is optionally clamped to [0,<see cref="max"/>].
        /// </para>
        /// </summary>
        [return: AssumeRange(0, 32)]
        public static int FirstBinaryOneLow(uint i, int max = 32)
            => math.min(math.select(math.tzcnt(i), 32, i == 0), max);

        /// <inheritdoc cref="FirstBinaryOneLow(uint, int)"/>
        public static int4 FirstBinaryOneLow(uint4 i, int max = 32)
            => math.min(math.select(math.tzcnt(i), 32, i == 0), max);

        /// <summary>
        /// <para>
        /// Returns the zero-indexed bit-position of the first set bit.
        /// The LSB is index 0, the MSB is index 63. Returns 64 if 0.
        /// </para>
        /// <para>
        /// This is optionally clamped to [0,<see cref="max"/>].
        /// </para>
        /// </summary>
        [return: AssumeRange(0, 64)]
        public static int FirstBinaryOneLow64(ulong i, int max = 64)
            => math.min(math.select(math.tzcnt(i), 64, i == 0), max);

        /// <summary>
        /// <para>
        /// Returns the zero-indexed bit-position of the first set bit.
        /// The MSB is index 0, the LSB is index 31. Returns 32 if 0.
        /// </para>
        /// <para>
        /// This is optionally clamped to [0,<see cref="max"/>].
        /// </para>
        /// </summary>
        [return: AssumeRange(0, 32)]
        public static int FirstBinaryOneHigh(uint i, int max = 32)
            => math.min(math.select(math.lzcnt(i), 32, i == 0), max);

        /// <inheritdoc cref="FirstBinaryOneHigh(uint, int)"/>
        public static int4 FirstBinaryOneHigh(uint4 i, int max = 32)
            => math.min(math.select(math.lzcnt(i), 32, i == 0), max);

        /// <summary>
        /// <para>
        /// Returns the zero-indexed bit-position of the first set bit.
        /// The MSB is index 0, the LSB is index 63. Returns 64 if 0.
        /// </para>
        /// <para>
        /// This is optionally clamped to [0,<see cref="max"/>].
        /// </para>
        /// </summary>
        [return: AssumeRange(0, 64)]
        public static int FirstBinaryOneHigh64(ulong i, int max = 64)
            => math.min(math.select(math.lzcnt(i), 64, i == 0), max);

        /// <summary>
        /// <para>
        /// Returns the zero-indexed bit-position of the last set bit.
        /// The LSB is index 0, the MSB is index 31. Returns 32 if 0.
        /// </para>
        /// <para>
        /// This is optionally clamped to [0,<see cref="max"/>].
        /// </para>
        /// </summary>
        [return: AssumeRange(0, 32)]
        public static int LastBinaryOneLow(uint i, int max = 32)
            => math.min(math.select(32 - math.lzcnt(i), 32, i == 0), max);

        /// <inheritdoc cref="LastBinaryOneLow(uint, int)"/>
        public static int4 LastBinaryOneLow(uint4 i, int max = 32)
            => math.min(math.select(32 - math.lzcnt(i), 32, i == 0), max);

        /// <summary>
        /// <para>
        /// Returns the zero-indexed bit-position of the last set bit.
        /// The LSB is index 0, the MSB is index 63. Returns 64 if 0.
        /// </para>
        /// <para>
        /// This is optionally clamped to [0,<see cref="max"/>].
        /// </para>
        /// </summary>
        [return: AssumeRange(0, 64)]
        public static int LastBinaryOneLow64(ulong i, int max = 64)
            => math.min(math.select(64 - math.lzcnt(i), 64, i == 0), max);

        /// <summary>
        /// <para>
        /// Returns the zero-indexed bit-position of the last set bit.
        /// The MSB is index 0, the LSB is index 31. Returns 32 if 0.
        /// </para>
        /// <para>
        /// This is optionally clamped to [0,<see cref="max"/>].
        /// </para>
        /// </summary>
        [return: AssumeRange(0, 32)]
        public static int LastBinaryOneHigh(uint i, int max = 32)
            => math.min(math.select(32 - math.tzcnt(i), 32, i == 0), max);

        /// <inheritdoc cref="LastBinaryOneLow(uint, int)"/>
        public static int4 LastBinaryOneHigh(uint4 i, int max = 32)
            => math.min(math.select(32 - math.tzcnt(i), 32, i == 0), max);

        /// <summary>
        /// <para>
        /// Returns the zero-indexed bit-position of the last set bit.
        /// The MSB is index 0, the LSB is index 63. Returns 64 if 0.
        /// </para>
        /// <para>
        /// This is optionally clamped to [0,<see cref="max"/>].
        /// </para>
        /// </summary>
        [return: AssumeRange(0, 64)]
        public static int LastBinaryOneHigh64(ulong i, int max = 64)
            => math.min(math.select(64 - math.tzcnt(i), 64, i == 0), max);

        /// <summary>
        /// <para>
        /// Returns a uint that, counting from the LSB, is set to <tt>1</tt>
        /// <paramref name="i"/> times, and then set to <tt>0</tt>
        /// 32-<paramref name="i"/> times.
        /// </para>
        /// <para>
        /// Valid inputs are 0..32. This is not verified.
        /// </para>
        /// </summary>
        /// <remarks>
        /// This is <i>excluding</i> index <paramref name="i"/> itself.
        /// As such, <code>FirstBinaryOneLow(AllOnesUpToLow(i+1)) = i</code>.
        /// </remarks>
        public static uint AllOnesUpToLow([AssumeRange(0, 32)] int i)
            => math.select(uint.MaxValue >> -i, 0, i == 0);

        /// <summary>
        /// <para>
        /// Returns a ulong that, counting from the LSB, is set to <tt>1</tt>
        /// <paramref name="i"/> times, and then set to <tt>0</tt>
        /// 64-<paramref name="i"/> times.
        /// </para>
        /// <para>
        /// Valid inputs are 0..64. This is not verified.
        /// </para>
        /// </summary>
        /// <remarks>
        /// This is <i>excluding</i> index <paramref name="i"/> itself.
        /// As such, <code>FirstBinaryOneLow(AllOnesUpToLow(i+1)) = i</code>.
        /// </remarks>
        public static ulong AllOnesUpToLow64([AssumeRange(0, 64)] int i)
            => math.select(ulong.MaxValue >> -i, 0, i == 0);

        /// <summary>
        /// <para>
        /// Returns a uint that, counting from the MSB, is set to <tt>1</tt>
        /// <paramref name="i"/> times, and then set to <tt>0</tt>
        /// 32-<paramref name="i"/> times.
        /// </para>
        /// <para>
        /// Valid inputs are 0..32. This is not verified.
        /// </para>
        /// </summary>
        /// <remarks>
        /// This is <i>excluding</i> index <paramref name="i"/> itself.
        /// As such, <code>FirstBinaryOneHigh(AllOnesUpToHigh(i+1)) = i</code>.
        /// </remarks>
        public static uint AllOnesUpToHigh([AssumeRange(0, 32)] int i)
            => math.select(uint.MaxValue - (uint.MaxValue >> i), uint.MaxValue, i == 32);

        /// <summary>
        /// <para>
        /// Returns a ulong that, counting from the MSB, is set to <tt>1</tt>
        /// <paramref name="i"/> times, and then set to <tt>0</tt>
        /// 64-<paramref name="i"/> times.
        /// </para>
        /// <para>
        /// Valid inputs are 0..64. This is not verified.
        /// </para>
        /// </summary>
        /// <remarks>
        /// This is <i>excluding</i> index <paramref name="i"/> itself.
        /// As such, <code>FirstBinaryOneHigh(AllOnesUpToHigh(i+1)) = i</code>.
        /// </remarks>
        public static ulong AllOnesUpToHigh64([AssumeRange(0, 64)] int i)
            => math.select(ulong.MaxValue - (ulong.MaxValue >> i), ulong.MaxValue, i == 64);

        /// <summary>
        /// <para>
        /// Returns a uint that, counting from the LSB, is set to <tt>1</tt>
        /// on indices [<paramref name="i1"/>,<paramref name="i2"/>).
        /// </para>
        /// <para>
        /// Valid inputs are 0..32. This is not verified. Returns 0 if
        /// <paramref name="i1"/> &gt;= <paramref name="i2"/>.
        /// </para>
        /// </summary>
        public static uint AllOnesIntervalLow([AssumeRange(0, 32)] int i1, [AssumeRange(0, 32)] int i2)
            => AllOnesUpToLow(i2) & ~AllOnesUpToLow(i1);

        /// <summary>
        /// <para>
        /// Returns a ulong that, counting from the LSB, is set to <tt>1</tt>
        /// on indices [<paramref name="i1"/>,<paramref name="i2"/>).
        /// </para>
        /// <para>
        /// Valid inputs are 0..64. This is not verified. Returns 0 if
        /// <paramref name="i1"/> &gt;= <paramref name="i2"/>.
        /// </para>
        /// </summary>
        public static ulong AllOnesIntervalLow64([AssumeRange(0, 64)] int i1, [AssumeRange(0, 64)] int i2)
            => AllOnesUpToLow64(i2) & ~AllOnesUpToLow64(i1);

        /// <summary>
        /// <para>
        /// Returns a uint that, counting from the MSB, is set to <tt>1</tt>
        /// on indices [<paramref name="i1"/>,<paramref name="i2"/>).
        /// </para>
        /// <para>
        /// Valid inputs are 0..32. This is not verified. Returns 0 if
        /// <paramref name="i1"/> &gt;= <paramref name="i2"/>.
        /// </para>
        /// </summary>
        public static uint AllOnesIntervalHigh([AssumeRange(0, 32)] int i1, [AssumeRange(0, 32)] int i2)
            => AllOnesUpToHigh(i2) & ~AllOnesUpToHigh(i1);

        /// <summary>
        /// <para>
        /// Returns a ulong that, counting from the MSB, is set to <tt>1</tt>
        /// on indices [<paramref name="i1"/>,<paramref name="i2"/>).
        /// </para>
        /// <para>
        /// Valid inputs are 0..64. This is not verified. Returns 0 if
        /// <paramref name="i1"/> &gt;= <paramref name="i2"/>.
        /// </para>
        /// </summary>
        public static ulong AllOnesIntervalHigh64([AssumeRange(0, 64)] int i1, [AssumeRange(0, 64)] int i2)
            => AllOnesUpToHigh64(i2) & ~AllOnesUpToHigh64(i1);
    }
}