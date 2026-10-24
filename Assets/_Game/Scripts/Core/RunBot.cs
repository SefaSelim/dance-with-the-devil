using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace DanceWithTheDevil
{
    /// <summary>
    /// 🧪 Test tool for the Phase 4 exit test (GDD §10: "the stand-still bot dies earlier than a human; read its
    /// results as a lower bound"). While enabled it turns off the player's input, stands still, turns toward the nearest
    /// living enemy and fires the rifle at it, and fires a rocket at any group of 3+ enemies within 5 m of each other.
    /// On each player death it logs the time, revives the player at once at full health (a simulated YES) with 2 s
    /// invulnerability and kills every enemy within 6 m (K-12 shockwave). Logs a summary at 5:00.
    /// Remove from the scene before release.
    /// </summary>
    [DefaultExecutionOrder(20)] // after PlayerController so its facing wins this frame
    [DisallowMultipleComponent]
    public sealed class RunBot : MonoBehaviour
    {
        [Tooltip("Player. If empty, taken from this GameObject.")]
        [SerializeField] private PlayerController _player;
        [Tooltip("Rifle. If empty, searched on the player.")]
        [SerializeField] private Rifle _rifle;
        [Tooltip("Rocket launcher. If empty, searched on the player.")]
        [SerializeField] private RocketLauncher _rocketLauncher;
        [Tooltip("Wave director (enemy list, clock, shockwave).")]
        [SerializeField] private WaveDirector _director;

        [Header("Simulated YES (K-12)")]
        [Tooltip("Invulnerability after the revive, seconds.")]
        [SerializeField, Min(0f)] private float _reviveInvulnerability = 2f;
        [Tooltip("Shockwave radius around the player on revive, metres.")]
        [SerializeField, Min(0f)] private float _shockwaveRadius = 6f;

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

        private readonly List<float> _deathTimes = new List<float>(8);
        private PlayerInputReader _input;
        private bool _inputWasEnabled;
        private bool _summaryLogged;
        private Enemy _currentTarget;
        private float _targetSince;

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
        }

        private void OnDisable()
        {
            if (_input != null && _inputWasEnabled) _input.enabled = true;
            Health health = GetHealth();
            if (health != null) health.Died -= OnPlayerDied;
        }

        private Health GetHealth() => _player != null ? _player.GetComponent<Health>() : null;

        private void Update()
        {
            if (_player == null || _director == null) return;

            if (!_summaryLogged && _director.HasBegun && _director.Elapsed >= _director.SpawnEndTime)
            {
                _summaryLogged = true;
                LogSummary();
            }

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
            if (nearest == null) return;
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

        private void OnPlayerDied(DamageInfo info)
        {
            float time = _director != null ? _director.Elapsed : Time.timeSinceLevelLoad;
            _deathTimes.Add(time);
            Debug.Log($"[RunBot] death {_deathTimes.Count} at {FormatTime(time)}", this);

            Health health = GetHealth();
            if (health != null)
            {
                health.Revive();
                health.SetInvulnerable(_reviveInvulnerability);
            }
            if (_director != null) _director.KillWithin(_player.transform.position, _shockwaveRadius);
        }

        private void LogSummary()
        {
            var text = new StringBuilder(64);
            text.Append("[RunBot] run end at ").Append(FormatTime(_director.SpawnEndTime)).Append(", deaths: ");
            if (_deathTimes.Count == 0) text.Append("none");
            for (int i = 0; i < _deathTimes.Count; i++)
            {
                if (i > 0) text.Append(", ");
                text.Append(FormatTime(_deathTimes[i]));
            }
            Debug.Log(text.ToString(), this);
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
