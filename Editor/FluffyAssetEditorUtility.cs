using System.IO;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

namespace Fluffy.Editor
{
    /// <summary>Shared save, revert, and folder-memory behaviour for Fluffy Bones assets.</summary>
    internal static class FluffyAssetEditorUtility
    {
        private const string LastFolderPreference = "Fluffy.LastAssetFolder";
        private const string DefaultFolder = "Assets";

        private static readonly GUIContent SaveLabel = new GUIContent(
            "Save",
            "Write this asset to its file now. Greyed out when the file already matches "
            + "what is on screen.");

        private static readonly GUIContent RevertLabel = new GUIContent(
            "Revert",
            "Throw away the edits made since the last save and read the values back "
            + "from the file.");

        /// <summary>The last valid project folder used by a Fluffy Bones save dialog.</summary>
        internal static string LastFolder
        {
            get
            {
                string folder = EditorPrefs.GetString(LastFolderPreference, DefaultFolder);
                return AssetDatabase.IsValidFolder(folder) ? folder : DefaultFolder;
            }
        }

        /// <summary>Remembers the containing folder of a path returned by a save dialog.</summary>
        internal static void RememberFolder(string assetPath)
        {
            string folder = Path.GetDirectoryName(assetPath)?.Replace('\\', '/');
            if (!string.IsNullOrEmpty(folder) && AssetDatabase.IsValidFolder(folder))
            {
                EditorPrefs.SetString(LastFolderPreference, folder);
            }
        }

        internal static bool HasUnsavedChanges(Object asset)
        {
            return asset != null
                   && !string.IsNullOrEmpty(AssetDatabase.GetAssetPath(asset))
                   && EditorUtility.IsDirty(asset);
        }

        /// <summary>Draws Revert and Save in the caller's current horizontal row.</summary>
        /// <returns>True when the asset was reverted and serialized views must refresh.</returns>
        internal static bool DrawSaveButtons<T>(T asset, string undoName) where T : Object
        {
            bool dirty = HasUnsavedChanges(asset);
            bool reverted = false;

            using (new EditorGUI.DisabledScope(!dirty))
            {
                if (GUILayout.Button(RevertLabel, GUILayout.Width(56f)))
                {
                    reverted = RevertToDisk(asset, undoName);
                }

                if (GUILayout.Button(SaveLabel, GUILayout.Width(46f)))
                {
                    AssetDatabase.SaveAssetIfDirty(asset);
                }
            }

            return reverted;
        }

        /// <summary>Restores an asset from its serialized file without reusing its dirty instance.</summary>
        internal static bool RevertToDisk<T>(T asset, string undoName) where T : Object
        {
            if (asset == null)
            {
                return false;
            }

            string path = AssetDatabase.GetAssetPath(asset);
            if (string.IsNullOrEmpty(path))
            {
                return false;
            }

            Object[] fromDisk = InternalEditorUtility.LoadSerializedFileAndForget(path);
            bool reverted = false;

            for (int i = 0; i < fromDisk.Length; i++)
            {
                if (fromDisk[i] is T saved)
                {
                    Undo.RecordObject(asset, undoName);
                    EditorUtility.CopySerialized(saved, asset);
                    EditorUtility.ClearDirty(asset);
                    reverted = true;
                    break;
                }
            }

            for (int i = 0; i < fromDisk.Length; i++)
            {
                Object.DestroyImmediate(fromDisk[i]);
            }

            return reverted;
        }
    }
}
