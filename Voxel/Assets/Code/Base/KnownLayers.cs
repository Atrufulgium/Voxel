using UnityEngine;

namespace Atrufulgium.Voxel.Base {
    // Please update this file whenever updating the Layers in Unity.
    public static partial class LayerMaskHelper {

        /// <summary>
        /// Represents no active layers.
        /// </summary>
        public static LayerMask None => 0;
        /// <summary>
        /// Represents all active layers.
        /// </summary>
        public static LayerMask All => int.MaxValue;

        // Unity built-ins
        public static LayerMask DefaultMask => None.WithLayer("Default");
        public static LayerMask TransparentFXMask => None.WithLayer("TransparentFX");
        public static LayerMask IgnoreRaycastMask => None.WithLayer("IgnoreRaycast");
        public static LayerMask WaterMask => None.WithLayer("Water");
        public static LayerMask UIMask => None.WithLayer("UI");

        // Custom
        public static LayerMask CulledChunk => None.WithLayer("CulledChunk");
    }
}
