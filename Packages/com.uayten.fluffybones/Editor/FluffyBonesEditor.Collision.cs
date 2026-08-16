using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Fluffy.Editor
{
    /// <summary>
    /// The collision tab: the shapes on the character, and how thick each chain is
    /// against them.
    /// </summary>
    /// <remarks>
    /// The shapes are separate components living on the bones they belong to, which is
    /// what makes a capsule on a thigh keep being a thigh while the leg animates. That
    /// is right for the runtime and wrong for finding them: nobody thinks "I should add
    /// a component to the thigh bone", they think "the skirt goes through the leg". So
    /// the character's own inspector lists every shape it will be solved against, adds
    /// one to a bone chosen by name, and edits them all in one place — the bones stay
    /// the home, this is the door.
    /// </remarks>
    public partial class FluffyBonesEditor
    {
        private const float ShapeFieldWidth = 90f;

        private readonly Dictionary<FluffyCollider, SerializedObject> _colliderEditors =
            new Dictionary<FluffyCollider, SerializedObject>();

        /// <summary>What each of the two Add rows is holding, until its button is pressed.</summary>
        private readonly AddShape _addBlocker = new AddShape();
        private readonly AddShape _addOnChain = new AddShape();

        private void DrawCollisionTab()
        {
            var body = (FluffyBones)target;
            FluffyCollider[] colliders = body.GetComponentsInChildren<FluffyCollider>(true);

            DrawColliderGroup(
                "Blockers",
                "On the body, holding the chains off it: a capsule on each thigh is where most skirts "
                + "start, and a plane on the spine is what keeps hair off the face.",
                colliders,
                body,
                ridingOnAChain: false,
                _addBlocker);

            EditorGUILayout.Space();

            DrawColliderGroup(
                "On the chains",
                "Riding bones the plugin moves, so that a cape has a body the skirt cannot walk "
                + "through. Each of these pushes every chain except the one carrying it.",
                colliders,
                body,
                ridingOnAChain: true,
                _addOnChain);

            EditorGUILayout.Space();
            DrawChainRadii();
        }

        /// <summary>
        /// One of the two kinds of shape: what it is for, the ones there are, and the row
        /// that adds another.
        /// </summary>
        /// <remarks>
        /// Split because the difference is invisible otherwise — the two are the same
        /// component and differ only in what they are parented to, and a shape that ended
        /// up on a cape bone by accident behaves nothing like the one that was meant.
        ///
        /// Each kind adds through its own row, whose bone picker offers only the bones
        /// that would make a shape of that kind. That is what makes the two headings mean
        /// something: you cannot answer "which bone" in a way that lands the shape in the
        /// other group.
        /// </remarks>
        private void DrawColliderGroup(
            string title,
            string explanation,
            FluffyCollider[] colliders,
            FluffyBones body,
            bool ridingOnAChain,
            AddShape add)
        {
            int count = 0;
            for (int i = 0; i < colliders.Length; i++)
            {
                if (RidesOnAChain(colliders[i], body) == ridingOnAChain)
                {
                    count++;
                }
            }

            EditorGUILayout.LabelField($"{title} ({count})", EditorStyles.boldLabel);
            DrawNote(explanation);

            for (int i = 0; i < colliders.Length; i++)
            {
                if (RidesOnAChain(colliders[i], body) == ridingOnAChain)
                {
                    DrawCollider(colliders[i]);
                }
            }

            DrawAddCollider(body, ridingOnAChain, add);
        }

        /// <summary>
        /// Whether the shape is carried by one of the character's chains.
        /// </summary>
        /// <remarks>
        /// Asked of the chains themselves, so the tab sorts shapes into the same two piles
        /// the solver does rather than into a second opinion about where a bone is.
        /// </remarks>
        private static bool RidesOnAChain(FluffyCollider collider, FluffyBones body)
        {
            return collider != null && RidesOnAChain(collider.transform, body);
        }

        /// <summary>Whether a bone belongs to one of the character's chains.</summary>
        private static bool RidesOnAChain(Transform bone, FluffyBones body)
        {
            IReadOnlyList<FluffyChain> chains = body.Chains;

            for (int i = 0; i < chains.Count; i++)
            {
                if (chains[i].Owns(bone))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// One shape, with only the fields its own kind uses.
        /// </summary>
        /// <remarks>
        /// A capsule's height on a sphere, or a radius on a plane, is a control that
        /// changes nothing — and a control that promises what nothing delivers is worse
        /// than no control, which is the same reason the twist range stays hidden.
        /// </remarks>
        private void DrawCollider(FluffyCollider collider)
        {
            if (collider == null)
            {
                return;
            }

            SerializedObject serialized = EditorFor(collider);
            serialized.Update();

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.ObjectField(collider.transform, typeof(Transform), true);

                    SerializedProperty shape = serialized.FindProperty("_shape");
                    EditorGUILayout.PropertyField(shape, GUIContent.none, GUILayout.Width(ShapeFieldWidth));

                    if (GUILayout.Button("Remove", GUILayout.Width(64f)))
                    {
                        RemoveCollider(collider);
                        return;
                    }
                }

                EditorGUILayout.PropertyField(serialized.FindProperty("_centre"), new GUIContent("Centre"));

                // A sphere has no orientation to set, and the file's own rule is that a
                // control which promises what nothing delivers is worse than no control.
                if (collider.Shape != FluffyColliderShape.Sphere)
                {
                    EditorGUILayout.PropertyField(
                        serialized.FindProperty("_rotation"), new GUIContent("Rotation"));
                }

                switch (collider.Shape)
                {
                    case FluffyColliderShape.Sphere:
                        EditorGUILayout.PropertyField(serialized.FindProperty("_radius"), new GUIContent("Radius"));
                        break;

                    case FluffyColliderShape.Capsule:
                        EditorGUILayout.PropertyField(serialized.FindProperty("_radius"), new GUIContent("Radius"));
                        EditorGUILayout.PropertyField(serialized.FindProperty("_height"), new GUIContent("Height"));
                        EditorGUILayout.PropertyField(
                            serialized.FindProperty("_direction"), new GUIContent("Along"));
                        break;

                    case FluffyColliderShape.Box:
                        EditorGUILayout.PropertyField(serialized.FindProperty("_size"), new GUIContent("Size"));
                        break;

                    case FluffyColliderShape.Plane:
                        EditorGUILayout.PropertyField(
                            serialized.FindProperty("_direction"), new GUIContent("Facing"));
                        EditorGUILayout.HelpBox(
                            "Everything on the far side is brought to the surface, however far away. "
                            + "The arrow in the scene points at the side that is allowed.",
                            MessageType.None);
                        break;
                }

                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.PropertyField(serialized.FindProperty("_draw"), new GUIContent("Draw"));
                    EditorGUILayout.PropertyField(serialized.FindProperty("_colour"), GUIContent.none);
                }
            }

            serialized.ApplyModifiedProperties();
        }

        /// <summary>
        /// Adds a shape of one kind to a bone chosen by name.
        /// </summary>
        /// <remarks>
        /// Through the same picker the chain fields use — the character's own bones,
        /// searchable, indented to show the hierarchy — so a shape goes on a thigh by
        /// picking "thigh_L" rather than by hunting the scene for it. Bones from another
        /// character are refused: one would travel with the wrong animation and be almost
        /// impossible to notice.
        ///
        /// The picker is narrowed to the bones that make this kind of shape, which is the
        /// only difference between the two rows: a blocker cannot be put on a cape bone,
        /// and a shape meant to ride the cape cannot end up on the hips.
        /// </remarks>
        private void DrawAddCollider(FluffyBones body, bool ridingOnAChain, AddShape add)
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                bool anyBone = HasABoneFor(body, ridingOnAChain);

                using (new EditorGUI.DisabledScope(!anyBone))
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        FluffyBoneField.Draw(
                            new GUIContent("Add on bone", "The bone the shape rides on, so it travels "
                                                          + "with the animation."),
                            add.Bone,
                            body.transform,
                            bone => add.Bone = bone,
                            bone => RidesOnAChain(bone, body) == ridingOnAChain);

                        add.Shape = (FluffyColliderShape)EditorGUILayout.EnumPopup(
                            add.Shape, GUILayout.Width(ShapeFieldWidth));
                    }

                    using (new EditorGUI.DisabledScope(add.Bone == null))
                    {
                        if (GUILayout.Button("Add"))
                        {
                            AddCollider(body, add.Bone, add.Shape);
                            add.Bone = null;
                        }
                    }
                }

                if (!anyBone)
                {
                    DrawNote(ridingOnAChain
                        ? "No chain with a start bone yet, so there is nothing for a shape to ride on. "
                          + "Set one up under Setup."
                        : "Every bone of this character belongs to a chain, so there is nowhere to put "
                          + "a shape that blocks them.");

                    return;
                }

                if (add.Bone == null)
                {
                    DrawNote(ridingOnAChain
                        ? "Pick a bone of a chain the plugin moves — capa_01, a strand of the skirt."
                        : "Pick the bone the shape belongs to — a thigh, the chest, the spine.");
                }
            }
        }

        /// <summary>Whether the character has any bone this kind of shape could go on.</summary>
        /// <remarks>
        /// Answered without walking the skeleton, since this runs on every repaint. A
        /// chain needs a start bone before anything can ride it; a blocker needs one bone
        /// outside every chain, and the character's own transform is that bone unless a
        /// chain starts at the character itself.
        /// </remarks>
        private static bool HasABoneFor(FluffyBones body, bool ridingOnAChain)
        {
            IReadOnlyList<FluffyChain> chains = body.Chains;

            if (!ridingOnAChain)
            {
                return !RidesOnAChain(body.transform, body);
            }

            for (int i = 0; i < chains.Count; i++)
            {
                if (chains[i].StartBone != null)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// The bone and shape an Add row is holding.
        /// </summary>
        /// <remarks>
        /// One of these per row rather than a pair of fields each, so adding a third kind
        /// some day is a third instance and not a third pair of names to keep straight.
        /// </remarks>
        private sealed class AddShape
        {
            public Transform Bone;
            public FluffyColliderShape Shape = FluffyColliderShape.Capsule;
        }

        /// <summary>
        /// How thick each chain is against the shapes.
        /// </summary>
        /// <remarks>
        /// Here rather than with the chain's other settings because it means nothing
        /// without a shape to be thick against, and because a skirt is tuned by looking
        /// at all its strands at once.
        /// </remarks>
        private void DrawChainRadii()
        {
            if (_chains.arraySize == 0)
            {
                return;
            }

            EditorGUILayout.LabelField("Chain thickness", EditorStyles.boldLabel);

            for (int i = 0; i < _chains.arraySize; i++)
            {
                SerializedProperty chain = _chains.GetArrayElementAtIndex(i);
                var start = chain.FindPropertyRelative("_startBone").objectReferenceValue as Transform;

                EditorGUILayout.PropertyField(
                    chain.FindPropertyRelative("_radius"),
                    new GUIContent(start != null ? start.name : $"Chain {i}"));
            }

            DrawNote("A strand is a rope rather than a line. Without a thickness it sinks into a leg up "
                     + "to its middle before anything stops it.");
        }

        private void AddCollider(FluffyBones body, Transform bone, FluffyColliderShape shape)
        {
            var added = Undo.AddComponent<FluffyCollider>(bone.gameObject);
            added.Shape = shape;

            // So the shape is a sensible size on this rig rather than a default that
            // happens to be right for a character one unit tall.
            float reach = Mathf.Max(0.02f, EstimateBoneLength(bone));
            added.Radius = reach * 0.5f;
            added.Height = reach * 2f;
            added.Size = Vector3.one * reach;

            body.CollectColliders();

            EditorGUIUtility.PingObject(added);
        }

        private void RemoveCollider(FluffyCollider collider)
        {
            _colliderEditors.Remove(collider);

            var body = (FluffyBones)target;
            Undo.DestroyObjectImmediate(collider);
            body.CollectColliders();
        }

        /// <summary>
        /// How long the bone is, measured to its first child, so a new shape arrives
        /// roughly the size of the thing it is meant to be.
        /// </summary>
        private static float EstimateBoneLength(Transform bone)
        {
            if (bone.childCount > 0)
            {
                return Vector3.Distance(bone.position, bone.GetChild(0).position);
            }

            return bone.parent != null ? Vector3.Distance(bone.position, bone.parent.position) : 0.1f;
        }

        private SerializedObject EditorFor(FluffyCollider collider)
        {
            if (_colliderEditors.TryGetValue(collider, out SerializedObject serialized)
                && serialized.targetObject != null)
            {
                return serialized;
            }

            serialized = new SerializedObject(collider);
            _colliderEditors[collider] = serialized;

            return serialized;
        }

        private void DisposeColliderEditors()
        {
            foreach (SerializedObject serialized in _colliderEditors.Values)
            {
                serialized?.Dispose();
            }

            _colliderEditors.Clear();
        }
    }
}
