using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Fluffy.Tests.Editor
{
    /// <summary>
    /// The roll a bone carries about its own axis when the rig rolls underneath it.
    /// </summary>
    /// <remarks>
    /// Everything else in the chain is a swing, and a swing comes out of where the tip is.
    /// The roll has no tip to be read from — the solver turns a bone by the shortest arc,
    /// which carries no roll by construction — so it is simulated on its own, and these
    /// are the four things that has to be true of it: it lags, it comes back, it stays
    /// inside its range, and it does not invent itself out of nothing.
    ///
    /// The rig is rolled about the axis the test skeleton runs along, so the bones' heads
    /// and tips do not move at all and the only thing the chain can do is roll. A test
    /// that rolled some other way would be watching the swing and the roll at once and
    /// could not say which had produced what.
    /// </remarks>
    public class FluffyChainTwistTests
    {
        private const float SixtiethOfASecond = 1f / 60f;

        /// <summary>The skeleton runs along X, so this is the roll axis.</summary>
        private static readonly Vector3 Along = Vector3.right;

        private readonly FluffyTestRig _rig = new FluffyTestRig();

        [TearDown]
        public void DestroyWhatTheTestMade()
        {
            _rig.DestroyEverything();
        }

        /// <summary>
        /// A bone whose rig rolls under it is left behind, which is the whole of what
        /// secondary motion is.
        /// </summary>
        /// <remarks>
        /// Measured on the first bone, the only one whose parent is the rig itself. Every
        /// bone below it hangs off a bone that has already been left behind, so what it
        /// sees is not the 30 degrees the rig turned but whatever share of them its parent
        /// passed on — which is the next test.
        /// </remarks>
        [Test]
        public void ABoneIsLeftBehindByARigThatRollsUnderIt()
        {
            FluffyChain chain = _rig.BuildChain();
            Transform rig = RigOf(chain);

            rig.rotation = Quaternion.AngleAxis(30f, Along);
            chain.Simulate(SixtiethOfASecond, CalmProfile());

            float twist = TwistOf(chain, bone: 0);

            // Nearly all of it: one step at sixty a second takes about an eighth of the
            // offset back, so a bone that stood still while the rig turned 30 degrees is a
            // little over 26 behind it.
            Assert.That(twist, Is.LessThan(-25f),
                $"The rig rolled 30 degrees and the bone kept only {twist:0.##} of that as lag. It is "
                + "being carried along with the rig rather than left behind by it.");
        }

        /// <summary>
        /// The roll travels down the chain instead of arriving everywhere at once.
        /// </summary>
        /// <remarks>
        /// A bone's rest frame is its parent's rotation, and its parent has already lagged,
        /// so each bone is handed only the share of the roll the one above it passed on.
        /// That is what makes a tail unwind along its length rather than in one piece, and
        /// it is worth pinning down: it is a consequence of where the roll is measured
        /// rather than anything the code says out loud, and it would be easy to lose by
        /// measuring against the character instead of against the parent.
        ///
        /// No bone leads. A bone ahead of its parent would be the roll gaining energy
        /// somewhere, which is the one direction this must never fail in.
        /// </remarks>
        [Test]
        public void TheRollArrivesDownTheChainRatherThanAllAtOnce()
        {
            FluffyChain chain = _rig.BuildChain();
            Transform rig = RigOf(chain);

            rig.rotation = Quaternion.AngleAxis(30f, Along);
            chain.Simulate(SixtiethOfASecond, CalmProfile());

            float atTheRoot = TwistOf(chain, bone: 0);
            float below = TwistOf(chain, bone: 1);

            Assert.That(Mathf.Abs(below), Is.LessThan(Mathf.Abs(atTheRoot)),
                $"The bone below lagged {below:0.##} degrees against the root's {atTheRoot:0.##}. It is "
                + "being measured against the character rather than against its own parent.");

            for (int bone = 0; bone < FluffyTestRig.BoneCount; bone++)
            {
                Assert.That(TwistOf(chain, bone), Is.LessThanOrEqualTo(0.01f),
                    $"Bone {bone} came out rolled ahead of its parent, which is the roll gaining "
                    + "energy rather than losing it.");
            }
        }

        /// <summary>
        /// And then catches up, because the spring that holds the swing to its pose holds
        /// the roll to the rig.
        /// </summary>
        [Test]
        public void TheRollComesBackToTheRigOnceTheRigStops()
        {
            FluffyChain chain = _rig.BuildChain();
            Transform rig = RigOf(chain);
            FluffyProfile profile = CalmProfile();

            rig.rotation = Quaternion.AngleAxis(60f, Along);
            chain.Simulate(SixtiethOfASecond, profile);

            float lag = Mathf.Abs(TwistOf(chain, bone: 0));
            Assert.That(lag, Is.GreaterThan(1f), "Nothing was left to come back from.");

            // Three seconds with the rig held still.
            for (int i = 0; i < 180; i++)
            {
                chain.Simulate(SixtiethOfASecond, profile);
            }

            float settled = Mathf.Abs(TwistOf(chain, bone: 0));

            Assert.That(settled, Is.LessThan(0.5f),
                $"Three seconds after the rig stopped, the bone was still rolled {settled:0.##} degrees "
                + "off it. The spring is not bringing the roll home.");
        }

        /// <summary>The twist range holds, the same way the two swing ranges do.</summary>
        /// <remarks>
        /// Checked every step rather than at the end: a clamp that only runs where the
        /// value is stored would let the drawn pose out past the limit in between, which
        /// is exactly the fault the swing had.
        /// </remarks>
        [Test]
        public void TheTwistRangeHoldsWhileTheRigKeepsRolling()
        {
            const float Range = 5f;

            FluffyPose pose = _rig.CreatePose(new FluffyLimits
            {
                Twist = new Vector2(-Range, Range),
                SwingY = FluffyLimits.FreeRange,
                SwingZ = FluffyLimits.FreeRange
            });

            FluffyChain chain = _rig.BuildChain(pose);
            Transform rig = RigOf(chain);
            FluffyProfile profile = CalmProfile();

            float widest = 0f;

            // A whole turn, a degree and a half at a time, which no spring is going to
            // keep up with.
            for (int i = 1; i <= 240; i++)
            {
                rig.rotation = Quaternion.AngleAxis(i * 1.5f, Along);
                chain.Simulate(SixtiethOfASecond, profile);

                for (int bone = 0; bone < FluffyTestRig.BoneCount; bone++)
                {
                    widest = Mathf.Max(widest, Mathf.Abs(TwistOf(chain, bone)));
                }
            }

            Assert.That(widest, Is.LessThan(Range + 0.5f),
                $"A bone limited to {Range} degrees of twist reached {widest:0.##}.");
        }

        /// <summary>
        /// A rig that does not roll leaves the chain unrolled, however long it is
        /// simulated for.
        /// </summary>
        /// <remarks>
        /// The weakest claim here and the one worth having: it fails if the roll ever
        /// invents itself out of a step, which is the shape of the bugs a one-dimensional
        /// simulation quietly produces. Gravity is on, so the chain is swinging the whole
        /// time — a swing must not leak into the roll.
        /// </remarks>
        [Test]
        public void AChainThatIsOnlySwingingDoesNotRollItself()
        {
            FluffyProfile profile = _rig.CreateProfile(
                returnStrength: 8f, drag: 0.15f, gravity: new Vector3(0f, -9.81f, 0f));

            FluffyChain chain = _rig.BuildChain();
            float widest = 0f;

            for (int i = 0; i < 120; i++)
            {
                chain.Simulate(SixtiethOfASecond, profile);

                for (int bone = 0; bone < FluffyTestRig.BoneCount; bone++)
                {
                    widest = Mathf.Max(widest, Mathf.Abs(TwistOf(chain, bone)));
                }
            }

            Assert.That(widest, Is.LessThan(0.5f),
                $"A chain that was only ever swung rolled itself {widest:0.##} degrees.");
        }

        /// <summary>The object the whole skeleton hangs off, which is what the tests roll.</summary>
        private static Transform RigOf(FluffyChain chain)
        {
            return chain.StartBone.parent;
        }

        /// <summary>
        /// How far a bone is rolled off the rig, read the way the trace reads it rather
        /// than recomputed here.
        /// </summary>
        private static float TwistOf(FluffyChain chain, int bone)
        {
            var states = new List<FluffyBoneState>();
            chain.CaptureState(states);

            return states[bone].Twist;
        }

        /// <summary>No gravity, so the only thing that can move is the roll.</summary>
        private FluffyProfile CalmProfile()
        {
            return _rig.CreateProfile(returnStrength: 8f, drag: 0.15f, gravity: Vector3.zero);
        }
    }
}
