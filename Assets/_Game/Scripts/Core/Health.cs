using System;
using UnityEngine;

namespace DanceWithTheDevil
{
    /// <summary>
    /// Hit points with post-hit invulnerability (GDD §4: player 100 HP, 0.6 s after a hit so simultaneous shots do
    /// not stack in one frame). Ignores damage while invulnerable or dead. Timers use scaled time (Time.time).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class Health : MonoBehaviour, IDamageable
    {
        [Tooltip("Maximum hit points.")]
        [SerializeField, Min(1f)] private float _maxHealth = 100f;
        [Tooltip("Invulnerability after a hit that was applied, in seconds. 0 = none (enemies, training dummy).")]
        [SerializeField, Min(0f)] private float _invulnerabilityAfterHit = 0.6f;

        private float _current;
        private float _invulnerableUntil = float.NegativeInfinity;
        private bool _isDead;
        private bool _initialized;

        /// <summary>Raised after damage is applied. Second argument is the remaining health.</summary>
        public event Action<DamageInfo, float> Damaged;
        /// <summary>Raised once when health reaches 0 (again only after <see cref="Revive"/>).</summary>
        public event Action<DamageInfo> Died;

        public float MaxHealth => _maxHealth;
        public float Current
        {
            get
            {
                EnsureInitialized();
                return _current;
            }
        }
        public float Normalized => MaxHealth > 0f ? Current / MaxHealth : 0f;
        public bool IsDead => _isDead;
        public bool IsInvulnerable => Time.time < _invulnerableUntil;
        public float InvulnerabilityAfterHit
        {
            get => _invulnerabilityAfterHit;
            set => _invulnerabilityAfterHit = Mathf.Max(0f, value);
        }

        private void Awake() => EnsureInitialized();

        private void EnsureInitialized()
        {
            if (_initialized) return;
            _initialized = true;
            _current = _maxHealth;
        }

        public bool TakeDamage(in DamageInfo info)
        {
            EnsureInitialized();
            if (_isDead || IsInvulnerable || info.Amount <= 0f) return false;

            _current = Mathf.Max(0f, _current - info.Amount);
            if (_invulnerabilityAfterHit > 0f) SetInvulnerable(_invulnerabilityAfterHit);

            Damaged?.Invoke(info, _current);

            if (_current <= 0f)
            {
                _isDead = true;
                Died?.Invoke(info);
            }
            return true;
        }

        /// <summary>Invulnerable for <paramref name="seconds"/> from now. Extends an active window, never shortens it.</summary>
        public void SetInvulnerable(float seconds)
        {
            if (seconds <= 0f) return;
            _invulnerableUntil = Mathf.Max(_invulnerableUntil, Time.time + seconds);
        }

        /// <summary>Sets max health (min 1). If <paramref name="refill"/>, current becomes max; otherwise it is clamped.</summary>
        public void SetMaxHealth(float value, bool refill)
        {
            EnsureInitialized();
            _maxHealth = Mathf.Max(1f, value);
            _current = refill ? _maxHealth : Mathf.Min(_current, _maxHealth);
        }

        /// <summary>Adds health up to max. Returns false when dead or nothing was healed.</summary>
        public bool Heal(float amount)
        {
            EnsureInitialized();
            if (_isDead || amount <= 0f || _current >= _maxHealth) return false;
            _current = Mathf.Min(_maxHealth, _current + amount);
            return true;
        }

        /// <summary>Back to full health, alive, no invulnerability.</summary>
        public void Revive()
        {
            EnsureInitialized();
            _isDead = false;
            _current = _maxHealth;
            _invulnerableUntil = float.NegativeInfinity;
        }
    }
}
