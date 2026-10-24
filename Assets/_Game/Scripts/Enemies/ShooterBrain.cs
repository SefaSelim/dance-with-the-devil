using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace DanceWithTheDevil
{
    /// <summary>
    /// Ranged brain for the Shooter (GDD §6): keeps 7–11 m from the player with a clear line of sight (walls block it;
    /// the player and other enemies do not), never fires through walls. Tell: 0.8 s orange aim line, the last 0.25 s
    /// locked (the line thickens); then one orb (14 m/s, 12 damage) that damages only the player and stops on walls.
    /// Lead (F2): while the line tracks, it points a modest lead ahead of a moving player (part of where they will be
    /// when the orb arrives if they keep going, capped at a few metres). The line always shows the real shot, so
    /// turning, stopping or dodging once it thickens escapes, while strafing in a straight line does not.
    /// Flanking (F2): during the 1.6 s recovery after each shot the shooter walks to a new spot on the ring around the
    /// player, preferring bearings away from other shooters (crossfire) and along the player's line of travel.
    /// </summary>
    [RequireComponent(typeof(Enemy))]
    [DisallowMultipleComponent]
    public sealed class ShooterBrain : MonoBehaviour
    {
        private const int MaxHits = 16;

        private static readonly float[] CandidateAngles = { 0f, 25f, -25f, 50f, -50f, 75f, -75f, 110f, -110f };
        private static readonly List<ShooterBrain> ActiveShooters = new List<ShooterBrain>(8);

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
        [Tooltip("Turn speed toward the aim, degrees per second.")]
        [SerializeField, Min(0f)] private float _turnSpeed = 360f;

        [Header("Lead (F2)")]
        [Tooltip("Share of the player's motion until the orb arrives that the aim leads by (0 = aim at the player).")]
        [SerializeField, Range(0f, 1f)] private float _aimLead = 0.85f;
        [Tooltip("Largest aim lead ahead of the player, metres.")]
        [SerializeField, Min(0f)] private float _maxAimLead = 3.5f;

        [Header("Positioning")]
        [Tooltip("Seconds between positioning decisions.")]
        [SerializeField, Min(0.05f)] private float _thinkInterval = 0.3f;
        [Tooltip("Distance aimed for when picking a new spot (inside the preferred band).")]
        [SerializeField, Min(0f)] private float _preferredDistance = 9f;
        [Tooltip("NavMesh sample radius for candidate spots, metres.")]
        [SerializeField, Min(0.1f)] private float _sampleRadius = 2f;

        [Header("Flanking (F2)")]
        [Tooltip("Walk to a new spot on the ring during the recovery after each shot.")]
        [SerializeField] private bool _flankAfterShot = true;
        [Tooltip("Smallest bearing change of a flank move, degrees.")]
        [SerializeField, Range(0f, 90f)] private float _minFlankAngle = 25f;
        [Tooltip("Score weight for bearing separation from other shooters (crossfire, full at 90°).")]
        [SerializeField, Min(0f)] private float _separationWeight = 1f;
        [Tooltip("Score weight for standing along the player's line of travel (their motion then runs along the shot).")]
        [SerializeField, Min(0f)] private float _travelLineWeight = 0.5f;
        [Tooltip("Score cost per metre walked to a spot.")]
        [SerializeField, Min(0f)] private float _walkCost = 0.08f;

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
        // Humanoid body: Tell clip while aiming, Attack clip on the shot, when the controller has them (null for cubes).
        private EnemyBodyAnimator _body;
        private LineRenderer _aimLine;
        private State _state;
        private float _stateStart;
        private float _nextThink;
        private Vector3 _aimDirection = Vector3.forward;
        private bool _locked;

        public bool IsTelling => _state == State.Tell;
        public bool IsLocked => _state == State.Tell && _locked;
        /// <summary>Flat direction of the aim line (valid while telling).</summary>
        public Vector3 AimDirection => _aimDirection;
        /// <summary>Where the orb leaves the body.</summary>
        public Vector3 MuzzlePoint => MuzzlePosition();
        /// <summary>Seconds left in the current tell (0 when not telling).</summary>
        public float TellRemaining => _state == State.Tell ? Mathf.Max(0f, _enemy.Stats.TellDuration - (Time.time - _stateStart)) : 0f;

        /// <summary>Raised when the 0.8 s aim tell starts (audio).</summary>
        public static event System.Action<Enemy> TellStarted;
        /// <summary>Raised when a tell ends without its shot (death, lost line of sight); the tell sound stops.</summary>
        public static event System.Action<Enemy> TellCancelled;

        private void Awake()
        {
            _enemy = GetComponent<Enemy>();
            _body = EnemyBodyAnimator.Find(_enemy.Model);
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
            if (_enemy.IsAlive)
            {
                Register(true);
                Enter(State.Reposition);
            }
        }

        private void OnDisable()
        {
            _enemy.Spawned -= OnSpawned;
            _enemy.Died -= OnDied;
            Register(false);
            HideLine();
        }

        private void OnSpawned(Enemy enemy)
        {
            HideLine();
            Register(true);
            Enter(State.Reposition);
            _nextThink = Time.time + Random.Range(0f, _thinkInterval);
        }

        private void OnDied(Enemy enemy)
        {
            if (_state == State.Tell) TellCancelled?.Invoke(_enemy);
            Register(false);
            HideLine();
            if (_body != null) _body.SetTell(false);
            _state = State.Idle;
        }

        private void Register(bool on)
        {
            int index = ActiveShooters.IndexOf(this);
            if (on && index < 0) ActiveShooters.Add(this);
            else if (!on && index >= 0) ActiveShooters.RemoveAt(index);
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

            if (TryFindSpot(false, out Vector3 spot)) _enemy.MoveTo(spot);
            else _enemy.MoveTo(_enemy.Target.position); // no good spot: close in until one appears
        }

        /// <summary>
        /// Best spot on a ring around the player (preferred distance) with sight of the player: candidates around the
        /// current bearing, scored by separation from other shooters, by lying along the player's line of travel and
        /// by the walk. <paramref name="mustMove"/>: skip bearings closer than the minimum flank angle.
        /// </summary>
        private bool TryFindSpot(bool mustMove, out Vector3 spot)
        {
            Transform target = _enemy.Target;
            Vector3 targetPosition = target.position;
            Vector3 position = transform.position;
            Vector3 away = position - targetPosition;
            away.y = 0f;
            if (away.sqrMagnitude < 1e-4f)
            {
                away = -transform.forward;
                away.y = 0f;
            }
            if (away.sqrMagnitude < 1e-6f) away = Vector3.back;
            away.Normalize();

            EnemyStats stats = _enemy.Stats;
            float radius = Mathf.Clamp(_preferredDistance, stats.PreferredRange.x, Mathf.Max(stats.PreferredRange.x, stats.PreferredRange.y));

            Vector3 velocity = TargetMotion.Velocity(target);
            float speed = velocity.magnitude;
            Vector3 travel = speed > 0.5f ? velocity / speed : Vector3.zero;
            float moving = Mathf.Clamp01(speed / 6f);

            bool found = false;
            float bestScore = float.NegativeInfinity;
            spot = default;
            for (int i = 0; i < CandidateAngles.Length; i++)
            {
                if (mustMove && Mathf.Abs(CandidateAngles[i]) < _minFlankAngle) continue;
                Vector3 bearing = Quaternion.AngleAxis(CandidateAngles[i], Vector3.up) * away;
                Vector3 candidate = targetPosition + bearing * radius;
                if (!NavMesh.SamplePosition(candidate, out NavMeshHit hit, _sampleRadius, NavMesh.AllAreas)) continue;
                if (!HasLineOfSight(hit.position)) continue;

                float score = _separationWeight * Separation(bearing, targetPosition)
                              + _travelLineWeight * moving * Mathf.Abs(Vector3.Dot(bearing, travel))
                              - _walkCost * Vector3.Distance(position, hit.position);
                if (score <= bestScore) continue;
                bestScore = score;
                spot = hit.position;
                found = true;
            }
            return found;
        }

        /// <summary>Smallest bearing difference to another living shooter, 0..1 (1 = 90° or more, or alone).</summary>
        private float Separation(Vector3 bearing, Vector3 targetPosition)
        {
            float smallest = 90f;
            for (int i = 0; i < ActiveShooters.Count; i++)
            {
                ShooterBrain other = ActiveShooters[i];
                if (other == null || other == this) continue;
                Vector3 otherBearing = other.transform.position - targetPosition;
                otherBearing.y = 0f;
                if (otherBearing.sqrMagnitude < 1e-4f) continue;
                float angle = Vector3.Angle(bearing, otherBearing);
                if (angle < smallest) smallest = angle;
            }
            return smallest / 90f;
        }

        private void TickTell(float elapsed, in EnemyStats stats)
        {
            float lockAt = Mathf.Max(0f, stats.TellDuration - stats.TellLockTime);
            Vector3 muzzle = MuzzlePosition();

            if (elapsed < lockAt)
            {
                _aimDirection = LeadAimPoint(muzzle, stats, elapsed) - muzzle;
                _aimDirection.y = 0f;
                if (_aimDirection.sqrMagnitude < 1e-6f) _aimDirection = transform.forward;
                _aimDirection.y = 0f;
                _aimDirection = _aimDirection.sqrMagnitude > 1e-6f ? _aimDirection.normalized : Vector3.forward;
                _enemy.FaceDirection(_aimDirection, _turnSpeed);
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

        /// <summary>
        /// The player's aim point plus a share of their motion until the orb would reach them (rest of the tell +
        /// flight time at the current distance), at most <see cref="_maxAimLead"/> ahead. At 9 m the lock-to-impact
        /// time is T = 0.25 + 9 / 14 = 0.89 s. A player crossing the line at u m/s travels T × u and the lead covers
        /// min(0.85 × T × u, 3.5 m); the orb hits within 0.2 (orb) + 0.35 (player) = 0.55 m, so anyone crossing
        /// slower than 0.55 / (0.15 × 0.89) = 4.1 m/s is hit, and a full 6 m/s sprint straight across misses by
        /// 6 × 0.89 − 3.5 = 1.9 m. Without lead the limit was 0.55 / 1.0 = 0.55 m/s (F2).
        /// </summary>
        private Vector3 LeadAimPoint(Vector3 muzzle, in EnemyStats stats, float elapsed)
        {
            Transform target = _enemy.Target;
            Vector3 toTarget = target.position - muzzle;
            toTarget.y = 0f;
            float flight = stats.ProjectileSpeed > 0.01f ? toTarget.magnitude / stats.ProjectileSpeed : 0f;
            float untilFire = Mathf.Max(0f, stats.TellDuration - elapsed);
            return TargetMotion.Predict(target, untilFire + flight, _aimLead, _maxAimLead) + Vector3.up * _targetHeight;
        }

        private void Fire(Vector3 muzzle, in EnemyStats stats)
        {
            if (_body != null) _body.TriggerAttack();
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
            if (_body != null) _body.SetTell(state == State.Tell);
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
                    _aimDirection.y = 0f;
                    _aimDirection = _aimDirection.sqrMagnitude > 1e-6f ? _aimDirection.normalized : Vector3.forward;
                    if (_aimLine != null)
                    {
                        _aimLine.widthMultiplier = _aimLineWidth;
                        _aimLine.gameObject.SetActive(true);
                    }
                    break;

                case State.Recover:
                    HideLine();
                    _enemy.StopMoving();
                    // Flank during the recovery: the next tell starts from a new bearing.
                    if (_flankAfterShot && _enemy.Target != null && TryFindSpot(true, out Vector3 spot))
                        _enemy.MoveTo(spot);
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
