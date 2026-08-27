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
        private readonly FluffyRuntimeRig _rig = new FluffyRuntimeRig();

        [SetUp]
        public void RememberTheClock()
        {
            _rig.RememberTheClock();
        }

        [TearDown]
        public void PutTheClockBackAndTidyUp()
        {
            _rig.PutTheClockBackAndTidyUp();
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
            FluffyBones body = _rig.BuildCharacter();

            yield return FluffyRuntimeRig.FramesOf(1f / 60f, 1);

            // One frame that took a whole second, the way an asset import or a breakpoint
            // hands one over.
            yield return FluffyRuntimeRig.FramesOf(1f, 1);

            Assert.That(body.LastStepCount, Is.EqualTo(8),
                $"A one second frame ran {body.LastStepCount} steps where the cap is 8.");

            float afterTheStall = FluffyRuntimeRig.WidestOffRest(body);

            Assert.That(float.IsNaN(afterTheStall), Is.False, "The chain came out of the stall as NaN.");
            Assert.That(afterTheStall, Is.LessThan(90f),
                $"The chain was {afterTheStall:0.#} degrees off its pose after one stalled frame.");

            yield return FluffyRuntimeRig.FramesOf(1f / 60f, 1);

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
            FluffyBones jumper = _rig.BuildCharacter();
            FluffyBones stayer = _rig.BuildCharacter();

            yield return FluffyRuntimeRig.FramesOf(1f / 60f, 30);

            // Far more than the default teleport distance of one unit, and far more than
            // the chain is long.
            jumper.transform.position += new Vector3(50f, 0f, 0f);
            yield return null;

            Assert.That(jumper.CarriedLastFrame, Is.True, "The jump was not recognised as one.");
            Assert.That(stayer.CarriedLastFrame, Is.False, "Standing still was mistaken for a jump.");

            float jumped = FluffyRuntimeRig.WidestOffRest(jumper);
            float stayed = FluffyRuntimeRig.WidestOffRest(stayer);

            Assert.That(jumped, Is.EqualTo(stayed).Within(0.5f),
                $"The chain that jumped ended {jumped:0.###} degrees off its pose where the one that "
                + $"stayed put ended {stayed:0.###}. The jump was felt by the chain.");
        }

        /// <summary>
        /// The character finds its collision shapes when it is built, and keeps its chain
        /// out of them while it runs.
        /// </summary>
        /// <remarks>
        /// Found at build rather than per frame: a hierarchy search per chain per step is
        /// the cost that does not show up on the one character being tested and does show
        /// up on a crowd. The price is that a shape added later needs a rebuild, which is
        /// the same rule the chains follow, and the test says so by adding one and
        /// checking it is not picked up until then.
        /// </remarks>
        [UnityTest]
        public IEnumerator TheCharacterFindsItsCollidersAtBuildAndStaysOutOfThem()
        {
            FluffyBones body = _rig.BuildCharacter();

            Assert.That(body.Colliders, Is.Empty, "A character with no shapes on it found some.");

            var host = new GameObject("Hip");
            host.transform.SetParent(body.transform, false);
            host.transform.localPosition = new Vector3(0.5f, -0.3f, 0f);

            FluffyCollider sphere = host.AddComponent<FluffyCollider>();
            sphere.Shape = FluffyColliderShape.Sphere;
            sphere.Radius = 0.3f;

            yield return FluffyRuntimeRig.FramesOf(1f / 60f, 2);

            Assert.That(body.Colliders, Is.Empty,
                "A shape added after the build was picked up without one, so the search is happening "
                + "per frame after all.");

            body.Rebuild();

            Assert.That(body.Colliders, Has.Count.EqualTo(1), "A rebuild did not find the shape.");

            var states = new List<FluffyBoneState>();
            float deepest = 0f;

            for (int frame = 0; frame < 120; frame++)
            {
                yield return null;

                for (int c = 0; c < body.Chains.Count; c++)
                {
                    states.Clear();
                    body.Chains[c].CaptureState(states);

                    for (int i = 0; i < states.Count; i++)
                    {
                        Vector3 tip = states[i].Head + states[i].Direction * states[i].Length;
                        deepest = Mathf.Max(deepest,
                            sphere.WorldRadius - Vector3.Distance(tip, sphere.WorldCentre));
                    }
                }
            }

            Assert.That(deepest, Is.LessThan(0.02f),
                $"A tip spent the run {deepest:0.####} units inside the shape.");
        }

        /// <summary>
        /// Runs the character for <paramref name="seconds"/> at a given frame length and
        /// hands back how far off its pose the chain ended up.
        /// </summary>
        private IEnumerator SettleAndMeasure(float frameLength, float seconds, System.Action<float> report)
        {
            FluffyBones body = _rig.BuildCharacter();

            yield return FluffyRuntimeRig.FramesOf(frameLength, Mathf.RoundToInt(seconds / frameLength));

            report(FluffyRuntimeRig.WidestOffRest(body));

            Object.Destroy(body.gameObject);
            yield return null;
        }
    }
}
