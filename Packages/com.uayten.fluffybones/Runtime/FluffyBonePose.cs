using System;
using UnityEngine;

namespace Fluffy
{
    /// <summary>
    /// What one bone of a chain is set to: the rotation it rests at, and how far it
    /// may swing away from it on each of its own axes.
    /// </summary>
    /// <remarks>
    /// Limits are a min and a max per axis, not a single cone, because the two sides
    /// are rarely the same — a cape billows far off the back and barely moves the
    /// other way. Y and Z open the cone the bone swings inside; X is the twist along
    /// the bone, drawn as a circle. Kept in one struct rather than parallel arrays,
    /// so trimming a pose cannot leave a rotation matched with another bone's limits.
    /// </remarks>
    [Serializable]
    public struct FluffyBonePose
    {
        /// <summary>A limit of 180° in both directions holds nothing back.</summary>
        public const float Free = 180f;

        [Tooltip("Local rotation the bone rests at, as euler angles.")]
        public Vector3 Rotation;

        [Tooltip("Twist along the bone, around its local X. Minimum and maximum degrees.")]
        public Vector2 TwistLimit;

        [Tooltip("Swing towards the bone's local Y. Minimum and maximum degrees — this " +
                 "is one of the two the cone opens on.")]
        public Vector2 SwingYLimit;

        [Tooltip("Swing towards the bone's local Z. Minimum and maximum degrees — this " +
                 "is the other one.")]
        public Vector2 SwingZLimit;

        /// <summary>A bone at <paramref name="rotation"/>, free to swing.</summary>
        public FluffyBonePose(Vector3 rotation)
        {
            Rotation = rotation;
            TwistLimit = FreeRange;
            SwingYLimit = FreeRange;
            SwingZLimit = FreeRange;
        }

        /// <summary>The min and max of an axis that is not limited at all.</summary>
        public static Vector2 FreeRange => new Vector2(-Free, Free);

        /// <summary>Whether an axis range lets the bone move as far as it likes.</summary>
        public static bool IsFree(Vector2 range)
        {
            return range.x <= -Free && range.y >= Free;
        }
    }
}
