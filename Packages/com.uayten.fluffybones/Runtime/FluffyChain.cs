using System;
using System.Collections.Generic;
using UnityEngine;

namespace Fluffy
{
    /// <summary>
    /// A single chain of bones driven by the Fluffy Bones solver — a tail, one
    /// strand of a skirt, a lock of hair, a length of chain.
    /// </summary>
    /// <remarks>
    /// Not a component: chains live in a list on the character's
    /// <see cref="FluffyBones"/> component, which owns them and steps them. The
    /// bones run from <see cref="StartBone"/> to <see cref="LastBone"/>, following
    /// the first child of each bone.
    /// </remarks>
    [Serializable]
    public class FluffyChain
    {
        private const float DefaultReturnStrength = 8f;
        private const float DefaultDrag = 0.15f;
        private const float MinBoneLength = 1e-5f;
        private const float DegreesPerSegment = 7.5f;
        private const int MinSegments = 6;
        private const int MaxSegments = 48;
        private const float TwistCircleScale = 0.35f;
        private const float TwistCircleOffset = 0.5f;

        // Shared by the axis lines and the limit shapes, so an arc and the axis it
        // belongs to are obviously the same thing.
        private static readonly Color AxisXColor = new Color(0.93f, 0.35f, 0.35f);
        private static readonly Color AxisYColor = new Color(0.5f, 0.86f, 0.3f);
        private static readonly Color AxisZColor = new Color(0.36f, 0.62f, 1f);

        /// <summary>
        /// The dummy bone length a new chain starts with, in world units. Public so the
        /// inspector can put it into an entry Unity created by zeroing one.
        /// </summary>
        public const float DefaultDummyLength = 0.1f;

        /// <summary>
        /// How big the drawn limit shapes are by default, as a fraction of the bone's
        /// length. Public so the component's own setting can start there.
        /// </summary>
        public const float DefaultLimitSize = 0.8f;

        [Tooltip("Where the chain starts. Everything below it comes along, following " +
                 "the first child of each bone.")]
        [SerializeField] private Transform _startBone;

        [Tooltip("Where the chain stops, included. Leave empty to run all the way to " +
                 "the end of the hierarchy.")]
        [SerializeField] private Transform _lastBone;

        [Tooltip("Invent a bone past the end of the chain, so the last real bone has " +
                 "something to swing towards. Rigs that already end in a spare bone do " +
                 "not need it.")]
        [SerializeField] private bool _useDummyBone = true;

        [Tooltip("Work out the dummy bone's length instead of setting it: the bone below " +
                 "the chain when the rig has one, otherwise the length of the bone before it.")]
        [SerializeField] private bool _autoDummyLength = true;

        [Tooltip("Length of the dummy bone, in world units. A bone in a game engine is a " +
                 "single point, so the last one has no length of its own — this stands in " +
                 "for the head-to-tail a bone has in Blender.")]
        [Min(0f)]
        [SerializeField] private float _dummyLength = DefaultDummyLength;

        [Tooltip("Tuning for this chain alone. Empty falls back to the body's profile.")]
        [SerializeField] private FluffyProfile _profileOverride;

        [Tooltip("A saved pose, shared with other chains. Empty keeps the rotations on " +
                 "this chain alone.")]
        [SerializeField] private FluffyPose _pose;

        [Tooltip("Limits every bone takes unless it overrides them. Used when no pose " +
                 "asset is assigned.")]
        [SerializeField] private FluffyLimits _globalLimits = FluffyLimits.Free;

        [Tooltip("What each bone rests at and how far it may swing. Used when no pose " +
                 "asset is assigned.")]
        [SerializeField] private FluffyBonePose[] _defaultPose;

        private readonly List<Joint> _joints = new List<Joint>();
        private bool _isBuilt;

        /// <summary>Creates an unconfigured chain. Used by the Unity serializer.</summary>
        public FluffyChain()
        {
        }

        /// <summary>Creates a chain starting at <paramref name="startBone"/>.</summary>
        public FluffyChain(Transform startBone)
        {
            _startBone = startBone;
        }

        /// <summary>Where the chain starts.</summary>
        public Transform StartBone
        {
            get => _startBone;
            set => _startBone = value;
        }

        /// <summary>Where the chain stops, included. Null runs to the end of the hierarchy.</summary>
        public Transform LastBone
        {
            get => _lastBone;
            set => _lastBone = value;
        }

        /// <summary>Tuning for this chain alone, or null to use the body's profile.</summary>
        public FluffyProfile ProfileOverride
        {
            get => _profileOverride;
            set => _profileOverride = value;
        }

        /// <summary>Whether the chain found usable bones and is being simulated.</summary>
        public bool IsBuilt => _isBuilt;

        /// <summary>Bones the chain is currently simulating, root first.</summary>
        public int JointCount => _joints.Count;

        /// <summary>
        /// Walks the hierarchy below the root, caches the rest pose and prepares the
        /// simulation state.
        /// </summary>
        /// <param name="context">Object blamed in warnings, so clicking one selects the character.</param>
        /// <returns>True when the chain has something to simulate.</returns>
        public bool Build(UnityEngine.Object context = null)
        {
            _isBuilt = false;
            _joints.Clear();

            if (_startBone == null)
            {
                return false;
            }

            List<Transform> bones = CollectChain(_startBone, _lastBone);
            if (bones.Count < 2)
            {
                Debug.LogWarning($"[Fluffy Bones] Chain '{_startBone.name}' needs at least two bones — " +
                                 "the start bone and one child. Nothing to simulate.", context);
                return false;
            }

            if (_lastBone != null && bones[bones.Count - 1] != _lastBone)
            {
                Debug.LogWarning($"[Fluffy Bones] '{_lastBone.name}' is not below '{_startBone.name}' in the " +
                                 "hierarchy, so the chain runs to the end instead of stopping there.", context);
            }

            // Pose the bones before measuring them: lengths and directions have to be
            // read off the pose the chain will rest at, not off whatever the animator
            // happens to be showing right now.
            Quaternion[] restRotations = ResolveRestRotations(bones);
            for (int i = 0; i < bones.Count; i++)
            {
                bones[i].localRotation = restRotations[i];
            }

            for (int i = 0; i < bones.Count; i++)
            {
                if (!_useDummyBone && HasVirtualTip(bones, i))
                {
                    // Without a dummy bone this one has nothing to aim at, so it is left
                    // to follow its parent rather than being simulated.
                    continue;
                }

                Transform bone = bones[i];
                Vector3 tip = ResolveTip(bones, i);
                float length = Vector3.Distance(bone.position, tip);
                if (length < MinBoneLength)
                {
                    // Zero-length bones have no direction to rotate towards; skip them
                    // rather than feeding NaNs into the solver.
                    continue;
                }

                _joints.Add(new Joint
                {
                    Transform = bone,
                    BoneIndex = i,
                    RestLocalRotation = restRotations[i],
                    BoneAxis = Quaternion.Inverse(bone.rotation) * ((tip - bone.position) / length),
                    Length = length,
                    NormalizedDepth = i / (float)(bones.Count - 1),
                    CurrentTip = tip,
                    PreviousTip = tip
                });
            }

            _isBuilt = _joints.Count > 0;
            return _isBuilt;
        }

        /// <summary>
        /// Snaps every bone back to the pose the chain was built in and clears the
        /// accumulated motion, so the next step starts from rest instead of catching up.
        /// </summary>
        public void ResetToRestPose()
        {
            if (!_isBuilt)
            {
                return;
            }

            for (int i = 0; i < _joints.Count; i++)
            {
                _joints[i].Transform.localRotation = _joints[i].RestLocalRotation;
            }

            // Read the tips only after the whole chain is back at rest — each bone's
            // world position depends on the rotations above it.
            for (int i = 0; i < _joints.Count; i++)
            {
                Joint joint = _joints[i];
                joint.CurrentTip = joint.Transform.position + joint.Transform.rotation * joint.BoneAxis * joint.Length;
                joint.PreviousTip = joint.CurrentTip;
            }
        }

        /// <summary>
        /// Reads out what every bone of the chain is doing this frame, for tracing.
        /// </summary>
        /// <remarks>
        /// The angles come out of the same frame and the same decomposition the solver
        /// clamps in, so a trace lines up with the numbers in the inspector instead of
        /// having to be translated. Works while the chain is running and while it is not.
        /// </remarks>
        public void CaptureState(List<FluffyBoneState> into)
        {
            if (into == null || _startBone == null)
            {
                return;
            }

            List<Transform> bones = CollectChain(_startBone, _lastBone);

            for (int i = 0; i < bones.Count; i++)
            {
                if (!ResolveRestFrame(bones, i, out Quaternion restRotation, out Vector3 boneAxis, out float length))
                {
                    continue;
                }

                Transform bone = bones[i];
                FluffyLimits limits = ResolveLimits(i);

                Vector3 direction = bone.rotation * boneAxis;
                Vector3 local = Quaternion.Inverse(restRotation) * direction;
                BuildSwingFrame(boneAxis, out Vector3 towardsY, out Vector3 towardsZ);

                float swingY = Mathf.Asin(Mathf.Clamp(Vector3.Dot(local, towardsY), -1f, 1f)) * Mathf.Rad2Deg;
                float swingZ = Mathf.Asin(Mathf.Clamp(Vector3.Dot(local, towardsZ), -1f, 1f)) * Mathf.Rad2Deg;

                into.Add(new FluffyBoneState
                {
                    Bone = bone,
                    Index = i,
                    Head = bone.position,
                    Direction = direction,
                    RestDirection = restRotation * boneAxis,
                    SwingY = swingY,
                    SwingZ = swingZ,
                    Twist = TwistAngle(Quaternion.Inverse(restRotation) * bone.rotation, boneAxis),
                    Limits = limits,
                    Length = length,
                    AtSwingYLimit = IsAgainst(swingY, limits.SwingY),
                    AtSwingZLimit = IsAgainst(swingZ, limits.SwingZ)
                });
            }
        }

        /// <summary>Whether an angle is sitting on one end of its range.</summary>
        private static bool IsAgainst(float angle, Vector2 range)
        {
            const float Touching = 0.05f;
            return !FluffyLimits.IsFree(range)
                   && (angle <= range.x + Touching || angle >= range.y - Touching);
        }

        /// <summary>
        /// How much of a rotation is a roll about <paramref name="axis"/>, in degrees.
        /// </summary>
        /// <remarks>
        /// The swing-twist decomposition: the part of the quaternion that lies along the
        /// axis is the roll, and what is left is the swing. Signed, so a trace shows
        /// which way it rolled rather than only how far.
        /// </remarks>
        private static float TwistAngle(Quaternion rotation, Vector3 axis)
        {
            var vector = new Vector3(rotation.x, rotation.y, rotation.z);
            Vector3 projection = Vector3.Project(vector, axis);
            var twist = new Quaternion(projection.x, projection.y, projection.z, rotation.w);

            if (twist.x * twist.x + twist.y * twist.y + twist.z * twist.z + twist.w * twist.w < 1e-8f)
            {
                return 0f;
            }

            twist.Normalize();
            float angle = Quaternion.Angle(Quaternion.identity, twist);

            return Vector3.Dot(projection, axis) < 0f ? -angle : angle;
        }

        /// <summary>
        /// Moves the running state with the character, as if the chain were rigid for
        /// that move.
        /// </summary>
        /// <remarks>
        /// For a jump the solver cannot swing through: a character that crosses the level
        /// between two frames, or simply a frame long enough that its head moved further
        /// than the bone is long. Left alone, the tips stay where they were and the bones
        /// are handed a direction that means nothing, so they fly out and spend the next
        /// frames coming back.
        ///
        /// Both tips go through the same rigid move, so the chain keeps the shape it had
        /// and the speed it was travelling at, and simply arrives with the character.
        /// Snapping it back to the rest pose instead throws both away, and that discard
        /// is itself the pop it was meant to prevent.
        /// </remarks>
        public void Carry(Vector3 fromPosition, Quaternion fromRotation, Vector3 toPosition, Quaternion toRotation)
        {
            if (!_isBuilt)
            {
                return;
            }

            Quaternion turn = toRotation * Quaternion.Inverse(fromRotation);

            for (int i = 0; i < _joints.Count; i++)
            {
                Joint joint = _joints[i];
                joint.CurrentTip = toPosition + turn * (joint.CurrentTip - fromPosition);
                joint.PreviousTip = toPosition + turn * (joint.PreviousTip - fromPosition);
            }
        }

        /// <summary>Advances the chain by one step.</summary>
        /// <param name="deltaTime">Seconds since the last step.</param>
        /// <param name="fallbackProfile">Used when the chain has no override of its own.</param>
        public void Simulate(float deltaTime, FluffyProfile fallbackProfile)
        {
            if (!_isBuilt || deltaTime <= 0f)
            {
                return;
            }

            FluffyProfile profile = _profileOverride != null ? _profileOverride : fallbackProfile;
            float drag = profile != null ? profile.Drag : DefaultDrag;
            Vector3 gravityStep = (profile != null ? profile.Gravity : Vector3.zero) * (deltaTime * deltaTime);
            float inertiaRetained = 1f - drag;

            // Root first: rotating a bone moves every bone below it, so each joint has
            // to read a position its parent has already settled this step.
            for (int i = 0; i < _joints.Count; i++)
            {
                Joint joint = _joints[i];
                Transform bone = joint.Transform;

                Vector3 position = bone.position;
                Quaternion parentRotation = bone.parent != null ? bone.parent.rotation : Quaternion.identity;
                Quaternion restRotation = parentRotation * joint.RestLocalRotation;
                Vector3 restDirection = restRotation * joint.BoneAxis;

                float returnStrength = profile != null
                    ? profile.EvaluateReturnStrength(joint.NormalizedDepth)
                    : DefaultReturnStrength;

                Vector3 inertia = (joint.CurrentTip - joint.PreviousTip) * inertiaRetained;
                Vector3 pullToRest = restDirection * (returnStrength * joint.Length * deltaTime);

                Vector3 nextTip = joint.CurrentTip + inertia + pullToRest + gravityStep;

                // The bone is rigid: the tip may swing anywhere, but only on the sphere
                // of its own rest length around the bone's head.
                Vector3 offset = nextTip - position;
                float distance = offset.magnitude;
                Vector3 direction = distance < MinBoneLength ? restDirection : offset / distance;

                // Read now, not copied when the chain was built: the limits are authoring
                // data and get tuned while watching the thing move.
                direction = ApplyAngleLimits(joint, ResolveLimits(joint.BoneIndex), restRotation, direction);

                nextTip = position + direction * joint.Length;

                joint.PreviousTip = joint.CurrentTip;
                joint.CurrentTip = nextTip;

                bone.rotation = Quaternion.FromToRotation(restDirection, nextTip - position) * restRotation;
            }
        }

        /// <summary>
        /// Holds a bone inside the cone it is allowed to swing through.
        /// </summary>
        /// <remarks>
        /// The swing is split into how far the bone has tipped towards its own Y and
        /// towards its own Z, each clamped against its own minimum and maximum. Two
        /// independent ranges rather than one angle is what makes the cone lopsided:
        /// a cape can be given a lot of room on one side and almost none on the other.
        /// </remarks>
        private static Vector3 ApplyAngleLimits(
            Joint joint, FluffyLimits limits, Quaternion restRotation, Vector3 direction)
        {
            if (FluffyLimits.IsFree(limits.SwingY) && FluffyLimits.IsFree(limits.SwingZ))
            {
                return direction;
            }

            // Into the bone's rest frame, where the limits are expressed.
            Vector3 local = Quaternion.Inverse(restRotation) * direction;
            BuildSwingFrame(joint.BoneAxis, out Vector3 towardsY, out Vector3 towardsZ);

            float swingY = Mathf.Asin(Mathf.Clamp(Vector3.Dot(local, towardsY), -1f, 1f)) * Mathf.Rad2Deg;
            float swingZ = Mathf.Asin(Mathf.Clamp(Vector3.Dot(local, towardsZ), -1f, 1f)) * Mathf.Rad2Deg;

            float clampedY = Mathf.Clamp(swingY, limits.SwingY.x, limits.SwingY.y);
            float clampedZ = Mathf.Clamp(swingZ, limits.SwingZ.x, limits.SwingZ.y);

            if (Mathf.Approximately(clampedY, swingY) && Mathf.Approximately(clampedZ, swingZ))
            {
                return direction;
            }

            return restRotation * SwingToDirection(joint.BoneAxis, towardsY, towardsZ, clampedY, clampedZ);
        }

        /// <summary>
        /// The bone's local Y and Z, squared up against the bone's own axis so the two
        /// swings stay independent of each other.
        /// </summary>
        private static void BuildSwingFrame(Vector3 axis, out Vector3 towardsY, out Vector3 towardsZ)
        {
            towardsY = Vector3.ProjectOnPlane(Vector3.up, axis);

            // A bone that runs along Y has no Y left to swing towards; borrow another.
            if (towardsY.sqrMagnitude < MinBoneLength)
            {
                towardsY = Vector3.ProjectOnPlane(Vector3.forward, axis);
            }

            towardsY.Normalize();
            towardsZ = Vector3.Cross(axis, towardsY);
        }

        /// <summary>Rebuilds a direction from how far it tips towards each axis.</summary>
        private static Vector3 SwingToDirection(
            Vector3 axis, Vector3 towardsY, Vector3 towardsZ, float degreesY, float degreesZ)
        {
            float y = Mathf.Sin(degreesY * Mathf.Deg2Rad);
            float z = Mathf.Sin(degreesZ * Mathf.Deg2Rad);
            float along = Mathf.Sqrt(Mathf.Max(0f, 1f - y * y - z * z));

            return (axis * along + towardsY * y + towardsZ * z).normalized;
        }

        /// <summary>
        /// Draws the chain's bones as wireframe octahedra, the shape a skeleton is
        /// normally shown with, so the chain can be seen and posed without a separate
        /// bone renderer component.
        /// </summary>
        public void DrawBoneGizmos()
        {
            if (_startBone == null)
            {
                return;
            }

            Color color = Gizmos.color;
            List<Transform> bones = CollectChain(_startBone, _lastBone);

            for (int i = 0; i < bones.Count; i++)
            {
                bool virtualTip = HasVirtualTip(bones, i);
                if (virtualTip && !_useDummyBone)
                {
                    continue;
                }

                // The dummy bone is drawn faded, so it reads as a stand-in rather than
                // a bone the rig actually has.
                Gizmos.color = virtualTip ? new Color(color.r, color.g, color.b, color.a * 0.45f) : color;
                DrawBone(bones[i].position, ResolveTip(bones, i));
            }

            Gizmos.color = color;
        }

        /// <summary>
        /// Draws what each bone is allowed to do, starting at its head: a flat arc for
        /// the Y swing, another for the Z, and a circle around the bone for the X twist.
        /// </summary>
        /// <remarks>
        /// One arc per axis rather than the rim of the cone they make together. The rim
        /// is the truthful shape, but on a chain of bones it reads as a knot of ellipses
        /// crossing each other, and an arc is what a number can be read off: it lies in
        /// the plane its axis swings in, and it is lopsided exactly when that axis's
        /// minimum and maximum differ.
        /// </remarks>
        public void DrawLimitGizmos(float scale = DefaultLimitSize)
        {
            if (_startBone == null)
            {
                return;
            }

            Color previous = Gizmos.color;
            List<Transform> bones = CollectChain(_startBone, _lastBone);

            for (int i = 0; i < bones.Count; i++)
            {
                Quaternion restRotation;
                Vector3 boneAxis;
                float length;

                if (!ResolveRestFrame(bones, i, out restRotation, out boneAxis, out length))
                {
                    continue;
                }

                FluffyLimits limits = ResolveLimits(i);
                Vector3 head = bones[i].position;
                float size = length * scale;

                // Everything hangs off the rest direction, in the bone's own axes, which
                // is where the limits are measured from — the same frame the solver
                // clamps in, so the shape stays put and the bone travels inside it.
                Vector3 restDirection = restRotation * boneAxis;
                BuildSwingFrame(boneAxis, out Vector3 localY, out Vector3 localZ);
                Vector3 towardsY = restRotation * localY;
                Vector3 towardsZ = restRotation * localZ;

                // Each axis in its own colour, matching the lines Show Axes draws: the
                // arc you are looking at names the field you need to edit.
                DrawSwingArc(head, restDirection, towardsY, limits.SwingY, size, AxisYColor);
                DrawSwingArc(head, restDirection, towardsZ, limits.SwingZ, size, AxisZColor);
                DrawTwistCircle(head, restDirection, towardsY, towardsZ, limits.Twist, size);
            }

            Gizmos.color = previous;
        }

        /// <summary>
        /// The frame a bone's limits are measured in: where it would point with only the
        /// animation on it, and the axis it runs along in its own space.
        /// </summary>
        /// <remarks>
        /// Read off the joint while the chain is running rather than off the bone, whose
        /// rotation is the thing being held inside the limit. Drawn from the bone, the
        /// shapes turned with it and there was no telling how far through its range it
        /// had travelled — the cone moved exactly as much as the bone did.
        ///
        /// The rest frame still follows the parent, because the limit does: a skirt
        /// strand's cone swings with the hips and holds the strand inside it.
        /// </remarks>
        private bool ResolveRestFrame(
            List<Transform> bones, int index, out Quaternion restRotation, out Vector3 boneAxis, out float length)
        {
            Transform bone = bones[index];
            Joint joint = FindJoint(bone);

            if (joint != null)
            {
                Quaternion parentRotation = bone.parent != null ? bone.parent.rotation : Quaternion.identity;
                restRotation = parentRotation * joint.RestLocalRotation;
                boneAxis = joint.BoneAxis;
                length = joint.Length;

                return length >= MinBoneLength;
            }

            // Not built, so nothing has moved the bone: where it points is where it rests.
            Vector3 toTip = ResolveTip(bones, index) - bone.position;
            length = toTip.magnitude;
            restRotation = bone.rotation;
            boneAxis = length < MinBoneLength
                ? Vector3.forward
                : Quaternion.Inverse(bone.rotation) * (toTip / length);

            return length >= MinBoneLength;
        }

        /// <summary>The running joint for a bone, or null when the chain is not built.</summary>
        private Joint FindJoint(Transform bone)
        {
            if (!_isBuilt)
            {
                return null;
            }

            for (int i = 0; i < _joints.Count; i++)
            {
                if (_joints[i].Transform == bone)
                {
                    return _joints[i];
                }
            }

            return null;
        }

        /// <summary>
        /// The flat arc one axis may swing through, drawn in that axis's colour: nothing
        /// at 0 to 0, a full circle round the bone's head at -180 to 180.
        /// </summary>
        private static void DrawSwingArc(
            Vector3 head, Vector3 axis, Vector3 towards, Vector2 range, float length, Color color)
        {
            float sweep = range.y - range.x;
            if (sweep <= 0f)
            {
                return;
            }

            Gizmos.color = color;

            int segments = SegmentsFor(sweep);
            Vector3 start = head + Swing(axis, towards, range.x) * length;
            Vector3 previous = start;

            for (int step = 1; step <= segments; step++)
            {
                float degrees = Mathf.Lerp(range.x, range.y, step / (float)segments);
                Vector3 point = head + Swing(axis, towards, degrees) * length;
                Gizmos.DrawLine(previous, point);
                previous = point;
            }

            // The two edges, so where the arc stops is unmistakable — unless it closes
            // on itself, where both fall on the same line behind the bone.
            if (sweep < 360f)
            {
                Gizmos.DrawLine(head, start);
                Gizmos.DrawLine(head, previous);
            }
        }

        /// <summary>
        /// How far the bone may roll, as a ring around it that spans the range and no
        /// more: nothing at all at 0 to 0, a full turn at -180 to 180.
        /// </summary>
        /// <remarks>
        /// The ring is the range, not a dial the range is marked on. Drawing the whole
        /// circle and putting two spokes on it said the opposite of what it meant — a
        /// bone locked at 0 to 0 wore the biggest shape on screen, and one free to roll
        /// all the way round wore none.
        /// </remarks>
        private static void DrawTwistCircle(
            Vector3 head, Vector3 axis, Vector3 towardsY, Vector3 towardsZ, Vector2 range, float length)
        {
            float sweep = range.y - range.x;
            if (sweep <= 0f)
            {
                return;
            }

            Gizmos.color = AxisXColor;

            float radius = length * TwistCircleScale;
            Vector3 centre = head + axis * (length * TwistCircleOffset);

            int segments = SegmentsFor(sweep);
            Vector3 previous = centre + TwistPoint(towardsY, towardsZ, radius, range.x);

            for (int step = 1; step <= segments; step++)
            {
                float degrees = Mathf.Lerp(range.x, range.y, step / (float)segments);
                Vector3 point = centre + TwistPoint(towardsY, towardsZ, radius, degrees);
                Gizmos.DrawLine(previous, point);
                previous = point;
            }

            // The ends, unless it closes on itself and they would read as a stray line
            // across the middle.
            if (sweep < 360f)
            {
                DrawTwistSpoke(centre, towardsY, towardsZ, radius, range.x);
                DrawTwistSpoke(centre, towardsY, towardsZ, radius, range.y);
            }
        }

        private static void DrawTwistSpoke(
            Vector3 centre, Vector3 towardsY, Vector3 towardsZ, float radius, float degrees)
        {
            Gizmos.DrawLine(centre, centre + TwistPoint(towardsY, towardsZ, radius, degrees));
        }

        /// <summary>A point on the twist ring, <paramref name="degrees"/> round from Y.</summary>
        private static Vector3 TwistPoint(Vector3 towardsY, Vector3 towardsZ, float radius, float degrees)
        {
            float radians = degrees * Mathf.Deg2Rad;
            return (towardsY * Mathf.Cos(radians) + towardsZ * Mathf.Sin(radians)) * radius;
        }

        /// <summary>
        /// How many lines to draw a range with. In proportion to how far it reaches, so a
        /// narrow one is not drawn with as many as a whole turn and a whole turn does not
        /// come out a visible polygon.
        /// </summary>
        private static int SegmentsFor(float sweep)
        {
            return Mathf.Clamp(Mathf.CeilToInt(sweep / DegreesPerSegment), MinSegments, MaxSegments);
        }

        /// <summary>The bone's direction tipped <paramref name="degrees"/> towards one axis.</summary>
        private static Vector3 Swing(Vector3 axis, Vector3 towards, float degrees)
        {
            float radians = degrees * Mathf.Deg2Rad;
            return axis * Mathf.Cos(radians) + towards * Mathf.Sin(radians);
        }

        /// <summary>
        /// Draws each bone's local axes — X red, Y green, Z blue. Which way a bone's
        /// axes point decides how it swings, and a rig that was exported with an
        /// unexpected orientation is invisible until you look at them.
        /// </summary>
        public void DrawAxisGizmos()
        {
            if (_startBone == null)
            {
                return;
            }

            Color color = Gizmos.color;
            List<Transform> bones = CollectChain(_startBone, _lastBone);

            for (int i = 0; i < bones.Count; i++)
            {
                Transform bone = bones[i];
                float size = Vector3.Distance(bone.position, ResolveTip(bones, i)) * 0.3f;
                if (size < MinBoneLength)
                {
                    continue;
                }

                Gizmos.color = AxisXColor;
                Gizmos.DrawLine(bone.position, bone.position + bone.right * size);

                Gizmos.color = AxisYColor;
                Gizmos.DrawLine(bone.position, bone.position + bone.up * size);

                Gizmos.color = AxisZColor;
                Gizmos.DrawLine(bone.position, bone.position + bone.forward * size);
            }

            Gizmos.color = color;
        }

        private static void DrawBone(Vector3 head, Vector3 tip)
        {
            Vector3 axis = tip - head;
            float length = axis.magnitude;
            if (length < MinBoneLength)
            {
                return;
            }

            Vector3 forward = axis / length;
            Vector3 reference = Mathf.Abs(Vector3.Dot(forward, Vector3.up)) > 0.99f ? Vector3.right : Vector3.up;
            Vector3 side = Vector3.Cross(forward, reference).normalized;
            Vector3 up = Vector3.Cross(side, forward);

            float radius = length * 0.1f;
            Vector3 waist = head + forward * (length * 0.15f);

            Vector3 a = waist + side * radius;
            Vector3 b = waist + up * radius;
            Vector3 c = waist - side * radius;
            Vector3 d = waist - up * radius;

            Gizmos.DrawLine(head, a);
            Gizmos.DrawLine(head, b);
            Gizmos.DrawLine(head, c);
            Gizmos.DrawLine(head, d);

            Gizmos.DrawLine(a, tip);
            Gizmos.DrawLine(b, tip);
            Gizmos.DrawLine(c, tip);
            Gizmos.DrawLine(d, tip);

            Gizmos.DrawLine(a, b);
            Gizmos.DrawLine(b, c);
            Gizmos.DrawLine(c, d);
            Gizmos.DrawLine(d, a);
        }

        /// <summary>Draws the chain in the scene view. Called by the owning body.</summary>
        public void DrawGizmos()
        {
            if (_isBuilt)
            {
                for (int i = 0; i < _joints.Count; i++)
                {
                    Joint joint = _joints[i];
                    Gizmos.DrawLine(joint.Transform.position, joint.CurrentTip);
                    Gizmos.DrawWireSphere(joint.Transform.position, joint.Length * 0.1f);
                }

                return;
            }

            if (_startBone == null)
            {
                return;
            }

            // Not playing: preview the bones the solver would pick up.
            List<Transform> bones = CollectChain(_startBone, _lastBone);
            for (int i = 0; i < bones.Count - 1; i++)
            {
                Gizmos.DrawLine(bones[i].position, bones[i + 1].position);
                Gizmos.DrawWireSphere(bones[i].position, 0.01f);
            }
        }

        /// <summary>
        /// Records the bones' current local rotations as the pose the chain rests at.
        /// Rotate the bones in the scene until the tail curves the way it should, then
        /// call this — the model keeps whatever it was exported with.
        /// </summary>
        /// <returns>True when a pose was captured.</returns>
        public bool CaptureDefaultPose()
        {
            if (_startBone == null)
            {
                return false;
            }

            List<Transform> bones = CollectChain(_startBone, _lastBone);
            FluffyBonePose[] existing = PoseData;
            var captured = new FluffyBonePose[bones.Count];

            for (int i = 0; i < bones.Count; i++)
            {
                bool kept = existing != null && i < existing.Length;

                captured[i] = new FluffyBonePose(bones[i].localRotation.eulerAngles)
                {
                    // Capture reads rotations off the scene; the limits were authored and
                    // have nothing to do with where the bones happen to be.
                    OverrideLimits = kept && existing[i].OverrideLimits,
                    Limits = kept ? existing[i].Limits : FluffyLimits.Free
                };
            }

            if (_pose != null)
            {
                _pose.SetBones(captured);
            }
            else
            {
                _defaultPose = captured;
            }

            return true;
        }

        /// <summary>The saved pose this chain reads from, or null when it keeps its own.</summary>
        public FluffyPose Pose
        {
            get => _pose;
            set => _pose = value;
        }

        /// <summary>The bones this chain covers, root first, or null without a start bone.</summary>
        public List<Transform> GetBones()
        {
            return _startBone == null ? null : CollectChain(_startBone, _lastBone);
        }

        /// <summary>Forgets the authored pose, going back to the one the model was imported with.</summary>
        public void ClearDefaultPose()
        {
            _defaultPose = null;
        }

        /// <summary>Whether this chain has an authored default pose.</summary>
        public bool HasDefaultPose => PoseData != null && PoseData.Length > 0;

        /// <summary>What the bone at <paramref name="index"/> is allowed to do.</summary>
        /// <remarks>
        /// Clamped here rather than trusted: the inspector keeps 0 inside every range,
        /// but the fields are public and a pose asset can be edited from elsewhere, and
        /// both the solver and the gizmos come through this one call.
        /// </remarks>
        private FluffyLimits ResolveLimits(int index)
        {
            FluffyLimits global = _pose != null ? _pose.GlobalLimits : _globalLimits;
            FluffyBonePose[] pose = PoseData;

            FluffyLimits limits = pose != null && index < pose.Length
                ? pose[index].Resolve(global)
                : global;

            return limits.Clamped;
        }

        /// <summary>
        /// Copies this chain's shared pose and dummy bone settings onto another, so a
        /// skirt is set up once instead of once per strand. Start and last bones are
        /// left alone — those belong to the strand.
        /// </summary>
        public void CopySettingsTo(FluffyChain other)
        {
            if (other == null || other == this)
            {
                return;
            }

            other._pose = _pose;
            other._useDummyBone = _useDummyBone;
            other._autoDummyLength = _autoDummyLength;
            other._dummyLength = _dummyLength;
            other._profileOverride = _profileOverride;

            if (_pose == null && _defaultPose != null)
            {
                other._defaultPose = (FluffyBonePose[])_defaultPose.Clone();
            }
        }

        /// <summary>
        /// Puts the bones back into the authored pose. Useful in the editor after play
        /// mode or an animation has moved them, so the pose can be adjusted further.
        /// </summary>
        /// <returns>The bones that were moved, for undo recording.</returns>
        public List<Transform> ApplyDefaultPose()
        {
            if (_startBone == null)
            {
                return null;
            }

            List<Transform> bones = CollectChain(_startBone, _lastBone);
            Quaternion[] restRotations = ResolveRestRotations(bones);

            for (int i = 0; i < bones.Count; i++)
            {
                bones[i].localRotation = restRotations[i];
            }

            return bones;
        }

        /// <summary>
        /// The local rotation each bone rests at: the authored pose when there is one
        /// that still matches the chain, and the bones' current rotations otherwise.
        /// </summary>
        private Quaternion[] ResolveRestRotations(List<Transform> bones)
        {
            FluffyBonePose[] pose = PoseData;
            int posed = pose == null ? 0 : Mathf.Min(pose.Length, bones.Count);
            var rotations = new Quaternion[bones.Count];

            for (int i = 0; i < bones.Count; i++)
            {
                // A pose longer than the chain hands its first entries to the bones that
                // exist; a shorter one leaves the rest where the model put them.
                rotations[i] = i < posed ? Quaternion.Euler(pose[i].Rotation) : bones[i].localRotation;
            }

            return rotations;
        }

        /// <summary>
        /// Follows the first child of each bone down from <paramref name="start"/>, which
        /// is how a tail, a hair strand or a skirt panel is normally rigged.
        /// </summary>
        /// <param name="start">Where the chain starts.</param>
        /// <param name="last">Where to stop, included. Null runs to the end of the hierarchy.</param>
        public static List<Transform> CollectChain(Transform start, Transform last = null)
        {
            var bones = new List<Transform>();
            Transform current = start;

            while (current != null)
            {
                bones.Add(current);

                if (last != null && current == last)
                {
                    break;
                }

                current = current.childCount > 0 ? current.GetChild(0) : null;
            }

            return bones;
        }

        /// <summary>
        /// Where the bone at <paramref name="index"/> points. Every bone aims at the next
        /// one in the chain; the last one has no next bone, so it aims at a tip that is
        /// either measured from the rig or set by hand.
        /// </summary>
        /// <remarks>
        /// A bone in a game engine is a single point with a rotation — it has no length of
        /// its own, unlike Blender's head-to-tail. The end of the chain therefore needs a
        /// tip invented for it, or it has no direction to rotate towards.
        /// </remarks>
        private Vector3 ResolveTip(List<Transform> bones, int index)
        {
            Transform bone = bones[index];

            if (index < bones.Count - 1)
            {
                return bones[index + 1].position;
            }

            if (!_autoDummyLength)
            {
                return bone.position + TipDirection(bones, index) * _dummyLength;
            }

            if (bone.childCount > 0)
            {
                // The chain was cut short by Last Bone. The bone below is not simulated,
                // but it still marks the direction this one rests in.
                return bone.GetChild(0).position;
            }

            return bone.position + TipDirection(bones, index) * AutoDummyLength(bones, index);
        }

        /// <summary>
        /// Which way a bone points when it has no next bone to aim at: along the rig when
        /// there is a bone below, otherwise off the bone's own rotation.
        /// </summary>
        /// <remarks>
        /// Off its rotation rather than as a straight continuation of the bone before it.
        /// The two agree while the chain is straight and part company the moment the last
        /// bone turns, which left the dummy pointing the way the chain used to go while
        /// the bone it belongs to had moved on — the one bone in the chain that did not
        /// follow its own rotation.
        ///
        /// The axis is read off the previous bone rather than assumed to be X: the
        /// direction from it to this one, taken into its own frame, is the axis a chain
        /// runs along, and putting this bone's rotation on it carries the tip round with
        /// the bone.
        /// </remarks>
        private static Vector3 TipDirection(List<Transform> bones, int index)
        {
            Transform bone = bones[index];

            if (bone.childCount > 0)
            {
                Vector3 toChild = bone.GetChild(0).position - bone.position;
                return toChild.sqrMagnitude < MinBoneLength ? bone.forward : toChild.normalized;
            }

            // A chain of one has no bone before it to read the axis off; its parent in the
            // rig is the next best thing, and a loose bone falls back to its own forward.
            Transform previous = index > 0 ? bones[index - 1] : bone.parent;
            if (previous == null)
            {
                return bone.forward;
            }

            Vector3 along = bone.position - previous.position;
            if (along.sqrMagnitude < MinBoneLength)
            {
                return bone.forward;
            }

            return bone.rotation * (Quaternion.Inverse(previous.rotation) * along.normalized);
        }

        /// <summary>How long an invented tip is when its length is measured off the rig.</summary>
        private static float AutoDummyLength(List<Transform> bones, int index)
        {
            Transform bone = bones[index];
            Transform previous = index > 0 ? bones[index - 1] : bone.parent;

            if (previous == null)
            {
                return DefaultDummyLength;
            }

            float length = Vector3.Distance(bone.position, previous.position);
            return length < MinBoneLength ? DefaultDummyLength : length;
        }

        /// <summary>Whether the bone at <paramref name="index"/> ends in an invented tip.</summary>
        private bool HasVirtualTip(List<Transform> bones, int index)
        {
            return index == bones.Count - 1 && (!_autoDummyLength || bones[index].childCount == 0);
        }

        /// <summary>What the chain rests at: the shared pose when there is one.</summary>
        private FluffyBonePose[] PoseData => _pose != null ? _pose.Bones : _defaultPose;

        /// <summary>Per-bone simulation state, cached at build time.</summary>
        private class Joint
        {
            /// <summary>The bone this joint rotates.</summary>
            public Transform Transform;

            /// <summary>Local rotation of the bone in the pose the chain was built in.</summary>
            public Quaternion RestLocalRotation;

            /// <summary>Unit direction from the bone's head to its tip, in the bone's own space.</summary>
            public Vector3 BoneAxis;

            /// <summary>Rest distance from the bone's head to its tip, in world units.</summary>
            public float Length;

            /// <summary>
            /// Which bone of the chain this is, so its limits can be looked up as they
            /// are now rather than as they were when the chain was built.
            /// </summary>
            public int BoneIndex;

            /// <summary>Position along the chain: 0 at the root, 1 at the tip.</summary>
            public float NormalizedDepth;

            /// <summary>World-space tip position this step.</summary>
            public Vector3 CurrentTip;

            /// <summary>World-space tip position last step — the velocity comes from the difference.</summary>
            public Vector3 PreviousTip;
        }

        // TODO: collision against FluffyCollider.
        // TODO: angle limits, so a skirt cannot fold through the leg.
        // TODO: chains defined by an explicit bone list, for rigs that branch.
    }
}
