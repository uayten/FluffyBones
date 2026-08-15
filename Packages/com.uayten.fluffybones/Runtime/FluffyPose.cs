using UnityEngine;

namespace Fluffy
{
    /// <summary>
    /// A saved default pose: the local rotation each bone of a chain rests at.
    /// </summary>
    /// <remarks>
    /// Rotations are local, so one pose fits every chain with the same number of
    /// bones — the eight strands of a skirt share a single asset and are posed
    /// once. What a profile is to how a chain feels, a pose is to the shape it
    /// holds.
    /// </remarks>
    [CreateAssetMenu(fileName = "FluffyPose", menuName = "Fluffy Bones/Pose")]
    public class FluffyPose : ScriptableObject
    {
        [Tooltip("Local rotation of each bone, in order from the start of the chain, " +
                 "as euler angles.")]
        [SerializeField] private Vector3[] _rotations;

        /// <summary>Local rotation of each bone, from the start of the chain.</summary>
        public Vector3[] Rotations => _rotations;

        /// <summary>How many bones this pose covers.</summary>
        public int BoneCount => _rotations?.Length ?? 0;

        /// <summary>Replaces the stored rotations.</summary>
        public void SetRotations(Vector3[] rotations)
        {
            _rotations = rotations;
        }
    }
}
