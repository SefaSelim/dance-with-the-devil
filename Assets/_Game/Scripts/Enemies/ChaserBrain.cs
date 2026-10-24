using UnityEngine;

namespace DanceWithTheDevil
{
    /// <summary>
    /// Melee brain for the Chaser and the Runner (GDD §6), driven by the <see cref="Enemy"/> stats.
    /// Approach on the NavMesh toward where the player is going: an intercept point up to a few metres ahead of a
    /// moving player, plus a per-enemy sideways offset far out so a pack closes in around the player instead of
    /// trailing in single file (the fast Runner therefore cuts across the player's path). In attack range: stop and
    /// tell (Chaser: arms open ~70° and an orange floor wedge; Runner: crouch and a short floor line). During the
    /// tell the body, and with it the marker, turns toward where the player will be when the lunge lands, and the
    /// marker grows to the planned dash length, so it always shows the real lunge. Lunge: a forward dash in the
    /// locked facing, long enough to reach that point (between LungeDistance and MaxLungeDistance) but stopping at
    /// body contact (no overlap or shove), that damages the player once (Contact) if the player is inside the hit
    /// sphere in front → recovery (the Runner backs off) → approach again.
    /// Counterplay (F2): running straight away or dodging through the lunge escapes; strafing across the marker
    /// without turning does not.
    /// </summary>
    [RequireComponent(typeof(Enemy))]
    [DisallowMultipleComponent]
    public sealed class ChaserBrain : MonoBehaviour
    {
        private const int MaxOverlaps = 16;
        private const float FallbackPlayerRadius = 0.35f;

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
        [Tooltip("Arm rotation around Z at the end of the tell, degrees (cubes \"Arm_L\"/\"Arm_R\" anywhere under the model).")]
        [SerializeField, Range(0f, 120f)] private float _armOpenAngle = 70f;
        [Tooltip("Model height scale at the bottom of the crouch (CrouchAndLine).")]
        [SerializeField, Range(0.3f, 1f)] private float _crouchScale = 0.75f;
        [Tooltip("Floor marker width in metres (wedge base or line width).")]
        [SerializeField, Min(0.02f)] private float _markerWidth = 1.2f;
        [Tooltip("Floor marker length in metres. 0 = the planned lunge plus the hit sphere's reach.")]
        [SerializeField, Min(0f)] private float _markerLength;
        [Tooltip("Floor marker material (unlit). Empty = shared runtime URP Unlit.")]
        [SerializeField] private Material _telegraphMaterial;
        [Tooltip("Floor marker colour (GDD: #FF5A1F).")]
        [SerializeField] private Color _telegraphColor = new Color(1f, 0.353f, 0.122f);

        [Header("Movement")]
        [Tooltip("Seconds between path updates while approaching.")]
        [SerializeField, Min(0.05f)] private float _repathInterval = 0.2f;
        [Tooltip("Turn speed toward the lunge aim during tell and recovery, degrees per second.")]
        [SerializeField, Min(0f)] private float _turnSpeed = 540f;

        [Header("Pursuit (F2)")]
        [Tooltip("Most seconds of the player's motion the approach leads by (intercept course). Lead time = distance / own speed, capped here.")]
        [SerializeField, Min(0f)] private float _maxLeadTime = 0.8f;
        [Tooltip("Largest approach lead ahead of the player, metres.")]
        [SerializeField, Min(0f)] private float _maxLeadDistance = 4f;
        [Tooltip("Largest sideways offset of the approach point (random side and size per spawn), metres. Fades out between 8 m and 3 m from the player.")]
        [SerializeField, Min(0f)] private float _flankOffset = 1.5f;

        [Header("Lunge aim (F2)")]
        [Tooltip("Share of the player's motion until the lunge lands that the tell facing leads by (0 = face the player).")]
        [SerializeField, Range(0f, 1f)] private float _tellLead = 0.85f;
        [Tooltip("Largest lead of the tell facing ahead of the player, metres.")]
        [SerializeField, Min(0f)] private float _maxTellLead = 2f;
        [Tooltip("Extra gap kept at body contact on top of the agent radius and the player's controller radius, metres.")]
        [SerializeField, Min(0f)] private float _contactMargin = 0.05f;

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
        private float _lungeLength;
        private float _lungeDuration;
        private float _plannedLunge;
        private bool _lungeHit;
        private float _flankSide;

        private Transform _contactTarget;
        private CharacterController _targetController;

        private Transform _armL;
        private Transform _armR;
        private Quaternion _armLBase;
        private Quaternion _armRBase;
        private Vector3 _modelBaseScale = Vector3.one;
        private MeshRenderer _marker;

        public bool IsTelling => _state == State.Tell;
        public bool IsLunging => _state == State.Lunge;
        /// <summary>Seconds left in the current tell (0 when not telling).</summary>
        public float TellRemaining => _state == State.Tell ? Mathf.Max(0f, _enemy.Stats.TellDuration - (Time.time - _stateStart)) : 0f;
        /// <summary>Flat direction of the coming (or running) lunge: the body facing during the tell.</summary>
        public Vector3 LungeDirection => _state == State.Lunge ? _lungeDirection : FlatForward();

        /// <summary>Raised when a tell starts (audio). Bool: Runner style (crouch and line).</summary>
        public static event System.Action<Enemy, bool> TellStarted;
        /// <summary>Raised when a tell ends without its lunge (death or reset).</summary>
        public static event System.Action<Enemy> TellCancelled;

        private void Awake()
        {
            _enemy = GetComponent<Enemy>();
            Transform model = _enemy.Model != null ? _enemy.Model : transform;
            _modelBaseScale = model.localScale;
            // Searched at any depth: a procedural animator may put the arms under shoulder pivots.
            _armL = FindDeep(model, "Arm_L");
            _armR = FindDeep(model, "Arm_R");
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
            _flankSide = Random.Range(-1f, 1f);
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
                    Vector3 aim = LungeAimPoint(stats, elapsed) - transform.position;
                    aim.y = 0f;
                    _enemy.FaceDirection(aim, _turnSpeed);
                    _plannedLunge = PlanLunge(aim, stats);
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
            float distance = toTarget.magnitude;
            if (distance <= stats.AttackRange)
            {
                Enter(State.Tell);
                return;
            }

            if (now >= _nextRepath)
            {
                _nextRepath = now + _repathInterval;
                if (!_enemy.MoveTo(ApproachPoint(toTarget, distance, stats))) _enemy.MoveTo(_enemy.Target.position);
            }
        }

        /// <summary>
        /// Intercept point: the player's position plus their velocity × (distance / own speed, at most
        /// <see cref="_maxLeadTime"/>), at most <see cref="_maxLeadDistance"/> ahead; far out, shifted sideways by
        /// this enemy's flank offset so a pack spreads around the player.
        /// </summary>
        private Vector3 ApproachPoint(Vector3 toTarget, float distance, in EnemyStats stats)
        {
            float speed = Mathf.Max(0.1f, stats.MoveSpeed * Enemy.SpeedMultiplier);
            float leadTime = Mathf.Min(distance / speed, _maxLeadTime);
            Vector3 point = TargetMotion.Predict(_enemy.Target, leadTime, 1f, _maxLeadDistance);
            if (_flankOffset > 0f && distance > 1e-3f)
            {
                float fade = Mathf.Clamp01((distance - 3f) / 5f);
                var side = new Vector3(toTarget.z, 0f, -toTarget.x) / distance;
                point += side * (_flankSide * _flankOffset * fade);
            }
            return point;
        }

        /// <summary>Where the player will be when a lunge started after the rest of the tell lands (scaled lead).</summary>
        private Vector3 LungeAimPoint(in EnemyStats stats, float tellElapsed)
        {
            float remaining = Mathf.Max(0f, stats.TellDuration - tellElapsed);
            float dash = Mathf.Min(_enemy.FlatToTarget().magnitude, MaxLunge(stats));
            return TargetMotion.Predict(_enemy.Target, remaining + dash / LungeSpeed(stats), _tellLead, _maxTellLead);
        }

        /// <summary>Dash length toward <paramref name="aim"/> along the current facing: ends at body contact, clamped to the stats.</summary>
        private float PlanLunge(Vector3 aim, in EnemyStats stats)
        {
            float along = Vector3.Dot(aim, FlatForward());
            return Mathf.Clamp(along - ContactDistance(), stats.LungeDistance, MaxLunge(stats));
        }

        private void TickLunge(in EnemyStats stats)
        {
            float step = Mathf.Min(LungeSpeed(stats) * Time.deltaTime, Mathf.Max(0f, _lungeLength - _lungeTravelled));
            // Stop at body contact (F23): never run into the player's capsule and shove them.
            float room = _enemy.FlatToTarget().magnitude - ContactDistance();
            step = Mathf.Min(step, Mathf.Max(0f, room));
            if (step > 0f)
            {
                _enemy.MoveBy(_lungeDirection * step);
                _lungeTravelled += step;
            }

            if (!_lungeHit) TryHit(stats);

            if (_lungeTravelled >= _lungeLength - 1e-4f || Time.time - _stateStart >= _lungeDuration)
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
                    _plannedLunge = stats.LungeDistance;
                    ShowMarker();
                    ApplyTellPose(0f);
                    TellStarted?.Invoke(_enemy, _tellStyle == TellStyle.CrouchAndLine);
                    break;

                case State.Lunge:
                    _lungeDirection = FlatForward();
                    Vector3 aim = _enemy.Target != null ? LungeAimPoint(stats, stats.TellDuration) - transform.position : _lungeDirection;
                    aim.y = 0f;
                    _lungeLength = PlanLunge(aim, stats);
                    _lungeDuration = _lungeLength / LungeSpeed(stats);
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

        /// <summary>Dash speed: the shortest lunge over its duration (a longer lunge lasts longer, it is not faster).</summary>
        private static float LungeSpeed(in EnemyStats stats) => stats.LungeDistance / Mathf.Max(0.01f, stats.LungeDuration);

        private static float MaxLunge(in EnemyStats stats) => Mathf.Max(stats.LungeDistance, stats.MaxLungeDistance);

        /// <summary>Flat distance between the centres at body contact: agent radius + player controller radius + margin.</summary>
        private float ContactDistance()
        {
            Transform target = _enemy.Target;
            if (target != _contactTarget)
            {
                _contactTarget = target;
                _targetController = target != null ? target.GetComponentInParent<CharacterController>() : null;
            }

            float playerRadius = FallbackPlayerRadius;
            if (_targetController != null)
            {
                Vector3 scale = _targetController.transform.lossyScale;
                playerRadius = _targetController.radius * Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.z));
            }
            float ownRadius = _enemy.Agent != null ? _enemy.Agent.radius : FallbackPlayerRadius;
            return ownRadius + playerRadius + _contactMargin;
        }

        private Vector3 FlatForward()
        {
            Vector3 forward = transform.forward;
            forward.y = 0f;
            return forward.sqrMagnitude > 1e-6f ? forward.normalized : Vector3.forward;
        }

        private float MarkerLength(in EnemyStats stats)
        {
            return _markerLength > 0f ? _markerLength : _plannedLunge + _hitForwardOffset + stats.LungeHitRadius * 0.5f;
        }

        private void ShowMarker()
        {
            if (_marker == null) return;
            Transform marker = _marker.transform;
            marker.SetPositionAndRotation(transform.position + Vector3.up * 0.03f, transform.rotation);
            marker.localScale = new Vector3(_markerWidth, 1f, MarkerLength(_enemy.Stats) * 0.4f);
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
                Transform marker = _marker.transform;
                marker.SetPositionAndRotation(transform.position + Vector3.up * 0.03f, transform.rotation);
                marker.localScale = new Vector3(_markerWidth, 1f, MarkerLength(_enemy.Stats) * Mathf.Lerp(0.4f, 1f, pose));
            }
        }

        private void ResetPose()
        {
            HideMarker();
            if (_armL != null) _armL.localRotation = _armLBase;
            if (_armR != null) _armR.localRotation = _armRBase;
            if (_enemy != null && _enemy.Model != null && _enemy.Model != transform) _enemy.Model.localScale = _modelBaseScale;
        }

        private static Transform FindDeep(Transform root, string childName)
        {
            Transform direct = root.Find(childName);
            if (direct != null) return direct;
            for (int i = 0; i < root.childCount; i++)
            {
                Transform found = FindDeep(root.GetChild(i), childName);
                if (found != null) return found;
            }
            return null;
        }
    }
}
