using UnityEngine;

namespace DanceWithTheDevil
{
    /// <summary>What dealt the damage. Receivers use it for kind-specific rules (e.g. the shielded brute, GDD §6).</summary>
    public enum DamageKind
    {
        Bullet,
        Rocket,
        Melee,
        Contact,
        Explosion,
    }

    /// <summary>One damage application. Passed by <c>in</c> reference; build it on the stack, never cache it.</summary>
    public struct DamageInfo
    {
        public float Amount;
        /// <summary>World point of the hit.</summary>
        public Vector3 Point;
        /// <summary>Incoming travel direction (normalised, may be zero).</summary>
        public Vector3 Direction;
        /// <summary>Who caused it (shooter root). May be null.</summary>
        public GameObject Source;
        public DamageKind Kind;
        /// <summary>True when this hit is a critical hit (Amount already includes the crit multiplier).</summary>
        public bool IsCrit;

        public DamageInfo(float amount, Vector3 point, Vector3 direction, GameObject source, DamageKind kind)
            : this(amount, point, direction, source, kind, false)
        {
        }

        public DamageInfo(float amount, Vector3 point, Vector3 direction, GameObject source, DamageKind kind, bool isCrit)
        {
            Amount = amount;
            Point = point;
            Direction = direction;
            Source = source;
            Kind = kind;
            IsCrit = isCrit;
        }
    }

    /// <summary>
    /// Critical hits (user decision 2026-10-25, production/design/crit-difficulty.md): every player weapon rolls per
    /// target hit with its own <c>CritChance</c> (base 5 %, + Keskin Göz module on the rifle, + contract 3 on all);
    /// a crit multiplies that hit's damage (×2). One shared random stream; tests call <see cref="Seed"/> for a
    /// repeatable sequence.
    /// </summary>
    public static class CritRoll
    {
        /// <summary>Default base crit chance of every weapon (5 %). The weapons serialize their own copy.</summary>
        public const float DefaultChance = 0.05f;
        /// <summary>Default damage multiplier of a crit (×2). The weapons serialize their own copy.</summary>
        public const float DefaultMultiplier = 2f;

        private static System.Random s_random = new System.Random();

        /// <summary>Restarts the crit random stream with <paramref name="seed"/> (tests).</summary>
        public static void Seed(int seed) => s_random = new System.Random(seed);

        /// <summary>True with probability <paramref name="chance"/> (clamped 0..1; 0 never rolls, 1 always crits).</summary>
        public static bool Roll(float chance)
        {
            if (chance <= 0f) return false;
            if (chance >= 1f) return true;
            return s_random.NextDouble() < chance;
        }

        /// <summary>Base + bonuses, clamped to 0..1 (the weapons' CritChance).</summary>
        public static float Combine(float baseChance, float bonusA, float bonusB) =>
            Mathf.Clamp01(baseChance + bonusA + bonusB);
    }

    /// <summary>Anything that can take damage. Looked up with GetComponentInParent from a hit collider.</summary>
    public interface IDamageable
    {
        /// <summary>Returns true if the damage was applied (false while invulnerable, dead, etc.).</summary>
        bool TakeDamage(in DamageInfo info);
    }

    /// <summary>Optional: things that can be pushed (rocket explosion, later module explosions).</summary>
    public interface IKnockbackable
    {
        /// <summary>
        /// Push the receiver. Direction is the push direction; magnitude is the intended push distance in metres
        /// (the receiver decides how to spend it over time).
        /// </summary>
        void ApplyKnockback(Vector3 impulse);
    }
}
