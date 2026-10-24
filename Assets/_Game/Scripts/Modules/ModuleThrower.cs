using System;
using System.Collections.Generic;
using UnityEngine;

namespace DanceWithTheDevil
{
    /// <summary>
    /// Thrown module (MAIN_GDD §7 Fırlatılan modül), used by reject and eject: ~16 m/s, explodes on the first
    /// enemy/wall contact or after 0.7 s; 25 damage within 1.8 m plus a short knockback; not through walls; never
    /// hurts the player; explodes once. Flight reuses <see cref="Projectile"/> in detonate mode.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ModuleThrower : MonoBehaviour
    {
        private const int MaxOverlaps = 128;
        private const int MaxLineHits = 16;

        [Tooltip("Pool for the flying modules. If empty, one is added to this GameObject (runtime cubes).")]
        [SerializeField] private ProjectilePool _pool;
        [Tooltip("Player (ignored by the flight and the explosion). If empty, found in parents.")]
        [SerializeField] private PlayerController _owner;

        [Header("Flight (GDD §7)")]
        [Tooltip("Speed in m/s (GDD: ~16).")]
        [SerializeField, Min(0.1f)] private float _speed = 16f;
        [Tooltip("Explodes after this many seconds without contact (GDD: 0.7).")]
        [SerializeField, Min(0.01f)] private float _flightTime = 0.7f;
        [Tooltip("Contact radius in metres.")]
        [SerializeField, Min(0.01f)] private float _radius = 0.3f;
        [Tooltip("Layers the module explodes on.")]
        [SerializeField] private LayerMask _hitMask = ~0;

        [Header("Explosion (GDD §7)")]
        [Tooltip("Damage to every enemy in the radius (GDD: 25).")]
        [SerializeField, Min(0f)] private float _damage = 25f;
        [Tooltip("Explosion radius in metres (GDD: 1.8).")]
        [SerializeField, Min(0.1f)] private float _explosionRadius = 1.8f;
        [Tooltip("Short knockback, push distance in metres.")]
        [SerializeField, Min(0f)] private float _knockback = 1f;
        [Tooltip("Layers the explosion reaches.")]
        [SerializeField] private LayerMask _explosionMask = ~0;
        [Tooltip("Layers that block the explosion (walls: non-damageable colliders).")]
        [SerializeField] private LayerMask _obstacleMask = ~0;
        [Tooltip("ImpactBus Explosion intensity.")]
        [SerializeField, Range(0f, 1f)] private float _explosionIntensity = 0.4f;

        private readonly Collider[] _overlaps = new Collider[MaxOverlaps];
        private readonly RaycastHit[] _lineHits = new RaycastHit[MaxLineHits];
        private readonly HashSet<IDamageable> _damaged = new HashSet<IDamageable>();
        private ProjectileDetonateHandler _onDetonate;
        private Transform _ownerRoot;

        /// <summary>Raised after each explosion with its centre.</summary>
        public event Action<Vector3> Exploded;

        public float Speed => _speed;
        public float ExplosionRadius => _explosionRadius;

        private void Awake()
        {
            if (_owner == null) _owner = GetComponentInParent<PlayerController>();
            _ownerRoot = _owner != null ? _owner.transform : transform.root;
            _onDetonate = Explode;
            if (_pool == null)
            {
                _pool = gameObject.AddComponent<ProjectilePool>();
                _pool.Configure(null, null);
            }
        }

        /// <summary>Throws a module of <paramref name="type"/> from <paramref name="origin"/> along a flat direction.</summary>
        public void Throw(ModuleType type, Vector3 origin, Vector3 direction)
        {
            direction.y = 0f;
            if (direction.sqrMagnitude < 1e-6f) direction = _owner != null ? _owner.AimDirection : Vector3.forward;
            direction.Normalize();

            GameObject source = _owner != null ? _owner.gameObject : gameObject;
            Projectile projectile = _pool.Get();
            projectile.Launch(new ProjectileLaunch
            {
                Origin = origin,
                Direction = direction,
                Speed = _speed,
                MaxRange = _speed * _flightTime,
                Radius = _radius,
                HitMask = _hitMask,
                IgnoreRoot = _ownerRoot,
                Source = source,
                SourceIsLocalPlayer = _owner != null,
                Kind = DamageKind.Explosion,
                OnDetonate = _onDetonate,
            });
            projectile.SetTint(ModuleCatalog.ColorOf(type));
        }

        private void Explode(Projectile projectile, Vector3 point, Vector3 normal)
        {
            // Centre = the module's position at contact (just in front of the surface), so walls block line of sight.
            Vector3 center = projectile.transform.position;
            GameObject source = _owner != null ? _owner.gameObject : gameObject;

            int count = Physics.OverlapSphereNonAlloc(center, _explosionRadius, _overlaps, _explosionMask,
                QueryTriggerInteraction.Ignore);
            _damaged.Clear();
            for (int i = 0; i < count; i++)
            {
                Collider collider = _overlaps[i];
                if (collider == null) continue;
                if (_ownerRoot != null && collider.transform.IsChildOf(_ownerRoot)) continue; // never the player

                IDamageable target = collider.GetComponentInParent<IDamageable>();
                if (target == null || _damaged.Contains(target)) continue;

                Vector3 targetCenter = collider.bounds.center;
                if (IsBlockedByWall(center, targetCenter)) continue;
                _damaged.Add(target);

                Vector3 away = targetCenter - center;
                away.y = 0f;
                away = away.sqrMagnitude > 1e-6f ? away.normalized : projectile.Direction;
                bool applied = target.TakeDamage(new DamageInfo(_damage, targetCenter, away, source, DamageKind.Explosion));
                if (applied && _knockback > 0f && target is Component component)
                {
                    IKnockbackable knockbackable = component.GetComponentInParent<IKnockbackable>();
                    if (knockbackable != null) knockbackable.ApplyKnockback(away * _knockback);
                }
            }

            ImpactBus.Raise(new ImpactEvent
            {
                kind = ImpactKind.Explosion,
                position = center,
                normal = normal,
                direction = projectile.Direction,
                intensity = _explosionIntensity,
                radius = _explosionRadius, // the blast ring shows the real 1.8 m reach
                surface = SurfaceType.Default,
                instigator = source,
                instigatorIsLocalPlayer = _owner != null,
            });
            Exploded?.Invoke(center);
        }

        private bool IsBlockedByWall(Vector3 from, Vector3 to)
        {
            Vector3 delta = to - from;
            float distance = delta.magnitude;
            if (distance <= 1e-4f) return false;
            int count = Physics.RaycastNonAlloc(from, delta / distance, _lineHits, distance, _obstacleMask,
                QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                Collider collider = _lineHits[i].collider;
                if (collider == null) continue;
                if (_ownerRoot != null && collider.transform.IsChildOf(_ownerRoot)) continue;
                if (collider.GetComponentInParent<IDamageable>() != null) continue;
                return true;
            }
            return false;
        }
    }
}
