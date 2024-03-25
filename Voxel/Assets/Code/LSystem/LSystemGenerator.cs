using Atrufulgium.Voxel.Collections;
using System;
using System.Collections.Generic;
using System.Text;
using Unity.Mathematics;
using UnityEngine;

namespace Atrufulgium.Voxel.LSystem {
    /// <summary>
    /// <para>
    /// <i>L-systems</i> are a set of rules for drawing figures with a turtle
    /// whose movement is specified by simple iterated rules. This class is a
    /// generator that can be used to specify an L-system that outputs line
    /// segment and single points.
    /// </para>
    /// <para>
    /// See <a href="https://en.wikipedia.org/wiki/L-system">Wikipedia</a> for more info.
    /// </para>
    /// </summary>
    /// <remarks>
    /// As we're working in 3D, our grammar is slightly different from usual:
    /// <list type="table">
    /// <item><term>+</term><description> increases yaw (turns right);</description></item>
    /// <item><term>-</term><description> decreases yaw (turns left);</description></item>
    /// <item><term>^</term><description> increases pitch (turns up);</description></item>
    /// <item><term>v</term><description> decreases pitch (turns down);</description></item>
    /// <item><term>&lt;</term><description> increases roll;</description></item>
    /// <item><term>&gt;</term><description> increases roll;</description></item>
    /// <item><term>M</term><description> moves a step and stores that line segment;</description></item>
    /// <item><term>N</term><description> moves a step.</description></item>
    /// </list>
    /// All of the above accept an optional argument <c>(a)</c> which fills in
    /// a pre-specified value of whatever <c>a</c> was assigned. Otherwise it
    /// returns to the default value. The rotation actions are in local space,
    /// and in degrees.
    /// <br />
    /// Other than the above operators, there is also:
    /// <list type="table">
    /// <item><term>[</term><description> saves turtle state to a stack;</description></item>
    /// <item><term>]</term><description> loads turtle state from a stack;</description></item>
    /// <item><term>L</term><description> stores a leaf position;</description></item>
    /// <item><term>A</term><description> is a placeholder, same for any other unused capital letter.</description></item>
    /// </list>
    /// Finally, replacement rules must be provided on separate lines. There
    /// are two possibilities:
    /// <list type="bullet">
    /// <item>Lines <c>A = A[A]</c> are simple replacement rules.</item>
    /// <item>
    /// Lines <c>A = 20 A[A], 80 F</c> are non-deterministic rule
    /// applications. The integer in front of each rule specifies its weight.
    /// </item>
    /// <item>Lines <c># Whatever you write here</c> are comments and ignored.</item>
    /// </list>
    /// </remarks>
    public class LSystemGenerator {

        readonly char[] axiom;
        char[] buffer;
        char[] doubleBuffer;
        int filledBufferLength;
        // A 26-char long array where capital letters map onto their rules.
        readonly IRule[] rules;
        // A 26-char long array where lower case letters map onto their values.
        // Note that index 'v' is allowed as it's not ambiguous with the 'v'
        // command.
        readonly float[] values;
        readonly float defaultRot;
        readonly float defaultMove;
        readonly bool logDebugs;

        public LSystemGenerator(
            char[] axiom,
            List<string> rules,
            int bufferSize = 1<<16,
            float defaultRot = 30,
            float defaultMove = 1,
            bool logDebugs = false
        ) {
            if (axiom.Length == 0)
                throw new ArgumentException("The axiom may not be empty.");

            buffer = new char[bufferSize];
            doubleBuffer = new char[bufferSize];
            this.axiom = axiom;
            values = new float[26];
            this.defaultRot = defaultRot;
            this.defaultMove = defaultMove;
            this.logDebugs = logDebugs;

            this.rules = new IRule[26];
            foreach (var rule in rules) {
                var (key, irule) = ParseRule(rule);
                if (this.rules[key - 'A'] != null)
                    throw new ArgumentException($"Multiple rules for key {key}, this is not allowed.");
                this.rules[key - 'A'] = irule;
            }
            // Default map for unset rules
            for (char c = 'A'; c <= 'Z'; c++) {
                if (this.rules[c - 'A'] == null) {
                    this.rules[c - 'A'] = new Rule(c.ToString());
                }
            }
        }

        (char key, IRule rule) ParseRule(string code) {
            char key;
            int i = 0;
            char c;

            // Identifier
            for (; true; i++) {
                c = code[i];
                if (char.IsWhiteSpace(c))
                    continue;
                if (c >= 'A' && c <= 'Z') {
                    key = c;
                    i++;
                    break;
                }
                throw new ArgumentException($"Expected placeholder LHS 'A = ...', but got unexpected char '{c}' in\n    {code}\ninstead.");
            }
            // The equals sign for which we also allow a few others
            for (; true; i++) {
                c = code[i];
                if (char.IsWhiteSpace(c))
                    continue;
                if (c == '=' || c == '→' || c == '←') {
                    i++;
                    break;
                }
                throw new ArgumentException($"Expected '=', but got '{c}' instead in\n    {code}");
            }

            StringBuilder rule = new();

            // Now we branch: either we encounter a digit and are rng, or, well, not.
            while (char.IsWhiteSpace(code[i]))
                i++;

            c = code[i];
            if (c >= '0' && c <= '9') {
                SampledList<string> randomRules = new();
                // Rng branch: multiple rules, each starting with digits.
                while (i < code.Length) {
                    bool foundAnyDigit = false;
                    int weight = 0;
                    // Grab the number
                    for (; true; i++) {
                        c = code[i];
                        if (c >= '0' && c <= '9') {
                            weight *= 10;
                            weight += c - '0';
                            foundAnyDigit = true;
                        } else {
                            break;
                        }
                    }
                    if (!foundAnyDigit)
                        throw new ArgumentException($"Random rule without weight in\n    {code}");
                    // Now parse rules regularly
                    randomRules.Add(ParseReplacement(code, ref i, rule), weight);
                    while (i < code.Length && char.IsWhiteSpace(code[i]))
                        i++;
                }
                return (key, new RandomRule(randomRules));
            }
            return (key, new Rule(ParseReplacement(code, ref i, rule)));
        }

        // Ends with i after last parsed char.
        string ParseReplacement(string code, ref int i, StringBuilder sb) {
            sb.Clear();
            // We expect a rule here.
            // That is basically "any valid character up until the end or ,".
            // Only specific validation we need to do is `(a)`-likes, and
            // ensure [] makes sense.
            int bracketCounter = 0;
            for (; i < code.Length; i++) {
                char c = code[i];
                if (char.IsWhiteSpace(c))
                    continue;
                if (c != ',') {
                    sb.Append(c);
                } else {
                    i++;
                    break;
                }

                if ((c >= 'A' && c <= 'Z') || c == '+' || c == '-' || c == '^' || c == 'v' || c == '<' || c == '>')
                    continue;
                if (c == '[') {
                    bracketCounter++;
                    continue;
                }
                if (c == ']') {
                    bracketCounter--;
                    if (bracketCounter < 0)
                        throw new ArgumentException($"Unbalanced brackets [] in replacement in\n    {code}");
                    continue;
                }
                if (c == '(') {
                    if (i >= code.Length - 2)
                        throw new ArgumentException($"Unclosed '(' near the end in replacement in\n    {code}");
                    c = code[i+1];
                    if (c < 'a' || c > 'z')
                        throw new ArgumentException($"Parameters in ()s may only be lower case letters in\n    {code}");
                    sb.Append("(" + c + ")");
                    c = code[i + 2];
                    if (c != ')')
                        throw new ArgumentException($"Unclosed '(' in \n    {code}");
                    i += 2;
                    continue;
                }
                throw new ArgumentException($"Unexpected character {c} in\n    {code}");
            }
            if (bracketCounter != 0)
                throw new ArgumentException($"Unbalanced brackets [] in replacement in\n    {code}");
            return sb.ToString();
        }

        public LSystemResult Generate(
            int iterations,
            int seed = 0,
            Dictionary<char, float> parameters = null,
            Quaternion initialRotation = default,
            float3 initialPosition = default
        ) {
            System.Random rng;
            if (seed == 0)
                rng = new();
            else
                rng = new(seed);

            FillBuffer(iterations, rng, parameters);
            return ParseBuffer(new TurtleState(initialRotation, initialPosition));
        }

        void FillBuffer(int iterations, System.Random rng, Dictionary<char, float> parameters) {
            // Set parameters: use infinity to message reading defaults.
            for (int i = 0; i < values.Length; i++)
                values[i] = float.PositiveInfinity;
            if (parameters != null)
                foreach (var kv in parameters)
                    values[kv.Key - 'a'] = kv.Value;

            // Initialize axiom
            for (int i = 0; i < axiom.Length; i++)
                buffer[i] = axiom[i];
            filledBufferLength = axiom.Length;

            int bufferIndex; // always points to current slot
            int doubleBufferIndex; // always points to first empty slot
            for (int i = 0; i < iterations; i++) {
                // Single iteration: walk over all chars in the buffer, and
                // replace select capitals with their value.
                // The rest is copied as-is.
                for (bufferIndex = 0, doubleBufferIndex = 0; bufferIndex < filledBufferLength; bufferIndex++) {
                    char c = buffer[bufferIndex];
                    if (c >= 'A' && c <= 'Z') {
                        // interesting
                        foreach (char c2 in rules[c-'A'].SampleResult(rng)) {
                            doubleBuffer[doubleBufferIndex] = c2;
                            doubleBufferIndex++;
                        }
                    } else {
                        // not interesting
                        doubleBuffer[doubleBufferIndex] = c;
                        doubleBufferIndex++;
                    }
                }

                filledBufferLength = doubleBufferIndex;
                (buffer, doubleBuffer) = (doubleBuffer, buffer);

                if (logDebugs) {
                    StringBuilder sb = new(filledBufferLength);
                    for (int ii = 0; ii < filledBufferLength; ii++)
                        sb.Append(buffer[ii]);
                    Debug.Log($"After iteration {i}: {sb}");
                }
            }
        }

        readonly Stack<TurtleState> turtlesAllTheWayDown = new();

        LSystemResult ParseBuffer(TurtleState initialState) {
            if (!initialState.IsValid) {
                initialState = new(Quaternion.identity, 0);
            }

            var turt = initialState;
            List<(float3, float3)> lines = new();
            List<float3> leaves = new();
            Bounds bounds = new();
            
            for (int i = 0; i < filledBufferLength; i++) {
                char c = buffer[i];

                float arg = defaultRot;
                if (c == 'M' || c == 'N')
                    arg = defaultMove;
                bool incrementThree = false;
                if (TryParseArgument(i + 1, out float filledArg) && filledArg != float.PositiveInfinity) {
                    arg = filledArg;
                    incrementThree = true;
                }

                switch (c) {
                    case '+': turt = turt.AfterRotateYaw(arg); break;
                    case '-': turt = turt.AfterRotateYaw(-arg); break;
                    case '^': turt = turt.AfterRotatePitch(arg); break;
                    case 'v': turt = turt.AfterRotatePitch(-arg); break;
                    case '<': turt = turt.AfterRotateRoll(-arg); break;
                    case '>': turt = turt.AfterRotateRoll(arg); break;
                    case 'M':
                        float3 a = turt.position;
                        turt = turt.AfterMove(turt.rotation * Vector3.forward * arg);
                        float3 b = turt.position;
                        lines.Add((a, b));
                        bounds.Encapsulate(a);
                        bounds.Encapsulate(b);
                        break;
                    case 'N': turt = turt.AfterMove(turt.rotation * Vector3.forward * arg); break;
                    case '[': turtlesAllTheWayDown.Push(turt); break;
                    case ']': turt = turtlesAllTheWayDown.Pop(); break;
                    case 'L':
                        leaves.Add(turt.position);
                        bounds.Encapsulate(turt.position);
                        break;
                    default: break;
                }


                if (incrementThree)
                    i += 3;
            }
            return new(lines, leaves, bounds);
        }

        /// <summary>
        /// At the current point in the buffer, if we're in a (a) construction,
        /// outputs either the parameter value or default and returns true.
        /// Otherwise, returns false and puts +∞ into the value.
        /// </summary>
        bool TryParseArgument(int index, out float value) {
            value = float.PositiveInfinity;
            if (buffer[index] != '(')
                return false;
            value = values[buffer[index + 1] - 'a'];
            return true;
        }

        readonly struct TurtleState {
            public readonly Quaternion rotation;
            public readonly float3 position;

            /// <summary>
            /// Whether this was properly constructed and usable, or most
            /// likely constructed via <c>default</c> and invalid.
            /// </summary>
            // Checking whether it equals the default quaternion doesn't work somewhy?
            public bool IsValid => rotation.x != 0 || rotation.y != 0 || rotation.z != 0 || rotation.w != 0;

            public TurtleState(Quaternion rotation, float3 position) {
                this.rotation = rotation;
                this.position = position;
            }

            public TurtleState AfterMove(float3 move)
                => new(rotation, position + move);

            public TurtleState AfterRotatePitch(float degrees)
                => new(rotation * Quaternion.Euler(degrees, 0, 0), position);
            public TurtleState AfterRotateYaw(float degrees)
                => new(rotation * Quaternion.Euler(0, degrees, 0), position);
            public TurtleState AfterRotateRoll(float degrees)
                => new(rotation * Quaternion.Euler(0, 0, degrees), position);
        }

        interface IRule {
            public string SampleResult(System.Random rng);
        }

        record Rule(string Value) : IRule {
            public string SampleResult(System.Random rng) => Value;
        }

        record RandomRule(SampledList<string> Rules) : IRule {
            public string SampleResult(System.Random rng) => Rules.Sample(rng);
        }
    }
}
