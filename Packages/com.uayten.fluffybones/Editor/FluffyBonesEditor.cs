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

        private SerializedProperty _mode;
        private SerializedProperty _profile;
        private SerializedProperty _chains;
        private SerializedProperty _detectionKeywords;
        private SerializedProperty _teleportThreshold;

        private UnityEditor.Editor _profileEditor;
        private bool _showAdvanced;

        private void OnEnable()
        {
            _mode = serializedObject.FindProperty("_mode");
            _profile = serializedObject.FindProperty("_profile");
            _chains = serializedObject.FindProperty("_chains");
            _detectionKeywords = serializedObject.FindProperty("_detectionKeywords");
            _teleportThreshold = serializedObject.FindProperty("_teleportThreshold");
        }

        private void OnDisable()
        {
            if (_profileEditor != null)
            {
                DestroyImmediate(_profileEditor);
                _profileEditor = null;
            }
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
            EditorGUILayout.PropertyField(
                chain.FindPropertyRelative("_startBone"),
                new GUIContent("Start Bone", "Where the chain starts."));
            EditorGUILayout.PropertyField(
                chain.FindPropertyRelative("_lastBone"),
                new GUIContent("Last Bone", "Where the chain stops, included. "
                                            + "Leave empty to run to the end of the hierarchy."));
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
