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
        /// <summary>
        /// Metres flown unseen (no visual, no trail) before the projectile shows. The rifle uses it so the bullet,
        /// which flies at the gameplay height under the shouldered gun, first appears ahead of the muzzle flash and
        /// reads as leaving the barrel. 0 = visible from the start.
        /// </summary>
        public float HiddenDistance;
        /// <summary>
        /// Metres the visual and the trail are drawn above the projectile (which flies, hits and bounces at its own
        /// height). The rifle lifts its bullets to the shouldered gun's muzzle, so the tracer leaves the barrel level
        /// while the hits stay at the gameplay height.
        /// </summary>
        public float VisualLift;
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
        /// Damage lost per enemy passed through (Delici cost 0.3): the next target takes Damage × (1 − this)^passed.
        /// 0 = full damage to every pierced target.
        /// </summary>
        public float PierceDamageFalloff;
        /// <summary>
        /// Optional (enemy shots): if set, only damageables under this root are hit; other damageables (enemies) are
        /// flown through. Colliders without a damageable (walls) still stop it. Null = hit any damageable.
        /// </summary>
        public Transform DamageOnlyUnder;
        public float HitIntensity;
        public float WallHitIntensity;
        /// <summary>
        /// Critical bullet, rolled once by the weapon at fire time (production/design/crit-difficulty.md §2): it crits
        /// every damageable it hits, through pierces and after an Echo bounce. Its own bounced hit on the shooter never
        /// crits (half of the non-crit damage). False for enemy shots.
        /// </summary>
        public bool IsCrit;
        /// <summary>Damage multiplier of a crit. 0 or less = <see cref="CritRoll.DefaultMultiplier"/>.</summary>
        public float CritMultiplier;

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
        /// <summary>
        /// On each bounce the range still left (MaxRange − travelled) is multiplied by this (Echo: 0.5, at most half of
        /// the remaining range after a bounce). 0 or ≥ 1 = unchanged.
        /// </summary>
        public float BounceRangeScale;
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
        [Tooltip("Stretch the visual by speed and hit size (runtime cube projectiles). Off for modelled projectiles " +
                 "(bullet, rocket), which keep their authored size; their trails are cleared on every launch.")]
        [SerializeField] private bool _stretchVisual = true;

        private TrailRenderer[] _trails;
        private readonly System.Collections.Generic.List<Renderer> _hiddenRenderers =
            new System.Collections.Generic.List<Renderer>(4);
        private bool _hidden;
        private Transform _liftedTrails;

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
        private float _rangeLimit;
        private int _pierceLeft;
        private float _pierceDamageScale = 1f;
        private bool _active;
        private int _bouncesLeft;
        private bool _bounced;
        private Transform _ignoreRoot;
        private Transform _selfRoot;
        private Collider _lastBounceCollider;

        public bool IsActive => _active;
        /// <summary>True if this flight is a critical bullet (optional visual).</summary>
        public bool IsCrit => _launch.IsCrit;
        /// <summary>True after the first wall bounce of this flight.</summary>
        public bool HasBounced => _bounced;

        /// <summary>Raised on every wall bounce with the bounce point (Echo "çın" audio cue).</summary>
        public static event System.Action<Vector3> Bounced;
        /// <summary>Distance travelled this flight (bounces keep counting toward the max range).</summary>
        public float Travelled => _travelled;
        /// <summary>Total travel at which this flight ends: MaxRange, shortened by <see cref="ProjectileLaunch.BounceRangeScale"/> on bounces.</summary>
        public float RangeLimit => _rangeLimit;
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
            _rangeLimit = launch.MaxRange;
            _pierceLeft = Mathf.Max(0, launch.PierceCount);
            _pierceDamageScale = 1f;
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

            // A pooled projectile keeps its old trail points; without a clear the trail streaks from where it died.
            if (_trails == null) _trails = GetComponentsInChildren<TrailRenderer>(true);
            if (launch.VisualLift != 0f && _liftedTrails == null) LiftTrails();
            if (_liftedTrails != null) _liftedTrails.localPosition = new Vector3(0f, launch.VisualLift, 0f);
            for (int i = 0; i < _trails.Length; i++) _trails[i].Clear();
            if (_hidden) SetHidden(false);
            if (launch.HiddenDistance > 0f) SetHidden(true);
        }

        /// <summary>
        /// Moves the trails under a "LiftedTrails" child that <see cref="ProjectileLaunch.VisualLift"/> raises: child
        /// trails are re-parented, a trail on this object itself is copied onto a new child and switched off (a
        /// component cannot change its object). Done once per pooled projectile.
        /// </summary>
        private void LiftTrails()
        {
            _liftedTrails = new GameObject("LiftedTrails").transform;
            _liftedTrails.SetParent(transform, false);
            for (int i = 0; i < _trails.Length; i++)
            {
                TrailRenderer source = _trails[i];
                if (source.transform != transform)
                {
                    source.transform.SetParent(_liftedTrails, false);
                    continue;
                }
                var copy = new GameObject("Trail").AddComponent<TrailRenderer>();
                copy.transform.SetParent(_liftedTrails, false);
                copy.time = source.time;
                copy.minVertexDistance = source.minVertexDistance;
                copy.widthCurve = source.widthCurve;
                copy.widthMultiplier = source.widthMultiplier;
                copy.colorGradient = source.colorGradient;
                copy.sharedMaterials = source.sharedMaterials;
                copy.numCornerVertices = source.numCornerVertices;
                copy.numCapVertices = source.numCapVertices;
                copy.alignment = source.alignment;
                copy.textureMode = source.textureMode;
                copy.shadowCastingMode = source.shadowCastingMode;
                copy.receiveShadows = source.receiveShadows;
                copy.sortingLayerID = source.sortingLayerID;
                copy.sortingOrder = source.sortingOrder;
                copy.autodestruct = false;
                copy.emitting = source.emitting;
                source.Clear();
                source.emitting = false;
                source.enabled = false;
                _trails[i] = copy;
            }
        }

        /// <summary>
        /// Hides or shows the visual's renderers and the trails. Only renderers that were on are hidden and later
        /// shown again (a thrown module keeps the pool cube it switched off). Showing clears the trails, so they start
        /// where the projectile appears.
        /// </summary>
        private void SetHidden(bool hidden)
        {
            if (hidden)
            {
                _hiddenRenderers.Clear();
                if (_visual != null)
                    foreach (Renderer r in _visual.GetComponentsInChildren<Renderer>())
                        if (r.enabled)
                        {
                            r.enabled = false;
                            _hiddenRenderers.Add(r);
                        }
            }
            else
            {
                for (int i = 0; i < _hiddenRenderers.Count; i++)
                    if (_hiddenRenderers[i] != null) _hiddenRenderers[i].enabled = true;
                _hiddenRenderers.Clear();
            }
            if (_trails != null)
                for (int i = 0; i < _trails.Length; i++)
                {
                    _trails[i].emitting = !hidden;
                    if (!hidden) _trails[i].Clear();
                }
            _hidden = hidden;
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
            float remaining = _rangeLimit - _travelled;
            bool reachesEnd = step >= remaining;
            if (reachesEnd) step = Mathf.Max(0f, remaining);

            if (step > 0f)
            {
                SweepResult result = Sweep(step);
                if (result == SweepResult.Ended) return;
                if (result == SweepResult.Bounced)
                {
                    // The rest of this frame's step is dropped; the flight resumes next frame from the wall.
                    if (_travelled >= _rangeLimit) Despawn();
                    return;
                }
            }

            transform.position += _direction * step;
            _travelled += step;
            if (_hidden && _travelled >= _launch.HiddenDistance) SetHidden(false);

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
                IDamageable target = collider.GetComponentInParent<IDamageable>();
                Vector3 point = hit.point;
                Vector3 normal = hit.normal;
                if (initialOverlap)
                {
                    OverlapContact(collider, origin, out point, out normal);
                    // A wall only grazed at the start while already moving away from (or along) it does not stop the shot.
                    if (target == null && Vector3.Dot(normal, _direction) >= 0f) continue;
                }
                Vector3 centerAtHit = origin + _direction * hit.distance;

                if (_launch.OnDetonate != null)
                {
                    transform.position = centerAtHit;
                    Detonate(point, normal);
                    return SweepResult.Ended;
                }

                if (target == null)
                {
                    RaiseImpact(collider, point, normal, _launch.WallHitIntensity, false);
                    if (CanBounce && Bounce(centerAtHit, normal, hit.distance, collider)) return SweepResult.Bounced;
                    transform.position = centerAtHit;
                    Despawn();
                    return SweepResult.Ended;
                }

                float damage = _launch.Damage * _pierceDamageScale; // Delici: weaker after each enemy passed
                bool selfHit = false;
                if (_selfRoot != null && collider.transform.IsChildOf(_selfRoot))
                {
                    // Own bounced bullet (Echo): reduced damage; harmless once the bounce switch is off.
                    if (!SelfHurtActive) continue;
                    damage *= _launch.BouncedSelfDamageScale;
                    selfHit = true;
                }
                else if (_launch.DamageOnlyUnder != null && !collider.transform.IsChildOf(_launch.DamageOnlyUnder))
                {
                    continue;
                }

                if (_alreadyHit.Contains(target)) continue;
                _alreadyHit.Add(target);

                // Crit bullet (rolled at fire time): every target crits; the shooter's own bounced bullet never does.
                bool crit = !selfHit && _launch.IsCrit;
                if (crit) damage *= _launch.CritMultiplier > 0f ? _launch.CritMultiplier : CritRoll.DefaultMultiplier;
                target.TakeDamage(new DamageInfo(damage, point, _direction, _launch.Source, _launch.Kind, crit));
                RaiseImpact(collider, point, normal, _launch.HitIntensity, crit);

                if (_pierceLeft <= 0)
                {
                    transform.position = centerAtHit;
                    Despawn();
                    return SweepResult.Ended;
                }
                _pierceLeft--;
                if (_launch.PierceDamageFalloff > 0f) _pierceDamageScale *= 1f - Mathf.Clamp01(_launch.PierceDamageFalloff);
            }
            return SweepResult.Continue;
        }

        /// <summary>
        /// Contact for a cast that starts overlapped (the cast gives no usable point or normal): the closest surface
        /// point and the direction from it to the centre, so a wall touched at the start reflects off its real face
        /// instead of straight back. Falls back to the reversed travel direction when the centre is inside the collider
        /// or the shape has no closest point (non-convex mesh).
        /// </summary>
        private void OverlapContact(Collider collider, Vector3 origin, out Vector3 point, out Vector3 normal)
        {
            point = origin;
            normal = -_direction;
            if (collider is MeshCollider meshCollider && !meshCollider.convex) return;

            Vector3 closest = collider.ClosestPoint(origin);
            Vector3 away = origin - closest;
            if (away.sqrMagnitude < 1e-8f) return; // centre inside the collider
            point = closest;
            normal = away.normalized;
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
            if (_hidden) SetHidden(false); // a wall right at the muzzle: show the bounce
            // Echo: after a bounce at most this fraction of the range still left (crit-difficulty design: half).
            float rangeScale = _launch.BounceRangeScale;
            if (rangeScale > 0f && rangeScale < 1f)
                _rangeLimit = _travelled + Mathf.Max(0f, _rangeLimit - _travelled) * rangeScale;
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

        private void RaiseImpact(Collider collider, Vector3 point, Vector3 normal, float intensity, bool isCrit)
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
                isCrit = isCrit,
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
            if (_visual == null || !_stretchVisual) return;
            float thickness = Mathf.Max(0.02f, _launch.Radius * 2f * _thicknessScale);
            float length = Mathf.Max(thickness, _launch.Speed * _stretchTime);
            _visual.localRotation = Quaternion.identity;
            _visual.localScale = new Vector3(thickness, thickness, length);
            _visual.localPosition = new Vector3(0f, _launch.VisualLift, -length * 0.5f); // trail behind the head
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
