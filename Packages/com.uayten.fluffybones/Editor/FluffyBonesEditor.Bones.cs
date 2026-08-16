using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Fluffy.Editor
{
    /// <summary>
    /// The bone list under Setup: which bones the chain picked up, and which of them the
    /// solver is allowed to move.
    /// </summary>
    /// <remarks>
    /// A chain is authored as two ends and walks the hierarchy between them. That is
    /// short to set up and impossible to see — nothing said which bones came along, nor
    /// why one of them never moves. This lists them and gives each a switch.
    ///
    /// The switch earns its place on rigs where the top of a chain belongs to something
    /// else: the first bone of a skirt is usually driven by the hip animation, and the
    /// only way to say so was to move the chain's start bone down one, which threw away
    /// that bone's pose and limits with it.
    ///
    /// The switches are stored per bone in the pose, beside the limits, so a shared pose
    /// asset turns the same bone off on all eight strands of a skirt at once.
    /// </remarks>
    public partial class FluffyBonesEditor
    {
        private const float BoneToggleWidth = 18f;
        private const float BoneNoteWidth = 140f;
        private const float BoneBulkButtonWidth = 46f;

        private static GUIStyle _rowNoteStyle;

        /// <summary>A note at the end of a row, quiet enough not to compete with the name.</summary>
        private static GUIStyle RowNoteStyle => _rowNoteStyle ??= new GUIStyle(EditorStyles.miniLabel)
        {
            alignment = TextAnchor.MiddleRight
        };

        private void DrawChainBones()
        {
            if (_chains.arraySize == 0)
            {
                return;
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Bones", EditorStyles.boldLabel);

            // The same chain selector the Pose and Limits tabs use, and the same field
            // behind it, so walking between tabs keeps showing the strand being worked on.
            bool isMultiple = (FluffyChainMode)_mode.enumValueIndex == FluffyChainMode.Multiple;
            _editingChain = isMultiple && _chains.arraySize > 1 ? DrawChainSelector() : 0;

            SerializedProperty chain =
                _chains.GetArrayElementAtIndex(Mathf.Clamp(_editingChain, 0, _chains.arraySize - 1));

            var startBone = chain.FindPropertyRelative("_startBone").objectReferenceValue as Transform;
            var lastBone = chain.FindPropertyRelative("_lastBone").objectReferenceValue as Transform;

            if (startBone == null)
            {
                EditorGUILayout.HelpBox("No start bone yet, so there is nothing to list.", MessageType.Info);
                return;
            }

            List<Transform> bones = FluffyChain.CollectChain(startBone, lastBone);

            // Written into whichever object holds this chain's pose — the asset when one
            // is assigned, the component otherwise. Same resolution the other two tabs do.
            SerializedObject owner = ResolvePoseOwner(
                chain.FindPropertyRelative("_pose"), out SerializedProperty pose, chain);

            if (owner != serializedObject)
            {
                owner.Update();
            }

            SeedPose(pose, bones);
            DrawBoneRows(pose, bones, chain);

            if (owner != serializedObject)
            {
                owner.ApplyModifiedProperties();
            }
        }

        private static void DrawBoneRows(SerializedProperty pose, List<Transform> bones, SerializedProperty chain)
        {
            bool useDummyBone = chain.FindPropertyRelative("_useDummyBone").boolValue;
            bool autoDummyLength = chain.FindPropertyRelative("_autoDummyLength").boolValue;

            DrawBoneSummary(pose, bones, useDummyBone, autoDummyLength);

            int count = Mathf.Min(pose.arraySize, bones.Count);
            for (int i = 0; i < count; i++)
            {
                DrawBoneRow(
                    pose.GetArrayElementAtIndex(i).FindPropertyRelative(nameof(FluffyBonePose.Skip)),
                    bones[i],
                    LeftOutForWantOfATip(bones, i, useDummyBone, autoDummyLength));
            }

            DrawNote("A bone turned off keeps whatever is animating it. The bones below it carry on "
                     + "swinging, from wherever it puts them.");
        }

        /// <summary>
        /// The count, and the two buttons worth having on a twenty-bone tail.
        /// </summary>
        private static void DrawBoneSummary(
            SerializedProperty pose, List<Transform> bones, bool useDummyBone, bool autoDummyLength)
        {
            int count = Mathf.Min(pose.arraySize, bones.Count);
            int simulated = 0;

            for (int i = 0; i < count; i++)
            {
                bool skipped = pose.GetArrayElementAtIndex(i)
                    .FindPropertyRelative(nameof(FluffyBonePose.Skip)).boolValue;

                if (!skipped && !LeftOutForWantOfATip(bones, i, useDummyBone, autoDummyLength))
                {
                    simulated++;
                }
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField(
                    $"{simulated} of {bones.Count} simulated", EditorStyles.miniLabel);

                if (GUILayout.Button("All", GUILayout.Width(BoneBulkButtonWidth)))
                {
                    SetSkipped(pose, count, false);
                }

                if (GUILayout.Button("None", GUILayout.Width(BoneBulkButtonWidth)))
                {
                    SetSkipped(pose, count, true);
                }
            }
        }

        /// <summary>
        /// One bone: the switch, the name, and the reason it is not moving when it is not.
        /// </summary>
        /// <remarks>
        /// The name is a label that pings rather than an object field. The bone is not
        /// being chosen here — the chain's two ends decide that — and a field would invite
        /// dropping a bone from somewhere else into a list that cannot take one.
        /// </remarks>
        private static void DrawBoneRow(SerializedProperty skip, Transform bone, bool leftOut)
        {
            Rect row = EditorGUI.IndentedRect(EditorGUILayout.GetControlRect());

            int indent = EditorGUI.indentLevel;
            EditorGUI.indentLevel = 0;

            var toggleRect = new Rect(row.x, row.y, BoneToggleWidth, row.height);
            var nameRect = new Rect(
                row.x + BoneToggleWidth,
                row.y,
                Mathf.Max(0f, row.width - BoneToggleWidth - BoneNoteWidth),
                row.height);
            var noteRect = new Rect(row.xMax - BoneNoteWidth, row.y, BoneNoteWidth, row.height);

            using (new EditorGUI.DisabledScope(leftOut))
            {
                EditorGUI.BeginChangeCheck();
                bool simulated = EditorGUI.Toggle(toggleRect, !skip.boolValue && !leftOut);

                if (EditorGUI.EndChangeCheck())
                {
                    skip.boolValue = !simulated;
                }
            }

            if (GUI.Button(nameRect, bone.name, EditorStyles.label))
            {
                EditorGUIUtility.PingObject(bone);
            }

            EditorGUI.LabelField(noteRect, Note(skip.boolValue, leftOut), RowNoteStyle);
            EditorGUI.indentLevel = indent;
        }

        private static string Note(bool skipped, bool leftOut)
        {
            if (leftOut)
            {
                return "no dummy bone";
            }

            return skipped ? "left to the animation" : string.Empty;
        }

        private static void SetSkipped(SerializedProperty pose, int count, bool skipped)
        {
            for (int i = 0; i < count; i++)
            {
                pose.GetArrayElementAtIndex(i)
                    .FindPropertyRelative(nameof(FluffyBonePose.Skip)).boolValue = skipped;
            }
        }

        /// <summary>
        /// Whether the solver leaves this bone alone whatever its switch says, because a
        /// chain with no dummy bone ends in one that has nothing to swing towards.
        /// </summary>
        private static bool LeftOutForWantOfATip(
            List<Transform> bones, int index, bool useDummyBone, bool autoDummyLength)
        {
            return !useDummyBone && FluffyChain.HasVirtualTip(bones, index, autoDummyLength);
        }
    }
}
