using System.Collections.Generic;
using UnityEngine;

namespace DanceWithTheDevil
{
    /// <summary>
    /// Machete (GDD §5 Silahlar): 35 damage in a 120° arc, 2 m in front of the aim direction. The swing lasts 0.2 s
    /// (the rifle is silent meanwhile, <see cref="IsSwinging"/>), then a 0.5 s cooldown measured from the end of the
    /// swing. Damage is applied once, at swing start, to each target in the arc that is not behind a wall.
    /// Blocked while the owner dodges. Not done yet: the planar cut on the surface it hits (needs voxels).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class Machete : MonoBehaviour
    {
        private const int MaxOverlaps = 128;
        private const int MaxLineHits = 16;

        [Header("References")]
        [Tooltip("Owner, for the dodge block and ignoring own colliders. If empty, found in parents.")]
        [SerializeField] private PlayerController _owner;
        [Tooltip("Optional placeholder visual, shown and rotated through the arc during the swing.")]
        [SerializeField] private Transform _swingVisual;

        [Header("Stats (GDD §5)")]
        [Tooltip("Damage per target.")]
        [SerializeField, Min(0f)] private float _damage = 35f;
        [Tooltip("Arc in degrees, centred on the swing direction.")]
        [SerializeField, Range(1f, 360f)] private float _arcDegrees = 120f;
        [Tooltip("Reach in metres from the swing origin.")]
        [SerializeField, Min(0.1f)] private float _reach = 2f;
        [Tooltip("Swing duration in seconds (rifle blocked meanwhile).")]
        [SerializeField, Min(0.01f)] private float _swingDuration = 0.2f;
        [Tooltip("Cooldown in seconds after the swing ends.")]
        [SerializeField, Min(0f)] private float _cooldown = 0.5f;
        [Tooltip("Swing origin height above this transform (chest/muzzle height).")]
        [SerializeField] private float _originHeight = 1f;
        [Tooltip("Layers that can be hit.")]
        [SerializeField] private LayerMask _hitMask = ~0;
        [Tooltip("Layers that block the blade (walls). Non-damageable colliders on these layers block.")]
        [SerializeField] private LayerMask _obstacleMask = ~0;

        [Header("Feel")]
        [Tooltip("ImpactBus Melee intensity per target hit.")]
        [SerializeField, Range(0f, 1f)] private float _hitIntensity = 0.5f;

        private readonly Collider[] _overlaps = new Collider[MaxOverlaps];
        private readonly RaycastHit[] _lineHits = new RaycastHit[MaxLineHits];
        private readonly HashSet<IDamageable> _hitThisSwing = new HashSet<IDamageable>();
        private Transform _ownerRoot;

        private float _swingStartTime = float.NegativeInfinity;
        private float _swingEndTime = float.NegativeInfinity;
        private float _readyTime = float.NegativeInfinity;
        private Quaternion _swingRotation = Quaternion.identity;
        private bool _visualActive;

        public bool IsSwinging => Time.time < _swingEndTime;
        public bool IsReady => Time.time >= _readyTime;
        public float Damage => _damage;
        public float Reach => _reach;
        public float ArcDegrees => _arcDegrees;
        public bool IsBlocked => _owner != null && _owner.IsDodging;
        public Transform SwingVisual
        {
            get => _swingVisual;
            set => _swingVisual = value;
        }

        private void Awake()
        {
            if (_owner == null) _owner = GetComponentInParent<PlayerController>();
            _ownerRoot = _owner != null ? _owner.transform : transform.root;
            if (_swingVisual != null) _swingVisual.gameObject.SetActive(false);
        }

        /// <summary>
        /// Swings toward <paramref name="direction"/> (flattened; zero = this transform's forward).
        /// Fails while swinging, on cooldown or while dodging. Returns true if the swing started (hit or miss).
        /// </summary>
        public bool TrySwing(Vector3 direction)
        {
            if (!isActiveAndEnabled || IsBlocked || IsSwinging || !IsReady) return false;

            direction.y = 0f;
            if (direction.sqrMagnitude < 1e-6f)
            {
                direction = transform.forward;
                direction.y = 0f;
                if (direction.sqrMagnitude < 1e-6f) direction = Vector3.forward;
            }
            direction.Normalize();

            float now = Time.time;
            _swingStartTime = now;
            _swingEndTime = now + _swingDuration;
            _readyTime = _swingEndTime + _cooldown;
            _swingRotation = Quaternion.LookRotation(direction, Vector3.up);

            ApplyHits(direction);
            UpdateVisual();
            return true;
        }

        private void Update() => UpdateVisual();

        private void ApplyHits(Vector3 direction)
        {
            Vector3 origin = transform.position + Vector3.up * _originHeight;
            float halfArc = _arcDegrees * 0.5f;
            GameObject source = _owner != null ? _owner.gameObject : gameObject;
            bool isPlayer = _owner != null;

            int count = Physics.OverlapSphereNonAlloc(origin, _reach, _overlaps, _hitMask, QueryTriggerInteraction.Ignore);
            _hitThisSwing.Clear();

            for (int i = 0; i < count; i++)
            {
                Collider collider = _overlaps[i];
                if (collider == null) continue;
                if (_ownerRoot != null && collider.transform.IsChildOf(_ownerRoot)) continue;

                IDamageable target = collider.GetComponentInParent<IDamageable>();
                if (target == null || _hitThisSwing.Contains(target)) continue;

                Vector3 center = collider.bounds.center;
                Vector3 toTarget = center - origin;
                toTarget.y = 0f;
                float flatDistance = toTarget.magnitude;
                Vector3 hitDirection = flatDistance > 1e-4f ? toTarget / flatDistance : direction;
                if (flatDistance > 1e-4f && Vector3.Angle(direction, hitDirection) > halfArc) continue;

                Vector3 targetPoint = new Vector3(center.x, origin.y, center.z);
                if (IsBlockedByWall(origin, hitDirection, flatDistance)) continue;

                _hitThisSwing.Add(target);
                if (!target.TakeDamage(new DamageInfo(_damage, targetPoint, hitDirection, source, DamageKind.Melee))) continue;
                ImpactBus.Raise(new ImpactEvent
                {
                    kind = ImpactKind.Melee,
                    position = targetPoint,
                    normal = -hitDirection,
                    direction = hitDirection,
                    intensity = _hitIntensity,
                    surface = SurfaceType.Default,
                    hitTransform = collider.transform,
                    instigator = source,
                    instigatorIsLocalPlayer = isPlayer,
                });
            }
        }

        /// <summary>True if a non-damageable collider (not the owner) lies between the origin and the target centre.</summary>
        private bool IsBlockedByWall(Vector3 origin, Vector3 direction, float distance)
        {
            if (distance <= 1e-4f) return false;
            int count = Physics.RaycastNonAlloc(origin, direction, _lineHits, distance, _obstacleMask,
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

        private void UpdateVisual()
        {
            if (_swingVisual == null) return;

            bool swinging = IsSwinging;
            if (swinging != _visualActive)
            {
                _visualActive = swinging;
                _swingVisual.gameObject.SetActive(swinging);
            }
            if (!swinging) return;

            float t = Mathf.Clamp01((Time.time - _swingStartTime) / _swingDuration);
            float yaw = Mathf.Lerp(-_arcDegrees * 0.5f, _arcDegrees * 0.5f, t);
            _swingVisual.rotation = _swingRotation * Quaternion.Euler(0f, yaw, 0f);
        }
    }
}
