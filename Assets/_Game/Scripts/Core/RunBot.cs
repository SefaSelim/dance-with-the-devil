using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace DanceWithTheDevil
{
    /// <summary>
    /// 🧪 Balance probe (GDD §10): plays like a mediocre human so its death times can be read against the target
    /// schedule (GDD §3: ~0:50, 1:40, 2:30, 3:15, 4:00, then ~4:40). While enabled it turns off the player's input and:
    /// - moves at run speed about three quarters of the time, strafing around the nearest enemy at a comfortable
    ///   distance, backing off from enemies that get too close, picking a new direction every 1-2.5 s and staying
    ///   inside the arena; the rest of the time it stands still to aim;
    /// - notices about two thirds of the tells aimed at it and reacts after 0.2-0.45 s: dodges sideways out of a
    ///   Chaser/Runner lunge (or runs straight away with no dodge left), sidesteps against a Shooter's aim line and,
    ///   half the time, dodges when the line locks;
    /// - fires the rifle at the nearest enemy with ±12° error after a 0.35 s reaction, and a rocket at clumps of 3+.
    /// With a <see cref="DevilDirector"/> set it dies into the real contract flow and always takes EVET (lines and
    /// waits skipped), so contract costs and upgrades are part of the measurement; without one it revives at once at
    /// full health with 2 s invulnerability and the whole-arena shockwave (K-16). Logs every death against its target
    /// and a summary at 5:00 and at the ending. Leave it disabled (or remove it) for release.
    /// </summary>
    [DefaultExecutionOrder(20)] // after PlayerController so its facing wins this frame
    [DisallowMultipleComponent]
    public sealed class RunBot : MonoBehaviour
    {
        private const int MaxWallHits = 8;

        private struct Threat
        {
            public Enemy Enemy;
            public bool Shooter;
            public float ActAt;
            public bool DodgeOnly;
        }

        [Tooltip("Player. If empty, taken from this GameObject.")]
        [SerializeField] private PlayerController _player;
        [Tooltip("Rifle. If empty, searched on the player.")]
        [SerializeField] private Rifle _rifle;
        [Tooltip("Rocket launcher. If empty, searched on the player.")]
        [SerializeField] private RocketLauncher _rocketLauncher;
        [Tooltip("Wave director (enemy list, clock, shockwave).")]
        [SerializeField] private WaveDirector _director;
        [Tooltip("Devil director. Set: deaths go through the real contract flow and the bot always takes EVET. Empty: simulated revive.")]
        [SerializeField] private DevilDirector _devil;

        [Header("Simulated YES (no Devil director)")]
        [Tooltip("Invulnerability after the revive, seconds.")]
        [SerializeField, Min(0f)] private float _reviveInvulnerability = 2f;

        [Header("Target schedule (GDD §3)")]
        [Tooltip("Target death times of a medium-skill player, seconds (0:50, 1:40, 2:30, 3:15, 4:00, 4:40).")]
        [SerializeField] private float[] _targetDeaths = { 50f, 100f, 150f, 195f, 240f, 280f };

        [Header("Human-like aim (a perfect bot never dies)")]
        [Tooltip("Random aim error per shot, degrees either side.")]
        [SerializeField, Range(0f, 45f)] private float _aimErrorDegrees = 12f;
        [Tooltip("Seconds before the bot starts firing at a newly chosen target.")]
        [SerializeField, Min(0f)] private float _reactionTime = 0.35f;

        [Header("Rocket use")]
        [Tooltip("Enemies needed in one group to fire a rocket.")]
        [SerializeField, Min(1)] private int _rocketGroupSize = 3;
        [Tooltip("Group radius: enemies within this distance of the group centre enemy, metres.")]
        [SerializeField, Min(0f)] private float _rocketGroupRadius = 5f;

        [Header("Movement (mediocre human)")]
        [Tooltip("Run speed, m/s (GDD §4: 6). The player's speed multiplier applies on top.")]
        [SerializeField, Min(0f)] private float _moveSpeed = 6f;
        [Tooltip("Chance that the next movement spell is spent moving rather than standing still to aim.")]
        [SerializeField, Range(0f, 1f)] private float _moveChance = 0.75f;
        [Tooltip("Seconds between movement decisions (min, max).")]
        [SerializeField] private Vector2 _decisionInterval = new Vector2(1f, 2.5f);
        [Tooltip("Distance kept from the nearest enemy while strafing, metres; closer than half of it the bot backs off even when standing.")]
        [SerializeField, Min(0f)] private float _comfortDistance = 6f;
        [Tooltip("Arena centre the bot stays around.")]
        [SerializeField] private Vector3 _arenaCenter = Vector3.zero;
        [Tooltip("Half size (x, z) of the area the bot stays inside, metres (GDD §9 arena: 32 × 28 m).")]
        [SerializeField] private Vector2 _arenaHalfExtents = new Vector2(13f, 11f);
        [Tooltip("Look-ahead for walls before walking into them, metres.")]
        [SerializeField, Min(0.1f)] private float _wallProbe = 1.2f;
        [Tooltip("Layers that block walking (walls).")]
        [SerializeField] private LayerMask _wallMask = ~0;

        [Header("Reading tells (mediocre human)")]
        [Tooltip("Chance to notice a tell at all.")]
        [SerializeField, Range(0f, 1f)] private float _noticeChance = 0.65f;
        [Tooltip("Reaction time to a noticed tell, seconds (min, max).")]
        [SerializeField] private Vector2 _tellReaction = new Vector2(0.2f, 0.45f);
        [Tooltip("A melee tell counts as aimed at the bot within this distance, metres.")]
        [SerializeField, Min(0f)] private float _meleeThreatDistance = 4.5f;
        [Tooltip("A shooter's aim line counts as aimed at the bot if it passes within this distance, metres.")]
        [SerializeField, Min(0f)] private float _shotThreatWidth = 1.5f;
        [Tooltip("Chance to dodge when a shooter's line locks (otherwise only the sidestep).")]
        [SerializeField, Range(0f, 1f)] private float _shotDodgeChance = 0.5f;
        [Tooltip("Seconds an evasive move overrides normal movement.")]
        [SerializeField, Min(0f)] private float _evadeTime = 0.7f;

        private readonly List<float> _deathTimes = new List<float>(8);
        private readonly List<Threat> _threats = new List<Threat>(16);
        private readonly RaycastHit[] _wallHits = new RaycastHit[MaxWallHits];
        private readonly StringBuilder _text = new StringBuilder(256);
        private System.Action<Enemy, bool> _onChaserTell;
        private System.Action<Enemy> _onShooterTell;

        private PlayerInputReader _input;
        private Health _health;
        private bool _inputWasEnabled;
        private bool _summaryLogged;
        private bool _endLogged;
        private Enemy _currentTarget;
        private float _targetSince;

        private bool _moving;
        private float _strafeSign = 1f;
        private float _nextDecision;
        private Vector3 _evadeDirection;
        private float _evadeUntil;
        private Vector3 _lastMove;

        /// <summary>Run-clock times (seconds) of every player death this run.</summary>
        public IReadOnlyList<float> DeathTimes => _deathTimes;

        private void Awake()
        {
            if (_player == null) _player = GetComponent<PlayerController>();
            if (_player != null)
            {
                if (_rifle == null) _rifle = _player.GetComponentInChildren<Rifle>();
                if (_rocketLauncher == null) _rocketLauncher = _player.GetComponentInChildren<RocketLauncher>();
                _input = _player.InputReader != null ? _player.InputReader : _player.GetComponent<PlayerInputReader>();
            }
            _onChaserTell = OnChaserTell;
            _onShooterTell = OnShooterTell;
        }

        private void OnEnable()
        {
            if (_input != null)
            {
                _inputWasEnabled = _input.enabled;
                _input.enabled = false; // its actions turn off with it: no movement, no weapon input
            }
            Health health = GetHealth();
            if (health != null) health.Died += OnPlayerDied;
            ChaserBrain.TellStarted += _onChaserTell;
            ShooterBrain.TellStarted += _onShooterTell;
        }

        private void OnDisable()
        {
            if (_input != null && _inputWasEnabled) _input.enabled = true;
            Health health = GetHealth();
            if (health != null) health.Died -= OnPlayerDied;
            ChaserBrain.TellStarted -= _onChaserTell;
            ShooterBrain.TellStarted -= _onShooterTell;
            _threats.Clear();
        }

        private Health GetHealth()
        {
            if (_health == null && _player != null) _health = _player.GetComponent<Health>();
            return _health;
        }

        private bool UsesDevil => _devil != null && _devil.isActiveAndEnabled;

        private void Update()
        {
            if (_player == null || _director == null) return;

            if (!_summaryLogged && _director.HasBegun && _director.Elapsed >= _director.SpawnEndTime)
            {
                _summaryLogged = true;
                LogSummary("5:00 mark");
            }

            if (UsesDevil && _devil.State != DevilState.Playing)
            {
                DriveDevil();
                return;
            }

            Health health = GetHealth();
            if (health != null && health.IsDead) return;

            IReadOnlyList<Enemy> enemies = _director.ActiveEnemies;
            Vector3 position = _player.transform.position;
            Enemy nearest = null;
            float best = float.PositiveInfinity;
            for (int i = 0; i < enemies.Count; i++)
            {
                Enemy enemy = enemies[i];
                if (enemy == null || !enemy.IsAlive) continue;
                float d = FlatSqr(enemy.transform.position - position);
                if (d < best)
                {
                    best = d;
                    nearest = enemy;
                }
            }

            ReadTells(position);
            Move(position, nearest);
            if (nearest != null) Shoot(position, nearest, enemies);
        }

        // ------------------------------------------------------------ Devil flow

        /// <summary>Always EVET; every line, wait and the forced card are skipped.</summary>
        private void DriveDevil()
        {
            switch (_devil.State)
            {
                case DevilState.Offer:
                    _devil.ChooseYes();
                    break;
                case DevilState.Freezing:
                case DevilState.Speaking:
                case DevilState.Forced:
                case DevilState.Crumbling:
                    _devil.Advance();
                    break;
                case DevilState.Ended:
                    if (!_endLogged)
                    {
                        _endLogged = true;
                        LogSummary("ending");
                    }
                    break;
            }
        }

        private void OnPlayerDied(DamageInfo info)
        {
            float time = _director != null ? _director.Elapsed : Time.timeSinceLevelLoad;
            _deathTimes.Add(time);
            int index = _deathTimes.Count - 1;
            _text.Clear();
            _text.Append("[RunBot] death ").Append(_deathTimes.Count).Append(" at ").Append(FormatTime(time));
            AppendTarget(index, time);
            Debug.Log(_text.ToString(), this);

            _threats.Clear();
            _evadeUntil = 0f;
            if (UsesDevil) return; // the Devil director revives (EVET) or ends the run

            Health health = GetHealth();
            if (health != null)
            {
                health.Revive();
                health.SetInvulnerable(_reviveInvulnerability);
            }
            if (_director != null) _director.KillAll();
        }

        // ------------------------------------------------------------ reading tells

        private void OnChaserTell(Enemy enemy, bool runner) => Notice(enemy, false);

        private void OnShooterTell(Enemy enemy) => Notice(enemy, true);

        private void Notice(Enemy enemy, bool shooter)
        {
            if (!isActiveAndEnabled || enemy == null || Random.value > _noticeChance) return;
            float reaction = Random.Range(_tellReaction.x, Mathf.Max(_tellReaction.x, _tellReaction.y));
            _threats.Add(new Threat { Enemy = enemy, Shooter = shooter, ActAt = Time.time + reaction });
        }

        private void ReadTells(Vector3 position)
        {
            float now = Time.time;
            for (int i = _threats.Count - 1; i >= 0; i--)
            {
                Threat threat = _threats[i];
                if (threat.Enemy == null || !threat.Enemy.IsAlive)
                {
                    _threats.RemoveAt(i);
                    continue;
                }
                if (now < threat.ActAt) continue;

                _threats.RemoveAt(i);
                if (threat.Shooter) ReactToShot(threat, position);
                else ReactToLunge(threat.Enemy, position);
            }
        }

        private void ReactToLunge(Enemy enemy, Vector3 position)
        {
            if (!enemy.TryGetComponent(out ChaserBrain brain) || !(brain.IsTelling || brain.IsLunging)) return;
            Vector3 fromEnemy = position - enemy.transform.position;
            fromEnemy.y = 0f;
            float distance = fromEnemy.magnitude;
            if (distance > _meleeThreatDistance || distance < 1e-3f) return;
            Vector3 lunge = brain.LungeDirection;
            if (Vector3.Angle(lunge, fromEnemy) > 45f && distance > 2f) return; // not aimed at the bot

            // Sideways out of the lunge line, on the side the bot already is, and a little away.
            Vector3 side = new Vector3(lunge.z, 0f, -lunge.x);
            if (Vector3.Dot(side, fromEnemy) < 0f) side = -side;
            Vector3 escape = (side + fromEnemy / distance * 0.5f).normalized;
            if (_player.DodgeCharges > 0 && _player.TryDodge(escape)) return;
            Evade(fromEnemy / distance);
        }

        private void ReactToShot(Threat threat, Vector3 position)
        {
            if (!threat.Enemy.TryGetComponent(out ShooterBrain brain) || !brain.IsTelling) return;
            Vector3 aim = brain.AimDirection;
            aim.y = 0f;
            if (aim.sqrMagnitude < 1e-6f) return;
            aim.Normalize();
            Vector3 fromMuzzle = position - brain.MuzzlePoint;
            fromMuzzle.y = 0f;
            float along = Vector3.Dot(fromMuzzle, aim);
            Vector3 side = new Vector3(aim.z, 0f, -aim.x);
            float offset = Vector3.Dot(fromMuzzle, side);
            if (along < 0f || Mathf.Abs(offset) > _shotThreatWidth) return; // the line does not pass near the bot

            // Step away from the line (the line leads the bot's motion, so this usually means turning back).
            float sign = Mathf.Abs(offset) > 0.1f ? Mathf.Sign(offset) : -Mathf.Sign(Vector3.Dot(_lastMove, side) + 1e-3f);
            Vector3 away = side * sign;

            if (threat.DodgeOnly)
            {
                if (_player.DodgeCharges > 0) _player.TryDodge(away);
                return;
            }

            Evade(away);
            if (_player.DodgeCharges > 0 && Random.value < _shotDodgeChance)
            {
                // Dodge at the lock (a dodge before it is wasted: the line still tracks).
                float lockIn = Mathf.Max(0f, brain.TellRemaining - threat.Enemy.Stats.TellLockTime);
                _threats.Add(new Threat
                {
                    Enemy = threat.Enemy, Shooter = true, DodgeOnly = true,
                    ActAt = Time.time + lockIn + Random.Range(0f, 0.12f),
                });
            }
        }

        private void Evade(Vector3 direction)
        {
            direction.y = 0f;
            if (direction.sqrMagnitude < 1e-6f) return;
            _evadeDirection = direction.normalized;
            _evadeUntil = Time.time + _evadeTime;
        }

        // ------------------------------------------------------------ movement

        private void Move(Vector3 position, Enemy nearest)
        {
            float dt = Time.deltaTime;
            if (dt <= 0f || _player.IsDodging) return;
            CharacterController controller = _player.CharacterController;
            if (controller == null || !controller.enabled) return;

            Vector3 desired = DesiredDirection(position, nearest);
            if (desired.sqrMagnitude < 1e-4f)
            {
                _lastMove = Vector3.zero;
                return;
            }
            desired = AvoidWalls(position, desired);
            _lastMove = desired;
            controller.Move(desired * (_moveSpeed * _player.MoveSpeedMultiplier * dt));
        }

        private Vector3 DesiredDirection(Vector3 position, Enemy nearest)
        {
            float now = Time.time;
            if (now < _evadeUntil) return _evadeDirection;

            if (now >= _nextDecision)
            {
                _nextDecision = now + Random.Range(_decisionInterval.x, Mathf.Max(_decisionInterval.x, _decisionInterval.y));
                _moving = Random.value < _moveChance;
                if (Random.value < 0.5f) _strafeSign = -_strafeSign;
            }

            Vector3 desired = Vector3.zero;
            Vector3 orbitCenter = nearest != null ? nearest.transform.position : _arenaCenter;
            Vector3 toCenter = orbitCenter - position;
            toCenter.y = 0f;
            float distance = toCenter.magnitude;
            if (distance > 1e-3f)
            {
                Vector3 direction = toCenter / distance;
                if (nearest != null && distance < _comfortDistance * 0.5f)
                {
                    desired = -direction; // too close: back off even while "standing"
                }
                else if (_moving)
                {
                    var tangent = new Vector3(direction.z, 0f, -direction.x) * _strafeSign;
                    float radial = 0f;
                    if (nearest != null)
                    {
                        if (distance < _comfortDistance) radial = -0.6f;
                        else if (distance > _comfortDistance + 4f) radial = 0.4f;
                    }
                    desired = tangent + direction * radial;
                }
            }
            else if (_moving)
            {
                desired = new Vector3(_strafeSign, 0f, 0f);
            }

            // Stay inside the arena: steer back toward the centre near the edges.
            if (desired.sqrMagnitude > 1e-4f)
            {
                Vector3 fromCenter = position - _arenaCenter;
                float outX = Mathf.Abs(fromCenter.x) - (_arenaHalfExtents.x - 2f);
                float outZ = Mathf.Abs(fromCenter.z) - (_arenaHalfExtents.y - 2f);
                if (outX > 0f) desired.x -= Mathf.Sign(fromCenter.x) * Mathf.Clamp01(outX * 0.5f) * 2f;
                if (outZ > 0f) desired.z -= Mathf.Sign(fromCenter.z) * Mathf.Clamp01(outZ * 0.5f) * 2f;
                desired.y = 0f;
            }
            return desired.sqrMagnitude > 1e-4f ? desired.normalized : Vector3.zero;
        }

        /// <summary>Turns away from a wall right ahead (90° either way, else back); flips the strafe on a turn.</summary>
        private Vector3 AvoidWalls(Vector3 position, Vector3 desired)
        {
            Vector3 origin = position + Vector3.up * 0.5f;
            if (!Blocked(origin, desired)) return desired;
            var left = new Vector3(-desired.z, 0f, desired.x);
            Vector3 first = _strafeSign > 0f ? left : -left;
            _strafeSign = -_strafeSign;
            if (!Blocked(origin, first)) return first;
            if (!Blocked(origin, -first)) return -first;
            return -desired;
        }

        private bool Blocked(Vector3 origin, Vector3 direction)
        {
            int count = Physics.RaycastNonAlloc(origin, direction, _wallHits, _wallProbe, _wallMask,
                QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                Collider collider = _wallHits[i].collider;
                if (collider == null || collider.transform.IsChildOf(_player.transform)) continue;
                if (collider.TryGetComponent(out Enemy _)) continue; // enemies are not walls
                return true;
            }
            return false;
        }

        // ------------------------------------------------------------ shooting

        private void Shoot(Vector3 position, Enemy nearest, IReadOnlyList<Enemy> enemies)
        {
            if (nearest != _currentTarget)
            {
                _currentTarget = nearest;
                _targetSince = Time.time;
            }

            Vector3 toEnemy = nearest.transform.position - position;
            toEnemy.y = 0f;
            if (toEnemy.sqrMagnitude < 1e-6f) return;
            Vector3 aim = toEnemy.normalized;
            _player.transform.rotation = Quaternion.LookRotation(aim, Vector3.up);

            if (_rocketLauncher != null && _rocketLauncher.Charges > 0 && TryFindGroup(enemies, out Vector3 groupCenter))
            {
                Vector3 toGroup = groupCenter - position;
                toGroup.y = 0f;
                if (toGroup.sqrMagnitude > 1e-6f) _rocketLauncher.TryFire(_rocketLauncher.MuzzlePosition, toGroup.normalized);
            }

            if (_rifle != null && Time.time - _targetSince >= _reactionTime)
            {
                Vector3 shot = Quaternion.Euler(0f, Random.Range(-_aimErrorDegrees, _aimErrorDegrees), 0f) * aim;
                _rifle.TryFire(_rifle.MuzzlePosition, shot);
            }
        }

        /// <summary>An enemy with at least the group size of living enemies (itself included) within the group radius.</summary>
        private bool TryFindGroup(IReadOnlyList<Enemy> enemies, out Vector3 center)
        {
            float radiusSqr = _rocketGroupRadius * _rocketGroupRadius;
            for (int i = 0; i < enemies.Count; i++)
            {
                Enemy a = enemies[i];
                if (a == null || !a.IsAlive) continue;
                int count = 0;
                for (int j = 0; j < enemies.Count; j++)
                {
                    Enemy b = enemies[j];
                    if (b == null || !b.IsAlive) continue;
                    if (FlatSqr(b.transform.position - a.transform.position) <= radiusSqr) count++;
                }
                if (count >= _rocketGroupSize)
                {
                    center = a.transform.position;
                    return true;
                }
            }
            center = default;
            return false;
        }

        // ------------------------------------------------------------ log

        private void LogSummary(string label)
        {
            _text.Clear();
            _text.Append("[RunBot] ").Append(label).Append(" at ")
                .Append(FormatTime(_director != null ? _director.Elapsed : 0f)).Append(", deaths: ");
            if (_deathTimes.Count == 0) _text.Append("none");
            for (int i = 0; i < _deathTimes.Count; i++)
            {
                if (i > 0) _text.Append(", ");
                _text.Append(FormatTime(_deathTimes[i]));
                AppendTarget(i, _deathTimes[i]);
            }
            Debug.Log(_text.ToString(), this);
        }

        /// <summary>Appends " (target m:ss, ±N s)" when death <paramref name="index"/> has a target.</summary>
        private void AppendTarget(int index, float time)
        {
            if (_targetDeaths == null || index < 0 || index >= _targetDeaths.Length) return;
            float target = _targetDeaths[index];
            int diff = Mathf.RoundToInt(time - target);
            _text.Append(" (target ").Append(FormatTime(target)).Append(", ").Append(diff >= 0 ? "+" : "").Append(diff).Append(" s)");
        }

        /// <summary>m:ss, e.g. 1:05.</summary>
        public static string FormatTime(float seconds)
        {
            int total = Mathf.Max(0, Mathf.FloorToInt(seconds));
            return $"{total / 60}:{total % 60:00}";
        }

        private static float FlatSqr(Vector3 v) => v.x * v.x + v.z * v.z;
    }
}
