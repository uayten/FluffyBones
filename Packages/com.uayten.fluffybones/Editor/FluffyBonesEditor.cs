using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Fluffy.Editor
{
    /// <summary>
    /// Inspector for <see cref="FluffyBones"/>: the mode switch, the profile slot
    /// with its create and duplicate actions, and the profile's own settings drawn
    /// inline so a chain can be tuned without leaving the character.
    /// </summary>
    [CustomEditor(typeof(FluffyBones))]
    public class FluffyBonesEditor : UnityEditor.Editor
    {
        private const string ProfileFolderHint = "Assets";
        private const float BoneLabelWidth = 110f;
        private const float PoseAssetLabelWidth = 175f;
        private const float AxisLabelWidth = 13f;
        private const float AxisSpacing = 4f;
        private const float DegreesPerPixel = 0.5f;
        private const float MinAngle = -180f;
        private const float MaxAngle = 180f;

        // Tuned for legibility on both editor skins rather than taken from
        // Handles.xAxisColor, whose blue is close to unreadable as small text.
        private static readonly Color AxisXColor = new Color(0.93f, 0.44f, 0.44f);
        private static readonly Color AxisYColor = new Color(0.55f, 0.85f, 0.36f);
        private static readonly Color AxisZColor = new Color(0.45f, 0.68f, 1f);

        private static readonly int DragHandleHint = "FluffyAngleDrag".GetHashCode();

        private static GUIStyle _axisXStyle;
        private static GUIStyle _axisYStyle;
        private static GUIStyle _axisZStyle;

        // Built on demand: EditorStyles is not ready while static fields initialise.
        private static GUIStyle AxisXStyle => _axisXStyle ??= AxisStyle(AxisXColor);
        private static GUIStyle AxisYStyle => _axisYStyle ??= AxisStyle(AxisYColor);
        private static GUIStyle AxisZStyle => _axisZStyle ??= AxisStyle(AxisZColor);

        private static GUIStyle AxisStyle(Color color)
        {
            return new GUIStyle(EditorStyles.label)
            {
                normal = { textColor = color },
                hover = { textColor = color },
                alignment = TextAnchor.MiddleLeft
            };
        }

        private SerializedProperty _mode;
        private SerializedProperty _profile;
        private SerializedProperty _chains;
        private SerializedProperty _detectionKeywords;
        private SerializedProperty _teleportThreshold;
        private SerializedProperty _showBones;
        private SerializedProperty _showAxes;
        private SerializedProperty _boneColor;

        private UnityEditor.Editor _profileEditor;
        private SerializedObject _poseSerialized;
        private bool _showAdvanced;
        private bool _showLimits;
        private int _editingChain;

        private void OnEnable()
        {
            _mode = serializedObject.FindProperty("_mode");
            _profile = serializedObject.FindProperty("_profile");
            _chains = serializedObject.FindProperty("_chains");
            _detectionKeywords = serializedObject.FindProperty("_detectionKeywords");
            _teleportThreshold = serializedObject.FindProperty("_teleportThreshold");
            _showBones = serializedObject.FindProperty("_showBones");
            _showAxes = serializedObject.FindProperty("_showAxes");
            _boneColor = serializedObject.FindProperty("_boneColor");
        }

        private void OnDisable()
        {
            if (_profileEditor != null)
            {
                DestroyImmediate(_profileEditor);
                _profileEditor = null;
            }

            _poseSerialized?.Dispose();
            _poseSerialized = null;
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            DrawMode();
            EditorGUILayout.Space();

            if ((FluffyChainMode)_mode.enumValueIndex == FluffyChainMode.Single)
            {
                DrawSingleChain();
            }
            else
            {
                DrawMultipleChains();
            }

            EditorGUILayout.Space();
            DrawDefaultPose();

            EditorGUILayout.Space();
            DrawProfile();

            EditorGUILayout.Space();
            DrawAdvanced();

            serializedObject.ApplyModifiedProperties();
        }

        private void DrawMode()
        {
            EditorGUILayout.LabelField("Setup", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(_mode, new GUIContent("Chains"));

            bool isSingle = (FluffyChainMode)_mode.enumValueIndex == FluffyChainMode.Single;
            EditorGUILayout.HelpBox(
                isSingle
                    ? "One chain — a tail, a ponytail, a cable."
                    : "Several chains sharing one profile — a skirt, a cape, a head of hair.",
                MessageType.None);
        }

        private void DrawSingleChain()
        {
            EditorGUILayout.LabelField("Chain", EditorStyles.boldLabel);

            // Single mode edits one entry. Keeping the list at exactly one element
            // means there is no second copy of the data to fall out of sync.
            if (_chains.arraySize == 0)
            {
                _chains.arraySize = 1;
            }
            else if (_chains.arraySize > 1)
            {
                EditorGUILayout.HelpBox(
                    $"{_chains.arraySize - 1} extra chain(s) are ignored in Single mode.",
                    MessageType.Warning);

                if (GUILayout.Button("Drop the extra chains"))
                {
                    _chains.arraySize = 1;
                }
            }

            SerializedProperty chain = _chains.GetArrayElementAtIndex(0);
            Transform character = ((FluffyBones)target).transform;

            FluffyBoneField.Draw(
                new GUIContent("Start Bone", "Where the chain starts."),
                chain.FindPropertyRelative("_startBone"),
                character);

            FluffyBoneField.Draw(
                new GUIContent("Last Bone", "Where the chain stops, included. "
                                            + "Leave empty to run to the end of the hierarchy."),
                chain.FindPropertyRelative("_lastBone"),
                character);

            FluffyChainDrawer.DrawDummyToggle(EditorGUILayout.GetControlRect(), chain);
            FluffyChainDrawer.DrawDummyLength(EditorGUILayout.GetControlRect(), chain);
        }

        private void DrawMultipleChains()
        {
            EditorGUILayout.LabelField("Chains", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(_chains, new GUIContent($"Chains ({_chains.arraySize})"), true);

            EditorGUILayout.Space(2f);

            if (GUILayout.Button("Detect chains"))
            {
                DetectChains();
            }

            EditorGUILayout.PropertyField(_detectionKeywords, new GUIContent("Detection Keywords"), true);
        }

        private void DrawDefaultPose()
        {
            var body = (FluffyBones)target;

            EditorGUILayout.LabelField("Default Pose", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(_showBones, new GUIContent("Show Bones"));

            EditorGUILayout.PropertyField(_showAxes, new GUIContent("Show Axes"));

            if (_showBones.boolValue || _showAxes.boolValue)
            {
                EditorGUILayout.PropertyField(_boneColor, new GUIContent("Bone Colour"));
            }

            EditorGUILayout.HelpBox(
                "The rotations below are the pose the chain springs back to. Edit them here "
                + "and the scene updates as you type, or rotate the bones in the scene and "
                + "press Capture to read them back in.",
                MessageType.None);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Capture from scene"))
                {
                    CaptureDefaultPose(body);
                }

                using (new EditorGUI.DisabledScope(!body.HasDefaultPose))
                {
                    if (GUILayout.Button("Apply to scene"))
                    {
                        ApplyDefaultPose(body);
                    }
                }
            }

            DrawBoneRotations();
        }

        /// <summary>
        /// One euler field per bone of the chain being edited. Typing in them moves the
        /// bone in the scene straight away, which is the whole point — posing a tail by
        /// numbers you cannot see is guesswork.
        /// </summary>
        private void DrawBoneRotations()
        {
            if (_chains.arraySize == 0)
            {
                return;
            }

            bool isMultiple = (FluffyChainMode)_mode.enumValueIndex == FluffyChainMode.Multiple;
            _editingChain = isMultiple && _chains.arraySize > 1 ? DrawChainSelector() : 0;

            SerializedProperty chain = _chains.GetArrayElementAtIndex(_editingChain);
            var startBone = chain.FindPropertyRelative("_startBone").objectReferenceValue as Transform;
            var lastBone = chain.FindPropertyRelative("_lastBone").objectReferenceValue as Transform;

            if (startBone == null)
            {
                EditorGUILayout.HelpBox("Assign a start bone to pose this chain.", MessageType.Info);
                return;
            }

            List<Transform> bones = FluffyChain.CollectChain(startBone, lastBone);
            SerializedProperty poseAsset = chain.FindPropertyRelative("_pose");

            DrawPoseAsset(poseAsset);

            // With an asset assigned, the rows edit the asset — which is what lets eight
            // skirt strands share one pose and be posed once.
            SerializedObject owner = ResolvePoseOwner(poseAsset, out SerializedProperty pose, chain);
            owner.Update();

            SeedPose(pose, bones);

            using (new EditorGUI.IndentLevelScope())
            {
                for (int i = 0; i < pose.arraySize; i++)
                {
                    SerializedProperty rotation = pose.GetArrayElementAtIndex(i)
                        .FindPropertyRelative(nameof(FluffyBonePose.Rotation));

                    if (i < bones.Count)
                    {
                        DrawBoneRotation(rotation, bones[i].name, bones[i]);
                        continue;
                    }

                    // Entries past the end of this chain: a pose written for a longer one.
                    // Shown greyed rather than hidden, so it is clear why the file is
                    // bigger than the chain.
                    using (new EditorGUI.DisabledScope(true))
                    {
                        DrawBoneRotation(rotation, $"(unused {i + 1})");
                    }
                }
            }

            DrawTrimButton(pose, bones.Count, owner);
            DrawAngleLimits(pose, bones);

            if (owner != serializedObject)
            {
                owner.ApplyModifiedProperties();
            }
        }

        /// <summary>
        /// Offers to drop the entries a shorter chain cannot use, and writes the file
        /// back out.
        /// </summary>
        private static void DrawTrimButton(SerializedProperty pose, int boneCount, SerializedObject owner)
        {
            int extra = pose.arraySize - boneCount;
            if (extra <= 0)
            {
                return;
            }

            EditorGUILayout.HelpBox(
                $"This pose describes {pose.arraySize} bones and the chain has {boneCount}. "
                + "The first ones are used and the rest are ignored.",
                MessageType.None);

            if (!GUILayout.Button($"Remove the {extra} unused entr{(extra == 1 ? "y" : "ies")}"))
            {
                return;
            }

            pose.arraySize = boneCount;
            owner.ApplyModifiedProperties();
            AssetDatabase.SaveAssets();
        }

        /// <summary>
        /// How far each bone may swing from the pose. Its own section rather than a
        /// fourth column, which would leave every row too narrow to read.
        /// </summary>
        private void DrawAngleLimits(SerializedProperty pose, List<Transform> bones)
        {
            EditorGUILayout.Space();

            _showLimits = EditorGUILayout.Foldout(_showLimits, "Angle Limits", true);
            if (!_showLimits)
            {
                return;
            }

            EditorGUILayout.HelpBox(
                "How far each bone may swing away from its pose, in degrees. 180 lets it go "
                + "anywhere, 0 pins it. Forward is the way a bone goes when the character "
                + "walks backwards; backward is where it flies when they walk forwards.",
                MessageType.None);

            using (new EditorGUI.IndentLevelScope())
            {
                DrawLimitHeader();

                int count = Mathf.Min(pose.arraySize, bones.Count);
                for (int i = 0; i < count; i++)
                {
                    SerializedProperty entry = pose.GetArrayElementAtIndex(i);
                    DrawBoneLimits(
                        entry.FindPropertyRelative(nameof(FluffyBonePose.ForwardLimit)),
                        entry.FindPropertyRelative(nameof(FluffyBonePose.BackwardLimit)),
                        bones[i].name);
                }
            }
        }

        private static void DrawLimitHeader()
        {
            Rect row = EditorGUI.IndentedRect(EditorGUILayout.GetControlRect());
            SplitLimitRow(row, out Rect _, out Rect forward, out Rect backward);

            int indent = EditorGUI.indentLevel;
            EditorGUI.indentLevel = 0;

            EditorGUI.LabelField(forward, "Forward", EditorStyles.miniLabel);
            EditorGUI.LabelField(backward, "Backward", EditorStyles.miniLabel);

            EditorGUI.indentLevel = indent;
        }

        private static void DrawBoneLimits(SerializedProperty forward, SerializedProperty backward, string label)
        {
            Rect row = EditorGUI.IndentedRect(EditorGUILayout.GetControlRect());
            SplitLimitRow(row, out Rect nameRect, out Rect forwardRect, out Rect backwardRect);

            int indent = EditorGUI.indentLevel;
            EditorGUI.indentLevel = 0;

            EditorGUI.LabelField(nameRect, label, EditorStyles.label);
            EditorGUI.PropertyField(forwardRect, forward, GUIContent.none);
            EditorGUI.PropertyField(backwardRect, backward, GUIContent.none);

            EditorGUI.indentLevel = indent;
        }

        private static void SplitLimitRow(Rect row, out Rect name, out Rect forward, out Rect backward)
        {
            name = new Rect(row.x, row.y, BoneLabelWidth, row.height);

            float width = (row.width - BoneLabelWidth - AxisSpacing) / 2f;
            forward = new Rect(row.x + BoneLabelWidth, row.y, width, row.height);
            backward = new Rect(forward.xMax + AxisSpacing, row.y, width, row.height);
        }

        private void DrawPoseAsset(SerializedProperty poseAsset)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                float previous = EditorGUIUtility.labelWidth;
                EditorGUIUtility.labelWidth = PoseAssetLabelWidth;

                EditorGUILayout.PropertyField(poseAsset, new GUIContent(
                    "Chain Bones Rotation Asset",
                    "A saved pose, shared with other chains. Rotations are local, so one "
                    + "asset fits every chain with the same bones."));

                EditorGUIUtility.labelWidth = previous;

                if (GUILayout.Button("New", GUILayout.Width(46f)))
                {
                    CreatePoseAsset(poseAsset);
                }
            }

            if (_chains.arraySize > 1 && GUILayout.Button("Copy this chain's setup to the others"))
            {
                CopySettingsToAllChains();
            }
        }

        /// <summary>
        /// Whichever object holds the rotations being edited: the pose asset when one is
        /// assigned, otherwise the component itself.
        /// </summary>
        private SerializedObject ResolvePoseOwner(
            SerializedProperty poseAsset, out SerializedProperty pose, SerializedProperty chain)
        {
            var asset = poseAsset.objectReferenceValue as FluffyPose;
            if (asset == null)
            {
                pose = chain.FindPropertyRelative("_defaultPose");
                return serializedObject;
            }

            if (_poseSerialized == null || _poseSerialized.targetObject != asset)
            {
                _poseSerialized = new SerializedObject(asset);
            }

            pose = _poseSerialized.FindProperty("_bones");
            return _poseSerialized;
        }

        /// <summary>
        /// Fills in entries for bones the pose does not cover yet, reading them off the
        /// scene so the fields show real rotations rather than zeros. Entries beyond the
        /// chain are left alone — a pose written for a longer chain stays intact.
        /// </summary>
        private static void SeedPose(SerializedProperty pose, List<Transform> bones)
        {
            if (pose.arraySize >= bones.Count)
            {
                return;
            }

            int first = pose.arraySize;
            pose.arraySize = bones.Count;

            for (int i = first; i < bones.Count; i++)
            {
                SerializedProperty entry = pose.GetArrayElementAtIndex(i);
                entry.FindPropertyRelative(nameof(FluffyBonePose.Rotation)).vector3Value =
                    NormalizeEuler(bones[i].localRotation.eulerAngles);
                entry.FindPropertyRelative(nameof(FluffyBonePose.ForwardLimit)).floatValue = FluffyBonePose.Free;
                entry.FindPropertyRelative(nameof(FluffyBonePose.BackwardLimit)).floatValue = FluffyBonePose.Free;
            }
        }

        private void CreatePoseAsset(SerializedProperty poseAsset)
        {
            string path = EditorUtility.SaveFilePanelInProject(
                "New Fluffy Pose",
                "FluffyPose",
                "asset",
                "Where should the pose be saved?",
                ProfileFolderHint);

            if (string.IsNullOrEmpty(path))
            {
                return;
            }

            var pose = CreateInstance<FluffyPose>();
            AssetDatabase.CreateAsset(pose, path);
            AssetDatabase.SaveAssets();

            poseAsset.objectReferenceValue = pose;
        }

        private void CopySettingsToAllChains()
        {
            var body = (FluffyBones)target;

            serializedObject.ApplyModifiedProperties();
            Undo.RecordObject(body, "Copy Fluffy Chain Setup");

            int changed = body.CopySettingsToAllChains(_editingChain);
            EditorUtility.SetDirty(body);
            serializedObject.Update();

            Debug.Log($"[Fluffy Bones] Copied the setup onto {changed} other chain(s) on '{body.name}'.", body);
        }

        private int DrawChainSelector()
        {
            var names = new string[_chains.arraySize];
            for (int i = 0; i < _chains.arraySize; i++)
            {
                var start = _chains.GetArrayElementAtIndex(i)
                    .FindPropertyRelative("_startBone").objectReferenceValue as Transform;

                names[i] = start != null ? start.name : $"Chain {i}";
            }

            int index = Mathf.Clamp(_editingChain, 0, _chains.arraySize - 1);
            return EditorGUILayout.Popup(new GUIContent("Editing Chain"), index, names);
        }

        private static void DrawBoneRotation(SerializedProperty rotation, string label, Transform bone = null)
        {
            Rect row = EditorGUI.IndentedRect(EditorGUILayout.GetControlRect());

            // Every label here is drawn with its own style. Tinting through
            // GUI.contentColor leaks into whatever the next control draws — which is
            // how the bone names came out red.
            int indent = EditorGUI.indentLevel;
            EditorGUI.indentLevel = 0;

            var nameRect = new Rect(row.x, row.y, BoneLabelWidth, row.height);
            EditorGUI.LabelField(nameRect, label, EditorStyles.label);

            var fields = new Rect(row.x + BoneLabelWidth, row.y, row.width - BoneLabelWidth, row.height);
            float width = (fields.width - AxisSpacing * 2f) / 3f;
            Vector3 euler = rotation.vector3Value;

            EditorGUI.BeginChangeCheck();

            euler.x = DrawAxis(new Rect(fields.x, fields.y, width, fields.height), "X", AxisXStyle, euler.x);
            euler.y = DrawAxis(new Rect(fields.x + width + AxisSpacing, fields.y, width, fields.height), "Y", AxisYStyle, euler.y);
            euler.z = DrawAxis(new Rect(fields.xMax - width, fields.y, width, fields.height), "Z", AxisZStyle, euler.z);

            bool changed = EditorGUI.EndChangeCheck();
            EditorGUI.indentLevel = indent;

            if (!changed)
            {
                return;
            }

            rotation.vector3Value = euler;

            if (bone == null)
            {
                return;
            }

            // Move the bone now rather than waiting for the next rebuild, so the scene
            // view follows the field while it is being dragged.
            Undo.RecordObject(bone, "Edit Fluffy Default Pose");
            bone.localRotation = Quaternion.Euler(euler);
        }

        /// <summary>
        /// One axis of a rotation: a letter tinted to match the axis colours in the
        /// scene view, which doubles as the drag handle, and the value beside it.
        /// </summary>
        private static float DrawAxis(Rect rect, string label, GUIStyle style, float value)
        {
            var labelRect = new Rect(rect.x, rect.y, AxisLabelWidth, rect.height);
            var fieldRect = new Rect(rect.x + AxisLabelWidth, rect.y, rect.width - AxisLabelWidth, rect.height);

            EditorGUI.LabelField(labelRect, label, style);
            value = DragAngle(labelRect, value);

            return ClampAngle(EditorGUI.FloatField(fieldRect, value));
        }

        /// <summary>
        /// Scrubs the value by dragging sideways on the axis letter, the way Unity's own
        /// numeric fields work. Written out because that behaviour comes free only when
        /// Unity draws the label, and it cannot draw a coloured one.
        /// </summary>
        private static float DragAngle(Rect handle, float value)
        {
            int id = GUIUtility.GetControlID(DragHandleHint, FocusType.Passive, handle);
            Event current = Event.current;

            EditorGUIUtility.AddCursorRect(handle, MouseCursor.SlideArrow);

            switch (current.GetTypeForControl(id))
            {
                case EventType.MouseDown when current.button == 0 && handle.Contains(current.mousePosition):
                    GUIUtility.hotControl = id;
                    GUIUtility.keyboardControl = 0;
                    current.Use();
                    break;

                case EventType.MouseDrag when GUIUtility.hotControl == id:
                    value = ClampAngle(value + HandleUtility.niceMouseDelta * DegreesPerPixel);
                    GUI.changed = true;
                    current.Use();
                    break;

                case EventType.MouseUp when GUIUtility.hotControl == id:
                    GUIUtility.hotControl = 0;
                    current.Use();
                    break;
            }

            return value;
        }

        /// <summary>
        /// Holds an angle in -180 to 180. Every rotation is reachable inside that range,
        /// so nothing is lost, and a drag cannot run off into the thousands.
        /// </summary>
        private static float ClampAngle(float angle)
        {
            return Mathf.Clamp(angle, MinAngle, MaxAngle);
        }

        /// <summary>
        /// Rewrites a rotation into -180 to 180 on every axis. Unity reports euler
        /// angles as 0 to 360, so a bone bent slightly back reads as 330 rather than
        /// -30 — the same rotation, but harder to pose with.
        /// </summary>
        private static Vector3 NormalizeEuler(Vector3 euler)
        {
            return new Vector3(NormalizeAngle(euler.x), NormalizeAngle(euler.y), NormalizeAngle(euler.z));
        }

        /// <summary>Rewrites an angle into -180 to 180 without changing the rotation.</summary>
        private static float NormalizeAngle(float angle)
        {
            angle %= 360f;

            if (angle > 180f)
            {
                return angle - 360f;
            }

            return angle < -180f ? angle + 360f : angle;
        }

        private void CaptureDefaultPose(FluffyBones body)
        {
            Undo.RecordObject(body, "Capture Fluffy Default Pose");

            int captured = body.CaptureDefaultPose();
            EditorUtility.SetDirty(body);
            serializedObject.Update();

            Debug.Log(captured > 0
                    ? $"[Fluffy Bones] Captured the default pose of {captured} chain(s) on '{body.name}'."
                    : $"[Fluffy Bones] Nothing captured on '{body.name}' — no chain has a start bone yet.",
                body);
        }

        private static void ApplyDefaultPose(FluffyBones body)
        {
            List<Transform> bones = body.CollectBones();
            if (bones.Count == 0)
            {
                return;
            }

            // Record the bones themselves — this moves transforms, not the component —
            // and record them before they move.
            Undo.RecordObjects(bones.ToArray(), "Apply Fluffy Default Pose");
            body.ApplyDefaultPose();
        }

        private void DrawProfile()
        {
            EditorGUILayout.LabelField("Behaviour", EditorStyles.boldLabel);

            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.PropertyField(_profile, new GUIContent("Profile"));

                using (new EditorGUI.DisabledScope(_profile.objectReferenceValue == null))
                {
                    if (GUILayout.Button("Duplicate", GUILayout.Width(70f)))
                    {
                        DuplicateProfile();
                    }
                }

                if (GUILayout.Button("New", GUILayout.Width(46f)))
                {
                    CreateProfile();
                }
            }

            if (_profile.objectReferenceValue == null)
            {
                EditorGUILayout.HelpBox(
                    "No profile assigned — the chains fall back to their built-in defaults. "
                    + "Create one to tune them, and reuse it on every character that should feel the same.",
                    MessageType.Info);
                return;
            }

            bool isReadOnly = IsInImmutablePackage(_profile.objectReferenceValue);
            if (isReadOnly)
            {
                EditorGUILayout.HelpBox(
                    "This profile ships with Fluffy Bones and cannot be edited. "
                    + "Press Duplicate to get a copy of your own.",
                    MessageType.None);
            }

            // The profile's own inspector, drawn inline: edits here write straight to
            // the asset, so every chain and character using it updates at once.
            CreateCachedEditor(_profile.objectReferenceValue, null, ref _profileEditor);
            if (_profileEditor == null)
            {
                return;
            }

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            using (new EditorGUI.DisabledScope(isReadOnly))
            {
                _profileEditor.OnInspectorGUI();
            }

            EditorGUILayout.LabelField(
                AssetDatabase.GetAssetPath(_profile.objectReferenceValue),
                EditorStyles.miniLabel);
        }

        private void DrawAdvanced()
        {
            _showAdvanced = EditorGUILayout.Foldout(_showAdvanced, "Advanced", true);
            if (!_showAdvanced)
            {
                return;
            }

            using (new EditorGUI.IndentLevelScope())
            {
                EditorGUILayout.PropertyField(_teleportThreshold, new GUIContent("Teleport Threshold"));

                using (new EditorGUI.DisabledScope(!Application.isPlaying))
                {
                    if (GUILayout.Button("Reset to rest pose"))
                    {
                        ((FluffyBones)target).ResetToRestPose();
                    }
                }
            }
        }

        /// <summary>
        /// Whether the asset lives in a package Unity treats as immutable — which is
        /// how every profile shipped inside Fluffy Bones reaches a customer. Embedded
        /// and local packages stay editable, so the plugin's own project can tune them.
        /// </summary>
        private static bool IsInImmutablePackage(Object asset)
        {
            string path = AssetDatabase.GetAssetPath(asset);
            if (string.IsNullOrEmpty(path))
            {
                return false;
            }

            UnityEditor.PackageManager.PackageInfo package =
                UnityEditor.PackageManager.PackageInfo.FindForAssetPath(path);

            return package != null
                   && package.source != UnityEditor.PackageManager.PackageSource.Embedded
                   && package.source != UnityEditor.PackageManager.PackageSource.Local;
        }

        private void DetectChains()
        {
            var body = (FluffyBones)target;
            Undo.RecordObject(body, "Detect Fluffy Chains");

            int added = body.DetectChains();
            EditorUtility.SetDirty(body);
            serializedObject.Update();

            Debug.Log(added > 0
                    ? $"[Fluffy Bones] Added {added} chain(s) to '{body.name}'."
                    : $"[Fluffy Bones] No new chains found on '{body.name}'. Check the detection "
                      + "keywords, or drag the root bones in by hand.",
                body);
        }

        private void CreateProfile()
        {
            string path = EditorUtility.SaveFilePanelInProject(
                "New Fluffy Profile",
                "FluffyProfile",
                "asset",
                "Where should the behaviour asset live?",
                ProfileFolderHint);

            if (string.IsNullOrEmpty(path))
            {
                return;
            }

            var profile = CreateInstance<FluffyProfile>();
            AssetDatabase.CreateAsset(profile, path);
            AssetDatabase.SaveAssets();

            _profile.objectReferenceValue = profile;
        }

        private void DuplicateProfile()
        {
            string source = AssetDatabase.GetAssetPath(_profile.objectReferenceValue);
            if (string.IsNullOrEmpty(source))
            {
                return;
            }

            string path = EditorUtility.SaveFilePanelInProject(
                "Duplicate Fluffy Profile",
                Path.GetFileNameWithoutExtension(source) + " Copy",
                "asset",
                "Where should the copy live?",
                Path.GetDirectoryName(source));

            if (string.IsNullOrEmpty(path) || !AssetDatabase.CopyAsset(source, path))
            {
                return;
            }

            AssetDatabase.SaveAssets();
            _profile.objectReferenceValue = AssetDatabase.LoadAssetAtPath<FluffyProfile>(path);
        }

        // TODO: scene view handles for the chains and their limits.
        // TODO: per-chain foldouts with a bone count, instead of the default list drawer.
    }
}
