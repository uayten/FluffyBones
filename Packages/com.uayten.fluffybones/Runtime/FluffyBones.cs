using System;
using System.Collections.Generic;
using UnityEngine;

namespace Fluffy
{
    /// <summary>How many bone chains a character drives.</summary>
    public enum FluffyChainMode
    {
        /// <summary>One chain: a tail, a ponytail, a single cable.</summary>
        Single,

        /// <summary>Several chains sharing one setup: a skirt, a cape, a head of hair.</summary>
        Multiple
    }

    /// <summary>
    /// The Fluffy Bones component. Goes on the character, holds its bone chains
    /// and steps them together, so the whole character is solved in one ordered
    /// pass after the animation has been applied.
    /// </summary>
    /// <remarks>
    /// Pick <see cref="FluffyChainMode.Single"/> for one tail or ponytail, or
    /// <see cref="FluffyChainMode.Multiple"/> for a skirt, where a dozen strands
    /// hang off the same hips and share one profile.
    /// </remarks>
    [DisallowMultipleComponent]
    [AddComponentMenu("Fluffy Bones/Fluffy Bones")]
    public class FluffyBones : MonoBehaviour
    {
        /// <summary>
        /// The most steps one frame may run. A frame that has fallen further behind than
        /// this gives up the rest rather than trying to catch up, which is what stops a
        /// slow machine from spiralling: every step it adds makes the next frame longer.
        /// </summary>
        private const int MaxStepsPerFrame = 8;


        [Tooltip("Single: one chain, like a tail. Multiple: many chains sharing one " +
                 "profile, like the strands of a skirt.")]
        [SerializeField] private FluffyChainMode _mode = FluffyChainMode.Single;

        [Tooltip("The behaviour asset. Reusable across chains and characters — one " +
                 "profile can drive every skirt in the game.")]
        [SerializeField] private FluffyProfile _profile;

        [Tooltip("Bone chains this character simulates.")]
        [SerializeField] private List<FluffyChain> _chains = new List<FluffyChain>();

        [Tooltip("Bone names searched by Detect chains. Case is ignored, and a match " +
                 "anywhere in the name counts.")]
        [SerializeField] private string[] _detectionKeywords =
        {
            "tail", "skirt", "hair", "ponytail", "cape", "cloak", "chain", "ribbon", "ear"
        };

        [Tooltip("How far the character may move between two frames before the chains are " +
                 "carried along rigidly instead of swinging, in world units. Past this the " +
                 "solver has no sensible swing to compute, so the chains simply travel " +
                 "with the character. Around a bone's length is a good value. 0 turns it off.")]
        [Min(0f)]
        [SerializeField] private float _teleportDistance = 1f;

        [Tooltip("How many times a second the chains are solved. The solver takes steps " +
                 "of this length whatever the frame rate, and what is drawn is worked out " +
                 "between the last two — so a chain behaves the same on every machine and " +
                 "does not care that frames arrive unevenly. Higher costs more and buys " +
                 "accuracy rather than a different look — a chain hangs and swings the " +
                 "same at every rate; 60 suits most characters.")]
        [Range(20f, 240f)]
        [SerializeField] private float _simulationRate = 60f;

        [Tooltip("Draw the chains' bones in the scene view, so they can be seen and " +
                 "posed without a separate bone renderer.")]
        [SerializeField] private bool _showBones = true;

        [Tooltip("Colour of the drawn bones.")]
        [SerializeField] private Color _boneColor = new Color(1f, 0.55f, 0.8f);

        [Tooltip("Draw each bone's local axes — X red, Y green, Z blue. Which way they " +
                 "point decides how the bone swings.")]
        [SerializeField] private bool _showAxes;

        [Tooltip("Draw how far each bone may move: an arc for the Y swing, another for " +
                 "the Z, and a ring around the bone for the X twist. Each in its axis " +
                 "colour, the same ones Show Axes uses, and each spanning its own range " +
                 "— so a free axis closes into a full circle.")]
        [SerializeField] private bool _showLimits;

        [Tooltip("Size of the drawn limits, as a fraction of the bone's length. Turn it " +
                 "down when the shapes of neighbouring bones run into each other and you " +
                 "cannot tell which belongs to which.")]
        [Range(0.1f, 1.5f)]
        [SerializeField] private float _limitSize = FluffyChain.DefaultLimitSize;

        [Tooltip("Draw each chain as a tube of its own thickness. That thickness is added " +
                 "to every shape the chain meets, and it is the one setting here with no " +
                 "shape of its own in the scene to look at.")]
        [SerializeField] private bool _showThickness;

        private readonly List<FluffyCollider> _colliders = new List<FluffyCollider>();

        /// <summary>Per chain, the shapes it is pushed out of: all but the ones it carries.</summary>
        private readonly List<List<FluffyCollider>> _chainColliders = new List<List<FluffyCollider>>();

        private Vector3 _lastPosition;
        private Quaternion _lastRotation = Quaternion.identity;
        private float _accumulator;
        private bool _stepped;

#if UNITY_EDITOR
        /// <summary>
        /// The profile shipped with the package, assigned to new components so they
        /// behave sensibly before anyone touches a slider. Read-only for anyone who
        /// installed Fluffy Bones as a package — duplicate it to tune.
        /// </summary>
        private const string GenericProfilePath =
            "Packages/com.uayten.fluffybones/Runtime/Profiles/FluffyGeneric.asset";

        private void Reset()
        {
            if (_profile == null)
            {
                _profile = UnityEditor.AssetDatabase.LoadAssetAtPath<FluffyProfile>(GenericProfilePath);
            }
        }
#endif

        /// <summary>Whether this character drives one chain or several.</summary>
        public FluffyChainMode Mode
        {
            get => _mode;
            set => _mode = value;
        }

        /// <summary>Behaviour asset used by chains without an override of their own.</summary>
        public FluffyProfile Profile
        {
            get => _profile;
            set => _profile = value;
        }

        /// <summary>Chains this character simulates.</summary>
        public IReadOnlyList<FluffyChain> Chains => _chains;

        /// <summary>Collision shapes found on the character at the last build.</summary>
        public IReadOnlyList<FluffyCollider> Colliders => _colliders;

        /// <summary>How far the character may move between frames before being carried.</summary>
        public float TeleportDistance => _teleportDistance;

        /// <summary>Seconds the last simulated frame covered.</summary>
        /// <remarks>
        /// With the two below, this is what a trace needs to tell a pop from fast
        /// motion: a bone that turns a long way in a frame that was itself long has
        /// only moved at its usual speed.
        /// </remarks>
        public float LastDeltaTime { get; private set; }

        /// <summary>How many steps the last frame was split into.</summary>
        public int LastStepCount { get; private set; }

        /// <summary>Whether the last frame moved far enough to carry the chains along.</summary>
        public bool CarriedLastFrame { get; private set; }

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
            float deltaTime = Time.deltaTime;
            if (deltaTime <= 0f)
            {
                return;
            }

            transform.GetPositionAndRotation(out Vector3 position, out Quaternion rotation);

            // A distance, and deliberately not a speed: what the solver cannot swing
            // through is how far the bones' heads moved between two of its samples, and
            // that is the same whether the character warped or the frame simply took a
            // third of a second. Either way the chains are carried along rather than
            // reset, so a frame that trips this by accident costs nothing to look at.
            bool jumped = _teleportDistance > 0f
                          && (position - _lastPosition).sqrMagnitude > _teleportDistance * _teleportDistance;

            if (jumped)
            {
                for (int i = 0; i < _chains.Count; i++)
                {
                    _chains[i].Carry(_lastPosition, _lastRotation, position, rotation);
                }
            }

            _lastPosition = position;
            _lastRotation = rotation;
            CarriedLastFrame = jumped;

            // Every step the same length, however long the frame was. Handed the frame
            // itself, the solver behaved differently on every machine and every stutter:
            // the spring is proportional to the time given, the damping compounds once a
            // step, and the stored motion means a different speed when replayed over a
            // different length. A fixed step removes all three at once, and the leftover
            // time is carried to the next frame rather than thrown away.
            float step = 1f / Mathf.Max(1f, _simulationRate);
            _accumulator += deltaTime;

            // Put the chains back where the physics actually left them before stepping
            // again: what the bones are carrying at this point is the drawn pose, which
            // sits between two steps, and feeding that back in would let the drawing
            // shift the simulation.
            if (_stepped)
            {
                for (int i = 0; i < _chains.Count; i++)
                {
                    _chains[i].ApplyPose(1f);
                }
            }

            int steps = 0;
            while (_accumulator >= step && steps < MaxStepsPerFrame)
            {
                for (int i = 0; i < _chains.Count; i++)
                {
                    _chains[i].Simulate(step, _profile, CollidersAgainst(i));
                }

                _accumulator -= step;
                steps++;
            }

            if (steps >= MaxStepsPerFrame)
            {
                // Too far behind to catch up. Dropping the debt keeps a slow frame from
                // making the next one slower still.
                _accumulator = 0f;
            }

            _stepped |= steps > 0;

            LastDeltaTime = deltaTime;
            LastStepCount = steps;

            // What is drawn is where the chain was partway through the step being
            // rendered, not where the last completed step left it.
            if (_stepped)
            {
                float alpha = Mathf.Clamp01(_accumulator / step);

                for (int i = 0; i < _chains.Count; i++)
                {
                    _chains[i].ApplyPose(alpha);
                }
            }
        }

        /// <summary>
        /// Rebuilds every chain from the current bone hierarchy. Call after changing
        /// the chain list or the skeleton at runtime.
        /// </summary>
        public void Rebuild()
        {
            for (int i = 0; i < _chains.Count; i++)
            {
                _chains[i].Build(this);
            }

            CollectColliders();

            transform.GetPositionAndRotation(out _lastPosition, out _lastRotation);
        }

        /// <summary>
        /// Finds the collision shapes on the character, once.
        /// </summary>
        /// <remarks>
        /// At build rather than per frame: a search of the hierarchy per chain per step
        /// is the kind of cost that does not show up on the character being tested and
        /// does show up on a crowd of them. A shape added at runtime needs a
        /// <see cref="Rebuild"/>, which is the same rule the chains themselves follow.
        ///
        /// Inactive ones are collected too, since a shape switched on for one animation
        /// is still the character's — the solver skips whatever is off when it reads
        /// them.
        /// </remarks>
        public void CollectColliders()
        {
            _colliders.Clear();
            GetComponentsInChildren(true, _colliders);

            SortCollidersByChain();

            // A shape put on a bone is also how thick that bone is, so the chains have to
            // find the one they are now carrying.
            for (int i = 0; i < _chains.Count; i++)
            {
                _chains[i].RefreshShapes();
            }
        }

        /// <summary>
        /// Works out, once, which shapes each chain is solved against.
        /// </summary>
        /// <remarks>
        /// Two kinds of shape live on a character and the difference is only where they
        /// are parented. One sits on the body — a capsule on a thigh, a plane on the
        /// spine — and blocks everything. The other rides on a chain the plugin is
        /// moving, so that a cape has a body the skirt cannot walk through, and blocks
        /// everything except the chain carrying it.
        ///
        /// Paired here rather than tested in the solver because the answer only changes
        /// when the hierarchy does: a chain against a dozen shapes, every step, for a
        /// question whose answer was already known at build.
        /// </remarks>
        private void SortCollidersByChain()
        {
            while (_chainColliders.Count < _chains.Count)
            {
                _chainColliders.Add(new List<FluffyCollider>());
            }

            for (int i = 0; i < _chains.Count; i++)
            {
                List<FluffyCollider> against = _chainColliders[i];
                against.Clear();

                for (int c = 0; c < _colliders.Count; c++)
                {
                    if (!_chains[i].Owns(_colliders[c]))
                    {
                        against.Add(_colliders[c]);
                    }
                }
            }
        }

        /// <summary>The shapes chain <paramref name="index"/> is pushed out of.</summary>
        /// <remarks>
        /// Falls back to all of them for a chain added since the last build, which is the
        /// same answer this gave before shapes could ride on a chain, and is corrected by
        /// the <see cref="Rebuild"/> that chain needs anyway.
        /// </remarks>
        private IReadOnlyList<FluffyCollider> CollidersAgainst(int index)
        {
            return index < _chainColliders.Count ? _chainColliders[index] : _colliders;
        }

        /// <summary>Snaps every chain back to its rest pose and clears the accumulated motion.</summary>
        public void ResetToRestPose()
        {
            for (int i = 0; i < _chains.Count; i++)
            {
                _chains[i].ResetToRestPose();
            }

            // Nothing to interpolate between any more, and no time owed.
            _accumulator = 0f;
            _stepped = false;

            transform.GetPositionAndRotation(out _lastPosition, out _lastRotation);
        }

        /// <summary>
        /// Records the bones' current rotations as the pose every chain rests at.
        /// Pose the character in the scene first — bend the tail into its curve — and
        /// the chains will spring back to that instead of to the imported pose.
        /// </summary>
        /// <returns>How many chains captured a pose.</returns>
        public int CaptureDefaultPose()
        {
            int captured = 0;

            for (int i = 0; i < _chains.Count; i++)
            {
                if (_chains[i].CaptureDefaultPose())
                {
                    captured++;
                }
            }

            return captured;
        }

        /// <summary>
        /// Copies one chain's pose and dummy bone settings onto every other chain, which
        /// is how a skirt gets set up once rather than eight times.
        /// </summary>
        /// <param name="sourceIndex">The chain to copy from.</param>
        /// <returns>How many chains were changed.</returns>
        public int CopySettingsToAllChains(int sourceIndex)
        {
            if (sourceIndex < 0 || sourceIndex >= _chains.Count)
            {
                return 0;
            }

            FluffyChain source = _chains[sourceIndex];
            int changed = 0;

            for (int i = 0; i < _chains.Count; i++)
            {
                if (i == sourceIndex)
                {
                    continue;
                }

                source.CopySettingsTo(_chains[i]);
                changed++;
            }

            return changed;
        }

        /// <summary>Forgets the authored pose on every chain.</summary>
        public void ClearDefaultPose()
        {
            for (int i = 0; i < _chains.Count; i++)
            {
                _chains[i].ClearDefaultPose();
            }
        }

        /// <summary>Whether any chain carries an authored default pose.</summary>
        public bool HasDefaultPose
        {
            get
            {
                for (int i = 0; i < _chains.Count; i++)
                {
                    if (_chains[i].HasDefaultPose)
                    {
                        return true;
                    }
                }

                return false;
            }
        }

        /// <summary>Puts every chain's bones back into its authored pose.</summary>
        public void ApplyDefaultPose()
        {
            for (int i = 0; i < _chains.Count; i++)
            {
                _chains[i].ApplyDefaultPose();
            }
        }

        /// <summary>Every bone covered by a chain, for undo recording before a pose change.</summary>
        public List<Transform> CollectBones()
        {
            var bones = new List<Transform>();

            for (int i = 0; i < _chains.Count; i++)
            {
                List<Transform> chainBones = _chains[i].GetBones();
                if (chainBones != null)
                {
                    bones.AddRange(chainBones);
                }
            }

            return bones;
        }

        /// <summary>
        /// Scans the skeleton for bones whose name matches one of the detection
        /// keywords and adds a chain for each one found, skipping bones already
        /// covered by an existing chain.
        /// </summary>
        /// <returns>How many chains were added.</returns>
        public int DetectChains()
        {
            int added = 0;

            foreach (Transform bone in GetComponentsInChildren<Transform>(true))
            {
                if (bone == transform || bone.childCount == 0 || !MatchesKeyword(bone.name))
                {
                    continue;
                }

                // A bone whose parent also matches is in the middle of a chain, not at
                // its root — tail_02 under tail_01 should not start a second chain.
                if (bone.parent != null && MatchesKeyword(bone.parent.name))
                {
                    continue;
                }

                if (HasChainRootedAt(bone))
                {
                    continue;
                }

                _chains.Add(new FluffyChain(bone));
                added++;
            }

            return added;
        }

        private bool HasChainRootedAt(Transform bone)
        {
            for (int i = 0; i < _chains.Count; i++)
            {
                if (_chains[i].StartBone == bone)
                {
                    return true;
                }
            }

            return false;
        }

        private bool MatchesKeyword(string boneName)
        {
            if (_detectionKeywords == null || string.IsNullOrEmpty(boneName))
            {
                return false;
            }

            for (int i = 0; i < _detectionKeywords.Length; i++)
            {
                string keyword = _detectionKeywords[i];
                if (!string.IsNullOrEmpty(keyword)
                    && boneName.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return true;
                }
            }

            return false;
        }

        private void OnDrawGizmos()
        {
            for (int i = 0; i < _chains.Count; i++)
            {
                Gizmos.color = _boneColor;

                if (_showBones)
                {
                    _chains[i].DrawBoneGizmos();
                }

                if (_showAxes)
                {
                    _chains[i].DrawAxisGizmos();
                }

                if (_showThickness)
                {
                    // Faded, because it is a width around the bones rather than a thing of
                    // its own, and at full strength it swallows the bones inside it.
                    Gizmos.color = new Color(_boneColor.r, _boneColor.g, _boneColor.b, _boneColor.a * 0.4f);
                    _chains[i].DrawThicknessGizmos();
                }

                if (_showLimits)
                {
                    // No colour set here: every limit shape carries its own axis colour.
                    _chains[i].DrawLimitGizmos(_limitSize);
                }
            }
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = _boneColor;

            for (int i = 0; i < _chains.Count; i++)
            {
                _chains[i].DrawGizmos();
            }
        }

        // TODO: skip the update when the character is off screen or far from the camera.
    }
}
