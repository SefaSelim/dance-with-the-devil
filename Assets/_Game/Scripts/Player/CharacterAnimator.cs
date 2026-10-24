using UnityEngine;

namespace DanceWithTheDevil
{
    /// <summary>
    /// Procedural animation for the cube characters (GDD §13: yordamsal animasyon — yürüme sallanması, nişan, geri
    /// tepme), shared by Defne and the enemies. Put it on the character root; the cube parts live under the child
    /// "Model".
    /// At start it finds the parts by name (Leg_L/R with Boot_L/R, Arm_L/R, Hips, and an enemy "Gun") and puts the legs
    /// and arms under pivots at the hips and shoulders. Every frame it measures the root's flat speed (so it works for
    /// the CharacterController, the NavMeshAgent and knockback alike) and drives: a step cycle (legs and arms swing
    /// along the direction of travel, a small bob; legless bodies waddle and twist their hips), a spring lean toward
    /// the movement, a recoil kick of the gun arm (<see cref="Kick"/>) and a hit flinch (<see cref="Flinch"/>).
    /// With <see cref="_hasGun"/> it builds a small cube rifle in the right hand, holds that arm in the aim pose (the
    /// root already faces the aim) and exposes the gun tip as <see cref="Muzzle"/>.
    /// Coexists with the other model writers: it never touches the Model's scale (Density widening, Runner crouch,
    /// ModelPunch squash), it writes the Model's local position and rotation in LateUpdate before ModelPunch (order 900)
    /// layers its knock on top, it rotates only the pivots it created (the Chaser tell still rotates the arm cubes
    /// inside them), and it builds in Start, after every Awake has cached its parts. Runs on scaled time, so hitstop
    /// and pauses freeze the pose.
    /// Humanoid mode: when the Model carries an <see cref="Animator"/> with a humanoid avatar (Defne's imported model),
    /// no pivots or cube gun are built. The Animator plays the clips (locomotion blend tree on MoveX/MoveZ, a "Hit"
    /// trigger) and this component feeds it the measured velocity in the body's frame, keeps the spring lean, pushes
    /// the held weapon back on <see cref="Kick"/>, and swaps the rifle in the hand for the rocket launcher for a moment
    /// after a launch (<see cref="ShowLauncher"/>). The weapons are children of a transform named "WeaponSocket"
    /// under the right hand: "Rifle" and "Launcher" (each with a "Muzzle" child), plus an optional "LauncherBack".
    /// Where the socket sits in the hands is <see cref="WeaponHold"/>'s job (tunable profiles + left-hand IK).
    /// </summary>
    [DefaultExecutionOrder(800)] // LateUpdate before ModelPunch (900) and the camera (1000)
    [DisallowMultipleComponent]
    public sealed class CharacterAnimator : MonoBehaviour
    {
        private const string ModelName = "Model";
        private const string GunPartName = "Gun";
        private const string TipMaterialPartName = "Detail_Bib";
        private const float TeleportDistance = 1.5f;   // a jump larger than this in one frame is a spawn, not a step
        private const float MaxLeanFactor = 1.8f;       // fast motion leans up to this many times the full lean
        private const float DodgeLeanBoost = 1.4f;      // the player's dodge may lean this much further still
        private const float ImpulseToPeak = 1.6f;       // spring impulse scale so an impulse peaks near its degrees
        private const float SwingFollowRate = 8f;       // swing size change per second (0..1)
        private const float PhaseWrap = Mathf.PI * 128f;

        [Header("Rig")]
        [Tooltip("Root of the cube parts. Empty = the child named \"Model\".")]
        [SerializeField] private Transform _model;
        [Tooltip("Player rig: reads the PlayerController so a dodge leans harder into the dash.")]
        [SerializeField] private bool _isPlayer;

        [Header("Gun")]
        [Tooltip("Build a small cube rifle in the right hand at start and hold that arm in the aim pose. Exposes Muzzle.")]
        [SerializeField] private bool _hasGun;
        [Tooltip("Material of the gun body (dark metal).")]
        [SerializeField] private Material _gunMaterial;
        [Tooltip("Material of the gun tip. Empty = the material of the model part \"Detail_Bib\" (turquoise), else the gun material.")]
        [SerializeField] private Material _gunTipMaterial;
        [Tooltip("Gun length in metres (behind plus in front of the hand).")]
        [SerializeField, Min(0.05f)] private float _gunLength = 0.5f;
        [Tooltip("Part of the gun in front of the hand, metres.")]
        [SerializeField, Min(0f)] private float _gunReach = 0.36f;
        [Tooltip("Aim pose: the right arm raised forward from hanging, degrees (90 = level).")]
        [SerializeField, Range(0f, 120f)] private float _aimArmPitch = 76f;
        [Tooltip("Aim pose: the right arm turned in toward the body's centre line, degrees.")]
        [SerializeField, Range(0f, 45f)] private float _aimArmInward = 8f;

        [Header("Walk")]
        [Tooltip("Vertical bob at full swing, metres (one texel is about 0.05 m).")]
        [SerializeField, Min(0f)] private float _bobHeight = 0.05f;
        [Tooltip("Metres travelled per step (sets the step rate from the speed).")]
        [SerializeField, Min(0.05f)] private float _strideLength = 1f;
        [Tooltip("Speed (m/s) at which the swing and bob reach full size.")]
        [SerializeField, Min(0.1f)] private float _fullSwingSpeed = 3f;
        [Tooltip("Speed (m/s) above which the step rate stops rising (dodges, knockback).")]
        [SerializeField, Min(0.1f)] private float _maxCadenceSpeed = 8f;
        [Tooltip("Leg swing at full speed, degrees each way.")]
        [SerializeField, Range(0f, 80f)] private float _legSwingDegrees = 32f;
        [Tooltip("Arm swing at full speed, degrees each way (opposite to the legs).")]
        [SerializeField, Range(0f, 80f)] private float _armSwingDegrees = 26f;
        [Tooltip("Side-to-side roll per step for bodies without legs (Chaser, Shooter), degrees.")]
        [SerializeField, Range(0f, 30f)] private float _waddleDegrees = 6f;
        [Tooltip("Hip twist per step of a \"Hips\" part on a body without legs (Chaser), degrees.")]
        [SerializeField, Range(0f, 45f)] private float _hipTwistDegrees = 12f;
        [Tooltip("How fast the measured speed follows the real one (1/s).")]
        [SerializeField, Min(0.1f)] private float _velocitySharpness = 18f;

        [Header("Lean")]
        [Tooltip("Lean toward the movement at the full-lean speed, degrees.")]
        [SerializeField, Range(0f, 30f)] private float _leanDegrees = 9f;
        [Tooltip("Speed (m/s) giving the full lean; faster motion (a dodge) leans up to 1.8 times as far.")]
        [SerializeField, Min(0.1f)] private float _fullLeanSpeed = 6f;
        [Tooltip("Lean spring frequency (Hz).")]
        [SerializeField, Min(0.1f)] private float _leanFrequency = 4.5f;
        [Tooltip("Lean spring damping (below 1 overshoots a little when stopping).")]
        [SerializeField, Range(0.05f, 2f)] private float _leanDamping = 0.5f;

        [Header("Recoil and hits")]
        [Tooltip("Gun arm pulled back at Kick(1), metres.")]
        [SerializeField, Min(0f)] private float _kickDistance = 0.12f;
        [Tooltip("Gun arm muzzle-up at Kick(1), degrees.")]
        [SerializeField, Range(0f, 45f)] private float _kickDegrees = 14f;
        [Tooltip("Body rock back at Kick(1), degrees.")]
        [SerializeField, Range(0f, 30f)] private float _kickBodyDegrees = 4f;
        [Tooltip("Seconds for the gun arm to recover from a kick.")]
        [SerializeField, Min(0.01f)] private float _kickRecoverTime = 0.08f;
        [Tooltip("Body tilt of a flinch, degrees.")]
        [SerializeField, Range(0f, 45f)] private float _flinchDegrees = 14f;

        [Header("Humanoid model (imported, animated)")]
        [Tooltip("Run speed in m/s that maps to a full locomotion clip (MoveX/MoveZ = ±1).")]
        [SerializeField, Min(0.1f)] private float _animRunSpeed = 6f;
        [Tooltip("Damping time of the locomotion parameters, seconds.")]
        [SerializeField, Min(0f)] private float _animDamping = 0.08f;
        [Tooltip("Held weapon pulled back along its barrel at Kick(1), metres.")]
        [SerializeField, Min(0f)] private float _weaponKickDistance = 0.08f;
        [Tooltip("Held weapon muzzle-up at Kick(1), degrees.")]
        [SerializeField, Range(0f, 45f)] private float _weaponKickDegrees = 10f;
        [Tooltip("Seconds the rocket launcher stays in the hands after a launch.")]
        [SerializeField, Min(0f)] private float _launcherShowTime = 0.55f;

        private static readonly int MoveXId = Animator.StringToHash("MoveX");
        private static readonly int MoveZId = Animator.StringToHash("MoveZ");
        private static readonly int SpeedId = Animator.StringToHash("Speed");
        private static readonly int HitId = Animator.StringToHash("Hit");

        private Animator _body;
        private bool _humanoid;
        private Transform _rifle;
        private Transform _launcher;
        private Transform _launcherBack;
        private Transform _launcherMuzzle;
        private Transform _launcherWarhead;
        private Transform _heldWeapon;
        private Vector3 _heldBasePosition;
        private Quaternion _heldBaseRotation = Quaternion.identity;
        private float _launcherHideTime = float.NegativeInfinity;

        private PlayerController _player;
        private SecondOrderDynamics3 _lean;
        private bool _built;

        private Transform _hipL;
        private Transform _hipR;
        private Transform _shoulderL;
        private Transform _shoulderR;
        private Transform _hips;
        private Transform _muzzle;
        private Vector3 _shoulderRBasePosition;
        private Quaternion _hipsBaseRotation = Quaternion.identity;
        private Vector3 _modelBasePosition;
        private Quaternion _modelBaseRotation = Quaternion.identity;
        private bool _rightArmHolds;
        private bool _hasLegs;

        private Vector3 _lastPosition;
        private Vector3 _velocity;
        private Vector3 _swingAxis = Vector3.right;
        private float _phase;
        private float _swingAmount;
        private float _kickStrength;
        private float _kickTime;

        /// <summary>The gun tip (bullets and rockets start here, see Rifle/RocketLauncher). Null when there is no gun.</summary>
        public Transform Muzzle
        {
            get
            {
                EnsureBuilt();
                return _muzzle;
            }
        }

        /// <summary>The rocket launcher's tip in humanoid mode (rockets start here); null otherwise.</summary>
        public Transform LauncherMuzzle
        {
            get
            {
                EnsureBuilt();
                return _launcherMuzzle;
            }
        }

        /// <summary>Root of the animated cube parts, or of the imported model in humanoid mode.</summary>
        public Transform Model => _model;

        /// <summary>True if this character builds and holds a gun.</summary>
        public bool HasGun => _hasGun;

        /// <summary>True when the model is an imported humanoid driven by an Animator.</summary>
        public bool IsHumanoid
        {
            get
            {
                EnsureBuilt();
                return _humanoid;
            }
        }

        /// <summary>
        /// Humanoid mode: puts the rocket launcher in the hands (rifle hidden, back copy hidden) for a moment, so the
        /// launch reads. Call before reading <see cref="LauncherMuzzle"/>. No effect on cube characters.
        /// </summary>
        public void ShowLauncher()
        {
            EnsureBuilt();
            if (!_humanoid || _launcher == null) return;
            _launcherHideTime = Time.time + _launcherShowTime;
            SetHeld(_launcher);
            // The fired rocket leaves the tube: the held launcher shows empty until it goes back.
            if (_launcherWarhead != null) _launcherWarhead.gameObject.SetActive(false);
        }

        private float Omega => 2f * Mathf.PI * _leanFrequency;
        private float CurrentKick => _kickStrength * _kickTime * _kickTime;

        private void Awake()
        {
            if (_model == null) _model = FindDirectChild(transform, ModelName);
            if (_isPlayer) _player = GetComponent<PlayerController>();
            _lean = new SecondOrderDynamics3(_leanFrequency, _leanDamping, 0f, Vector3.zero);
        }

        private void OnValidate()
        {
            if (_lean != null) _lean.SetParameters(_leanFrequency, _leanDamping, 0f);
        }

        private void OnEnable()
        {
            // Pooled enemies come back elsewhere: start from rest.
            _lastPosition = transform.position;
            _velocity = Vector3.zero;
            _swingAmount = 0f;
            _kickTime = 0f;
            if (_lean != null) _lean.Reset(Vector3.zero);
        }

        private void Start() => EnsureBuilt();

        /// <summary>
        /// Recoil: the gun arm snaps back and muzzle-up, the body rocks back a little. Strength 0..2 (rifle about 0.5,
        /// rocket about 1.4). A new kick never weakens one still showing.
        /// </summary>
        public void Kick(float strength)
        {
            strength = Mathf.Clamp(strength, 0f, 2f);
            if (strength <= 0f) return;
            _kickStrength = Mathf.Max(strength, CurrentKick);
            _kickTime = 1f;
            if (_lean != null) _lean.AddVelocity(new Vector3(-strength * _kickBodyDegrees * Omega * ImpulseToPeak, 0f, 0f));
        }

        private void SetHeld(Transform weapon)
        {
            if (_heldWeapon == weapon) return;
            if (_heldWeapon != null)
            {
                _heldWeapon.localPosition = _heldBasePosition;
                _heldWeapon.localRotation = _heldBaseRotation;
            }
            if (_rifle != null) _rifle.gameObject.SetActive(weapon == _rifle);
            if (_launcher != null) _launcher.gameObject.SetActive(weapon == _launcher);
            if (_launcherBack != null) _launcherBack.gameObject.SetActive(weapon != _launcher);
            _heldWeapon = weapon;
            if (weapon != null)
            {
                _heldBasePosition = weapon.localPosition;
                _heldBaseRotation = weapon.localRotation;
            }
        }

        /// <summary>
        /// Hit reaction: the top of the body is pushed away from <paramref name="fromDirection"/>, the world direction
        /// the hit came from (toward the attacker, i.e. -DamageInfo.Direction), then springs back. Zero = rock straight
        /// back. Enemy and HurtFeedback call this on damage; this component does not subscribe itself.
        /// </summary>
        public void Flinch(Vector3 fromDirection)
        {
            if (_lean == null) return;
            Vector3 push = transform.InverseTransformDirection(-fromDirection);
            push.y = 0f;
            push = push.sqrMagnitude > 1e-6f ? push.normalized : Vector3.back;
            // Pitch +x tilts the top toward local +z; roll -z tilts it toward local +x.
            _lean.AddVelocity(new Vector3(push.z, -push.x, 0f) * (_flinchDegrees * Omega * ImpulseToPeak));
            if (_humanoid && _body != null && _body.isActiveAndEnabled) _body.SetTrigger(HitId);
        }

        private void LateUpdate()
        {
            if (!_built) EnsureBuilt();
            if (_model == null) return;
            float dt = Time.deltaTime;
            if (dt <= 0f) return; // hitstop or pause: hold the pose
            if (_humanoid)
            {
                UpdateHumanoid(dt);
                return;
            }

            // Measured flat velocity, smoothed; a teleport (spawn) counts as standing still.
            Vector3 position = transform.position;
            Vector3 delta = position - _lastPosition;
            _lastPosition = position;
            delta.y = 0f;
            Vector3 raw = delta.sqrMagnitude > TeleportDistance * TeleportDistance ? Vector3.zero : delta / dt;
            _velocity = Vector3.Lerp(_velocity, raw, 1f - Mathf.Exp(-_velocitySharpness * dt));

            Vector3 local = transform.InverseTransformDirection(_velocity);
            local.y = 0f;
            float speed = local.magnitude;

            // Step cycle: one step (half a sine period) per stride length travelled.
            _swingAmount = Mathf.MoveTowards(_swingAmount, Mathf.Clamp01(speed / _fullSwingSpeed), SwingFollowRate * dt);
            _phase += Mathf.Min(speed, _maxCadenceSpeed) / _strideLength * Mathf.PI * dt;
            if (_phase > PhaseWrap) _phase -= PhaseWrap;
            if (speed > 0.2f) _swingAxis = Vector3.Cross(Vector3.up, local / speed); // limbs swing along the travel
            float s = Mathf.Sin(_phase);
            float leg = s * _legSwingDegrees * _swingAmount;
            float arm = s * _armSwingDegrees * _swingAmount;

            // AngleAxis(-a, axis) moves a hanging limb's lower end toward the travel direction.
            if (_hipL != null) _hipL.localRotation = Quaternion.AngleAxis(-leg, _swingAxis);
            if (_hipR != null) _hipR.localRotation = Quaternion.AngleAxis(leg, _swingAxis);
            if (_shoulderL != null) _shoulderL.localRotation = Quaternion.AngleAxis(arm, _swingAxis);
            if (_hips != null)
                _hips.localRotation = _hipsBaseRotation * Quaternion.Euler(0f, s * _hipTwistDegrees * _swingAmount, 0f);

            if (_shoulderR != null)
            {
                if (_rightArmHolds)
                {
                    float kick = CurrentKick;
                    float pitch = (_hasGun ? _aimArmPitch : 0f) + kick * _kickDegrees;
                    float inward = _hasGun ? _aimArmInward : 0f;
                    _shoulderR.localRotation = Quaternion.Euler(-pitch, -inward, 0f);
                    _shoulderR.localPosition = _shoulderRBasePosition + Vector3.back * (kick * _kickDistance);
                }
                else
                {
                    _shoulderR.localRotation = Quaternion.AngleAxis(-arm, _swingAxis);
                }
            }
            _kickTime = Mathf.MoveTowards(_kickTime, 0f, dt / _kickRecoverTime); // after use: a kick shows at full on its frame

            // Spring lean toward the movement (kicks and flinches add impulses to the same spring).
            float maxLean = _leanDegrees * MaxLeanFactor;
            if (_player != null && _player.IsDodging) maxLean *= DodgeLeanBoost; // a dodge reads as a dash
            float leanPerSpeed = _leanDegrees / _fullLeanSpeed;
            float leanForward = Mathf.Clamp(local.z * leanPerSpeed, -maxLean, maxLean);
            float leanRight = Mathf.Clamp(local.x * leanPerSpeed, -maxLean, maxLean);
            Vector3 lean = _lean.Update(dt, new Vector3(leanForward, -leanRight, 0f));

            float waddle = _hasLegs ? 0f : s * _waddleDegrees * _swingAmount;
            float bob = (1f - Mathf.Abs(s)) * _bobHeight * _swingAmount;
            _model.localRotation = _modelBaseRotation * Quaternion.Euler(lean.x, 0f, lean.y + waddle);
            _model.localPosition = _modelBasePosition + new Vector3(0f, bob, 0f);
        }

        /// <summary>
        /// Humanoid mode per frame: locomotion parameters in the body's frame (the root faces the aim, so walking
        /// backwards while shooting plays the backward clip), the spring lean on the model, weapon recoil and the
        /// launcher-back-to-rifle swap.
        /// </summary>
        private void UpdateHumanoid(float dt)
        {

            Vector3 position = transform.position;
            Vector3 delta = position - _lastPosition;
            _lastPosition = position;
            delta.y = 0f;
            Vector3 raw = delta.sqrMagnitude > TeleportDistance * TeleportDistance ? Vector3.zero : delta / dt;
            _velocity = Vector3.Lerp(_velocity, raw, 1f - Mathf.Exp(-_velocitySharpness * dt));

            Vector3 local = transform.InverseTransformDirection(_velocity);
            local.y = 0f;
            if (_body != null && _body.isActiveAndEnabled && _body.runtimeAnimatorController != null)
            {
                Vector3 move = local / _animRunSpeed;
                if (move.sqrMagnitude > 1f) move.Normalize();
                _body.SetFloat(MoveXId, move.x, _animDamping, dt);
                _body.SetFloat(MoveZId, move.z, _animDamping, dt);
                _body.SetFloat(SpeedId, move.magnitude, _animDamping, dt);
            }

            float maxLean = _leanDegrees * MaxLeanFactor;
            if (_player != null && _player.IsDodging) maxLean *= DodgeLeanBoost;
            float leanPerSpeed = _leanDegrees / _fullLeanSpeed;
            float leanForward = Mathf.Clamp(local.z * leanPerSpeed, -maxLean, maxLean);
            float leanRight = Mathf.Clamp(local.x * leanPerSpeed, -maxLean, maxLean);
            Vector3 lean = _lean.Update(dt, new Vector3(leanForward, -leanRight, 0f));
            _model.localRotation = _modelBaseRotation * Quaternion.Euler(lean.x, 0f, lean.y);
            _model.localPosition = _modelBasePosition;

            if (_launcher != null && _heldWeapon == _launcher && Time.time >= _launcherHideTime)
            {
                SetHeld(_rifle);
                if (_launcherWarhead != null) _launcherWarhead.gameObject.SetActive(true); // reloaded off-screen
            }

            if (_heldWeapon != null)
            {
                float kick = CurrentKick;
                // The weapon roots are built with +Z along the barrel: back along -Z, muzzle up around -X.
                _heldWeapon.localPosition = _heldBasePosition + _heldBaseRotation * Vector3.back * (kick * _weaponKickDistance);
                _heldWeapon.localRotation = _heldBaseRotation * Quaternion.Euler(-kick * _weaponKickDegrees, 0f, 0f);
            }
            _kickTime = Mathf.MoveTowards(_kickTime, 0f, dt / _kickRecoverTime);
        }

        /// <summary>Humanoid mode setup: finds the weapons under "WeaponSocket" and holds the rifle.</summary>
        private void BuildHumanoid()
        {
            _humanoid = true;
            Transform socket = FindDeep(_model, "WeaponSocket");
            _rifle = FindDeep(socket, "Rifle");
            _launcher = FindDeep(socket, "Launcher");
            _launcherBack = FindDeep(_model, "LauncherBack");
            _muzzle = FindDeep(_rifle, "Muzzle");
            _launcherMuzzle = FindDeep(_launcher, "Muzzle");
            _launcherWarhead = FindDeep(_launcher, "Rocket");
            // Enemies' humanoid bodies carry no weapons (their shots come from the brain).
            if (_rifle == null && _isPlayer) Debug.LogWarning($"{nameof(CharacterAnimator)}: humanoid model has no WeaponSocket/Rifle.", this);
            SetHeld(_rifle);
        }

        private static Transform FindDeep(Transform parent, string childName)
        {
            if (parent == null) return null;
            for (int i = 0; i < parent.childCount; i++)
            {
                Transform child = parent.GetChild(i);
                if (child.name == childName) return child;
                Transform found = FindDeep(child, childName);
                if (found != null) return found;
            }
            return null;
        }

        /// <summary>Creates the pivots and the gun once (Start, or the first Muzzle request after it).</summary>
        private void EnsureBuilt()
        {
            if (_built) return;
            if (_model == null) _model = FindDirectChild(transform, ModelName); // safe when asked before Awake
            _built = true;
            if (_model == null) return;

            _modelBasePosition = _model.localPosition;
            _modelBaseRotation = _model.localRotation;

            _body = _model.GetComponentInChildren<Animator>(true);
            if (_body != null && _body.avatar != null && _body.avatar.isHuman)
            {
                BuildHumanoid();
                return;
            }

            Transform legL = FindDirectChild(_model, "Leg_L");
            Transform legR = FindDirectChild(_model, "Leg_R");
            Transform armL = FindDirectChild(_model, "Arm_L");
            Transform armR = FindDirectChild(_model, "Arm_R");
            Transform gunPart = FindDirectChild(_model, GunPartName);

            if (legL != null)
            {
                _hipL = CreatePivot("Hip_L_Pivot", UpperEnd(legL));
                Attach(legL, _hipL);
                Attach(FindDirectChild(_model, "Boot_L"), _hipL);
            }
            if (legR != null)
            {
                _hipR = CreatePivot("Hip_R_Pivot", UpperEnd(legR));
                Attach(legR, _hipR);
                Attach(FindDirectChild(_model, "Boot_R"), _hipR);
            }
            if (armL != null)
            {
                _shoulderL = CreatePivot("Shoulder_L_Pivot", UpperEnd(armL));
                Attach(armL, _shoulderL);
            }
            if (armR != null)
            {
                _shoulderR = CreatePivot("Shoulder_R_Pivot", UpperEnd(armR));
                _shoulderRBasePosition = _shoulderR.localPosition;
                Attach(armR, _shoulderR);
                Attach(gunPart, _shoulderR); // the Shooter's gun stays in its hand
            }

            _hasLegs = _hipL != null || _hipR != null;
            if (!_hasLegs)
            {
                _hips = FindDirectChild(_model, "Hips");
                if (_hips != null) _hipsBaseRotation = _hips.localRotation;
            }

            _rightArmHolds = _shoulderR != null && (_hasGun || gunPart != null);
            if (_hasGun && _shoulderR != null) BuildGun(armR);
        }

        /// <summary>
        /// Builds the rifle under the right shoulder pivot. In the pivot's space the hanging arm points down (-y); the
        /// aim pose turns that toward the facing, so the gun runs along -y from the hand.
        /// </summary>
        private void BuildGun(Transform arm)
        {
            const float side = -0.02f; // a hair toward the body's centre line
            const float lift = 0.04f;  // sits on top of the forearm in the aim pose
            float hand = arm.localScale.y * 0.9f;
            float back = Mathf.Max(0f, _gunLength - _gunReach);

            Material tip = _gunTipMaterial != null ? _gunTipMaterial : PartMaterial(TipMaterialPartName);
            if (tip == null) tip = _gunMaterial;

            CreateCube("Gun_Body", _shoulderR, new Vector3(side, -(hand + (_gunReach - back) * 0.5f), lift),
                new Vector3(0.09f, _gunLength, 0.11f), _gunMaterial);
            CreateCube("Gun_Tip", _shoulderR, new Vector3(side, -(hand + _gunReach - 0.04f), lift),
                new Vector3(0.11f, 0.08f, 0.13f), tip);

            _muzzle = new GameObject("Muzzle").transform;
            _muzzle.SetParent(_shoulderR, false);
            _muzzle.localPosition = new Vector3(side, -(hand + _gunReach + 0.02f), lift);
        }

        private Transform CreatePivot(string pivotName, Vector3 localPoint)
        {
            Transform pivot = new GameObject(pivotName).transform;
            pivot.gameObject.layer = _model.gameObject.layer;
            pivot.SetParent(_model, false);
            pivot.localPosition = localPoint;
            pivot.localRotation = Quaternion.identity;
            pivot.localScale = Vector3.one;
            return pivot;
        }

        private static void Attach(Transform part, Transform pivot)
        {
            if (part != null) part.SetParent(pivot, true);
        }

        /// <summary>Joint end of a limb cube: its top for a hanging limb, its back end for one modelled pointing forward.</summary>
        private static Vector3 UpperEnd(Transform part)
        {
            Vector3 size = part.localScale;
            return size.z > size.y
                ? part.localPosition - part.localRotation * Vector3.forward * (size.z * 0.5f)
                : part.localPosition + part.localRotation * Vector3.up * (size.y * 0.5f);
        }

        private void CreateCube(string partName, Transform parent, Vector3 localPosition, Vector3 size, Material material)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = partName;
            go.layer = parent.gameObject.layer;
            var collider = go.GetComponent<Collider>();
            if (collider != null)
            {
                collider.enabled = false;
                Destroy(collider);
            }
            Transform t = go.transform;
            t.SetParent(parent, false);
            t.localPosition = localPosition;
            t.localRotation = Quaternion.identity;
            t.localScale = size;
            if (material != null) go.GetComponent<MeshRenderer>().sharedMaterial = material;
        }

        private Material PartMaterial(string partName)
        {
            Transform part = FindDirectChild(_model, partName);
            if (part == null) return null;
            var partRenderer = part.GetComponent<Renderer>();
            return partRenderer != null ? partRenderer.sharedMaterial : null;
        }

        private static Transform FindDirectChild(Transform parent, string childName)
        {
            if (parent == null) return null;
            for (int i = 0; i < parent.childCount; i++)
            {
                Transform child = parent.GetChild(i);
                if (child.name == childName) return child;
            }
            return null;
        }
    }
}
