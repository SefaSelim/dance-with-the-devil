using System.Collections.Generic;
using UnityEngine;

namespace DanceWithTheDevil
{
    /// <summary>Called when a projectile in detonate mode touches a collider or reaches max range.</summary>
    public delegate void ProjectileDetonateHandler(Projectile projectile, Vector3 point, Vector3 normal);

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
        public float HitIntensity;
        public float WallHitIntensity;

        /// <summary>If set: detonate mode. No direct damage; calls this on first contact or at max range.</summary>
        public ProjectileDetonateHandler OnDetonate;
    }

    /// <summary>
    /// Non-physics projectile: sphere-casts along its velocity every frame (no Rigidbody, no collider of its own).
    /// Bullet mode damages IDamageables (pierce count), stops on anything else (walls).
    /// Detonate mode calls back on the first contact or at max range. Returns itself to its <see cref="ProjectilePool"/>.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class Projectile : MonoBehaviour
    {
        private const int MaxHits = 16;

        [Tooltip("Visual child stretched along travel (z). Optional.")]
        [SerializeField] private Transform _visual;
        [Tooltip("Visual length = speed × this (seconds), at least the thickness.")]
        [SerializeField, Min(0f)] private float _stretchTime = 0.02f;
        [Tooltip("Visual thickness = hit diameter × this.")]
        [SerializeField, Min(0.01f)] private float _thicknessScale = 1f;

        private readonly RaycastHit[] _hits = new RaycastHit[MaxHits];
        private readonly List<IDamageable> _alreadyHit = new List<IDamageable>(4);

        private ProjectilePool _pool;
        private ProjectileLaunch _launch;
        private Vector3 _direction;
        private float _travelled;
        private int _pierceLeft;
        private bool _active;

        public bool IsActive => _active;
        public Vector3 Direction => _direction;
        public float Speed => _launch.Speed;
        public GameObject Source => _launch.Source;
        public Transform Visual
        {
            get => _visual;
            set => _visual = value;
        }

        internal void BindPool(ProjectilePool pool) => _pool = pool;

        public void Launch(in ProjectileLaunch launch)
        {
            _launch = launch;
            _direction = launch.Direction.sqrMagnitude > 1e-8f ? launch.Direction.normalized : Vector3.forward;
            _travelled = 0f;
            _pierceLeft = Mathf.Max(0, launch.PierceCount);
            _alreadyHit.Clear();
            _active = true;

            transform.SetPositionAndRotation(launch.Origin, Quaternion.LookRotation(_direction, Vector3.up));
            UpdateVisual();
        }

        private void OnDisable()
        {
            _active = false;
            _launch = default;
        }

        private void Update()
        {
            if (!_active) return;

            float step = _launch.Speed * Time.deltaTime;
            float remaining = _launch.MaxRange - _travelled;
            bool reachesEnd = step >= remaining;
            if (reachesEnd) step = Mathf.Max(0f, remaining);

            if (step > 0f && Sweep(step)) return;

            transform.position += _direction * step;
            _travelled += step;

            if (reachesEnd)
            {
                if (_launch.OnDetonate != null) Detonate(transform.position, -_direction);
                else Despawn();
            }
        }

        /// <summary>Casts this frame's step. Returns true if the projectile ended.</summary>
        private bool Sweep(float step)
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
                if (_launch.IgnoreRoot != null && collider.transform.IsChildOf(_launch.IgnoreRoot)) continue;

                // Overlaps at the cast start report distance 0 and no usable point/normal.
                bool initialOverlap = hit.distance <= 0f;
                Vector3 point = initialOverlap ? origin : hit.point;
                Vector3 normal = initialOverlap ? -_direction : hit.normal;
                Vector3 centerAtHit = origin + _direction * hit.distance;

                if (_launch.OnDetonate != null)
                {
                    transform.position = centerAtHit;
                    Detonate(point, normal);
                    return true;
                }

                IDamageable target = collider.GetComponentInParent<IDamageable>();
                if (target == null)
                {
                    RaiseImpact(collider, point, normal, _launch.WallHitIntensity);
                    transform.position = centerAtHit;
                    Despawn();
                    return true;
                }

                if (_alreadyHit.Contains(target)) continue;
                _alreadyHit.Add(target);

                target.TakeDamage(new DamageInfo(_launch.Damage, point, _direction, _launch.Source, _launch.Kind));
                RaiseImpact(collider, point, normal, _launch.HitIntensity);

                if (_pierceLeft <= 0)
                {
                    transform.position = centerAtHit;
                    Despawn();
                    return true;
                }
                _pierceLeft--;
            }
            return false;
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
