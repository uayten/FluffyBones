using System.Collections.Generic;
using UnityEngine;

namespace FluffyBones
{
    /// <summary>
    /// The Fluffy Bones component. Goes on the character, holds its chains and
    /// steps them together, so the whole character is solved in one ordered pass.
    /// </summary>
    /// <remarks>
    /// Add it to the character root, press <em>Detect chains</em> to pick up the
    /// tails, skirts and hair by bone name, or drag the root bones in by hand.
    /// </remarks>
    [DisallowMultipleComponent]
    [AddComponentMenu("Fluffy Bones/Fluffy Body")]
    public class FluffyBody : MonoBehaviour
    {
        [Header("Chains")]
        [Tooltip("Bone chains this character simulates. Use Detect chains to fill it " +
                 "from the bone names, or add entries by hand.")]
        [SerializeField] private List<FluffyChain> _chains = new List<FluffyChain>();

        [Tooltip("Bone names searched by Detect chains. Case is ignored, and a match " +
                 "anywhere in the name counts.")]
        [SerializeField] private string[] _detectionKeywords =
        {
            "tail", "skirt", "hair", "ponytail", "cape", "cloak", "chain", "ribbon", "ear"
        };

        [Header("Simulation")]
        [Tooltip("Tuning used by every chain that has no override of its own. " +
                 "With none assigned the chains fall back to their defaults.")]
        [SerializeField] private FluffyProfile _profile;

        [Tooltip("How far the character may move in a single frame before the chains are " +
                 "snapped back to their rest pose instead of swinging. Keeps teleports " +
                 "from launching them across the level.")]
        [Min(0f)]
        [SerializeField] private float _teleportThreshold = 1f;

        private Vector3 _lastPosition;

        /// <summary>Chains this character simulates.</summary>
        public IReadOnlyList<FluffyChain> Chains => _chains;

        /// <summary>Tuning used by chains without an override.</summary>
        public FluffyProfile Profile
        {
            get => _profile;
            set => _profile = value;
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
                if (_chains[i].RootBone == bone)
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
                    && boneName.IndexOf(keyword, System.StringComparison.OrdinalIgnoreCase) >= 0)
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
