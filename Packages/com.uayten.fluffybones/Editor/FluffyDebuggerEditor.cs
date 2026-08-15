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
        private static readonly GUIContent StartBoneLabel = new GUIContent(
            "Start Bone",
            "Where to start recording. Empty records every bone of every chain.");

        private static readonly GUIContent EndBoneLabel = new GUIContent(
            "End Bone",
            "Where to stop, included. Empty records the start bone alone — not the rest "
            + "of the chain, which is what this field means on Fluffy Bones.");

        private SerializedProperty _body;
        private SerializedProperty _startBone;
        private SerializedProperty _endBone;
        private SerializedProperty _automatic;
        private SerializedProperty _fromFrame;
        private SerializedProperty _toFrame;
        private SerializedProperty _maxFrames;
        private SerializedProperty _folder;

        private void OnEnable()
        {
            _body = serializedObject.FindProperty("_body");
            _startBone = serializedObject.FindProperty("_startBone");
            _endBone = serializedObject.FindProperty("_endBone");
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

            // The same picker the chain's own bone slots use, so it lists this
            // character's bones instead of every transform in the scene.
            Transform character = ResolveCharacter(debugger);
            FluffyBoneField.Draw(StartBoneLabel, _startBone, character);
            FluffyBoneField.Draw(EndBoneLabel, _endBone, character);
            DrawCoverage();

            EditorGUILayout.Space();
            DrawRange();

            EditorGUILayout.Space();
            EditorGUILayout.PropertyField(_folder, new GUIContent("Folder"));
            EditorGUILayout.LabelField(" ", debugger.ResolveFolder(), EditorStyles.miniLabel);

            EditorGUILayout.Space();
            DrawButtons(debugger);

            serializedObject.ApplyModifiedProperties();
        }

        /// <summary>
        /// What the two bone slots add up to, spelled out under them.
        /// </summary>
        /// <remarks>
        /// An empty End Bone means one bone here and the whole chain on Fluffy Bones.
        /// Two fields with the same name and different meanings need the answer written
        /// down rather than remembered.
        /// </remarks>
        private void DrawCoverage()
        {
            var start = _startBone.objectReferenceValue as Transform;
            var end = _endBone.objectReferenceValue as Transform;

            string message;
            if (start == null)
            {
                message = "Every bone of every chain.";
            }
            else if (end == null)
            {
                message = $"{start.name} alone. Set an end bone to take in the ones below it.";
            }
            else
            {
                int count = FluffyChain.CollectChain(start, end).Count;
                message = $"{count} bone(s), {start.name} down to {end.name}.";
            }

            EditorGUILayout.LabelField(" ", message, EditorStyles.miniLabel);
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
            bool playing = Application.isPlaying;

            EditorGUILayout.LabelField(
                "Frame",
                !playing
                    ? "not playing"
                    : debugger.IsRecording
                        ? $"{debugger.Frame} — recording, {debugger.RecordedFrames} frames captured"
                        : $"{debugger.Frame} — idle");

            // Drawn whatever the mode, greyed rather than hidden: a button that is not
            // there reads as a feature that is not there.
            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUI.DisabledScope(!playing || debugger.IsRecording))
                {
                    if (GUILayout.Button("Start Record", GUILayout.Height(24f)))
                    {
                        debugger.StartRecording();
                    }
                }

                using (new EditorGUI.DisabledScope(!playing || !debugger.IsRecording))
                {
                    if (GUILayout.Button("Stop Record", GUILayout.Height(24f)))
                    {
                        debugger.StopRecording();
                    }
                }
            }

            if (!playing)
            {
                EditorGUILayout.LabelField(
                    " ",
                    "Nothing moves outside play mode. Press play to record.",
                    EditorStyles.miniLabel);
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
