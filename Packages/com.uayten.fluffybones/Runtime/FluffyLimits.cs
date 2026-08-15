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

        /// <summary>
        /// Holds a range to one a bone can actually sit in: the minimum at or below the
        /// pose, the maximum at or above it, both inside -180 to 180.
        /// </summary>
        /// <remarks>
        /// A range is measured from the bone's pose, so 0 is where it rests. A minimum
        /// above 0 would ask the solver to hold the bone off its own pose, which the
        /// spring pulls it straight back to, and the rim drawn in the scene reads each
        /// side as a distance from 0, so a positive minimum turns it inside out. Keeping
        /// 0 inside is also what stops the two ends crossing.
        /// </remarks>
        public static Vector2 ClampRange(Vector2 range)
        {
            return new Vector2(Mathf.Clamp(range.x, -Open, 0f), Mathf.Clamp(range.y, 0f, Open));
        }

        /// <summary>Every axis held to a range the bone can sit in.</summary>
        public FluffyLimits Clamped => new FluffyLimits
        {
            Twist = ClampRange(Twist),
            SwingY = ClampRange(SwingY),
            SwingZ = ClampRange(SwingZ)
        };
    }
}
