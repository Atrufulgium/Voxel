using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Atrufulgium.Voxel.LSystem {
    public class LSystemTestBehaviour : MonoBehaviour {
        public string axiom;
        [TextArea]
        public string rules;
        public int iterations;
        public int seed;
        public Dictionary<char, float> parameters;
        public float defaultRot = 30;
        public float defaultMove = 1;
        public bool logDebugs = false;

        public bool generate = false;

        LSystemResult result;

        private void OnValidate() {
            if (generate) {
                generate = false;
                Generate();
            }
        }

        private void OnDrawGizmos() {
            if (result == null)
                return;

            Gizmos.color = new Color(0.46f, 0.30f, 0.1f);
            foreach (var line in result.LineSegments) {
                Gizmos.DrawLine(line.Item1, line.Item2);
            }
            Gizmos.color = Color.green;
            foreach (var leaf in result.Leaves) {
                Gizmos.DrawSphere(leaf, 0.1f);
            }
        }

        void Generate() {
            if (axiom.Length == 0) {
                Debug.LogWarning("Empty axiom -- did not generate.");
                return;
            }
            if (rules.Length == 0) {
                Debug.LogWarning("No rules -- did not generate.");
                return;
            }
            var splitRules = rules.Replace("\r\n", "\n").Replace("\n\r", "\n").Split('\n').ToList();
            LSystemGenerator gen = new(axiom.ToCharArray(), splitRules, defaultRot: defaultRot, defaultMove: defaultMove, logDebugs: logDebugs);
            result = gen.Generate(iterations, seed, parameters);
        }
    }
}
