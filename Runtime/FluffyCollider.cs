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
        Capsule,

        /// <summary>A brick. A chest, a bag, a plinth.</summary>
        Box,

        /// <summary>
        /// A half of the world, divided by a surface with no edges.
        /// </summary>
        /// <remarks>
        /// The one shape with no size: everything on the wrong side of it is pushed to
        /// the right side, however far away. It is the cheapest way to say "the hair
        /// never comes forward over the shoulders" or "the cape stays off the back" —
        /// one plane on the spine does what a row of boxes would do worse.
        /// </remarks>
        Plane
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
    /// frame beyond reading the transform. That is also what makes a skirt lift when the
    /// leg does, rather than the leg passing through it.
    /// </remarks>
    [AddComponentMenu("Fluffy Bones/Fluffy Collider")]
    public class FluffyCollider : MonoBehaviour
    {
        private const float Tiny = 1e-4f;

        [Tooltip("Sphere for a head or a shoulder, capsule for a limb, box for a chest " +
                 "or a bag, plane for a surface nothing may cross.")]
        [SerializeField] private FluffyColliderShape _shape = FluffyColliderShape.Capsule;

        [Tooltip("Where the shape sits, relative to the object it is on.")]
        [SerializeField] private Vector3 _centre = Vector3.zero;

        [Tooltip("How the shape is turned on the bone, as euler angles. A thigh capsule " +
                 "leaning with the muscle, a chest box squared to the ribs rather than to " +
                 "the bone that happens to carry it.")]
        [SerializeField] private Vector3 _rotation = Vector3.zero;

        [Tooltip("How thick the shape is, in the object's own units before scaling. " +
                 "Sphere and capsule only.")]
        [Min(0f)]
        [SerializeField] private float _radius = 0.1f;

        [Tooltip("End to end length of a capsule, caps included, the way Unity's own " +
                 "capsule measures. Shorter than two radii is a sphere.")]
        [Min(0f)]
        [SerializeField] private float _height = 0.4f;

        [Tooltip("Width, height and depth of a box, in the object's own units.")]
        [SerializeField] private Vector3 _size = new Vector3(0.3f, 0.3f, 0.3f);

        [Tooltip("Which of the object's own axes the capsule runs along, or the plane " +
                 "faces. A plane pushes everything to the side its axis points at.")]
        [SerializeField] private FluffyAxis _direction = FluffyAxis.Y;

        [Tooltip("Draw the shape in the scene. Worth turning off once a character has a " +
                 "dozen of them and the view is more collider than character.")]
        [SerializeField] private bool _draw = true;

        [Tooltip("Colour of the drawn shape.")]
        [SerializeField] private Color _colour = new Color(0.36f, 0.62f, 1f, 0.9f);

        /// <summary>Sphere, capsule, box or plane.</summary>
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

        /// <summary>How the shape is turned on the object, as euler angles.</summary>
        public Vector3 Rotation
        {
            get => _rotation;
            set => _rotation = value;
        }

        /// <summary>How thick a sphere or capsule is, before the object's scale.</summary>
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

        /// <summary>Width, height and depth of a box, before the object's scale.</summary>
        public Vector3 Size
        {
            get => _size;
            set => _size = new Vector3(Mathf.Max(0f, value.x), Mathf.Max(0f, value.y), Mathf.Max(0f, value.z));
        }

        /// <summary>Which axis a capsule runs along, or a plane faces.</summary>
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
        public float WorldRadius => _radius * LargestScale;

        /// <summary>Where the shape's centre is in the world.</summary>
        public Vector3 WorldCentre => transform.TransformPoint(_centre);

        /// <summary>
        /// How the shape is turned in the world: the bone's own rotation with the shape's
        /// turned on top of it.
        /// </summary>
        /// <remarks>
        /// Every part of the shape that has a direction goes through here — the capsule's
        /// axis, the plane's normal, the box's frame — so turning a shape turns all of it
        /// at once, and the rotation is about the shape's centre rather than the bone's
        /// origin.
        /// </remarks>
        public Quaternion WorldRotation => transform.rotation * Quaternion.Euler(_rotation);

        /// <summary>Which way a plane faces, or a capsule runs, in the world.</summary>
        public Vector3 WorldAxis
        {
            get
            {
                Vector3 axis = WorldRotation * AxisVector(_direction);

                return axis.sqrMagnitude > Tiny ? axis.normalized : Vector3.up;
            }
        }

        /// <summary>
        /// The line a capsule's centre sweeps along, from one cap's centre to the other.
        /// Both ends are the same point for every other shape, and for a capsule too
        /// short to have a middle.
        /// </summary>
        public void WorldSegment(out Vector3 from, out Vector3 to)
        {
            Vector3 centre = WorldCentre;
            from = centre;
            to = centre;

            if (_shape != FluffyColliderShape.Capsule)
            {
                return;
            }

            float half = Mathf.Max(0f, _height * 0.5f - _radius);

            if (half <= Tiny)
            {
                return;
            }

            // Scaled along the axis it runs on: a capsule on a bone stretched by its
            // animation should stretch with it.
            Vector3 reach = WorldAxis * (half * AxisScale(_direction));

            from = centre - reach;
            to = centre + reach;
        }

        /// <summary>Half the box's width, height and depth, in world units.</summary>
        public Vector3 WorldHalfSize
        {
            get
            {
                Vector3 scale = transform.lossyScale;

                return new Vector3(
                    Mathf.Abs(_size.x * scale.x), Mathf.Abs(_size.y * scale.y), Mathf.Abs(_size.z * scale.z)) * 0.5f;
            }
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
        public bool PushOut(ref Vector3 point, float thickness = 0f)
        {
            float skin = Mathf.Max(0f, thickness);

            switch (_shape)
            {
                case FluffyColliderShape.Box:
                    return PushOutOfBox(ref point, skin);

                case FluffyColliderShape.Plane:
                    return PushOffPlane(ref point, skin);

                default:
                    return PushOffSegment(ref point, skin);
            }
        }

        /// <summary>
        /// A sphere, and a capsule, which is a sphere whose centre is a line.
        /// </summary>
        /// <remarks>
        /// A point exactly at the centre has no shortest way out, and the direction
        /// picked for it has to be something rather than a NaN. The capsule's axis is the
        /// least surprising answer for a limb: a bone that has ended up inside a thigh
        /// leaves along the leg rather than sideways through it.
        /// </remarks>
        private bool PushOffSegment(ref Vector3 point, float thickness)
        {
            float radius = WorldRadius + thickness;

            if (radius <= Tiny)
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

            away = distance <= Tiny ? WorldAxis : away / distance;
            point = nearest + away * radius;

            return true;
        }

        /// <summary>
        /// A brick, worked out in its own frame so that any scale and any rotation are
        /// the same problem.
        /// </summary>
        /// <remarks>
        /// Two cases and they are not the same. A point outside the box but within the
        /// chain's own thickness leaves along the line from the nearest point of the
        /// surface, which rounds the corners the way a rope resting on one would. A point
        /// inside leaves through the nearest face, because any other choice drags it
        /// across the middle of the box on its way out — a strand that clips into a chest
        /// should come out of the front it went in at, not out of the back.
        /// </remarks>
        private bool PushOutOfBox(ref Vector3 point, float thickness)
        {
            Vector3 half = WorldHalfSize;

            if (half.x <= Tiny && half.y <= Tiny && half.z <= Tiny)
            {
                return false;
            }

            Quaternion rotation = WorldRotation;
            Vector3 centre = WorldCentre;
            Vector3 local = Quaternion.Inverse(rotation) * (point - centre);

            var clamped = new Vector3(
                Mathf.Clamp(local.x, -half.x, half.x),
                Mathf.Clamp(local.y, -half.y, half.y),
                Mathf.Clamp(local.z, -half.z, half.z));

            Vector3 away = local - clamped;
            float distance = away.magnitude;

            if (distance > Tiny)
            {
                if (distance >= thickness)
                {
                    return false;
                }

                local = clamped + away / distance * thickness;
                point = centre + rotation * local;

                return true;
            }

            // Inside. Whichever face is nearest is the way out.
            Vector3 depth = half - new Vector3(Mathf.Abs(local.x), Mathf.Abs(local.y), Mathf.Abs(local.z));
            int axis = depth.x <= depth.y && depth.x <= depth.z ? 0 : depth.y <= depth.z ? 1 : 2;

            float sign = local[axis] < 0f ? -1f : 1f;
            local[axis] = sign * (half[axis] + thickness);
            point = centre + rotation * local;

            return true;
        }

        /// <summary>
        /// A surface with no edges, pushing everything to the side its axis points at.
        /// </summary>
        /// <remarks>
        /// No size and no extent on purpose. A plane on the spine, facing back, is the
        /// whole of "the hair never falls forward over the face" — and it keeps meaning
        /// that when the character turns, because it turns with the bone. A box would
        /// have to be big enough to cover every place the hair might reach, and would
        /// still let it through at the edges.
        /// </remarks>
        private bool PushOffPlane(ref Vector3 point, float thickness)
        {
            Vector3 normal = WorldAxis;
            float above = Vector3.Dot(point - WorldCentre, normal);

            if (above >= thickness)
            {
                return false;
            }

            point += normal * (thickness - above);

            return true;
        }

        /// <summary>The point on a segment closest to <paramref name="point"/>.</summary>
        private static Vector3 NearestOnSegment(Vector3 from, Vector3 to, Vector3 point)
        {
            Vector3 along = to - from;
            float lengthSquared = along.sqrMagnitude;

            if (lengthSquared <= Tiny)
            {
                return from;
            }

            float t = Mathf.Clamp01(Vector3.Dot(point - from, along) / lengthSquared);

            return from + along * t;
        }

        private float LargestScale
        {
            get
            {
                Vector3 scale = transform.lossyScale;

                return Mathf.Max(Mathf.Abs(scale.x), Mathf.Max(Mathf.Abs(scale.y), Mathf.Abs(scale.z)));
            }
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
            _size = new Vector3(Mathf.Max(0f, _size.x), Mathf.Max(0f, _size.y), Mathf.Max(0f, _size.z));
        }

        private void OnDrawGizmos()
        {
            if (!_draw)
            {
                return;
            }

            Gizmos.color = _colour;

            switch (_shape)
            {
                case FluffyColliderShape.Box:
                    DrawBox();
                    break;

                case FluffyColliderShape.Plane:
                    DrawPlane();
                    break;

                default:
                    DrawSegment();
                    break;
            }
        }

        private void DrawSegment()
        {
            WorldSegment(out Vector3 from, out Vector3 to);
            float radius = WorldRadius;

            Gizmos.DrawWireSphere(from, radius);

            if (from == to)
            {
                return;
            }

            Gizmos.DrawWireSphere(to, radius);

            // Four rails along the capsule's side, so the shape reads as a tube rather
            // than as two loose balls.
            Vector3 along = (to - from).normalized;
            Vector3 side = Vector3.Cross(along, Vector3.up);

            if (side.sqrMagnitude < Tiny)
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

        private void DrawBox()
        {
            Matrix4x4 previous = Gizmos.matrix;

            Gizmos.matrix = Matrix4x4.TRS(WorldCentre, WorldRotation, Vector3.one);
            Gizmos.DrawWireCube(Vector3.zero, WorldHalfSize * 2f);
            Gizmos.matrix = previous;
        }

        /// <summary>
        /// A patch of the plane with an arrow off it, since an infinite surface cannot be
        /// drawn and a patch with no arrow does not say which side is the allowed one.
        /// </summary>
        private void DrawPlane()
        {
            Vector3 centre = WorldCentre;
            Vector3 normal = WorldAxis;

            Vector3 side = Vector3.Cross(normal, Vector3.up);

            if (side.sqrMagnitude < Tiny)
            {
                side = Vector3.Cross(normal, Vector3.forward);
            }

            float reach = Mathf.Max(0.25f, LargestScale * 0.5f);
            side = side.normalized * reach;
            Vector3 other = Vector3.Cross(normal, side.normalized) * reach;

            Gizmos.DrawLine(centre + side + other, centre + side - other);
            Gizmos.DrawLine(centre + side - other, centre - side - other);
            Gizmos.DrawLine(centre - side - other, centre - side + other);
            Gizmos.DrawLine(centre - side + other, centre + side + other);

            Gizmos.DrawLine(centre + side + other, centre - side - other);
            Gizmos.DrawLine(centre + side - other, centre - side + other);

            Vector3 tip = centre + normal * reach;
            Gizmos.DrawLine(centre, tip);
            Gizmos.DrawLine(tip, tip - normal * (reach * 0.25f) + side.normalized * (reach * 0.15f));
            Gizmos.DrawLine(tip, tip - normal * (reach * 0.25f) - side.normalized * (reach * 0.15f));
        }

        // TODO: an inside-out mode, for a chain that has to stay within a volume.
    }
}
