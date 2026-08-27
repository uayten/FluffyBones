using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Fluffy.Tests.Editor
{
    /// <summary>
    /// Whether what a trace says about a bone is where the bone actually is.
    /// </summary>
    /// <remarks>
    /// A trace is only worth reading if it describes the run rather than a folded copy
    /// of it, and it is the one part of the plugin nothing else can check: every other
    /// test in this suite reads the world through <c>CaptureState</c>, so a trace that
    /// lies takes the tests with it.
    /// </remarks>
    public class FluffyChainTraceTests
    {
        private const float SixtiethOfASecond = 1f / 60f;

        private readonly FluffyTestRig _rig = new FluffyTestRig();

        [TearDown]
        public void DestroyWhatTheTestMade()
        {
            _rig.DestroyEverything();
        }

        /// <summary>
        /// The two swings a trace reports rebuild the angle the bone is actually sitting
        /// at, on every step of a hard run.
        /// </summary>
        /// <remarks>
        /// Two independent readings of the same thing. The swings come out of the
        /// decomposition the solver clamps in — an arcsine per axis, unfolded past the
        /// quarter turn — while <c>OffRest</c> is a plain angle between two vectors that
        /// knows nothing about axes. In an orthonormal frame the first rebuilds the
        /// second exactly, so any disagreement means the decomposition is describing a
        /// bone that is not there.
        ///
        /// This is the shape of the fault the unfolding was written for: a bone at 136
        /// degrees reporting 44. Both numbers are still available here, and they would
        /// stop matching.
        ///
        /// Limits are left free on purpose. Clamping would hold the bone where the two
        /// readings trivially agree, and the interesting range is past the quarter turn,
        /// which a bone only reaches when nothing is holding it.
        /// </remarks>
        [Test]
        public void TheSwingsInATraceRebuildTheAngleTheBoneIsSittingAt()
        {
            FluffyChain chain = _rig.BuildChain();
            FluffyProfile profile = _rig.CreateProfile(
                returnStrength: 2f, drag: 0.02f, gravity: new Vector3(0f, -60f, 0f));

            var states = new List<FluffyBoneState>();
            float worstDisagreement = 0f;
            float furthest = 0f;

            for (int step = 0; step < 240; step++)
            {
                chain.Simulate(SixtiethOfASecond, profile);

                states.Clear();
                chain.CaptureState(states);

                for (int i = 0; i < states.Count; i++)
                {
                    float rebuilt = OffRestFromSwings(states[i].SwingY, states[i].SwingZ);

                    worstDisagreement = Mathf.Max(worstDisagreement, Mathf.Abs(rebuilt - states[i].OffRest));
                    furthest = Mathf.Max(furthest, states[i].OffRest);
                }
            }

            Assert.That(furthest, Is.GreaterThan(90f),
                $"The chain only reached {furthest:0.#} degrees off its pose, so the run never got past "
                + "the quarter turn where the two readings can differ. The test proves nothing as it is.");

            Assert.That(worstDisagreement, Is.LessThan(0.5f),
                $"The swings a trace reports and the angle the bone sits at disagreed by "
                + $"{worstDisagreement:0.##} degrees.");
        }

        /// <summary>
        /// A trace never reports a bone outside the limits it also reports, and says so
        /// when the bone is sitting against one.
        /// </summary>
        /// <remarks>
        /// The limits in a row are the ones the solver resolved for that bone on that
        /// step, clamping included, so the row carries everything needed to judge itself
        /// — which is what makes a CSV readable away from the running game. A row whose
        /// angle sits outside its own bounds means the clamp and the reading came from
        /// different places.
        /// </remarks>
        [Test]
        public void ATraceNeverReportsABoneOutsideTheLimitsItAlsoReports()
        {
            const float allowed = 20f;

            FluffyLimits limits = FluffyLimits.Free;
            limits.SwingY = new Vector2(-allowed, allowed);
            limits.SwingZ = new Vector2(-allowed, allowed);

            FluffyChain chain = _rig.BuildChain(_rig.CreatePose(limits));
            FluffyProfile profile = _rig.CreateProfile(
                returnStrength: 8f, drag: 0.05f, gravity: new Vector3(0f, -30f, 0f));

            var states = new List<FluffyBoneState>();
            bool anythingReachedItsLimit = false;

            for (int step = 0; step < 240; step++)
            {
                chain.Simulate(SixtiethOfASecond, profile);

                states.Clear();
                chain.CaptureState(states);

                for (int i = 0; i < states.Count; i++)
                {
                    FluffyBoneState state = states[i];

                    Assert.That(state.SwingY, Is.InRange(state.Limits.SwingY.x - 0.5f, state.Limits.SwingY.y + 0.5f),
                        $"Bone {state.Index} reported a Y swing of {state.SwingY:0.##} against its own "
                        + $"reported range of {state.Limits.SwingY.x} to {state.Limits.SwingY.y}.");

                    Assert.That(state.SwingZ, Is.InRange(state.Limits.SwingZ.x - 0.5f, state.Limits.SwingZ.y + 0.5f),
                        $"Bone {state.Index} reported a Z swing of {state.SwingZ:0.##} against its own "
                        + $"reported range of {state.Limits.SwingZ.x} to {state.Limits.SwingZ.y}.");

                    if (state.AtSwingYLimit)
                    {
                        anythingReachedItsLimit = true;

                        Assert.That(Mathf.Min(
                                Mathf.Abs(state.SwingY - state.Limits.SwingY.x),
                                Mathf.Abs(state.SwingY - state.Limits.SwingY.y)),
                            Is.LessThan(1f),
                            $"Bone {state.Index} was flagged as against its Y limit while sitting at "
                            + $"{state.SwingY:0.##}, which is not near either end.");
                    }
                }
            }

            Assert.That(anythingReachedItsLimit, Is.True,
                "Nothing ever reached its limit, so the flag was never exercised.");
        }

        /// <summary>
        /// The angle a bone sits at, worked back out of the two swings that describe it.
        /// </summary>
        /// <remarks>
        /// The frame is orthonormal, so the two sines and the component along the bone
        /// make a unit vector: whatever is left over after the two swings is how much of
        /// the bone still points the way it rests. A swing past the quarter turn is what
        /// says that component points backwards, which is the whole reason the swings
        /// carry angles beyond 90 rather than folding into them.
        /// </remarks>
        private static float OffRestFromSwings(float swingY, float swingZ)
        {
            float y = Mathf.Sin(swingY * Mathf.Deg2Rad);
            float z = Mathf.Sin(swingZ * Mathf.Deg2Rad);
            float alongTheBone = Mathf.Sqrt(Mathf.Max(0f, 1f - y * y - z * z));

            if (Mathf.Abs(swingY) > 90f || Mathf.Abs(swingZ) > 90f)
            {
                alongTheBone = -alongTheBone;
            }

            return Mathf.Acos(Mathf.Clamp(alongTheBone, -1f, 1f)) * Mathf.Rad2Deg;
        }
    }
}
