using UnityEngine;

namespace FluffyBones
{
    /// <summary>
    /// A single chain of bones driven by the Fluffy Bones solver — a tail, one
    /// strand of a skirt, a lock of hair, a length of chain.
    /// </summary>
    /// <remarks>
    /// Placed on the first bone of the chain; the remaining bones are taken
    /// from the transform hierarchy below it.
    /// </remarks>
    [DisallowMultipleComponent]
    public class FluffyChain : MonoBehaviour
    {
        // TODO: root bone, chain discovery and the per-bone simulation state.
        // TODO: profile reference, with per-chain overrides.
        // TODO: registration with the owning FluffyBody.
    }
}
