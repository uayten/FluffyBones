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

        private Transform _addColliderTo;
        private FluffyColliderShape _addColliderShape = FluffyColliderShape.Capsule;

        /// <summary>Whether the character has any shape to be pushed out of.</summary>
        private bool HasColliders => ((FluffyBones)target).GetComponentInChildren<FluffyCollider>(true) != null;

        private void DrawCollisionTab()
        {
            var body = (FluffyBones)target;
            FluffyCollider[] colliders = body.GetComponentsInChildren<FluffyCollider>(true);

            DrawColliderList(colliders);

            EditorGUILayout.Space();
            DrawAddCollider(body);

            EditorGUILayout.Space();
            DrawChainRadii();
        }

        private void DrawColliderList(FluffyCollider[] colliders)
        {
            EditorGUILayout.LabelField($"Shapes ({colliders.Length})", EditorStyles.boldLabel);

            if (colliders.Length == 0)
            {
                EditorGUILayout.HelpBox(
                    "Nothing on this character to be pushed out of. A capsule on each thigh is where "
                    + "most skirts start; a plane on the spine is what keeps hair off the face.",
                    MessageType.Info);
                return;
            }

            for (int i = 0; i < colliders.Length; i++)
            {
                DrawCollider(colliders[i]);
            }
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
        /// Adds a shape to a bone chosen by name.
        /// </summary>
        /// <remarks>
        /// Through the same picker the chain fields use, so it offers this character's
        /// bones and refuses everything else — a collider parented to another character
        /// would travel with the wrong animation and be almost impossible to notice.
        /// </remarks>
        private void DrawAddCollider(FluffyBones body)
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("Add a shape", EditorStyles.boldLabel);

                using (new EditorGUILayout.HorizontalScope())
                {
                    var picked = EditorGUILayout.ObjectField(
                        "On bone", _addColliderTo, typeof(Transform), true) as Transform;

                    // The same rule the chain slots enforce: a shape parented to another
                    // character would travel with the wrong animation, and nobody would
                    // spot it in a hierarchy.
                    _addColliderTo = picked == null || picked.IsChildOf(body.transform) ? picked : _addColliderTo;

                    _addColliderShape = (FluffyColliderShape)EditorGUILayout.EnumPopup(
                        _addColliderShape, GUILayout.Width(ShapeFieldWidth));
                }

                using (new EditorGUI.DisabledScope(_addColliderTo == null))
                {
                    if (GUILayout.Button("Add"))
                    {
                        AddCollider(body, _addColliderTo, _addColliderShape);
                    }
                }

                if (_addColliderTo == null)
                {
                    EditorGUILayout.LabelField(
                        "Pick the bone the shape belongs to — a thigh, the chest, the spine.",
                        EditorStyles.miniLabel);
                }
            }
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

            EditorGUILayout.LabelField(
                "A strand is a rope rather than a line. Without a thickness it sinks into a leg up "
                + "to its middle before anything stops it.",
                EditorStyles.miniLabel);
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
            _addColliderTo = null;

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
