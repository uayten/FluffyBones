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
        private const float RangeLabelWidth = 30f;
        private const float AxisSpacing = 4f;
        private const float OverrideToggleWidth = 74f;
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

        private static GUIStyle _sectionStyle;

        /// <summary>A foldout that reads as a section header rather than a field.</summary>
        private static GUIStyle SectionStyle =>
            _sectionStyle ??= new GUIStyle(EditorStyles.foldout) { fontStyle = FontStyle.Bold };

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
        private SerializedProperty _teleportDistance;
        private SerializedProperty _showBones;
        private SerializedProperty _showAxes;
        private SerializedProperty _showLimits;
        private SerializedProperty _boneColor;
        private SerializedProperty _limitSize;
        private SerializedProperty _simulationRate;

        /// <summary>Which face of the component the inspector is showing.</summary>
        private enum Tab
        {
            Setup,
            Pose,
            Limits,
            Behaviour,
            Advanced
        }

        private const string TabPreference = "Fluffy.InspectorTab";

        private UnityEditor.Editor _profileEditor;
        private SerializedObject _poseSerialized;
        private Tab _tab;
        private bool _rotationsExpanded = true;
        private int _editingChain;

        private void OnEnable()
        {
            _mode = serializedObject.FindProperty("_mode");
            _profile = serializedObject.FindProperty("_profile");
            _chains = serializedObject.FindProperty("_chains");
            _detectionKeywords = serializedObject.FindProperty("_detectionKeywords");
            _teleportDistance = serializedObject.FindProperty("_teleportDistance");
            _showBones = serializedObject.FindProperty("_showBones");
            _showAxes = serializedObject.FindProperty("_showAxes");
            _showLimits = serializedObject.FindProperty("_showLimits");
            _boneColor = serializedObject.FindProperty("_boneColor");
            _limitSize = serializedObject.FindProperty("_limitSize");
            _simulationRate = serializedObject.FindProperty("_simulationRate");

            _tab = (Tab)EditorPrefs.GetInt(TabPreference, 0);
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

            SeedAllChains(_chains);

            DrawTabs();
            EditorGUILayout.Space();

            switch (_tab)
            {
                case Tab.Setup:
                    DrawSetupTab();
                    break;

                case Tab.Pose:
                    DrawPoseTab();
                    break;

                case Tab.Limits:
                    DrawLimitsTab();
                    break;

                case Tab.Behaviour:
                    DrawProfile();
                    break;

                default:
                    DrawAdvancedTab();
                    break;
            }

            serializedObject.ApplyModifiedProperties();
        }

        /// <summary>
        /// The row of tabs, and the mark on the ones holding something worth knowing
        /// about from another tab.
        /// </summary>
        /// <remarks>
        /// Everything used to be drawn in a column, which meant scrolling the whole pose
        /// of a twenty-bone chain to reach the limits underneath it. A tab hides four
        /// fifths of the inspector, though, so a setting changed and forgotten is easy to
        /// lose: the dot says a tab is holding something other than its default.
        /// </remarks>
        private void DrawTabs()
        {
            var labels = new[]
            {
                new GUIContent(Marked("Setup", HasChains)),
                new GUIContent(Marked("Pose", HasPose)),
                new GUIContent(Marked("Limits", HasLimits)),
                new GUIContent(Marked("Behaviour", _profile.objectReferenceValue != null)),
                new GUIContent(Marked("Advanced", _showLimits.boolValue || _showAxes.boolValue))
            };

            int chosen = GUILayout.Toolbar((int)_tab, labels, GUILayout.Height(24f));

            if (chosen == (int)_tab)
            {
                return;
            }

            _tab = (Tab)chosen;

            // Remembered rather than reset, because the tab you were in is almost always
            // the one you want when you come back to the object.
            EditorPrefs.SetInt(TabPreference, chosen);
            GUI.FocusControl(null);
        }

        private static string Marked(string label, bool marked)
        {
            return marked ? label + " •" : label;
        }

        /// <summary>
        /// Gives every chain a pose, not only the one the dropdown is showing.
        /// </summary>
        /// <remarks>
        /// A chain with no pose of its own takes whatever its bones happen to be when it
        /// is built, so anything that moves those transforms first quietly becomes the
        /// pose it springs back to. Seeding them all the moment the inspector is open
        /// settles it while the bones are still where the rig put them — which is a
        /// scene wrecked once already, seven strands of a skirt adopting a pose they were
        /// left in by accident.
        ///
        /// Chains reading a shared pose asset are left alone: their pose is a file, and
        /// writing to it on behalf of one chain would change every chain sharing it.
        /// </remarks>
        internal static void SeedAllChains(SerializedProperty chains)
        {
            for (int i = 0; i < chains.arraySize; i++)
            {
                SerializedProperty chain = chains.GetArrayElementAtIndex(i);

                if (chain.FindPropertyRelative("_pose").objectReferenceValue != null)
                {
                    continue;
                }

                var start = chain.FindPropertyRelative("_startBone").objectReferenceValue as Transform;
                if (start == null)
                {
                    continue;
                }

                var last = chain.FindPropertyRelative("_lastBone").objectReferenceValue as Transform;
                SeedPose(chain.FindPropertyRelative("_defaultPose"), FluffyChain.CollectChain(start, last));
            }
        }

        /// <summary>Whether any chain has a bone in it.</summary>
        private bool HasChains
        {
            get
            {
                for (int i = 0; i < _chains.arraySize; i++)
                {
                    if (_chains.GetArrayElementAtIndex(i)
                        .FindPropertyRelative("_startBone").objectReferenceValue != null)
                    {
                        return true;
                    }
                }

                return false;
            }
        }

        /// <summary>Whether the chain being edited rests at something other than its import pose.</summary>
        private bool HasPose => ((FluffyBones)target).HasDefaultPose;

        /// <summary>Whether anything is holding a bone back.</summary>
        private bool HasLimits
        {
            get
            {
                for (int i = 0; i < _chains.arraySize; i++)
                {
                    SerializedProperty limits = _chains.GetArrayElementAtIndex(i)
                        .FindPropertyRelative("_globalLimits");

                    if (!FluffyLimits.IsFree(limits.FindPropertyRelative(nameof(FluffyLimits.SwingY)).vector2Value)
                        || !FluffyLimits.IsFree(limits.FindPropertyRelative(nameof(FluffyLimits.SwingZ)).vector2Value))
                    {
                        return true;
                    }
                }

                return false;
            }
        }

        private void DrawSetupTab()
        {
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
        }

        private void DrawPoseTab()
        {
            var body = (FluffyBones)target;

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

            DrawChainPose(rotations: true, limits: false);
        }

        private void DrawLimitsTab()
        {
            DrawChainPose(rotations: false, limits: true);
        }

        private void DrawAdvancedTab()
        {
            EditorGUILayout.PropertyField(_simulationRate, new GUIContent(
                "Simulation Rate",
                "How many times a second the chains are solved. Steps are this long "
                + "whatever the frame rate, and what is drawn is worked out between the "
                + "last two, so a chain behaves the same on every machine."));

            EditorGUILayout.PropertyField(_teleportDistance, new GUIContent(
                "Teleport Distance",
                "Movement further than this between two frames, in world units, is "
                + "more than the chains can swing through: they are carried along "
                + "rigidly for it instead. Around a bone's length is a good value."));

            EditorGUILayout.Space();

            using (new EditorGUI.DisabledScope(!Application.isPlaying))
            {
                if (GUILayout.Button("Reset to rest pose"))
                {
                    ((FluffyBones)target).ResetToRestPose();
                }
            }
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
            // Single mode edits one entry. Keeping the list at exactly one element
            // means there is no second copy of the data to fall out of sync.
            if (_chains.arraySize == 0)
            {
                _chains.arraySize = 1;
                InitialiseChain(_chains.GetArrayElementAtIndex(0));
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
            int before = _chains.arraySize;
            EditorGUILayout.PropertyField(_chains, new GUIContent($"Chains ({_chains.arraySize})"), true);

            for (int i = before; i < _chains.arraySize; i++)
            {
                InitialiseChain(_chains.GetArrayElementAtIndex(i));
            }

            EditorGUILayout.Space(2f);

            if (GUILayout.Button("Detect chains"))
            {
                DetectChains();
            }

            EditorGUILayout.PropertyField(_detectionKeywords, new GUIContent("Detection Keywords"), true);
        }

        /// <summary>
        /// Writes into a list entry the inspector has just made what the chain's own
        /// field initialisers would have put there.
        /// </summary>
        /// <remarks>
        /// Unity builds a new array element by zeroing it, not by running the class's
        /// initialisers, and every zero here means the opposite of the default: no dummy
        /// bone, no automatic length, and limits of 0 to 0 on all three axes, which the
        /// solver reads as a bone that may not leave its pose. A chain added with the
        /// list's + button was being born frozen.
        ///
        /// Entries Unity filled by copying the one before them come out authored, and
        /// are left alone — inheriting the previous strand's setup is what you want from
        /// a + button on a skirt.
        /// </remarks>
        internal static void InitialiseChain(SerializedProperty chain)
        {
            SerializedProperty limits = chain.FindPropertyRelative("_globalLimits");
            SerializedProperty swingY = limits.FindPropertyRelative(nameof(FluffyLimits.SwingY));
            SerializedProperty swingZ = limits.FindPropertyRelative(nameof(FluffyLimits.SwingZ));
            SerializedProperty twist = limits.FindPropertyRelative(nameof(FluffyLimits.Twist));

            bool zeroed = swingY.vector2Value == Vector2.zero
                          && swingZ.vector2Value == Vector2.zero
                          && twist.vector2Value == Vector2.zero;

            if (!zeroed)
            {
                return;
            }

            swingY.vector2Value = FluffyLimits.FreeRange;
            swingZ.vector2Value = FluffyLimits.FreeRange;
            twist.vector2Value = FluffyLimits.FreeRange;

            chain.FindPropertyRelative("_useDummyBone").boolValue = true;
            chain.FindPropertyRelative("_autoDummyLength").boolValue = true;
            chain.FindPropertyRelative("_dummyLength").floatValue = FluffyChain.DefaultDummyLength;
        }

        /// <summary>
        /// The chain being edited, and whichever of its two halves the tab asked for:
        /// one euler field per bone, and how far each may swing.
        /// </summary>
        /// <remarks>
        /// Both halves need the same half page of work first — which chain, which bones,
        /// and whether the pose lives on the component or in a shared asset — so they are
        /// drawn from one place rather than each resolving it again. Typing in a rotation
        /// moves the bone in the scene straight away, which is the whole point: posing a
        /// tail by numbers you cannot see is guesswork.
        /// </remarks>
        private void DrawChainPose(bool rotations, bool limits)
        {
            if (_chains.arraySize == 0)
            {
                EditorGUILayout.HelpBox("No chains yet. Add one under Setup.", MessageType.Info);
                return;
            }

            bool isMultiple = (FluffyChainMode)_mode.enumValueIndex == FluffyChainMode.Multiple;
            _editingChain = isMultiple && _chains.arraySize > 1 ? DrawChainSelector() : 0;

            SerializedProperty chain = _chains.GetArrayElementAtIndex(_editingChain);
            var startBone = chain.FindPropertyRelative("_startBone").objectReferenceValue as Transform;
            var lastBone = chain.FindPropertyRelative("_lastBone").objectReferenceValue as Transform;

            if (startBone == null)
            {
                EditorGUILayout.HelpBox(
                    "This chain has no start bone yet. Assign one under Setup.", MessageType.Info);
                return;
            }

            List<Transform> bones = FluffyChain.CollectChain(startBone, lastBone);
            SerializedProperty poseAsset = chain.FindPropertyRelative("_pose");

            // With an asset assigned, the rows edit the asset — which is what lets eight
            // skirt strands share one pose and be posed once.
            SerializedObject owner = ResolvePoseOwner(poseAsset, out SerializedProperty pose, chain);

            // Only the asset's. Refreshing the component's own halfway through drawing it
            // throws away everything edited higher up the inspector this frame, which is
            // why picking Multiple sprang back to Single: the popup wrote the new mode,
            // and this read the old one back over it before it was ever applied.
            if (owner != serializedObject)
            {
                owner.Update();
            }

            SeedPose(pose, bones);

            if (rotations && DrawSubSection("Chain Bones Rotation Asset", ref _rotationsExpanded))
            {
                using (new EditorGUI.IndentLevelScope())
                {
                    DrawPoseAsset(poseAsset);
                    DrawRotationRows(pose, bones);
                    DrawTrimButton(pose, bones.Count, owner);
                }
            }

            if (limits)
            {
                DrawAngleLimits(pose, bones, owner, chain);
            }

            if (owner != serializedObject)
            {
                owner.ApplyModifiedProperties();
            }
        }

        private static void DrawRotationRows(SerializedProperty pose, List<Transform> bones)
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

        /// <summary>A section nested inside another, so the hierarchy stays readable.</summary>
        private static bool DrawSubSection(string title, ref bool expanded)
        {
            EditorGUILayout.Space(2f);
            expanded = EditorGUILayout.Foldout(expanded, title, true);

            return expanded;
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
        /// How far each bone may swing from the pose. Its own tab rather than a fourth
        /// column, which would leave every row too narrow to read.
        /// </summary>
        private void DrawAngleLimits(
            SerializedProperty pose, List<Transform> bones, SerializedObject owner, SerializedProperty chain)
        {
            EditorGUILayout.PropertyField(_showLimits, new GUIContent("Show Limits"));

            if (_showLimits.boolValue)
            {
                EditorGUILayout.PropertyField(_limitSize, new GUIContent(
                    "Limit Size",
                    "How big the shapes are drawn, as a fraction of the bone's length. "
                    + "Turn it down when neighbouring bones' shapes run into each other."));
            }

            EditorGUILayout.HelpBox(
                "How far a bone may turn from its pose, in degrees, per axis. Every bone "
                + "takes the global limits unless it overrides them. Each shape drawn "
                + "spans its own range: nothing at 0 to 0, all the way round at -180 to "
                + "180, which is free.",
                MessageType.None);

            SerializedProperty global = ResolveGlobalLimits(owner, chain);
            EditorGUILayout.LabelField("Global", EditorStyles.miniBoldLabel);

            using (new EditorGUI.IndentLevelScope())
            {
                DrawLimits(global);
            }

            EditorGUILayout.Space(2f);
            EditorGUILayout.LabelField("Per Bone", EditorStyles.miniBoldLabel);

            using (new EditorGUI.IndentLevelScope())
            {
                int count = Mathf.Min(pose.arraySize, bones.Count);
                for (int i = 0; i < count; i++)
                {
                    DrawBoneLimits(pose.GetArrayElementAtIndex(i), bones[i].name, global);
                }
            }
        }

        /// <summary>The global limits of whichever object holds this chain's pose.</summary>
        private SerializedProperty ResolveGlobalLimits(SerializedObject owner, SerializedProperty chain)
        {
            return owner == serializedObject
                ? chain.FindPropertyRelative("_globalLimits")
                : owner.FindProperty("_globalLimits");
        }

        /// <summary>
        /// One bone's limits, behind a foldout. Closed it shows whether the bone follows
        /// the global setting; open it shows the three axes and how to go back.
        /// </summary>
        private static void DrawBoneLimits(SerializedProperty entry, string label, SerializedProperty global)
        {
            SerializedProperty overrides = entry.FindPropertyRelative(nameof(FluffyBonePose.OverrideLimits));

            Rect row = EditorGUI.IndentedRect(EditorGUILayout.GetControlRect());
            var foldoutRect = new Rect(row.x, row.y, row.width - OverrideToggleWidth, row.height);
            var toggleRect = new Rect(row.xMax - OverrideToggleWidth, row.y, OverrideToggleWidth, row.height);

            int indent = EditorGUI.indentLevel;
            EditorGUI.indentLevel = 0;

            entry.isExpanded = EditorGUI.Foldout(foldoutRect, entry.isExpanded, label, true);
            overrides.boolValue = EditorGUI.ToggleLeft(toggleRect, "Override", overrides.boolValue);

            EditorGUI.indentLevel = indent;

            if (!entry.isExpanded)
            {
                return;
            }

            using (new EditorGUI.IndentLevelScope())
            {
                if (!overrides.boolValue)
                {
                    using (new EditorGUI.DisabledScope(true))
                    {
                        DrawLimits(global);
                    }

                    return;
                }

                DrawLimits(entry.FindPropertyRelative(nameof(FluffyBonePose.Limits)));

                if (GUILayout.Button("Reset to global"))
                {
                    CopyLimits(global, entry.FindPropertyRelative(nameof(FluffyBonePose.Limits)));
                    overrides.boolValue = false;
                }
            }
        }

        private static void DrawLimits(SerializedProperty limits)
        {
            DrawRange(limits.FindPropertyRelative(nameof(FluffyLimits.SwingY)), "Y Swing", AxisYStyle);
            DrawRange(limits.FindPropertyRelative(nameof(FluffyLimits.SwingZ)), "Z Swing", AxisZStyle);

            // Hidden while the solver produces no twist to hold back. The value is still
            // stored, shared and copied, so nothing is lost by not showing a field that
            // would do nothing.
            if (FluffyLimits.TwistEnforced)
            {
                DrawRange(limits.FindPropertyRelative(nameof(FluffyLimits.Twist)), "X Twist", AxisXStyle);
            }
        }

        private static void CopyLimits(SerializedProperty from, SerializedProperty to)
        {
            to.FindPropertyRelative(nameof(FluffyLimits.SwingY)).vector2Value =
                from.FindPropertyRelative(nameof(FluffyLimits.SwingY)).vector2Value;
            to.FindPropertyRelative(nameof(FluffyLimits.SwingZ)).vector2Value =
                from.FindPropertyRelative(nameof(FluffyLimits.SwingZ)).vector2Value;
            to.FindPropertyRelative(nameof(FluffyLimits.Twist)).vector2Value =
                from.FindPropertyRelative(nameof(FluffyLimits.Twist)).vector2Value;
        }

        /// <summary>
        /// A minimum and a maximum in degrees, side by side, under a label in the axis's
        /// own colour so it matches the arc drawn in the scene. The axis letter cannot be
        /// the drag handle here the way it is for a rotation — one label sits over two
        /// numbers — so each end gets its own word, tinted the same colour, as its handle.
        /// </summary>
        private static void DrawRange(SerializedProperty range, string label, GUIStyle style)
        {
            Rect row = EditorGUI.IndentedRect(EditorGUILayout.GetControlRect());

            int indent = EditorGUI.indentLevel;
            EditorGUI.indentLevel = 0;

            var nameRect = new Rect(row.x, row.y, BoneLabelWidth, row.height);
            EditorGUI.LabelField(nameRect, label, style);

            float width = (row.width - BoneLabelWidth - AxisSpacing) / 2f;
            var minRect = new Rect(row.x + BoneLabelWidth, row.y, width, row.height);
            var maxRect = new Rect(minRect.xMax + AxisSpacing, row.y, width, row.height);

            Vector2 value = range.vector2Value;

            EditorGUI.BeginChangeCheck();
            float min = DrawAngle(minRect, "Min", style, RangeLabelWidth, value.x);
            float max = DrawAngle(maxRect, "Max", style, RangeLabelWidth, value.y);

            if (EditorGUI.EndChangeCheck())
            {
                // The range is measured from the pose, so the minimum stops at 0 and the
                // maximum starts there — a drag runs out of room at the pose rather than
                // through it, and the two ends can never cross.
                range.vector2Value = FluffyLimits.ClampRange(new Vector2(min, max));
            }

            EditorGUI.indentLevel = indent;
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
                entry.FindPropertyRelative(nameof(FluffyBonePose.OverrideLimits)).boolValue = false;

                SerializedProperty limits = entry.FindPropertyRelative(nameof(FluffyBonePose.Limits));
                limits.FindPropertyRelative(nameof(FluffyLimits.SwingY)).vector2Value = FluffyLimits.FreeRange;
                limits.FindPropertyRelative(nameof(FluffyLimits.SwingZ)).vector2Value = FluffyLimits.FreeRange;
                limits.FindPropertyRelative(nameof(FluffyLimits.Twist)).vector2Value = FluffyLimits.FreeRange;
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
            return DrawAngle(rect, label, style, AxisLabelWidth, value);
        }

        /// <summary>
        /// An angle field with a tinted label that doubles as the drag handle. The label
        /// names whatever the number is — an axis in a rotation, an end of a range — so
        /// the width it needs comes from the caller.
        /// </summary>
        private static float DrawAngle(Rect rect, string label, GUIStyle style, float labelWidth, float value)
        {
            var labelRect = new Rect(rect.x, rect.y, labelWidth, rect.height);
            var fieldRect = new Rect(rect.x + labelWidth, rect.y, rect.width - labelWidth, rect.height);

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

            // A field greys itself out on its own; a hand-rolled handle does not, and the
            // global limits are drawn disabled under every bone that does not override
            // them — dragging there would edit the greyed row.
            if (!GUI.enabled)
            {
                return value;
            }

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
            // Anything edited earlier this pass goes in before the object is written to
            // directly, or the Update below reads over it.
            serializedObject.ApplyModifiedProperties();
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

            // Same as above: apply first, so switching to Multiple and pressing Detect
            // does not lose the switch.
            serializedObject.ApplyModifiedProperties();
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
