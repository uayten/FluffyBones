using UnityEditor;
using UnityEditorInternal;
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

        /// <summary>
        /// Set by an inspector that draws the buttons itself, in a row of its own.
        /// </summary>
        /// <remarks>
        /// The character's inspector puts them beside Duplicate and New, where the
        /// profile is chosen, so drawing them here as well would show them twice in
        /// the one panel.
        /// </remarks>
        public bool SaveControlsDrawnElsewhere { get; set; }

        private static readonly GUIContent SaveLabel = new GUIContent(
            "Save",
            "Write this profile to its file now, instead of waiting for the project to "
            + "save. Greyed out when the file already matches what is on screen.");

        private static readonly GUIContent RevertLabel = new GUIContent(
            "Revert",
            "Throw away the edits made since the last save and read the values back "
            + "from the file.");

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

            if (!SaveControlsDrawnElsewhere)
            {
                DrawSaveControls();
            }
        }

        /// <summary>Writes the asset to disk, and puts it back the way it was on disk.</summary>
        /// <remarks>
        /// A profile is an asset, so Unity holds every edit in memory and writes it
        /// out whenever the project next saves. Nothing goes missing that way, but
        /// nothing on screen says so either: there is no telling whether the file
        /// matches the sliders, and a value dragged too far has no way back except
        /// dragging it again by eye. Both buttons are dead while the asset is clean,
        /// which makes the pair as much an indicator as a control — greyed out means
        /// the file agrees with what is drawn.
        /// </remarks>
        private void DrawSaveControls()
        {
            var profile = (FluffyProfile)target;

            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField(
                    HasUnsavedChanges(profile) ? "Unsaved changes" : "Saved",
                    EditorStyles.miniLabel);

                GUILayout.FlexibleSpace();

                if (DrawSaveButtons(profile))
                {
                    serializedObject.Update();
                    Repaint();
                }
            }
        }

        /// <summary>Whether the profile holds edits its file has not been given yet.</summary>
        /// <remarks>
        /// An asset that was never written has nothing to compare against and nothing
        /// to revert to; writing it for the first time belongs to whoever is creating
        /// it.
        /// </remarks>
        public static bool HasUnsavedChanges(FluffyProfile profile)
        {
            return profile != null
                   && !string.IsNullOrEmpty(AssetDatabase.GetAssetPath(profile))
                   && EditorUtility.IsDirty(profile);
        }

        /// <summary>
        /// The Revert and Save pair, for a row the caller has already opened.
        /// </summary>
        /// <returns>
        /// True when the profile was reverted, so a caller holding a
        /// <see cref="SerializedObject"/> over it knows to read it again.
        /// </returns>
        public static bool DrawSaveButtons(FluffyProfile profile)
        {
            bool dirty = HasUnsavedChanges(profile);
            bool reverted = false;

            using (new EditorGUI.DisabledScope(!dirty))
            {
                if (GUILayout.Button(RevertLabel, GUILayout.Width(56f)))
                {
                    RevertToDisk(profile, AssetDatabase.GetAssetPath(profile));
                    reverted = true;
                }

                if (GUILayout.Button(SaveLabel, GUILayout.Width(46f)))
                {
                    AssetDatabase.SaveAssetIfDirty(profile);
                }
            }

            return reverted;
        }

        /// <summary>Puts the profile back to the values held in its file.</summary>
        /// <remarks>
        /// The file is read into throwaway objects rather than through
        /// <see cref="AssetDatabase.LoadAssetAtPath"/>, which hands back the instance
        /// already loaded — the edited one — and would revert it to itself. The
        /// throwaways are destroyed here because nothing else owns them: forgetting
        /// them leaks a profile per press.
        /// </remarks>
        private static void RevertToDisk(FluffyProfile profile, string path)
        {
            Object[] fromDisk = InternalEditorUtility.LoadSerializedFileAndForget(path);

            for (int i = 0; i < fromDisk.Length; i++)
            {
                if (fromDisk[i] is FluffyProfile saved)
                {
                    Undo.RecordObject(profile, "Revert Fluffy Profile");
                    EditorUtility.CopySerialized(saved, profile);

                    // It now matches the file, so it is no longer waiting to be written.
                    EditorUtility.ClearDirty(profile);
                    break;
                }
            }

            for (int i = 0; i < fromDisk.Length; i++)
            {
                Object.DestroyImmediate(fromDisk[i]);
            }
        }
    }
}
