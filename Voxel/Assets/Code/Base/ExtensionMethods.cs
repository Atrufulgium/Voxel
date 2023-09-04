using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;

namespace Atrufulgium.Voxel.Base {
    public static class ExtensionMethods {
        /// <summary>
        /// Resets the entire array contents to <tt>default</tt>.
        /// </summary>
        public static unsafe void Clear<T>(this NativeArray<T> arr) where T : struct {
            int length = arr.Length;
            void* start = arr.GetUnsafePtr();
            UnsafeUtility.MemClear(start, (long)length * UnsafeUtility.SizeOf<T>());
        }
    }
}