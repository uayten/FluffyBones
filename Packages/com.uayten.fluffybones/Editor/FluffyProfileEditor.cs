using UnityEditor;
using UnityEngine;

namespace Fluffy.Editor
{
    /// <summary>
    /// Inspector for <see cref="FluffyProfile"/>.
    /// </summary>
    /// <remarks>
    /// Written by hand rather than left to the default drawer for one reason: the
    /// labels say what the values do in plain words, and words that plain do not fit
    /// the standard label column. This widens it.
    /// </remarks>
    [CustomEditor(typeof(FluffyProfile))]
    public class FluffyProfileEditor : UnityEditor.Editor
    {
        private const float LabelWidth = 230f;

        private static readonly GUIContent ReturnStrengthLabel = new GUIContent(
            "Strength to Return to Default Pose",
            "How hard the chain pulls back to its default pose. High keeps it close to "
            + "the pose; 0 leaves it limp and it just hangs.");

        private static readonly GUIContent FalloffLabel = new GUIContent(
            "Strength Falloff Along Chain",
            "Scales that strength from the start of the chain (0) to its end (1). "
            + "Lower at the end makes the tip whip.");

        private static readonly GUIContent DampingLabel = new GUIContent(
            "Damping",
            "How much motion is bled off each frame. 0 swings forever, 1 kills it "
            + "instantly. This is what stops the wobble — not the return strength.");

        private static readonly GUIContent GravityLabel = new GUIContent(
            "Gravity",
            "Constant world acceleration, in units per second squared. A light droop "
            + "usually reads better than a physical -9.81.");

        private SerializedProperty _returnStrength;
        private SerializedProperty _returnStrengthFalloff;
        private SerializedProperty _drag;
        private SerializedProperty _gravity;

        private void OnEnable()
        {
            _returnStrength = serializedObject.FindProperty("_returnStrength");
            _returnStrengthFalloff = serializedObject.FindProperty("_returnStrengthFalloff");
            _drag = serializedObject.FindProperty("_drag");
            _gravity = serializedObject.FindProperty("_gravity");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            float previousWidth = EditorGUIUtility.labelWidth;
            EditorGUIUtility.labelWidth = LabelWidth;

            EditorGUILayout.PropertyField(_returnStrength, ReturnStrengthLabel);
            EditorGUILayout.PropertyField(_returnStrengthFalloff, FalloffLabel);
            EditorGUILayout.PropertyField(_drag, DampingLabel);
            EditorGUILayout.PropertyField(_gravity, GravityLabel);

            EditorGUIUtility.labelWidth = previousWidth;

            serializedObject.ApplyModifiedProperties();
        }
    }
}
