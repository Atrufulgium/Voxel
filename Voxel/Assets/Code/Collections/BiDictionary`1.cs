using System.Collections;
using System.Collections.Generic;

namespace Atrufulgium.Voxel.Collections {
    /// <summary>
    /// Represents a dictionary with two-way O(1) lookup. The standard
    /// dictionary methods here have variants for the other direction,
    /// usually denoted by <tt>Reversed</tt>, but sometimes with
    /// <tt>key</tt> and <tt>value</tt> swapped in the method names.
    /// </summary>
    /// <remarks>
    /// If you want the keys and values to be the different, use the two
    /// generic parameter variant <see cref="BiDictionary{T1, T2}"/>.
    /// </remarks>
    public class BiDictionary<T> :
        ICollection<KeyValuePair<T, T>>,
        IDictionary<T, T>,
        IEnumerable<KeyValuePair<T, T>>,
        IReadOnlyCollection<KeyValuePair<T, T>> {

        readonly Dictionary<T, T> forward;
        readonly Dictionary<T, T> backward;

        public BiDictionary() {
            forward = new();
            backward = new();
        }
        public BiDictionary(IEnumerable<KeyValuePair<T, T>> dictionary) {
            forward = new(dictionary);
            backward = new(forward.Count);
            foreach (var kv in dictionary)
                backward.Add(kv.Value, kv.Key);
        }
        public BiDictionary(IDictionary<T, T> dictionary)
            : this((IEnumerable<KeyValuePair<T, T>>)dictionary) { }
        public BiDictionary(int capacity) {
            forward = new(capacity);
            backward = new(capacity);
        }
        public BiDictionary(IEqualityComparer<T> keyComparer) {
            forward = new(keyComparer);
            backward = new();
        }
        public BiDictionary(IEqualityComparer<T> keyComparer, IEqualityComparer<T> valueComparer) {
            forward = new(keyComparer);
            backward = new(valueComparer);
        }
        public BiDictionary(BiDictionary<T> old) {
            forward = new(old.forward);
            backward = new(old.backward);
        }

        T IDictionary<T,T>.this[T key] {
            get => GetFromKey(key);
            set => SetFromKey(key, value);
        }
        /// <summary> Indexes the bidictionary from the first entry. </summary>
        public T GetFromKey(T key) => forward[key];
        /// <inheritdoc cref="GetFromKey(T)"/>
        public void SetFromKey(T key, T value) {
            forward[key] = value;
            backward[value] = key;
        }
        /// <summary> Indexes the bidictionary from the second entry. </summary>
        public T GetFromValue(T value) => backward[value];
        /// <inheritdoc cref="GetFromValue(T)"/>
        public void SetFromValue(T value, T key)
            => SetFromKey(key, value);

        public int Count => forward.Count;

        bool ICollection<KeyValuePair<T, T>>.IsReadOnly => ((ICollection<KeyValuePair<T, T>>)forward).IsReadOnly;

        public ICollection<T> Keys => forward.Keys;

        public ICollection<T> Values => forward.Values;

        public void Add(KeyValuePair<T, T> keyvalue) {
            ((ICollection<KeyValuePair<T, T>>)forward).Add(keyvalue);
            ((ICollection<KeyValuePair<T, T>>)backward).Add(new(keyvalue.Value, keyvalue.Key));
        }
        public void AddReversed(KeyValuePair<T, T> valuekey)
            => Add(new KeyValuePair<T, T>(valuekey.Value, valuekey.Key));

        public void Add(T key, T value) {
            forward.Add(key, value);
            backward.Add(value, key);
        }
        public void AddReversed(T value, T key)
            => Add(key, value);

        public void Clear() {
            forward.Clear();
            backward.Clear();
        }

        public bool Contains(KeyValuePair<T, T> keyvalue) {
            return ((ICollection<KeyValuePair<T, T>>)forward).Contains(keyvalue);
        }
        public bool ContainsReversed(KeyValuePair<T, T> valuekey)
            => Contains(new KeyValuePair<T, T>(valuekey.Value, valuekey.Key));

        public bool ContainsKey(T key)
            => forward.ContainsKey(key);
        public bool ContainsValue(T value)
            => backward.ContainsKey(value);

        public void CopyTo(KeyValuePair<T, T>[] array, int arrayIndex)
            => ((ICollection<KeyValuePair<T, T>>)forward).CopyTo(array, arrayIndex);
        public void CopyToReversed(KeyValuePair<T, T>[] array, int arrayIndex)
            => ((ICollection<KeyValuePair<T, T>>)backward).CopyTo(array, arrayIndex);

        public IEnumerator<KeyValuePair<T, T>> GetEnumerator()
            => forward.GetEnumerator();

        public bool Remove(KeyValuePair<T, T> keyvalue) {
            ((ICollection<KeyValuePair<T, T>>)backward).Remove(new(keyvalue.Value, keyvalue.Key));
            return ((ICollection<KeyValuePair<T, T>>)forward).Remove(keyvalue);
        }
        public bool RemoveReversed(KeyValuePair<T, T> valuekey)
            => Remove(new KeyValuePair<T, T>(valuekey.Value, valuekey.Key));

        bool IDictionary<T, T>.Remove(T key)
            => RemoveByKey(key);
        public bool RemoveByKey(T key) {
            if (forward.TryGetValue(key, out T value)) {
                forward.Remove(key);
                backward.Remove(value);
                return true;
            }
            return false;
        }
        public bool RemoveByValue(T value) {
            if (backward.TryGetValue(value, out T key)) {
                backward.Remove(value);
                forward.Remove(key);
                return true;
            }
            return false;
        }

        public bool TryGetValue(T key, out T value)
            => forward.TryGetValue(key, out value);
        public bool TryGetKey(T value, out T key)
            => backward.TryGetValue(value, out key);

        IEnumerator IEnumerable.GetEnumerator() {
            return ((IEnumerable)forward).GetEnumerator();
        }
    }
}
