using Fluffy.Editor;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Fluffy.Tests.Editor
{
    /// <summary>
    /// The pose every chain is given before anything has a chance to move its bones.
    /// </summary>
    /// <remarks>
    /// A chain with no pose of its own takes whatever its bones happen to be at the
    /// moment it is built, so anything that touches those transforms first quietly
    /// becomes the pose it springs back to. The inspector settles it by seeding every
    /// chain as soon as it is open, while the bones are still where the rig put them.
    ///
    /// This has wrecked a scene once already — seven strands of a skirt adopting a pose
    /// they had been left in by accident — and the damage is silent, which is what makes
    /// it worth a test. Nothing throws; the skirt simply hangs wrong from then on.
    /// </remarks>
    public class FluffyPoseSeedingTests
    {
        private readonly FluffyTestRig _rig = new FluffyTestRig();

        [TearDown]
        public void DestroyWhatTheTestMade()
        {
            _rig.DestroyEverything();
        }

        /// <summary>
        /// Every chain is seeded, not only the first — which is the one the pose tab
        /// happens to be showing.
        /// </summary>
        [Test]
        public void EveryChainIsSeededAndNotOnlyTheOneBeingEdited()
        {
            SerializedObject serialized = TwoChains(out Transform[] starts);

            // Bend both chains, so a pose read off the scene is distinguishable from a
            // pose of zeros.
            for (int c = 0; c < starts.Length; c++)
            {
                foreach (Transform bone in FluffyChain.CollectChain(starts[c]))
                {
                    bone.localRotation = Quaternion.Euler(0f, 0f, 9f * (c + 1));
                }
            }

            FluffyBonesEditor.SeedAllChains(serialized.FindProperty("_chains"));
            serialized.ApplyModifiedPropertiesWithoutUndo();

            SerializedProperty chains = serialized.FindProperty("_chains");

            for (int c = 0; c < chains.arraySize; c++)
            {
                SerializedProperty pose = chains.GetArrayElementAtIndex(c).FindPropertyRelative("_defaultPose");

                Assert.That(pose.arraySize, Is.EqualTo(FluffyTestRig.BoneCount),
                    $"Chain {c} was left with {pose.arraySize} pose entries for "
                    + $"{FluffyTestRig.BoneCount} bones.");

                var bones = FluffyChain.CollectChain(starts[c]);

                for (int i = 0; i < pose.arraySize; i++)
                {
                    Vector3 stored = pose.GetArrayElementAtIndex(i)
                        .FindPropertyRelative(nameof(FluffyBonePose.Rotation)).vector3Value;

                    Assert.That(Quaternion.Angle(Quaternion.Euler(stored), bones[i].localRotation),
                        Is.LessThan(0.01f),
                        $"Chain {c}, bone {i} was seeded with a rotation that is not the one the bone "
                        + "is sitting at.");
                }
            }
        }

        /// <summary>
        /// A seeded entry is free to move, which a zeroed one would not be.
        /// </summary>
        /// <remarks>
        /// The same trap the list's + button falls into: limits of 0 to 0 read as a bone
        /// pinned to its pose. Seeding writes the rotation and the limits together for
        /// exactly this reason.
        /// </remarks>
        [Test]
        public void ASeededEntryIsFreeToMove()
        {
            SerializedObject serialized = TwoChains(out _);

            FluffyBonesEditor.SeedAllChains(serialized.FindProperty("_chains"));
            serialized.ApplyModifiedPropertiesWithoutUndo();

            SerializedProperty entry = serialized.FindProperty("_chains")
                .GetArrayElementAtIndex(0)
                .FindPropertyRelative("_defaultPose")
                .GetArrayElementAtIndex(0);

            SerializedProperty limits = entry.FindPropertyRelative(nameof(FluffyBonePose.Limits));

            Assert.That(FluffyLimits.IsFree(
                    limits.FindPropertyRelative(nameof(FluffyLimits.SwingY)).vector2Value), Is.True,
                "A seeded bone came out pinned on Y.");
            Assert.That(FluffyLimits.IsFree(
                    limits.FindPropertyRelative(nameof(FluffyLimits.SwingZ)).vector2Value), Is.True,
                "A seeded bone came out pinned on Z.");
            Assert.That(entry.FindPropertyRelative(nameof(FluffyBonePose.OverrideLimits)).boolValue, Is.False,
                "A seeded bone was given an override nobody asked for.");
        }

        /// <summary>
        /// A chain reading a shared pose asset is left alone.
        /// </summary>
        /// <remarks>
        /// Its pose is a file, and the file belongs to every chain reading it — a skirt
        /// shares one across all eight strands. Seeding on behalf of one strand would
        /// write the other seven's rest pose from whatever this one's bones happened to
        /// be.
        /// </remarks>
        [Test]
        public void AChainOnASharedPoseAssetIsNotSeededOverTheTopOfIt()
        {
            SerializedObject serialized = TwoChains(out _);
            SerializedProperty chains = serialized.FindProperty("_chains");

            FluffyPose shared = _rig.CreatePose(FluffyLimits.Free);
            chains.GetArrayElementAtIndex(1).FindPropertyRelative("_pose").objectReferenceValue = shared;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            FluffyBonesEditor.SeedAllChains(chains);
            serialized.ApplyModifiedPropertiesWithoutUndo();

            Assert.That(chains.GetArrayElementAtIndex(0).FindPropertyRelative("_defaultPose").arraySize,
                Is.EqualTo(FluffyTestRig.BoneCount), "The chain without an asset was not seeded.");

            Assert.That(chains.GetArrayElementAtIndex(1).FindPropertyRelative("_defaultPose").arraySize,
                Is.Zero, "The chain on a shared asset had a private pose written underneath it.");

            Assert.That(shared.BoneCount, Is.Zero, "The shared asset itself was written to.");
        }

        /// <summary>
        /// A character with two chains, each on its own skeleton, the way a skirt of two
        /// strands is set up.
        /// </summary>
        private SerializedObject TwoChains(out Transform[] starts)
        {
            var character = new GameObject("Character");
            _rig.Track(character);

            FluffyBones body = character.AddComponent<FluffyBones>();
            var serialized = new SerializedObject(body);

            SerializedProperty chains = serialized.FindProperty("_chains");
            chains.arraySize = 2;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            starts = new Transform[2];

            for (int c = 0; c < 2; c++)
            {
                starts[c] = _rig.BuildSkeleton(FluffyTestRig.BoneCount);
                body.Chains[c].StartBone = starts[c];
            }

            // Written back, since the chains were reached as objects and the seeding reads
            // them as serialized properties.
            serialized.Update();

            return serialized;
        }
    }
}
