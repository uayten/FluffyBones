using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Fluffy.Tests.Editor
{
    /// <summary>
    /// The invented bone past the end of a chain, and what depends on it.
    /// </summary>
    /// <remarks>
    /// A bone in a game engine is a single point. The last one in a chain therefore has
    /// no length and nothing to aim at, and a rig exported from Blender usually ends in
    /// a spare bone precisely to give it one. When the rig does not, the dummy stands in
    /// — and whether it does decides whether the last bone moves at all, which is the
    /// difference between a tail that flicks and a tail with a stiff end.
    /// </remarks>
    public class FluffyDummyBoneTests
    {
        private const float SixtiethOfASecond = 1f / 60f;

        private readonly FluffyTestRig _rig = new FluffyTestRig();

        [TearDown]
        public void DestroyWhatTheTestMade()
        {
            _rig.DestroyEverything();
        }

        /// <summary>
        /// With a dummy bone, the last real bone is simulated and turns under its own
        /// steam.
        /// </summary>
        /// <remarks>
        /// Turning by more than its parent is the assertion that matters. The end of a
        /// chain inherits everything above it, so a bone that is merely being carried
        /// still looks like it is moving when watched in the scene — a still, or a
        /// careless test, cannot tell the two apart. Its own rest frame can.
        /// </remarks>
        [Test]
        public void WithADummyBoneTheLastBoneTurnsAndNotJustItsParent()
        {
            FluffyChain chain = _rig.BuildChainThroughAComponent(useDummyBone: true, out FluffyBones body);

            Assert.That(chain.Build(body), Is.True);
            Assert.That(chain.JointCount, Is.EqualTo(FluffyTestRig.BoneCount),
                "The last bone was left out of the simulation despite having a dummy to aim at.");

            chain.ResetToRestPose();

            float furthest = FurthestTheLastBoneGot(chain, Gravity());

            Assert.That(furthest, Is.GreaterThan(1f),
                $"The last bone moved {furthest:0.###} degrees against its own rest frame, so it was "
                + "carried by its parent rather than simulated.");
        }

        /// <summary>
        /// Without a dummy bone, the last real bone is left to follow its parent instead
        /// of being simulated.
        /// </summary>
        /// <remarks>
        /// Not a lesser version of the test above but the other half of the same choice:
        /// a bone with no tip has no direction to rotate towards, and simulating it
        /// anyway is how a chain ends in a bone that spins. Following the parent is the
        /// right answer, and it has to stay the answer.
        /// </remarks>
        [Test]
        public void WithoutADummyBoneTheLastBoneIsLeftToFollowItsParent()
        {
            FluffyChain chain = _rig.BuildChainThroughAComponent(useDummyBone: false, out FluffyBones body);

            Assert.That(chain.Build(body), Is.True);
            Assert.That(chain.JointCount, Is.EqualTo(FluffyTestRig.BoneCount - 1),
                "The last bone was simulated with nothing to swing towards.");

            chain.ResetToRestPose();

            List<Transform> bones = chain.GetBones();
            Transform last = bones[bones.Count - 1];
            Quaternion restLocal = last.localRotation;

            float furthest = FurthestTheLastBoneGot(chain, Gravity());

            Assert.That(Quaternion.Angle(last.localRotation, restLocal), Is.LessThan(0.01f),
                "The last bone's own rotation was changed, so something simulated it.");

            Assert.That(furthest, Is.LessThan(0.01f),
                $"The last bone reported {furthest:0.###} degrees off its own rest frame while it was "
                + "supposed to be doing nothing but following.");
        }

        /// <summary>
        /// Runs the chain and returns how far the last bone got from its own rest frame,
        /// which is the part of its movement that is not inherited.
        /// </summary>
        private static float FurthestTheLastBoneGot(FluffyChain chain, FluffyProfile profile)
        {
            var states = new List<FluffyBoneState>();
            float furthest = 0f;

            for (int step = 0; step < 120; step++)
            {
                chain.Simulate(SixtiethOfASecond, profile);

                states.Clear();
                chain.CaptureState(states);

                for (int i = 0; i < states.Count; i++)
                {
                    if (states[i].Index == FluffyTestRig.BoneCount - 1)
                    {
                        furthest = Mathf.Max(furthest, states[i].OffRest);
                    }
                }
            }

            return furthest;
        }

        private FluffyProfile Gravity()
        {
            return _rig.CreateProfile(returnStrength: 8f, drag: 0.15f, gravity: new Vector3(0f, -10f, 0f));
        }
    }
}
