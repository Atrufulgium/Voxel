using System;
using System.Collections;
using System.Collections.Generic;

namespace Atrufulgium.Voxel.Collections {
    /// <summary>
    /// Represents a priority queue that contains no duplicate elements. Adding an
    /// element that already exists does nothing.
    /// </summary>
    /// <remarks>
    /// This is implemented the lame way - using both a <see cref="PriorityQueue{TElement, TPriority}"/>
    /// and <see cref="HashSet{T}"/> at once, so if you care about memory, be
    /// careful.
    /// </remarks>
    public class PriorityQueueSet<TElement, TPriority> : IEnumerable<TElement>, IReadOnlyCollection<TElement>, ICollection {

        private readonly PriorityQueue<TElement, TPriority> queue = new();
        private readonly HashSet<TElement> set = new();

        public int Count => queue.Count;

        public void Clear() {
            queue.Clear();
            set.Clear();
        }

        public bool Contains(TElement item)
            => set.Contains(item);

        public void CopyTo(TElement[] array, int arrayIndex)
            => ((ICollection<TElement>)queue).CopyTo(array, arrayIndex);

        public TElement Dequeue() {
            TElement item = queue.Dequeue();
            set.Remove(item);
            return item;
        }

        public void Enqueue(TElement item, TPriority priority) {
            if (!set.Contains(item)) {
                queue.Enqueue(item, priority);
                set.Add(item);
            }
        }

        public IEnumerator<TElement> GetEnumerator()
            => ((IEnumerable<TElement>)queue).GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator()
            => ((IEnumerable)queue).GetEnumerator();

        public TElement Peek()
            => queue.Peek();

        public bool TryDequeue(out TElement result) {
            if (Count > 0) {
                result = Dequeue();
                return true;
            }
            result = default;
            return false;
        }

        public bool TryPeek(out TElement result)
            => queue.TryPeek(out result);

        void ICollection.CopyTo(Array array, int index)
            => ((ICollection)queue).CopyTo(array, index);

        bool ICollection.IsSynchronized => ((ICollection)queue).IsSynchronized;

        object ICollection.SyncRoot => ((ICollection)queue).SyncRoot;
    }
}
