using UnityEditor;
using UnityEngine;

namespace Fluffy.Editor
{
    /// <summary>
    /// Inspector for <see cref="FluffyDebugger"/>: the bone slot gets the character's
    /// own picker, and starting and stopping are buttons rather than a tick box.
    /// </summary>
    [CustomEditor(typeof(FluffyDebugger))]
    public class FluffyDebuggerEditor : UnityEditor.Editor
    {
        private static readonly GUIContent BoneLabel = new GUIContent(
            "Bone",
            "Record this bone alone. Empty records every bone of the chain above.");

        private SerializedProperty _body;
        private SerializedProperty _chain;
        private SerializedProperty _bone;
        private SerializedProperty _automatic;
        private SerializedProperty _fromFrame;
        private SerializedProperty _toFrame;
        private SerializedProperty _maxFrames;
        private SerializedProperty _folder;

        private void OnEnable()
        {
            _body = serializedObject.FindProperty("_body");
            _chain = serializedObject.FindProperty("_chain");
            _bone = serializedObject.FindProperty("_bone");
            _automatic = serializedObject.FindProperty("_automatic");
            _fromFrame = serializedObject.FindProperty("_fromFrame");
            _toFrame = serializedObject.FindProperty("_toFrame");
            _maxFrames = serializedObject.FindProperty("_maxFrames");
            _folder = serializedObject.FindProperty("_folder");
        }

        public override bool RequiresConstantRepaint()
        {
            // The frame counter and the recording light are only worth anything if they
            // keep up with the game.
            return Application.isPlaying;
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            var debugger = (FluffyDebugger)target;

            EditorGUILayout.PropertyField(_body, new GUIContent(
                "Body", "The character to record. Empty uses the Fluffy Bones on this object."));

            EditorGUILayout.PropertyField(_chain, new GUIContent(
                "Chain", "Which chain to record. -1 records every chain, which for a skirt "
                         + "is a lot of rows."));

            // The same picker the chain's own bone slots use, so it lists this
            // character's bones instead of every transform in the scene.
            FluffyBoneField.Draw(BoneLabel, _bone, ResolveCharacter(debugger));

            EditorGUILayout.Space();
            DrawRange();

            EditorGUILayout.Space();
            EditorGUILayout.PropertyField(_folder, new GUIContent("Folder"));
            EditorGUILayout.LabelField(" ", debugger.ResolveFolder(), EditorStyles.miniLabel);

            EditorGUILayout.Space();
            DrawButtons(debugger);

            serializedObject.ApplyModifiedProperties();
        }

        private void DrawRange()
        {
            EditorGUILayout.PropertyField(_automatic, new GUIContent(
                "Automatic",
                "Start and stop by frame number instead of by hand. The first frames of "
                + "play are never the interesting ones."));

            if (_automatic.boolValue)
            {
                using (new EditorGUI.IndentLevelScope())
                {
                    EditorGUILayout.PropertyField(_fromFrame, new GUIContent("From Frame"));
                    EditorGUILayout.PropertyField(_toFrame, new GUIContent("To Frame"));

                    int frames = Mathf.Max(0, _toFrame.intValue - _fromFrame.intValue);
                    EditorGUILayout.LabelField(
                        " ",
                        $"{frames} frames, about {frames / 60f:0.0} s at 60 fps",
                        EditorStyles.miniLabel);
                }

                return;
            }

            using (new EditorGUI.IndentLevelScope())
            {
                EditorGUILayout.PropertyField(_maxFrames, new GUIContent(
                    "Max Frames", "A recording started by hand stops itself here, so one "
                                  + "left running does not eat memory."));
            }
        }

        private void DrawButtons(FluffyDebugger debugger)
        {
            if (!Application.isPlaying)
            {
                EditorGUILayout.HelpBox(
                    "Nothing moves outside play mode, so there is nothing to record. Press "
                    + "play, then start the recording here — or tick Automatic and let the "
                    + "frame range do it.",
                    MessageType.Info);

                return;
            }

            EditorGUILayout.LabelField(
                "Frame",
                debugger.IsRecording
                    ? $"{debugger.Frame} — recording, {debugger.RecordedFrames} frames captured"
                    : $"{debugger.Frame} — idle");

            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUI.DisabledScope(debugger.IsRecording))
                {
                    if (GUILayout.Button("Start Record", GUILayout.Height(24f)))
                    {
                        debugger.StartRecording();
                    }
                }

                using (new EditorGUI.DisabledScope(!debugger.IsRecording))
                {
                    if (GUILayout.Button("Stop Record", GUILayout.Height(24f)))
                    {
                        debugger.StopRecording();
                    }
                }
            }

            if (_automatic.boolValue && debugger.AutomaticDone)
            {
                EditorGUILayout.HelpBox(
                    "The frame range has been through. Press play again for another, or "
                    + "use the buttons.",
                    MessageType.None);
            }
        }

        /// <summary>The character whose bones the picker should offer.</summary>
        private static Transform ResolveCharacter(FluffyDebugger debugger)
        {
            var body = debugger.Body;
            return body != null ? body.transform : debugger.transform;
        }
    }
}
