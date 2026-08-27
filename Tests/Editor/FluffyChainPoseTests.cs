using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Fluffy.Tests.Editor
{
    /// <summary>
    /// The authoring side: the pose a chain rests at, the limits one bone carries alone,
    /// and the copying of both onto the other strands of a skirt.
    /// </summary>
    public class FluffyChainPoseTests
    {
        private const float SixtiethOfASecond = 1f / 60f;

        private readonly FluffyTestRig _rig = new FluffyTestRig();

        [TearDown]
        public void DestroyWhatTheTestMade()
        {
            _rig.DestroyEverything();
        }

        /// <summary>
        /// A bone carrying its own limits is held to them while the rest of the chain
        /// swings free.
        /// </summary>
        /// <remarks>
        /// The override is the whole reason limits live per bone rather than per chain —
        /// the bone at a skirt's waist is the one that must not fold, and the hem is the
        /// one that must. Testing it needs both halves in one run: a limit that binds
        /// everything, override or not, would pass an assertion that only looks at the
        /// held bone.
        /// </remarks>
        [Test]
        public void ABoneWithItsOwnLimitsIsHeldWhileTheRestSwingFree()
        {
            const int held = 1;
            const float allowed = 5f;

            var bones = new FluffyBonePose[FluffyTestRig.BoneCount];

            for (int i = 0; i < bones.Length; i++)
            {
                bones[i] = new FluffyBonePose(Vector3.zero);
            }

            bones[held].OverrideLimits = true;
            bones[held].Limits = new FluffyLimits
            {
                Twist = FluffyLimits.FreeRange,
                SwingY = new Vector2(-allowed, allowed),
                SwingZ = new Vector2(-allowed, allowed)
            };

            FluffyChain chain = _rig.BuildChain(_rig.CreatePose(FluffyLimits.Free, bones));
            FluffyProfile profile = _rig.CreateProfile(
                returnStrength: 8f, drag: 0.05f, gravity: new Vector3(0f, -30f, 0f));

            var states = new List<FluffyBoneState>();
            float widestOnTheHeldBone = 0f;
            float widestAnywhereElse = 0f;

            for (int step = 0; step < 240; step++)
            {
                chain.Simulate(SixtiethOfASecond, profile);

                states.Clear();
                chain.CaptureState(states);

                for (int i = 0; i < states.Count; i++)
                {
                    float swing = Mathf.Max(Mathf.Abs(states[i].SwingY), Mathf.Abs(states[i].SwingZ));

                    if (states[i].Index == held)
                    {
                        widestOnTheHeldBone = Mathf.Max(widestOnTheHeldBone, swing);
                    }
                    else
                    {
                        widestAnywhereElse = Mathf.Max(widestAnywhereElse, swing);
                    }
                }
            }

            Assert.That(widestOnTheHeldBone, Is.LessThanOrEqualTo(allowed + 0.5f),
                $"The bone with its own limits swung {widestOnTheHeldBone:0.##} degrees against a "
                + $"limit of {allowed}.");

            Assert.That(widestAnywhereElse, Is.GreaterThan(allowed * 2f),
                $"Nothing else in the chain got past {widestAnywhereElse:0.##} degrees, so the run "
                + "does not show that the override was what held the one bone.");
        }

        /// <summary>
        /// A captured pose puts the bones back where they were captured from, and
        /// clearing it forgets them.
        /// </summary>
        /// <remarks>
        /// This is the loop someone works in while posing a tail: bend, capture, play,
        /// come back, bend again. Losing the pose halfway through costs an afternoon, and
        /// the loss would be silent — the bones simply sit somewhere else.
        /// </remarks>
        [Test]
        public void AnAuthoredPoseComesBackExactlyAsItWasCaptured()
        {
            FluffyChain chain = _rig.BuildChain();
            List<Transform> bones = chain.GetBones();

            Assert.That(chain.HasDefaultPose, Is.False, "A fresh chain claimed to carry an authored pose.");

            // Bend the chain into a curve, the way it would be posed by hand.
            for (int i = 0; i < bones.Count; i++)
            {
                bones[i].localRotation = Quaternion.Euler(0f, 0f, 12f * i);
            }

            Assert.That(chain.CaptureDefaultPose(), Is.True);
            Assert.That(chain.HasDefaultPose, Is.True, "The captured pose was not kept.");

            var captured = new Quaternion[bones.Count];

            for (int i = 0; i < bones.Count; i++)
            {
                captured[i] = bones[i].localRotation;
                bones[i].localRotation = Quaternion.Euler(0f, 40f, 0f);
            }

            chain.ApplyDefaultPose();

            for (int i = 0; i < bones.Count; i++)
            {
                Assert.That(Quaternion.Angle(bones[i].localRotation, captured[i]), Is.LessThan(0.01f),
                    $"Bone '{bones[i].name}' came back {Quaternion.Angle(bones[i].localRotation, captured[i]):0.##} "
                    + "degrees away from where it was captured.");
            }

            chain.ClearDefaultPose();

            Assert.That(chain.HasDefaultPose, Is.False, "Clearing the pose left it behind.");
        }

        /// <summary>
        /// Copying one strand's settings onto another carries the pose and the dummy bone
        /// across, and leaves the bones where they are.
        /// </summary>
        /// <remarks>
        /// This is how a skirt is set up once instead of eight times, and the one thing
        /// it must never do is take the source's bones with it — every strand would end
        /// up simulating the first one, which looks like the skirt collapsing into a
        /// single fin.
        /// </remarks>
        [Test]
        public void CopyingSettingsAcrossLeavesEachStrandItsOwnBones()
        {
            FluffyPose shared = _rig.CreatePose(FluffyLimits.Free);

            FluffyChain source = _rig.BuildChain(shared);
            FluffyChain other = _rig.BuildChain();

            Transform ownStart = other.StartBone;
            Transform ownLast = other.LastBone;

            source.CopySettingsTo(other);

            Assert.That(other.Pose, Is.SameAs(shared), "The pose asset did not come across.");
            Assert.That(other.StartBone, Is.SameAs(ownStart), "The strand lost its own start bone.");
            Assert.That(other.LastBone, Is.SameAs(ownLast), "The strand lost its own last bone.");
        }
    }
}
