using UnityEditor;
using UnityEngine;

namespace Fluffy.Editor
{
    /// <summary>
    /// Draws a <see cref="FluffyChain"/> entry: the bone slots go through
    /// <see cref="FluffyBoneField"/>, so the list in Multiple mode gets the same
    /// character-only picker the Single mode field has.
    /// </summary>
    [CustomPropertyDrawer(typeof(FluffyChain))]
    public class FluffyChainDrawer : PropertyDrawer
    {
        private static readonly GUIContent StartBoneLabel =
            new GUIContent("Start Bone", "Where the chain starts.");

        private static readonly GUIContent LastBoneLabel =
            new GUIContent("Last Bone", "Where the chain stops, included. "
                                        + "Leave empty to run to the end of the hierarchy.");

        private static readonly GUIContent ProfileLabel =
            new GUIContent("Profile Override", "Tuning for this chain alone. "
                                               + "Empty falls back to the character's profile.");

        private static readonly GUIContent DummyLabel =
            new GUIContent("Dummy Bone", "Invent a bone past the end of the chain, so the last "
                                         + "real bone has something to swing towards. Rigs that "
                                         + "already end in a spare bone do not need it.");

        private static readonly GUIContent DummyLengthLabel =
            new GUIContent("Dummy Bone Length", "A bone in a game engine is a single point, so "
                                                + "the last one has no length. This stands in for it. "
                                                + "Auto measures it from the rig.");

        private const float AutoToggleWidth = 54f;
        private const int ExpandedLines = 6;

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            float line = EditorGUIUtility.singleLineHeight;
            float spacing = EditorGUIUtility.standardVerticalSpacing;

            return property.isExpanded ? line * ExpandedLines + spacing * ExpandedLines : line;
        }

        /// <summary>
        /// The dummy bone toggle. Shared with the component's Single mode, so both draw
        /// the same control.
        /// </summary>
        public static void DrawDummyToggle(Rect position, SerializedProperty chain)
        {
            EditorGUI.PropertyField(position, chain.FindPropertyRelative("_useDummyBone"), DummyLabel);
        }

        /// <summary>The dummy bone's length, with an Auto toggle beside it.</summary>
        public static void DrawDummyLength(Rect position, SerializedProperty chain)
        {
            SerializedProperty use = chain.FindPropertyRelative("_useDummyBone");
            SerializedProperty auto = chain.FindPropertyRelative("_autoDummyLength");
            SerializedProperty length = chain.FindPropertyRelative("_dummyLength");

            using (new EditorGUI.DisabledScope(!use.boolValue))
            {
                Rect field = EditorGUI.PrefixLabel(position, DummyLengthLabel);
                var lengthRect = new Rect(field.x, field.y, Mathf.Max(0f, field.width - AutoToggleWidth), field.height);
                var toggleRect = new Rect(field.xMax - AutoToggleWidth + 4f, field.y, AutoToggleWidth, field.height);

                // PrefixLabel already consumed the indent; leaving it on would shift the
                // controls a second time.
                int indent = EditorGUI.indentLevel;
                EditorGUI.indentLevel = 0;

                using (new EditorGUI.DisabledScope(auto.boolValue))
                {
                    EditorGUI.PropertyField(lengthRect, length, GUIContent.none);
                }

                auto.boolValue = EditorGUI.ToggleLeft(toggleRect, "Auto", auto.boolValue);
                EditorGUI.indentLevel = indent;
            }
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            float line = EditorGUIUtility.singleLineHeight;
            float spacing = EditorGUIUtility.standardVerticalSpacing;

            SerializedProperty start = property.FindPropertyRelative("_startBone");
            SerializedProperty last = property.FindPropertyRelative("_lastBone");
            SerializedProperty profileOverride = property.FindPropertyRelative("_profileOverride");

            // Name the entry after the bone it starts on — "Element 7" tells nobody
            // which strand of the skirt they are looking at.
            var startBone = start.objectReferenceValue as Transform;
            var header = new GUIContent(startBone != null ? startBone.name : label.text);

            var row = new Rect(position.x, position.y, position.width, line);
            property.isExpanded = EditorGUI.Foldout(row, property.isExpanded, header, true);

            if (!property.isExpanded)
            {
                return;
            }

            Transform root = (property.serializedObject.targetObject as Component)?.transform;

            using (new EditorGUI.IndentLevelScope())
            {
                row.y += line + spacing;
                FluffyBoneField.Draw(row, StartBoneLabel, start, root);

                row.y += line + spacing;
                Transform automaticLast = last.objectReferenceValue == null
                    ? FluffyBoneField.FindAutomaticLastBone(startBone)
                    : null;
                FluffyBoneField.Draw(
                    row,
                    LastBoneLabel,
                    last,
                    root,
                    automaticLast != null ? automaticLast.name + " (Auto)" : null);

                row.y += line + spacing;
                DrawDummyToggle(row, property);

                row.y += line + spacing;
                DrawDummyLength(row, property);

                row.y += line + spacing;
                EditorGUI.PropertyField(row, profileOverride, ProfileLabel);
            }
        }
    }
}
