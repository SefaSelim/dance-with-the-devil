using UnityEngine;

namespace DanceWithTheDevil
{
    /// <summary>
    /// Melee brain for the Chaser and the Runner (GDD §6), driven by the <see cref="Enemy"/> stats.
    /// Approach on the NavMesh (agent avoidance keeps them from fully stacking) → in attack range: stop and tell
    /// (Chaser: arms open ~70° and an orange floor wedge; Runner: crouch and a short floor line) → lunge: a short
    /// forward dash in the locked facing that damages the player once (Contact) if the player is inside the hit
    /// sphere in front → recovery (the Runner backs off) → approach again.
    /// </summary>
    [RequireComponent(typeof(Enemy))]
    [DisallowMultipleComponent]
    public sealed class ChaserBrain : MonoBehaviour
    {
        private const int MaxOverlaps = 16;

        private enum State
        {
            Idle,
            Approach,
            Tell,
            Lunge,
            Recover,
        }

        /// <summary>How the tell looks (GDD §6: Chaser opens its arms + wedge; Runner crouches + short line).</summary>
        public enum TellStyle
        {
            ArmsAndWedge,
            CrouchAndLine,
        }

        [Header("Tell")]
        [Tooltip("Chaser: ArmsAndWedge. Runner: CrouchAndLine.")]
        [SerializeField] private TellStyle _tellStyle = TellStyle.ArmsAndWedge;
        [Tooltip("Arm rotation around Z at the end of the tell, degrees (child cubes \"Arm_L\"/\"Arm_R\" under the model).")]
        [SerializeField, Range(0f, 120f)] private float _armOpenAngle = 70f;
        [Tooltip("Model height scale at the bottom of the crouch (CrouchAndLine).")]
        [SerializeField, Range(0.3f, 1f)] private float _crouchScale = 0.75f;
        [Tooltip("Floor marker width in metres (wedge base or line width).")]
        [SerializeField, Min(0.02f)] private float _markerWidth = 1.2f;
        [Tooltip("Floor marker length in metres. 0 = attack range + lunge hit radius.")]
        [SerializeField, Min(0f)] private float _markerLength;
        [Tooltip("Floor marker material (unlit). Empty = shared runtime URP Unlit.")]
        [SerializeField] private Material _telegraphMaterial;
        [Tooltip("Floor marker colour (GDD: #FF5A1F).")]
        [SerializeField] private Color _telegraphColor = new Color(1f, 0.353f, 0.122f);

        [Header("Movement")]
        [Tooltip("Seconds between path updates while approaching.")]
        [SerializeField, Min(0.05f)] private float _repathInterval = 0.2f;
        [Tooltip("Turn speed toward the player during tell and recovery, degrees per second.")]
        [SerializeField, Min(0f)] private float _turnSpeed = 540f;

        [Header("Lunge hit")]
        [Tooltip("Hit sphere centre height above the feet, metres.")]
        [SerializeField, Min(0f)] private float _hitHeight = 0.9f;
        [Tooltip("Hit sphere centre distance in front of the body, metres.")]
        [SerializeField, Min(0f)] private float _hitForwardOffset = 0.6f;
        [Tooltip("Layers the hit sphere checks (must include the player).")]
        [SerializeField] private LayerMask _hitMask = ~0;
        [Tooltip("ImpactBus Melee intensity when the lunge lands.")]
        [SerializeField, Range(0f, 1f)] private float _hitIntensity = 0.4f;

        private readonly Collider[] _overlaps = new Collider[MaxOverlaps];

        private Enemy _enemy;
        private State _state;
        private float _stateStart;
        private float _nextRepath;
        private Vector3 _lungeDirection;
        private float _lungeTravelled;
        private bool _lungeHit;

        private Transform _armL;
        private Transform _armR;
        private Quaternion _armLBase;
        private Quaternion _armRBase;
        private Vector3 _modelBaseScale = Vector3.one;
        private MeshRenderer _marker;

        public bool IsTelling => _state == State.Tell;
        public bool IsLunging => _state == State.Lunge;

        /// <summary>Raised when a tell starts (audio). Bool: Runner style (crouch and line).</summary>
        public static event System.Action<Enemy, bool> TellStarted;
        /// <summary>Raised when a tell ends without its lunge (death or reset).</summary>
        public static event System.Action<Enemy> TellCancelled;

        private void Awake()
        {
            _enemy = GetComponent<Enemy>();
            Transform model = _enemy.Model != null ? _enemy.Model : transform;
            _modelBaseScale = model.localScale;
            _armL = model.Find("Arm_L");
            _armR = model.Find("Arm_R");
            if (_armL != null) _armLBase = _armL.localRotation;
            if (_armR != null) _armRBase = _armR.localRotation;
            _marker = EnemyTelegraphs.CreateWedge(name + "_Tell", _telegraphMaterial, _telegraphColor);
        }

        private void OnDestroy() => EnemyTelegraphs.Destroy(_marker);

        private void OnEnable()
        {
            _enemy.Spawned += OnSpawned;
            _enemy.Died += OnDied;
            if (_enemy.IsAlive) Enter(State.Approach);
        }

        private void OnDisable()
        {
            _enemy.Spawned -= OnSpawned;
            _enemy.Died -= OnDied;
            ResetPose();
        }

        private void OnSpawned(Enemy enemy)
        {
            ResetPose();
            _nextRepath = Time.time + Random.Range(0f, _repathInterval);
            Enter(State.Approach);
        }

        private void OnDied(Enemy enemy)
        {
            if (_state == State.Tell) TellCancelled?.Invoke(_enemy);
            ResetPose();
            _state = State.Idle;
        }

        private void Update()
        {
            if (!_enemy.IsAlive || _state == State.Idle) return;
            if (_enemy.Target == null)
            {
                _enemy.StopMoving();
                return;
            }

            float now = Time.time;
            float elapsed = now - _stateStart;
            EnemyStats stats = _enemy.Stats;

            switch (_state)
            {
                case State.Approach:
                    TickApproach(now, stats);
                    break;

                case State.Tell:
                    _enemy.FaceDirection(_enemy.FlatToTarget(), _turnSpeed);
                    float t = stats.TellDuration > 0f ? Mathf.Clamp01(elapsed / stats.TellDuration) : 1f;
                    ApplyTellPose(t);
                    if (elapsed >= stats.TellDuration) Enter(State.Lunge);
                    break;

                case State.Lunge:
                    TickLunge(stats);
                    break;

                case State.Recover:
                    _enemy.FaceDirection(_enemy.FlatToTarget(), _turnSpeed);
                    if (elapsed >= stats.Recovery) Enter(State.Approach);
                    break;
            }
        }

        private void TickApproach(float now, in EnemyStats stats)
        {
            Vector3 toTarget = _enemy.FlatToTarget();
            if (toTarget.sqrMagnitude <= stats.AttackRange * stats.AttackRange)
            {
                Enter(State.Tell);
                return;
            }

            if (now >= _nextRepath)
            {
                _nextRepath = now + _repathInterval;
                _enemy.MoveTo(_enemy.Target.position);
            }
        }

        private void TickLunge(in EnemyStats stats)
        {
            float speed = stats.LungeDistance / Mathf.Max(0.01f, stats.LungeDuration);
            float step = Mathf.Min(speed * Time.deltaTime, Mathf.Max(0f, stats.LungeDistance - _lungeTravelled));
            if (step > 0f)
            {
                _enemy.MoveBy(_lungeDirection * step);
                _lungeTravelled += step;
            }

            if (!_lungeHit) TryHit(stats);

            if (_lungeTravelled >= stats.LungeDistance - 1e-4f || Time.time - _stateStart >= stats.LungeDuration)
                Enter(State.Recover);
        }

        private void TryHit(in EnemyStats stats)
        {
            Transform target = _enemy.Target;
            IDamageable damageable = _enemy.TargetDamageable;
            if (target == null || damageable == null) return;

            Vector3 center = transform.position + _lungeDirection * _hitForwardOffset + Vector3.up * _hitHeight;
            int count = Physics.OverlapSphereNonAlloc(center, stats.LungeHitRadius, _overlaps, _hitMask,
                QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                Collider collider = _overlaps[i];
                if (collider == null || !collider.transform.IsChildOf(target)) continue;

                Vector3 point = collider.ClosestPoint(center);
                bool applied = damageable.TakeDamage(new DamageInfo(stats.Damage, point, _lungeDirection, gameObject,
                    DamageKind.Contact));
                if (!applied) return; // invulnerable (dodge): a later frame of the same lunge may still land

                _lungeHit = true;
                ImpactBus.Raise(new ImpactEvent
                {
                    kind = ImpactKind.Melee,
                    position = point,
                    normal = -_lungeDirection,
                    direction = _lungeDirection,
                    intensity = _hitIntensity,
                    surface = SurfaceType.Flesh,
                    hitTransform = collider.transform,
                    instigator = gameObject,
                });
                return;
            }
        }

        private void Enter(State state)
        {
            if (_state == State.Tell && state != State.Lunge) TellCancelled?.Invoke(_enemy);
            _state = state;
            _stateStart = Time.time;
            EnemyStats stats = _enemy.Stats;
            var agent = _enemy.Agent;

            switch (state)
            {
                case State.Approach:
                    ResetPose();
                    _enemy.SpeedScale = 1f;
                    if (agent.enabled) agent.updateRotation = true;
                    if (_enemy.HasNavMesh) agent.stoppingDistance = stats.AttackRange * 0.75f;
                    _nextRepath = Time.time; // path right away
                    break;

                case State.Tell:
                    _enemy.StopMoving();
                    if (agent.enabled) agent.updateRotation = false;
                    ShowMarker();
                    ApplyTellPose(0f);
                    TellStarted?.Invoke(_enemy, _tellStyle == TellStyle.CrouchAndLine);
                    break;

                case State.Lunge:
                    _lungeDirection = transform.forward;
                    _lungeDirection.y = 0f;
                    _lungeDirection = _lungeDirection.sqrMagnitude > 1e-6f ? _lungeDirection.normalized : Vector3.forward;
                    _lungeTravelled = 0f;
                    _lungeHit = false;
                    HideMarker();
                    break;

                case State.Recover:
                    ResetPose();
                    if (stats.RetreatDistance > 0f)
                        _enemy.MoveTo(transform.position - _lungeDirection * stats.RetreatDistance);
                    break;
            }
        }

        private void ShowMarker()
        {
            if (_marker == null) return;
            EnemyStats stats = _enemy.Stats;
            float length = _markerLength > 0f ? _markerLength : stats.AttackRange + stats.LungeHitRadius;
            Transform marker = _marker.transform;
            marker.SetPositionAndRotation(transform.position + Vector3.up * 0.03f, transform.rotation);
            marker.localScale = new Vector3(_markerWidth, 1f, length * 0.4f);
            _marker.gameObject.SetActive(true);
        }

        private void HideMarker()
        {
            if (_marker != null && _marker.gameObject.activeSelf) _marker.gameObject.SetActive(false);
        }

        /// <summary>Tell pose at progress <paramref name="t"/> (0..1): arms or crouch reach full pose early and hold.</summary>
        private void ApplyTellPose(float t)
        {
            float pose = Mathf.Clamp01(t * 2.5f);
            pose = 1f - (1f - pose) * (1f - pose); // ease out

            if (_tellStyle == TellStyle.ArmsAndWedge)
            {
                if (_armL != null) _armL.localRotation = _armLBase * Quaternion.Euler(0f, 0f, -_armOpenAngle * pose);
                if (_armR != null) _armR.localRotation = _armRBase * Quaternion.Euler(0f, 0f, _armOpenAngle * pose);
            }
            else if (_enemy.Model != null && _enemy.Model != transform)
            {
                Vector3 scale = _modelBaseScale;
                scale.y *= Mathf.Lerp(1f, _crouchScale, pose);
                _enemy.Model.localScale = scale;
            }

            if (_marker != null && _marker.gameObject.activeSelf)
            {
                EnemyStats stats = _enemy.Stats;
                float length = _markerLength > 0f ? _markerLength : stats.AttackRange + stats.LungeHitRadius;
                Transform marker = _marker.transform;
                marker.SetPositionAndRotation(transform.position + Vector3.up * 0.03f, transform.rotation);
                marker.localScale = new Vector3(_markerWidth, 1f, length * Mathf.Lerp(0.4f, 1f, pose));
            }
        }

        private void ResetPose()
        {
            HideMarker();
            if (_armL != null) _armL.localRotation = _armLBase;
            if (_armR != null) _armR.localRotation = _armRBase;
            if (_enemy != null && _enemy.Model != null && _enemy.Model != transform) _enemy.Model.localScale = _modelBaseScale;
        }
    }
}
