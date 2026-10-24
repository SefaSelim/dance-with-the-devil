using UnityEngine;

namespace DanceWithTheDevil
{
    /// <summary>
    /// Textured, layered particle effects (fire, smoke, sparks, dust, scorch) for the gunfire and the rocket, built by
    /// <c>Dance/VFX/Build Effects</c> from the Kenney Particle Pack (CC0). Listens to the same <see cref="ImpactBus"/>
    /// event as <see cref="PixelImpactFeedback"/> and plays on that frame: rifle muzzle flash, rocket launch (front
    /// flame + backblast), rocket explosion (flash, fireball, smoke, dirt, embers, ground dust ring, scorch) and the
    /// bullet impact puff. <see cref="PixelImpactFeedback"/> keeps the light, shake and hitstop of those events and
    /// leaves the flash shape and cube burst to this component for the kinds in <see cref="Covers"/>.
    /// Pooling: a fixed round-robin set of instances per effect; a reused instance is cleared and replayed.
    /// </summary>
    // Late, so the muzzle flash is snapped to the gun after WeaponHold placed it and CharacterAnimator kicked it.
    [DefaultExecutionOrder(1000)]
    [DisallowMultipleComponent]
    public sealed class RealisticVfx : MonoBehaviour
    {
        public static RealisticVfx Instance { get; private set; }

        [Tooltip("Rifle muzzle flash (weapon-forward along +Z).")]
        [SerializeField] private GameObject _muzzlePrefab;
        [Tooltip("Rocket launch: front flame and backblast (+Z = launch direction).")]
        [SerializeField] private GameObject _launchPrefab;
        [Tooltip("Rocket explosion (+Y = up).")]
        [SerializeField] private GameObject _explosionPrefab;
        [Tooltip("Bullet impact puff (+Z = surface normal).")]
        [SerializeField] private GameObject _impactPrefab;
        [Tooltip("MuzzleFire events at or above this intensity are rocket launches (rifle 0.15, rocket 0.35).")]
        [SerializeField, Range(0f, 1f)] private float _launchIntensity = 0.3f;
        [Tooltip("Global size multiplier (tuning).")]
        [SerializeField, Min(0.1f)] private float _scale = 1f;
        [Tooltip("Seconds a rifle flash stays attached to the gun's muzzle (its core and flame are local-space).")]
        [SerializeField, Min(0f)] private float _muzzleFollowSeconds = 0.12f;
        [Tooltip("Metres in front of the Muzzle point where the flash is centred (the flash starts at the bore).")]
        [SerializeField] private float _muzzleForwardOffset = 0.03f;

        // One follow slot per pooled muzzle instance.
        private Transform[] _followTarget;
        private float[] _followUntil;

        private Pool _muzzles;
        private Pool _launches;
        private Pool _explosions;
        private Pool _impacts;

        private sealed class Pool
        {
            private readonly Transform[] _roots;
            private readonly ParticleSystem[] _systems;
            private int _next;

            public Pool(GameObject prefab, int count, Transform parent)
            {
                _roots = new Transform[count];
                _systems = new ParticleSystem[count];
                for (int i = 0; i < count; i++)
                {
                    GameObject instance = Instantiate(prefab, parent);
                    instance.name = prefab.name + "_" + i;
                    _roots[i] = instance.transform;
                    _systems[i] = instance.GetComponent<ParticleSystem>();
                }
            }

            public int Count => _roots.Length;
            public Transform Root(int index) => _roots[index];

            /// <summary>Plays the next instance; returns its index, or -1 if it has no particle system.</summary>
            public int Play(Vector3 position, Quaternion rotation, float scale)
            {
                int index = _next;
                Transform root = _roots[index];
                ParticleSystem system = _systems[index];
                _next = (_next + 1) % _roots.Length;
                if (system == null) return -1;
                system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                root.SetPositionAndRotation(position, rotation);
                root.localScale = Vector3.one * scale;
                system.Play(true);
                return index;
            }
        }

        /// <summary>True when this component draws the flash and burst of <paramref name="e"/> (not the revive wave).</summary>
        public bool Covers(in ImpactEvent e)
        {
            switch (e.kind)
            {
                case ImpactKind.MuzzleFire: return e.intensity >= _launchIntensity ? _launches != null : _muzzles != null;
                case ImpactKind.Explosion: return _explosions != null && !IsRevive(e);
                default: return false;
            }
        }

        private void Awake()
        {
            Instance = this;
            if (_muzzlePrefab != null)
            {
                _muzzles = new Pool(_muzzlePrefab, 6, transform);
                _followTarget = new Transform[_muzzles.Count];
                _followUntil = new float[_muzzles.Count];
            }
            if (_launchPrefab != null) _launches = new Pool(_launchPrefab, 3, transform);
            if (_explosionPrefab != null) _explosions = new Pool(_explosionPrefab, 4, transform);
            if (_impactPrefab != null) _impacts = new Pool(_impactPrefab, 10, transform);
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void OnEnable() => ImpactBus.OnImpact += Handle;
        private void OnDisable() => ImpactBus.OnImpact -= Handle;

        private void Handle(ImpactEvent e)
        {
            switch (e.kind)
            {
                case ImpactKind.MuzzleFire:
                {
                    Quaternion facing = LookAlong(e.normal.sqrMagnitude > 1e-6f ? e.normal : e.direction);
                    if (e.intensity >= _launchIntensity)
                    {
                        _launches?.Play(e.position, facing, _scale);
                        break;
                    }
                    if (_muzzles == null) break;
                    // The shot itself leaves from a flattened, wall-safe point; the flash belongs on the gun's bore.
                    Transform muzzle = e.hitTransform;
                    if (muzzle != null && muzzle.gameObject.activeInHierarchy)
                    {
                        int index = _muzzles.Play(MuzzlePoint(muzzle), muzzle.rotation, _scale);
                        if (index >= 0)
                        {
                            _followTarget[index] = muzzle;
                            _followUntil[index] = Time.time + _muzzleFollowSeconds;
                        }
                    }
                    else _muzzles.Play(e.position, facing, _scale);
                    break;
                }
                case ImpactKind.Explosion:
                    if (IsRevive(e)) break;
                    // Bigger blasts (contract 1) grow with their real radius; 2.5 m is the base rocket.
                    float grow = e.radius > 0f ? Mathf.Clamp(e.radius / 2.5f, 0.6f, 1.6f) : 1f;
                    _explosions?.Play(e.position, Quaternion.identity, _scale * grow);
                    break;
                case ImpactKind.Bullet:
                    _impacts?.Play(e.position, LookAlong(e.normal.sqrMagnitude > 1e-6f ? e.normal : -e.direction), _scale);
                    break;
            }
        }

        /// <summary>Keeps fresh rifle flashes on the gun: it moves with the aim, the run and the recoil kick.</summary>
        private void LateUpdate()
        {
            if (_followTarget == null) return;
            float now = Time.time;
            for (int i = 0; i < _followTarget.Length; i++)
            {
                Transform target = _followTarget[i];
                if (target == null) continue;
                if (now > _followUntil[i] || !target.gameObject.activeInHierarchy)
                {
                    _followTarget[i] = null;
                    continue;
                }
                _muzzles.Root(i).SetPositionAndRotation(MuzzlePoint(target), target.rotation);
            }
        }

        private Vector3 MuzzlePoint(Transform muzzle) => muzzle.position + muzzle.forward * _muzzleForwardOffset;

        private static Quaternion LookAlong(Vector3 direction)
        {
            if (direction.sqrMagnitude < 1e-6f) return Quaternion.identity;
            Vector3 up = Mathf.Abs(Vector3.Dot(direction.normalized, Vector3.up)) > 0.95f ? Vector3.forward : Vector3.up;
            return Quaternion.LookRotation(direction, up);
        }

        private static bool IsRevive(in ImpactEvent e) =>
            e.instigator != null && e.instigator.TryGetComponent<DevilDirector>(out _);
    }
}
