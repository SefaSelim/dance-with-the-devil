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
        [Tooltip("Shortest lunge dash in metres; Lunge Distance / Lunge Duration is the dash speed.")]
        [Min(0f)] public float LungeDistance;
        [Tooltip("Lunge dash duration in seconds at the shortest lunge.")]
        [Min(0.01f)] public float LungeDuration;
        [Tooltip("Longest lunge dash in metres, used when the player is moving out of reach during the tell " +
                 "(0 = always Lunge Distance). The dash still stops at body contact.")]
        [Min(0f)] public float MaxLungeDistance;
        [Tooltip("Radius of the hit sphere in front of the body during the lunge, in metres.")]
        [Min(0.05f)] public float LungeHitRadius;
        [Tooltip("Distance backed off during recovery (Runner). 0 = stand still.")]
        [Min(0f)] public float RetreatDistance;

        [Header("Ranged")]
        [Tooltip("Shooter: preferred distance band to the player (min, max) in metres.")]
        public Vector2 PreferredRange;
        [Tooltip("Shooter: projectile speed in m/s.")]
        [Min(0f)] public float ProjectileSpeed;

        /// <summary>
        /// GDD §6 defaults. Values marked * in the GDD are starting suggestions; the 2026-10-24 retune (F2, a player
        /// who keeps moving must still get hit) changed three of them:
        /// - Chaser speed 3.5 → 4.2 m/s*: still below the player's 6 m/s, so running straight away always escapes,
        ///   but a chaser on an intercept course now closes the gap when the player turns or strafes.
        /// - Runner damage 6 → 8*: the Runner dies to two rifle shots, so the hits it does land must matter.
        /// - Shooter orb 12 → 14 m/s*: lock-to-impact at 9 m drops from 0.25 + 0.75 = 1.0 s to 0.25 + 0.64 = 0.89 s,
        ///   so with the aim lead (ShooterBrain) anyone crossing the line slower than ~4 m/s is hit; only a full
        ///   sprint straight across it, a turn or a dodge escapes.
        /// New (not in the GDD): Max Lunge Distance. The tell (1.6 m trigger, 0.45 s / 0.35 s stop) is unchanged; the
        /// dash may stretch from 1.5 to 2.6 m (Runner 1.2 to 2.2 m) toward where the player is going. A player
        /// running straight away during a Chaser tell is 1.6 + 6 × 0.45 = 4.3 m off when the dash starts and
        /// 4.3 + 6 × 0.21 = 5.56 m when it ends; the dash reaches 2.6 + 0.6 (sphere offset) + 1.0 (sphere) + 0.35
        /// (player) = 4.55 m, so running away still escapes. Strafing across at 6 m/s ends ~1.1 m from the sphere
        /// centre (≤ 1.35 m), so it is hit unless the player turns away or dodges.
        /// </summary>
        public static EnemyStats For(EnemyType type)
        {
            switch (type)
            {
                case EnemyType.Runner:
                    return new EnemyStats
                    {
                        MaxHealth = 15f, MoveSpeed = 7.5f, AttackRange = 1.2f, TellDuration = 0.35f, Damage = 8f,
                        Recovery = 1.0f, ThreatPoints = 1,
                        LungeDistance = 1.2f, LungeDuration = 0.12f, MaxLungeDistance = 2.2f, LungeHitRadius = 0.8f,
                        RetreatDistance = 3f,
                    };
                case EnemyType.Shooter:
                    return new EnemyStats
                    {
                        MaxHealth = 40f, MoveSpeed = 3.0f, AttackRange = 11f, TellDuration = 0.8f, TellLockTime = 0.25f,
                        Damage = 12f, Recovery = 1.6f, ThreatPoints = 2,
                        LungeDuration = 0.12f, LungeHitRadius = 1f,
                        PreferredRange = new Vector2(7f, 11f), ProjectileSpeed = 14f,
                    };
                default:
                    return new EnemyStats
                    {
                        MaxHealth = 30f, MoveSpeed = 4.2f, AttackRange = 1.6f, TellDuration = 0.45f, Damage = 10f,
                        Recovery = 1.2f, ThreatPoints = 1,
                        LungeDistance = 1.5f, LungeDuration = 0.12f, MaxLungeDistance = 2.6f, LungeHitRadius = 1.0f,
                        RetreatDistance = 0f,
                    };
            }
        }
    }
}
