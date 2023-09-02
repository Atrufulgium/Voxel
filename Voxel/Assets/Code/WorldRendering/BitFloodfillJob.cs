using System;
using System.Runtime.CompilerServices;
using Unity.Burst;
using Unity.Burst.CompilerServices;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;

namespace Atrufulgium.Voxel.WorldRendering {

    /// <summary>
    /// Does a floodfill over a compact binary collection of n^3, where n is
    /// one of 4, 8, 16, or 32.
    /// </summary>
    // There's some commented code that's helpful when debugging. Please keep it.
    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, OptimizeFor = OptimizeFor.Performance)]
    public struct BitFloodfillJob : IJob {
        /// <summary>
        /// <para>
        /// A 3D array with each axis up to 32 of length
        /// <see cref="max"/> × <see cref="max"/>. The other axis is inside
        /// the bits themselves.
        /// </para>
        /// <para>
        /// A value of <tt>1</tt> says floodfill is allowed, while a value of
        /// <tt>0</tt> represents a wall.
        /// </para>
        /// </summary>
        /// <remarks>
        /// No need to specify any bits inside the uints beyond
        /// <see cref="max"/>. We count from the LSB.
        /// </remarks>
        [ReadOnly]
        internal NativeArray<uint> allowsFloodfill;
        /// <summary>
        /// The length of each axis. MUST be in [1,32] inclusive and divisible
        /// by four.
        /// </summary>
        [ReadOnly]
        internal NativeReference<int> maxIndex;

        /// <summary>
        /// <para>
        /// Before running: an array in the same format as
        /// <see cref="allowsFloodfill"/>. All positions the floodfill starts
        /// from are set to 1. Must be a subset of <see cref="allowsFloodfill"/>
        /// (and this is not checked).
        /// </para>
        /// <para>
        /// After running: values of <tt>1</tt> represent being reached by
        /// this floodfill.
        /// </para>
        /// </summary>
        internal NativeArray<uint> arena;

        [SkipLocalsInit] // Don't zero-init stackallocs
        unsafe public void Execute() {
            // X-axis: inside the uint itself.
            // Y-axis: SIMD'd four uints at a time.
            // Z-axis: one row of the above at a time.
            // Kinda sad how this doesn't align with int2's x/y.
            int2 max = default;
            ref int maxY = ref max.x;
            ref int maxZ = ref max.y;

            maxZ = maxIndex.Value;
            if (!(maxZ == 4 || maxZ == 8 || maxZ == 16 || maxZ == 32))
                throw new InvalidOperationException("The max value is invalid.");

            maxY = maxZ / 4;

            uint4* allowsFloodfill = (uint4*)this.allowsFloodfill.GetUnsafeReadOnlyPtr();
            uint4* arena = (uint4*)this.arena.GetUnsafePtr();

            #region scheduling
            // Z-axis: inside the uint itself.
            // Y-axis: the pointer.
            uint* scheduled = stackalloc uint[maxY];
            // Up to 1kB.
            // TODO: Prove whether this can be smaller.
            int* todoStack = stackalloc int[maxY * maxZ];
            int todoStackTop = -1; // -1 represents "empty"
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            void Push(int2 index) {
                if (math.any(index < 0 | index >= max))
                    return;
                // Would really like to just write maxY instead of max.x but
                // c# hates me.
                int y = index.x;
                int z = index.y;
                int maxY = max.x;
                todoStackTop++;
                todoStack[todoStackTop] = y + maxY * z;
                scheduled[y] |= 1u << z;
            }
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            int2 Pop() {
                var index = todoStack[todoStackTop];
                todoStackTop--;
                int y = index % max.x;
                int z = index / max.x;
                scheduled[y] &= ~(1u << z);
                return new(y, z);
            }
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            bool TestScheduled(int y, int z) {
                return (scheduled[y] & (1u << z)) > 0;
            }
            #endregion

            for (int y = 0; y < maxY; y++) {
                for (int z = 0; z < maxZ; z++) {
                    int index = y + maxY * z;
                    if (math.any(arena[index] != 0)) {
                        arena[index] = arena[index];
                        Push(new(y, z));
                    }
                }
            }

            //int iter = 0;
            while (todoStackTop != -1) {
                int2 i = Pop();
                ref int y = ref i.x;
                ref int z = ref i.y;
                Hint.Assume(y >= 0 && y < maxY);
                Hint.Assume(z >= 0 && z < maxZ);
                bool2 small = i < max - 1;
                ref bool smallY = ref small.x;
                ref bool smallZ = ref small.y;
                bool2 large = i > 0;
                ref bool largeY = ref large.x;
                ref bool largeZ = ref large.y;

                uint4* curr = arena + (y + maxY * z);      // Always safe to access
                uint4* prev = (uint4*)((uint*)curr - 1);    // Safe if largeY
                uint4* next = (uint4*)((uint*)curr + 1);    // Safe if smallY
                uint4* left = curr - maxY;                  // Safe if largeZ
                uint4* righ = curr + maxY;                  // Safe if smallZ
                long pointerDiff = allowsFloodfill - arena;
                uint4* currAllowed = curr + pointerDiff;    // Always safe to access
                uint4* prevAllowed = prev + pointerDiff;    // Safe if largeY
                uint4* nextAllowed = next + pointerDiff;    // safe if smallY
                uint4* leftAllowed = left + pointerDiff;    // Safe if largeZ
                uint4* righAllowed = righ + pointerDiff;    // Safe if smallZ

                uint4 currVal = *curr;
                uint4 prevVal = Hint.Likely(largeY) ? *prev : 0;
                uint4 nextVal = Hint.Likely(smallY) ? *next : 0;
                uint4 leftVal = Hint.Likely(largeZ) ? *left : 0;
                uint4 righVal = Hint.Likely(smallZ) ? *righ : 0;

                uint4 currValOld = currVal;
                uint4 prevValOld = prevVal;
                uint4 nextValOld = nextVal;
                uint4 leftValOld = leftVal;
                uint4 righValOld = righVal;

                // Grow the neighbours with the current, and grow the current
                // with the neighbours.
                // Grow current and neighbours: X-axis
                currVal |= (currVal << 1) | (currVal >> 1);
                // Grow current: Y-axis (recall the aliasing)
                currVal |= Hint.Likely(largeY) ? prevVal : new(0, currVal.xyz);
                currVal |= Hint.Likely(smallY) ? nextVal : new(currVal.yzw, 0);
                // Grow current: Z-axis
                if (Hint.Likely(largeZ))
                    currVal |= leftVal;
                if (Hint.Likely(smallZ))
                    currVal |= righVal;
                currVal &= *currAllowed;
                // Grow neighbours: Y-axis
                if (Hint.Likely(largeY))
                    prevVal |= currVal & *prevAllowed;
                if (Hint.Likely(smallY))
                    nextVal |= currVal & *nextAllowed;
                // Grow neighbours: Z-axis
                if (Hint.Likely(largeZ))
                    leftVal |= currVal & *leftAllowed;
                if (Hint.Likely(smallZ))
                    righVal |= currVal & *righAllowed;

                // Assign back and schedule if changed and unscheduled.
                // Prioritise widening the X-axis as | is OP for Y, and
                // _especially_ Z down the line.
                // After that, prioritise the Y axis as moving Y across Z
                // with the SIMD is still 4x moving Z across Y.
                if (math.any(leftVal != leftValOld)) {
                    *left = leftVal;
                    if (!TestScheduled(y, z - 1))
                        Push(new(y, z - 1));
                }
                if (math.any(righVal != righValOld)) {
                    *righ = righVal;
                    if (!TestScheduled(y, z + 1))
                        Push(new(y, z + 1));
                }
                if (math.any(prevVal != prevValOld)) {
                    *prev = prevVal;
                    if (!TestScheduled(y-1, z))
                        Push(new(y-1, z));
                }
                if (math.any(nextVal != nextValOld)) {
                    *next = nextVal;
                    if (!TestScheduled(y+1, z))
                        Push(new(y+1, z));
                }
                if (Hint.Likely(math.any(currVal != currValOld))) {
                    *curr = currVal;
                    if (!TestScheduled(y, z))
                        Push(new(y, z));
                }

                // (When uncommenting this, don't forget iter's init above the while)
                //iter++;
                //string s = $"Iteration {iter}: Popped (Y {i.x}, Z {i.y})\n";
                //s += "  (Key: ← X, ↓ Y, →→ Z)\n";
                //for (int py = 0; py < maxY; py++) {
                //    s += "  ";
                //    for (int pz = 0; pz < maxZ; pz++) {
                //        s += "simd".PadLeft(maxZ, ' ');
                //        s += "   ";
                //    }
                //    s += "\n";
                //    for (int sub = 0; sub < 4; sub++) {
                //        s += sub switch { 
                //            0 => "x ", 1 => "y ", 2 => "z ", 3 => "w ", _ => ""
                //        };
                //        for (int pz = 0; pz < maxZ; pz++) { 
                //            s += Convert.ToString(result[py + maxY * pz][sub], 2).PadLeft(maxZ, '0').Replace('0', '_');
                //            s += "   ";
                //        }
                //        s += "\n";
                //    }
                //}
                //UnityEngine.Debug.Log(s);
            }
        }
    }
}
