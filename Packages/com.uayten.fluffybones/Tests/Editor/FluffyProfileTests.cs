using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Fluffy.Tests.Editor
{
    /// <summary>
    /// The profile's own arithmetic, which every chain reads on every step.
    /// </summary>
    public class FluffyProfileTests
    {
        private readonly FluffyTestRig _rig = new FluffyTestRig();

        [TearDown]
        public void DestroyWhatTheTestMade()
        {
            _rig.DestroyEverything();
        }

        /// <summary>
        /// The falloff scales the return strength along the chain, and a curve with no
        /// keys leaves it alone instead of zeroing it.
        /// </summary>
        /// <remarks>
        /// An empty curve is what an inspector leaves behind when someone deletes the
        /// last key, and <c>AnimationCurve.Evaluate</c> answers 0 to everything in that
        /// state. Multiplied through, that is a chain with no spring at all — limp, and
        /// with no error anywhere to explain it. The guard exists; this is what keeps it.
        /// </remarks>
        [Test]
        public void AFalloffCurveWithNoKeysLeavesTheReturnStrengthAlone()
        {
            FluffyProfile profile = _rig.CreateProfile(returnStrength: 8f, drag: 0.15f, gravity: Vector3.zero);

            var serialized = new SerializedObject(profile);
            serialized.FindProperty("_returnStrengthFalloff").animationCurveValue = new AnimationCurve();
            serialized.ApplyModifiedPropertiesWithoutUndo();

            Assert.That(profile.ReturnStrengthFalloff.length, Is.Zero, "The curve kept a key somehow.");

            Assert.That(profile.EvaluateReturnStrength(0f), Is.EqualTo(8f).Within(1e-4f),
                "An empty falloff curve zeroed the spring at the start of the chain.");
            Assert.That(profile.EvaluateReturnStrength(1f), Is.EqualTo(8f).Within(1e-4f),
                "An empty falloff curve zeroed the spring at the end of the chain.");
        }

        /// <summary>
        /// A falloff that drops along the chain is what makes the end whip: less spring
        /// at the tip than at the root.
        /// </summary>
        [Test]
        public void AFalloffThatDropsLeavesLessSpringAtTheEndThanAtTheStart()
        {
            FluffyProfile profile = _rig.CreateProfile(returnStrength: 10f, drag: 0.15f, gravity: Vector3.zero);

            var serialized = new SerializedObject(profile);
            serialized.FindProperty("_returnStrengthFalloff").animationCurveValue =
                AnimationCurve.Linear(0f, 1f, 1f, 0.25f);
            serialized.ApplyModifiedPropertiesWithoutUndo();

            float atTheStart = profile.EvaluateReturnStrength(0f);
            float atTheEnd = profile.EvaluateReturnStrength(1f);

            Assert.That(atTheStart, Is.EqualTo(10f).Within(1e-3f));
            Assert.That(atTheEnd, Is.EqualTo(2.5f).Within(1e-3f));
            Assert.That(atTheEnd, Is.LessThan(atTheStart));
        }
    }
}
