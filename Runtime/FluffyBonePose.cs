using System;
using UnityEngine;

namespace Fluffy
{
    /// <summary>
    /// What one bone of a chain is set to: the rotation it rests at, whether the
    /// solver touches it at all, and optionally limits of its own.
    /// </summary>
    /// <remarks>
    /// Most bones of a chain want the same limits, so they take the chain's global
    /// ones and only the odd bone that needs to differ carries its own. Rotation and
    /// limits are kept in one struct rather than parallel arrays, so trimming a pose
    /// cannot leave a rotation matched with another bone's limits.
    ///
    /// <see cref="Skip"/> rides here for the same reason the limits do: it is per bone,
    /// and a shared pose asset should turn the same bone off on all eight strands of a
    /// skirt at once rather than eight times.
    /// </remarks>
    [Serializable]
    public struct FluffyBonePose
    {
        [Tooltip("Local rotation the bone rests at, as euler angles.")]
        public Vector3 Rotation;

        [Tooltip("Leave this bone to whatever is animating it instead of simulating it. " +
                 "The bones below it still swing, from wherever it puts them.")]
        public bool Skip;

        [Tooltip("Use limits set on this bone instead of the chain's global ones.")]
        public bool OverrideLimits;

        [Tooltip("This bone's own limits, used only while Override Limits is on.")]
        public FluffyLimits Limits;

        /// <summary>A bone at <paramref name="rotation"/>, simulated, taking the global limits.</summary>
        public FluffyBonePose(Vector3 rotation)
        {
            Rotation = rotation;
            Skip = false;
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
