using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Fluffy.Tests.Editor
{
    /// <summary>
    /// The objects a solver test needs, and the tearing down of them afterwards.
    /// </summary>
    /// <remarks>
    /// Skeletons are built here rather than loaded from the playground scene, so a test
    /// says what it depends on and nobody breaks one by dragging a bone. Everything made
    /// goes on a list: an edit-mode test leaves its objects in whatever scene the editor
    /// happens to have open, so anything created has to be destroyed by hand.
    /// </remarks>
    internal sealed class FluffyTestRig
    {
        /// <summary>Length of every bone the rig builds, in world units.</summary>
        public const float BoneLength = 0.25f;

        /// <summary>Bones per chain. Enough for the end to lag behind the start.</summary>
        public const int BoneCount = 5;

        private readonly List<Object> _created = new List<Object>();

        /// <summary>
        /// A chain of bones laid out along X.
        /// </summary>
        /// <remarks>
        /// Along X so that a downward gravity swings the chain rather than pulling it
        /// along its own length, where a rigid bone has nowhere to go and the test would
        /// be watching nothing happen.
        /// </remarks>
        /// <param name="pose">Assigned before building, since the rest rotations are read from it.</param>
        public FluffyChain BuildChain(FluffyPose pose = null)
        {
            var chain = new FluffyChain(BuildSkeleton(BoneCount)) { Pose = pose };

            Assert.That(chain.Build(), Is.True, "The test skeleton produced nothing to simulate.");
            chain.ResetToRestPose();

            return chain;
        }

        /// <summary>
        /// A chain that came through Unity's serializer on a real component, rather than
        /// one built by a constructor.
        /// </summary>
        /// <remarks>
        /// The only way to reach the settings that have no public setter, and the way a
        /// user's chain is actually made. The limits have to be written by hand because
        /// Unity zeroes a new array element, and zeroed limits are a frozen bone — the
        /// inspector has its own fix-up for that, which is tested elsewhere.
        /// </remarks>
        public FluffyChain BuildChainThroughAComponent(bool useDummyBone, out FluffyBones body)
        {
            var character = new GameObject("Character");
            _created.Add(character);

            body = character.AddComponent<FluffyBones>();

            var serialized = new SerializedObject(body);
            SerializedProperty chains = serialized.FindProperty("_chains");
            chains.arraySize = 1;

            SerializedProperty entry = chains.GetArrayElementAtIndex(0);
            SerializedProperty limits = entry.FindPropertyRelative("_globalLimits");
            limits.FindPropertyRelative(nameof(FluffyLimits.Twist)).vector2Value = FluffyLimits.FreeRange;
            limits.FindPropertyRelative(nameof(FluffyLimits.SwingY)).vector2Value = FluffyLimits.FreeRange;
            limits.FindPropertyRelative(nameof(FluffyLimits.SwingZ)).vector2Value = FluffyLimits.FreeRange;
            entry.FindPropertyRelative("_useDummyBone").boolValue = useDummyBone;
            entry.FindPropertyRelative("_autoDummyLength").boolValue = true;
            entry.FindPropertyRelative("_dummyLength").floatValue = FluffyChain.DefaultDummyLength;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            FluffyChain chain = body.Chains[0];
            chain.StartBone = BuildSkeleton(BoneCount);

            return chain;
        }

        /// <summary>
        /// A straight run of bones along X, and the first of them.
        /// </summary>
        /// <param name="boneCount">How many bones. One is the case a chain refuses.</param>
        public Transform BuildSkeleton(int boneCount)
        {
            var root = new GameObject("Chain Root");
            _created.Add(root);

            Transform parent = root.transform;
            Transform start = null;

            for (int i = 0; i < boneCount; i++)
            {
                Transform bone = AddBone(parent, $"bone_{i:00}", i == 0 ? Vector3.zero : new Vector3(BoneLength, 0f, 0f));

                if (i == 0)
                {
                    start = bone;
                }

                parent = bone;
            }

            return start;
        }

        /// <summary>Hands the rig something it did not make, to be destroyed with the rest.</summary>
        public void Track(Object created)
        {
            _created.Add(created);
        }

        /// <summary>One more bone under <paramref name="parent"/>, for hand-built shapes.</summary>
        public Transform AddBone(Transform parent, string name, Vector3 localPosition)
        {
            var bone = new GameObject(name).transform;
            bone.SetParent(parent, false);
            bone.localPosition = localPosition;

            return bone;
        }

        /// <summary>
        /// A profile with the values the test wants.
        /// </summary>
        /// <remarks>
        /// Through <see cref="SerializedObject"/> because a profile is authoring data and
        /// exposes only getters — which is right, and worth going around here rather than
        /// widening the runtime API for the benefit of a test.
        /// </remarks>
        public FluffyProfile CreateProfile(float returnStrength, float drag, Vector3 gravity)
        {
            var profile = ScriptableObject.CreateInstance<FluffyProfile>();
            _created.Add(profile);

            var serialized = new SerializedObject(profile);
            serialized.FindProperty("_returnStrength").floatValue = returnStrength;
            serialized.FindProperty("_drag").floatValue = drag;
            serialized.FindProperty("_gravity").vector3Value = gravity;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            return profile;
        }

        /// <summary>
        /// A pose asset carrying nothing but the limits every bone of the chain takes.
        /// </summary>
        /// <remarks>
        /// Without bone entries every bone falls through to the global limits, which is
        /// the common case — a chain whose bones each carry their own is the exception.
        /// Pass <paramref name="bones"/> to test that exception.
        /// </remarks>
        public FluffyPose CreatePose(FluffyLimits globalLimits, FluffyBonePose[] bones = null)
        {
            var pose = ScriptableObject.CreateInstance<FluffyPose>();
            _created.Add(pose);

            if (bones != null)
            {
                pose.SetBones(bones);
            }

            var serialized = new SerializedObject(pose);
            SerializedProperty limits = serialized.FindProperty("_globalLimits");
            limits.FindPropertyRelative("Twist").vector2Value = globalLimits.Twist;
            limits.FindPropertyRelative("SwingY").vector2Value = globalLimits.SwingY;
            limits.FindPropertyRelative("SwingZ").vector2Value = globalLimits.SwingZ;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            return pose;
        }

        public void DestroyEverything()
        {
            for (int i = 0; i < _created.Count; i++)
            {
                if (_created[i] != null)
                {
                    Object.DestroyImmediate(_created[i]);
                }
            }

            _created.Clear();
        }
    }
}
