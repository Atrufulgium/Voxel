using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace Atrufulgium.Voxel.Collections {
    /// <summary>
    /// A priority-queue with FIFO for same-priority elements.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Laziest implementation -- all operations are slower than they should
    /// be, and it absolutely destroys your memory.
    /// </para>
    /// <para>
    /// At some point replace with the proper implementation using heaps.
    /// </para>
    /// </remarks>
    public class PriorityQueue<TElement, TPriority>
        : IEnumerable<TElement>,
          IReadOnlyCollection<TElement>,
          ICollection<TElement>,
          ICollection<(TElement element, TPriority priority)>
    {

        // Note that every queue in here is non-empty at all times for easy "first"ing.
        readonly SortedDictionary<TPriority, Queue<TElement>> elements;

        public PriorityQueue() {
            elements = new();
        }
        public PriorityQueue(IComparer<TPriority> comparer) {
            elements = new(comparer);
        }
        public PriorityQueue(IEnumerable<(TElement element, TPriority priority)> elements) : this() {
            foreach (var (element, priority) in elements)
                Enqueue(element, priority);
        }
        public PriorityQueue(IComparer<TPriority> comparer, IEnumerable<(TElement element, TPriority priority)> elements) : this(comparer) {
            foreach (var (element, priority) in elements)
                Enqueue(element, priority);
        }

        public int Count { get; private set; } = 0;


        public void Enqueue(TElement element, TPriority priority) {
            if (!elements.TryGetValue(priority, out var q)) {
                q = new();
                elements.Add(priority, q);
            }
            q.Enqueue(element);
            Count++;
        }

        public bool TryDequeue(out TElement result) {
            result = default;
            if (Count == 0)
                return false;

            var first = elements.First();
            var priority = first.Key;
            var q = first.Value;
            result = q.Dequeue();
            Count--;
            if (q.Count == 0)
                elements.Remove(priority);
            return true;
        }

        public bool TryPeek(out TElement result) {
            result = default;
            if (Count == 0)
                return false;

            result = elements.First().Value.Peek();
            return true;
        }

        public TElement Dequeue()
            => TryDequeue(out var result) ? result
            : throw new InvalidOperationException("The priority queue is empty");

        public TElement Peek()
            => TryPeek(out var result) ? result
            : throw new InvalidOperationException("The priority queue is empty");

        public bool Contains(TElement item) {
            foreach (var q in elements.Values) {
                if (q.Contains(item))
                    return true;
            }
            return false;
        }

        public void Clear() {
            Count = 0;
            elements.Clear();
        }

        public bool Remove((TElement element, TPriority priority) item) {
            if (!elements.TryGetValue(item.priority, out var q))
                return false;

            Queue<TElement> newQ = new(q.Count);
            bool found = false;
            foreach (var ele in q) {
                if (!found && ele.Equals(item.element)) {
                    found = true;
                    continue;
                }
                newQ.Enqueue(ele);
            }
            if(found) {
                Count--;
                if (newQ.Count == 0)
                    elements.Remove(item.priority);
                else
                    elements[item.priority] = newQ;
            }
            return found;
        }

        public void RemoveAll((TElement element,TPriority priority) item) {
            if (!elements.TryGetValue(item.priority, out var q))
                return;

            Queue<TElement> newQ = new(q.Count);
            foreach (var ele in q) {
                if (ele.Equals(item.element)) {
                    Count--;
                    continue;
                }
                newQ.Enqueue(ele);
            }
            if (newQ.Count == 0)
                elements.Remove(item.priority);
            else
                elements[item.priority] = newQ;
        }

        public bool Remove(TElement item) {
            foreach (var priority in elements.Keys) {
                if (Remove((item, priority)))
                    return true;
            }
            return false;
        }

        public void RemoveAll(TElement item) {
            foreach (var priority in elements.Keys)
                RemoveAll((item, priority));
        }

        bool ICollection<TElement>.IsReadOnly => false;
        bool ICollection<(TElement element, TPriority priority)>.IsReadOnly => false;

        void ICollection<TElement>.Add(TElement item)
            => Enqueue(item, default);

        void ICollection<(TElement element, TPriority priority)>.Add((TElement element, TPriority priority) item)
            => Enqueue(item.element, item.priority);

        bool ICollection<(TElement element, TPriority priority)>.Contains((TElement element, TPriority priority) item)
            => elements.TryGetValue(item.priority, out var q) && q.Contains(item.element);

        void ICollection<(TElement element, TPriority priority)>.CopyTo((TElement element, TPriority priority)[] array, int arrayIndex)
            => ((IEnumerable<(TElement element, TPriority priority)>)this).ToList().CopyTo(array, arrayIndex);

        void ICollection<TElement>.CopyTo(TElement[] array, int arrayIndex)
            => ((IEnumerable<TElement>)this).ToList().CopyTo(array, arrayIndex);

        IEnumerator IEnumerable.GetEnumerator()
            => ((IEnumerable<TElement>)this).GetEnumerator();

        IEnumerator<(TElement element, TPriority priority)> IEnumerable<(TElement element, TPriority priority)>.GetEnumerator() {
            foreach (var (priority, q) in elements) {
                foreach (var item in q)
                    yield return (item, priority);
            }
        }

        IEnumerator<TElement> IEnumerable<TElement>.GetEnumerator() {
            foreach (var (_, q) in elements) {
                foreach (var item in q)
                    yield return item;
            }
        }
    }
}
