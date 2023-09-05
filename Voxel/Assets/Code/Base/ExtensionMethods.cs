using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;

namespace Atrufulgium.Voxel.Base {
    public static class ExtensionMethods {
        /// <summary>
        /// Resets the entire array contents to <tt>default</tt>.
        /// </summary>
        /// <remarks>
        /// Using this en masse makes it likely performance will be memory-bound.
        /// Please prefer the overload <see cref="Clear{T}(NativeArray{T}, int)"/>
        /// if it's possible to define an upper bound on nonzero data.
        /// </remarks>
        public static unsafe void Clear<T>(this NativeArray<T> arr) where T : struct {
            int length = arr.Length;
            void* start = arr.GetUnsafePtr();
            UnsafeUtility.MemClear(start, (long)length * UnsafeUtility.SizeOf<T>());
        }

        /// <summary>
        /// From the first index, clears up to length <paramref name="length"/>
        /// or <see cref="NativeArray{T}.Length"/>, whichever is smaller.
        /// </summary>
        public static unsafe void Clear<T>(this NativeArray<T> arr, int length) where T : struct {
            if (length > arr.Length)
                length = arr.Length;
            void* start = arr.GetUnsafePtr();
            UnsafeUtility.MemClear(start, (long)length * UnsafeUtility.SizeOf<T>());
        }
    }
}