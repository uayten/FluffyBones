using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Fluffy.Tests.Editor
{
    /// <summary>
    /// The shapes a chain is kept out of, and what happens when they disagree with the
    /// limits.
    /// </summary>
    /// <remarks>
    /// "The skirt goes through the leg" is the first thing anyone reports, and the shape
    /// of the fix is a push out along the shortest way. What makes it worth testing
    /// carefully is not the push — that is arithmetic — but everything around it: that a
    /// bone stays its own length afterwards, that a shape a bone is exactly inside the
    /// centre of does not produce a NaN, and that a push the angle limits forbid loses to
    /// the limits rather than winning.
    /// </remarks>
    public class FluffyColliderTests
    {
        private const float SixtiethOfASecond = 1f / 60f;

        private readonly FluffyTestRig _rig = new FluffyTestRig();

        [TearDown]
        public void DestroyWhatTheTestMade()
        {
            _rig.DestroyEverything();
        }

        /// <summary>
        /// A point inside a sphere comes out on its surface, along the line from the
        /// centre.
        /// </summary>
        [Test]
        public void ASphereShovesAPointOutToItsSurface()
        {
            FluffyCollider sphere = Sphere(Vector3.zero, radius: 1f);

            var point = new Vector3(0.25f, 0f, 0f);

            Assert.That(sphere.PushOut(ref point), Is.True, "A point at a quarter of the radius was left alone.");
            Assert.That(point.magnitude, Is.EqualTo(1f).Within(1e-4f));
            Assert.That(point.normalized, Is.EqualTo(Vector3.right).Using(DirectionComparer),
                "The point left along a direction that is not the one it went in on.");

            var outside = new Vector3(3f, 0f, 0f);

            Assert.That(sphere.PushOut(ref outside), Is.False, "A point well outside was moved anyway.");
            Assert.That(outside, Is.EqualTo(new Vector3(3f, 0f, 0f)));
        }

        /// <summary>
        /// The chain's own thickness is added to the shape's, so a rope keeps its width
        /// off the surface rather than sinking in to the middle.
        /// </summary>
        [Test]
        public void TheChainsOwnRadiusIsAddedToTheShapes()
        {
            FluffyCollider sphere = Sphere(Vector3.zero, radius: 1f);

            var point = new Vector3(0.5f, 0f, 0f);
            sphere.PushOut(ref point, thickness: 0.25f);

            Assert.That(point.magnitude, Is.EqualTo(1.25f).Within(1e-4f));
        }

        /// <summary>
        /// A point exactly at the centre has no shortest way out, and gets one anyway.
        /// </summary>
        /// <remarks>
        /// A normalise of a zero vector is a NaN, and a NaN in a tip spreads to every
        /// bone below it within a frame and to the bone's transform, which Unity then
        /// complains about forever. Rare, and cheap to be right about.
        /// </remarks>
        [Test]
        public void APointAtTheCentreLeavesInSomeDirectionRatherThanAsNaN()
        {
            FluffyCollider capsule = Capsule(Vector3.zero, radius: 0.5f, height: 2f, FluffyAxis.Y);

            Vector3 point = capsule.WorldCentre;

            Assert.That(capsule.PushOut(ref point), Is.True);
            Assert.That(float.IsNaN(point.x) || float.IsNaN(point.y) || float.IsNaN(point.z), Is.False,
                "A point at the centre came out as NaN.");
            Assert.That(Vector3.Distance(point, capsule.WorldCentre), Is.GreaterThan(0.1f),
                "A point at the centre was left inside the shape.");
        }

        /// <summary>
        /// A capsule pushes out from the nearest point of its line, so the sides push
        /// sideways and the ends push like a sphere.
        /// </summary>
        [Test]
        public void ACapsulePushesOutFromTheNearestPointOfItsLine()
        {
            FluffyCollider capsule = Capsule(Vector3.zero, radius: 0.5f, height: 3f, FluffyAxis.Y);

            // Beside the middle: straight out sideways, and no higher than it went in.
            var beside = new Vector3(0.1f, 0.4f, 0f);
            Assert.That(capsule.PushOut(ref beside), Is.True);
            Assert.That(beside.y, Is.EqualTo(0.4f).Within(1e-4f),
                "A point beside the capsule was moved along it instead of away from it.");
            Assert.That(new Vector2(beside.x, beside.z).magnitude, Is.EqualTo(0.5f).Within(1e-4f));

            // Past the end: the cap behaves as a sphere around the end of the line.
            capsule.WorldSegment(out _, out Vector3 top);
            var above = top + new Vector3(0f, 0.2f, 0f);
            Assert.That(capsule.PushOut(ref above), Is.True);
            Assert.That(Vector3.Distance(above, top), Is.EqualTo(0.5f).Within(1e-4f));
        }

        /// <summary>
        /// A chain solved against a sphere in its way ends up outside it, and its bones
        /// keep their length.
        /// </summary>
        /// <remarks>
        /// The length is the half worth watching. A push moves a tip off the sphere of
        /// its own bone's reach, and a solver that accepted that would grow the chain a
        /// little on every step it touched a collider — a skirt that gets longer while
        /// you walk past a table.
        /// </remarks>
        [Test]
        public void AChainSolvedAgainstASphereEndsUpOutsideItWithItsLengthIntact()
        {
            FluffyChain chain = _rig.BuildChain();

            // Right under the chain, so the gravity below drags the bones into it.
            FluffyCollider sphere = Sphere(new Vector3(0.5f, -0.35f, 0f), radius: 0.35f);
            var colliders = new List<FluffyCollider> { sphere };

            FluffyProfile profile = _rig.CreateProfile(
                returnStrength: 4f, drag: 0.1f, gravity: new Vector3(0f, -20f, 0f));

            var states = new List<FluffyBoneState>();
            float deepest = 0f;

            for (int step = 0; step < 240; step++)
            {
                chain.Simulate(SixtiethOfASecond, profile, colliders);

                states.Clear();
                chain.CaptureState(states);

                for (int i = 0; i < states.Count; i++)
                {
                    Vector3 tip = states[i].Head + states[i].Direction * states[i].Length;
                    float inside = sphere.WorldRadius - Vector3.Distance(tip, sphere.WorldCentre);

                    deepest = Mathf.Max(deepest, inside);

                    Assert.That(states[i].Length, Is.EqualTo(FluffyTestRig.BoneLength).Within(1e-3f),
                        $"Bone {i} changed length while being pushed around.");
                }
            }

            Assert.That(deepest, Is.LessThan(0.01f),
                $"A tip ended up {deepest:0.####} units inside a shape it was supposed to stay out of.");
        }

        /// <summary>
        /// When the shape and the limits disagree, the limits win.
        /// </summary>
        /// <remarks>
        /// The rule, stated the way the roadmap stated it. A bone shoved somewhere its
        /// cone forbids is a skirt strand folding backwards through the hip, which reads
        /// as broken; a strand held at its limit and slightly inside a leg reads as a
        /// strand resting against a leg. So the test wants both halves at once: the bone
        /// stays inside its cone, and it does end up inside the shape — otherwise the
        /// collider was simply not in the way and the case never happened.
        /// </remarks>
        [Test]
        public void WhenTheShapeAndTheLimitsDisagreeTheLimitsWin()
        {
            const float allowed = 8f;

            FluffyLimits limits = FluffyLimits.Free;
            limits.SwingY = new Vector2(-allowed, allowed);
            limits.SwingZ = new Vector2(-allowed, allowed);

            FluffyChain chain = _rig.BuildChain(_rig.CreatePose(limits));

            // Swallowing the whole chain: there is nowhere inside the cone that is also
            // outside this, so the two cannot both be satisfied.
            FluffyCollider sphere = Sphere(new Vector3(0.6f, 0f, 0f), radius: 1.5f);
            var colliders = new List<FluffyCollider> { sphere };

            FluffyProfile profile = _rig.CreateProfile(
                returnStrength: 8f, drag: 0.1f, gravity: new Vector3(0f, -20f, 0f));

            var states = new List<FluffyBoneState>();
            float widestSwing = 0f;
            bool anythingStayedInside = false;

            for (int step = 0; step < 240; step++)
            {
                chain.Simulate(SixtiethOfASecond, profile, colliders);

                states.Clear();
                chain.CaptureState(states);

                for (int i = 0; i < states.Count; i++)
                {
                    widestSwing = Mathf.Max(widestSwing, Mathf.Abs(states[i].SwingY));
                    widestSwing = Mathf.Max(widestSwing, Mathf.Abs(states[i].SwingZ));

                    Vector3 tip = states[i].Head + states[i].Direction * states[i].Length;

                    if (Vector3.Distance(tip, sphere.WorldCentre) < sphere.WorldRadius - 0.05f)
                    {
                        anythingStayedInside = true;
                    }
                }
            }

            Assert.That(widestSwing, Is.LessThanOrEqualTo(allowed + 0.5f),
                $"A collider pushed a bone to {widestSwing:0.##} degrees, past a limit of {allowed}. "
                + "The shape won an argument it is supposed to lose.");

            Assert.That(anythingStayedInside, Is.True,
                "Nothing ended up inside the shape, so the two never actually disagreed and the test "
                + "proves nothing.");
        }

        /// <summary>
        /// A chain with no colliders behaves exactly as it did before there were any.
        /// </summary>
        [Test]
        public void AChainWithNothingInItsWayIsUnaffected()
        {
            FluffyProfile profile = _rig.CreateProfile(
                returnStrength: 8f, drag: 0.15f, gravity: new Vector3(0f, -10f, 0f));

            float withNull = RunAndReturnWidestOffRest(_rig.BuildChain(), profile, null);
            float withEmpty = RunAndReturnWidestOffRest(
                _rig.BuildChain(), profile, new List<FluffyCollider>());

            Assert.That(withEmpty, Is.EqualTo(withNull).Within(1e-4f),
                "An empty collider list moved the chain somewhere a null one did not.");
            Assert.That(withNull, Is.GreaterThan(1f), "The chain never moved, so nothing was compared.");
        }

        /// <summary>
        /// A collider switched off is not in anybody's way.
        /// </summary>
        /// <remarks>
        /// The list is collected at build and read every step, so a shape can be disabled
        /// between the two — which is how a rig turns off a limb's collider for one
        /// animation. A destroyed one has to be survivable for the same reason.
        /// </remarks>
        [Test]
        public void ADisabledOrDestroyedColliderIsSkipped()
        {
            FluffyProfile profile = _rig.CreateProfile(
                returnStrength: 8f, drag: 0.15f, gravity: new Vector3(0f, -10f, 0f));

            FluffyCollider disabled = Sphere(new Vector3(0.5f, -0.2f, 0f), radius: 0.5f);
            disabled.enabled = false;

            var colliders = new List<FluffyCollider> { disabled, null };

            float free = RunAndReturnWidestOffRest(_rig.BuildChain(), profile, null);
            float withDisabled = RunAndReturnWidestOffRest(_rig.BuildChain(), profile, colliders);

            Assert.That(withDisabled, Is.EqualTo(free).Within(1e-4f),
                "A disabled collider pushed the chain around.");
        }

        private static float RunAndReturnWidestOffRest(
            FluffyChain chain, FluffyProfile profile, List<FluffyCollider> colliders)
        {
            var states = new List<FluffyBoneState>();
            float widest = 0f;

            for (int step = 0; step < 120; step++)
            {
                chain.Simulate(SixtiethOfASecond, profile, colliders);

                states.Clear();
                chain.CaptureState(states);

                for (int i = 0; i < states.Count; i++)
                {
                    widest = Mathf.Max(widest, states[i].OffRest);
                }
            }

            return widest;
        }

        private FluffyCollider Sphere(Vector3 at, float radius)
        {
            var host = new GameObject("Sphere");
            _rig.Track(host);
            host.transform.position = at;

            FluffyCollider collider = host.AddComponent<FluffyCollider>();
            collider.Shape = FluffyColliderShape.Sphere;
            collider.Radius = radius;

            return collider;
        }

        private FluffyCollider Capsule(Vector3 at, float radius, float height, FluffyAxis direction)
        {
            var host = new GameObject("Capsule");
            _rig.Track(host);
            host.transform.position = at;

            FluffyCollider collider = host.AddComponent<FluffyCollider>();
            collider.Shape = FluffyColliderShape.Capsule;
            collider.Radius = radius;
            collider.Height = height;
            collider.Direction = direction;

            return collider;
        }

        /// <summary>Compares directions by angle, since component equality is too strict.</summary>
        private static readonly IEqualityComparer<Vector3> DirectionComparer = new ByAngle();

        private class ByAngle : IEqualityComparer<Vector3>
        {
            public bool Equals(Vector3 a, Vector3 b) => Vector3.Angle(a, b) < 0.01f;

            public int GetHashCode(Vector3 value) => 0;
        }
    }
}
