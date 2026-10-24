using System.Collections.Generic;
using UnityEngine;

namespace DanceWithTheDevil
{
    /// <summary>
    /// Rocket (GDD §5 Silahlar): 2 charges, 6.0 s refill per charge (one at a time), 18 m/s, explodes on first contact
    /// or after 20 m; flat 40 damage within 2.5 m to every damageable once, ~2 m knockback, never hurts the player.
    /// Blocked while the owner dodges. Not done yet: breaking inner walls/props (needs the voxel arena).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class RocketLauncher : MonoBehaviour
    {
        private const int MaxOverlaps = 256;

        [Header("References")]
        [Tooltip("Rocket pool. If empty, one is added to this GameObject (runtime cubes).")]
        [SerializeField] private ProjectilePool _pool;
        [Tooltip("Material for the runtime rocket cubes when the pool is created automatically.")]
        [SerializeField] private Material _projectileMaterial;
        [Tooltip("Rocket spawn point. If empty: this transform + muzzle height.")]
        [SerializeField] private Transform _muzzle;
        [Tooltip("Fallback muzzle height above this transform when no muzzle is set.")]
        [SerializeField] private float _fallbackMuzzleHeight = 1f;
        [Tooltip("Owner, for the dodge block and the no-self-damage rule. If empty, found in parents.")]
        [SerializeField] private PlayerController _owner;

        [Header("Charges (GDD §5)")]
        [Tooltip("Maximum charges.")]
        [SerializeField, Min(1)] private int _maxCharges = 2;
        [Tooltip("Refill time per charge in seconds. Charges refill one at a time.")]
        [SerializeField, Min(0.01f)] private float _refillTime = 6f;

        [Header("Flight (GDD §5)")]
        [Tooltip("Rocket speed in m/s.")]
        [SerializeField, Min(0.1f)] private float _projectileSpeed = 18f;
        [Tooltip("Explodes after this distance in metres.")]
        [SerializeField, Min(0.1f)] private float _maxRange = 20f;
        [Tooltip("Rocket contact radius in metres.")]
        [SerializeField, Min(0.01f)] private float _projectileRadius = 0.2f;
        [Tooltip("Layers the rocket explodes on.")]
        [SerializeField] private LayerMask _hitMask = ~0;

        [Header("Explosion (GDD §5)")]
        [Tooltip("Flat damage to every damageable in the radius.")]
        [SerializeField, Min(0f)] private float _damage = 40f;
        [Tooltip("Explosion radius in metres.")]
        [SerializeField, Min(0.1f)] private float _explosionRadius = 2.5f;
        [Tooltip("Knockback (push distance in metres) passed to IKnockbackable.")]
        [SerializeField, Min(0f)] private float _knockback = 2f;
        [Tooltip("Layers the explosion reaches.")]
        [SerializeField] private LayerMask _explosionMask = ~0;

        [Header("Feel")]
        [Tooltip("ImpactBus Explosion intensity.")]
        [SerializeField, Range(0f, 1f)] private float _explosionIntensity = 0.9f;
        [Tooltip("ImpactBus MuzzleFire intensity on launch.")]
        [SerializeField, Range(0f, 1f)] private float _launchIntensity = 0.35f;

        private readonly Collider[] _overlaps = new Collider[MaxOverlaps];
        private readonly HashSet<IDamageable> _damagedThisExplosion = new HashSet<IDamageable>();
        private ProjectileDetonateHandler _onDetonate;
        private Transform _ownerRoot;

        private int _charges;
        private float _refillTimer;
        private float _damageMultiplier = 1f;
        private float _radiusMultiplier = 1f;
        private float _refillTimeMultiplier = 1f;

        public int Charges => _charges;
        public int MaxCharges => _maxCharges;
        /// <summary>0..1 progress of the charge currently refilling; 0 when full.</summary>
        public float RefillProgress => _charges >= _maxCharges ? 0f : Mathf.Clamp01(_refillTimer / CurrentRefillTime);
        public float Damage => _damage * _damageMultiplier;
        public float ExplosionRadius => _explosionRadius * _radiusMultiplier;
        public Transform Muzzle => _muzzle;
        public Vector3 MuzzlePosition =>
            _muzzle != null ? _muzzle.position : transform.position + Vector3.up * _fallbackMuzzleHeight;
        public bool IsBlocked => _owner != null && _owner.IsDodging;

        /// <summary>Raised when a rocket is launched (audio).</summary>
        public static event System.Action<RocketLauncher> Launched;
        /// <summary>Raised after a rocket explosion with its point (audio).</summary>
        public static event System.Action<Vector3> Exploded;

        /// <summary>Multiplies explosion damage (contracts). Min 0.</summary>
        public float DamageMultiplier
        {
            get => _damageMultiplier;
            set => _damageMultiplier = Mathf.Max(0f, value);
        }

        /// <summary>Multiplies explosion radius (contracts). Min 0.01.</summary>
        public float RadiusMultiplier
        {
            get => _radiusMultiplier;
            set => _radiusMultiplier = Mathf.Max(0.01f, value);
        }

        /// <summary>Multiplies the per-charge refill time (contracts; below 1 = faster). Min 0.01.</summary>
        public float RefillTimeMultiplier
        {
            get => _refillTimeMultiplier;
            set => _refillTimeMultiplier = Mathf.Max(0.01f, value);
        }

        private float CurrentRefillTime => _refillTime * _refillTimeMultiplier;

        private void Awake()
        {
            if (_owner == null) _owner = GetComponentInParent<PlayerController>();
            _ownerRoot = _owner != null ? _owner.transform : transform.root;
            _onDetonate = Explode; // cached: no delegate allocation per shot
            _charges = _maxCharges;

            if (_pool == null)
            {
                _pool = gameObject.AddComponent<ProjectilePool>();
                _pool.Configure(null, _projectileMaterial);
            }
        }

        /// <summary>Sets the max charges (min 1). If <paramref name="refill"/>, all charges are filled; else clamped.</summary>
        public void SetMaxCharges(int count, bool refill = false)
        {
            _maxCharges = Mathf.Max(1, count);
            _charges = refill ? _maxCharges : Mathf.Min(_charges, _maxCharges);
            if (_charges >= _maxCharges) _refillTimer = 0f;
        }

        private void Update()
        {
            if (_charges >= _maxCharges)
            {
                _refillTimer = 0f;
                return;
            }

            _refillTimer += Time.deltaTime;
            float refillTime = CurrentRefillTime;
            while (_refillTimer >= refillTime && _charges < _maxCharges)
            {
                _refillTimer -= refillTime;
                _charges++;
            }
            if (_charges >= _maxCharges) _refillTimer = 0f;
        }

        /// <summary>Launches a rocket if a charge is available and nothing blocks it. Uses one charge.</summary>
        public bool TryFire(Vector3 origin, Vector3 direction)
        {
            if (!isActiveAndEnabled || IsBlocked || _charges <= 0) return false;
            if (direction.sqrMagnitude < 1e-6f) return false;

            direction.Normalize();
            _charges--;

            GameObject source = _owner != null ? _owner.gameObject : gameObject;
            bool isPlayer = _owner != null;

            Projectile projectile = _pool.Get();
            projectile.Launch(new ProjectileLaunch
            {
                Origin = origin,
                Direction = direction,
                Speed = _projectileSpeed,
                MaxRange = _maxRange,
                Radius = _projectileRadius,
                HitMask = _hitMask,
                IgnoreRoot = _ownerRoot,
                Source = source,
                SourceIsLocalPlayer = isPlayer,
                Kind = DamageKind.Rocket,
                OnDetonate = _onDetonate,
            });

            Launched?.Invoke(this);
            ImpactBus.Raise(new ImpactEvent
            {
                kind = ImpactKind.MuzzleFire,
                position = origin,
                normal = direction,
                direction = direction,
                intensity = _launchIntensity,
                surface = SurfaceType.Default,
                hitTransform = _muzzle,
                instigator = source,
                instigatorIsLocalPlayer = isPlayer,
            });
            return true;
        }

        private void Explode(Projectile projectile, Vector3 point, Vector3 normal)
        {
            GameObject source = _owner != null ? _owner.gameObject : gameObject;
            float radius = ExplosionRadius;
            float damage = Damage;

            int count = Physics.OverlapSphereNonAlloc(point, radius, _overlaps, _explosionMask,
                QueryTriggerInteraction.Ignore);
            _damagedThisExplosion.Clear();

            for (int i = 0; i < count; i++)
            {
                Collider collider = _overlaps[i];
                if (collider == null) continue;
                if (_ownerRoot != null && collider.transform.IsChildOf(_ownerRoot)) continue; // never the player

                IDamageable target = collider.GetComponentInParent<IDamageable>();
                if (target == null || !_damagedThisExplosion.Add(target)) continue;

                var targetComponent = target as Component;
                Vector3 targetPosition = targetComponent != null ? targetComponent.transform.position : collider.bounds.center;
                Vector3 away = targetPosition - point;
                away.y = 0f;
                away = away.sqrMagnitude > 1e-6f ? away.normalized : projectile.Direction;

                bool applied = target.TakeDamage(new DamageInfo(damage, targetPosition, away, source, DamageKind.Rocket));
                if (applied && _knockback > 0f && targetComponent != null)
                {
                    IKnockbackable knockbackable = targetComponent.GetComponentInParent<IKnockbackable>();
                    if (knockbackable != null) knockbackable.ApplyKnockback(away * _knockback);
                }
            }

            ImpactBus.Raise(new ImpactEvent
            {
                kind = ImpactKind.Explosion,
                position = point,
                normal = normal,
                direction = projectile.Direction,
                intensity = _explosionIntensity,
                surface = SurfaceType.Default,
                instigator = source,
                instigatorIsLocalPlayer = _owner != null,
            });
            Exploded?.Invoke(point);
        }
    }
}
