using Fluffy.Editor;
using NUnit.Framework;
using UnityEngine;

namespace Fluffy.Tests.Editor
{
    /// <summary>
    /// What the bone slot accepts when something is dropped on it.
    /// </summary>
    /// <remarks>
    /// The rule the field exists to enforce is that a chain belongs to one character. A
    /// bone from another character in the same scene resolves, builds and simulates
    /// perfectly happily — and produces a tail that reaches across the level to a body
    /// that is not its own. Nothing downstream can catch that, because nothing
    /// downstream knows which character the chain was supposed to belong to.
    /// </remarks>
    public class FluffyBoneFieldTests
    {
        private readonly FluffyTestRig _rig = new FluffyTestRig();

        [TearDown]
        public void DestroyWhatTheTestMade()
        {
            _rig.DestroyEverything();
        }

        [Test]
        public void ABoneOfTheCharacterIsAccepted()
        {
            Transform start = _rig.BuildSkeleton(3);
            Transform root = start.root;
            Transform middle = start.GetChild(0);

            Assert.That(FluffyBoneField.ResolveBone(new Object[] { middle }, root), Is.SameAs(middle));
        }

        /// <summary>
        /// A dragged GameObject counts as its transform, since that is what the hierarchy
        /// hands over.
        /// </summary>
        [Test]
        public void AGameObjectDraggedFromTheHierarchyResolvesToItsTransform()
        {
            Transform start = _rig.BuildSkeleton(3);
            Transform root = start.root;
            Transform middle = start.GetChild(0);

            Assert.That(FluffyBoneField.ResolveBone(new Object[] { middle.gameObject }, root),
                Is.SameAs(middle));
        }

        /// <summary>
        /// A bone belonging to another character is refused, and refused even when it
        /// arrives first in the same drag.
        /// </summary>
        [Test]
        public void ABoneFromAnotherCharacterIsRefused()
        {
            Transform mine = _rig.BuildSkeleton(3);
            Transform root = mine.root;
            Transform stranger = _rig.BuildSkeleton(3);

            Assert.That(FluffyBoneField.ResolveBone(new Object[] { stranger }, root), Is.Null,
                "A bone from another character was accepted into this one's chain.");

            Assert.That(FluffyBoneField.ResolveBone(new Object[] { stranger, mine.GetChild(0) }, root),
                Is.SameAs(mine.GetChild(0)),
                "A stranger arriving first in the drag hid the character's own bone behind it.");
        }

        /// <summary>
        /// Anything that is not part of a scene hierarchy is refused rather than reached
        /// into.
        /// </summary>
        /// <remarks>
        /// Dropping an asset on a bone slot is an ordinary slip — the project window is
        /// right there — and the answer has to be nothing rather than an exception.
        /// </remarks>
        [Test]
        public void SomethingThatIsNotABoneAtAllIsRefused()
        {
            Transform root = _rig.BuildSkeleton(3).root;

            var asset = ScriptableObject.CreateInstance<FluffyProfile>();
            _rig.Track(asset);

            Assert.That(FluffyBoneField.ResolveBone(new Object[] { asset }, root), Is.Null);
            Assert.That(FluffyBoneField.ResolveBone(new Object[] { null }, root), Is.Null);
            Assert.That(FluffyBoneField.ResolveBone(null, root), Is.Null);
            Assert.That(FluffyBoneField.ResolveBone(new Object[] { root }, null), Is.Null,
                "With no character to check against, everything has to be refused.");
        }
    }
}
