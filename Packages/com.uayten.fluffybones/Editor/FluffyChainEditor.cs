using UnityEditor;

namespace FluffyBones.Editor
{
    /// <summary>
    /// Custom inspector for <see cref="FluffyChain"/>.
    /// </summary>
    [CustomEditor(typeof(FluffyChain))]
    [CanEditMultipleObjects]
    public class FluffyChainEditor : UnityEditor.Editor
    {
        // TODO: inspector layout, grouped by profile override sections.
        // TODO: scene view handles for the chain and its limits.
        // TODO: "bake to profile" / "reset to rest pose" buttons.
    }
}
