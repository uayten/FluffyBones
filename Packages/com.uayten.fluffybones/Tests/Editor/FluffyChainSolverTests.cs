using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Fluffy.Tests.Editor
{
    /// <summary>
    /// Steps a chain by hand, outside play mode, and reads what it did.
    /// </summary>
    /// <remarks>
    /// A chain's behaviour is a sequence rather than a picture: what goes wrong shows up
    /// as a bone travelling further in one step than that step paid for, and a still
    /// cannot tell that from motion that is simply fast. Driving
    /// <see cref="FluffyChain.Simulate"/> directly gives the same numbers a trace would,
    /// with the step lengths chosen instead of measured — so a failure names a step
    /// length rather than the machine that happened to produce one.
    /// </remarks>
    public class FluffyChainSolverTests
    {
        private const float SixtiethOfASecond = 1f / 60f;

        private readonly FluffyTestRig _rig = new FluffyTestRig();

        [TearDown]
        public void DestroyWhatTheTestMade()
        {
            _rig.DestroyEverything();
        }

        /// <summary>
        /// A chain already sitting at its rest pose, with nothing pulling on it, has
        /// nowhere to go.
        /// </summary>
        /// <remarks>
        /// The weakest claim in the file and the one worth having first: it fails if the
        /// solver ever invents motion out of a step, which is the shape of most of the
        /// bugs worth catching here. Gravity is off precisely so that any movement at all
        /// is the solver's own doing.
        /// </remarks>
        [Test]
        public void AChainWithNothingPullingOnItStaysWhereItIs()
        {
            FluffyProfile profile = _rig.CreateProfile(returnStrength: 8f, drag: 0.15f, gravity: Vector3.zero);
            FluffyChain chain = _rig.BuildChain();

            float peak = StepAndReturnPeakOffRest(chain, profile, EvenSteps(SixtiethOfASecond, 120));

            Assert.That(peak, Is.LessThan(0.01f),
                $"A chain at rest under no forces drifted {peak:0.####} degrees off its pose.");
        }

        /// <summary>
        /// The same two seconds, cut into even steps and into uneven ones, leave the
        /// chain having swung about as far.
        /// </summary>
        /// <remarks>
        /// This is the regression for the free speed an uneven frame used to hand the
        /// chain. What the solver stores between steps is the distance a bone covered,
        /// not its speed, so replaying that distance over a longer step made the bone
        /// travel faster than it had been travelling — and a frame longer than the one
        /// before it arrives constantly in an editor. Peak rather than final position:
        /// both schedules settle in the same place, and the fault showed itself on the
        /// way there.
        ///
        /// As measured, the even schedule peaks at 11.16 degrees and the uneven one at
        /// 8.30. Uneven steps land under rather than over, and that is expected: gravity
        /// and the pull to the pose are both scaled by the step squared, and squares
        /// favour the uneven pair — a short step and a long one add up to more than two
        /// average ones, so the chain is pulled about a quarter harder and swings less
        /// wide. The assertion is one-sided for exactly that reason. What it guards
        /// against is the other direction, where the fault reached 90 degrees against a
        /// steady schedule's 16.
        ///
        /// Worth knowing while reading this: since the solver moved to a fixed step, the
        /// only way to reach <see cref="FluffyChain.Simulate"/> with steps of differing
        /// lengths is to call it directly, as this test does. Through
        /// <see cref="FluffyBones"/> every step is the same length by construction.
        /// </remarks>
        [Test]
        public void UnevenStepsDoNotHandTheChainFreeSpeed()
        {
            FluffyProfile profile = _rig.CreateProfile(
                returnStrength: 8f, drag: 0.15f, gravity: new Vector3(0f, -2f, 0f));

            // A hundred and twentieth and a fortieth make a thirtieth: sixty of those
            // pairs cover the same two seconds the even schedule does.
            List<float> even = EvenSteps(SixtiethOfASecond, 120);
            List<float> uneven = AlternatingSteps(1f / 120f, 1f / 40f, 60);

            Assert.That(Total(uneven), Is.EqualTo(Total(even)).Within(1e-4f),
                "The two schedules have to cover the same stretch of time to be comparable.");

            float steadyPeak = StepAndReturnPeakOffRest(_rig.BuildChain(), profile, even);
            float unevenPeak = StepAndReturnPeakOffRest(_rig.BuildChain(), profile, uneven);

            Assert.That(unevenPeak, Is.LessThan(steadyPeak * 1.15f),
                $"Uneven steps swung the chain to {unevenPeak:0.##} degrees off its pose where even "
                + $"steps over the same {Total(even):0.##} seconds reached {steadyPeak:0.##}.");
        }

        /// <summary>
        /// Two seconds solved at sixty steps a second and at a hundred and twenty leave
        /// the chain in about the same place.
        /// </summary>
        /// <remarks>
        /// Damping is authored as how much speed survives a sixtieth of a second and the
        /// solver raises it to the length of the step, so it is not what this catches.
        /// What it caught when it was written was the pull back to the pose: scaled by the
        /// step where gravity was scaled by the step squared, the balance between them
        /// carried the step length into the answer, and the same chain settled 1.91 degrees
        /// off its pose at 30 steps a second, 0.95 at 60 and 0.48 at 120. Both are
        /// accelerations now.
        ///
        /// Where the previous test varies the steps within one run, this one keeps every
        /// step even and varies the rate between two runs — the difference between a
        /// stuttering machine and a fast one.
        /// </remarks>
        [Test]
        public void DampingIsCountedInSecondsRatherThanInSteps()
        {
            FluffyProfile profile = _rig.CreateProfile(
                returnStrength: 8f, drag: 0.15f, gravity: new Vector3(0f, -2f, 0f));

            List<float> sixty = EvenSteps(SixtiethOfASecond, 120);
            List<float> hundredAndTwenty = EvenSteps(1f / 120f, 240);

            Assert.That(Total(hundredAndTwenty), Is.EqualTo(Total(sixty)).Within(1e-4f),
                "The two rates have to cover the same stretch of time to be comparable.");

            float atSixty = StepAndReturnFinalOffRest(_rig.BuildChain(), profile, sixty);
            float atDouble = StepAndReturnFinalOffRest(_rig.BuildChain(), profile, hundredAndTwenty);

            // Two percent, where the two runs actually agree to a rounding error: the
            // margin is there for a float drifting on another platform, not for a rate
            // that leaves the chain somewhere else.
            Assert.That(atDouble, Is.EqualTo(atSixty).Within(2f).Percent,
                $"Solving twice as often left the chain {atDouble:0.##} degrees off its pose where "
                + $"sixty steps a second left it {atSixty:0.##}.");
        }

        /// <summary>
        /// Runs the chain through a schedule of steps and returns the furthest any bone
        /// got from its pose along the way.
        /// </summary>
        private static float StepAndReturnPeakOffRest(
            FluffyChain chain, FluffyProfile profile, IReadOnlyList<float> steps)
        {
            var states = new List<FluffyBoneState>();
            float peak = 0f;

            for (int i = 0; i < steps.Count; i++)
            {
                chain.Simulate(steps[i], profile);
                peak = Mathf.Max(peak, WidestOffRest(chain, states));
            }

            return peak;
        }

        /// <summary>
        /// Runs the schedule and returns how far off its pose the chain ended up, which
        /// is where it settled rather than how it got there.
        /// </summary>
        private static float StepAndReturnFinalOffRest(
            FluffyChain chain, FluffyProfile profile, IReadOnlyList<float> steps)
        {
            var states = new List<FluffyBoneState>();

            for (int i = 0; i < steps.Count; i++)
            {
                chain.Simulate(steps[i], profile);
            }

            return WidestOffRest(chain, states);
        }

        /// <summary>The furthest any bone of the chain currently sits from its pose.</summary>
        private static float WidestOffRest(FluffyChain chain, List<FluffyBoneState> reused)
        {
            reused.Clear();
            chain.CaptureState(reused);

            float widest = 0f;

            for (int i = 0; i < reused.Count; i++)
            {
                widest = Mathf.Max(widest, reused[i].OffRest);
            }

            return widest;
        }

        private static List<float> EvenSteps(float length, int count)
        {
            var steps = new List<float>(count);

            for (int i = 0; i < count; i++)
            {
                steps.Add(length);
            }

            return steps;
        }

        /// <summary>A short step and a long one, over and over, for <paramref name="pairs"/> pairs.</summary>
        private static List<float> AlternatingSteps(float shortStep, float longStep, int pairs)
        {
            var steps = new List<float>(pairs * 2);

            for (int i = 0; i < pairs; i++)
            {
                steps.Add(shortStep);
                steps.Add(longStep);
            }

            return steps;
        }

        private static float Total(IReadOnlyList<float> steps)
        {
            float total = 0f;

            for (int i = 0; i < steps.Count; i++)
            {
                total += steps[i];
            }

            return total;
        }
    }
}
