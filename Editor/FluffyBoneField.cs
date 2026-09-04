using System;
using System.Linq;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEngine;

namespace Fluffy.Editor
{
    /// <summary>
    /// A bone slot that only offers the character's own bones.
    /// </summary>
    /// <remarks>
    /// Unity's object field cannot be filtered — its picker lists every transform in
    /// the scene, which for a bone slot is noise a hundred entries deep. This draws
    /// the field itself and opens a searchable dropdown of the hierarchy under the
    /// character instead. Dragging a bone in from the Hierarchy window still works,
    /// and bones from outside the character are refused.
    /// </remarks>
    public static class FluffyBoneField
    {
        /// <summary>Draws the bone slot on the next layout line.</summary>
        public static void Draw(
            GUIContent label,
            SerializedProperty property,
            Transform root,
            string emptyDisplayName = null)
        {
            Draw(EditorGUILayout.GetControlRect(), label, property, root, emptyDisplayName);
        }

        /// <summary>Draws the bone slot in <paramref name="position"/>.</summary>
        public static void Draw(
            Rect position,
            GUIContent label,
            SerializedProperty property,
            Transform root,
            string emptyDisplayName = null)
        {
            var current = property.objectReferenceValue as Transform;

            bool clicked = DrawSlot(
                position,
                label,
                current,
                root,
                canPick: null,
                dragged => property.objectReferenceValue = dragged,
                out Rect fieldRect,
                emptyDisplayName);

            if (!clicked || !HasRoot(root))
            {
                return;
            }

            // The dropdown outlives this OnGUI call, and the SerializedProperty does
            // not: resolve it again from the target when the pick comes back.
            UnityEngine.Object target = property.serializedObject.targetObject;
            string path = property.propertyPath;

            ShowDropdown(fieldRect, root, canPick: null, bone =>
            {
                var serialized = new SerializedObject(target);
                serialized.FindProperty(path).objectReferenceValue = bone;
                serialized.ApplyModifiedProperties();

                EditorUtility.SetDirty(target);
                RepaintInspectors();
            });
        }

        /// <summary>Draws the bone slot on the next layout line, reporting the pick to a callback.</summary>
        public static void Draw(
            GUIContent label,
            Transform current,
            Transform root,
            Action<Transform> onPicked,
            Func<Transform, bool> canPick = null)
        {
            Draw(EditorGUILayout.GetControlRect(), label, current, root, onPicked, canPick);
        }

        /// <summary>
        /// Draws the bone slot for a value no serialized property is behind — the
        /// collision tab's "which bone does this shape go on" is one, since it is a
        /// choice the inspector holds until the Add button is pressed rather than a
        /// field on the character.
        /// </summary>
        /// <param name="canPick">
        /// Which bones the slot will take. The rest are left out of the dropdown and
        /// refused from a drag, which is how the collision tab keeps a blocker from being
        /// put on a cape bone by mistake — the two kinds are the same component, and
        /// where it is parented is the whole difference between them.
        /// </param>
        public static void Draw(
            Rect position,
            GUIContent label,
            Transform current,
            Transform root,
            Action<Transform> onPicked,
            Func<Transform, bool> canPick = null)
        {
            bool clicked = DrawSlot(
                position, label, current, root, canPick, onPicked, out Rect fieldRect);

            if (!clicked || !HasRoot(root))
            {
                return;
            }

            ShowDropdown(fieldRect, root, canPick, bone =>
            {
                onPicked?.Invoke(bone);
                RepaintInspectors();
            });
        }

        /// <summary>
        /// The slot itself: the label, the drag target, and the button that opens the
        /// dropdown. Shared, so a slot backed by a property and one backed by a plain
        /// field cannot drift into looking like two different controls.
        /// </summary>
        /// <returns>Whether the slot was clicked, meaning the dropdown should open.</returns>
        private static bool DrawSlot(
            Rect position,
            GUIContent label,
            Transform current,
            Transform root,
            Func<Transform, bool> canPick,
            Action<Transform> onDragged,
            out Rect fieldRect,
            string emptyDisplayName = null)
        {
            fieldRect = EditorGUI.PrefixLabel(position, label);

            HandleDragAndDrop(fieldRect, root, canPick, onDragged);

            bool showingAutomaticBone = current == null && !string.IsNullOrEmpty(emptyDisplayName);
            var content = new GUIContent(
                showingAutomaticBone ? emptyDisplayName : current != null ? current.name : "None (Bone)",
                current != null || showingAutomaticBone
                    ? EditorGUIUtility.IconContent("Avatar Icon").image
                    : null,
                showingAutomaticBone
                    ? "Automatically detected end bone. Assign Last Bone to override it."
                    : string.Empty);

            Color previousColor = GUI.contentColor;
            if (showingAutomaticBone)
            {
                GUI.contentColor = EditorStyles.miniLabel.normal.textColor;
            }

            bool clicked = GUI.Button(fieldRect, content, EditorStyles.objectField);
            GUI.contentColor = previousColor;
            return clicked;
        }

        /// <summary>The leaf reached by the same first-child walk used by an automatic chain.</summary>
        internal static Transform FindAutomaticLastBone(Transform start)
        {
            Transform current = start;
            while (current != null && current.childCount > 0)
            {
                current = current.GetChild(0);
            }

            return current;
        }

        private static bool HasRoot(Transform root)
        {
            if (root != null)
            {
                return true;
            }

            Debug.LogWarning("[Fluffy Bones] No character to pick bones from.");
            return false;
        }

        private static void ShowDropdown(
            Rect rect, Transform root, Func<Transform, bool> canPick, Action<Transform> onPicked)
        {
            // A fresh state each time: a shared one remembers a selection that means
            // nothing once the dropdown is listing a different character's bones.
            new BoneDropdown(new AdvancedDropdownState(), root, canPick, onPicked).Show(rect);
        }

        /// <summary>
        /// The pick lands after the inspector has finished drawing, so ask for the
        /// repaint that shows it.
        /// </summary>
        private static void RepaintInspectors()
        {
            foreach (UnityEditor.Editor editor in ActiveEditorTracker.sharedTracker.activeEditors)
            {
                editor.Repaint();
            }
        }

        private static void HandleDragAndDrop(
            Rect rect, Transform root, Func<Transform, bool> canPick, Action<Transform> onDragged)
        {
            Event current = Event.current;
            if (current.type != EventType.DragUpdated && current.type != EventType.DragPerform)
            {
                return;
            }

            if (!rect.Contains(current.mousePosition))
            {
                return;
            }

            Transform dragged = ResolveBone(DragAndDrop.objectReferences, root, canPick);
            DragAndDrop.visualMode = dragged != null ? DragAndDropVisualMode.Link : DragAndDropVisualMode.Rejected;

            if (current.type != EventType.DragPerform || dragged == null)
            {
                return;
            }

            DragAndDrop.AcceptDrag();
            onDragged?.Invoke(dragged);
            current.Use();
        }

        /// <summary>
        /// The first dragged object that is a bone of this character, and one the slot
        /// will take. Anything from another character, or from the project, is refused
        /// rather than silently producing a chain that reaches across the scene.
        /// </summary>
        internal static Transform ResolveBone(
            UnityEngine.Object[] dragged, Transform root, Func<Transform, bool> canPick = null)
        {
            if (dragged == null || root == null)
            {
                return null;
            }

            for (int i = 0; i < dragged.Length; i++)
            {
                Transform candidate = dragged[i] switch
                {
                    Transform transform => transform,
                    GameObject gameObject => gameObject.transform,
                    _ => null
                };

                if (candidate != null && candidate.IsChildOf(root) && (canPick == null || canPick(candidate)))
                {
                    return candidate;
                }
            }

            return null;
        }

        /// <summary>
        /// Lists the character's bones, searchable, indented to show the hierarchy.
        /// Flat rather than nested, because a nested dropdown makes every bone that
        /// has children a submenu you cannot pick — which is most of a skeleton.
        /// </summary>
        private class BoneDropdown : AdvancedDropdown
        {
            private const string Indent = "   ";

            private readonly Transform _root;
            private readonly Func<Transform, bool> _canPick;
            private readonly Action<Transform> _onPicked;

            /// <remarks>
            /// A filtered list still walks the whole skeleton and only leaves rows out of
            /// it, since a bone the slot refuses is perfectly likely to be the parent of
            /// one it wants. The indentation is of the rig rather than of what survived
            /// the filter, so a bone sits where it lives.
            /// </remarks>
            public BoneDropdown(
                AdvancedDropdownState state,
                Transform root,
                Func<Transform, bool> canPick,
                Action<Transform> onPicked)
                : base(state)
            {
                _root = root;
                _canPick = canPick;
                _onPicked = onPicked;
                minimumSize = new Vector2(260f, 320f);
            }

            protected override AdvancedDropdownItem BuildRoot()
            {
                var root = new AdvancedDropdownItem("Bones");

                root.AddChild(new BoneItem("None", null));
                root.AddSeparator();

                AddBone(root, _root, 0);
                return root;
            }

            private void AddBone(AdvancedDropdownItem parent, Transform bone, int depth)
            {
                if (_canPick == null || _canPick(bone))
                {
                    parent.AddChild(new BoneItem(RepeatIndent(depth) + bone.name, bone));
                }

                for (int i = 0; i < bone.childCount; i++)
                {
                    AddBone(parent, bone.GetChild(i), depth + 1);
                }
            }

            private static string RepeatIndent(int depth)
            {
                return depth <= 0 ? string.Empty : string.Concat(Enumerable.Repeat(Indent, depth));
            }

            protected override void ItemSelected(AdvancedDropdownItem item)
            {
                if (item is BoneItem bone)
                {
                    _onPicked?.Invoke(bone.Bone);
                }
            }
        }

        /// <summary>
        /// A dropdown row that carries its bone.
        /// </summary>
        /// <remarks>
        /// <c>AdvancedDropdownItem.id</c> is reassigned while the dropdown builds its
        /// tree, so an id set here does not survive to <c>ItemSelected</c> — looking
        /// the bone up by it silently picks nothing. The reference rides along instead.
        /// </remarks>
        private class BoneItem : AdvancedDropdownItem
        {
            public BoneItem(string name, Transform bone) : base(name)
            {
                Bone = bone;
            }

            /// <summary>The bone this row picks, or null for the None row.</summary>
            public Transform Bone { get; }
        }
    }
}
