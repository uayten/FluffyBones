using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Fluffy.Tests.Editor
{
    /// <summary>
    /// What the angle limits are supposed to hold back, and whether they do.
    /// </summary>
    public class FluffyChainLimitsTests
    {
        private const float SixtiethOfASecond = 1f / 60f;

        private readonly FluffyTestRig _rig = new FluffyTestRig();

        [TearDown]
        public void DestroyWhatTheTestMade()
        {
            _rig.DestroyEverything();
        }

        /// <summary>
        /// A chain driven far harder than anything in a game will drive it never gets
        /// outside the cone its limits describe.
        /// </summary>
        /// <remarks>
        /// The second assertion is the point of the test, and it is deliberately not
        /// written in terms of the swing angles. The solver clamps what
        /// <c>SwingAngle</c> reports, and a trace reads the same function — so a fault in
        /// how a swing is measured fools the clamp and the check in the same direction,
        /// and a test asking "did the reported swing stay under 25" would pass while a
        /// bone sat at 136 degrees. That is not hypothetical: it is the fault the
        /// unfolding in <c>SwingAngle</c> was written to fix. The angle between where the
        /// bone rests and where it points is a plain angle between two vectors, measured
        /// nowhere near the swing decomposition, so it cannot be fooled by the same bug.
        ///
        /// Gravity is set absurdly high on purpose. A limit that holds under a gentle
        /// droop but not under a slam is a limit that fails the first time a character
        /// is thrown by a hit.
        /// </remarks>
        [Test]
        public void ABoneNeverLeavesTheConeItIsAllowedToSwingThrough()
        {
            const float allowed = 25f;
            const float slack = 0.5f;

            FluffyLimits limits = FluffyLimits.Free;
            limits.SwingY = new Vector2(-allowed, allowed);
            limits.SwingZ = new Vector2(-allowed, allowed);

            FluffyChain chain = _rig.BuildChain(_rig.CreatePose(limits));
            FluffyProfile profile = _rig.CreateProfile(
                returnStrength: 8f, drag: 0.05f, gravity: new Vector3(0f, -30f, 0f));

            var states = new List<FluffyBoneState>();
            float widestSwing = 0f;
            float furthestOffRest = 0f;

            for (int step = 0; step < 240; step++)
            {
                chain.Simulate(SixtiethOfASecond, profile);

                states.Clear();
                chain.CaptureState(states);

                for (int i = 0; i < states.Count; i++)
                {
                    widestSwing = Mathf.Max(widestSwing, Mathf.Abs(states[i].SwingY));
                    widestSwing = Mathf.Max(widestSwing, Mathf.Abs(states[i].SwingZ));
                    furthestOffRest = Mathf.Max(furthestOffRest, states[i].OffRest);
                }
            }

            Assert.That(widestSwing, Is.LessThanOrEqualTo(allowed + slack),
                $"A bone reported a swing of {widestSwing:0.##} degrees against a limit of {allowed}.");

            float corner = CornerOfTheCone(allowed);

            Assert.That(furthestOffRest, Is.LessThanOrEqualTo(corner + slack),
                $"A bone sat {furthestOffRest:0.##} degrees off its pose, where the widest a bone inside "
                + $"a {allowed} degree cone can be is {corner:0.##}. The limit is being measured, not held.");
        }

        /// <summary>
        /// A range keeps the pose inside it, whatever it was authored as.
        /// </summary>
        /// <remarks>
        /// Zero is where the bone rests, so a minimum above it asks the solver to hold the
        /// bone off its own pose while the spring pulls it straight back — the two fight,
        /// and the rim drawn in the scene turns inside out. Cheap to check and easy to
        /// break, since the clamping happens away from the code that reads the values.
        /// </remarks>
        [Test]
        public void ARangeAlwaysKeepsTheRestPoseInsideIt()
        {
            Assert.That(FluffyLimits.ClampRange(new Vector2(10f, 40f)), Is.EqualTo(new Vector2(0f, 40f)),
                "A minimum above the pose was not pulled back down to it.");

            Assert.That(FluffyLimits.ClampRange(new Vector2(-30f, -5f)), Is.EqualTo(new Vector2(-30f, 0f)),
                "A maximum below the pose was not pulled back up to it.");

            Assert.That(FluffyLimits.ClampRange(new Vector2(-200f, 200f)),
                Is.EqualTo(new Vector2(-FluffyLimits.Open, FluffyLimits.Open)),
                "A range wider than a half turn was not brought back inside one.");

            Assert.That(FluffyLimits.Free.IsUnrestricted, Is.True,
                "The free limits stopped counting as free.");
        }

        /// <summary>
        /// How far off its pose a bone can be while still inside the cone: both swings at
        /// their limit at once, which is further than either alone.
        /// </summary>
        private static float CornerOfTheCone(float degrees)
        {
            float sine = Mathf.Sin(degrees * Mathf.Deg2Rad);
            float alongTheBone = Mathf.Sqrt(Mathf.Max(0f, 1f - 2f * sine * sine));

            return Mathf.Acos(alongTheBone) * Mathf.Rad2Deg;
        }
    }
}
