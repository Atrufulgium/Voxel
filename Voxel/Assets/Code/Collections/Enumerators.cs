using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;

namespace Atrufulgium.Voxel.Collections {
    public static class Enumerators {

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

        /// <summary>
        /// <para>
        /// Enumerates a diamond grid. Starting out from the center, it returns
        /// in order the following coordinates:
        /// <code>
        /// X→Y↑   7 ..
        ///     8  2  6 ..
        ///  9  3  0  1  5 13
        ///    10  4 12
        ///       11
        /// </code>
        /// In words, every diamond circle starts at (x,0) and goes counter-clockwise.
        /// </para>
        /// <para>
        /// This enumeration goes on forever. Once you have handled a
        /// sufficient number of layers (detectable by whether x = k for some
        /// k for the first time), or a sufficient number of blocks (detectable
        /// by just keeping track), break from the loop.
        /// </para>
        /// </summary>
        public static IEnumerable<int2> EnumerateDiamondInfinite2D(int2 center = default) {
            int2 current = 0;
            yield return center;
            while (true) {
                // The initial outset of a ring.
                current.x++;
                yield return center + current;
                // Now at (k,0). Move ↖ until x = 0.
                while (current.x > 0) {
                    current += new int2(-1, 1);
                    yield return center + current;
                }
                // Now at (0,k). Move ↙ until y= 0.
                while (current.y > 0) {
                    current += new int2(-1, -1);
                    yield return center + current;
                }
                // Now at (-k, 0). Move ↘ until x = 0.
                while (current.x < 0) {
                    current += new int2(1, -1);
                    yield return center + current;
                }
                // Now at (0, -k). Move ↗ until y = -1 to complete the ring.
                while (current.y < -1) {
                    current += new int2(1, 1);
                    yield return center + current;
                }
                // Don't return this last one
                current += new int2(1, 1);
            }
        }

        /// <summary>
        /// <para>
        /// The 3d equivalent of <see cref="EnumerateDiamondInfinite2D(int2)"/>.
        /// Each layer is done in slices, ordered from +X to -X. Each
        /// individual slice is done by the 2d version.
        /// </para>
        /// <para>
        /// As such, checking whether you have finished k layers can still be
        /// done by checking when the x component reaches k+1.
        /// </para>
        /// </summary>
        public static IEnumerable<int3> EnumerateDiamondInfinite3D(int3 center = default) {
            yield return center;
            int layerIndex = 1;
            while (true) {
                // The code below does not work for the single-block case.
                yield return center + new int3(layerIndex, 0, 0);
                for (int x = layerIndex - 1; x >= -layerIndex + 1; x--) {
                    int max = layerIndex - math.abs(x);
                    int2 current2D = new(max, 0);
                    yield return center + new int3(x, current2D);
                    // Now at (k,0). Move ↖ until x = 0.
                    while (current2D.x > 0) {
                        current2D += new int2(-1, 1);
                        yield return center + new int3(x, current2D);
                    }
                    // Now at (0,k). Move ↙ until y= 0.
                    while (current2D.y > 0) {
                        current2D += new int2(-1, -1);
                        yield return center + new int3(x, current2D);
                    }
                    // Now at (-k, 0). Move ↘ until x = 0.
                    while (current2D.x < 0) {
                        current2D += new int2(1, -1);
                        yield return center + new int3(x, current2D);
                    }
                    // Now at (0, -k). Move ↗ until y = -1 to complete the ring.
                    while (current2D.y < -1) {
                        current2D += new int2(1, 1);
                        yield return center + new int3(x, current2D);
                    }
                }
                yield return center + new int3(-layerIndex, 0, 0);
                layerIndex++;
            }
        }
    }
}
