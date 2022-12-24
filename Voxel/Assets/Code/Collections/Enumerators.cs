using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;

namespace Atrufulgium.Voxel.Collections {
    internal static class Enumerators {

        /// <summary>
        /// <inheritdoc cref="EnumerateVolume(int3)"/>
        /// <para> There is also a stepsize for each axis. </para>
        /// </summary>
        public static IEnumerable<int3> EnumerateVolume(int3 max, int3 step) {
            for (int z = 0; z < max.z; z += step.z)
                for (int y = 0; y < max.y; y += step.y)
                    for (int x = 0; x < max.x; x += step.x)
                        yield return new int3(x, y, z);
        }

        /// <summary>
        /// Enumerates over the 3d volume spanning (0,0,0) (inclusive) up to
        /// this int3 (exclusive).
        /// </summary>
        public static IEnumerable<int3> EnumerateVolume(int3 max)
            => EnumerateVolume(max, new int3(1, 1, 1));

        /// <summary>
        /// Iterates the corners of the rectangle in clockwise order.
        /// </summary>
        public static IEnumerable<int2> EnumerateCornersClockwise(RectInt rect) {
            yield return new(rect.xMin, rect.yMin);
            yield return new(rect.xMin, rect.yMax);
            yield return new(rect.xMax, rect.yMax);
            yield return new(rect.xMax, rect.yMin);
        }

        /// <summary>
        /// Iterates the corners of the rectangle in counterclockwise order.
        /// </summary>
        public static IEnumerable<int2> EnumerateCornersCounterclockwise(RectInt rect) {
            yield return new(rect.xMax, rect.yMin);
            yield return new(rect.xMax, rect.yMax);
            yield return new(rect.xMin, rect.yMax);
            yield return new(rect.xMin, rect.yMin);
        }

        /// <summary>
        /// Iterates over all elements in a copy of ts.
        /// In other words, ts is safe to modify.
        /// </summary>
        /// <remarks>
        /// This of course means the iteration is preceded by an O(n) operation.
        /// </remarks>
        public static IEnumerable<T> EnumerateCopy<T>(IEnumerable<T> ts) {
            // Copying to a list is nice for another reason: the constructor
            // takes into account IEnumerable<> vs ICollection<> which may save
            // some allocation from list-resizing.
            List<T> list = new(ts);
            foreach (T t in list)
                yield return t;
        }

        /// <summary>
        /// Iterates over all pairs of elements (T1,T2).
        /// </summary>
        public static IEnumerable<(T1, T2)> EnumerateTuple<T1, T2>(IEnumerable<T1> t1s, IEnumerable<T2> t2s) {
            foreach (T1 t1 in t1s)
                foreach (T2 t2 in t2s)
                    yield return (t1, t2);
        }

        /// <inheritdoc cref="EnumerateTuple{T1, T2}(IEnumerable{T1}, IEnumerable{T2})"/>
        public static IEnumerable<(T1, T2)> EnumerateTuple<T1, T2>((IEnumerable<T1>, IEnumerable<T2>) ts)
            => EnumerateTuple(ts.Item1, ts.Item2);

        /// <inheritdoc cref="EnumerateTuple{T1, T2}(IEnumerable{T1}, IEnumerable{T2})"/>
        public static IEnumerator<(T1, T2)> GetEnumerator<T1, T2>(this (IEnumerable<T1>, IEnumerable<T2>) ts) {
            foreach (T1 t1 in ts.Item1)
                foreach (T2 t2 in ts.Item2)
                    yield return (t1, t2);
        }

        /// <summary>
        /// Iterates over all triples in (T1,T2,T3).
        /// </summary>
        public static IEnumerable<(T1, T2, T3)> EnumerateTuple<T1, T2, T3>(IEnumerable<T1> t1s, IEnumerable<T2> t2s, IEnumerable<T3> t3s) {
            foreach (T1 t1 in t1s)
                foreach (T2 t2 in t2s)
                    foreach (T3 t3 in t3s)
                        yield return (t1, t2, t3);
        }

        /// <inheritdoc cref="EnumerateTuple{T1, T2, T3}(IEnumerable{T1}, IEnumerable{T2}, IEnumerable{T3})"/>
        public static IEnumerable<(T1, T2, T3)> EnumerateTuple<T1, T2, T3>((IEnumerable<T1>, IEnumerable<T2>, IEnumerable<T3>) ts)
            => EnumerateTuple(ts.Item1, ts.Item2, ts.Item3);

        /// <inheritdoc cref="EnumerateTuple{T1, T2, T3}(IEnumerable{T1}, IEnumerable{T2}, IEnumerable{T3})"/>
        public static IEnumerator<(T1, T2, T3)> GetEnumerator<T1, T2, T3>(this (IEnumerable<T1>, IEnumerable<T2>, IEnumerable<T3>) ts) {
            foreach (T1 t1 in ts.Item1)
                foreach (T2 t2 in ts.Item2)
                    foreach (T3 t3 in ts.Item3)
                        yield return (t1, t2, t3);
        }

        /// <summary>
        /// Iterates over all combinations in (T1,T2,T3,T4).
        /// </summary>
        /// <remarks>
        /// Bless your soul if you actually need this.
        /// Otherwise, go rethink your setup.
        /// </remarks>
        public static IEnumerable<(T1, T2, T3, T4)> EnumerateTuple<T1, T2, T3, T4>(IEnumerable<T1> t1s, IEnumerable<T2> t2s, IEnumerable<T3> t3s, IEnumerable<T4> t4s) {
            foreach (T1 t1 in t1s)
                foreach (T2 t2 in t2s)
                    foreach (T3 t3 in t3s)
                        foreach (T4 t4 in t4s)
                            yield return (t1, t2, t3, t4);
        }

        /// <inheritdoc cref="EnumerateTuple{T1, T2, T3, T4}(IEnumerable{T1}, IEnumerable{T2}, IEnumerable{T3}, IEnumerable{T4})"/>
        public static IEnumerable<(T1, T2, T3, T4)> EnumerateTuple<T1, T2, T3, T4>((IEnumerable<T1>, IEnumerable<T2>, IEnumerable<T3>, IEnumerable<T4>) ts)
            => EnumerateTuple(ts.Item1, ts.Item2, ts.Item3, ts.Item4);

        /// <inheritdoc cref="EnumerateTuple{T1, T2, T3, T4}(IEnumerable{T1}, IEnumerable{T2}, IEnumerable{T3}, IEnumerable{T4})"/>
        public static IEnumerator<(T1, T2, T3, T4)> GetEnumerator<T1, T2, T3, T4>(this (IEnumerable<T1>, IEnumerable<T2>, IEnumerable<T3>, IEnumerable<T4>) ts) {
            foreach (T1 t1 in ts.Item1)
                foreach (T2 t2 in ts.Item2)
                    foreach (T3 t3 in ts.Item3)
                        foreach (T4 t4 in ts.Item4)
                            yield return (t1, t2, t3, t4);
        }
    }
}
