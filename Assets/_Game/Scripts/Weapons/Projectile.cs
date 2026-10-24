using System.Collections.Generic;
using UnityEngine;

namespace DanceWithTheDevil
{
    /// <summary>Called when a projectile in detonate mode touches a collider or reaches max range.</summary>
    public delegate void ProjectileDetonateHandler(Projectile projectile, Vector3 point, Vector3 normal);

    /// <summary>
    /// Shared switch for the bounce behaviour of every bullet launched with it (Echo module, MAIN_GDD §7). When the
    /// shooter turns it off, bullets already in flight lose their remaining bounces and their bounced form no longer
    /// hurts the shooter. Allocated only when the behaviour is switched on, never per shot.
    /// </summary>
    public sealed class ProjectileBounceToken
    {
        public bool Active = true;
    }

    /// <summary>Everything a projectile needs for one flight. Built on the stack by the weapon.</summary>
    public struct ProjectileLaunch
    {
        public Vector3 Origin;
        public Vector3 Direction;
        public float Speed;
        public float MaxRange;
        /// <summary>Sphere-cast radius (hit size).</summary>
        public float Radius;
        public LayerMask HitMask;
        /// <summary>Colliders under this root are ignored (the shooter).</summary>
        public Transform IgnoreRoot;
        public GameObject Source;
        public bool SourceIsLocalPlayer;

        // Bullet mode
        public float Damage;
        public DamageKind Kind;
        /// <summary>Extra damageables passed through. 0 = stop on the first damageable.</summary>
        public int PierceCount;
        /// <summary>
        /// Optional (enemy shots): if set, only damageables under this root are hit; other damageables (enemies) are
        /// flown through. Colliders without a damageable (walls) still stop it. Null = hit any damageable.
        /// </summary>
        public Transform DamageOnlyUnder;
        public float HitIntensity;
        public float WallHitIntensity;

        // Bounce (Echo module, MAIN_GDD §7)
        /// <summary>Wall reflections allowed (bullet mode). A wall is any collider without a damageable. 0 = none.</summary>
        public int Bounces;
        /// <summary>Optional switch; when set and inactive, no bounce happens and bounced bullets spare the shooter.</summary>
        public ProjectileBounceToken BounceToken;
        /// <summary>
        /// After its first bounce the bullet stops ignoring <see cref="IgnoreRoot"/> and damages it with
        /// Damage × this. 0 = the shooter stays immune.
        /// </summary>
        public float BouncedSelfDamageScale;
        /// <summary>If true, the visual takes <see cref="BounceTint"/> after the first bounce.</summary>
        public bool HasBounceTint;
        public Color BounceTint;

        /// <summary>If set: detonate mode. No direct damage; calls this on first contact or at max range.</summary>
        public ProjectileDetonateHandler OnDetonate;
    }

    /// <summary>
    /// Non-physics projectile: sphere-casts along its velocity every frame (no Rigidbody, no collider of its own).
    /// Bullet mode damages IDamageables (pierce count), stops on anything else (walls) unless it has bounces left:
    /// then it reflects off the wall (flattened, using the hit normal), keeps its remaining range, can tint its visual
    /// and from then on can hurt its own shooter (Echo, MAIN_GDD §7).
    /// Detonate mode calls back on the first contact or at max range. Returns itself to its <see cref="ProjectilePool"/>.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class Projectile : MonoBehaviour
    {
        private const int MaxHits = 16;
        private const float BounceSkin = 0.02f;
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

        private enum SweepResult
        {
            Continue,
            Ended,
            Bounced,
        }

        [Tooltip("Visual child stretched along travel (z). Optional.")]
        [SerializeField] private Transform _visual;
        [Tooltip("Visual length = speed × this (seconds), at least the thickness.")]
        [SerializeField, Min(0f)] private float _stretchTime = 0.02f;
        [Tooltip("Visual thickness = hit diameter × this.")]
        [SerializeField, Min(0.01f)] private float _thicknessScale = 1f;

        private readonly RaycastHit[] _hits = new RaycastHit[MaxHits];
        private readonly List<IDamageable> _alreadyHit = new List<IDamageable>(4);
        private MaterialPropertyBlock _tintBlock;
        private Renderer _visualRenderer;
        private bool _visualRendererResolved;
        private bool _tinted;

        private ProjectilePool _pool;
        private ProjectileLaunch _launch;
        private Vector3 _direction;
        private float _travelled;
        private int _pierceLeft;
        private bool _active;
        private int _bouncesLeft;
        private bool _bounced;
        private Transform _ignoreRoot;
        private Transform _selfRoot;
        private Collider _lastBounceCollider;

        public bool IsActive => _active;
        /// <summary>True after the first wall bounce of this flight.</summary>
        public bool HasBounced => _bounced;

        /// <summary>Raised on every wall bounce with the bounce point (Echo "çın" audio cue).</summary>
        public static event System.Action<Vector3> Bounced;
        /// <summary>Distance travelled this flight (bounces keep counting toward the max range).</summary>
        public float Travelled => _travelled;
        public Vector3 Direction => _direction;
        public float Speed => _launch.Speed;
        public GameObject Source => _launch.Source;
        public Transform Visual
        {
            get => _visual;
            set
            {
                _visual = value;
                _visualRendererResolved = false;
            }
        }

        internal void BindPool(ProjectilePool pool) => _pool = pool;

        public void Launch(in ProjectileLaunch launch)
        {
            _launch = launch;
            _direction = launch.Direction.sqrMagnitude > 1e-8f ? launch.Direction.normalized : Vector3.forward;
            _travelled = 0f;
            _pierceLeft = Mathf.Max(0, launch.PierceCount);
            _bouncesLeft = Mathf.Max(0, launch.Bounces);
            _bounced = false;
            _ignoreRoot = launch.IgnoreRoot;
            _selfRoot = null;
            _lastBounceCollider = null;
            _alreadyHit.Clear();
            _active = true;
            ClearTint();

            transform.SetPositionAndRotation(launch.Origin, Quaternion.LookRotation(_direction, Vector3.up));
            UpdateVisual();
        }

        private void OnDisable()
        {
            _active = false;
            _launch = default;
            _ignoreRoot = null;
            _selfRoot = null;
            _lastBounceCollider = null;
        }

        private void Update()
        {
            if (!_active) return;

            float step = _launch.Speed * Time.deltaTime;
            float remaining = _launch.MaxRange - _travelled;
            bool reachesEnd = step >= remaining;
            if (reachesEnd) step = Mathf.Max(0f, remaining);

            if (step > 0f)
            {
                SweepResult result = Sweep(step);
                if (result == SweepResult.Ended) return;
                if (result == SweepResult.Bounced)
                {
                    // The rest of this frame's step is dropped; the flight resumes next frame from the wall.
                    if (_travelled >= _launch.MaxRange) Despawn();
                    return;
                }
            }

            transform.position += _direction * step;
            _travelled += step;

            if (reachesEnd)
            {
                if (_launch.OnDetonate != null) Detonate(transform.position, -_direction);
                else Despawn();
            }
        }

        /// <summary>Casts this frame's step: ended, bounced (already placed at the wall) or free to continue.</summary>
        private SweepResult Sweep(float step)
        {
            Vector3 origin = transform.position;
            int count = Physics.SphereCastNonAlloc(origin, _launch.Radius, _direction, _hits, step,
                _launch.HitMask, QueryTriggerInteraction.Ignore);
            SortByDistance(_hits, count);

            for (int i = 0; i < count; i++)
            {
                RaycastHit hit = _hits[i];
                Collider collider = hit.collider;
                if (collider == null) continue;
                if (_ignoreRoot != null && collider.transform.IsChildOf(_ignoreRoot)) continue;

                // Overlaps at the cast start report distance 0 and no usable point/normal.
                bool initialOverlap = hit.distance <= 0f;
                if (initialOverlap && collider == _lastBounceCollider) continue; // still touching the wall it left
                Vector3 point = initialOverlap ? origin : hit.point;
                Vector3 normal = initialOverlap ? -_direction : hit.normal;
                Vector3 centerAtHit = origin + _direction * hit.distance;

                if (_launch.OnDetonate != null)
                {
                    transform.position = centerAtHit;
                    Detonate(point, normal);
                    return SweepResult.Ended;
                }

                IDamageable target = collider.GetComponentInParent<IDamageable>();
                if (target == null)
                {
                    RaiseImpact(collider, point, normal, _launch.WallHitIntensity);
                    if (CanBounce && Bounce(centerAtHit, normal, hit.distance, collider)) return SweepResult.Bounced;
                    transform.position = centerAtHit;
                    Despawn();
                    return SweepResult.Ended;
                }

                float damage = _launch.Damage;
                if (_selfRoot != null && collider.transform.IsChildOf(_selfRoot))
                {
                    // Own bounced bullet (Echo): reduced damage; harmless once the bounce switch is off.
                    if (!SelfHurtActive) continue;
                    damage *= _launch.BouncedSelfDamageScale;
                }
                else if (_launch.DamageOnlyUnder != null && !collider.transform.IsChildOf(_launch.DamageOnlyUnder))
                {
                    continue;
                }

                if (_alreadyHit.Contains(target)) continue;
                _alreadyHit.Add(target);

                target.TakeDamage(new DamageInfo(damage, point, _direction, _launch.Source, _launch.Kind));
                RaiseImpact(collider, point, normal, _launch.HitIntensity);

                if (_pierceLeft <= 0)
                {
                    transform.position = centerAtHit;
                    Despawn();
                    return SweepResult.Ended;
                }
                _pierceLeft--;
            }
            return SweepResult.Continue;
        }

        private bool BounceSwitchOn => _launch.BounceToken == null || _launch.BounceToken.Active;
        private bool CanBounce => _bouncesLeft > 0 && BounceSwitchOn;
        private bool SelfHurtActive => _bounced && _launch.BouncedSelfDamageScale > 0f && BounceSwitchOn;

        /// <summary>Reflects off a wall (flattened). Returns false if the reflection has no horizontal direction.</summary>
        private bool Bounce(Vector3 centerAtHit, Vector3 normal, float distance, Collider wall)
        {
            Vector3 reflected = Vector3.Reflect(_direction, normal);
            reflected.y = 0f;
            if (reflected.sqrMagnitude < 1e-6f) return false;

            _direction = reflected.normalized;
            _travelled += distance;
            _bouncesLeft--;
            _bounced = true;
            _lastBounceCollider = wall;
            _alreadyHit.Clear(); // the reflected bullet may hit an enemy it passed (pierce) on the way in
            if (_launch.BouncedSelfDamageScale > 0f && _launch.IgnoreRoot != null)
            {
                _selfRoot = _launch.IgnoreRoot;
                _ignoreRoot = null;
            }

            Vector3 flatNormal = normal;
            flatNormal.y = 0f;
            flatNormal = flatNormal.sqrMagnitude > 1e-6f ? flatNormal.normalized : _direction;
            transform.SetPositionAndRotation(centerAtHit + flatNormal * BounceSkin,
                Quaternion.LookRotation(_direction, Vector3.up));
            if (_launch.HasBounceTint) SetTint(_launch.BounceTint);
            Bounced?.Invoke(centerAtHit);
            return true;
        }

        private void RaiseImpact(Collider collider, Vector3 point, Vector3 normal, float intensity)
        {
            ImpactBus.Raise(new ImpactEvent
            {
                kind = ImpactKind.Bullet,
                position = point,
                normal = normal,
                direction = _direction,
                intensity = intensity,
                surface = SurfaceType.Default,
                hitTransform = collider.transform,
                instigator = _launch.Source,
                instigatorIsLocalPlayer = _launch.SourceIsLocalPlayer,
            });
        }

        private void Detonate(Vector3 point, Vector3 normal)
        {
            ProjectileDetonateHandler callback = _launch.OnDetonate;
            Despawn();
            callback?.Invoke(this, point, normal);
        }

        /// <summary>Ends the flight and returns to the pool (or deactivates if it has none).</summary>
        public void Despawn()
        {
            if (!_active) return;
            _active = false;
            if (_pool != null) _pool.Release(this);
            else gameObject.SetActive(false);
        }

        /// <summary>Tints the visual (base and emission colour) with a property block. No allocation after the first call.</summary>
        public void SetTint(Color color)
        {
            Renderer visualRenderer = VisualRenderer;
            if (visualRenderer == null) return;
            if (_tintBlock == null) _tintBlock = new MaterialPropertyBlock();
            _tintBlock.Clear();
            _tintBlock.SetColor(BaseColorId, color);
            _tintBlock.SetColor(EmissionColorId, color);
            visualRenderer.SetPropertyBlock(_tintBlock);
            _tinted = true;
        }

        /// <summary>Removes the tint (back to the material colours).</summary>
        public void ClearTint()
        {
            if (!_tinted) return;
            _tinted = false;
            Renderer visualRenderer = VisualRenderer;
            if (visualRenderer == null || _tintBlock == null) return;
            _tintBlock.Clear();
            visualRenderer.SetPropertyBlock(_tintBlock);
        }

        private Renderer VisualRenderer
        {
            get
            {
                if (!_visualRendererResolved)
                {
                    _visualRendererResolved = true;
                    _visualRenderer = _visual != null ? _visual.GetComponentInChildren<Renderer>() : null;
                }
                return _visualRenderer;
            }
        }

        private void UpdateVisual()
        {
            if (_visual == null) return;
            float thickness = Mathf.Max(0.02f, _launch.Radius * 2f * _thicknessScale);
            float length = Mathf.Max(thickness, _launch.Speed * _stretchTime);
            _visual.localRotation = Quaternion.identity;
            _visual.localScale = new Vector3(thickness, thickness, length);
            _visual.localPosition = new Vector3(0f, 0f, -length * 0.5f); // trail behind the head
        }

        private static void SortByDistance(RaycastHit[] hits, int count)
        {
            for (int i = 1; i < count; i++)
            {
                RaycastHit key = hits[i];
                int j = i - 1;
                while (j >= 0 && hits[j].distance > key.distance)
                {
                    hits[j + 1] = hits[j];
                    j--;
                }
                hits[j + 1] = key;
            }
        }
    }
}
