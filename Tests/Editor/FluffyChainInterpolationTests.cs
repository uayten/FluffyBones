using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Fluffy.Tests.Editor
{
    /// <summary>
    /// The pose that gets drawn, which is not the pose the solver last computed.
    /// </summary>
    /// <remarks>
    /// The solver moves in steps of a fixed length and frames do not land on them, so
    /// what is drawn is worked out between the last two steps. That is the whole reason
    /// a chain looks smooth at a frame rate the solver never runs at, and until now it
    /// was only checked end to end — two runs at different frame rates ending in the
    /// same place, which says nothing about where the chain was on the way there.
    /// </remarks>
    public class FluffyChainInterpolationTests
    {
        private const float SixtiethOfASecond = 1f / 60f;

        private readonly FluffyTestRig _rig = new FluffyTestRig();

        [TearDown]
        public void DestroyWhatTheTestMade()
        {
            _rig.DestroyEverything();
        }

        /// <summary>
        /// Drawing at the end of the step puts the chain exactly where the step left it.
        /// </summary>
        /// <remarks>
        /// This is what lets the solver step again from a pose the drawing has touched:
        /// <see cref="FluffyBones"/> calls <c>ApplyPose(1)</c> before every catch-up loop
        /// to undo the interpolation, and if that did not land exactly on the last
        /// completed step, the drawing would be feeding the simulation.
        /// </remarks>
        [Test]
        public void DrawingAtTheEndOfAStepLandsOnWhereTheStepLeftIt()
        {
            FluffyChain chain = SettledChain(out FluffyProfile profile);

            chain.Simulate(SixtiethOfASecond, profile);

            Vector3[] whereTheStepLeftIt = DirectionsNow(chain);

            // Away from it, then back.
            chain.ApplyPose(0f);
            chain.ApplyPose(1f);

            Vector3[] whereItIsDrawn = DirectionsNow(chain);

            for (int i = 0; i < whereTheStepLeftIt.Length; i++)
            {
                Assert.That(Vector3.Angle(whereItIsDrawn[i], whereTheStepLeftIt[i]), Is.LessThan(0.01f),
                    $"Bone {i} was drawn {Vector3.Angle(whereItIsDrawn[i], whereTheStepLeftIt[i]):0.###} degrees "
                    + "away from where the last completed step left it.");
            }
        }

        /// <summary>
        /// A frame that falls between two steps is drawn between the two poses, and not
        /// at either of them.
        /// </summary>
        /// <remarks>
        /// Betweenness rather than an exact halfway: the interpolation is linear on the
        /// tips and the angle it is measured by is not, so a half-step lands near the
        /// middle without landing on it. What matters is that the drawn pose is on the
        /// way from one to the other — a chain that snapped to whichever step was nearest
        /// would judder at every frame rate that is not a multiple of the simulation
        /// rate, which is most of them.
        /// </remarks>
        [Test]
        public void AFrameBetweenTwoStepsIsDrawnBetweenTwoPoses()
        {
            FluffyChain chain = SettledChain(out FluffyProfile profile);

            chain.Simulate(SixtiethOfASecond, profile);

            chain.ApplyPose(0f);
            Vector3[] atTheStart = DirectionsNow(chain);

            chain.ApplyPose(1f);
            Vector3[] atTheEnd = DirectionsNow(chain);

            chain.ApplyPose(0.5f);
            Vector3[] halfway = DirectionsNow(chain);

            bool anythingMoved = false;

            for (int i = 0; i < atTheStart.Length; i++)
            {
                float wholeStep = Vector3.Angle(atTheStart[i], atTheEnd[i]);

                if (wholeStep < 0.05f)
                {
                    // A bone that barely moved this step has no middle worth checking.
                    continue;
                }

                anythingMoved = true;

                float toHalfway = Vector3.Angle(atTheStart[i], halfway[i]);
                float fromHalfway = Vector3.Angle(halfway[i], atTheEnd[i]);

                Assert.That(toHalfway + fromHalfway, Is.EqualTo(wholeStep).Within(0.05f),
                    $"Bone {i} was drawn off the path between the two steps: {toHalfway:0.####} plus "
                    + $"{fromHalfway:0.####} degrees against a step of {wholeStep:0.####}.");

                Assert.That(toHalfway, Is.EqualTo(wholeStep * 0.5f).Within(wholeStep * 0.1f),
                    $"Bone {i} was drawn {toHalfway:0.####} degrees into a step of {wholeStep:0.####}, "
                    + "which is not the middle of it.");
            }

            Assert.That(anythingMoved, Is.True,
                "No bone moved enough in one step for the interpolation to have anything to do.");
        }

        /// <summary>
        /// A chain with a step behind it, so there are two poses to interpolate between.
        /// </summary>
        private FluffyChain SettledChain(out FluffyProfile profile)
        {
            FluffyChain chain = _rig.BuildChain();
            profile = _rig.CreateProfile(returnStrength: 8f, drag: 0.05f, gravity: new Vector3(0f, -20f, 0f));

            // Enough to be moving, not so much that it has stopped: a chain at rest has
            // the same pose at both ends of the step and nothing to interpolate.
            for (int step = 0; step < 10; step++)
            {
                chain.Simulate(SixtiethOfASecond, profile);
            }

            return chain;
        }

        /// <summary>Where every bone of the chain points, right now.</summary>
        private static Vector3[] DirectionsNow(FluffyChain chain)
        {
            var states = new List<FluffyBoneState>();
            chain.CaptureState(states);

            var directions = new Vector3[states.Count];

            for (int i = 0; i < states.Count; i++)
            {
                directions[i] = states[i].Direction;
            }

            return directions;
        }
    }
}
