using UnityEngine;

namespace Fluffy
{
    /// <summary>
    /// One bone as the solver sees it, for a given frame.
    /// </summary>
    /// <remarks>
    /// Read out rather than reconstructed: the angles here are measured in the same
    /// frame the solver clamps in, so a trace can be compared against the limits in
    /// the inspector without translating between two ideas of "the bone's Y".
    /// </remarks>
    public struct FluffyBoneState
    {
        /// <summary>The bone itself.</summary>
        public Transform Bone;

        /// <summary>Which bone of the chain it is, counting from the start bone.</summary>
        public int Index;

        /// <summary>Where the bone's head is, in world space.</summary>
        public Vector3 Head;

        /// <summary>Where the bone points now, in world space.</summary>
        public Vector3 Direction;

        /// <summary>Where the bone would point with only the animation on it.</summary>
        public Vector3 RestDirection;

        /// <summary>How far it has swung towards its own Y, in degrees.</summary>
        public float SwingY;

        /// <summary>How far it has swung towards its own Z, in degrees.</summary>
        public float SwingZ;

        /// <summary>How far it has rolled about its own axis, in degrees.</summary>
        public float Twist;

        /// <summary>What it is allowed to do, as the solver resolved it this frame.</summary>
        public FluffyLimits Limits;

        /// <summary>Rest length from the bone's head to its tip.</summary>
        public float Length;

        /// <summary>Whether the Y swing is sitting on one end of its range.</summary>
        public bool AtSwingYLimit;

        /// <summary>Whether the Z swing is sitting on one end of its range.</summary>
        public bool AtSwingZLimit;

        /// <summary>How far the bone is from where its pose puts it, in degrees.</summary>
        public float OffRest => Vector3.Angle(RestDirection, Direction);
    }
}
