using UnityEngine;

namespace FluffyBones
{
    /// <summary>
    /// Reusable set of tuning values for the Fluffy Bones solver — stiffness,
    /// damping, gravity, limits — shareable between chains and characters.
    /// </summary>
    [CreateAssetMenu(fileName = "FluffyProfile", menuName = "Fluffy Bones/Profile")]
    public class FluffyProfile : ScriptableObject
    {
        // TODO: stiffness, damping, drag and gravity.
        // TODO: angle and stretch limits.
        // TODO: curves for weighting values along the chain.
    }
}
