using System;
using UnityEngine;

namespace DanceWithTheDevil
{
    /// <summary>
    /// First-GDD score (MAIN_GDD §10/§11), shown small at the bottom of the results. Implemented: +100 per enemy
    /// killed (any cause, via <see cref="WaveDirector.EnemyKilled"/>), multiplied by the combo multiplier when Defne
    /// made the kill (<see cref="ComboTracker"/>: 100 × 1.0-3.0, always an integer); revive-shockwave kills stay a
    /// flat 100. Crit bonus (user decision 2026-10-25): every crit hit Defne lands on an enemy adds
    /// 25 × the current combo multiplier (rounded half up, <see cref="CritBonus"/>), on top of the kill points. Not yet: +25 per module throw that hits
    /// (needs the module system to report it; call <see cref="Add"/>), fast-clear bonus, and damage avoidance
    /// (open question A-16). Those count 0 for now.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ScoreKeeper : MonoBehaviour
    {
        [Tooltip("Wave director whose kills are scored.")]
        [SerializeField] private WaveDirector _waves;
        [Tooltip("Points per enemy killed (GDD §11: 100).")]
        [SerializeField, Min(0)] private int _pointsPerKill = 100;
        [Tooltip("Combo multiplier source. Empty = every kill pays the flat points.")]
        [SerializeField] private ComboTracker _combo;
        [Tooltip("Points per crit hit on an enemy, × the combo multiplier (user decision 2026-10-25: 25). Needs the combo tracker.")]
        [SerializeField, Min(0)] private int _pointsPerCrit = 25;

        private Action<Enemy> _onKilled;
        private Action<Enemy, DamageInfo> _onCrit;
        private ComboTracker _critSubscribed;

        public int Score { get; private set; }

        /// <summary>Raised with the new score.</summary>
        public event Action<int> Changed;

        private void Awake()
        {
            _onKilled = OnEnemyKilled;
            _onCrit = OnCritHit;
        }

        private void OnEnable()
        {
            if (_waves != null) _waves.EnemyKilled += _onKilled;
            SubscribeCrit();
        }

        private void OnDisable()
        {
            if (_waves != null) _waves.EnemyKilled -= _onKilled;
            UnsubscribeCrit();
        }

        /// <summary>Crit bonus for one crit hit: points × tenths / 10, rounded half up (25 at ×1.1 → 28).</summary>
        public static int CritBonus(int pointsPerCrit, int multiplierTenths) =>
            (Mathf.Max(0, pointsPerCrit) * Mathf.Max(0, multiplierTenths) + 5) / 10;

        private void SubscribeCrit()
        {
            if (_critSubscribed == _combo) return;
            UnsubscribeCrit();
            if (_combo == null || _onCrit == null) return;
            _combo.CritHit += _onCrit;
            _critSubscribed = _combo;
        }

        private void UnsubscribeCrit()
        {
            if (_critSubscribed != null) _critSubscribed.CritHit -= _onCrit;
            _critSubscribed = null;
        }

        /// <summary>Code-built setups. Re-subscribes to the new director.</summary>
        public void Configure(WaveDirector waves)
        {
            if (isActiveAndEnabled && _waves != null) _waves.EnemyKilled -= _onKilled;
            _waves = waves;
            if (isActiveAndEnabled && _waves != null) _waves.EnemyKilled += _onKilled;
        }

        /// <summary>Code-built setups: kills pay points × the combo multiplier.</summary>
        public void ConfigureCombo(ComboTracker combo)
        {
            _combo = combo;
            if (isActiveAndEnabled) SubscribeCrit();
        }

        public void Add(int points)
        {
            if (points == 0) return;
            Score += points;
            Changed?.Invoke(Score);
        }

        // The tracker counted the killing hit and the kill before EnemyKilled fired, so a tier-crossing kill pays the new tier.
        private void OnEnemyKilled(Enemy enemy)
        {
            int tenths = _combo != null ? _combo.ConsumeKillMultiplierTenths(enemy) : 10;
            Add(_pointsPerKill * tenths / 10);
        }

        // Raised by the tracker after the crit hit counted, so the multiplier already includes it.
        private void OnCritHit(Enemy enemy, DamageInfo info) => Add(CritBonus(_pointsPerCrit, _combo != null ? _combo.MultiplierTenths : 10));
    }
}
