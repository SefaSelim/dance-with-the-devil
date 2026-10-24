using UnityEngine;

namespace DanceWithTheDevil
{
    /// <summary>
    /// The rifle (GDD §5 Silahlar: 10 damage, 6 shots/s while held, 40 m/s, 25 m, no pierce, unlimited ammo).
    /// Blocked while the owner dodges or the machete swings (GDD §4, §5). Whisper modules (GDD §7) will later
    /// change it through <see cref="DamageMultiplier"/> and <see cref="PierceCount"/>.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class Rifle : MonoBehaviour
    {
        [Header("References")]
        [Tooltip("Bullet pool. If empty, one is added to this GameObject (runtime cubes).")]
        [SerializeField] private ProjectilePool _pool;
        [Tooltip("Material for the runtime bullet cubes when the pool is created automatically.")]
        [SerializeField] private Material _projectileMaterial;
        [Tooltip("Bullet spawn point. If empty: this transform + muzzle height.")]
        [SerializeField] private Transform _muzzle;
        [Tooltip("Fallback muzzle height above this transform when no muzzle is set.")]
        [SerializeField] private float _fallbackMuzzleHeight = 1f;
        [Tooltip("Owner, for the dodge block and ignoring own colliders. If empty, found in parents.")]
        [SerializeField] private PlayerController _owner;
        [Tooltip("Machete, for the swing block. If empty, found in parents.")]
        [SerializeField] private Machete _machete;

        [Header("Stats (GDD §5)")]
        [Tooltip("Damage per bullet.")]
        [SerializeField, Min(0f)] private float _damage = 10f;
        [Tooltip("Shots per second while Fire is held.")]
        [SerializeField, Min(0.01f)] private float _fireRate = 6f;
        [Tooltip("Bullet speed in m/s.")]
        [SerializeField, Min(0.1f)] private float _projectileSpeed = 40f;
        [Tooltip("Bullet range in metres.")]
        [SerializeField, Min(0.1f)] private float _range = 25f;
        [Tooltip("Bullet hit radius in metres.")]
        [SerializeField, Min(0.01f)] private float _projectileRadius = 0.12f;
        [Tooltip("Extra enemies a bullet passes through. 0 = stops on the first.")]
        [SerializeField, Min(0)] private int _pierceCount;
        [Tooltip("Layers bullets collide with.")]
        [SerializeField] private LayerMask _hitMask = ~0;

        [Header("Feel")]
        [Tooltip("ImpactBus MuzzleFire intensity per shot.")]
        [SerializeField, Range(0f, 1f)] private float _muzzleIntensity = 0.15f;
        [Tooltip("ImpactBus Bullet intensity on a damageable hit.")]
        [SerializeField, Range(0f, 1f)] private float _hitIntensity = 0.25f;
        [Tooltip("ImpactBus Bullet intensity on a wall hit.")]
        [SerializeField, Range(0f, 1f)] private float _wallHitIntensity = 0.1f;

        private float _nextFireTime = float.NegativeInfinity;
        private float _damageMultiplier = 1f;
        private Transform _ownerRoot;

        /// <summary>Multiplies bullet damage (modules). Min 0.</summary>
        public float DamageMultiplier
        {
            get => _damageMultiplier;
            set => _damageMultiplier = Mathf.Max(0f, value);
        }

        /// <summary>Extra enemies a bullet passes through (modules). Min 0.</summary>
        public int PierceCount
        {
            get => _pierceCount;
            set => _pierceCount = Mathf.Max(0, value);
        }

        public float Damage => _damage * _damageMultiplier;
        public float FireRate => _fireRate;
        public Transform Muzzle => _muzzle;
        public Vector3 MuzzlePosition =>
            _muzzle != null ? _muzzle.position : transform.position + Vector3.up * _fallbackMuzzleHeight;
        /// <summary>True while dodging or swinging the machete.</summary>
        public bool IsBlocked => (_owner != null && _owner.IsDodging) || (_machete != null && _machete.IsSwinging);

        private void Awake()
        {
            if (_owner == null) _owner = GetComponentInParent<PlayerController>();
            if (_machete == null) _machete = GetComponentInParent<Machete>();
            _ownerRoot = _owner != null ? _owner.transform : transform.root;

            if (_pool == null)
            {
                _pool = gameObject.AddComponent<ProjectilePool>();
                _pool.Configure(null, _projectileMaterial);
            }
        }

        /// <summary>
        /// Fires one bullet from <paramref name="origin"/> along <paramref name="direction"/> if the fire rate allows
        /// and nothing blocks it. Call every frame while Fire is held.
        /// </summary>
        public bool TryFire(Vector3 origin, Vector3 direction)
        {
            if (!isActiveAndEnabled || IsBlocked) return false;
            if (direction.sqrMagnitude < 1e-6f) return false;

            float now = Time.time;
            if (now < _nextFireTime) return false;

            // Held fire keeps an exact cadence; a fresh press starts a new one.
            float interval = 1f / _fireRate;
            _nextFireTime = now - _nextFireTime < interval ? _nextFireTime + interval : now + interval;

            direction.Normalize();
            GameObject source = _owner != null ? _owner.gameObject : gameObject;
            bool isPlayer = _owner != null;

            Projectile projectile = _pool.Get();
            projectile.Launch(new ProjectileLaunch
            {
                Origin = origin,
                Direction = direction,
                Speed = _projectileSpeed,
                MaxRange = _range,
                Radius = _projectileRadius,
                HitMask = _hitMask,
                IgnoreRoot = _ownerRoot,
                Source = source,
                SourceIsLocalPlayer = isPlayer,
                Damage = Damage,
                Kind = DamageKind.Bullet,
                PierceCount = _pierceCount,
                HitIntensity = _hitIntensity,
                WallHitIntensity = _wallHitIntensity,
            });

            ImpactBus.Raise(new ImpactEvent
            {
                kind = ImpactKind.MuzzleFire,
                position = origin,
                normal = direction,
                direction = direction,
                intensity = _muzzleIntensity,
                surface = SurfaceType.Default,
                hitTransform = _muzzle,
                instigator = source,
                instigatorIsLocalPlayer = isPlayer,
            });
            return true;
        }
    }
}
