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

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1f, 0.55f, 0.8f);

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
