using System.Collections.Generic;
using UnityEngine;

namespace Atrufulgium.Voxel.Base {
    // This class assumes the layers themselves don't change.
    // This is a valid assumption in builds, but not in-editor.
    // Also this class assumes that
    /// <see cref="LayerMask.NameToLayer(string)"/>
    // does not cache anything.
    /// <summary>
    /// Containing some useful methods and constants for working with layer
    /// masks more easily.
    /// </summary>
    public static partial class LayerMaskHelper {

        readonly static Dictionary<string, int> maskNames = new(32);

        /// <summary>
        /// Returns an existing mask with one or multiple specified other masks
        /// added to it. These can either be (a) string(s) or other masks.
        /// </summary>
        /// <param name="layerName"> The name of the mask to add. </param>
        /// <param name="other"> The other mask to add. </param>
        /// <remarks>
        /// Using named masks is kinda unsafe. It is recommended to use the
        /// constants in <see cref="LayerMaskHelper"/>.
        /// </remarks>
        public static LayerMask WithLayer(this LayerMask mask, string layerName) {
            if (!maskNames.TryGetValue(layerName, out int val)) {
                val = LayerMask.NameToLayer(layerName);
                maskNames.Add(layerName, val);
            }
            return mask | (1 << val);
        }

        /// <inheritdoc cref="WithLayer(LayerMask, string)"/>
        public static LayerMask WithLayer(this LayerMask mask, params string[] layerName) {
            foreach (var l in layerName)
                mask = mask.WithLayer(l);
            return mask;
        }

        /// <inheritdoc cref="WithLayer(LayerMask, string)"/>
        public static LayerMask WithLayer(this LayerMask mask, LayerMask other)
            => mask | other;

        /// <summary>
        /// Returns an existing mask with one or multiple specified other masks
        /// removed. These can either be (a) string(s) or other masks. It is
        /// not checked whether the masks were enabled in the first place.
        /// </summary>
        /// <param name="layerName"> The name of the mask to remove. </param>
        /// <param name="other"> The other mask to remove. </param>
        /// <remarks>
        /// Using named masks is kinda unsafe. It is recommended to use the
        /// constants in <see cref="LayerMaskHelper"/>.
        /// </remarks>
        public static LayerMask WithoutLayer(this LayerMask mask, string layerName) {
            if (!maskNames.TryGetValue(layerName, out int val)) {
                val = LayerMask.NameToLayer(layerName);
                maskNames.Add(layerName, val);
            }
            return mask & ~(1 << val);
        }

        /// <inheritdoc cref="WithoutLayer(LayerMask, string)"/>
        public static LayerMask WithoutLayer(this LayerMask mask, params string[] layerName) {
            foreach (var l in layerName)
                mask = mask.WithoutLayer(l);
            return mask;
        }

        /// <inheritdoc cref="WithoutLayer(LayerMask, string)"/>
        public static LayerMask WithoutLayer(this LayerMask mask, LayerMask other)
            => mask & ~other;

        /// <summary>
        /// Whether a layer mask contains a every mask in <paramref name="other"/>.
        /// </summary>
        public static bool HasMask(this LayerMask mask, LayerMask other)
            => mask.WithLayer(other) == mask;
    }
}
