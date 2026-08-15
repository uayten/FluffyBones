using System;
using UnityEngine;

namespace Fluffy
{
    /// <summary>
    /// How far a bone may turn on each of its own axes: a minimum and a maximum per
    /// axis, in degrees.
    /// </summary>
    /// <remarks>
    /// Minimum and maximum are kept apart rather than collapsed into one angle
    /// because the two sides are rarely equal — a cape billows far off the back and
    /// barely moves the other way, and a symmetric limit cannot say that.
    /// </remarks>
    [Serializable]
    public struct FluffyLimits
    {
        /// <summary>A limit of 180° in both directions holds nothing back.</summary>
        public const float Open = 180f;

        [Tooltip("Twist along the bone, around its local X. Drawn as a circle.")]
        public Vector2 Twist;

        [Tooltip("Swing towards the bone's local Y. One of the two the cone opens on.")]
        public Vector2 SwingY;

        [Tooltip("Swing towards the bone's local Z. The other one.")]
        public Vector2 SwingZ;

        /// <summary>Limits that hold nothing back.</summary>
        public static FluffyLimits Free => new FluffyLimits
        {
            Twist = FreeRange,
            SwingY = FreeRange,
            SwingZ = FreeRange
        };

        /// <summary>The minimum and maximum of an axis that is not limited at all.</summary>
        public static Vector2 FreeRange => new Vector2(-Open, Open);

        /// <summary>Whether an axis range lets the bone move as far as it likes.</summary>
        public static bool IsFree(Vector2 range)
        {
            return range.x <= -Open && range.y >= Open;
        }

        /// <summary>Whether every axis is unrestricted, so nothing needs clamping or drawing.</summary>
        public bool IsUnrestricted => IsFree(Twist) && IsFree(SwingY) && IsFree(SwingZ);
    }
}
