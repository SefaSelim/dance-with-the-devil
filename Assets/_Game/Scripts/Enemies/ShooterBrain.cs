using UnityEngine;
using UnityEngine.AI;

namespace DanceWithTheDevil
{
    /// <summary>
    /// Ranged brain for the Shooter (GDD §6): keeps 7–11 m from the player with a clear line of sight (walls block it;
    /// the player and other enemies do not), never fires through walls. Tell: 0.8 s orange aim line that tracks the
    /// player, the last 0.25 s locked (the line thickens); then one orb (12 m/s, 12 damage) that damages only the
    /// player and stops on walls; then 1.6 s recovery standing still.
    /// </summary>
    [RequireComponent(typeof(Enemy))]
    [DisallowMultipleComponent]
    public sealed class ShooterBrain : MonoBehaviour
    {
        private const int MaxHits = 16;

        private static readonly float[] CandidateAngles = { 0f, 35f, -35f, 70f, -70f, 110f, -110f };

        private enum State
        {
            Idle,
            Reposition,
            Tell,
            Recover,
        }

        [Header("References")]
        [Tooltip("Enemy projectile pool. Empty = one is added to this object (runtime cubes with the material below).")]
        [SerializeField] private ProjectilePool _pool;
        [Tooltip("Material for the runtime orb cubes when the pool is created automatically (orange).")]
        [SerializeField] private Material _projectileMaterial;

        [Header("Aim")]
        [Tooltip("Muzzle height above the feet, metres (also the line-of-sight height).")]
        [SerializeField, Min(0f)] private float _muzzleHeight = 1f;
        [Tooltip("Muzzle distance in front of the body, metres.")]
        [SerializeField, Min(0f)] private float _muzzleForward = 0.5f;
        [Tooltip("Player point aimed at, height above the player's feet.")]
        [SerializeField, Min(0f)] private float _targetHeight = 1f;
        [Tooltip("Layers that can block sight and shots (walls). Player and enemy colliders are ignored.")]
        [SerializeField] private LayerMask _obstacleMask = ~0;
        [Tooltip("Turn speed toward the player, degrees per second.")]
        [SerializeField, Min(0f)] private float _turnSpeed = 360f;

        [Header("Positioning")]
        [Tooltip("Seconds between positioning decisions.")]
        [SerializeField, Min(0.05f)] private float _thinkInterval = 0.3f;
        [Tooltip("Distance aimed for when picking a new spot (inside the preferred band).")]
        [SerializeField, Min(0f)] private float _preferredDistance = 9f;
        [Tooltip("NavMesh sample radius for candidate spots, metres.")]
        [SerializeField, Min(0.1f)] private float _sampleRadius = 2f;

        [Header("Projectile")]
        [Tooltip("Orb hit radius, metres.")]
        [SerializeField, Min(0.01f)] private float _projectileRadius = 0.2f;
        [Tooltip("Orb range, metres.")]
        [SerializeField, Min(0.1f)] private float _projectileRange = 25f;
        [Tooltip("ImpactBus Bullet intensity when the orb hits the player / a wall.")]
        [SerializeField, Range(0f, 1f)] private float _hitIntensity = 0.35f;
        [SerializeField, Range(0f, 1f)] private float _wallHitIntensity = 0.1f;
        [Tooltip("ImpactBus MuzzleFire intensity.")]
        [SerializeField, Range(0f, 1f)] private float _muzzleIntensity = 0.2f;

        [Header("Aim line (tell)")]
        [Tooltip("Aim line material (unlit). Empty = shared runtime URP Unlit.")]
        [SerializeField] private Material _aimLineMaterial;
        [Tooltip("Aim line colour (GDD: #FF7A1A).")]
        [SerializeField] private Color _aimLineColor = new Color(1f, 0.478f, 0.102f);
        [Tooltip("Aim line width while tracking, metres.")]
        [SerializeField, Min(0.005f)] private float _aimLineWidth = 0.08f;
        [Tooltip("Aim line width once the direction is locked, metres.")]
        [SerializeField, Min(0.005f)] private float _lockedLineWidth = 0.14f;
        [Tooltip("Aim line maximum length, metres (stops at walls).")]
        [SerializeField, Min(0.1f)] private float _aimLineLength = 20f;

        private readonly RaycastHit[] _hits = new RaycastHit[MaxHits];

        private Enemy _enemy;
        private LineRenderer _aimLine;
        private State _state;
        private float _stateStart;
        private float _nextThink;
        private Vector3 _aimDirection = Vector3.forward;
        private bool _locked;

        public bool IsTelling => _state == State.Tell;
        public bool IsLocked => _state == State.Tell && _locked;

        /// <summary>Raised when the 0.8 s aim tell starts (audio).</summary>
        public static event System.Action<Enemy> TellStarted;
        /// <summary>Raised when a tell ends without its shot (death, lost line of sight); the tell sound stops.</summary>
        public static event System.Action<Enemy> TellCancelled;

        private void Awake()
        {
            _enemy = GetComponent<Enemy>();
            _aimLine = EnemyTelegraphs.CreateLine(name + "_AimLine", _aimLineMaterial, _aimLineColor, _aimLineWidth);
            if (_pool == null)
            {
                _pool = gameObject.AddComponent<ProjectilePool>();
                _pool.Configure(null, _projectileMaterial);
            }
        }

        private void OnDestroy() => EnemyTelegraphs.Destroy(_aimLine);

        private void OnEnable()
        {
            _enemy.Spawned += OnSpawned;
            _enemy.Died += OnDied;
            if (_enemy.IsAlive) Enter(State.Reposition);
        }

        private void OnDisable()
        {
            _enemy.Spawned -= OnSpawned;
            _enemy.Died -= OnDied;
            HideLine();
        }

        private void OnSpawned(Enemy enemy)
        {
            HideLine();
            Enter(State.Reposition);
            _nextThink = Time.time + Random.Range(0f, _thinkInterval);
        }

        private void OnDied(Enemy enemy)
        {
            if (_state == State.Tell) TellCancelled?.Invoke(_enemy);
            HideLine();
            _state = State.Idle;
        }

        private void Update()
        {
            if (!_enemy.IsAlive || _state == State.Idle) return;
            Transform target = _enemy.Target;
            if (target == null)
            {
                _enemy.StopMoving();
                HideLine();
                return;
            }

            float now = Time.time;
            float elapsed = now - _stateStart;
            EnemyStats stats = _enemy.Stats;

            switch (_state)
            {
                case State.Reposition:
                    _enemy.FaceDirection(_enemy.FlatToTarget(), _turnSpeed);
                    if (now >= _nextThink)
                    {
                        _nextThink = now + _thinkInterval;
                        Think(stats);
                    }
                    break;

                case State.Tell:
                    TickTell(elapsed, stats);
                    break;

                case State.Recover:
                    _enemy.FaceDirection(_enemy.FlatToTarget(), _turnSpeed);
                    if (elapsed >= stats.Recovery) Enter(State.Reposition);
                    break;
            }
        }

        private void Think(in EnemyStats stats)
        {
            Vector3 position = transform.position;
            float distance = _enemy.FlatToTarget().magnitude;
            bool inBand = distance >= stats.PreferredRange.x && distance <= stats.PreferredRange.y;
            if (inBand && HasLineOfSight(position))
            {
                Enter(State.Tell);
                return;
            }

            if (TryFindSpot(position, out Vector3 spot)) _enemy.MoveTo(spot);
            else _enemy.MoveTo(_enemy.Target.position); // no good spot: close in until one appears
        }

        /// <summary>Candidate spots on a ring around the player, nearest bearing first; first with sight wins.</summary>
        private bool TryFindSpot(Vector3 position, out Vector3 spot)
        {
            Vector3 targetPosition = _enemy.Target.position;
            Vector3 away = position - targetPosition;
            away.y = 0f;
            if (away.sqrMagnitude < 1e-4f) away = -transform.forward;
            away.Normalize();

            EnemyStats stats = _enemy.Stats;
            float radius = Mathf.Clamp(_preferredDistance, stats.PreferredRange.x, Mathf.Max(stats.PreferredRange.x, stats.PreferredRange.y));

            for (int i = 0; i < CandidateAngles.Length; i++)
            {
                Vector3 candidate = targetPosition + Quaternion.AngleAxis(CandidateAngles[i], Vector3.up) * away * radius;
                if (!NavMesh.SamplePosition(candidate, out NavMeshHit hit, _sampleRadius, NavMesh.AllAreas)) continue;
                if (!HasLineOfSight(hit.position)) continue;
                spot = hit.position;
                return true;
            }
            spot = default;
            return false;
        }

        private void TickTell(float elapsed, in EnemyStats stats)
        {
            float lockAt = Mathf.Max(0f, stats.TellDuration - stats.TellLockTime);
            Vector3 muzzle = MuzzlePosition();

            if (elapsed < lockAt)
            {
                _enemy.FaceDirection(_enemy.FlatToTarget(), _turnSpeed);
                _aimDirection = AimPoint() - muzzle;
                _aimDirection.y = 0f;
                if (_aimDirection.sqrMagnitude < 1e-6f) _aimDirection = transform.forward;
                _aimDirection.Normalize();
            }
            else if (!_locked)
            {
                // Lock moment: never commit to a shot that a wall already blocks.
                if (!HasLineOfSight(transform.position))
                {
                    Enter(State.Reposition);
                    return;
                }
                _locked = true;
                if (_aimLine != null) _aimLine.widthMultiplier = _lockedLineWidth;
            }

            UpdateLine(muzzle);

            if (elapsed >= stats.TellDuration)
            {
                Fire(muzzle, stats);
                Enter(State.Recover);
            }
        }

        private void Fire(Vector3 muzzle, in EnemyStats stats)
        {
            Projectile projectile = _pool.Get();
            projectile.Launch(new ProjectileLaunch
            {
                Origin = muzzle,
                Direction = _aimDirection,
                Speed = stats.ProjectileSpeed,
                MaxRange = _projectileRange,
                Radius = _projectileRadius,
                HitMask = ~0,
                IgnoreRoot = transform,
                Source = gameObject,
                SourceIsLocalPlayer = false,
                Damage = stats.Damage,
                Kind = DamageKind.Bullet,
                PierceCount = 0,
                HitIntensity = _hitIntensity,
                WallHitIntensity = _wallHitIntensity,
                DamageOnlyUnder = _enemy.Target,
            });

            ImpactBus.Raise(new ImpactEvent
            {
                kind = ImpactKind.MuzzleFire,
                position = muzzle,
                normal = _aimDirection,
                direction = _aimDirection,
                intensity = _muzzleIntensity,
                surface = SurfaceType.Default,
                instigator = gameObject,
            });
        }

        private void Enter(State state)
        {
            if (_state == State.Tell && state != State.Recover) TellCancelled?.Invoke(_enemy);
            _state = state;
            _stateStart = Time.time;
            var agent = _enemy.Agent;
            if (agent.enabled) agent.updateRotation = false; // the shooter always faces the player, even backing off

            switch (state)
            {
                case State.Reposition:
                    HideLine();
                    _nextThink = Time.time;
                    break;

                case State.Tell:
                    _enemy.StopMoving();
                    _locked = false;
                    TellStarted?.Invoke(_enemy);
                    _aimDirection = transform.forward;
                    if (_aimLine != null)
                    {
                        _aimLine.widthMultiplier = _aimLineWidth;
                        _aimLine.gameObject.SetActive(true);
                    }
                    break;

                case State.Recover:
                    HideLine();
                    _enemy.StopMoving();
                    break;
            }
        }

        private void UpdateLine(Vector3 muzzle)
        {
            if (_aimLine == null) return;
            float length = _aimLineLength;
            int count = Physics.RaycastNonAlloc(muzzle, _aimDirection, _hits, _aimLineLength, _obstacleMask,
                QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                if (IsIgnored(_hits[i].collider)) continue;
                if (_hits[i].distance < length) length = _hits[i].distance;
            }
            _aimLine.SetPosition(0, muzzle);
            _aimLine.SetPosition(1, muzzle + _aimDirection * length);
        }

        private void HideLine()
        {
            if (_aimLine != null && _aimLine.gameObject.activeSelf) _aimLine.gameObject.SetActive(false);
        }

        private Vector3 MuzzlePosition()
        {
            Vector3 forward = transform.forward;
            forward.y = 0f;
            return transform.position + Vector3.up * _muzzleHeight + forward.normalized * _muzzleForward;
        }

        private Vector3 AimPoint() => _enemy.Target.position + Vector3.up * _targetHeight;

        /// <summary>
        /// True if no wall lies between <paramref name="feet"/> (at muzzle height) and the player's aim point.
        /// Colliders of the player, of enemies and triggers do not block.
        /// </summary>
        public bool HasLineOfSight(Vector3 feet)
        {
            Transform target = _enemy.Target;
            if (target == null) return false;
            Vector3 from = feet + Vector3.up * _muzzleHeight;
            Vector3 to = AimPoint();
            Vector3 delta = to - from;
            float distance = delta.magnitude;
            if (distance < 1e-4f) return true;

            int count = Physics.RaycastNonAlloc(from, delta / distance, _hits, distance, _obstacleMask,
                QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
                if (!IsIgnored(_hits[i].collider)) return false;
            return true;
        }

        private bool IsIgnored(Collider collider)
        {
            if (collider == null) return true;
            Transform t = collider.transform;
            if (t.IsChildOf(transform)) return true;
            Transform target = _enemy.Target;
            if (target != null && t.IsChildOf(target)) return true;
            return collider.GetComponentInParent<Enemy>() != null;
        }
    }
}
