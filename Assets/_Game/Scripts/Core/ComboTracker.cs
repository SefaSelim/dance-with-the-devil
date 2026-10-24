using System;
using System.Collections.Generic;
using UnityEngine;

namespace DanceWithTheDevil
{
    /// <summary>Why a combo ended (the HUD shows the two differently, design note combo-haste §2).</summary>
    public enum ComboBreak
    {
        /// <summary>No counted hit or kill for the whole window while enemies were alive.</summary>
        Timeout,
        /// <summary>Defne lost health.</summary>
        Damage,
    }

    /// <summary>
    /// Combo rules without Unity scene dependencies (testable in EditMode). Design: production/design/combo-haste.md §2.
    /// +1 per damaging hit per enemy, +1 more per kill; window 3.0 s after the last count; the window does not run while
    /// no enemy is alive or while frozen (Devil screen, pause, hitstop); Defne losing health resets to 0.
    /// Multiplier = min(3.0, 1.0 + 0.1 × floor(count / 5)), kept in tenths so 100 × multiplier is an exact integer.
    /// Tiers: 1 at 10 (×1.2), 2 at 25 (×1.5), 3 at 50 (×2.0), 4 at 100 (×3.0).
    /// </summary>
    public sealed class ComboCounter
    {
        public const float DefaultWindow = 3f;
        public const int CountPerStep = 5;
        public const int MaxMultiplierTenths = 30;
        /// <summary>Count at which tier 1..4 starts.</summary>
        public static readonly int[] TierThresholds = { 10, 25, 50, 100 };

        private float _window = DefaultWindow;

        /// <summary>Raised with the new count after every change (each +1 and the drop to 0).</summary>
        public event Action<int> Changed;
        /// <summary>Raised with the new tier (1..4) right after the Changed that crossed it.</summary>
        public event Action<int> TierUp;
        /// <summary>Raised after the count dropped to 0: reason and the count that was lost.</summary>
        public event Action<ComboBreak, int> Broken;

        public ComboCounter(float window = DefaultWindow) => Window = window;

        /// <summary>Seconds without a count before the combo ends (min 0.1).</summary>
        public float Window
        {
            get => _window;
            set => _window = Mathf.Max(0.1f, value);
        }

        /// <summary>Shockwave source (the wave director): its hits and kills never count.</summary>
        public GameObject IgnoredSource { get; set; }
        /// <summary>When set, only damage whose Source is this transform or under it counts (Defne).</summary>
        public Transform CreditedRoot { get; set; }

        public int Count { get; private set; }
        public int Best { get; private set; }
        public float TimeLeft { get; private set; }
        public float TimeLeft01 => Count > 0 ? Mathf.Clamp01(TimeLeft / _window) : 0f;
        /// <summary>True while the window is not draining (no combo, empty arena, frozen).</summary>
        public bool TimerPaused { get; private set; } = true;
        public int MultiplierTenths => TenthsFor(Count);
        public float Multiplier => MultiplierTenths / 10f;
        public int Tier => TierFor(Count);

        public static int TenthsFor(int count) => Mathf.Min(MaxMultiplierTenths, 10 + Mathf.Max(0, count) / CountPerStep);

        public static int TierFor(int count)
        {
            int tier = 0;
            for (int i = 0; i < TierThresholds.Length; i++)
                if (count >= TierThresholds[i]) tier = i + 1;
            return tier;
        }

        /// <summary>Does this damage earn combo? Positive amount, not the shockwave, and from Defne when a root is set.</summary>
        public static bool IsCredited(in DamageInfo info, GameObject ignoredSource, Transform creditedRoot)
        {
            if (info.Amount <= 0f || info.Source == null) return false;
            if (ignoredSource != null && info.Source == ignoredSource) return false;
            if (creditedRoot == null) return true;
            Transform source = info.Source.transform;
            return source == creditedRoot || source.IsChildOf(creditedRoot);
        }

        public bool IsCredited(in DamageInfo info) => IsCredited(info, IgnoredSource, CreditedRoot);

        /// <summary>One enemy took damage. Returns true if it counted (+1).</summary>
        public bool RegisterHit(in DamageInfo info)
        {
            if (!IsCredited(info)) return false;
            Increment();
            return true;
        }

        /// <summary>One enemy died. Returns true if it counted (+1). Call after <see cref="RegisterHit"/> for the killing hit.</summary>
        public bool RegisterKill(in DamageInfo info)
        {
            if (!IsCredited(info)) return false;
            Increment();
            return true;
        }

        /// <summary>
        /// Drains the window by <paramref name="dt"/> (game time) unless no enemy is alive or <paramref name="frozen"/>.
        /// Call after the frame's hits (LateUpdate) so a hit on the expiry frame wins.
        /// </summary>
        public void Tick(float dt, int enemiesAlive, bool frozen)
        {
            TimerPaused = Count == 0 || frozen || enemiesAlive <= 0;
            if (TimerPaused || dt <= 0f) return;
            TimeLeft -= dt;
            if (TimeLeft <= 0f) Break(ComboBreak.Timeout);
        }

        /// <summary>Defne lost health: the combo ends (nothing happens at 0).</summary>
        public void PlayerHurt()
        {
            if (Count > 0) Break(ComboBreak.Damage);
        }

        /// <summary>Back to 0 without events (new run).</summary>
        public void Reset()
        {
            Count = 0;
            TimeLeft = 0f;
            TimerPaused = true;
        }

        private void Increment()
        {
            int oldTier = Tier;
            Count++;
            if (Count > Best) Best = Count;
            TimeLeft = _window;
            Changed?.Invoke(Count);
            int newTier = Tier;
            if (newTier > oldTier) TierUp?.Invoke(newTier);
        }

        private void Break(ComboBreak reason)
        {
            int lost = Count;
            Count = 0;
            TimeLeft = 0f;
            TimerPaused = true;
            Changed?.Invoke(0);
            Broken?.Invoke(reason, lost);
        }
    }

    /// <summary>
    /// Scene side of the combo: hooks every spawned enemy's Health (hits and kills, before
    /// <see cref="WaveDirector.EnemyKilled"/> fires) and Defne's Health (reset), and drains the window in LateUpdate on
    /// scaled time, frozen while the Devil screen, pause or a global hitstop is up. <see cref="ScoreKeeper"/> asks
    /// <see cref="ConsumeKillMultiplierTenths"/> for each kill: Defne's kills pay the current multiplier, shockwave kills ×1.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ComboTracker : MonoBehaviour
    {
        [Tooltip("Wave director whose enemies are tracked. Its own kills (revive shockwave) never count.")]
        [SerializeField] private WaveDirector _waves;
        [Tooltip("Devil flow: the window is frozen while its State is not Playing. Empty = no check.")]
        [SerializeField] private DevilDirector _devil;
        [Tooltip("Defne's Health: losing health resets the combo. Empty = taken from the wave director's player.")]
        [SerializeField] private Health _playerHealth;
        [Tooltip("Seconds after the last counted hit or kill (design: 3.0).")]
        [SerializeField, Min(0.1f)] private float _window = ComboCounter.DefaultWindow;

        private readonly ComboCounter _counter = new ComboCounter();
        private readonly Dictionary<Enemy, Action<DamageInfo, float>> _handlers = new Dictionary<Enemy, Action<DamageInfo, float>>();
        private readonly HashSet<Enemy> _hooked = new HashSet<Enemy>();
        private readonly HashSet<Enemy> _creditedKills = new HashSet<Enemy>();
        private readonly List<Enemy> _scratch = new List<Enemy>(32);
        private Action<Enemy> _onSpawned;
        private Action<DamageInfo, float> _onPlayerDamaged;
        private bool _subscribed;

        public ComboCounter Counter => _counter;
        public int Count => _counter.Count;
        public float Multiplier => _counter.Multiplier;
        public int MultiplierTenths => _counter.MultiplierTenths;
        public int Tier => _counter.Tier;
        public float TimeLeft01 => _counter.TimeLeft01;
        public bool TimerPaused => _counter.TimerPaused;

        public event Action<int> Changed { add => _counter.Changed += value; remove => _counter.Changed -= value; }
        public event Action<int> TierUp { add => _counter.TierUp += value; remove => _counter.TierUp -= value; }
        public event Action<ComboBreak, int> Broken { add => _counter.Broken += value; remove => _counter.Broken -= value; }

        private void Awake()
        {
            _counter.Window = _window;
            _onSpawned = Hook;
            _onPlayerDamaged = OnPlayerDamaged;
        }

        private void OnEnable() => Subscribe();
        private void OnDisable() => Unsubscribe();

        /// <summary>Code-built setups (tests). Re-subscribes.</summary>
        public void Configure(WaveDirector waves, Health playerHealth, DevilDirector devil = null)
        {
            Unsubscribe();
            _waves = waves;
            _playerHealth = playerHealth;
            _devil = devil;
            if (isActiveAndEnabled) Subscribe();
        }

        /// <summary>
        /// Multiplier in tenths for this kill (10 = ×1.0): the current one if Defne's hit killed it, else 10.
        /// Call once per kill from <see cref="WaveDirector.EnemyKilled"/>.
        /// </summary>
        public int ConsumeKillMultiplierTenths(Enemy enemy)
        {
            return enemy != null && _creditedKills.Remove(enemy) ? _counter.MultiplierTenths : 10;
        }

        private void Subscribe()
        {
            if (_subscribed || _onSpawned == null) return;
            if (_playerHealth == null && _waves != null && _waves.Player != null)
                _playerHealth = _waves.Player.GetComponentInParent<Health>();
            // Without Defne's Health every source would count as hers and damage would never break the combo:
            // stay unsubscribed and retry (LateUpdate) until the scene has wired her.
            if (_playerHealth == null) return;
            _subscribed = true;

            _counter.IgnoredSource = _waves != null ? _waves.gameObject : null;
            _counter.CreditedRoot = _playerHealth != null ? _playerHealth.transform : null;

            if (_playerHealth != null) _playerHealth.Damaged += _onPlayerDamaged;
            if (_waves != null)
            {
                _waves.EnemySpawned += _onSpawned;
                IReadOnlyList<Enemy> active = _waves.ActiveEnemies;
                for (int i = 0; i < active.Count; i++) Hook(active[i]);
            }
        }

        private void Unsubscribe()
        {
            if (!_subscribed) return;
            _subscribed = false;
            if (_playerHealth != null) _playerHealth.Damaged -= _onPlayerDamaged;
            if (_waves != null) _waves.EnemySpawned -= _onSpawned;
            _scratch.Clear();
            _scratch.AddRange(_hooked);
            for (int i = 0; i < _scratch.Count; i++) Unhook(_scratch[i]);
            _scratch.Clear();
            _hooked.Clear();
        }

        private void Hook(Enemy enemy)
        {
            if (enemy == null || enemy.Health == null || !_hooked.Add(enemy)) return;
            if (!_handlers.TryGetValue(enemy, out Action<DamageInfo, float> handler))
            {
                handler = (info, remaining) => OnEnemyDamaged(enemy, info, remaining);
                _handlers.Add(enemy, handler);
            }
            enemy.Health.Damaged += handler;
        }

        private void Unhook(Enemy enemy)
        {
            if (enemy == null) return;
            _hooked.Remove(enemy);
            if (enemy.Health != null && _handlers.TryGetValue(enemy, out Action<DamageInfo, float> handler))
                enemy.Health.Damaged -= handler;
        }

        // Health.Damaged runs before Health.Died → Enemy.Died → WaveDirector.EnemyKilled → ScoreKeeper, so the killing
        // hit and the kill are counted before the score is paid (design: the kill that crosses a tier pays the new tier).
        private void OnEnemyDamaged(Enemy enemy, DamageInfo info, float remaining)
        {
            _counter.RegisterHit(info);
            if (remaining > 0f) return;
            if (_counter.RegisterKill(info)) _creditedKills.Add(enemy);
            Unhook(enemy);
        }

        private void OnPlayerDamaged(DamageInfo info, float remaining) => _counter.PlayerHurt();

        private void LateUpdate()
        {
            if (!_subscribed) Subscribe(); // Defne's Health may be wired after this enabled
            _creditedKills.Clear(); // a kill's score is paid in the same call stack; nothing should survive the frame
            bool frozen = Time.timeScale <= 0f
                          || RunPause.IsPaused
                          || (_devil != null && _devil.State != DevilState.Playing)
                          || (HitStop.Instance != null && HitStop.Instance.IsGlobalActive);
            int alive = _waves != null ? _waves.ActiveCount : 0;
            _counter.Tick(Time.deltaTime, alive, frozen);
        }
    }
}
