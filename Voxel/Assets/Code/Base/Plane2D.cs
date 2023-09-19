using Unity.Mathematics;

namespace Atrufulgium.Voxel.Base {

    /// <summary>
    /// Represents an infinite 2D plane: a line that splits ℝ^2 into a
    /// positive and negative halfspace.
    /// </summary>
    /// <remarks>
    /// Do not use the constructor. Instead, use any of the static
    /// <tt>FromXXX()</tt> methods.
    /// </remarks>
    public readonly struct Plane2D {
        // (Note that the base point is not unique.)
        readonly float2 basePos;
        readonly float2 normal;

        /// <summary>
        /// Whether this halfplane is correctly constructed. This can go wrong
        /// if either (1) the empty constructor is used, (2) the normal of
        /// <see cref="FromNormalUnsafe(float2, float2)"/> was not a normal, or
        /// (3) the normal of <see cref="FromNormal(float2, float2)"/> was 0.
        /// </summary>
        public bool IsValid => math.abs(math.lengthsq(normal) - 1) < 0.00001;

        private Plane2D(float2 basePos, float2 normal) {
            this.basePos = basePos;
            this.normal = normal;
        }

        /// <summary>
        /// Projects a points to lie on the plane.
        /// </summary>
        public float2 Project(float2 pos) {
            pos -= basePos;
            pos -= math.dot(pos, normal) * normal;
            pos += basePos;
            return pos;
        }

        /// <summary>
        /// Returns a copy of this plane with the positive and negative halves
        /// swapped around.
        /// </summary>
        public Plane2D Flipped()
            => new(basePos, -normal);

        /// <summary>
        /// Returns the signed distance between a point and the plane. A signed
        /// distance is positive in the positive halfspace and negative in
        /// the negative halfspace.
        /// </summary>
        public float DistanceSigned(float2 pos)
            => math.dot(pos - basePos, normal);

        /// <summary>
        /// Returns the distance between a point and the plane.
        /// </summary>
        public float Distance(float2 pos)
            => math.abs(DistanceSigned(pos));

        /// <summary>
        /// Whether the given position is on the (strictly) positive half of
        /// this plane.
        /// </summary>
        public bool InPositiveHalfspace(float2 pos)
            => DistanceSigned(pos) > 0;

        /// <inheritdoc cref="TryIntersect(Plane2D, Plane2D, out float2)"/>
        public bool TryIntersectWith(Plane2D other, out float2 pos)
            => TryIntersect(this, other, out pos);

        public bool ContainsPoint(float2 pos, float tolerance = 0.00001f)
            => Distance(pos) < tolerance;

        /// <summary>
        /// Creates a 2D plane through a point with a normal vector.
        /// The positive side of the plane is the one the normal faces.
        /// </summary>
        /// <remarks>
        /// <see cref="FromNormal(float2, float2)"/> normalizes the given
        /// <paramref name="normal"/>. When you're sure you're giving a valid
        /// normal, use <see cref="FromNormalUnsafe(float2, float2)"/>.
        /// </remarks>
        public static Plane2D FromNormal(float2 basePos, float2 normal)
            => new(basePos, math.normalizesafe(normal));

        /// <inheritdoc cref="FromNormal(float2, float2)"/>
        public static Plane2D FromNormalUnsafe(float2 pos, float2 normal)
            => new(pos, normal);

        /// <summary>
        /// Creates a 2D plane going through two points.
        /// The positive side of the plane is the one on your left when walking
        /// from <paramref name="posA"/> to <paramref name="posB"/>.
        /// </summary>
        public static Plane2D FromPoints(float2 posA, float2 posB)
            => FromNormal(posA, (posB - posA).yx * new float2(-1, 1));

        /// <summary>
        /// Creates a 2D plane going through two points.
        /// The positive side of the plane is the one containing <paramref name="facing"/>.
        /// </summary>
        public static Plane2D FromFacing(float2 posA, float2 posB, float2 facing) {
            var plane = FromPoints(posA, posB);
            if (plane.InPositiveHalfspace(facing))
                return plane;
            return plane.Flipped();
        }

        /// <summary>
        /// Creates a horizontal 2D plane. The positive side of the plane is
        /// the upper halfspace.
        /// </summary>
        public static Plane2D FromHorizontal(float posY)
            => new(new(0, posY), new(0, 1));

        /// <summary>
        /// Creates a vertical 2D plane. The positive side of the plane is the
        /// right halfspace.
        /// </summary>
        public static Plane2D FromVertical(float posX)
            => new(new(posX, 0), new(1, 0));

        /// <summary>
        /// Returns <tt>true</tt> if the planes intersect, and puts the
        /// resulting point in <paramref name="intersection"/>.
        /// Returns <tt>false</tt> if the planes do not intersect; then the
        /// contents of <paramref name="intersection"/> are unspecified.
        /// </summary>
        /// <remarks>
        /// This can go wrong if the two planes are parallel, or at least
        /// parallel enough for floats to not notice the difference.
        /// </remarks>
        public static bool TryIntersect(Plane2D plane1, Plane2D plane2, out float2 intersection) {
            // If the two planes are the same the code below breaks down.
            if (math.all(plane1.basePos == plane2.basePos & plane1.normal == plane2.normal)) {
                intersection = plane1.basePos;
                return true;
            }

            // With plane1 = tangent1・t1 + basePos1
            //      plane2 = tangent2・t2 + basePos2
            // over all t1, t2, setting them equal gives
            //      tangent1・t1 - tangent2・t2 = basePos2 - basePos1
            // which is just a 2x2 system.
            // (Note the sign of the tangent does not matter here.)
            // In particular, as we're in 2D and normal.xy = tangent.+y-x.
            float2 tangent1 = new(plane1.normal.y, -plane1.normal.x);
            float2 tangent2 = new(plane2.normal.y, -plane2.normal.x);
            float2x2 tangents = new(tangent1, tangent2);
            float2 diff = plane2.basePos - plane1.basePos;
            float2 t = math.mul(math.inverse(tangents), diff);
            intersection = plane1.basePos + tangent1 * t.x;
            // The entries are not finite if noninvertible.
            // (The inverse is just calculated with 1/det so infinities)
            return math.all(math.isfinite(intersection));
        }
    }
}