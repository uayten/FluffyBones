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

        [Tooltip("How far the character may move in a single frame before the chains are " +
                 "snapped back to their rest pose instead of swinging. Keeps teleports " +
                 "from launching them across the level.")]
        [Min(0f)]
        [SerializeField] private float _teleportThreshold = 1f;

        [Tooltip("Draw the chains' bones in the scene view, so they can be seen and " +
                 "posed without a separate bone renderer.")]
        [SerializeField] private bool _showBones = true;

        [Tooltip("Colour of the drawn bones.")]
        [SerializeField] private Color _boneColor = new Color(1f, 0.55f, 0.8f);

        [Tooltip("Draw each bone's local axes — X red, Y green, Z blue. Which way they " +
                 "point decides how the bone swings.")]
        [SerializeField] private bool _showAxes;

        private Vector3 _lastPosition;

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

            Vector3 position = transform.position;
            bool teleported = _teleportThreshold > 0f
                              && (position - _lastPosition).sqrMagnitude > _teleportThreshold * _teleportThreshold;

            _lastPosition = position;

            if (teleported)
            {
                ResetToRestPose();
                return;
            }

            for (int i = 0; i < _chains.Count; i++)
            {
                _chains[i].Simulate(deltaTime, _profile);
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

            _lastPosition = transform.position;
        }

        /// <summary>Snaps every chain back to its rest pose and clears the accumulated motion.</summary>
        public void ResetToRestPose()
        {
            for (int i = 0; i < _chains.Count; i++)
            {
                _chains[i].ResetToRestPose();
            }

            _lastPosition = transform.position;
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
            Gizmos.color = _boneColor;

            for (int i = 0; i < _chains.Count; i++)
            {
                if (_showBones)
                {
                    _chains[i].DrawBoneGizmos();
                }

                if (_showAxes)
                {
                    _chains[i].DrawAxisGizmos();
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

        // TODO: collect FluffyColliders under the character and solve every chain
        //       against them in the same pass.
        // TODO: skip the update when the character is off screen or far from the camera.
    }
}
