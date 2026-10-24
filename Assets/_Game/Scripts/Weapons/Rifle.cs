using UnityEngine;

namespace DanceWithTheDevil
{
    /// <summary>
    /// The rifle (GDD §5 Silahlar: 10 damage, 6 shots/s while held, 40 m/s, 25 m, no pierce, unlimited ammo).
    /// Blocked while the owner dodges or the machete swings (GDD §4, §5).
    /// Contracts (GDD §8) use <see cref="DamageMultiplier"/> and <see cref="PierceCount"/>. Whisper modules (GDD §7,
    /// decision K-7: only the rifle) are switched by <see cref="PlayerModules"/> through <see cref="TripleShot"/>,
    /// <see cref="RecoilSpeed"/>, <see cref="EchoEnabled"/> and <see cref="ModuleDamageMultiplier"/>; the two damage
    /// multipliers stack by product (GDD §8 Birikme kuralları).
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

        [Header("Echo module (GDD §7)")]
        [Tooltip("Wall bounces per bullet while Echo is equipped (GDD: 1; the second wall ends it).")]
        [SerializeField, Min(0)] private int _echoBounces = 1;
        [Tooltip("Damage to the player from an own bounced bullet, as a fraction of its damage (GDD: half).")]
        [SerializeField, Range(0f, 1f)] private float _echoSelfDamageScale = 0.5f;
        [Tooltip("Tint of a bullet after it bounced (Echo purple #9B5CFF).")]
        [SerializeField] private Color _echoTint = new Color(0x9B / 255f, 0x5C / 255f, 0xFF / 255f, 1f);

        [Header("Feel")]
        [Tooltip("ImpactBus MuzzleFire intensity per shot.")]
        [SerializeField, Range(0f, 1f)] private float _muzzleIntensity = 0.15f;
        [Tooltip("ImpactBus Bullet intensity on a damageable hit.")]
        [SerializeField, Range(0f, 1f)] private float _hitIntensity = 0.25f;
        [Tooltip("ImpactBus Bullet intensity on a wall hit.")]
        [SerializeField, Range(0f, 1f)] private float _wallHitIntensity = 0.1f;

        private float _nextFireTime = float.NegativeInfinity;
        private float _damageMultiplier = 1f;
        private float _moduleDamageMultiplier = 1f;
        private bool _tripleShot;
        private float _tripleSpreadDegrees = 12f;
        private float _recoilSpeed;
        private ProjectileBounceToken _echoToken;
        private Transform _ownerRoot;

        /// <summary>Multiplies bullet damage (contracts). Min 0. Stacks by product with <see cref="ModuleDamageMultiplier"/>.</summary>
        public float DamageMultiplier
        {
            get => _damageMultiplier;
            set => _damageMultiplier = Mathf.Max(0f, value);
        }

        /// <summary>Damage multiplier owned by the module system (Density ×1.6). Min 0. Set only by <see cref="PlayerModules"/>.</summary>
        public float ModuleDamageMultiplier
        {
            get => _moduleDamageMultiplier;
            set => _moduleDamageMultiplier = Mathf.Max(0f, value);
        }

        /// <summary>Extra enemies a bullet passes through (contracts). Min 0.</summary>
        public int PierceCount
        {
            get => _pierceCount;
            set => _pierceCount = Mathf.Max(0, value);
        }

        /// <summary>Triple Shot: each shot is a centre bullet plus two at ±<see cref="TripleSpreadDegrees"/>, each full damage.</summary>
        public bool TripleShot
        {
            get => _tripleShot;
            set => _tripleShot = value;
        }

        /// <summary>Side-bullet angle of Triple Shot in degrees (GDD: 12).</summary>
        public float TripleSpreadDegrees
        {
            get => _tripleSpreadDegrees;
            set => _tripleSpreadDegrees = Mathf.Clamp(value, 0f, 90f);
        }

        /// <summary>
        /// Speed (m/s) pushed onto the owner opposite the aim once per salvo (Triple Shot cost). 0 = none. The owner's
        /// <see cref="PlayerController.AddExternalVelocity"/> caps and decays it.
        /// </summary>
        public float RecoilSpeed
        {
            get => _recoilSpeed;
            set => _recoilSpeed = Mathf.Max(0f, value);
        }

        /// <summary>
        /// Echo: bullets bounce off walls and may then hurt the owner. Turning it off also disarms bullets in flight
        /// (no further bounces, bounced ones harmless to the owner).
        /// </summary>
        public bool EchoEnabled
        {
            get => _echoToken != null;
            set
            {
                if (value == (_echoToken != null)) return;
                if (value)
                {
                    _echoToken = new ProjectileBounceToken(); // only on toggle, never per shot
                }
                else
                {
                    _echoToken.Active = false;
                    _echoToken = null;
                }
            }
        }

        public float BaseDamage => _damage;
        /// <summary>Damage per bullet with every multiplier (contracts × modules).</summary>
        public float Damage => _damage * _damageMultiplier * _moduleDamageMultiplier;
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
        /// Fires one salvo (one bullet, or three with Triple Shot) from <paramref name="origin"/> along
        /// <paramref name="direction"/> if the fire rate allows and nothing blocks it. Call every frame while Fire is held.
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
            bool echo = _echoToken != null;

            var launch = new ProjectileLaunch
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
                Bounces = echo ? _echoBounces : 0,
                BounceToken = _echoToken,
                BouncedSelfDamageScale = echo ? _echoSelfDamageScale : 0f,
                HasBounceTint = echo,
                BounceTint = _echoTint,
            };
            _pool.Get().Launch(launch);

            if (_tripleShot)
            {
                launch.Direction = Quaternion.AngleAxis(-_tripleSpreadDegrees, Vector3.up) * direction;
                _pool.Get().Launch(launch);
                launch.Direction = Quaternion.AngleAxis(_tripleSpreadDegrees, Vector3.up) * direction;
                _pool.Get().Launch(launch);
            }

            // One push per salvo, never per bullet (GDD §7 Üçlü Atış).
            if (_recoilSpeed > 0f && _owner != null) _owner.AddExternalVelocity(-direction * _recoilSpeed);

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
