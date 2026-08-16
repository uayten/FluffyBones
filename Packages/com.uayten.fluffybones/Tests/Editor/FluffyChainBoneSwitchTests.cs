using System.Collections.Generic;
using Fluffy.Editor;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Fluffy.Tests.Editor
{
    /// <summary>
    /// Bones a chain collected but is told not to move.
    /// </summary>
    /// <remarks>
    /// A chain is authored as two ends and takes everything between them, which is right
    /// nearly always and wrong at the top: the first bone of a skirt usually belongs to
    /// the hip animation, and simulating it fights whatever is driving it. The switch
    /// says so per bone.
    ///
    /// What is worth testing is not the flag but what the solver does with it — that the
    /// bone gets no joint, that the bones below it carry on, and that a pose too short to
    /// mention a bone does not decide anything about it.
    /// </remarks>
    public class FluffyChainBoneSwitchTests
    {
        private const float SixtiethOfASecond = 1f / 60f;

        /// <summary>Far enough into the chain that it has a bone above it and bones below.</summary>
        private const int SwitchedOff = 2;

        private readonly FluffyTestRig _rig = new FluffyTestRig();

        [TearDown]
        public void DestroyWhatTheTestMade()
        {
            _rig.DestroyEverything();
        }

        /// <summary>A bone that is switched off is not one of the chain's joints.</summary>
        [Test]
        public void ABoneThatIsSwitchedOffIsNotGivenAJoint()
        {
            FluffyChain chain = _rig.BuildChain(PoseWithOneSwitchedOff(SwitchedOff));

            Assert.That(chain.JointCount, Is.EqualTo(FluffyTestRig.BoneCount - 1),
                "The chain should have taken every bone but the one that was switched off.");
        }

        /// <summary>
        /// The switched-off bone stays where whatever is animating it left it, and the
        /// bones below it go on swinging.
        /// </summary>
        /// <remarks>
        /// The second half is the whole point. Cutting a bone out of the simulation by
        /// moving the chain's start bone past it also cuts out everything above it; this
        /// takes one bone out of the middle and leaves the rest of the chain working.
        /// </remarks>
        [Test]
        public void ABoneThatIsSwitchedOffKeepsItsAnimationWhileTheOnesBelowSwing()
        {
            FluffyProfile profile = _rig.CreateProfile(
                returnStrength: 0f, drag: 0f, gravity: new Vector3(0f, -9.81f, 0f));

            FluffyChain chain = _rig.BuildChain(PoseWithOneSwitchedOff(SwitchedOff));
            List<Transform> bones = chain.GetBones();

            Quaternion before = bones[SwitchedOff].localRotation;

            for (int i = 0; i < 60; i++)
            {
                chain.Simulate(SixtiethOfASecond, profile);
            }

            Assert.That(Quaternion.Angle(bones[SwitchedOff].localRotation, before), Is.LessThan(0.01f),
                "The solver moved a bone it was told to leave alone.");

            Assert.That(Quaternion.Angle(bones[SwitchedOff + 1].localRotation, Quaternion.identity),
                Is.GreaterThan(1f),
                "The bone below the switched-off one stopped swinging with it.");
        }

        /// <summary>A chain with every bone switched off has nothing to build.</summary>
        /// <remarks>
        /// Built by hand rather than through the rig, which asserts that a chain came out
        /// with something to simulate — here the empty result is the answer being checked.
        /// </remarks>
        [Test]
        public void SwitchingEveryBoneOffLeavesNothingToSimulate()
        {
            var entries = new FluffyBonePose[FluffyTestRig.BoneCount];
            for (int i = 0; i < entries.Length; i++)
            {
                entries[i] = new FluffyBonePose { Skip = true };
            }

            var chain = new FluffyChain(_rig.BuildSkeleton(FluffyTestRig.BoneCount))
            {
                Pose = _rig.CreatePose(FluffyLimits.Free, entries)
            };

            Assert.That(chain.Build(), Is.False, "A chain with no bone left to move reported itself built.");
            Assert.That(chain.IsBuilt, Is.False);
        }

        /// <summary>
        /// A pose written for a shorter chain answers for the bones it covers and says
        /// nothing about the rest.
        /// </summary>
        /// <remarks>
        /// Poses outliving the chain they were written for is a case the inspector already
        /// handles in the other direction, by offering to trim the extra entries. This is
        /// the same mismatch the other way round, where reading past the end would throw
        /// or, worse, read whatever the last entry said.
        /// </remarks>
        [Test]
        public void APoseShorterThanTheChainAnswersOnlyForTheBonesItCovers()
        {
            var entries = new[]
            {
                new FluffyBonePose(),
                new FluffyBonePose { Skip = true }
            };

            FluffyChain chain = _rig.BuildChain(_rig.CreatePose(FluffyLimits.Free, entries));

            Assert.That(chain.JointCount, Is.EqualTo(FluffyTestRig.BoneCount - 1),
                "Only the one bone the pose switches off should have been left out.");
        }

        /// <summary>
        /// Seeding a pose for a longer chain does not hand the new bones the last one's
        /// switch.
        /// </summary>
        /// <remarks>
        /// Growing a serialized array copies the last element into every new one, which is
        /// why the seeding writes each field rather than trusting the default. A tail
        /// whose tip was switched off would otherwise grow a switched-off tail.
        /// </remarks>
        [Test]
        public void GrowingAPoseDoesNotHandTheNewBonesTheLastOnesSwitch()
        {
            FluffyChain chain = _rig.BuildChainThroughAComponent(useDummyBone: true, out FluffyBones body);
            List<Transform> bones = chain.GetBones();

            var serialized = new SerializedObject(body);
            SerializedProperty pose = serialized.FindProperty("_chains")
                .GetArrayElementAtIndex(0)
                .FindPropertyRelative("_defaultPose");

            pose.arraySize = 2;
            pose.GetArrayElementAtIndex(1).FindPropertyRelative(nameof(FluffyBonePose.Skip)).boolValue = true;

            FluffyBonesEditor.SeedPose(pose, bones);

            for (int i = 2; i < bones.Count; i++)
            {
                bool skipped = pose.GetArrayElementAtIndex(i)
                    .FindPropertyRelative(nameof(FluffyBonePose.Skip)).boolValue;

                Assert.That(skipped, Is.False,
                    $"Bone {i} was seeded switched off, copied from the entry before it.");
            }
        }

        /// <summary>A pose covering the whole chain with one bone switched off.</summary>
        private FluffyPose PoseWithOneSwitchedOff(int index)
        {
            var entries = new FluffyBonePose[FluffyTestRig.BoneCount];
            entries[index] = new FluffyBonePose { Skip = true };

            return _rig.CreatePose(FluffyLimits.Free, entries);
        }
    }
}
