using UnityEngine;

namespace Fluffy
{
    /// <summary>
    /// The behaviour asset: a reusable set of tuning values for the Fluffy Bones
    /// solver — stiffness, damping, gravity. One profile can drive a single chain,
    /// every strand of a skirt, or every character in the game.
    /// </summary>
    [CreateAssetMenu(fileName = "FluffyProfile", menuName = "Fluffy Bones/Profile")]
    public class FluffyProfile : ScriptableObject
    {
        [Header("Spring")]
        [Tooltip("How hard the chain pulls back to its default pose, per second, " +
                 "relative to each bone's length. 0 leaves it limp.")]
        [Min(0f)]
        [SerializeField] private float _returnStrength = 8f;

        [Tooltip("Scales the return strength along the chain: 0 on the horizontal axis " +
                 "is the start, 1 is the end. Lower at the end makes it whip more.")]
        [SerializeField] private AnimationCurve _returnStrengthFalloff = AnimationCurve.Constant(0f, 1f, 1f);

        [Header("Damping")]
        [Tooltip("How much motion is bled off every frame. 0 swings forever, " +
                 "1 kills the motion instantly.")]
        [Range(0f, 1f)]
        [SerializeField] private float _drag = 0.15f;

        [Header("Forces")]
        [Tooltip("Constant world-space acceleration, in units per second squared. " +
                 "Not Physics.gravity — a light droop usually reads better than -9.81.")]
        [SerializeField] private Vector3 _gravity = new Vector3(0f, -2f, 0f);

        /// <summary>How hard the chain returns to its default pose, per second.</summary>
        public float ReturnStrength => _returnStrength;

        /// <summary>Return strength multiplier along the chain, from start (0) to end (1).</summary>
        public AnimationCurve ReturnStrengthFalloff => _returnStrengthFalloff;

        /// <summary>Fraction of the motion removed each frame, in the 0–1 range.</summary>
        public float Drag => _drag;

        /// <summary>Constant world-space acceleration applied to every bone.</summary>
        public Vector3 Gravity => _gravity;

        /// <summary>
        /// Samples <see cref="ReturnStrengthFalloff"/> safely, falling back to a flat
        /// curve when it has no keys.
        /// </summary>
        /// <param name="normalizedDepth">0 at the start of the chain, 1 at the end.</param>
        public float EvaluateReturnStrength(float normalizedDepth)
        {
            if (_returnStrengthFalloff == null || _returnStrengthFalloff.length == 0)
            {
                return _returnStrength;
            }

            return _returnStrength * _returnStrengthFalloff.Evaluate(normalizedDepth);
        }

        // TODO: angle and stretch limits.
        // TODO: collider radius per bone.
        // TODO: wind and other external forces.
    }
}
