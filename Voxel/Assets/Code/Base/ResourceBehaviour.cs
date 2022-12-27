using Atrufulgium.Voxel.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Atrufulgium.Voxel.Base {
    /// <summary>
    /// A will-probably-be-monolithic class that handles loading a bunch of
    /// stuff in the Resources folder and turning it into a form other code
    /// needs it in.
    /// </summary>
    internal class ResourceBehaviour : MonoBehaviour {

        public BiDictionary<string, ushort> textures;

        private void Awake() {
            List<Texture2D> textures = new();
            List<string> textureNames = new();
            foreach (var i in Resources.LoadAll<Texture2D>("Textures/Blocks")) {
                textures.Add(i);
                textureNames.Add(i.name);
            }
            Texture2D first = textures[0];
            // +1s because "air" has ID 0.
            Texture2DArray arr = new(first.width, first.height, textures.Count+1, first.format, false) {
                wrapMode = TextureWrapMode.Repeat,
                filterMode = FilterMode.Point
            };
            for (int i = 0; i < textures.Count; i++) {
                Graphics.CopyTexture(textures[i], 0, arr, i + 1);
            }
            arr.Apply();
            Shader.SetGlobalTexture("_VoxelTex", arr);
        }
    }
}
