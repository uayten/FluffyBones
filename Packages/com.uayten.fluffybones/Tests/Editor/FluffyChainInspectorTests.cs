using System.Collections.Generic;
using Fluffy.Editor;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Fluffy.Tests.Editor
{
    /// <summary>
    /// What the inspector hands the solver when someone presses the list's + button.
    /// </summary>
    public class FluffyChainInspectorTests
    {
        private const float SixtiethOfASecond = 1f / 60f;

        private readonly FluffyTestRig _rig = new FluffyTestRig();

        [TearDown]
        public void DestroyWhatTheTestMade()
        {
            _rig.DestroyEverything();
        }

        /// <summary>
        /// Unity builds a new list entry by zeroing it, and zeroed limits mean a bone
        /// that may not leave its pose.
        /// </summary>
        /// <remarks>
        /// Written down as its own test because it is the reason the fix-up below has to
        /// exist, and because it is the half that will still be true in five years: Unity
        /// is not going to start running a class's field initialisers for array elements.
        /// A range of 0 to 0 is a legitimate thing to author — a pinned bone — so nothing
        /// downstream can tell it apart from an accident and correct it. The correcting
        /// has to happen where the entry is made.
        /// </remarks>
        [Test]
        public void AnEntryUnityJustCreatedComesOutPinnedOnEveryAxis()
        {
            SerializedProperty chain = AddChainEntryTheWayTheButtonDoes(out _, out SerializedObject _);

            Assert.That(SwingYOf(chain), Is.EqualTo(Vector2.zero),
                "Unity started filling new entries in; the fix-up may no longer be needed.");
            Assert.That(FluffyLimits.IsFree(SwingYOf(chain)), Is.False);
        }

        /// <summary>
        /// The entry the inspector hands over is free to move on every axis, and carries
        /// the dummy bone settings a chain written in code would have.
        /// </summary>
        /// <remarks>
        /// The assertion that matters is the last one: the chain actually swings. Reading
        /// the fields back only says the fix-up wrote what it meant to write, and a limit
        /// that reads as free but does not behave as free is exactly the sort of thing
        /// these tests exist to catch.
        /// </remarks>
        [Test]
        public void AChainAddedFromTheInspectorIsNotBornFrozen()
        {
            SerializedProperty entry = AddChainEntryTheWayTheButtonDoes(
                out FluffyBones body, out SerializedObject serialized);

            FluffyBonesEditor.InitialiseChain(entry);
            serialized.ApplyModifiedPropertiesWithoutUndo();

            Assert.That(FluffyLimits.IsFree(SwingYOf(entry)), Is.True, "The Y swing came out restricted.");
            Assert.That(FluffyLimits.IsFree(SwingZOf(entry)), Is.True, "The Z swing came out restricted.");
            Assert.That(entry.FindPropertyRelative("_useDummyBone").boolValue, Is.True,
                "Without a dummy bone the last bone of the chain has nothing to swing towards.");
            Assert.That(entry.FindPropertyRelative("_dummyLength").floatValue,
                Is.EqualTo(FluffyChain.DefaultDummyLength).Within(1e-5f));

            // And now the part that a field read cannot tell you.
            FluffyChain chain = body.Chains[0];
            chain.StartBone = _rig.BuildSkeleton(FluffyTestRig.BoneCount);

            Assert.That(chain.Build(body), Is.True, "The chain from the inspector had nothing to simulate.");
            chain.ResetToRestPose();

            FluffyProfile profile = _rig.CreateProfile(
                returnStrength: 8f, drag: 0.15f, gravity: new Vector3(0f, -10f, 0f));

            var states = new List<FluffyBoneState>();
            float widest = 0f;

            for (int step = 0; step < 120; step++)
            {
                chain.Simulate(SixtiethOfASecond, profile);

                states.Clear();
                chain.CaptureState(states);

                for (int i = 0; i < states.Count; i++)
                {
                    widest = Mathf.Max(widest, states[i].OffRest);
                }
            }

            Assert.That(widest, Is.GreaterThan(1f),
                $"Two seconds of gravity moved the chain {widest:0.###} degrees. It is frozen.");
        }

        /// <summary>
        /// Grows the chain list the way the inspector's + button does and hands back the
        /// entry Unity made, along with the component and the serialized object it came
        /// from.
        /// </summary>
        /// <remarks>
        /// Through <see cref="SerializedObject"/> rather than by adding to the list in
        /// code, because the whole point is what Unity's own serializer puts in an entry
        /// it created. Adding a <c>new FluffyChain()</c> would run the field initialisers
        /// and test nothing.
        /// </remarks>
        private SerializedProperty AddChainEntryTheWayTheButtonDoes(
            out FluffyBones body, out SerializedObject serialized)
        {
            var character = new GameObject("Character");
            _rig.Track(character);

            body = character.AddComponent<FluffyBones>();
            serialized = new SerializedObject(body);

            SerializedProperty chains = serialized.FindProperty("_chains");
            chains.arraySize = 1;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            return serialized.FindProperty("_chains").GetArrayElementAtIndex(0);
        }

        private static Vector2 SwingYOf(SerializedProperty chain)
        {
            return chain.FindPropertyRelative("_globalLimits")
                .FindPropertyRelative(nameof(FluffyLimits.SwingY)).vector2Value;
        }

        private static Vector2 SwingZOf(SerializedProperty chain)
        {
            return chain.FindPropertyRelative("_globalLimits")
                .FindPropertyRelative(nameof(FluffyLimits.SwingZ)).vector2Value;
        }
    }
}
