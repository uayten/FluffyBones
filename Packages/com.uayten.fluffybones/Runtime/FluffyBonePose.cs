using System;
using UnityEngine;

namespace Fluffy
{
    /// <summary>
    /// What one bone of a chain is set to: the rotation it rests at, and how far it
    /// is allowed to swing away from it in each direction.
    /// </summary>
    /// <remarks>
    /// Kept together in one struct rather than in parallel arrays, so trimming or
    /// reordering a pose cannot leave a rotation matched with another bone's limits.
    /// </remarks>
    [Serializable]
    public struct FluffyBonePose
    {
        /// <summary>An angle limit of 180° allows every rotation, so nothing is held back.</summary>
        public const float Free = 180f;

        [Tooltip("Local rotation the bone rests at, as euler angles.")]
        public Vector3 Rotation;

        [Tooltip("How far the bone may swing towards the character's front, in degrees. " +
                 "This is the side it goes when the character walks backwards.")]
        [Range(0f, Free)]
        public float ForwardLimit;

        [Tooltip("How far the bone may swing towards the character's back, in degrees. " +
                 "This is the side it goes when the character walks forwards.")]
        [Range(0f, Free)]
        public float BackwardLimit;

        /// <summary>A bone at <paramref name="rotation"/>, free to swing either way.</summary>
        public FluffyBonePose(Vector3 rotation)
        {
            Rotation = rotation;
            ForwardLimit = Free;
            BackwardLimit = Free;
        }
    }
}
