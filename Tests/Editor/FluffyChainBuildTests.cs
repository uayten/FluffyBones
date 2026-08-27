using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Fluffy.Tests.Editor
{
    /// <summary>
    /// What a chain makes of the hierarchy it is pointed at.
    /// </summary>
    /// <remarks>
    /// The rigs people bring are not the tidy one the playground has: a bone with two
    /// children, a last bone dragged in from another character, a chain of one. None of
    /// those should throw, and none should be simulated wrongly and quietly.
    /// </remarks>
    public class FluffyChainBuildTests
    {
        private readonly FluffyTestRig _rig = new FluffyTestRig();

        [TearDown]
        public void DestroyWhatTheTestMade()
        {
            _rig.DestroyEverything();
        }

        /// <summary>
        /// A start bone with nothing under it is refused, and said so out loud.
        /// </summary>
        /// <remarks>
        /// A single bone has no tip to swing towards, so there is nothing to simulate.
        /// Refusing quietly would leave someone staring at a chain that does not move
        /// with no idea why, which is the worst of the three possible outcomes — worse
        /// than throwing.
        /// </remarks>
        [Test]
        public void AChainOfOneBoneRefusesToBuildAndSaysWhy()
        {
            var chain = new FluffyChain(_rig.BuildSkeleton(1));

            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex(
                "needs at least two bones"));

            Assert.That(chain.Build(), Is.False, "A chain of one bone claimed it had something to simulate.");
            Assert.That(chain.IsBuilt, Is.False);
            Assert.That(chain.JointCount, Is.Zero);
        }

        /// <summary>
        /// Simulating a chain that refused to build does nothing at all.
        /// </summary>
        /// <remarks>
        /// The guard exists in the solver; this is what stops someone removing it. A
        /// chain that failed to build is the state a rig is in while it is being set up,
        /// and the editor keeps calling into it the whole time.
        /// </remarks>
        [Test]
        public void AChainThatRefusedToBuildIsSafeToStep()
        {
            var chain = new FluffyChain(_rig.BuildSkeleton(1));

            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex(
                "needs at least two bones"));
            chain.Build();

            Transform bone = chain.StartBone;
            Quaternion before = bone.rotation;

            chain.Simulate(1f / 60f, _rig.CreateProfile(8f, 0.15f, new Vector3(0f, -30f, 0f)));
            chain.ApplyPose(1f);
            chain.ResetToRestPose();

            Assert.That(bone.rotation, Is.EqualTo(before), "A chain with no joints moved a bone anyway.");
        }

        /// <summary>
        /// The walk down the hierarchy takes the first child of each bone and stops where
        /// it is told to.
        /// </summary>
        /// <remarks>
        /// A bone with two children is the normal shape of a rig, not a broken one — the
        /// hips of a skirt have eight. Which one a chain follows has to be decided rather
        /// than discovered, so a strand keeps meaning the same bones after someone
        /// reorders the hierarchy.
        /// </remarks>
        [Test]
        public void TheChainFollowsTheFirstChildAndStopsAtTheLastBone()
        {
            Transform start = _rig.BuildSkeleton(4);
            Transform second = start.GetChild(0);
            Transform third = second.GetChild(0);

            // A sibling for the second bone, added after its own child so it is second in
            // the hierarchy and must not be picked up.
            Transform branch = _rig.AddBone(second, "branch_00", new Vector3(0f, FluffyTestRig.BoneLength, 0f));

            List<Transform> whole = FluffyChain.CollectChain(start);

            Assert.That(whole, Has.Count.EqualTo(4), "The walk did not run to the end of the hierarchy.");

            // By identity rather than through NUnit's collection matchers: a Transform is
            // itself an IEnumerable of its children, so those matchers compare two bones
            // by what hangs off them and read any two childless bones as the same one.
            Assert.That(whole.Contains(branch), Is.False, "The walk wandered into the second child.");

            List<Transform> cut = FluffyChain.CollectChain(start, third);

            Assert.That(cut, Has.Count.EqualTo(3), "The walk did not stop at the last bone.");
            Assert.That(cut[0], Is.SameAs(start));
            Assert.That(cut[1], Is.SameAs(second));
            Assert.That(cut[2], Is.SameAs(third), "The last bone was not included in the chain.");
        }

        /// <summary>
        /// A last bone from somewhere else does not silently truncate the chain.
        /// </summary>
        /// <remarks>
        /// Dragging the wrong transform into the slot is a slip anyone makes, and the
        /// wrong outcome is a chain that quietly simulates one bone. It runs to the end
        /// instead, and complains.
        /// </remarks>
        [Test]
        public void ALastBoneFromAnotherHierarchyWarnsAndRunsToTheEnd()
        {
            Transform start = _rig.BuildSkeleton(4);
            Transform stranger = _rig.BuildSkeleton(2);

            var chain = new FluffyChain(start) { LastBone = stranger };

            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex(
                "is not below"));

            Assert.That(chain.Build(), Is.True, "The chain gave up instead of running to the end.");
            Assert.That(chain.JointCount, Is.EqualTo(4), "The chain did not cover every bone below the start.");
        }

        /// <summary>
        /// A bone sitting exactly on top of its child is skipped rather than divided by.
        /// </summary>
        /// <remarks>
        /// Zero length means no direction to point in, and the normalisation in the
        /// solver would hand back a NaN that spreads to every bone below it within a
        /// frame. Rigs exported from elsewhere carry these more often than you would
        /// hope.
        /// </remarks>
        [Test]
        public void ABoneWithNoLengthIsLeftOutRatherThanDividedBy()
        {
            Transform start = _rig.BuildSkeleton(3);
            Transform second = start.GetChild(0);

            // On top of its parent: the first bone now measures zero.
            second.localPosition = Vector3.zero;

            var chain = new FluffyChain(start);

            Assert.That(chain.Build(), Is.True);
            Assert.That(chain.JointCount, Is.EqualTo(2), "The zero-length bone was simulated anyway.");

            chain.Simulate(1f / 60f, _rig.CreateProfile(8f, 0.15f, new Vector3(0f, -2f, 0f)));

            var states = new List<FluffyBoneState>();
            chain.CaptureState(states);

            for (int i = 0; i < states.Count; i++)
            {
                Assert.That(float.IsNaN(states[i].OffRest), Is.False,
                    $"Bone '{states[i].Bone.name}' came out of the solver as NaN.");
            }
        }
    }
}
