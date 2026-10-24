using System.Collections.Generic;
using UnityEngine;

namespace DanceWithTheDevil
{
    /// <summary>
    /// Rocket (GDD §5 Silahlar): 2 charges, 6.0 s refill per charge (one at a time), 18 m/s, explodes on first contact
    /// or after 20 m; flat 40 damage within 2.5 m to every damageable once, ~2 m knockback, never hurts the player.
    /// Blocked while the owner dodges. Not done yet: breaking inner walls/props (needs the voxel arena).
    /// Aiming (GDD §4) works like the rifle's: <see cref="TryFireAt"/> fires from the muzzle toward the aim point, from a
    /// spawn point pulled back out of walls; the muzzle is the gun in Defne's hand when there is one, and a launch
    /// kicks it hard.
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
        [Tooltip("Rocket spawn point when the character has no animated gun (CharacterAnimator.Muzzle wins). If empty: this transform + muzzle height.")]
        [SerializeField] private Transform _muzzle;
        [Tooltip("Fallback muzzle height above this transform when no muzzle is set.")]
        [SerializeField] private float _fallbackMuzzleHeight = 1f;
        [Tooltip("Owner, for the dodge block and the no-self-damage rule. If empty, found in parents.")]
        [SerializeField] private PlayerController _owner;
        [Tooltip("Character animator holding the gun (muzzle and recoil kick). If empty, found in parents.")]
        [SerializeField] private CharacterAnimator _animator;

        [Header("Aim (GDD §4)")]
        [Tooltip("With an owner, rockets leave at this height above the owner's feet (GDD: muzzle height 1.0 m). 0 = the muzzle's own height.")]
        [SerializeField, Min(0f)] private float _shotHeight = 1f;
        [Tooltip("If the aim point is closer than this to the muzzle (flat metres), fire along the body aim direction instead.")]
        [SerializeField, Min(0f)] private float _minConvergeDistance = 1f;
        [Tooltip("If the muzzle-to-aim direction differs from the body aim direction by more than this (degrees), fire along the body aim direction.")]
        [SerializeField, Range(0f, 180f)] private float _maxConvergeAngle = 60f;
        [Tooltip("Visual recoil kick passed to CharacterAnimator.Kick per launch (0..2).")]
        [SerializeField, Range(0f, 2f)] private float _recoilKick = 1.4f;

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

        /// <summary>The muzzle in use: the animated gun's (<see cref="CharacterAnimator.Muzzle"/>), else the assigned one.</summary>
        public Transform Muzzle
        {
            get
            {
                if (_animator != null)
                {
                    // An imported humanoid holds a real launcher; cube characters fire from their rifle tip.
                    Transform launcherMuzzle = _animator.LauncherMuzzle;
                    if (launcherMuzzle != null) return launcherMuzzle;
                    Transform gunMuzzle = _animator.Muzzle;
                    if (gunMuzzle != null) return gunMuzzle;
                }
                return _muzzle;
            }
        }

        /// <summary>
        /// Where a rocket starts: the muzzle (at the shot height above the owner's feet), pulled back toward the body
        /// until it is clear of walls. Without an owner: the raw muzzle.
        /// </summary>
        public Vector3 MuzzlePosition
        {
            get
            {
                Transform muzzle = Muzzle;
                Vector3 raw = muzzle != null ? muzzle.position : transform.position + Vector3.up * _fallbackMuzzleHeight;
                if (_owner == null) return raw;
                Vector3 feet = _owner.transform.position;
                if (_shotHeight > 0f) raw.y = feet.y + _shotHeight;
                return WeaponAim.ClearSpawnPoint(feet, raw, _projectileRadius, _hitMask, _ownerRoot);
            }
        }

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
            if (_animator == null) _animator = GetComponentInParent<CharacterAnimator>();
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

        /// <summary>
        /// Launches a rocket from <see cref="MuzzlePosition"/> toward <paramref name="aimPoint"/> (flat, at muzzle
        /// height), so it flies to the cursor (GDD §4). Uses <paramref name="fallbackDirection"/> (the body aim direction)
        /// when the aim point is too close to the muzzle or far off to its side, and launches from the body axis when the
        /// aim point is point-blank (see <see cref="WeaponAim.ResolveShot"/>). Uses one charge.
        /// </summary>
        public bool TryFireAt(Vector3 aimPoint, Vector3 fallbackDirection)
        {
            if (!isActiveAndEnabled || IsBlocked || _charges <= 0) return false;
            if (_animator != null) _animator.ShowLauncher(); // launcher into the hands before its muzzle is read
            Vector3 body = _owner != null ? _owner.transform.position : transform.position;
            WeaponAim.ResolveShot(body, MuzzlePosition, aimPoint, fallbackDirection, _minConvergeDistance,
                _maxConvergeAngle, out Vector3 origin, out Vector3 direction);
            return TryFire(origin, direction);
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

            if (_animator != null) _animator.Kick(_recoilKick);
            Launched?.Invoke(this);
            ImpactBus.Raise(new ImpactEvent
            {
                kind = ImpactKind.MuzzleFire,
                position = origin,
                normal = direction,
                direction = direction,
                intensity = _launchIntensity,
                surface = SurfaceType.Default,
                hitTransform = Muzzle,
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
