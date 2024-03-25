using System.Collections.Generic;
using System.Collections.ObjectModel;
using Unity.Mathematics;
using UnityEngine;

namespace Atrufulgium.Voxel.LSystem {
    /// <summary>
    /// <para>
    /// The result of using <see cref="LSystemGenerator"/>. These contain the
    /// resulting lines and points of the L-system.
    /// </para>
    /// <para>
    /// You can either choose to enumerate <see cref="LineSegments"/> and
    /// <see cref="Leaves"/> directly, or use TODO to apply a transform.
    /// </para>
    /// </summary>
    public class LSystemResult {
        
        public ReadOnlyCollection<(float3, float3)> LineSegments { get; private set; }
        public ReadOnlyCollection<float3> Leaves { get; private set; }
        public readonly Bounds bounds;

        /// <summary>
        /// Copies generated results into an LSystemResults class.
        /// The ReadOnlyCollections in this class are views, and not copies.
        /// Nothing is validated.
        /// </summary>
        internal LSystemResult(IList<(float3, float3)> lineSegments, IList<float3> leaves, Bounds bounds) {
            LineSegments = new(lineSegments);
            Leaves = new(leaves);
            this.bounds = bounds;
        }
    }
}
