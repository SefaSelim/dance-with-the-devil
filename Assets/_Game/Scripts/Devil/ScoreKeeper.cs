using System;
using UnityEngine;

namespace DanceWithTheDevil
{
    /// <summary>
    /// First-GDD score (MAIN_GDD §10/§11), shown small at the bottom of the results. Implemented: +100 per enemy
    /// killed (any cause, via <see cref="WaveDirector.EnemyKilled"/>), multiplied by the combo multiplier when Defne
    /// made the kill (<see cref="ComboTracker"/>: 100 × 1.0-3.0, always an integer); revive-shockwave kills stay a
    /// flat 100. Not yet: +25 per module throw that hits
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

        private Action<Enemy> _onKilled;

        public int Score { get; private set; }

        /// <summary>Raised with the new score.</summary>
        public event Action<int> Changed;

        private void Awake() => _onKilled = OnEnemyKilled;

        private void OnEnable()
        {
            if (_waves != null) _waves.EnemyKilled += _onKilled;
        }

        private void OnDisable()
        {
            if (_waves != null) _waves.EnemyKilled -= _onKilled;
        }

        /// <summary>Code-built setups. Re-subscribes to the new director.</summary>
        public void Configure(WaveDirector waves)
        {
            if (isActiveAndEnabled && _waves != null) _waves.EnemyKilled -= _onKilled;
            _waves = waves;
            if (isActiveAndEnabled && _waves != null) _waves.EnemyKilled += _onKilled;
        }

        /// <summary>Code-built setups: kills pay points × the combo multiplier.</summary>
        public void ConfigureCombo(ComboTracker combo) => _combo = combo;

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
    }
}
