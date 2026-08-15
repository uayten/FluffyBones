using System.Collections.Generic;
using UnityEngine;

namespace FluffyBones
{
    /// <summary>
    /// A single chain of bones driven by the Fluffy Bones solver — a tail, one
    /// strand of a skirt, a lock of hair, a length of chain.
    /// </summary>
    /// <remarks>
    /// Placed on the first bone of the chain; the remaining bones are taken from
    /// the transform hierarchy below it, following the first child of each bone.
    /// The solver runs in <c>LateUpdate</c>, after the Animator has written the
    /// animated pose, and rotates each bone so its tip lags behind that pose.
    /// </remarks>
    [DisallowMultipleComponent]
    [AddComponentMenu("Fluffy Bones/Fluffy Chain")]
    public class FluffyChain : MonoBehaviour
    {
        private const float DefaultStiffness = 8f;
        private const float DefaultDrag = 0.15f;
        private const float MinBoneLength = 1e-5f;

        [Header("Chain")]
        [Tooltip("First bone of the chain. Defaults to this transform.")]
        [SerializeField] private Transform _root;

        [Tooltip("Virtual bone length past the last real bone, in world units, so the " +
                 "tip swings too. 0 leaves the last bone rigid.")]
        [Min(0f)]
        [SerializeField] private float _tipLength = 0.05f;

        [Header("Simulation")]
        [Tooltip("Tuning values. With none assigned the chain falls back to its defaults.")]
        [SerializeField] private FluffyProfile _profile;

        [Tooltip("How far the root may move in a single frame before the chain is " +
                 "snapped back to its rest pose instead of swinging. Keeps teleports " +
                 "from launching the chain across the level.")]
        [Min(0f)]
        [SerializeField] private float _teleportThreshold = 1f;

        private readonly List<Joint> _joints = new List<Joint>();
        private Vector3 _lastRootPosition;
        private bool _isBuilt;

        /// <summary>Bones the chain is currently simulating, root first.</summary>
        public int JointCount => _joints.Count;

        /// <summary>Tuning values in use. Assigning rebuilds nothing — it takes effect next frame.</summary>
        public FluffyProfile Profile
        {
            get => _profile;
            set => _profile = value;
        }

        private void Reset()
        {
            _root = transform;
        }

        private void Awake()
        {
            Rebuild();
        }

        private void OnEnable()
        {
            ResetToRestPose();
        }

        private void LateUpdate()
        {
            if (!_isBuilt)
            {
                return;
            }

            float deltaTime = Time.deltaTime;
            if (deltaTime <= 0f)
            {
                return;
            }

            Vector3 rootPosition = _root.position;
            bool teleported = _teleportThreshold > 0f
                              && (rootPosition - _lastRootPosition).sqrMagnitude > _teleportThreshold * _teleportThreshold;

            if (teleported)
            {
                ResetToRestPose();
                return;
            }

            _lastRootPosition = rootPosition;
            Simulate(deltaTime);
        }

        /// <summary>
        /// Walks the hierarchy below the root, caches the rest pose and prepares the
        /// simulation state. Call after changing the bone hierarchy at runtime.
        /// </summary>
        public void Rebuild()
        {
            _isBuilt = false;
            _joints.Clear();

            if (_root == null)
            {
                _root = transform;
            }

            List<Transform> bones = CollectChain(_root);
            if (bones.Count < 2)
            {
                Debug.LogWarning($"[Fluffy Bones] '{name}' needs at least two bones in the chain — " +
                                 "the root and one child. Nothing to simulate.", this);
                return;
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
            _lastRootPosition = _root.position;
        }

        /// <summary>
        /// Snaps every bone back to the pose it was built in and clears the accumulated
        /// motion, so the next frame starts from rest instead of catching up.
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

            _lastRootPosition = _root.position;
        }

        private void Simulate(float deltaTime)
        {
            float drag = _profile != null ? _profile.Drag : DefaultDrag;
            Vector3 gravityStep = (_profile != null ? _profile.Gravity : Vector3.zero) * (deltaTime * deltaTime);
            float inertiaRetained = 1f - drag;

            // Root first: rotating a bone moves every bone below it, so each joint has
            // to read a position its parent has already settled this frame.
            for (int i = 0; i < _joints.Count; i++)
            {
                Joint joint = _joints[i];
                Transform bone = joint.Transform;

                Vector3 position = bone.position;
                Quaternion parentRotation = bone.parent != null ? bone.parent.rotation : Quaternion.identity;
                Quaternion restRotation = parentRotation * joint.RestLocalRotation;
                Vector3 restDirection = restRotation * joint.BoneAxis;

                float stiffness = _profile != null
                    ? _profile.EvaluateStiffness(joint.NormalizedDepth)
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

        /// <summary>
        /// Follows the first child of each bone down from <paramref name="root"/>,
        /// which is how a tail, a hair strand or a skirt panel is normally rigged.
        /// </summary>
        private static List<Transform> CollectChain(Transform root)
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

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1f, 0.55f, 0.8f);

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

            // Not playing: preview the chain the solver would pick up.
            List<Transform> bones = CollectChain(_root != null ? _root : transform);
            for (int i = 0; i < bones.Count - 1; i++)
            {
                Gizmos.DrawLine(bones[i].position, bones[i + 1].position);
                Gizmos.DrawWireSphere(bones[i].position, 0.01f);
            }
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

            /// <summary>World-space tip position this frame.</summary>
            public Vector3 CurrentTip;

            /// <summary>World-space tip position last frame — the velocity comes from the difference.</summary>
            public Vector3 PreviousTip;
        }

        // TODO: collision against FluffyCollider.
        // TODO: angle limits, so a skirt cannot fold through the leg.
        // TODO: hand the update loop over to FluffyBody, for a single ordered pass
        //       per character.
    }
}
