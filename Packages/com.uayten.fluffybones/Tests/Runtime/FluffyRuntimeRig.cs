using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Fluffy.Tests
{
    /// <summary>
    /// A character and a clock, for the tests that need the game to be running.
    /// </summary>
    /// <remarks>
    /// No editor API anywhere in here: this assembly is built for every platform, and a
    /// UnityEditor call in it stops the project building at all. Everything the tests
    /// need therefore has to be reachable from the public runtime surface — which is a
    /// useful constraint, since it is the same surface a customer has.
    /// </remarks>
    internal sealed class FluffyRuntimeRig
    {
        /// <summary>Bones per tail. The same shape the edit-mode rig builds.</summary>
        public const int BoneCount = 5;
        public const float BoneLength = 0.25f;

        private readonly List<Object> _created = new List<Object>();
        private float _captureDeltaTime;

        /// <summary>Remembers the editor's own frame pacing, to be put back afterwards.</summary>
        public void RememberTheClock()
        {
            _captureDeltaTime = Time.captureDeltaTime;
        }

        public void PutTheClockBackAndTidyUp()
        {
            // Left behind, this pins the frame rate for every test after this one and for
            // the editor itself.
            Time.captureDeltaTime = _captureDeltaTime;

            for (int i = 0; i < _created.Count; i++)
            {
                if (_created[i] != null)
                {
                    Object.Destroy(_created[i]);
                }
            }

            _created.Clear();
        }

        /// <summary>Hands the rig something it did not make, to be destroyed with the rest.</summary>
        public void Track(Object created)
        {
            _created.Add(created);
        }

        /// <summary>
        /// Runs <paramref name="frames"/> frames that each last <paramref name="length"/>.
        /// </summary>
        /// <remarks>
        /// One frame is thrown away first: <see cref="Time.captureDeltaTime"/> takes
        /// effect on the frame after the one it is set in, so the frame in progress still
        /// carries the old length. Asserting on that frame reads the previous rate and
        /// says nothing about the one being asked for.
        /// </remarks>
        public static IEnumerator FramesOf(float length, int frames)
        {
            Time.captureDeltaTime = length;
            yield return null;

            for (int frame = 0; frame < frames; frame++)
            {
                yield return null;
            }
        }

        /// <summary>The furthest any bone of the character sits from its pose.</summary>
        public static float WidestOffRest(FluffyBones body)
        {
            var states = new List<FluffyBoneState>();
            float widest = 0f;

            for (int c = 0; c < body.Chains.Count; c++)
            {
                states.Clear();
                body.Chains[c].CaptureState(states);

                for (int i = 0; i < states.Count; i++)
                {
                    widest = Mathf.Max(widest, states[i].OffRest);
                }
            }

            return widest;
        }

        /// <summary>
        /// A character with one tail, found the way a user finds it: by name.
        /// </summary>
        /// <remarks>
        /// Through <see cref="FluffyBones.DetectChains"/> rather than by assigning the
        /// chain list, which is private and serialized — and going through the public
        /// path means the detection keywords are covered by every test that builds one.
        /// </remarks>
        public FluffyBones BuildCharacter()
        {
            var character = new GameObject("Character");
            _created.Add(character);

            Transform parent = character.transform;

            for (int i = 0; i < BoneCount; i++)
            {
                var bone = new GameObject($"tail_{i:00}").transform;
                bone.SetParent(parent, false);
                bone.localPosition = i == 0 ? Vector3.zero : new Vector3(BoneLength, 0f, 0f);
                parent = bone;
            }

            FluffyBones body = character.AddComponent<FluffyBones>();

            // Defaults out of the asset: a return strength of 8, a drag of 0.15 and a
            // light downward gravity, none of which requires an editor API to set.
            var profile = ScriptableObject.CreateInstance<FluffyProfile>();
            _created.Add(profile);
            body.Profile = profile;

            Assert.That(body.DetectChains(), Is.EqualTo(1), "The tail was not detected as a chain.");

            body.Rebuild();
            body.ResetToRestPose();

            return body;
        }
    }
}
