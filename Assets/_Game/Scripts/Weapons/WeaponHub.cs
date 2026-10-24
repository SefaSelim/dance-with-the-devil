using UnityEngine;

namespace DanceWithTheDevil
{
    /// <summary>
    /// Drives the player's weapons from input (GDD §4 Kontroller, §5 Silahlar). Runs after PlayerController so it
    /// uses this frame's aim. Nothing fires while dodging, and presses during a dodge are dropped, not buffered.
    /// The rifle is silent while the machete swings. Each weapon also enforces its own blocks.
    /// </summary>
    [DefaultExecutionOrder(10)]
    [DisallowMultipleComponent]
    public sealed class WeaponHub : MonoBehaviour
    {
        [Tooltip("Input reader. If empty, taken from this GameObject.")]
        [SerializeField] private PlayerInputReader _input;
        [Tooltip("Player. If empty, taken from this GameObject.")]
        [SerializeField] private PlayerController _player;
        [Tooltip("Rifle. If empty, searched in children.")]
        [SerializeField] private Rifle _rifle;
        [Tooltip("Rocket launcher. If empty, searched in children.")]
        [SerializeField] private RocketLauncher _rocketLauncher;
        [Tooltip("Machete. If empty, searched in children.")]
        [SerializeField] private Machete _machete;

        public Rifle Rifle => _rifle;
        public RocketLauncher RocketLauncher => _rocketLauncher;
        public Machete Machete => _machete;

        private void Awake()
        {
            if (_input == null) _input = GetComponent<PlayerInputReader>();
            if (_player == null) _player = GetComponent<PlayerController>();
            if (_rifle == null) _rifle = GetComponentInChildren<Rifle>();
            if (_rocketLauncher == null) _rocketLauncher = GetComponentInChildren<RocketLauncher>();
            if (_machete == null) _machete = GetComponentInChildren<Machete>();
        }

        private void Update()
        {
            if (_input == null || _player == null || _player.Health.IsDead) return;
            if (_player.IsDodging) return;

            Vector3 aim = _player.AimDirection;

            if (_machete != null && _input.MeleePressed) _machete.TrySwing(aim);

            if (_rocketLauncher != null && _input.RocketPressed)
                _rocketLauncher.TryFire(_rocketLauncher.MuzzlePosition, aim);

            bool swinging = _machete != null && _machete.IsSwinging;
            if (_rifle != null && !swinging && _input.FireHeld)
                _rifle.TryFire(_rifle.MuzzlePosition, aim);
        }
    }
}
