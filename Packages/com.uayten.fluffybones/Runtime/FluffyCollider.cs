using UnityEngine;

namespace Fluffy
{
    /// <summary>One of an object's own three axes.</summary>
    public enum FluffyAxis
    {
        X,
        Y,
        Z
    }

    /// <summary>The shapes a chain can be pushed out of.</summary>
    public enum FluffyColliderShape
    {
        /// <summary>A ball. A head, a shoulder, a hip.</summary>
        Sphere,

        /// <summary>A ball swept along a line. A thigh, an upper arm, a torso.</summary>
        Capsule
    }

    /// <summary>
    /// A collision shape that Fluffy Bones chains are pushed out of.
    /// </summary>
    /// <remarks>
    /// Independent of Unity's physics colliders: the solver reads these directly, so no
    /// rigidbodies, no physics layers and no physics scene are involved. That is the
    /// point of the plugin — a skirt that stays off the legs should not require the
    /// character to be a physics object.
    ///
    /// Put one on the bone it belongs to and it travels with the animation: a capsule on
    /// the thigh bone is a thigh for as long as the character has one, at no cost per
    /// frame beyond reading the transform.
    /// </remarks>
    [AddComponentMenu("Fluffy Bones/Fluffy Collider")]
    public class FluffyCollider : MonoBehaviour
    {
        private const float MinRadius = 1e-4f;

        [Tooltip("Sphere for a head or a shoulder, capsule for a limb or a torso.")]
        [SerializeField] private FluffyColliderShape _shape = FluffyColliderShape.Capsule;

        [Tooltip("Where the shape sits, relative to the object it is on.")]
        [SerializeField] private Vector3 _centre = Vector3.zero;

        [Tooltip("How thick the shape is, in the object's own units before scaling.")]
        [Min(0f)]
        [SerializeField] private float _radius = 0.1f;

        [Tooltip("End to end length of a capsule, caps included, the way Unity's own " +
                 "capsule measures. Shorter than two radii is a sphere.")]
        [Min(0f)]
        [SerializeField] private float _height = 0.4f;

        [Tooltip("Which of the object's own axes the capsule runs along.")]
        [SerializeField] private FluffyAxis _direction = FluffyAxis.Y;

        [Tooltip("Draw the shape in the scene. Worth turning off once a character has a " +
                 "dozen of them and the view is more collider than character.")]
        [SerializeField] private bool _draw = true;

        [Tooltip("Colour of the drawn shape.")]
        [SerializeField] private Color _colour = new Color(0.36f, 0.62f, 1f, 0.9f);

        /// <summary>Sphere or capsule.</summary>
        public FluffyColliderShape Shape
        {
            get => _shape;
            set => _shape = value;
        }

        /// <summary>Where the shape sits, in the object's own space.</summary>
        public Vector3 Centre
        {
            get => _centre;
            set => _centre = value;
        }

        /// <summary>How thick the shape is, before the object's scale.</summary>
        public float Radius
        {
            get => _radius;
            set => _radius = Mathf.Max(0f, value);
        }

        /// <summary>End to end length of a capsule, caps included.</summary>
        public float Height
        {
            get => _height;
            set => _height = Mathf.Max(0f, value);
        }

        /// <summary>Which axis a capsule runs along.</summary>
        public FluffyAxis Direction
        {
            get => _direction;
            set => _direction = value;
        }

        /// <summary>
        /// The shape's radius in world units, taking the object's scale into account.
        /// </summary>
        /// <remarks>
        /// The largest of the three scale components rather than an average: a shape
        /// scaled unevenly is not a sphere any more, and of the two ways to be wrong,
        /// slightly too fat keeps the skirt off the leg while slightly too thin lets it
        /// through, which is the failure anyone would report.
        /// </remarks>
        public float WorldRadius
        {
            get
            {
                Vector3 scale = transform.lossyScale;

                return _radius * Mathf.Max(
                    Mathf.Abs(scale.x), Mathf.Max(Mathf.Abs(scale.y), Mathf.Abs(scale.z)));
            }
        }

        /// <summary>Where the shape's centre is in the world.</summary>
        public Vector3 WorldCentre => transform.TransformPoint(_centre);

        /// <summary>
        /// The line a capsule's centre sweeps along, from one cap's centre to the other.
        /// Both ends are the same point for a sphere, and for a capsule too short to have
        /// a middle.
        /// </summary>
        public void WorldSegment(out Vector3 from, out Vector3 to)
        {
            Vector3 centre = WorldCentre;

            if (_shape == FluffyColliderShape.Sphere)
            {
                from = centre;
                to = centre;
                return;
            }

            float half = Mathf.Max(0f, _height * 0.5f - _radius);

            if (half <= MinRadius)
            {
                from = centre;
                to = centre;
                return;
            }

            // Scaled along the axis it runs on: a capsule on a bone stretched by its
            // animation should stretch with it.
            Vector3 axis = transform.TransformDirection(AxisVector(_direction));
            Vector3 reach = axis.normalized * (half * AxisScale(_direction));

            from = centre - reach;
            to = centre + reach;
        }

        /// <summary>
        /// Moves a point out of the shape, along the shortest way out.
        /// </summary>
        /// <param name="point">The point to push. Left alone when it is already outside.</param>
        /// <param name="thickness">
        /// How far from the surface the point wants to stay — the chain's own radius,
        /// since a skirt strand is a rope rather than a line.
        /// </param>
        /// <returns>True when the point was moved.</returns>
        /// <remarks>
        /// A point exactly at the centre has no shortest way out, and the direction
        /// picked for it has to be something rather than a NaN. The capsule's axis is the
        /// least surprising answer for a limb: a bone that has ended up inside a thigh
        /// leaves along the leg rather than sideways through it.
        /// </remarks>
        public bool PushOut(ref Vector3 point, float thickness = 0f)
        {
            float radius = WorldRadius + Mathf.Max(0f, thickness);

            if (radius <= MinRadius)
            {
                return false;
            }

            WorldSegment(out Vector3 from, out Vector3 to);
            Vector3 nearest = NearestOnSegment(from, to, point);

            Vector3 away = point - nearest;
            float distance = away.magnitude;

            if (distance >= radius)
            {
                return false;
            }

            if (distance <= MinRadius)
            {
                Vector3 escape = from == to
                    ? transform.TransformDirection(AxisVector(_direction))
                    : (to - from);

                away = escape.sqrMagnitude > MinRadius ? escape.normalized : Vector3.up;
            }
            else
            {
                away /= distance;
            }

            point = nearest + away * radius;
            return true;
        }

        /// <summary>The point on a segment closest to <paramref name="point"/>.</summary>
        private static Vector3 NearestOnSegment(Vector3 from, Vector3 to, Vector3 point)
        {
            Vector3 along = to - from;
            float lengthSquared = along.sqrMagnitude;

            if (lengthSquared <= MinRadius)
            {
                return from;
            }

            float t = Mathf.Clamp01(Vector3.Dot(point - from, along) / lengthSquared);

            return from + along * t;
        }

        private static Vector3 AxisVector(FluffyAxis axis)
        {
            switch (axis)
            {
                case FluffyAxis.X: return Vector3.right;
                case FluffyAxis.Z: return Vector3.forward;
                default: return Vector3.up;
            }
        }

        private float AxisScale(FluffyAxis axis)
        {
            Vector3 scale = transform.lossyScale;

            switch (axis)
            {
                case FluffyAxis.X: return Mathf.Abs(scale.x);
                case FluffyAxis.Z: return Mathf.Abs(scale.z);
                default: return Mathf.Abs(scale.y);
            }
        }

        private void OnValidate()
        {
            _height = Mathf.Max(_height, _radius * 2f);
        }

        private void OnDrawGizmos()
        {
            if (!_draw)
            {
                return;
            }

            Gizmos.color = _colour;
            WorldSegment(out Vector3 from, out Vector3 to);
            float radius = WorldRadius;

            Gizmos.DrawWireSphere(from, radius);

            if (from == to)
            {
                return;
            }

            Gizmos.DrawWireSphere(to, radius);

            // Four rails along the capsule's side, squared up against the axis so the
            // shape reads as a tube rather than as two loose balls.
            Vector3 along = (to - from).normalized;
            Vector3 side = Vector3.Cross(along, Vector3.up);

            if (side.sqrMagnitude < MinRadius)
            {
                side = Vector3.Cross(along, Vector3.forward);
            }

            side = side.normalized * radius;
            Vector3 other = Vector3.Cross(along, side.normalized) * radius;

            Gizmos.DrawLine(from + side, to + side);
            Gizmos.DrawLine(from - side, to - side);
            Gizmos.DrawLine(from + other, to + other);
            Gizmos.DrawLine(from - other, to - other);
        }

        // TODO: an inside-out mode, for a chain that has to stay within a volume.
    }
}
