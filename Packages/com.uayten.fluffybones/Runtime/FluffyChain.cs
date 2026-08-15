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
        private const int ConeSegments = 32;
        private const int ArcSegments = 24;
        private const float ConeScale = 0.8f;
        private const float TwistCircleScale = 0.35f;
        private const float TwistCircleOffset = 0.5f;

        // Shared by the axis lines and the limit shapes, so an arc and the axis it
        // belongs to are obviously the same thing.
        private static readonly Color AxisXColor = new Color(0.93f, 0.35f, 0.35f);
        private static readonly Color AxisYColor = new Color(0.5f, 0.86f, 0.3f);
        private static readonly Color AxisZColor = new Color(0.36f, 0.62f, 1f);

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
        [SerializeField] private float _dummyLength = 0.1f;

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

                FluffyLimits limits = ResolveLimits(i);

                _joints.Add(new Joint
                {
                    Transform = bone,
                    SwingYLimit = limits.SwingY,
                    SwingZLimit = limits.SwingZ,
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
                direction = ApplyAngleLimits(joint, restRotation, direction);

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
        private static Vector3 ApplyAngleLimits(Joint joint, Quaternion restRotation, Vector3 direction)
        {
            if (FluffyLimits.IsFree(joint.SwingYLimit) && FluffyLimits.IsFree(joint.SwingZLimit))
            {
                return direction;
            }

            // Into the bone's rest frame, where the limits are expressed.
            Vector3 local = Quaternion.Inverse(restRotation) * direction;
            BuildSwingFrame(joint.BoneAxis, out Vector3 towardsY, out Vector3 towardsZ);

            float swingY = Mathf.Asin(Mathf.Clamp(Vector3.Dot(local, towardsY), -1f, 1f)) * Mathf.Rad2Deg;
            float swingZ = Mathf.Asin(Mathf.Clamp(Vector3.Dot(local, towardsZ), -1f, 1f)) * Mathf.Rad2Deg;

            float clampedY = Mathf.Clamp(swingY, joint.SwingYLimit.x, joint.SwingYLimit.y);
            float clampedZ = Mathf.Clamp(swingZ, joint.SwingZLimit.x, joint.SwingZLimit.y);

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
        public void DrawLimitGizmos()
        {
            if (_startBone == null)
            {
                return;
            }

            Color previous = Gizmos.color;
            List<Transform> bones = CollectChain(_startBone, _lastBone);

            for (int i = 0; i < bones.Count; i++)
            {
                Transform bone = bones[i];
                Vector3 head = bone.position;
                Vector3 toTip = ResolveTip(bones, i) - head;
                float length = toTip.magnitude;

                if (length < MinBoneLength)
                {
                    continue;
                }

                FluffyLimits limits = ResolveLimits(i);
                if (limits.IsUnrestricted)
                {
                    continue;
                }

                Vector3 axis = toTip / length;
                BuildSwingFrame(axis, out Vector3 towardsY, out Vector3 towardsZ);
                float size = length * ConeScale;

                // Each axis in its own colour, matching the lines Show Axes draws: the
                // arc you are looking at names the field you need to edit.
                DrawSwingArc(head, axis, towardsY, limits.SwingY, size, AxisYColor);
                DrawSwingArc(head, axis, towardsZ, limits.SwingZ, size, AxisZColor);
                DrawTwistCircle(head, axis, towardsY, towardsZ, limits.Twist, size);
            }

            Gizmos.color = previous;
        }

        /// <summary>
        /// The flat arc one axis may swing through, drawn in that axis's colour.
        /// </summary>
        private static void DrawSwingArc(
            Vector3 head, Vector3 axis, Vector3 towards, Vector2 range, float length, Color color)
        {
            if (FluffyLimits.IsFree(range))
            {
                return;
            }

            Gizmos.color = color;

            Vector3 start = head + Swing(axis, towards, range.x) * length;
            Vector3 previous = start;

            for (int step = 1; step <= ArcSegments; step++)
            {
                float degrees = Mathf.Lerp(range.x, range.y, step / (float)ArcSegments);
                Vector3 point = head + Swing(axis, towards, degrees) * length;
                Gizmos.DrawLine(previous, point);
                previous = point;
            }

            // The two edges of the arc, so where it stops is unmistakable.
            Gizmos.DrawLine(head, start);
            Gizmos.DrawLine(head, previous);
        }

        private static void DrawTwistCircle(
            Vector3 head, Vector3 axis, Vector3 towardsY, Vector3 towardsZ, Vector2 range, float length)
        {
            if (FluffyLimits.IsFree(range))
            {
                return;
            }

            Gizmos.color = AxisXColor;

            float radius = length * TwistCircleScale;
            Vector3 centre = head + axis * (length * TwistCircleOffset);
            Vector3 previous = centre + towardsY * radius;

            for (int step = 1; step <= ConeSegments; step++)
            {
                float around = step / (float)ConeSegments * Mathf.PI * 2f;
                Vector3 point = centre + (towardsY * Mathf.Cos(around) + towardsZ * Mathf.Sin(around)) * radius;
                Gizmos.DrawLine(previous, point);
                previous = point;
            }

            // Two spokes marking where the twist is allowed to stop.
            DrawTwistSpoke(centre, towardsY, towardsZ, radius, range.x);
            DrawTwistSpoke(centre, towardsY, towardsZ, radius, range.y);
        }

        private static void DrawTwistSpoke(
            Vector3 centre, Vector3 towardsY, Vector3 towardsZ, float radius, float degrees)
        {
            float radians = degrees * Mathf.Deg2Rad;
            Gizmos.DrawLine(centre, centre + (towardsY * Mathf.Cos(radians) + towardsZ * Mathf.Sin(radians)) * radius);
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

            return bone.position + (bone.position - bones[index - 1].position);
        }

        /// <summary>
        /// Which way the invented tip points: along the rig when there is a bone below,
        /// otherwise carrying on from the bone before it.
        /// </summary>
        private static Vector3 TipDirection(List<Transform> bones, int index)
        {
            Transform bone = bones[index];

            Vector3 direction = bone.childCount > 0
                ? bone.GetChild(0).position - bone.position
                : bone.position - bones[index - 1].position;

            return direction.sqrMagnitude < MinBoneLength ? bone.forward : direction.normalized;
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

            /// <summary>Min and max swing towards the bone's local Y, in degrees.</summary>
            public Vector2 SwingYLimit;

            /// <summary>Min and max swing towards the bone's local Z, in degrees.</summary>
            public Vector2 SwingZLimit;

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
