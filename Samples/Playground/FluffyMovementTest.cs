using UnityEngine;

namespace Fluffy.Samples
{
    /// <summary>
    /// Walks the object it sits on from side to side, so the chains hanging off it
    /// have something to react to. Stands in for the character in a scene that has
    /// no model and no animation, and is the movement everything in the playground
    /// is tested against.
    /// </summary>
    /// <remarks>
    /// Nothing here reads the keyboard. The playground has to open in any project,
    /// and a project set to the new Input System throws on the old <c>Input</c> class
    /// while one set to the old one has no new package to read — so the teleport is a
    /// context menu item on the component instead, which works in both and in play
    /// mode.
    /// </remarks>
    [AddComponentMenu("Fluffy Bones/Samples/Fluffy Movement Test")]
    public class FluffyMovementTest : MonoBehaviour
    {
        [Tooltip("How far it travels either side of where it started, in world units.")]
        [SerializeField] private Vector3 _travel = new Vector3(1.2f, 0f, 0f);

        [Tooltip("How far it turns either way, in degrees around its own Y.")]
        [SerializeField] private float _turn = 35f;

        [Tooltip("Trips back and forth per second. Slow shows the lag, fast shows the whip.")]
        [Min(0f)]
        [SerializeField] private float _speed = 0.4f;

        [Tooltip("How far Teleport jumps, in world units. For trying Teleport Distance on " +
                 "the component: a jump this size in one frame is more than the chains can " +
                 "swing through, so they are carried along instead of being flung.")]
        [SerializeField] private Vector3 _teleport = new Vector3(0f, 0f, 6f);

        private Vector3 _origin;
        private Quaternion _restRotation;
        private float _phase;

        private void OnEnable()
        {
            _origin = transform.position;
            _restRotation = transform.rotation;
        }

        private void OnDisable()
        {
            transform.SetPositionAndRotation(_origin, _restRotation);
        }

        private void Update()
        {
            _phase += Time.deltaTime * _speed * Mathf.PI * 2f;
            float wave = Mathf.Sin(_phase);

            transform.SetPositionAndRotation(
                _origin + _travel * wave,
                _restRotation * Quaternion.Euler(0f, _turn * wave, 0f));
        }

        /// <summary>Jumps the character, so Teleport Distance can be watched working.</summary>
        [ContextMenu("Teleport")]
        public void Teleport()
        {
            _origin += _teleport;
        }
    }
}
