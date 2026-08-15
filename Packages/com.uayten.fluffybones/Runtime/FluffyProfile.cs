using UnityEngine;

namespace FluffyBones
{
    /// <summary>
    /// Reusable set of tuning values for the Fluffy Bones solver — stiffness,
    /// damping, gravity — shareable between chains and characters.
    /// </summary>
    [CreateAssetMenu(fileName = "FluffyProfile", menuName = "Fluffy Bones/Profile")]
    public class FluffyProfile : ScriptableObject
    {
        [Header("Spring")]
        [Tooltip("How hard the chain pulls back to its animated pose, per second, " +
                 "relative to each bone's length. 0 leaves it limp.")]
        [Min(0f)]
        [SerializeField] private float _stiffness = 8f;

        [Tooltip("Scales stiffness along the chain: 0 on the horizontal axis is the " +
                 "root, 1 is the tip. Lower values at the tip make it whip more.")]
        [SerializeField] private AnimationCurve _stiffnessFalloff = AnimationCurve.Constant(0f, 1f, 1f);

        [Header("Damping")]
        [Tooltip("How much motion is bled off every frame. 0 swings forever, " +
                 "1 kills the motion instantly.")]
        [Range(0f, 1f)]
        [SerializeField] private float _drag = 0.15f;

        [Header("Forces")]
        [Tooltip("Constant world-space acceleration, in units per second squared. " +
                 "Not Physics.gravity — a light droop usually reads better than -9.81.")]
        [SerializeField] private Vector3 _gravity = new Vector3(0f, -2f, 0f);

        /// <summary>How hard the chain returns to its animated pose, per second.</summary>
        public float Stiffness => _stiffness;

        /// <summary>Stiffness multiplier along the chain, sampled from root (0) to tip (1).</summary>
        public AnimationCurve StiffnessFalloff => _stiffnessFalloff;

        /// <summary>Fraction of the motion removed each frame, in the 0–1 range.</summary>
        public float Drag => _drag;

        /// <summary>Constant world-space acceleration applied to every bone.</summary>
        public Vector3 Gravity => _gravity;

        /// <summary>
        /// Samples <see cref="StiffnessFalloff"/> safely, falling back to 1 when the
        /// curve has no keys.
        /// </summary>
        /// <param name="normalizedDepth">0 at the root of the chain, 1 at the tip.</param>
        public float EvaluateStiffness(float normalizedDepth)
        {
            if (_stiffnessFalloff == null || _stiffnessFalloff.length == 0)
            {
                return _stiffness;
            }

            return _stiffness * _stiffnessFalloff.Evaluate(normalizedDepth);
        }

        // TODO: angle and stretch limits.
        // TODO: collider radius per bone.
        // TODO: wind and other external forces.
    }
}
