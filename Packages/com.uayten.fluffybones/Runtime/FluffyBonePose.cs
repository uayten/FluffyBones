using System;
using UnityEngine;

namespace Fluffy
{
    /// <summary>
    /// What one bone of a chain is set to: the rotation it rests at, and how far it
    /// is allowed to swing away from it.
    /// </summary>
    /// <remarks>
    /// Kept together in one struct rather than in parallel arrays, so trimming or
    /// reordering a pose cannot leave a rotation matched with another bone's limit.
    /// </remarks>
    [Serializable]
    public struct FluffyBonePose
    {
        /// <summary>An angle limit of 180° allows every rotation, so nothing is held back.</summary>
        public const float Free = 180f;

        [Tooltip("Local rotation the bone rests at, as euler angles.")]
        public Vector3 Rotation;

        [Tooltip("How far the bone may swing from its rest rotation, in degrees. " +
                 "180 lets it go anywhere, 0 pins it to the pose.")]
        [Range(0f, Free)]
        public float AngleLimit;

        /// <summary>A bone at <paramref name="rotation"/>, free to swing.</summary>
        public FluffyBonePose(Vector3 rotation)
        {
            Rotation = rotation;
            AngleLimit = Free;
        }
    }
}
