using UnityEngine;

namespace Fluffy
{
    /// <summary>
    /// A saved chain setup: the local offset from each chain's own rest rotation,
    /// and how far each bone may swing from there.
    /// </summary>
    /// <remarks>
    /// Rotation offsets are local to each bone's own rest orientation, so one pose
    /// fits chains placed around a character at different rotations — the eight
    /// strands of a skirt share a single asset and are posed once. A pose
    /// longer than the chain is not a problem: the bones that exist take the entries
    /// in order and the rest are ignored, so one file can serve a ten-bone tail and
    /// a three-bone one. What a profile is to how a chain feels, a pose is to the
    /// shape it holds.
    /// </remarks>
    [CreateAssetMenu(fileName = "FluffyPose", menuName = "Fluffy Bones/Pose")]
    public class FluffyPose : ScriptableObject
    {
        [Tooltip("Limits every bone takes unless it overrides them.")]
        [SerializeField] private FluffyLimits _globalLimits = FluffyLimits.Free;

        [Tooltip("One entry per bone, in order from the start of the chain.")]
        [SerializeField] private FluffyBonePose[] _bones;

        [SerializeField, HideInInspector] private bool _usesLocalRotationOffsets;

        /// <summary>Limits every bone takes unless it overrides them.</summary>
        public FluffyLimits GlobalLimits => _globalLimits;

        /// <summary>One entry per bone, from the start of the chain.</summary>
        public FluffyBonePose[] Bones => _bones;

        /// <summary>How many bones this pose covers.</summary>
        public int BoneCount => _bones?.Length ?? 0;

        /// <summary>Whether rotations are offsets from each chain's own rest pose.</summary>
        public bool UsesLocalRotationOffsets => _usesLocalRotationOffsets;

        /// <summary>Replaces the stored entries.</summary>
        public void SetBones(FluffyBonePose[] bones)
        {
            _bones = bones;
        }

        /// <summary>Marks this asset as using chain-relative local rotation offsets.</summary>
        public void UseLocalRotationOffsets()
        {
            _usesLocalRotationOffsets = true;
        }
    }
}
