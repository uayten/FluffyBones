using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Fluffy.Tests
{
    /// <summary>
    /// The part of the plugin that only exists while the game is running: the frame
    /// arriving, the time being cut into steps, and the pose drawn between two of them.
    /// </summary>
    /// <remarks>
    /// Everything here goes through <see cref="FluffyBones"/> rather than reaching for a
    /// chain directly, because what is being tested is the bookkeeping around the solver
    /// and not the solver. Frame lengths come from <see cref="Time.captureDeltaTime"/>,
    /// which makes the editor hand every frame the same delta — the only way to ask
    /// "what happens at 30 frames a second" without owning a machine that runs at 30.
    /// </remarks>
    public class FluffyBodyPlayModeTests
    {
        /// <summary>Bones per tail. The same shape the edit-mode rig builds.</summary>
        private const int BoneCount = 5;
        private const float BoneLength = 0.25f;

        private readonly List<Object> _created = new List<Object>();
        private float _captureDeltaTime;

        [SetUp]
        public void RememberTheClock()
        {
            _captureDeltaTime = Time.captureDeltaTime;
        }

        [TearDown]
        public void PutTheClockBackAndTidyUp()
        {
            // Left behind, this pins the editor's frame rate for every test after this
            // one and for the editor itself.
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

        /// <summary>
        /// Two seconds at thirty frames a second and two at a hundred and twenty leave
        /// the chain in the same place.
        /// </summary>
        /// <remarks>
        /// The claim the fixed step was introduced to make, tested where it is actually
        /// made rather than one layer below it: the accumulator, the catch-up loop and
        /// the pose drawn between two steps all take part. A chain that hangs differently
        /// on a fast machine is the bug this catches, and it is the kind that never shows
        /// up on the machine it was authored on.
        /// </remarks>
        [UnityTest]
        public IEnumerator TheChainSettlesTheSameAtThirtyFramesASecondAndAtAHundredAndTwenty()
        {
            float atThirty = 0f;
            float atHundredAndTwenty = 0f;

            yield return SettleAndMeasure(1f / 30f, 2f, result => atThirty = result);
            yield return SettleAndMeasure(1f / 120f, 2f, result => atHundredAndTwenty = result);

            Assert.That(atHundredAndTwenty, Is.EqualTo(atThirty).Within(5f).Percent,
                $"Thirty frames a second left the chain {atThirty:0.###} degrees off its pose and a "
                + $"hundred and twenty left it {atHundredAndTwenty:0.###}.");
        }

        /// <summary>
        /// A frame long enough to stall the editor does not let the chain run away, and
        /// the frame after it is normal again.
        /// </summary>
        /// <remarks>
        /// A second-long frame owes the solver sixty steps. Running all of them makes the
        /// frame longer still, which makes the next one owe more — the spiral the step
        /// cap exists to stop. What the cap costs is that the chain moves in slow motion
        /// through a stall, which is the right trade and worth pinning down: the
        /// alternative is a skirt that catches up by teleporting.
        /// </remarks>
        [UnityTest]
        public IEnumerator AStalledFrameIsCappedRatherThanCaughtUpWith()
        {
            FluffyBones body = BuildCharacter();

            yield return FramesOf(1f / 60f, 1);

            // One frame that took a whole second, the way an asset import or a breakpoint
            // hands one over.
            yield return FramesOf(1f, 1);

            Assert.That(body.LastStepCount, Is.EqualTo(8),
                $"A one second frame ran {body.LastStepCount} steps where the cap is 8.");

            float afterTheStall = WidestOffRest(body);

            Assert.That(float.IsNaN(afterTheStall), Is.False, "The chain came out of the stall as NaN.");
            Assert.That(afterTheStall, Is.LessThan(90f),
                $"The chain was {afterTheStall:0.#} degrees off its pose after one stalled frame.");

            yield return FramesOf(1f / 60f, 1);

            Assert.That(body.LastStepCount, Is.LessThanOrEqualTo(1),
                $"The frame after the stall still ran {body.LastStepCount} steps, so the debt was "
                + "carried over instead of dropped.");
        }

        /// <summary>
        /// A character that jumps further than its teleport distance takes the chain with
        /// it instead of leaving it behind.
        /// </summary>
        /// <remarks>
        /// The solver has no sensible swing for a head that moved further than the chain
        /// is long, and left to it the tail whips across the level and takes a second to
        /// come back. Every respawn, every cutscene cut and every fast-travel does this.
        ///
        /// Against a second character that stays put rather than against the jumper's own
        /// previous frame: a chain that is still settling keeps moving on its own, and
        /// that movement would otherwise be counted as damage done by the jump. Both are
        /// built the same way and stepped in the same frames, so whatever the settling is
        /// doing, it is doing it to both.
        /// </remarks>
        [UnityTest]
        public IEnumerator AJumpBeyondTheTeleportDistanceCarriesTheChainAlong()
        {
            FluffyBones jumper = BuildCharacter();
            FluffyBones stayer = BuildCharacter();

            yield return FramesOf(1f / 60f, 30);

            // Far more than the default teleport distance of one unit, and far more than
            // the chain is long.
            jumper.transform.position += new Vector3(50f, 0f, 0f);
            yield return null;

            Assert.That(jumper.CarriedLastFrame, Is.True, "The jump was not recognised as one.");
            Assert.That(stayer.CarriedLastFrame, Is.False, "Standing still was mistaken for a jump.");

            float jumped = WidestOffRest(jumper);
            float stayed = WidestOffRest(stayer);

            Assert.That(jumped, Is.EqualTo(stayed).Within(0.5f),
                $"The chain that jumped ended {jumped:0.###} degrees off its pose where the one that "
                + $"stayed put ended {stayed:0.###}. The jump was felt by the chain.");
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
        private static IEnumerator FramesOf(float length, int frames)
        {
            Time.captureDeltaTime = length;
            yield return null;

            for (int frame = 0; frame < frames; frame++)
            {
                yield return null;
            }
        }

        /// <summary>
        /// Runs the character for <paramref name="seconds"/> at a given frame length and
        /// hands back how far off its pose the chain ended up.
        /// </summary>
        private IEnumerator SettleAndMeasure(float frameLength, float seconds, System.Action<float> report)
        {
            FluffyBones body = BuildCharacter();

            yield return FramesOf(frameLength, Mathf.RoundToInt(seconds / frameLength));

            report(WidestOffRest(body));

            Object.Destroy(body.gameObject);
            yield return null;
        }

        /// <summary>The furthest any bone of the character's first chain sits from its pose.</summary>
        private static float WidestOffRest(FluffyBones body)
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
        /// path means the detection keywords are covered by every test in the file.
        /// </remarks>
        private FluffyBones BuildCharacter()
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
            // light downward gravity, which is all this file needs and none of which
            // requires an editor API to set.
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
