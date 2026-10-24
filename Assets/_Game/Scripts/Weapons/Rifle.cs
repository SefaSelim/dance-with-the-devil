using UnityEngine;

namespace DanceWithTheDevil
{
    /// <summary>
    /// The rifle (GDD §5 Silahlar: 10 damage, 6 shots/s while held, 40 m/s, 25 m, no pierce, unlimited ammo).
    /// Blocked while the owner dodges or the machete swings (GDD §4, §5).
    /// Contracts (GDD §8) use <see cref="DamageMultiplier"/> and <see cref="PierceCount"/>. Whisper modules (GDD §7,
    /// decision K-7: only the rifle) are switched by <see cref="PlayerModules"/> through <see cref="TripleShot"/>,
    /// <see cref="RecoilSpeed"/>, <see cref="EchoEnabled"/>, <see cref="ModuleDamageMultiplier"/>,
    /// <see cref="ModuleFireRateMultiplier"/> and <see cref="ModuleSpreadDegrees"/> (Hız); the two damage multipliers
    /// stack by product (GDD §8 Birikme kuralları), and so do the two fire-rate multipliers (contract
    /// <see cref="AttackSpeedMultiplier"/> × module; production/design/combo-haste.md §3).
    /// Aiming (GDD §4): <see cref="TryFireAt"/> fires from the muzzle toward the aim point, so shots converge on the
    /// cursor; the spawn point is pulled back out of walls (<see cref="WeaponAim.ClearSpawnPoint"/>). The muzzle is the
    /// gun in Defne's hand (<see cref="CharacterAnimator.Muzzle"/>) when there is one; each salvo kicks that gun.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class Rifle : MonoBehaviour
    {
        [Header("References")]
        [Tooltip("Bullet pool. If empty, one is added to this GameObject (runtime cubes).")]
        [SerializeField] private ProjectilePool _pool;
        [Tooltip("Material for the runtime bullet cubes when the pool is created automatically.")]
        [SerializeField] private Material _projectileMaterial;
        [Tooltip("Bullet spawn point when the character has no animated gun (CharacterAnimator.Muzzle wins). If empty: this transform + muzzle height.")]
        [SerializeField] private Transform _muzzle;
        [Tooltip("Fallback muzzle height above this transform when no muzzle is set.")]
        [SerializeField] private float _fallbackMuzzleHeight = 1f;
        [Tooltip("Owner, for the dodge block and ignoring own colliders. If empty, found in parents.")]
        [SerializeField] private PlayerController _owner;
        [Tooltip("Machete, for the swing block. If empty, found in parents.")]
        [SerializeField] private Machete _machete;
        [Tooltip("Character animator holding the gun (muzzle and recoil kick). If empty, found in parents.")]
        [SerializeField] private CharacterAnimator _animator;

        [Header("Aim (GDD §4)")]
        [Tooltip("With an owner, bullets leave at this height above the owner's feet (GDD: muzzle height 1.0 m); the gun model only sets where they start sideways. 0 = the muzzle's own height.")]
        [SerializeField, Min(0f)] private float _shotHeight = 1f;
        [Tooltip("If the aim point is closer than this to the muzzle (flat metres), shoot along the body aim direction instead.")]
        [SerializeField, Min(0f)] private float _minConvergeDistance = 1f;
        [Tooltip("If the muzzle-to-aim direction differs from the body aim direction by more than this (degrees), shoot along the body aim direction.")]
        [SerializeField, Range(0f, 180f)] private float _maxConvergeAngle = 60f;
        [Tooltip("Visual recoil kick passed to CharacterAnimator.Kick per salvo (0..2).")]
        [SerializeField, Range(0f, 2f)] private float _recoilKick = 0.5f;

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
        [Tooltip("After a bounce the bullet may travel at most this fraction of its remaining range (design: 0.5).")]
        [SerializeField, Range(0f, 1f)] private float _echoRangeAfterBounce = 0.5f;

        [Header("Critical hits (crit-difficulty design)")]
        [Tooltip("Base crit chance per bullet, rolled at fire time, 0..1 (design: 0.05).")]
        [SerializeField, Range(0f, 1f)] private float _baseCritChance = CritRoll.DefaultChance;
        [Tooltip("Damage multiplier of a crit (design: 2).")]
        [SerializeField, Min(1f)] private float _critMultiplier = CritRoll.DefaultMultiplier;

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
        private float _attackSpeedMultiplier = 1f;
        private float _moduleFireRateMultiplier = 1f;
        private float _moduleSpreadDegrees;
        private float _moduleCritChance;
        private float _contractCritChance;
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

        /// <summary>Fire-rate multiplier from contracts (Contract 4: ×1.25). Min 0.01. Stacks by product with the module one.</summary>
        public float AttackSpeedMultiplier
        {
            get => _attackSpeedMultiplier;
            set => _attackSpeedMultiplier = Mathf.Max(0.01f, value);
        }

        /// <summary>Fire-rate multiplier owned by the module system (Hız ×1.4). Min 0.01. Set only by <see cref="PlayerModules"/>.</summary>
        public float ModuleFireRateMultiplier
        {
            get => _moduleFireRateMultiplier;
            set => _moduleFireRateMultiplier = Mathf.Max(0.01f, value);
        }

        /// <summary>
        /// Random yaw of every bullet, uniform in ±this many degrees around its own direction (Hız cost: 6; with Triple
        /// Shot each bullet rolls its own on top of its ±12°). 0 = none. Set only by <see cref="PlayerModules"/>.
        /// </summary>
        public float ModuleSpreadDegrees
        {
            get => _moduleSpreadDegrees;
            set => _moduleSpreadDegrees = Mathf.Clamp(value, 0f, 45f);
        }

        /// <summary>Base crit chance (serialized 0.05). Settable for tests. Clamped 0..1.</summary>
        public float BaseCritChance
        {
            get => _baseCritChance;
            set => _baseCritChance = Mathf.Clamp01(value);
        }

        /// <summary>Crit chance added by contracts (Contract 3: +0.15). Additive. Set by <see cref="DealEffects"/>.</summary>
        public float ContractCritChance
        {
            get => _contractCritChance;
            set => _contractCritChance = Mathf.Max(0f, value);
        }

        /// <summary>Damage multiplier of a crit (x2).</summary>
        public float CritMultiplier => _critMultiplier;

        /// <summary>Crit chance from the module system (Keskin Göz +0.2). Set only by <see cref="PlayerModules"/>.</summary>
        public float ModuleCritChance
        {
            get => _moduleCritChance;
            set => _moduleCritChance = Mathf.Max(0f, value);
        }

        /// <summary>Crit chance per bullet (rolled at fire time): base + module + contract, clamped 0..1.</summary>
        public float CritChance => CritRoll.Combine(_baseCritChance, _moduleCritChance, _contractCritChance);

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
        /// <summary>Base shots per second (GDD: 6), without multipliers.</summary>
        public float FireRate => _fireRate;
        /// <summary>Shots per second with every multiplier (contracts × modules).</summary>
        public float EffectiveFireRate => _fireRate * _attackSpeedMultiplier * _moduleFireRateMultiplier;
        /// <summary>Seconds between salvos while Fire is held.</summary>
        public float FireInterval => 1f / EffectiveFireRate;

        /// <summary>The muzzle in use: the animated gun's (<see cref="CharacterAnimator.Muzzle"/>), else the assigned one.</summary>
        public Transform Muzzle
        {
            get
            {
                if (_animator != null)
                {
                    Transform gunMuzzle = _animator.Muzzle;
                    if (gunMuzzle != null) return gunMuzzle;
                }
                return _muzzle;
            }
        }

        /// <summary>
        /// Where a bullet starts: the muzzle (at <see cref="_shotHeight"/> above the owner's feet), pulled back toward
        /// the body until it is clear of walls. Without an owner: the raw muzzle.
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

        /// <summary>True while dodging or swinging the machete.</summary>
        public bool IsBlocked => (_owner != null && _owner.IsDodging) || (_machete != null && _machete.IsSwinging);

        /// <summary>True if a shot would leave now (enabled, not blocked, fire rate allows).</summary>
        public bool IsReady => isActiveAndEnabled && !IsBlocked && Time.time >= _nextFireTime;

        /// <summary>Raised once per salvo (audio). Bool: Triple Shot salvo.</summary>
        public static event System.Action<Rifle, bool> Fired;

        private void Awake()
        {
            if (_owner == null) _owner = GetComponentInParent<PlayerController>();
            if (_machete == null) _machete = GetComponentInParent<Machete>();
            if (_animator == null) _animator = GetComponentInParent<CharacterAnimator>();
            _ownerRoot = _owner != null ? _owner.transform : transform.root;

            if (_pool == null)
            {
                _pool = gameObject.AddComponent<ProjectilePool>();
                _pool.Configure(null, _projectileMaterial);
            }
        }

        /// <summary>
        /// Fires one salvo from <see cref="MuzzlePosition"/> toward <paramref name="aimPoint"/> (flat, at muzzle height),
        /// so the shots converge on the cursor (GDD §4). Uses <paramref name="fallbackDirection"/> (the body aim
        /// direction) when the aim point is too close to the muzzle or far off to its side, and fires from the body axis
        /// when the aim point is point-blank (see <see cref="WeaponAim.ResolveShot"/>). Call every frame while Fire is
        /// held; no physics query runs while the rifle is not ready.
        /// </summary>
        public bool TryFireAt(Vector3 aimPoint, Vector3 fallbackDirection)
        {
            if (!IsReady) return false;
            Vector3 body = _owner != null ? _owner.transform.position : transform.position;
            WeaponAim.ResolveShot(body, MuzzlePosition, aimPoint, fallbackDirection, _minConvergeDistance,
                _maxConvergeAngle, out Vector3 origin, out Vector3 direction);
            return TryFire(origin, direction);
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
            float interval = FireInterval;
            _nextFireTime = now - _nextFireTime < interval ? _nextFireTime + interval : now + interval;

            direction.Normalize();
            GameObject source = _owner != null ? _owner.gameObject : gameObject;
            bool isPlayer = _owner != null;
            bool echo = _echoToken != null;

            var launch = new ProjectileLaunch
            {
                Origin = origin,
                Direction = Stray(direction),
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
                BounceRangeScale = echo ? _echoRangeAfterBounce : 0f,
                CritMultiplier = _critMultiplier,
            };
            // Crit: one roll per bullet at fire time; each Triple Shot bullet rolls on its own (crit-difficulty §2).
            float critChance = CritChance;
            launch.IsCrit = CritRoll.Roll(critChance);
            _pool.Get().Launch(launch);

            if (_tripleShot)
            {
                launch.Direction = Stray(Quaternion.AngleAxis(-_tripleSpreadDegrees, Vector3.up) * direction);
                launch.IsCrit = CritRoll.Roll(critChance);
                _pool.Get().Launch(launch);
                launch.Direction = Stray(Quaternion.AngleAxis(_tripleSpreadDegrees, Vector3.up) * direction);
                launch.IsCrit = CritRoll.Roll(critChance);
                _pool.Get().Launch(launch);
            }

            // One push per salvo, never per bullet (GDD §7 Üçlü Atış).
            if (_recoilSpeed > 0f && _owner != null) _owner.AddExternalVelocity(-direction * _recoilSpeed);
            if (_animator != null) _animator.Kick(_recoilKick);

            Fired?.Invoke(this, _tripleShot);
            ImpactBus.Raise(new ImpactEvent
            {
                kind = ImpactKind.MuzzleFire,
                position = origin,
                normal = direction,
                direction = direction,
                intensity = _muzzleIntensity,
                surface = SurfaceType.Default,
                hitTransform = Muzzle,
                instigator = source,
                instigatorIsLocalPlayer = isPlayer,
            });
            return true;
        }

        // Hız cost: each bullet leaves up to ±_moduleSpreadDegrees off its direction (flat yaw). Recoil, muzzle flash
        // and the Triple Shot fan stay on the true aim.
        private Vector3 Stray(Vector3 direction)
        {
            if (_moduleSpreadDegrees <= 0f) return direction;
            return Quaternion.AngleAxis(Random.Range(-_moduleSpreadDegrees, _moduleSpreadDegrees), Vector3.up) * direction;
        }
    }
}
