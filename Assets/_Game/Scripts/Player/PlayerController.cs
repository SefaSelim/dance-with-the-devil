using UnityEngine;

namespace DanceWithTheDevil
{
    /// <summary>
    /// Player movement, mouse aim and dodge (GDD §4 Kontroller / Kaçınma).
    /// Movement is camera-relative (W = screen up, using the camera rig's yaw). Aim: the rig's mouse ray against a
    /// horizontal plane at muzzle height; the body turns (yaw only) instantly toward the aim point.
    /// Dodge: fixed distance over a fixed time, invulnerable for its duration, charges refill one at a time.
    /// Expects the root pivot at the feet (set the CharacterController center to about half its height).
    /// </summary>
    [RequireComponent(typeof(CharacterController), typeof(Health))]
    [DisallowMultipleComponent]
    public sealed class PlayerController : MonoBehaviour
    {
        private const float MinDirectionSqr = 1e-4f;

        [Header("References")]
        [Tooltip("Camera rig used for aiming and camera-relative movement. Without it: world-relative movement, no mouse aim.")]
        [SerializeField] private PixelCameraRig _cameraRig;
        [Tooltip("Input reader. If empty, taken from this GameObject. Without it the player only moves through code.")]
        [SerializeField] private PlayerInputReader _input;

        [Header("Movement")]
        [Tooltip("Run speed in m/s (GDD §4: 6).")]
        [SerializeField, Min(0f)] private float _moveSpeed = 6f;
        [Tooltip("Gravity in m/s².")]
        [SerializeField, Min(0f)] private float _gravity = 25f;
        [Tooltip("Downward speed kept while grounded so the controller stays on slopes and steps.")]
        [SerializeField, Min(0f)] private float _groundedStickSpeed = 2f;

        [Header("Aim")]
        [Tooltip("Height of the aim plane above the player's feet (muzzle height, GDD: 1.0 m).")]
        [SerializeField] private float _aimHeight = 1f;

        [Header("Dodge")]
        [Tooltip("Dodge distance in metres (GDD §4: ~3).")]
        [SerializeField, Min(0f)] private float _dodgeDistance = 3f;
        [Tooltip("Dodge duration in seconds (GDD §4: 0.18). Invulnerable for this long.")]
        [SerializeField, Min(0.01f)] private float _dodgeDuration = 0.18f;
        [Tooltip("Refill time per dodge charge in seconds (GDD §4: 1.6). Charges refill one at a time.")]
        [SerializeField, Min(0.01f)] private float _dodgeCooldown = 1.6f;
        [Tooltip("Maximum dodge charges.")]
        [SerializeField, Min(1)] private int _dodgeCharges = 1;

        private CharacterController _characterController;
        private Health _health;

        private float _verticalVelocity;
        private Vector3 _moveDirection;
        private Vector3 _aimPoint;
        private Vector3 _aimDirection = Vector3.forward;

        private Vector3 _dodgeVelocity;
        private float _dodgeTimeRemaining;
        private int _dodgeChargesAvailable;
        private float _dodgeRefillTimer;

        private float _moveSpeedMultiplier = 1f;
        private float _dodgeCooldownMultiplier = 1f;

        public CharacterController CharacterController => _characterController;
        public Health Health => _health;
        public PixelCameraRig CameraRig
        {
            get => _cameraRig;
            set => _cameraRig = value;
        }
        public PlayerInputReader InputReader
        {
            get => _input;
            set => _input = value;
        }

        /// <summary>World aim point on the aim plane.</summary>
        public Vector3 AimPoint => _aimPoint;
        /// <summary>Flat (y = 0), normalised aim direction from the body.</summary>
        public Vector3 AimDirection => _aimDirection;
        /// <summary>Flat world move direction from input this frame (length 0..1).</summary>
        public Vector3 MoveDirection => _moveDirection;

        public bool IsDodging => _dodgeTimeRemaining > 0f;
        public int DodgeCharges => _dodgeChargesAvailable;
        public int MaxDodgeCharges => _dodgeCharges;
        /// <summary>0..1 progress of the charge currently refilling; 0 when all charges are full.</summary>
        public float DodgeRefillProgress =>
            _dodgeChargesAvailable >= _dodgeCharges ? 0f : Mathf.Clamp01(_dodgeRefillTimer / CurrentDodgeCooldown);

        /// <summary>Multiplies run speed (later contracts). Min 0.</summary>
        public float MoveSpeedMultiplier
        {
            get => _moveSpeedMultiplier;
            set => _moveSpeedMultiplier = Mathf.Max(0f, value);
        }

        /// <summary>Multiplies the per-charge dodge refill time (later contracts). Min 0.01.</summary>
        public float DodgeCooldownMultiplier
        {
            get => _dodgeCooldownMultiplier;
            set => _dodgeCooldownMultiplier = Mathf.Max(0.01f, value);
        }

        private float CurrentDodgeCooldown => _dodgeCooldown * _dodgeCooldownMultiplier;

        private void Awake()
        {
            _characterController = GetComponent<CharacterController>();
            _health = GetComponent<Health>();
            if (_input == null) _input = GetComponent<PlayerInputReader>();

            _dodgeChargesAvailable = _dodgeCharges;
            _aimDirection = Flatten(transform.forward, Vector3.forward);
            _aimPoint = transform.position + Vector3.up * _aimHeight + _aimDirection;
        }

        /// <summary>Sets the max dodge charges (min 1). Available charges are clamped; refill continues.</summary>
        public void SetDodgeCharges(int count)
        {
            _dodgeCharges = Mathf.Max(1, count);
            _dodgeChargesAvailable = Mathf.Min(_dodgeChargesAvailable, _dodgeCharges);
            if (_dodgeChargesAvailable >= _dodgeCharges) _dodgeRefillTimer = 0f;
        }

        /// <summary>
        /// Starts a dodge toward <paramref name="worldDirection"/> (flattened; if zero, the aim direction).
        /// Fails while dodging or with no charge. Makes Health invulnerable for the dodge duration.
        /// </summary>
        public bool TryDodge(Vector3 worldDirection)
        {
            if (IsDodging || _dodgeChargesAvailable <= 0) return false;

            Vector3 direction = Flatten(worldDirection, _aimDirection);
            _dodgeChargesAvailable--;
            _dodgeVelocity = direction * (_dodgeDistance / _dodgeDuration);
            _dodgeTimeRemaining = _dodgeDuration;
            if (_health != null) _health.SetInvulnerable(_dodgeDuration);
            return true;
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            TickDodgeRefill(dt);
            UpdateAim();

            _moveDirection = ReadMoveDirection();

            // Presses during a dodge are ignored, not buffered (GDD §4).
            if (_input != null && !IsDodging && _input.DodgePressed)
                TryDodge(_moveDirection);

            Vector3 horizontal;
            if (IsDodging)
            {
                // Clamp the last dodge frame so the total distance stays exact.
                float dodgeDt = Mathf.Min(dt, _dodgeTimeRemaining);
                horizontal = dt > 0f ? _dodgeVelocity * (dodgeDt / dt) : Vector3.zero;
                _dodgeTimeRemaining -= dt;
            }
            else
            {
                horizontal = _moveDirection * (_moveSpeed * _moveSpeedMultiplier);
            }

            if (_characterController.isGrounded && _verticalVelocity <= 0f)
                _verticalVelocity = -_groundedStickSpeed;
            else
                _verticalVelocity -= _gravity * dt;

            Vector3 velocity = horizontal;
            velocity.y = _verticalVelocity;
            if (_characterController.enabled) _characterController.Move(velocity * dt);
        }

        private void TickDodgeRefill(float dt)
        {
            if (_dodgeChargesAvailable >= _dodgeCharges)
            {
                _dodgeRefillTimer = 0f;
                return;
            }

            _dodgeRefillTimer += dt;
            float cooldown = CurrentDodgeCooldown;
            while (_dodgeRefillTimer >= cooldown && _dodgeChargesAvailable < _dodgeCharges)
            {
                _dodgeRefillTimer -= cooldown;
                _dodgeChargesAvailable++;
            }
            if (_dodgeChargesAvailable >= _dodgeCharges) _dodgeRefillTimer = 0f;
        }

        private Vector3 ReadMoveDirection()
        {
            if (_input == null) return Vector3.zero;
            Vector2 move = _input.MoveInput;
            if (move.sqrMagnitude < MinDirectionSqr) return Vector3.zero;

            Vector3 forward = Vector3.forward;
            Vector3 right = Vector3.right;
            if (_cameraRig != null)
            {
                Quaternion view = _cameraRig.ViewRotation;
                forward = Flatten(view * Vector3.forward, Vector3.forward);
                right = Flatten(view * Vector3.right, Vector3.right);
            }

            Vector3 direction = right * move.x + forward * move.y;
            return direction.sqrMagnitude > 1f ? direction.normalized : direction;
        }

        private void UpdateAim()
        {
            if (_cameraRig == null || !_cameraRig.isActiveAndEnabled || _input == null || !_input.HasMouse) return;

            Vector3 position = transform.position;
            Ray ray = _cameraRig.ScreenPointToWorldRay(_input.AimScreenPosition);
            var plane = new Plane(Vector3.up, new Vector3(0f, position.y + _aimHeight, 0f));
            if (!plane.Raycast(ray, out float enter)) return;

            _aimPoint = ray.GetPoint(enter);
            Vector3 toAim = _aimPoint - position;
            toAim.y = 0f;
            if (toAim.sqrMagnitude < MinDirectionSqr) return;

            _aimDirection = toAim.normalized;
            transform.rotation = Quaternion.LookRotation(_aimDirection, Vector3.up);
        }

        private static Vector3 Flatten(Vector3 direction, Vector3 fallback)
        {
            direction.y = 0f;
            if (direction.sqrMagnitude < MinDirectionSqr)
            {
                fallback.y = 0f;
                return fallback.sqrMagnitude < MinDirectionSqr ? Vector3.forward : fallback.normalized;
            }
            return direction.normalized;
        }
    }
}
