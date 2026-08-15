using UnityEngine;

namespace FluffyBones
{
    /// <summary>
    /// Per-character owner of the Fluffy Bones simulation. Collects the chains
    /// and colliders under it and steps them together, so the whole character
    /// is solved in one pass with one update order.
    /// </summary>
    [DisallowMultipleComponent]
    public class FluffyBody : MonoBehaviour
    {
        // TODO: chain and collider registries.
        // TODO: update loop, fixed timestep and substepping.
        // TODO: teleport handling and reset-to-rest-pose.
    }
}
