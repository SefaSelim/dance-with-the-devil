using System;
using System.Collections.Generic;
using UnityEngine;

namespace DanceWithTheDevil
{
    /// <summary>
    /// Clock-based wave director (GDD §10, K-6). 30 s windows each carry a budget per enemy type and an active cap.
    /// Pacing (F10, F34): a window's budget is cut into packs of 2-4 enemies (a bigger cap gives bigger packs) whose
    /// release times are spread evenly over the window. A pack comes out of one entrance, its members a quarter
    /// second apart, so the player meets small groups (rocket targets) instead of a lone trickle. While the active cap
    /// has room, packs due within the next few seconds come early, so the cap meters a steady stream; if nothing is
    /// alive or warning for a couple of seconds the next pack comes at once, so the arena is never empty for more
    /// than ~3.5 s while the run is on.
    /// Each release waits for room under the cap (warnings count), picks an entrance at least 9 m from the player,
    /// shows that entrance's warning for 0.8 s, then spawns (GDD §6 Doğuş güvenliği); no qualifying entrance
    /// postpones it. Shooters use at most 2 different entrances within 3 s (GDD §6, §8).
    /// After the authored schedule (5:00) windows keep coming and keep growing until the player dies (K-15; death is
    /// guaranteed, K-4). The clock runs on scaled time, so it stops while Time.timeScale is 0 (contract screen,
    /// hitstop); <see cref="Paused"/> stops it explicitly.
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
            public int Count => Chasers + Runners + Shooters;
        }

        private struct PendingSpawn
        {
            public EnemyType Type;
            public float ReleaseTime;
            public int Pack;
        }

        private struct WarningSpawn
        {
            public EnemyType Type;
            public int Entrance;
            public float SpawnTime;
            public int Pack;
        }

        private struct ShooterUse
        {
            public int Entrance;
            public float Time;
        }

        private struct PackDoor
        {
            public int Pack;
            public int Entrance;
            public float Time;
        }

        private const float PackDoorMemory = 8f;

        private static readonly Comparison<PendingSpawn> ByReleaseTime = (a, b) =>
        {
            int order = a.ReleaseTime.CompareTo(b.ReleaseTime);
            return order != 0 ? order : a.Pack.CompareTo(b.Pack);
        };

        private static readonly Comparison<WaveWindow> ByStartTime = (a, b) => a.StartTime.CompareTo(b.StartTime);

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
        [Tooltip("Length of one budget window, seconds. Windows past the authored schedule follow each other at this step.")]
        [SerializeField, Min(1f)] private float _windowLength = 30f;
        [Tooltip("Shooters may use at most Max Shooter Entrances different entrances within this many seconds.")]
        [SerializeField, Min(0f)] private float _shooterSideWindow = 3f;
        [SerializeField, Min(1)] private int _maxShooterEntrances = 2;
        [Tooltip("Random sideways offset of a spawn along the entrance, metres (avoids exact overlaps).")]
        [SerializeField, Min(0f)] private float _spawnJitter = 0.75f;

        [Header("Pacing (F10, F34)")]
        [Tooltip("A window's packs are released evenly over this many seconds from its start.")]
        [SerializeField, Min(0f)] private float _windowSpread = 18f;
        [Tooltip("Pack size = the window's active cap × this, rounded, then clamped to Min/Max Pack Size.")]
        [SerializeField, Min(0f)] private float _packSizePerCap = 0.4f;
        [SerializeField, Min(1)] private int _minPackSize = 2;
        [SerializeField, Min(1)] private int _maxPackSize = 4;
        [Tooltip("Members of one pack leave their entrance this many seconds apart.")]
        [SerializeField, Min(0f)] private float _packStagger = 0.25f;
        [Tooltip("While the active cap has room, packs due within this many seconds are released early (steady stream).")]
        [SerializeField, Min(0f)] private float _pullAhead = 6f;
        [Tooltip("If nothing is alive or warning for this long, the next pack is released at once " +
                 "(the arena is then empty for at most this + the warning time).")]
        [SerializeField, Min(0f)] private float _maxEmptyTime = 2.5f;

        [Header("After the schedule (K-15)")]
        [Tooltip("Added to the last authored window for every window past it (Start Time is ignored).")]
        [SerializeField] private WaveWindow _lateGrowth = new WaveWindow(0f, 1, 1, 1, 1);
        [Tooltip("The active cap never goes above this.")]
        [SerializeField, Min(1)] private int _maxActiveCap = 16;

        // GDD §10 budget, retuned 2026-10-24 (F2, F10, F26, owner decisions D2/K-15 and D4/K-17). Imp and brute are
        // cut (K-11), so the windows are written directly in Chasers / Runners / Shooters.
        // TODO(GDD §10): the main session copies this table into the §10 "Önerilen pencere bütçesi" table.
        //
        // Rough arithmetic behind the numbers (calibrate with RunBot and a human keyboard-and-mouse playtest):
        // - Expected damage one spawned enemy deals to a moving, dodging average player before it dies
        //   = attack attempts × hit chance × damage: Chaser 0.8 × 0.45 × 10 = 3.6, Runner 0.6 × 0.45 × 8 = 2.2,
        //   Shooter 1.7 × 0.40 × 12 = 8.2 (per-window "dmg" below = C × 3.6 + R × 2.2 + S × 8.2).
        // - The player's damage grows with the contracts: attempts per enemy × 0.85 after contract 1 (rocket ×2),
        //   × 0.70 after contract 3 (pierce) but × 1.1 for enemy speed +15 %, × 0.62 after contract 4, × 0.55 after
        //   contract 5. Max HP is 100, 100, 80, 80, 60, 60 per life; each revive costs ~4 s of no damage
        //   (2 s invulnerability + the refill walk), ~2.5 s once contract 5 removes the invulnerability.
        // - Life 1: W0 43 + W1 68 dmg → 100 HP gone at ~0:55 (target 0:50).
        //   Life 2 (100 HP, × 0.85): W1-W3 → ~1:45 (target 1:40).
        //   Life 3 (80 HP, × 0.85): W3-W4 → ~2:24 (target 2:30).
        //   Life 4 (80 HP, × 0.70 × 1.1): W4-W6 → ~3:05 (target 3:15).
        //   Life 5 (60 HP, × 0.62 × 1.1): W6-W7 → ~3:39 (target 4:00).
        //   Life 6 (60 HP, × 0.55 × 1.1, no revive invulnerability): W7-W8 → ~4:13 (target 4:40).
        //   Life 7 (the ending, A-4): ~4:45.
        // - K-5 ("3:30'dan sonra adet düşer") was replaced by the owner's D4 decision (K-17, "3:00'ten sonra oyun
        //   kolaylaşmaz"): the counts no longer drop after 3:00 but creep up (+1-3 TP a window). The lower max HP
        //   and the stronger player already keep deaths ~35-45 s apart, so a steep rise would bunch the late deaths.
        // - Past 5:00 (K-15) each window adds Late Growth (+1 C, +1 R, +1 S = +4 TP, cap +1 up to 16): with 60 HP and
        //   no revive invulnerability a 4:30-level wave kills in ~29 s and a 6:00-level wave in ~20 s.
        // Pack size = round(cap × 0.4) clamped 2-4: cap 4-6 → 2, 7-8 → 3, 9-11 → 4. Packs per window and spacing are
        // noted per line (26 s spread).
        [Header("Schedule (GDD §10; K-17: counts keep rising after 3:00)")]
        [SerializeField] private List<WaveWindow> _schedule = new List<WaveWindow>
        {
            new WaveWindow(0f, 18, 0, 0, 6),    // ×1.5 counts, cap +2 after three bot runs (2026-10-24: ×1.35 → first death 1:47; ×1.75 → 0:33 and 1:14; this sits between): 12 TP, dmg 43,  6 packs of 2 every 4.3 s (was 8 C, cap 3)
            new WaveWindow(30f, 21, 12, 0, 8),   // ×1.5 counts, cap +2 after three bot runs (2026-10-24: ×1.35 → first death 1:47; ×1.75 → 0:33 and 1:14; this sits between): 22 TP, dmg 68, 11 packs of 2 every 2.4 s (was 14 TP, cap 5)
            new WaveWindow(60f, 17, 8, 4, 9),   // ×1.5 counts, cap +2 after three bot runs (2026-10-24: ×1.35 → first death 1:47; ×1.75 → 0:33 and 1:14; this sits between): 23 TP, dmg 77,  7 packs of 3 every 3.7 s; first shooters
            new WaveWindow(90f, 17, 8, 4, 10),   // ×1.5 counts, cap +2 after three bot runs (2026-10-24: ×1.35 → first death 1:47; ×1.75 → 0:33 and 1:14; this sits between): 23 TP, dmg 77,  7 packs of 3
            new WaveWindow(120f, 15, 8, 5, 10),  // ×1.5 counts, cap +2 after three bot runs (2026-10-24: ×1.35 → first death 1:47; ×1.75 → 0:33 and 1:14; this sits between): 24 TP, dmg 82,  7 packs of 3
            new WaveWindow(150f, 15, 10, 5, 11),  // ×1.5 counts, cap +2 after three bot runs (2026-10-24: ×1.35 → first death 1:47; ×1.75 → 0:33 and 1:14; this sits between): 25 TP, dmg 84,  6 packs of 4 every 4.3 s
            new WaveWindow(180f, 17, 10, 5, 11),  // ×1.5 counts, cap +2 after three bot runs (2026-10-24: ×1.35 → first death 1:47; ×1.75 → 0:33 and 1:14; this sits between): 26 TP, dmg 87,  6 packs of 4
            new WaveWindow(210f, 17, 12, 5, 12), // ×1.5 counts, cap +2 after three bot runs (2026-10-24: ×1.35 → first death 1:47; ×1.75 → 0:33 and 1:14; this sits between): 27 TP, dmg 90,  6 packs of 4 (K-5 had 20 TP here; K-17 keeps rising)
            new WaveWindow(240f, 18, 12, 8, 12), // ×1.5 counts, cap +2 after three bot runs (2026-10-24: ×1.35 → first death 1:47; ×1.75 → 0:33 and 1:14; this sits between): 30 TP, dmg 101, 7 packs of 4
            new WaveWindow(270f, 18, 14, 8, 13), // ×1.5 counts, cap +2 after three bot runs (2026-10-24: ×1.35 → first death 1:47; ×1.75 → 0:33 and 1:14; this sits between): 31 TP, dmg 103, 7 packs of 4; then Late Growth every 30 s
        };

        private readonly List<Enemy> _active = new List<Enemy>(32);
        private readonly List<Enemy> _scratch = new List<Enemy>(32);
        private readonly List<PendingSpawn> _pending = new List<PendingSpawn>(128);
        private readonly List<WarningSpawn> _warningSpawns = new List<WarningSpawn>(16);
        private readonly List<ShooterUse> _shooterUses = new List<ShooterUse>(8);
        private readonly List<PackDoor> _packDoors = new List<PackDoor>(16);
        private int[] _candidates = new int[4];
        private Action<Enemy> _onEnemyDied;

        private float _elapsed;
        private bool _begun;
        private bool _spawning;
        private int _openWindow = -1;
        private int _queuedWindows;
        private int _nextPack;
        private int _activeCap = 1;
        private float _emptySince = -1f;

        /// <summary>Raised when a spawned enemy appears.</summary>
        public event Action<Enemy> EnemySpawned;
        /// <summary>Raised when a spawned enemy dies (any cause).</summary>
        public event Action<Enemy> EnemyKilled;
        /// <summary>Raised when an entrance's spawn warning comes on (once per pack), with the entrance position (audio).</summary>
        public event Action<Vector3> SpawnWarningStarted;

        /// <summary>Seconds of run clock since <see cref="Begin"/>.</summary>
        public float Elapsed => _elapsed;
        /// <summary>Living spawned enemies.</summary>
        public int ActiveCount => _active.Count;
        /// <summary>Spawns showing their warning right now (they count toward the cap).</summary>
        public int WarningCount => _warningSpawns.Count;
        /// <summary>Queued spawns not yet warning.</summary>
        public int PendingCount => _pending.Count;
        /// <summary>The current window's active cap.</summary>
        public int ActiveCap => _activeCap;
        /// <summary>Index of the window in force (past the authored schedule it keeps counting); -1 before <see cref="Begin"/>.</summary>
        public int WindowIndex => _openWindow;
        /// <summary>The window in force (an extrapolated one past the authored schedule).</summary>
        public WaveWindow CurrentWindow => WindowAt(Mathf.Max(0, _openWindow));
        /// <summary>True from <see cref="Begin"/> until <see cref="StopSpawning"/> (spawning no longer ends at 5:00, K-15).</summary>
        public bool IsRunning => _begun && _spawning;
        /// <summary>True once <see cref="Begin"/> was called (the clock runs).</summary>
        public bool HasBegun => _begun;
        /// <summary>
        /// End of the authored schedule (5:00 with the default table). Spawning goes on and grows past it (K-15);
        /// kept for the HUD and the bot.
        /// </summary>
        public float SpawnEndTime => ScheduleEnd();
        public float MinSpawnDistance => _minSpawnDistance;
        /// <summary>Stops the clock (and spawning) while true, e.g. on the contract screen.</summary>
        public bool Paused { get; set; }
        public Transform Player => _player;
        public IReadOnlyList<Enemy> ActiveEnemies => _active;
        /// <summary>The authored windows (the schedule up to 5:00).</summary>
        public IReadOnlyList<WaveWindow> Windows => _schedule;

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
            if (windows != null) _schedule = windows;
            if (beginOnStart.HasValue) _beginOnStart = beginOnStart.Value;
        }

        /// <summary>Starts (or restarts) the clock at 0:00 with an empty queue.</summary>
        public void Begin()
        {
            _schedule.Sort(ByStartTime);
            _elapsed = 0f;
            _openWindow = -1;
            _queuedWindows = 0;
            _nextPack = 0;
            _emptySince = -1f;
            _activeCap = Mathf.Clamp(WindowAt(0).ActiveCap, 1, _maxActiveCap);
            _pending.Clear();
            CancelWarnings();
            _shooterUses.Clear();
            _packDoors.Clear();
            _begun = true;
            _spawning = true;
            if (_candidates.Length < _spawnPoints.Length) _candidates = new int[_spawnPoints.Length];
            OpenWindows();
        }

        /// <summary>No more spawns: clears the queue and cancels warnings. Living enemies stay.</summary>
        public void StopSpawning()
        {
            _spawning = false;
            _pending.Clear();
            _packDoors.Clear();
            CancelWarnings();
        }

        /// <summary>
        /// Kills every living spawned enemy and cancels every spawn still showing its entrance warning, so nothing
        /// steps out right after (the Devil's revive shockwave clears the whole arena, D3 / K-16).
        /// The queue is kept: after a revive the next packs warn at once and arrive ≥ 9 m away 0.8 s later.
        /// </summary>
        // TODO(open, F19): a short no-spawn grace after the revive, or clearing shooter orbs still in flight, would be
        // new rules and need Burak's decision; neither is done here.
        public void KillAll() => KillWithin(Vector3.zero, float.PositiveInfinity);

        /// <summary>
        /// Kills every living spawned enemy whose flat distance to <paramref name="center"/> is within
        /// <paramref name="radius"/>, with an Explosion hit pushing outward, and cancels the spawn warnings at entrances
        /// within the radius (infinite radius: all of them). Returns the number of enemies killed.
        /// </summary>
        public int KillWithin(Vector3 center, float radius)
        {
            bool everywhere = float.IsPositiveInfinity(radius);
            float radiusSqr = everywhere ? float.PositiveInfinity : radius * radius;

            for (int i = _warningSpawns.Count - 1; i >= 0; i--)
            {
                int entrance = _warningSpawns[i].Entrance;
                if (!everywhere)
                {
                    Transform point = entrance >= 0 && entrance < _spawnPoints.Length ? _spawnPoints[entrance] : null;
                    if (point == null || FlatDistanceSqr(point.position, center) > radiusSqr) continue;
                }
                _warningSpawns.RemoveAt(i);
                RefreshWarning(entrance);
            }

            _scratch.Clear();
            _scratch.AddRange(_active);
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

            OpenWindows();
            PrunePackDoors();
            FinishWarnings();
            KeepArenaBusy();
            ReleasePending();
        }

        /// <summary>
        /// Window <paramref name="index"/>: an authored one, or past the schedule the last authored window plus
        /// Late Growth per extra window (K-15), the cap clamped to Max Active Cap.
        /// </summary>
        private WaveWindow WindowAt(int index)
        {
            int count = _schedule.Count;
            if (index < count) return _schedule[Mathf.Max(0, index)];

            WaveWindow last = count > 0 ? _schedule[count - 1] : new WaveWindow(-_windowLength, 0, 0, 0, 1);
            int steps = index - (count - 1);
            return new WaveWindow(
                last.StartTime + steps * _windowLength,
                last.Chasers + _lateGrowth.Chasers * steps,
                last.Runners + _lateGrowth.Runners * steps,
                last.Shooters + _lateGrowth.Shooters * steps,
                Mathf.Min(last.ActiveCap + _lateGrowth.ActiveCap * steps, _maxActiveCap));
        }

        private float ScheduleEnd()
        {
            float last = 0f;
            for (int i = 0; i < _schedule.Count; i++)
                if (_schedule[i].StartTime > last) last = _schedule[i].StartTime;
            return _schedule.Count > 0 ? last + _windowLength : 0f;
        }

        /// <summary>Opens every window whose start has passed (its cap applies) and keeps the next window queued too.</summary>
        private void OpenWindows()
        {
            while (WindowAt(_openWindow + 1).StartTime <= _elapsed)
            {
                _openWindow++;
                _activeCap = Mathf.Clamp(WindowAt(_openWindow).ActiveCap, 1, _maxActiveCap);
            }
            // The window in force and the next one are always queued, so early releases have packs to take.
            while (_queuedWindows <= _openWindow + 1) QueueNextWindow();
        }

        /// <summary>
        /// Cuts the next unqueued window into packs (types spread evenly through it) with release times spread
        /// evenly over the window, and queues them.
        /// </summary>
        private void QueueNextWindow()
        {
            WaveWindow window = WindowAt(_queuedWindows);
            _queuedWindows++;
            int total = window.Count;
            if (total <= 0) return;

            int packSize = PackSizeFor(Mathf.Clamp(window.ActiveCap, 1, _maxActiveCap));
            int packs = (total + packSize - 1) / packSize;
            int chasers = window.Chasers;
            int runners = window.Runners;
            int shooters = window.Shooters;
            for (int p = 0; p < packs; p++)
            {
                int size = total / packs + (p < total % packs ? 1 : 0);
                float release = window.StartTime + _windowSpread * p / packs;
                int pack = _nextPack++;
                for (int m = 0; m < size; m++)
                {
                    EnemyType type = NextType(window, ref chasers, ref runners, ref shooters);
                    _pending.Add(new PendingSpawn { Type = type, ReleaseTime = release, Pack = pack });
                }
            }
            // Once per window: keep the queue ordered by release time (late spawns of earlier windows stay in front).
            _pending.Sort(ByReleaseTime);
        }

        private int PackSizeFor(int cap)
        {
            int max = Mathf.Max(_minPackSize, _maxPackSize);
            return Mathf.Clamp(Mathf.RoundToInt(cap * _packSizePerCap), _minPackSize, max);
        }

        /// <summary>The type with the largest share of its budget still unplaced, so every type is spread through the window.</summary>
        private static EnemyType NextType(in WaveWindow window, ref int chasers, ref int runners, ref int shooters)
        {
            float c = window.Chasers > 0 ? (float)chasers / window.Chasers : -1f;
            float r = window.Runners > 0 ? (float)runners / window.Runners : -1f;
            float s = window.Shooters > 0 ? (float)shooters / window.Shooters : -1f;
            if (chasers > 0 && c >= r && c >= s)
            {
                chasers--;
                return EnemyType.Chaser;
            }
            if (runners > 0 && r >= s)
            {
                runners--;
                return EnemyType.Runner;
            }
            if (shooters > 0)
            {
                shooters--;
                return EnemyType.Shooter;
            }
            if (runners > 0)
            {
                runners--;
                return EnemyType.Runner;
            }
            chasers--;
            return EnemyType.Chaser;
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
                    _pending.Insert(0, new PendingSpawn { Type = warning.Type, ReleaseTime = _elapsed, Pack = warning.Pack });
                }
                else
                {
                    SpawnAt(warning.Type, warning.Entrance);
                }
                RefreshWarning(warning.Entrance);
            }
        }

        /// <summary>
        /// Never leave the arena empty for long (F10): after Max Empty Time with nothing alive or warning, the next
        /// queued pack is released now (queueing the next window first if the player ran through the queue).
        /// </summary>
        private void KeepArenaBusy()
        {
            if (_active.Count + _warningSpawns.Count > 0)
            {
                _emptySince = -1f;
                return;
            }
            if (_emptySince < 0f)
            {
                _emptySince = _elapsed;
                return;
            }
            if (_elapsed - _emptySince < _maxEmptyTime) return;
            _emptySince = _elapsed; // at most one pull per Max Empty Time

            if (_pending.Count == 0) QueueNextWindow();
            if (_pending.Count == 0 || _pending[0].ReleaseTime <= _elapsed) return; // nothing to bring forward

            int pack = _pending[0].Pack;
            for (int i = 0; i < _pending.Count; i++)
            {
                if (_pending[i].Pack != pack) continue;
                PendingSpawn spawn = _pending[i];
                spawn.ReleaseTime = _elapsed;
                _pending[i] = spawn;
            }
            _pending.Sort(ByReleaseTime);
        }

        private void ReleasePending()
        {
            int room = _activeCap - (_active.Count + _warningSpawns.Count);
            if (room <= 0 || _pending.Count == 0) return; // cap full: wait

            float horizon = _elapsed + _pullAhead;
            Vector3 playerPosition = _player != null ? _player.position : Vector3.zero;
            for (int i = 0; i < _pending.Count && room > 0; i++)
            {
                PendingSpawn spawn = _pending[i];
                if (spawn.ReleaseTime > horizon) break; // sorted: nothing later is due

                int entrance = EntranceFor(spawn, playerPosition);
                if (entrance < 0) continue; // postponed; a different type may still find an entrance

                _pending.RemoveAt(i);
                i--;
                StartWarning(spawn.Type, entrance, spawn.Pack);
                room--;
            }
        }

        /// <summary>The pack's entrance if one member already took it and it still qualifies, else a fresh pick.</summary>
        private int EntranceFor(in PendingSpawn spawn, Vector3 playerPosition)
        {
            for (int i = 0; i < _packDoors.Count; i++)
            {
                if (_packDoors[i].Pack != spawn.Pack) continue;
                int door = _packDoors[i].Entrance;
                if (IsEntranceUsable(door, playerPosition, spawn.Type)) return door;
                break;
            }
            return PickEntrance(playerPosition, spawn.Type);
        }

        private bool IsEntranceUsable(int entrance, Vector3 playerPosition, EnemyType type)
        {
            if (entrance < 0 || entrance >= _spawnPoints.Length || _spawnPoints[entrance] == null) return false;
            if (FlatDistanceSqr(_spawnPoints[entrance].position, playerPosition) < _minSpawnDistance * _minSpawnDistance)
                return false;
            if (type != EnemyType.Shooter) return true;
            PruneShooterUses();
            return ShooterMayUse(entrance);
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

        private void PrunePackDoors()
        {
            for (int i = _packDoors.Count - 1; i >= 0; i--)
                if (_elapsed - _packDoors[i].Time > PackDoorMemory) _packDoors.RemoveAt(i);
        }

        private void RememberPackDoor(int pack, int entrance)
        {
            for (int i = 0; i < _packDoors.Count; i++)
            {
                if (_packDoors[i].Pack != pack) continue;
                _packDoors[i] = new PackDoor { Pack = pack, Entrance = entrance, Time = _elapsed };
                return;
            }
            _packDoors.Add(new PackDoor { Pack = pack, Entrance = entrance, Time = _elapsed });
        }

        /// <summary>Starts a spawn's warning; pack members at the same entrance come out Pack Stagger apart.</summary>
        private void StartWarning(EnemyType type, int entrance, int pack)
        {
            if (type == EnemyType.Shooter) _shooterUses.Add(new ShooterUse { Entrance = entrance, Time = _elapsed });

            bool plateWasOn = HasWarning(entrance);
            float spawnTime = _elapsed + _warningTime;
            for (int i = 0; i < _warningSpawns.Count; i++)
                if (_warningSpawns[i].Entrance == entrance)
                    spawnTime = Mathf.Max(spawnTime, _warningSpawns[i].SpawnTime + _packStagger);

            _warningSpawns.Add(new WarningSpawn { Type = type, Entrance = entrance, SpawnTime = spawnTime, Pack = pack });
            RememberPackDoor(pack, entrance);
            RefreshWarning(entrance);
            if (!plateWasOn)
            {
                Transform point = entrance >= 0 && entrance < _spawnPoints.Length ? _spawnPoints[entrance] : null;
                SpawnWarningStarted?.Invoke(point != null ? point.position : transform.position);
            }
            if (spawnTime <= _elapsed) FinishWarnings();
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
