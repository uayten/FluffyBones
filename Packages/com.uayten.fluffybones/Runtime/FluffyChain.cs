using System;
using System.Collections.Generic;
using UnityEngine;

namespace FluffyBones
{
    /// <summary>
    /// A single chain of bones driven by the Fluffy Bones solver — a tail, one
    /// strand of a skirt, a lock of hair, a length of chain.
    /// </summary>
    /// <remarks>
    /// Not a component: chains live in a list on the character's
    /// <see cref="FluffyBody"/>, which owns them and steps them. The bones are
    /// taken from the hierarchy below <see cref="RootBone"/>, following the first
    /// child of each bone.
    /// </remarks>
    [Serializable]
    public class FluffyChain
    {
        private const float DefaultStiffness = 8f;
        private const float DefaultDrag = 0.15f;
        private const float MinBoneLength = 1e-5f;

        [Tooltip("First bone of the chain. Everything below it comes along, " +
                 "following the first child of each bone.")]
        [SerializeField] private Transform _rootBone;

        [Tooltip("Tuning for this chain alone. Empty falls back to the body's profile.")]
        [SerializeField] private FluffyProfile _profileOverride;

        [Tooltip("Virtual bone length past the last real bone, in world units, so the " +
                 "tip swings too. 0 leaves the last bone rigid.")]
        [Min(0f)]
        [SerializeField] private float _tipLength = 0.05f;

        private readonly List<Joint> _joints = new List<Joint>();
        private bool _isBuilt;

        /// <summary>Creates an unconfigured chain. Used by the Unity serializer.</summary>
        public FluffyChain()
        {
        }

        /// <summary>Creates a chain rooted at <paramref name="rootBone"/>.</summary>
        public FluffyChain(Transform rootBone)
        {
            _rootBone = rootBone;
        }

        /// <summary>First bone of the chain.</summary>
        public Transform RootBone
        {
            get => _rootBone;
            set => _rootBone = value;
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

            if (_rootBone == null)
            {
                return false;
            }

            List<Transform> bones = CollectChain(_rootBone);
            if (bones.Count < 2)
            {
                Debug.LogWarning($"[Fluffy Bones] Chain '{_rootBone.name}' needs at least two bones — " +
                                 "the root and one child. Nothing to simulate.", context);
                return false;
            }

            for (int i = 0; i < bones.Count; i++)
            {
                Transform bone = bones[i];
                bool isLeaf = i == bones.Count - 1;
                Vector3 tip = isLeaf
                    ? bone.position + (bone.position - bones[i - 1].position).normalized * _tipLength
                    : bones[i + 1].position;

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
                    RestLocalRotation = bone.localRotation,
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

                float stiffness = profile != null
                    ? profile.EvaluateStiffness(joint.NormalizedDepth)
                    : DefaultStiffness;

                Vector3 inertia = (joint.CurrentTip - joint.PreviousTip) * inertiaRetained;
                Vector3 pullToRest = restDirection * (stiffness * joint.Length * deltaTime);

                Vector3 nextTip = joint.CurrentTip + inertia + pullToRest + gravityStep;

                // The bone is rigid: the tip may swing anywhere, but only on the sphere
                // of its own rest length around the bone's head.
                Vector3 offset = nextTip - position;
                float distance = offset.magnitude;
                nextTip = distance < MinBoneLength
                    ? position + restDirection * joint.Length
                    : position + offset * (joint.Length / distance);

                joint.PreviousTip = joint.CurrentTip;
                joint.CurrentTip = nextTip;

                bone.rotation = Quaternion.FromToRotation(restDirection, nextTip - position) * restRotation;
            }
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

            if (_rootBone == null)
            {
                return;
            }

            // Not playing: preview the bones the solver would pick up.
            List<Transform> bones = CollectChain(_rootBone);
            for (int i = 0; i < bones.Count - 1; i++)
            {
                Gizmos.DrawLine(bones[i].position, bones[i + 1].position);
                Gizmos.DrawWireSphere(bones[i].position, 0.01f);
            }
        }

        /// <summary>
        /// Follows the first child of each bone down from <paramref name="root"/>, which
        /// is how a tail, a hair strand or a skirt panel is normally rigged.
        /// </summary>
        public static List<Transform> CollectChain(Transform root)
        {
            var bones = new List<Transform>();
            Transform current = root;

            while (current != null)
            {
                bones.Add(current);
                current = current.childCount > 0 ? current.GetChild(0) : null;
            }

            return bones;
        }

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
