using UnityEngine;

namespace DanceWithTheDevil
{
    /// <summary>
    /// Player movement, mouse aim and dodge (GDD §4 Kontroller / Kaçınma).
    /// Movement is camera-relative (W = screen up, using the camera rig's yaw), with a very short ramp up and down
    /// (about 0.06 s to full speed, 0.05 s to a stop) so it stays tight without sliding like a chess piece.
    /// Aim (GDD §4 "imleç noktasına nişan"): the rig's mouse ray. If it first touches a damageable (an enemy), the aim
    /// point is that target's centre; otherwise it is the ray's crossing with a horizontal plane at about an enemy's
    /// body height. Weapons fire from their muzzle toward this point, so shots converge on the cursor. The body turns
    /// (yaw only) quickly and smoothly toward the aim point.
    /// Dodge: fixed distance over a fixed time, invulnerable for its duration, charges refill one at a time.
    /// Expects the root pivot at the feet (set the CharacterController center to about half its height).
    /// External velocity (Triple Shot recoil, GDD §7): added on top of running, capped, decays, and the part blocked
    /// by a wall is dropped so it never builds up against walls (the player slides along them instead).
    /// Body radius scale (Density, GDD §7): the model widens and the CharacterController (= hurt capsule) radius grows
    /// together; height, speed, dodge and camera stay unchanged.
    /// </summary>
    [RequireComponent(typeof(CharacterController), typeof(Health))]
    [DisallowMultipleComponent]
    public sealed class PlayerController : MonoBehaviour
    {
        private const float MinDirectionSqr = 1e-4f;
        private const int MaxAimHits = 16;

        [Header("References")]
        [Tooltip("Camera rig used for aiming and camera-relative movement. Without it: world-relative movement, no mouse aim.")]
        [SerializeField] private PixelCameraRig _cameraRig;
        [Tooltip("Input reader. If empty, taken from this GameObject. Without it the player only moves through code.")]
        [SerializeField] private PlayerInputReader _input;
        [Tooltip("Visual root scaled by the body radius scale (Density). If empty: child named \"Model\", else this transform.")]
        [SerializeField] private Transform _model;

        [Header("Movement")]
        [Tooltip("Run speed in m/s (GDD §4: 6).")]
        [SerializeField, Min(0f)] private float _moveSpeed = 6f;
        [Tooltip("Gravity in m/s².")]
        [SerializeField, Min(0f)] private float _gravity = 25f;
        [Tooltip("Downward speed kept while grounded so the controller stays on slopes and steps.")]
        [SerializeField, Min(0f)] private float _groundedStickSpeed = 2f;
        [Tooltip("Seconds to reach full run speed from a stop (0 = instant).")]
        [SerializeField, Min(0f)] private float _accelerationTime = 0.06f;
        [Tooltip("Seconds to stop from full run speed once the keys are released (0 = instant).")]
        [SerializeField, Min(0f)] private float _decelerationTime = 0.05f;

        [Header("Aim")]
        [Tooltip("Height above the feet of the plane the cursor ray is crossed with when no target is under the cursor (about an enemy's body centre, so a click on an enemy's body lands on it).")]
        [SerializeField] private float _aimPlaneHeight = 0.7f;
        // TODO(design): the audit skeptic called picking the enemy under the cursor borderline aim help; the owner should
        // confirm it. Turning this off leaves the plain aim plane (still fixes the misses together with the converging shots).
        [Tooltip("If the cursor ray first touches a damageable (an enemy), aim at its centre: the shot goes where the cursor is.")]
        [SerializeField] private bool _aimAtTargetUnderCursor = true;
        [Tooltip("Layers the cursor ray checks for a target under the cursor.")]
        [SerializeField] private LayerMask _aimPickMask = ~0;
        [Tooltip("Longest cursor ray, metres.")]
        [SerializeField, Min(1f)] private float _aimPickDistance = 500f;
        [Tooltip("How fast the body turns toward the aim (1/s; higher is snappier, 0 = instant).")]
        [SerializeField, Min(0f)] private float _turnSharpness = 30f;

        [Header("Dodge")]
        [Tooltip("Dodge distance in metres (GDD §4: ~3).")]
        [SerializeField, Min(0f)] private float _dodgeDistance = 3f;
        [Tooltip("Dodge duration in seconds (GDD §4: 0.18). Invulnerable for this long.")]
        [SerializeField, Min(0.01f)] private float _dodgeDuration = 0.18f;
        [Tooltip("Refill time per dodge charge in seconds (GDD §4: 1.6). Charges refill one at a time.")]
        [SerializeField, Min(0.01f)] private float _dodgeCooldown = 1.6f;
        [Tooltip("Maximum dodge charges.")]
        [SerializeField, Min(1)] private int _dodgeCharges = 1;

        [Header("External velocity (Triple Shot recoil, GDD §7)")]
        [Tooltip("Largest external speed added to movement, m/s (GDD §7: at most 2.5).")]
        [SerializeField, Min(0f)] private float _maxExternalSpeed = 2.5f;
        [Tooltip("External speed lost per second, m/s².")]
        [SerializeField, Min(0f)] private float _externalDecay = 8f;

        private readonly RaycastHit[] _aimHits = new RaycastHit[MaxAimHits];
        private CharacterController _characterController;
        private Health _health;

        private float _verticalVelocity;
        private Vector3 _moveDirection;
        private Vector3 _aimPoint;
        private Vector3 _aimDirection = Vector3.forward;
        private bool _hasCursorAim;
        private Vector3 _runVelocity;

        private Vector3 _dodgeVelocity;
        private float _dodgeTimeRemaining;
        private int _dodgeChargesAvailable;
        private float _dodgeRefillTimer;

        private float _moveSpeedMultiplier = 1f;
        private float _dodgeCooldownMultiplier = 1f;

        private Vector3 _externalVelocity;
        private float _bodyRadiusScale = 1f;
        private float _baseControllerRadius;
        private Vector3 _baseModelScale = Vector3.one;

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

        /// <summary>World aim point: the centre of the target under the cursor, else the cursor on the aim plane.</summary>
        public Vector3 AimPoint => _aimPoint;
        /// <summary>Flat (y = 0), normalised aim direction from the body.</summary>
        public Vector3 AimDirection => _aimDirection;
        /// <summary>True once the aim point has come from the mouse cursor (false for bots and tests without a mouse).</summary>
        public bool HasCursorAim => _hasCursorAim;
        /// <summary>Current flat run velocity from input (m/s), without dodge or external push.</summary>
        public Vector3 RunVelocity => _runVelocity;
        /// <summary>Flat world move direction from input this frame (length 0..1).</summary>
        public Vector3 MoveDirection => _moveDirection;

        public bool IsDodging => _dodgeTimeRemaining > 0f;

        /// <summary>Raised when a dodge starts (audio cue).</summary>
        public event System.Action DodgeStarted;
        /// <summary>Raised each time a dodge charge finishes refilling (K-8 "kaçınma hazır" cue).</summary>
        public event System.Action DodgeReady;
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

        /// <summary>Current external velocity (flat, m/s) added to running.</summary>
        public Vector3 ExternalVelocity => _externalVelocity;
        public float MaxExternalSpeed => _maxExternalSpeed;

        /// <summary>The visual root that the body radius scale widens.</summary>
        public Transform Model => _model;

        /// <summary>
        /// Body radius scale (Density ×1.5, GDD §7): widens the model (x/z) and multiplies the CharacterController
        /// radius, which is also the hurt capsule. 1 = base. Min 0.1.
        /// </summary>
        public float BodyRadiusScale
        {
            get => _bodyRadiusScale;
            set
            {
                _bodyRadiusScale = Mathf.Max(0.1f, value);
                ApplyBodyScale();
            }
        }

        private void Awake()
        {
            _characterController = GetComponent<CharacterController>();
            _health = GetComponent<Health>();
            if (_input == null) _input = GetComponent<PlayerInputReader>();
            if (_model == null)
            {
                Transform child = transform.Find("Model");
                _model = child != null ? child : transform;
            }
            _baseControllerRadius = _characterController.radius;
            _baseModelScale = _model.localScale;

            _dodgeChargesAvailable = _dodgeCharges;
            _aimDirection = Flatten(transform.forward, Vector3.forward);
            _aimPoint = transform.position + Vector3.up * _aimPlaneHeight + _aimDirection;
        }

        /// <summary>Sets the max dodge charges (min 1). Available charges are clamped; refill continues.</summary>
        public void SetDodgeCharges(int count)
        {
            _dodgeCharges = Mathf.Max(1, count);
            _dodgeChargesAvailable = Mathf.Min(_dodgeChargesAvailable, _dodgeCharges);
            if (_dodgeChargesAvailable >= _dodgeCharges) _dodgeRefillTimer = 0f;
        }

        /// <summary>
        /// Adds a flat push (m/s) to the external velocity, capped at <see cref="MaxExternalSpeed"/>. It decays over
        /// time and is not applied during a dodge.
        /// </summary>
        public void AddExternalVelocity(Vector3 velocity)
        {
            velocity.y = 0f;
            _externalVelocity = Vector3.ClampMagnitude(_externalVelocity + velocity, _maxExternalSpeed);
        }

        /// <summary>Stops any remaining external push (e.g. Triple Shot removed).</summary>
        public void ClearExternalVelocity() => _externalVelocity = Vector3.zero;

        private void ApplyBodyScale()
        {
            if (_characterController == null || _model == null) return;
            float s = _bodyRadiusScale;
            _model.localScale = new Vector3(_baseModelScale.x * s, _baseModelScale.y, _baseModelScale.z * s);
            // A scaled root already scales the controller; only a separate model needs the radius change.
            _characterController.radius = _model == transform ? _baseControllerRadius : _baseControllerRadius * s;
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
            DodgeStarted?.Invoke();
            return true;
        }

        private void Update()
        {
            if (_health.IsDead) return; // the Devil's contract (Phase 5) decides what happens next
            float dt = Time.deltaTime;
            TickDodgeRefill(dt);
            UpdateAim();
            UpdateFacing(dt);

            _moveDirection = ReadMoveDirection();

            // Presses during a dodge are ignored, not buffered (GDD §4).
            if (_input != null && !IsDodging && _input.DodgePressed)
                TryDodge(_moveDirection);

            Vector3 targetRun = _moveDirection * (_moveSpeed * _moveSpeedMultiplier);
            Vector3 horizontal;
            if (IsDodging)
            {
                // Clamp the last dodge frame so the total distance stays exact.
                float dodgeDt = Mathf.Min(dt, _dodgeTimeRemaining);
                horizontal = dt > 0f ? _dodgeVelocity * (dodgeDt / dt) : Vector3.zero;
                _dodgeTimeRemaining -= dt;
                _runVelocity = targetRun; // running goes on at full speed after the dodge, no ramp-up stall
            }
            else
            {
                TickRunVelocity(targetRun, dt);
                horizontal = _runVelocity + _externalVelocity;
            }

            if (_characterController.isGrounded && _verticalVelocity <= 0f)
                _verticalVelocity = -_groundedStickSpeed;
            else
                _verticalVelocity -= _gravity * dt;

            Vector3 velocity = horizontal;
            velocity.y = _verticalVelocity;
            Vector3 before = transform.position;
            CollisionFlags flags = CollisionFlags.None;
            if (_characterController.enabled) flags = _characterController.Move(velocity * dt);
            TickExternalVelocity(dt, flags, transform.position - before);
        }

        /// <summary>
        /// Ramps the run velocity toward the input target: <see cref="_accelerationTime"/> to full speed while a key is
        /// held (also when turning), <see cref="_decelerationTime"/> to a stop once released.
        /// </summary>
        private void TickRunVelocity(Vector3 target, float dt)
        {
            bool hasInput = target.sqrMagnitude > MinDirectionSqr;
            float rampTime = hasInput ? _accelerationTime : _decelerationTime;
            if (rampTime <= 0f)
            {
                _runVelocity = target;
                return;
            }
            float referenceSpeed = Mathf.Max(1f, _moveSpeed * _moveSpeedMultiplier);
            _runVelocity = Vector3.MoveTowards(_runVelocity, target, referenceSpeed / rampTime * dt);
        }

        /// <summary>
        /// Drops the blocked part of the external push after a wall contact (keeps only what actually moved the
        /// player, so it slides along the wall and never accumulates), then decays it linearly.
        /// </summary>
        private void TickExternalVelocity(float dt, CollisionFlags flags, Vector3 moved)
        {
            if (_externalVelocity == Vector3.zero || dt <= 0f) return;

            if ((flags & CollisionFlags.Sides) != 0 && !IsDodging)
            {
                moved.y = 0f;
                if (moved.sqrMagnitude < 1e-8f)
                {
                    _externalVelocity = Vector3.zero;
                    return;
                }
                Vector3 movedDirection = moved.normalized;
                float along = Vector3.Dot(_externalVelocity, movedDirection);
                _externalVelocity = along > 0f ? movedDirection * along : Vector3.zero;
            }

            float speed = _externalVelocity.magnitude - _externalDecay * dt;
            _externalVelocity = speed > 0f ? _externalVelocity.normalized * speed : Vector3.zero;
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
                DodgeReady?.Invoke();
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
            if (!TryPickTarget(ray, out Vector3 point))
            {
                var plane = new Plane(Vector3.up, new Vector3(0f, position.y + _aimPlaneHeight, 0f));
                if (!plane.Raycast(ray, out float enter)) return;
                point = ray.GetPoint(enter);
            }

            _aimPoint = point;
            _hasCursorAim = true;
            Vector3 toAim = _aimPoint - position;
            toAim.y = 0f;
            if (toAim.sqrMagnitude < MinDirectionSqr) return;

            _aimDirection = toAim.normalized;
        }

        /// <summary>
        /// The orthographic cursor ray covers every height along one line, so a click on an enemy's body would otherwise
        /// land on the aim plane in front of or behind it. If the nearest thing the ray touches (not the player) is a
        /// damageable, its collider centre is the aim point.
        /// </summary>
        private bool TryPickTarget(Ray ray, out Vector3 point)
        {
            point = default;
            if (!_aimAtTargetUnderCursor) return false;

            int count = Physics.RaycastNonAlloc(ray, _aimHits, _aimPickDistance, _aimPickMask,
                QueryTriggerInteraction.Ignore);
            Collider nearest = null;
            float nearestDistance = float.PositiveInfinity;
            for (int i = 0; i < count; i++)
            {
                Collider collider = _aimHits[i].collider;
                if (collider == null || collider.transform.IsChildOf(transform)) continue;
                if (_aimHits[i].distance >= nearestDistance) continue;
                nearestDistance = _aimHits[i].distance;
                nearest = collider;
            }
            if (nearest == null || nearest.GetComponentInParent<IDamageable>() == null) return false;

            point = nearest.bounds.center;
            return true;
        }

        /// <summary>Turns the body (yaw only) toward the aim: fast exponential approach, frozen while time is stopped.</summary>
        private void UpdateFacing(float dt)
        {
            if (!_hasCursorAim) return; // bots and tests set the facing themselves
            Quaternion look = Quaternion.LookRotation(_aimDirection, Vector3.up);
            transform.rotation = _turnSharpness <= 0f
                ? look
                : Quaternion.Slerp(transform.rotation, look, 1f - Mathf.Exp(-_turnSharpness * dt));
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
