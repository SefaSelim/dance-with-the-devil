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

        public DamageInfo(float amount, Vector3 point, Vector3 direction, GameObject source, DamageKind kind)
        {
            Amount = amount;
            Point = point;
            Direction = direction;
            Source = source;
            Kind = kind;
        }
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
