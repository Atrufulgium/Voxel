using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Jobs;

namespace Atrufulgium.Voxel.Collections {
    /// <summary>
    /// Represents a queue that contains no duplicate elements. Adding an
    /// existing element does nothing.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This is implemented the lame way - using both a <see cref="NativeQueue{T}"/>
    /// and <see cref="NativeParallelHashSet{T}"/> at once, so if you care about
    /// memory and cache misses, be careful and preferably use something else
    /// if possible.
    /// </para>
    /// <para>
    /// As such, whenever you write, you also read: [WriteOnly] never works.
    /// </para>
    /// </remarks>
    public readonly struct NativeQueueSet<T>
        : INativeDisposable, IDisposable, IEnumerable<T>, IEnumerable, IReadOnlyCollection<T>
        where T : unmanaged, IEquatable<T>
    {
        private readonly NativeQueue<T> queue;
        private readonly NativeParallelHashSet<T> set;

        public NativeQueueSet(Allocator allocator) {
            queue = new(allocator);
            set = new(100, allocator);
        }

        public NativeQueueSet(Allocator allocator, int capacity) {
            queue = new(allocator);
            set = new(capacity, allocator);
        }

        public int Count => queue.Count;

        public void Clear() {
            queue.Clear();
            set.Clear();
        }

        public bool Contains(T item)
            => set.Contains(item);

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

        public T Peek()
            => queue.Peek();

        public bool TryDequeue(out T result) {
            if (Count > 0) {
                result = Dequeue();
                return true;
            }
            result = default;
            return false;
        }

        public void Dispose() {
            queue.Dispose();
            set.Dispose();
        }

        public JobHandle Dispose(JobHandle inputDeps) {
            return JobHandle.CombineDependencies(
                ((INativeDisposable)queue).Dispose(inputDeps),
                ((INativeDisposable)set).Dispose(inputDeps)
            );
        }

        public IEnumerator GetEnumerator()
            => throw new NotSupportedException("Use entities 2.2.0, or just Dequeue()-loop.");

        IEnumerator<T> IEnumerable<T>.GetEnumerator()
            => throw new NotSupportedException("Use entities 2.2.0, or just Dequeue()-loop.");
    }
}