using System.Collections;
using System.Collections.Generic;

namespace Atrufulgium.Voxel.Collections {
    /// <summary>
    /// Represents a dictionary with two-way O(1) lookup. The standard
    /// dictionary methods here have overloads for *both* generic types.
    /// </summary>
    /// <remarks>
    /// If you want both keys and values to be the same, use the single-generic
    /// parameter variant, <see cref="BiDictionary{T}"/>. Otherwise, all
    /// methods here get incredibly awkward to call.
    /// </remarks>
    public class BiDictionary<T1, T2> :
        ICollection<KeyValuePair<T1, T2>>,
        IDictionary<T1, T2>,
        IEnumerable<KeyValuePair<T1, T2>>,
        IReadOnlyCollection<KeyValuePair<T1, T2>> {

        readonly Dictionary<T1, T2> forward;
        readonly Dictionary<T2, T1> backward;

        public BiDictionary() {
            forward = new();
            backward = new();
        }
        public BiDictionary(IEnumerable<KeyValuePair<T1, T2>> dictionary) {
            forward = new(dictionary);
            backward = new(forward.Count);
            foreach (var kv in dictionary)
                backward.Add(kv.Value, kv.Key);
        }
        public BiDictionary(IDictionary<T1, T2> dictionary)
            : this((IEnumerable<KeyValuePair<T1, T2>>)dictionary) { }
        public BiDictionary(IEnumerable<KeyValuePair<T2, T1>> dictionary) {
            backward = new(dictionary);
            forward = new(backward.Count);
            foreach (var kv in dictionary)
                forward.Add(kv.Value, kv.Key);
        }
        public BiDictionary(IDictionary<T2, T1> dictionary)
            : this((IEnumerable<KeyValuePair<T2, T1>>)dictionary) { }
        public BiDictionary(int capacity) {
            forward = new(capacity);
            backward = new(capacity);
        }
        public BiDictionary(IEqualityComparer<T1> t1Comparer) {
            forward = new(t1Comparer);
            backward = new();
        }
        public BiDictionary(IEqualityComparer<T2> t2Comparer) {
            forward = new();
            backward = new(t2Comparer);
        }
        public BiDictionary(IEqualityComparer<T1> t1Comparer, IEqualityComparer<T2> t2Comparer) {
            forward = new(t1Comparer);
            backward = new(t2Comparer);
        }
        public BiDictionary(BiDictionary<T1,T2> old) {
            forward = new(old.forward);
            backward = new(old.backward);
        }

        public T2 this[T1 key] {
            get => forward[key];
            set {
                forward[key] = value;
                backward[value] = key;
            }
        }

        public T1 this[T2 key] {
            get => backward[key];
            set {
                backward[key] = value;
                forward[value] = key;
            }
        }

        public int Count => forward.Count;

        bool ICollection<KeyValuePair<T1,T2>>.IsReadOnly => ((ICollection<KeyValuePair<T1, T2>>)forward).IsReadOnly;

        public ICollection<T1> Keys => forward.Keys;

        public ICollection<T2> Values => forward.Values;

        public void Add(KeyValuePair<T1, T2> item) {
            ((ICollection<KeyValuePair<T1, T2>>)forward).Add(item);
            ((ICollection<KeyValuePair<T2, T1>>)backward).Add(new(item.Value, item.Key));
        }
        public void Add(KeyValuePair<T2, T1> item)
            => Add(new KeyValuePair<T1, T2>(item.Value, item.Key));

        public void Add(T1 key, T2 value) {
            forward.Add(key, value);
            backward.Add(value, key);
        }
        public void Add(T2 key, T1 value)
            => Add(value, key);

        public void Clear() {
            forward.Clear();
            backward.Clear();
        }

        public bool Contains(KeyValuePair<T1, T2> item) {
            return ((ICollection<KeyValuePair<T1, T2>>)forward).Contains(item);
        }
        public bool Contains(KeyValuePair<T2, T1> item)
            => Contains(new KeyValuePair<T1, T2>(item.Value, item.Key));

        public bool ContainsKey(T1 key)
            => forward.ContainsKey(key);
        public bool ContainsKey(T2 key)
            => backward.ContainsKey(key);

        public void CopyTo(KeyValuePair<T1, T2>[] array, int arrayIndex)
            => ((ICollection<KeyValuePair<T1, T2>>)forward).CopyTo(array, arrayIndex);
        public void CopyTo(KeyValuePair<T2, T1>[] array, int arrayIndex)
            => ((ICollection<KeyValuePair<T2, T1>>)backward).CopyTo(array, arrayIndex);

        public IEnumerator<KeyValuePair<T1, T2>> GetEnumerator()
            => forward.GetEnumerator();

        public bool Remove(KeyValuePair<T1, T2> item) {
            ((ICollection<KeyValuePair<T2, T1>>)backward).Remove(new(item.Value, item.Key));
            return ((ICollection<KeyValuePair<T1, T2>>)forward).Remove(item);
        }
        public bool Remove(KeyValuePair<T2, T1> item)
            => Remove(new KeyValuePair<T1, T2>(item.Value, item.Key));

        public bool Remove(T1 key) {
            if (forward.TryGetValue(key, out T2 value)) {
                forward.Remove(key);
                backward.Remove(value);
                return true;
            }
            return false;
        }
        public bool Remove(T2 key) {
            if (backward.TryGetValue(key, out T1 value)) {
                backward.Remove(key);
                forward.Remove(value);
                return true;
            }
            return false;
        }

        public bool TryGetValue(T1 key, out T2 value)
            => forward.TryGetValue(key, out value);
        public bool TryGetValue(T2 key, out T1 value)
            => backward.TryGetValue(key, out value);

        IEnumerator IEnumerable.GetEnumerator() {
            return ((IEnumerable)forward).GetEnumerator();
        }
    }
}
