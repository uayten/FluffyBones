using UnityEngine;

namespace FluffyBones
{
    /// <summary>
    /// A collision shape that Fluffy Bones chains are pushed out of.
    /// </summary>
    /// <remarks>
    /// Independent of Unity's physics colliders: the solver reads these
    /// directly, so no rigidbodies or physics layers are involved.
    /// </remarks>
    public class FluffyCollider : MonoBehaviour
    {
        // TODO: shape kind (sphere, capsule, plane) and its dimensions.
        // TODO: inside/outside mode, for containment volumes.
        // TODO: gizmo drawing.
    }
}
