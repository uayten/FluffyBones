using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Fluffy.Tests
{
    /// <summary>
    /// Whether the limits hold on the pose that is actually on screen.
    /// </summary>
    /// <remarks>
    /// The solver clamps every step, and the edit-mode tests prove it. What is drawn is
    /// not a solved step, though: it is worked out between the last two, and at a frame
    /// rate well above the simulation rate most frames contain no step at all. Whatever
    /// happens to the character in those frames happens to a chain nobody is clamping.
    ///
    /// Found in a recorded trace of the playground before it was found here: a bone with
    /// a twelve degree limit sitting at 22.1, in a frame whose step count was zero.
    /// </remarks>
    public class FluffyDrawnPoseLimitsTests
    {
        private const float Allowed = 12f;

        private readonly FluffyRuntimeRig _rig = new FluffyRuntimeRig();

        [SetUp]
        public void RememberTheClock()
        {
            _rig.RememberTheClock();
        }

        [TearDown]
        public void PutTheClockBackAndTidyUp()
        {
            _rig.PutTheClockBackAndTidyUp();
        }

        /// <summary>
        /// A character walking at a frame rate far above the simulation rate never shows
        /// a bone outside the cone it is allowed to swing through.
        /// </summary>
        /// <remarks>
        /// Three hundred frames a second against sixty steps: four frames out of every
        /// five draw without a step behind them, which is the ordinary case on a machine
        /// that is not struggling. The character has to be moving for the fault to show,
        /// because it is the head moving away from a tip left behind by the last step
        /// that bends the drawn bone past its limit.
        /// </remarks>
        [UnityTest]
        public IEnumerator AWalkingCharacterNeverDrawsABoneOutsideItsLimits()
        {
            FluffyBones body = _rig.BuildCharacter();
            HoldEveryBoneTo(body, Allowed);

            var states = new List<FluffyBoneState>();
            float widest = 0f;
            int framesWithoutAStep = 0;

            Time.captureDeltaTime = 1f / 300f;
            yield return null;

            for (int frame = 0; frame < 300; frame++)
            {
                // Walking pace, and the direction changes so the chain never settles into
                // trailing straight behind.
                float t = frame / 300f;
                body.transform.position = new Vector3(Mathf.Sin(t * 12f) * 1.5f, 0f, 0f);

                yield return null;

                if (body.LastStepCount == 0)
                {
                    framesWithoutAStep++;
                }

                for (int c = 0; c < body.Chains.Count; c++)
                {
                    states.Clear();
                    body.Chains[c].CaptureState(states);

                    for (int i = 0; i < states.Count; i++)
                    {
                        widest = Mathf.Max(widest, Mathf.Abs(states[i].SwingY));
                        widest = Mathf.Max(widest, Mathf.Abs(states[i].SwingZ));
                    }
                }
            }

            Assert.That(framesWithoutAStep, Is.GreaterThan(100),
                $"Only {framesWithoutAStep} frames of 300 drew without a step behind them, so the run "
                + "does not exercise the case this test is about.");

            Assert.That(widest, Is.LessThanOrEqualTo(Allowed + 0.5f),
                $"A bone was drawn {widest:0.##} degrees from its pose against a limit of {Allowed}. "
                + "The limit holds on the solved step and not on the pose anyone can see.");
        }

        /// <summary>
        /// Puts the same limits on every bone of every chain, through a pose asset.
        /// </summary>
        /// <remarks>
        /// Per bone rather than through the chain's global limits, which have no public
        /// setter — and this reaches the same clamp, since a bone with an override is
        /// what the solver resolves to.
        /// </remarks>
        private void HoldEveryBoneTo(FluffyBones body, float degrees)
        {
            var limits = new FluffyLimits
            {
                Twist = FluffyLimits.FreeRange,
                SwingY = new Vector2(-degrees, degrees),
                SwingZ = new Vector2(-degrees, degrees)
            };

            for (int c = 0; c < body.Chains.Count; c++)
            {
                FluffyChain chain = body.Chains[c];
                var pose = ScriptableObject.CreateInstance<FluffyPose>();
                _rig.Track(pose);

                var bones = new FluffyBonePose[FluffyRuntimeRig.BoneCount];

                for (int i = 0; i < bones.Length; i++)
                {
                    bones[i] = new FluffyBonePose(Vector3.zero)
                    {
                        OverrideLimits = true,
                        Limits = limits
                    };
                }

                pose.SetBones(bones);
                chain.Pose = pose;
            }

            body.Rebuild();
            body.ResetToRestPose();
        }
    }
}
