using System;
using System.Collections;
using System.Collections.Generic;

namespace Atrufulgium.Voxel.Collections {
    /// <summary>
    /// Represents a queue that contains no duplicate elements. Adding an
    /// element that already exists does nothing. Otherwise, see the docs of
    /// <see cref="Queue{T}"/>.
    /// </summary>
    /// <remarks>
    /// This is implemented the lame way - using both a <see cref="Queue{T}"/>
    /// and <see cref="HashSet{T}"/> at once, so if you care about memory, be
    /// careful.
    /// </remarks>
    public class QueueSet<T> : IEnumerable<T>, IReadOnlyCollection<T>, ICollection {

        private readonly Queue<T> queue = new();
        private readonly HashSet<T> set = new();

        public int Count => queue.Count;

        public void Clear() {
            queue.Clear();
            set.Clear();
        }

        public bool Contains(T item)
            => set.Contains(item);

        public void CopyTo(T[] array, int arrayIndex)
            => queue.CopyTo(array, arrayIndex);

        public T Dequeue() {
            T item = queue.Dequeue();
            set.Remove(item);
            return item;
        }

        public void Enqueue(T item) {
            if (!set.Contains(item)) {
                queue.Enqueue(item);
                set.Add(item);
            }
        }

        public IEnumerator<T> GetEnumerator()
            => ((IEnumerable<T>)queue).GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator()
            => ((IEnumerable)queue).GetEnumerator();

        public T Peek()
            => queue.Peek();

        public T[] ToArray()
            => queue.ToArray();

        public bool TryDequeue(out T result) {
            if (Count > 0) {
                result = Dequeue();
                return true;
            }
            result = default;
            return false;
        }

        public bool TryPeek(out T result)
            => queue.TryPeek(out result);

        void ICollection.CopyTo(Array array, int index)
            => ((ICollection)queue).CopyTo(array, index);

        bool ICollection.IsSynchronized => ((ICollection)queue).IsSynchronized;

        object ICollection.SyncRoot => ((ICollection)queue).SyncRoot;
    }
}
