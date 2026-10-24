using System.Collections.Generic;
using UnityEngine;

namespace DanceWithTheDevil
{
    /// <summary>
    /// Machete (GDD §5 Silahlar): 35 damage in a 120° arc, 2 m in front of the aim direction. The swing lasts 0.2 s
    /// (the rifle is silent meanwhile, <see cref="IsSwinging"/>), then a 0.5 s cooldown measured from the end of the
    /// swing. Damage is applied once, at swing start, to each target in the arc that is not behind a wall.
    /// Blocked while the owner dodges. Not done yet: the planar cut on the surface it hits (needs voxels).
    /// Contract hooks (GDD §8): <see cref="DamageMultiplier"/> (blade and wave), <see cref="AttackSpeedMultiplier"/>
    /// (Contract 4: divides only the post-swing cooldown, 0.5 → 0.4 s; the 0.2 s swing never changes) and
    /// <see cref="WaveEnabled"/> (Contract 2):
    /// every swing also sends a forward wave (GDD §5: 20 damage, pierces, 8 m, 2.5 m wide, 20 m/s). The wave hits each
    /// damageable once and not through walls (line from the swing origin). Shielded fronts do not exist yet.
    /// Visual: on the frame the damage lands, a bright flat fan covering the real hit area (arc × reach) appears and
    /// thins out over ~0.12 s, and the blade crosses the arc in ~0.06 s. Both run on unscaled time, so they read during
    /// the melee hitstop instead of freezing at the start angle.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class Machete : MonoBehaviour
    {
        private const int MaxOverlaps = 128;
        private const int MaxLineHits = 16;
        private const int ArcSegments = 16;

        [Header("References")]
        [Tooltip("Owner, for the dodge block and ignoring own colliders. If empty, found in parents.")]
        [SerializeField] private PlayerController _owner;
        [Tooltip("Optional placeholder visual, shown and rotated through the arc during the swing.")]
        [SerializeField] private Transform _swingVisual;

        [Header("Swing visual")]
        [Tooltip("Material of the swing arc fan. Empty = the swing visual's material.")]
        [SerializeField] private Material _arcMaterial;
        [Tooltip("Seconds (unscaled) the arc stays on screen, thinning out from the inside.")]
        [SerializeField, Min(0.01f)] private float _arcDuration = 0.12f;
        [Tooltip("Inner radius of the arc on its first frame, metres (the outer radius is the reach).")]
        [SerializeField, Min(0f)] private float _arcInnerRadius = 0.35f;
        [Tooltip("How far the arc's outer edge grows while it fades, metres.")]
        [SerializeField, Min(0f)] private float _arcGrowth = 0.2f;
        [Tooltip("Height of the arc above the feet, metres (about an enemy's body centre).")]
        [SerializeField] private float _arcHeight = 0.7f;
        [Tooltip("Seconds (unscaled) for the blade to cross the arc; it then holds at the end while the arc fades.")]
        [SerializeField, Min(0.001f)] private float _bladeSweepTime = 0.06f;
        [Tooltip("Blade thickness multiplier applied once at start, so the blade shows at pixel scale.")]
        [SerializeField, Min(0.1f)] private float _bladeWidthScale = 2.5f;

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

        [Header("Wave (GDD §5, Contract 2)")]
        [Tooltip("Wave damage per target.")]
        [SerializeField, Min(0f)] private float _waveDamage = 20f;
        [Tooltip("Wave range in metres.")]
        [SerializeField, Min(0.1f)] private float _waveRange = 8f;
        [Tooltip("Wave width in metres.")]
        [SerializeField, Min(0.1f)] private float _waveWidth = 2.5f;
        [Tooltip("Wave height in metres (centred on the swing origin).")]
        [SerializeField, Min(0.1f)] private float _waveHeight = 1.4f;
        [Tooltip("Wave speed in m/s.")]
        [SerializeField, Min(0.1f)] private float _waveSpeed = 20f;
        [Tooltip("Material of the runtime wave visual. Empty = the swing visual's material.")]
        [SerializeField] private Material _waveMaterial;
        [Tooltip("ImpactBus Melee intensity per wave hit.")]
        [SerializeField, Range(0f, 1f)] private float _waveHitIntensity = 0.3f;

        [Header("Critical hits (crit-difficulty design)")]
        [Tooltip("Base crit chance per target hit, 0..1 (design: 0.05).")]
        [SerializeField, Range(0f, 1f)] private float _baseCritChance = CritRoll.DefaultChance;
        [Tooltip("Damage multiplier of a crit (design: 2).")]
        [SerializeField, Min(1f)] private float _critMultiplier = CritRoll.DefaultMultiplier;

        [Header("Feel")]
        [Tooltip("ImpactBus Melee intensity per target hit.")]
        [SerializeField, Range(0f, 1f)] private float _hitIntensity = 0.5f;

        private readonly Collider[] _overlaps = new Collider[MaxOverlaps];
        private readonly RaycastHit[] _lineHits = new RaycastHit[MaxLineHits];
        private readonly HashSet<IDamageable> _hitThisSwing = new HashSet<IDamageable>();
        private readonly RaycastHit[] _waveHits = new RaycastHit[MaxOverlaps];
        private WaveSlot[] _waves;
        private float _damageMultiplier = 1f;
        private float _attackSpeedMultiplier = 1f;
        private bool _waveEnabled;
        private float _contractCritChance;

        private sealed class WaveSlot
        {
            public bool Active;
            public Vector3 Origin;
            public Vector3 Direction;
            public float Travelled;
            public Transform Visual;
            public readonly HashSet<IDamageable> Hit = new HashSet<IDamageable>();
        }
        private Transform _ownerRoot;

        private float _swingStartUnscaled = float.NegativeInfinity;
        private float _swingEndTime = float.NegativeInfinity;
        private float _readyTime = float.NegativeInfinity;
        private Quaternion _swingRotation = Quaternion.identity;
        private bool _visualActive;

        private Transform _arc;
        private Mesh _arcMesh;
        private Vector3[] _arcVertices;
        private bool _arcActive;

        public bool IsSwinging => Time.time < _swingEndTime;
        public bool IsReady => Time.time >= _readyTime;

        /// <summary>Raised when a swing starts, hit or miss (audio; the wave has no sound of its own).</summary>
        public static event System.Action<Machete> SwingStarted;
        /// <summary>Raised once per swing whose blade damaged at least one target (audio).</summary>
        public static event System.Action<Machete> SwingHit;
        public float Damage => _damage * _damageMultiplier;
        public float WaveDamage => _waveDamage * _damageMultiplier;

        /// <summary>Multiplies blade and wave damage (contracts). Min 0.</summary>
        public float DamageMultiplier
        {
            get => _damageMultiplier;
            set => _damageMultiplier = Mathf.Max(0f, value);
        }

        /// <summary>
        /// Attack speed (contracts, design combo-haste §3): the post-swing cooldown is divided by it; the swing
        /// duration is not. Multipliers stack by product. Min 0.01.
        /// </summary>
        public float AttackSpeedMultiplier
        {
            get => _attackSpeedMultiplier;
            set => _attackSpeedMultiplier = Mathf.Max(0.01f, value);
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

        /// <summary>Crit chance per target (swing and wave): base + contract, clamped 0..1.</summary>
        public float CritChance => CritRoll.Combine(_baseCritChance, 0f, _contractCritChance);

        /// <summary>Cooldown after the swing ends, with attack speed applied (base 0.5 s).</summary>
        public float Cooldown => _cooldown / _attackSpeedMultiplier;
        public float SwingDuration => _swingDuration;

        /// <summary>Contract 2: every swing also sends the forward wave.</summary>
        public bool WaveEnabled
        {
            get => _waveEnabled;
            set => _waveEnabled = value;
        }

        /// <summary>Number of waves in flight.</summary>
        public int ActiveWaveCount
        {
            get
            {
                int n = 0;
                if (_waves != null) for (int i = 0; i < _waves.Length; i++) if (_waves[i].Active) n++;
                return n;
            }
        }
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
            if (_swingVisual != null)
            {
                // The placeholder blade is ~1.7 texels wide: thicken it once so it reads at pixel scale.
                for (int i = 0; i < _swingVisual.childCount; i++)
                {
                    Transform blade = _swingVisual.GetChild(i);
                    Vector3 scale = blade.localScale;
                    scale.x *= _bladeWidthScale;
                    blade.localScale = scale;
                }
                _swingVisual.gameObject.SetActive(false);
            }
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
            _swingStartUnscaled = Time.unscaledTime;
            _swingEndTime = now + _swingDuration;
            _readyTime = _swingEndTime + Cooldown;
            _swingRotation = Quaternion.LookRotation(direction, Vector3.up);

            // The arc shows on the same frame the damage lands.
            ShowArc();
            bool hit = ApplyHits(direction);
            SwingStarted?.Invoke(this);
            if (hit) SwingHit?.Invoke(this);
            if (_waveEnabled) StartWave(direction);
            UpdateVisual();
            return true;
        }

        private void Update()
        {
            UpdateVisual();
            TickArc();
            TickWaves(Time.deltaTime);
        }

        private void OnDisable()
        {
            if (_arc != null) _arc.gameObject.SetActive(false);
            _arcActive = false;
        }

        private void OnDestroy()
        {
            if (_arc != null) Destroy(_arc.gameObject);
            if (_arcMesh != null) Destroy(_arcMesh);
            if (_waves == null) return;
            for (int i = 0; i < _waves.Length; i++)
                if (_waves[i].Visual != null) Destroy(_waves[i].Visual.gameObject);
        }

        private void ShowArc()
        {
            if (_arc == null) CreateArc();
            _arcActive = true;
            WriteArc(0f);
            PlaceArc();
            if (!_arc.gameObject.activeSelf) _arc.gameObject.SetActive(true);
        }

        private void TickArc()
        {
            if (!_arcActive) return;
            float t = (Time.unscaledTime - _swingStartUnscaled) / _arcDuration;
            if (t >= 1f)
            {
                _arcActive = false;
                _arc.gameObject.SetActive(false);
                return;
            }
            WriteArc(t);
            PlaceArc();
        }

        private void PlaceArc() =>
            _arc.SetPositionAndRotation(transform.position + Vector3.up * _arcHeight, _swingRotation);

        /// <summary>Fan over the hit area at fade time <paramref name="t"/> (0..1): the inner edge eases out to the rim.</summary>
        private void WriteArc(float t)
        {
            float outer = _reach + _arcGrowth * t;
            float inner = Mathf.Lerp(Mathf.Min(_arcInnerRadius, _reach), outer, t * t);
            float half = _arcDegrees * 0.5f;
            for (int k = 0; k <= ArcSegments; k++)
            {
                float angle = Mathf.Lerp(-half, half, k / (float)ArcSegments) * Mathf.Deg2Rad;
                var direction = new Vector3(Mathf.Sin(angle), 0f, Mathf.Cos(angle));
                _arcVertices[k * 2] = direction * inner;
                _arcVertices[k * 2 + 1] = direction * outer;
            }
            _arcMesh.SetVertices(_arcVertices);
            _arcMesh.RecalculateBounds();
        }

        /// <summary>Creates the arc fan once: a flat ring sector facing up, no collider, no shadows.</summary>
        private void CreateArc()
        {
            var go = new GameObject(name + "_MacheteArc");
            _arc = go.transform;
            var filter = go.AddComponent<MeshFilter>();
            var meshRenderer = go.AddComponent<MeshRenderer>();

            int vertexCount = (ArcSegments + 1) * 2;
            _arcVertices = new Vector3[vertexCount];
            var normals = new Vector3[vertexCount];
            for (int i = 0; i < vertexCount; i++) normals[i] = Vector3.up;
            var triangles = new int[ArcSegments * 6];
            for (int k = 0; k < ArcSegments; k++)
            {
                int inner0 = k * 2;
                int outer0 = inner0 + 1;
                int inner1 = inner0 + 2;
                int outer1 = inner0 + 3;
                int t = k * 6;
                // Clockwise seen from above, so the fan faces the camera.
                triangles[t] = inner0;
                triangles[t + 1] = outer0;
                triangles[t + 2] = outer1;
                triangles[t + 3] = inner0;
                triangles[t + 4] = outer1;
                triangles[t + 5] = inner1;
            }

            _arcMesh = new Mesh { name = "MacheteArc" };
            _arcMesh.MarkDynamic();
            WriteArc(0f);
            _arcMesh.SetNormals(normals);
            _arcMesh.SetTriangles(triangles, 0);
            _arcMesh.RecalculateBounds();
            filter.sharedMesh = _arcMesh;

            Material material = _arcMaterial;
            if (material == null && _swingVisual != null)
            {
                var bladeRenderer = _swingVisual.GetComponentInChildren<Renderer>(true);
                if (bladeRenderer != null) material = bladeRenderer.sharedMaterial;
            }
            if (material != null) meshRenderer.sharedMaterial = material;
            meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            meshRenderer.receiveShadows = false;
            go.SetActive(false);
        }

        private void StartWave(Vector3 direction)
        {
            if (_waves == null)
            {
                // A swing every >= 0.7 s, a wave lives 0.4 s: two slots are plenty. Created once, on first use.
                _waves = new WaveSlot[2];
                for (int i = 0; i < _waves.Length; i++) _waves[i] = new WaveSlot();
            }

            WaveSlot wave = null;
            for (int i = 0; i < _waves.Length; i++)
            {
                if (!_waves[i].Active)
                {
                    wave = _waves[i];
                    break;
                }
            }
            if (wave == null) wave = _waves[0];

            wave.Active = true;
            wave.Origin = transform.position + Vector3.up * _originHeight;
            wave.Direction = direction;
            wave.Travelled = 0f;
            wave.Hit.Clear();
            if (wave.Visual == null) wave.Visual = CreateWaveVisual();
            wave.Visual.gameObject.SetActive(true);
            wave.Visual.SetPositionAndRotation(wave.Origin, Quaternion.LookRotation(direction, Vector3.up));
        }

        private Transform CreateWaveVisual()
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = "MacheteWave";
            var collider = go.GetComponent<Collider>();
            if (collider != null)
            {
                collider.enabled = false;
                Destroy(collider);
            }
            go.transform.localScale = new Vector3(_waveWidth, 0.15f, 0.3f);
            var meshRenderer = go.GetComponent<MeshRenderer>();
            Material material = _waveMaterial;
            if (material == null && _swingVisual != null)
            {
                var bladeRenderer = _swingVisual.GetComponentInChildren<Renderer>(true);
                if (bladeRenderer != null) material = bladeRenderer.sharedMaterial;
            }
            if (material != null) meshRenderer.sharedMaterial = material;
            meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return go.transform;
        }

        private void TickWaves(float dt)
        {
            if (_waves == null) return;
            for (int i = 0; i < _waves.Length; i++)
            {
                WaveSlot wave = _waves[i];
                if (!wave.Active) continue;

                float step = Mathf.Min(_waveSpeed * dt, _waveRange - wave.Travelled);
                if (step > 0f) SweepWave(wave, step);
                wave.Travelled += Mathf.Max(0f, step);
                if (wave.Visual != null) wave.Visual.position = wave.Origin + wave.Direction * wave.Travelled;

                if (wave.Travelled >= _waveRange - 1e-4f)
                {
                    wave.Active = false;
                    if (wave.Visual != null) wave.Visual.gameObject.SetActive(false);
                }
            }
        }

        private void SweepWave(WaveSlot wave, float step)
        {
            Vector3 center = wave.Origin + wave.Direction * wave.Travelled;
            var halfExtents = new Vector3(_waveWidth * 0.5f, _waveHeight * 0.5f, 0.1f);
            Quaternion orientation = Quaternion.LookRotation(wave.Direction, Vector3.up);
            int count = Physics.BoxCastNonAlloc(center, halfExtents, wave.Direction, _waveHits, orientation, step,
                _hitMask, QueryTriggerInteraction.Ignore);
            GameObject source = _owner != null ? _owner.gameObject : gameObject;
            bool isPlayer = _owner != null;
            float damage = WaveDamage;

            for (int i = 0; i < count; i++)
            {
                Collider collider = _waveHits[i].collider;
                if (collider == null) continue;
                if (_ownerRoot != null && collider.transform.IsChildOf(_ownerRoot)) continue;
                IDamageable target = collider.GetComponentInParent<IDamageable>();
                if (target == null || wave.Hit.Contains(target)) continue; // walls do not stop it; it pierces

                Vector3 targetCenter = collider.bounds.center;
                Vector3 toTarget = targetCenter - wave.Origin;
                toTarget.y = 0f;
                float flatDistance = toTarget.magnitude;
                Vector3 hitDirection = flatDistance > 1e-4f ? toTarget / flatDistance : wave.Direction;
                if (IsBlockedByWall(wave.Origin, hitDirection, flatDistance)) continue;
                wave.Hit.Add(target);

                // Melee kind: DamageKind has no separate wave entry yet (needed later for the shield rule, K-13).
                bool crit = CritRoll.Roll(CritChance); // one roll per target (crit-difficulty design)
                float dealt = crit ? damage * _critMultiplier : damage;
                if (!target.TakeDamage(new DamageInfo(dealt, targetCenter, wave.Direction, source, DamageKind.Melee, crit))) continue;
                ImpactBus.Raise(new ImpactEvent
                {
                    kind = ImpactKind.Melee,
                    position = targetCenter,
                    normal = -wave.Direction,
                    direction = wave.Direction,
                    intensity = _waveHitIntensity,
                    surface = SurfaceType.Default,
                    hitTransform = collider.transform,
                    instigator = source,
                    instigatorIsLocalPlayer = isPlayer,
                    isCrit = crit,
                });
            }
        }

        private bool ApplyHits(Vector3 direction)
        {
            bool any = false;
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
                bool crit = CritRoll.Roll(CritChance); // one roll per target (crit-difficulty design)
                float dealt = crit ? Damage * _critMultiplier : Damage;
                if (!target.TakeDamage(new DamageInfo(dealt, targetPoint, hitDirection, source, DamageKind.Melee, crit))) continue;
                any = true;
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
                    isCrit = crit,
                });
            }
            return any;
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

            // Unscaled: the blade crosses the arc during the hitstop instead of freezing at the start angle, and leaves
            // with the arc.
            float elapsed = Time.unscaledTime - _swingStartUnscaled;
            bool show = IsSwinging && elapsed < Mathf.Max(_bladeSweepTime, _arcDuration);
            if (show != _visualActive)
            {
                _visualActive = show;
                _swingVisual.gameObject.SetActive(show);
            }
            if (!show) return;

            float t = Mathf.Clamp01(elapsed / _bladeSweepTime);
            t = 1f - (1f - t) * (1f - t); // fast out of the start, settles at the end
            float yaw = Mathf.Lerp(-_arcDegrees * 0.5f, _arcDegrees * 0.5f, t);
            _swingVisual.rotation = _swingRotation * Quaternion.Euler(0f, yaw, 0f);
        }
    }
}
