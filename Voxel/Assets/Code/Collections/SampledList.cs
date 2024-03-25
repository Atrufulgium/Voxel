using System;
using System.Collections.Generic;

namespace Atrufulgium.Voxel.Collections {
    /// <summary>
    /// A list from which you randomly sample by weight.
    /// </summary>
    public class SampledList<T> {
        readonly List<T> entries = new();
        readonly List<float> weights = new();
        float maxWeight = 0f;

        public void Add(T entry, float weight) {
            if (weight <= 0 || !float.IsFinite(weight)) {
                throw new ArgumentException($"Weights must be positive and finite, but got weight {weight} for entry {entry}.", nameof(weight));
            }
            entries.Add(entry);
            weights.Add(weight);
            maxWeight += weight;
        }

        /// <summary>
        /// Use a specified random instance to sample from this list.
        /// </summary>
        public T Sample(Random rng) {
            float roll = ((float)rng.NextDouble()) * maxWeight;
            for (int i = 0; i < weights.Count; i++) {
                // As soon as we roll (haha) over to negatives, we landed in
                // the interval of that specific item.
                roll -= weights[i];
                if (roll < 0)
                    return entries[i];
            }

            return default; // possible if empty or denegerate float arithmetic the user is practically begging for
        }
    }
}
