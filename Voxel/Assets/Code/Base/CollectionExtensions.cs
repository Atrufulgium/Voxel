using System;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;

namespace Atrufulgium.Voxel.Base {
    internal static class CollectionExtensions {
        public unsafe static T* GetUnsafeTypedPtr<T>(this NativeReference<T> reference) where T : unmanaged
            => (T*)reference.GetUnsafePtr();

        public static unsafe T* GetUnsafeTypedPtr<T>(this NativeArray<T> nativeArray) where T : unmanaged
            => (T*)nativeArray.GetUnsafePtr();

        public static unsafe T* GetUnsafeTypedReadOnlyPtr<T>(this NativeArray<T> nativeArray) where T : unmanaged
            => (T*)nativeArray.GetUnsafeReadOnlyPtr();

        /// <summary>
        /// Gets a typed pointer to the collection underlying a NativeList.
        /// <br/>
        /// <b><i>Warning:</i></b> Lists' underlying pointer may be moved
        /// around after changing capacity. Persistent use of this pointer, or
        /// use of this pointer in contexts where capacity changes, is dangerous
        /// and likely to result in unintended behaviour.
        /// </summary>
        public static unsafe T* GetUnsafeTypedPtr<T>(this NativeList<T> nativeArray) where T : unmanaged
            => (T*)nativeArray.GetUnsafePtr();

        /// <inheritdoc cref="GetUnsafeTypedPtr{T}(NativeList{T})"/>
        public static unsafe T* GetUnsafeTypedReadOnlyPtr<T>(this NativeList<T> nativeArray) where T : unmanaged
            => (T*)nativeArray.GetUnsafeReadOnlyPtr();

        // Just here for consistency, imma not remember that this one specifically has a public field
        public static unsafe T* GetUnsafeTypedPtr<T>(this UnsafeList<T> unsafeList) where T : unmanaged
            => unsafeList.Ptr;

        /// <summary>
        /// Makes a copy of the contents of a list into a new list. This copies
        /// over all values.
        /// <br/>
        /// (But due to the existence of <see cref="NativeReference{T}"/>, the
        ///  referenced variables may end up being the same.)
        /// </summary>
        public static unsafe UnsafeList<T> Clone<T>(ref this UnsafeList<T> list, Allocator allocator) where T : unmanaged {
            UnsafeList<T> ret = new(list.Length, allocator);
            UnsafeUtility.MemCpy(ret.Ptr, list.Ptr, list.Length);
            return ret;
        }

        /// <summary>
        /// Inserts an item into the array such that `list[index]` becomes `item`.
        /// <br/>
        /// <b><i>Warning:</i></b> This may move the List's underlying pointer.
        /// </summary>
        // InsertRangeWithBeginEnd didn't work as I expected it to, so handrolling it :(
        public static unsafe void Insert<T>(ref this NativeList<T> list, int index, T item) where T : unmanaged {
            if (index < 0 || index > list.Length)
                throw new ArgumentOutOfRangeException("Tried to insert outside of the list.");

            // Shortcut: we can just add at the end of the array
            if (index == list.Length) {
                list.Add(item);
                return;
            }

            // Placeholder entry at the end. This may resize the underlaying
            // array and thus move the pointer, so we can only obtain that
            // afterwards.
            list.Add(default);

            // Note that UnsafeUtility.MemMove also correctly handles overlap.
            var ptr = list.GetUnsafeTypedPtr();
            UnsafeUtility.MemMove(
                destination: ptr + index + 1,
                source: ptr + index,
                // -1 to account for the added entry (just draw it out)
                // due to our early return branch, we copy more than nothing
                size: sizeof(T) * (list.Length - 1 - index)
            );
            *(ptr + index) = item;
        }

        /// <summary>
        /// Inserts an item into the array such that `list[index]` becomes `item1`
        /// and `list[index+1]` becomes `item2`.
        /// <br/>
        /// <b><i>Warning:</i></b> This may move the List's underlying pointer.
        /// </summary>
        public static unsafe void InserTwo<T>(ref this NativeList<T> list, int index, T item1, T item2) where T : unmanaged {
            if (index < 0 || index > list.Length)
                throw new ArgumentOutOfRangeException("Tried to insert outside of the list.");

            if (index == list.Length) {
                list.Add(item1);
                list.Add(item2);
                return;
            }

            list.Add(default);
            list.Add(default);

            var ptr = list.GetUnsafeTypedPtr();
            UnsafeUtility.MemMove(
                destination: ptr + index + 2,
                source: ptr + index,
                size: sizeof(T) * (list.Length - 2 - index)
            );
            *(ptr + index) = item1;
            *(ptr + index + 1) = item2;
        }
    }
}
