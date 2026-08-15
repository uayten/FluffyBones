using System;
using UnityEngine;

namespace Fluffy
{
    /// <summary>
    /// What one bone of a chain is set to: the rotation it rests at, and optionally
    /// limits of its own.
    /// </summary>
    /// <remarks>
    /// Most bones of a chain want the same limits, so they take the chain's global
    /// ones and only the odd bone that needs to differ carries its own. Rotation and
    /// limits are kept in one struct rather than parallel arrays, so trimming a pose
    /// cannot leave a rotation matched with another bone's limits.
    /// </remarks>
    [Serializable]
    public struct FluffyBonePose
    {
        [Tooltip("Local rotation the bone rests at, as euler angles.")]
        public Vector3 Rotation;

        [Tooltip("Use limits set on this bone instead of the chain's global ones.")]
        public bool OverrideLimits;

        [Tooltip("This bone's own limits, used only while Override Limits is on.")]
        public FluffyLimits Limits;

        /// <summary>A bone at <paramref name="rotation"/>, taking the global limits.</summary>
        public FluffyBonePose(Vector3 rotation)
        {
            Rotation = rotation;
            OverrideLimits = false;
            Limits = FluffyLimits.Free;
        }

        /// <summary>The limits that apply, given the chain's global ones.</summary>
        public FluffyLimits Resolve(FluffyLimits global)
        {
            return OverrideLimits ? Limits : global;
        }
    }
}
