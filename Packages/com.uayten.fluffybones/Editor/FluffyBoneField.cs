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
        public static void Draw(GUIContent label, SerializedProperty property, Transform root)
        {
            Draw(EditorGUILayout.GetControlRect(), label, property, root);
        }

        /// <summary>Draws the bone slot in <paramref name="position"/>.</summary>
        public static void Draw(Rect position, GUIContent label, SerializedProperty property, Transform root)
        {
            Rect fieldRect = EditorGUI.PrefixLabel(position, label);
            var current = property.objectReferenceValue as Transform;

            HandleDragAndDrop(fieldRect, property, root);

            var content = new GUIContent(
                current != null ? current.name : "None (Bone)",
                current != null ? EditorGUIUtility.IconContent("Avatar Icon").image : null);

            if (!GUI.Button(fieldRect, content, EditorStyles.objectField))
            {
                return;
            }

            if (root == null)
            {
                Debug.LogWarning("[Fluffy Bones] No character to pick bones from.");
                return;
            }

            ShowDropdown(fieldRect, property, root);
        }

        private static void ShowDropdown(Rect rect, SerializedProperty property, Transform root)
        {
            // The dropdown outlives this OnGUI call, and the SerializedProperty does
            // not: resolve it again from the target when the pick comes back.
            UnityEngine.Object target = property.serializedObject.targetObject;
            string path = property.propertyPath;

            // A fresh state each time: a shared one remembers a selection that means
            // nothing once the dropdown is listing a different character's bones.
            var dropdown = new BoneDropdown(new AdvancedDropdownState(), root, bone =>
            {
                var serialized = new SerializedObject(target);
                serialized.FindProperty(path).objectReferenceValue = bone;
                serialized.ApplyModifiedProperties();

                // The pick lands after the inspector has finished drawing, so ask for
                // the repaint that shows it.
                EditorUtility.SetDirty(target);
                foreach (UnityEditor.Editor editor in ActiveEditorTracker.sharedTracker.activeEditors)
                {
                    editor.Repaint();
                }
            });

            dropdown.Show(rect);
        }

        private static void HandleDragAndDrop(Rect rect, SerializedProperty property, Transform root)
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

            Transform dragged = ResolveBone(DragAndDrop.objectReferences, root);
            DragAndDrop.visualMode = dragged != null ? DragAndDropVisualMode.Link : DragAndDropVisualMode.Rejected;

            if (current.type != EventType.DragPerform || dragged == null)
            {
                return;
            }

            DragAndDrop.AcceptDrag();
            property.objectReferenceValue = dragged;
            current.Use();
        }

        /// <summary>
        /// The first dragged object that is a bone of this character. Anything from
        /// another character, or from the project, is refused rather than silently
        /// producing a chain that reaches across the scene.
        /// </summary>
        internal static Transform ResolveBone(UnityEngine.Object[] dragged, Transform root)
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

                if (candidate != null && candidate.IsChildOf(root))
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
            private readonly Action<Transform> _onPicked;

            public BoneDropdown(AdvancedDropdownState state, Transform root, Action<Transform> onPicked)
                : base(state)
            {
                _root = root;
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
                parent.AddChild(new BoneItem(RepeatIndent(depth) + bone.name, bone));

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
