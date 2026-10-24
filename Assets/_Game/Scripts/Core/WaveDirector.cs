using System;
using System.Collections.Generic;
using UnityEngine;

namespace DanceWithTheDevil
{
    /// <summary>
    /// Clock-based wave director (GDD §10, K-6). 30 s windows each carry a budget per enemy type and an active cap.
    /// At a window's start its budget is split into 3 sub-groups released at +0 s, +4 s and +8 s. Each release waits
    /// for room under the active cap (late spawns are not dumped at once), picks an entrance at least 9 m from the
    /// player, shows that entrance's warning for 0.8 s, then spawns (GDD §6 Doğuş güvenliği). If no entrance qualifies
    /// the spawn is postponed. Shooters use at most 2 different entrances within 3 s, so they never cover every
    /// direction at once (GDD §6, §8). No new spawns from 5:00. The clock runs on scaled time, so it stops while
    /// Time.timeScale is 0 (contract screen, hitstop); <see cref="Paused"/> stops it explicitly.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class WaveDirector : MonoBehaviour
    {
        /// <summary>One 30 s budget window (GDD §10).</summary>
        [Serializable]
        public struct WaveWindow
        {
            [Tooltip("Window start, seconds from Begin.")]
            [Min(0f)] public float StartTime;
            [Min(0)] public int Chasers;
            [Min(0)] public int Runners;
            [Min(0)] public int Shooters;
            [Tooltip("Most enemies alive (plus warnings in progress) at once during this window.")]
            [Min(1)] public int ActiveCap;

            public WaveWindow(float startTime, int chasers, int runners, int shooters, int activeCap)
            {
                StartTime = startTime;
                Chasers = chasers;
                Runners = runners;
                Shooters = shooters;
                ActiveCap = activeCap;
            }

            public int ThreatPoints => Chasers + Runners + Shooters * 2;
        }

        private struct PendingSpawn
        {
            public EnemyType Type;
            public float ReleaseTime;
        }

        private struct WarningSpawn
        {
            public EnemyType Type;
            public int Entrance;
            public float SpawnTime;
        }

        private struct ShooterUse
        {
            public int Entrance;
            public float Time;
        }

        [Header("References")]
        [Tooltip("Player root (enemy target; spawn distance is measured from it).")]
        [SerializeField] private Transform _player;
        [Tooltip("Entrance spawn points (GDD §9: four edge entrances). Must be on the NavMesh.")]
        [SerializeField] private Transform[] _spawnPoints = Array.Empty<Transform>();
        [Tooltip("Per-entrance warning object (same order as Spawn Points), shown for the warning time. Entries may be empty.")]
        [SerializeField] private GameObject[] _warnings = Array.Empty<GameObject>();
        [SerializeField] private EnemyPool _chaserPool;
        [SerializeField] private EnemyPool _runnerPool;
        [SerializeField] private EnemyPool _shooterPool;

        [Header("Rules (GDD §6, §10)")]
        [Tooltip("Start the clock in Start. Off: call Begin().")]
        [SerializeField] private bool _beginOnStart = true;
        [Tooltip("Minimum flat distance from the player to an entrance at warning start and at spawn, metres.")]
        [SerializeField, Min(0f)] private float _minSpawnDistance = 9f;
        [Tooltip("Warning shown before a spawn, seconds.")]
        [SerializeField, Min(0f)] private float _warningTime = 0.8f;
        [Tooltip("Sub-group release offsets inside a window, seconds.")]
        [SerializeField] private float[] _subGroupOffsets = { 0f, 4f, 8f };
        [Tooltip("No new spawns from this time, seconds (5:00).")]
        [SerializeField, Min(0f)] private float _spawnEndTime = 300f;
        [Tooltip("Shooters may use at most Max Shooter Entrances different entrances within this many seconds.")]
        [SerializeField, Min(0f)] private float _shooterSideWindow = 3f;
        [SerializeField, Min(1)] private int _maxShooterEntrances = 2;
        [Tooltip("Random sideways offset of a spawn along the entrance, metres (avoids exact overlaps).")]
        [SerializeField, Min(0f)] private float _spawnJitter = 0.75f;

        // GDD §10 suggested budget. Imp and brute are cut for the minimum scope (K-11), keeping each window's threat
        // points: each imp (TP 2) became 1 Chaser + 1 Runner, each brute (TP 4) became 2 Shooters.
        [Header("Windows (GDD §10, imp → Chaser + Runner, brute → 2 Shooters)")]
        [SerializeField] private List<WaveWindow> _windows = new List<WaveWindow>
        {
            new WaveWindow(0f, 8, 0, 0, 3),     // 8 TP
            new WaveWindow(30f, 10, 4, 0, 5),   // 14 TP
            new WaveWindow(60f, 8, 4, 3, 6),    // 18 TP
            new WaveWindow(90f, 9, 6, 3, 7),    // 7C 4R 3S + 2 imp = 21 TP
            new WaveWindow(120f, 9, 7, 5, 8),   // 6C 4R 3S + 3 imp + 1 brute = 25 TP
            new WaveWindow(150f, 9, 8, 6, 8),   // 6C 5R 4S + 3 imp + 1 brute = 29 TP
            new WaveWindow(180f, 9, 8, 6, 9),   // 6C 5R 4S + 3 imp + 1 brute = 29 TP
            new WaveWindow(210f, 6, 5, 5, 9),   // 4C 3R 3S + 2 imp + 1 brute = 20 TP (K-5: fewer, heavier)
            new WaveWindow(240f, 7, 6, 5, 9),   // 4C 3R 3S + 3 imp + 1 brute = 22 TP
            new WaveWindow(270f, 7, 7, 5, 9),   // 4C 4R 3S + 3 imp + 1 brute = 23 TP
        };

        private readonly List<Enemy> _active = new List<Enemy>(32);
        private readonly List<Enemy> _scratch = new List<Enemy>(32);
        private readonly List<PendingSpawn> _pending = new List<PendingSpawn>(64);
        private readonly List<WarningSpawn> _warningSpawns = new List<WarningSpawn>(8);
        private readonly List<ShooterUse> _shooterUses = new List<ShooterUse>(8);
        private int[] _candidates = new int[4];
        private Action<Enemy> _onEnemyDied;

        private float _elapsed;
        private bool _begun;
        private bool _spawning;
        private int _nextWindow;
        private int _activeCap = 1;

        /// <summary>Raised when a spawned enemy appears.</summary>
        public event Action<Enemy> EnemySpawned;
        /// <summary>Raised when a spawned enemy dies (any cause).</summary>
        public event Action<Enemy> EnemyKilled;
        /// <summary>Raised when an entrance's spawn warning starts, with the entrance position (audio).</summary>
        public event Action<Vector3> SpawnWarningStarted;

        /// <summary>Seconds of run clock since <see cref="Begin"/>.</summary>
        public float Elapsed => _elapsed;
        /// <summary>Living spawned enemies.</summary>
        public int ActiveCount => _active.Count;
        /// <summary>Spawns showing their warning right now (they count toward the cap).</summary>
        public int WarningCount => _warningSpawns.Count;
        /// <summary>Released or scheduled spawns not yet warning.</summary>
        public int PendingCount => _pending.Count;
        /// <summary>The current window's active cap.</summary>
        public int ActiveCap => _activeCap;
        /// <summary>True from <see cref="Begin"/> until 5:00 or <see cref="StopSpawning"/>.</summary>
        public bool IsRunning => _begun && _spawning;
        /// <summary>True once <see cref="Begin"/> was called (the clock runs).</summary>
        public bool HasBegun => _begun;
        public float SpawnEndTime => _spawnEndTime;
        public float MinSpawnDistance => _minSpawnDistance;
        /// <summary>Stops the clock (and spawning) while true, e.g. on the contract screen.</summary>
        public bool Paused { get; set; }
        public Transform Player => _player;
        public IReadOnlyList<Enemy> ActiveEnemies => _active;
        public IReadOnlyList<WaveWindow> Windows => _windows;

        private void Awake()
        {
            _onEnemyDied = OnEnemyDied;
        }

        private void Start()
        {
            if (_beginOnStart && !_begun) Begin();
        }

        /// <summary>Code-built setups (tests, builders). Call before <see cref="Begin"/>. Null arguments keep the current value.</summary>
        public void Configure(Transform player, Transform[] spawnPoints, GameObject[] warnings,
            EnemyPool chaserPool, EnemyPool runnerPool, EnemyPool shooterPool, List<WaveWindow> windows = null,
            bool? beginOnStart = null)
        {
            if (player != null) _player = player;
            if (spawnPoints != null) _spawnPoints = spawnPoints;
            if (warnings != null) _warnings = warnings;
            if (chaserPool != null) _chaserPool = chaserPool;
            if (runnerPool != null) _runnerPool = runnerPool;
            if (shooterPool != null) _shooterPool = shooterPool;
            if (windows != null) _windows = windows;
            if (beginOnStart.HasValue) _beginOnStart = beginOnStart.Value;
        }

        /// <summary>Starts (or restarts) the clock at 0:00 with an empty queue.</summary>
        public void Begin()
        {
            _windows.Sort((a, b) => a.StartTime.CompareTo(b.StartTime));
            _elapsed = 0f;
            _nextWindow = 0;
            _activeCap = _windows.Count > 0 ? Mathf.Max(1, _windows[0].ActiveCap) : 1;
            _pending.Clear();
            CancelWarnings();
            _shooterUses.Clear();
            _begun = true;
            _spawning = true;
            if (_candidates.Length < _spawnPoints.Length) _candidates = new int[_spawnPoints.Length];
        }

        /// <summary>No more spawns: clears the queue and cancels warnings. Living enemies stay.</summary>
        public void StopSpawning()
        {
            _spawning = false;
            _pending.Clear();
            CancelWarnings();
        }

        /// <summary>Kills every living spawned enemy.</summary>
        public void KillAll() => KillWithin(Vector3.zero, float.PositiveInfinity);

        /// <summary>
        /// Kills every living spawned enemy whose flat distance to <paramref name="center"/> is within
        /// <paramref name="radius"/> (Devil's revive shockwave, K-12), with an Explosion hit pushing outward.
        /// Returns the number killed.
        /// </summary>
        public int KillWithin(Vector3 center, float radius)
        {
            _scratch.Clear();
            _scratch.AddRange(_active);
            float radiusSqr = float.IsPositiveInfinity(radius) ? float.PositiveInfinity : radius * radius;
            int killed = 0;
            for (int i = 0; i < _scratch.Count; i++)
            {
                Enemy enemy = _scratch[i];
                if (enemy == null || !enemy.IsAlive) continue;
                Vector3 offset = enemy.transform.position - center;
                offset.y = 0f;
                if (offset.sqrMagnitude > radiusSqr) continue;

                Vector3 direction = offset.sqrMagnitude > 1e-6f ? offset.normalized : Vector3.zero;
                enemy.Kill(new DamageInfo(0f, enemy.transform.position + Vector3.up, direction, gameObject,
                    DamageKind.Explosion));
                killed++;
            }
            _scratch.Clear();
            return killed;
        }

        private void Update()
        {
            if (!_begun || Paused) return;
            _elapsed += Time.deltaTime;
            if (!_spawning) return;

            if (_elapsed >= _spawnEndTime)
            {
                StopSpawning();
                return;
            }

            OpenWindows();
            FinishWarnings();
            ReleasePending();
        }

        private void OpenWindows()
        {
            while (_nextWindow < _windows.Count && _elapsed >= _windows[_nextWindow].StartTime)
            {
                WaveWindow window = _windows[_nextWindow];
                _activeCap = Mathf.Max(1, window.ActiveCap);
                EnqueueWindow(window);
                _nextWindow++;
            }
        }

        /// <summary>Splits the window budget into sub-groups; types are interleaved inside each group.</summary>
        private void EnqueueWindow(in WaveWindow window)
        {
            int groups = _subGroupOffsets != null && _subGroupOffsets.Length > 0 ? _subGroupOffsets.Length : 1;
            for (int g = 0; g < groups; g++)
            {
                float offset = _subGroupOffsets != null && _subGroupOffsets.Length > 0 ? _subGroupOffsets[g] : 0f;
                float release = window.StartTime + offset;
                int chasers = Share(window.Chasers, g, groups);
                int runners = Share(window.Runners, g, groups);
                int shooters = Share(window.Shooters, g, groups);
                while (chasers > 0 || runners > 0 || shooters > 0)
                {
                    if (chasers-- > 0) _pending.Add(new PendingSpawn { Type = EnemyType.Chaser, ReleaseTime = release });
                    if (runners-- > 0) _pending.Add(new PendingSpawn { Type = EnemyType.Runner, ReleaseTime = release });
                    if (shooters-- > 0) _pending.Add(new PendingSpawn { Type = EnemyType.Shooter, ReleaseTime = release });
                }
            }
            // Keep the queue ordered by release time (earlier windows' late spawns stay in front).
            _pending.Sort((a, b) => a.ReleaseTime.CompareTo(b.ReleaseTime));
        }

        /// <summary>Group <paramref name="index"/>'s share of <paramref name="total"/>; the remainder goes to earlier groups.</summary>
        private static int Share(int total, int index, int groups)
        {
            int share = total / groups;
            if (index < total % groups) share++;
            return share;
        }

        private void FinishWarnings()
        {
            for (int i = _warningSpawns.Count - 1; i >= 0; i--)
            {
                WarningSpawn warning = _warningSpawns[i];
                if (_elapsed < warning.SpawnTime) continue;
                _warningSpawns.RemoveAt(i);

                if (!IsEntranceSafe(warning.Entrance))
                {
                    // The player walked up to the entrance during the warning: back to the front of the queue.
                    _pending.Insert(0, new PendingSpawn { Type = warning.Type, ReleaseTime = _elapsed });
                }
                else
                {
                    SpawnAt(warning.Type, warning.Entrance);
                }
                RefreshWarning(warning.Entrance);
            }
        }

        private void ReleasePending()
        {
            for (int i = 0; i < _pending.Count; i++)
            {
                PendingSpawn spawn = _pending[i];
                if (spawn.ReleaseTime > _elapsed) break; // sorted: nothing later is due
                if (_active.Count + _warningSpawns.Count >= _activeCap) break; // cap full: wait

                int entrance = PickEntrance(_player != null ? _player.position : Vector3.zero, spawn.Type);
                if (entrance < 0) continue; // postponed; a different type may still find an entrance

                _pending.RemoveAt(i);
                i--;
                StartWarning(spawn.Type, entrance);
            }
        }

        /// <summary>
        /// Chooses a random entrance at least <see cref="MinSpawnDistance"/> (flat) from
        /// <paramref name="playerPosition"/>, preferring entrances without a warning running; shooters also obey the
        /// entrance-spread rule. Returns -1 if none qualifies (the spawn is postponed).
        /// </summary>
        public int PickEntrance(Vector3 playerPosition, EnemyType type)
        {
            if (_spawnPoints == null || _spawnPoints.Length == 0) return -1;
            if (_candidates.Length < _spawnPoints.Length) _candidates = new int[_spawnPoints.Length];
            if (type == EnemyType.Shooter) PruneShooterUses();

            int count = 0;
            int idleCount = 0;
            for (int i = 0; i < _spawnPoints.Length; i++)
            {
                if (_spawnPoints[i] == null) continue;
                if (FlatDistanceSqr(_spawnPoints[i].position, playerPosition) < _minSpawnDistance * _minSpawnDistance) continue;
                if (type == EnemyType.Shooter && !ShooterMayUse(i)) continue;

                // Idle entrances first in the candidate array.
                if (!HasWarning(i))
                {
                    _candidates[count] = _candidates[idleCount];
                    _candidates[idleCount] = i;
                    idleCount++;
                }
                else
                {
                    _candidates[count] = i;
                }
                count++;
            }

            if (count == 0) return -1;
            return idleCount > 0 ? _candidates[UnityEngine.Random.Range(0, idleCount)] : _candidates[UnityEngine.Random.Range(0, count)];
        }

        private bool IsEntranceSafe(int entrance)
        {
            if (_player == null) return true;
            Transform point = _spawnPoints[entrance];
            return point != null && FlatDistanceSqr(point.position, _player.position) >= _minSpawnDistance * _minSpawnDistance;
        }

        private void PruneShooterUses()
        {
            for (int i = _shooterUses.Count - 1; i >= 0; i--)
                if (_elapsed - _shooterUses[i].Time > _shooterSideWindow) _shooterUses.RemoveAt(i);
        }

        /// <summary>True if a shooter may come from <paramref name="entrance"/> without exceeding the entrance spread.</summary>
        private bool ShooterMayUse(int entrance)
        {
            int distinct = 0;
            for (int i = 0; i < _shooterUses.Count; i++)
            {
                int e = _shooterUses[i].Entrance;
                if (e == entrance) return true;
                bool seen = false;
                for (int j = 0; j < i; j++)
                    if (_shooterUses[j].Entrance == e) { seen = true; break; }
                if (!seen) distinct++;
            }
            return distinct < _maxShooterEntrances;
        }

        private void StartWarning(EnemyType type, int entrance)
        {
            if (type == EnemyType.Shooter) _shooterUses.Add(new ShooterUse { Entrance = entrance, Time = _elapsed });
            _warningSpawns.Add(new WarningSpawn { Type = type, Entrance = entrance, SpawnTime = _elapsed + _warningTime });
            RefreshWarning(entrance);
            Transform point = entrance >= 0 && entrance < _spawnPoints.Length ? _spawnPoints[entrance] : null;
            SpawnWarningStarted?.Invoke(point != null ? point.position : transform.position);
            if (_warningTime <= 0f) FinishWarnings();
        }

        private bool HasWarning(int entrance)
        {
            for (int i = 0; i < _warningSpawns.Count; i++)
                if (_warningSpawns[i].Entrance == entrance) return true;
            return false;
        }

        private void RefreshWarning(int entrance)
        {
            if (_warnings == null || entrance < 0 || entrance >= _warnings.Length) return;
            GameObject warning = _warnings[entrance];
            if (warning == null) return;
            bool on = HasWarning(entrance);
            if (warning.activeSelf != on) warning.SetActive(on);
        }

        private void CancelWarnings()
        {
            _warningSpawns.Clear();
            if (_warnings == null) return;
            for (int i = 0; i < _warnings.Length; i++)
                if (_warnings[i] != null && _warnings[i].activeSelf) _warnings[i].SetActive(false);
        }

        private void SpawnAt(EnemyType type, int entrance)
        {
            EnemyPool pool = PoolFor(type);
            Transform point = _spawnPoints[entrance];
            if (pool == null || point == null) return;

            Vector3 position = point.position + point.right * UnityEngine.Random.Range(-_spawnJitter, _spawnJitter);
            Vector3 facing = _player != null ? _player.position - position : point.forward;
            facing.y = 0f;
            Quaternion rotation = facing.sqrMagnitude > 1e-6f ? Quaternion.LookRotation(facing, Vector3.up) : point.rotation;

            Enemy enemy = pool.Get(position, rotation, _player);
            if (enemy == null) return;
            enemy.Died += _onEnemyDied;
            _active.Add(enemy);
            EnemySpawned?.Invoke(enemy);
        }

        private EnemyPool PoolFor(EnemyType type)
        {
            switch (type)
            {
                case EnemyType.Runner: return _runnerPool;
                case EnemyType.Shooter: return _shooterPool;
                default: return _chaserPool;
            }
        }

        private void OnEnemyDied(Enemy enemy)
        {
            enemy.Died -= _onEnemyDied;
            _active.Remove(enemy);
            EnemyKilled?.Invoke(enemy);
        }

        private static float FlatDistanceSqr(Vector3 a, Vector3 b)
        {
            float dx = a.x - b.x;
            float dz = a.z - b.z;
            return dx * dx + dz * dz;
        }
    }
}
