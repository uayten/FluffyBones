using UnityEditor;
using UnityEngine;

namespace FluffyBones.Editor
{
    /// <summary>
    /// Custom inspector for <see cref="FluffyBody"/>, with the chain detection button.
    /// </summary>
    [CustomEditor(typeof(FluffyBody))]
    [CanEditMultipleObjects]
    public class FluffyBodyEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            EditorGUILayout.Space();

            if (GUILayout.Button("Detect chains"))
            {
                DetectChains();
            }

            using (new EditorGUI.DisabledScope(!Application.isPlaying))
            {
                if (GUILayout.Button("Reset to rest pose"))
                {
                    foreach (Object item in targets)
                    {
                        ((FluffyBody)item).ResetToRestPose();
                    }
                }
            }
        }

        private void DetectChains()
        {
            foreach (Object item in targets)
            {
                var body = (FluffyBody)item;
                Undo.RecordObject(body, "Detect Fluffy Chains");

                int added = body.DetectChains();
                EditorUtility.SetDirty(body);

                Debug.Log(added > 0
                        ? $"[Fluffy Bones] Added {added} chain(s) to '{body.name}'."
                        : $"[Fluffy Bones] No new chains found on '{body.name}'. Check the detection " +
                          "keywords, or add the root bones by hand.",
                    body);
            }
        }

        // TODO: scene view handles for the chains and their limits.
        // TODO: per-chain foldouts instead of the default list drawer.
    }
}
