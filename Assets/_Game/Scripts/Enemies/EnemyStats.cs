using UnityEngine;

namespace DanceWithTheDevil
{
    /// <summary>Enemy kinds in the minimum scope (GDD §6, K-11). Imp and brute are cut.</summary>
    public enum EnemyType
    {
        Chaser,
        Runner,
        Shooter,
    }

    /// <summary>
    /// Per-type tuning values from GDD §6 (Düşmanlar). Serialized on each enemy prefab's <see cref="Enemy"/>;
    /// <see cref="For"/> gives the GDD defaults. Tell durations are never changed by contracts (GDD §6).
    /// </summary>
    [System.Serializable]
    public struct EnemyStats
    {
        [Tooltip("Hit points (GDD §6: constant, never scaled by waves).")]
        [Min(1f)] public float MaxHealth;
        [Tooltip("Base move speed in m/s (multiplied by Enemy.SpeedMultiplier).")]
        [Min(0f)] public float MoveSpeed;
        [Tooltip("Melee: distance at which the tell starts. Shooter: unused (see preferred range).")]
        [Min(0f)] public float AttackRange;
        [Tooltip("Tell (telegraph) duration in seconds.")]
        [Min(0f)] public float TellDuration;
        [Tooltip("Shooter: last part of the tell with the aim direction locked, in seconds.")]
        [Min(0f)] public float TellLockTime;
        [Tooltip("Damage per hit (melee lunge or projectile).")]
        [Min(0f)] public float Damage;
        [Tooltip("Recovery after an attack in seconds.")]
        [Min(0f)] public float Recovery;
        [Tooltip("Threat points for the wave budget (GDD §10).")]
        [Min(0)] public int ThreatPoints;

        [Header("Melee lunge")]
        [Tooltip("Lunge dash distance in metres.")]
        [Min(0f)] public float LungeDistance;
        [Tooltip("Lunge dash duration in seconds.")]
        [Min(0.01f)] public float LungeDuration;
        [Tooltip("Radius of the hit sphere in front of the body during the lunge, in metres.")]
        [Min(0.05f)] public float LungeHitRadius;
        [Tooltip("Distance backed off during recovery (Runner). 0 = stand still.")]
        [Min(0f)] public float RetreatDistance;

        [Header("Ranged")]
        [Tooltip("Shooter: preferred distance band to the player (min, max) in metres.")]
        public Vector2 PreferredRange;
        [Tooltip("Shooter: projectile speed in m/s.")]
        [Min(0f)] public float ProjectileSpeed;

        public static EnemyStats For(EnemyType type)
        {
            switch (type)
            {
                case EnemyType.Runner:
                    return new EnemyStats
                    {
                        MaxHealth = 15f, MoveSpeed = 7.5f, AttackRange = 1.2f, TellDuration = 0.35f, Damage = 6f,
                        Recovery = 1.0f, ThreatPoints = 1,
                        LungeDistance = 1.2f, LungeDuration = 0.12f, LungeHitRadius = 0.8f, RetreatDistance = 3f,
                    };
                case EnemyType.Shooter:
                    return new EnemyStats
                    {
                        MaxHealth = 40f, MoveSpeed = 3.0f, AttackRange = 11f, TellDuration = 0.8f, TellLockTime = 0.25f,
                        Damage = 12f, Recovery = 1.6f, ThreatPoints = 2,
                        LungeDuration = 0.12f, LungeHitRadius = 1f,
                        PreferredRange = new Vector2(7f, 11f), ProjectileSpeed = 12f,
                    };
                default:
                    return new EnemyStats
                    {
                        MaxHealth = 30f, MoveSpeed = 3.5f, AttackRange = 1.6f, TellDuration = 0.45f, Damage = 10f,
                        Recovery = 1.2f, ThreatPoints = 1,
                        LungeDistance = 1.5f, LungeDuration = 0.12f, LungeHitRadius = 1.0f, RetreatDistance = 0f,
                    };
            }
        }
    }
}
