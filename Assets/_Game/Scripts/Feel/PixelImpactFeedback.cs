using UnityEngine;

namespace DanceWithTheDevil
{
    /// <summary>
    /// Fans every <see cref="ImpactEvent"/> out to all feel channels on the same frame: core flash, cube sparks, an
    /// expanding ring of cubes for blasts, light, camera trauma and hitstop. Tuned for the follow camera at
    /// orthographic size 9 (20 texels per metre, 1 texel = 0.05 m): every flash is several texels and lives a few
    /// frames, every shape is an opaque chunky cube (no soft sprites), and shake is an image offset in texels
    /// (see <see cref="PixelCameraRig.AddTrauma(float)"/>). Blasts show a thin ring of cubes at the real blast radius
    /// and only a short small core, so the blast area stays readable (audit F13).
    /// Death bursts (<see cref="Shatter"/>) use their own particle system: chunky cells that settle on the floor and
    /// lie there a moment before shrinking away (audit F21).
    /// Builds its particle systems, flash meshes, ring meshes and lights at runtime, so the scene only needs this
    /// component.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PixelImpactFeedback : MonoBehaviour
    {
        [System.Serializable]
        private struct Profile
        {
            [Tooltip("Cubes emitted at full intensity (a weak event still emits 60% of this, at least 1).")]
            public int Cubes;
            [Tooltip("Cube edge length range in metres (1 texel = 0.05 m at orthographic size 9).")]
            public Vector2 CubeSize;
            [Tooltip("Cube speed range in m/s.")]
            public Vector2 Speed;
            [Tooltip("Cube lifetime range in seconds.")]
            public Vector2 Lifetime;
            [Tooltip("Hot colour (flash, first cubes) and cool colour (last cubes).")]
            public Color Hot;
            public Color Cool;
            [Tooltip("Flash shape peak size in metres (0 = none).")]
            public float FlashSize;
            [Tooltip("Flash grow time and fade time in seconds (unscaled).")]
            public float FlashGrow;
            public float FlashFade;
            [Tooltip("Flash length along the fire / hit direction relative to its size (1 = cube).")]
            public float FlashStretch;
            [Tooltip("Camera trauma added (0..1).")]
            public float Trauma;
            [Tooltip("Global hitstop in seconds (0 = none). Only for hits the player causes.")]
            public float HitStop;
            [Tooltip("Light peak intensity (0 = none), range in metres and pulse length in seconds (unscaled).")]
            public float LightIntensity;
            public float LightRange;
            public float LightDuration;
            [Tooltip("Blast ring radius in metres when the event carries no radius (0 = no ring).")]
            public float RingRadius;
        }

        [System.Serializable]
        private struct RingStyle
        {
            [Tooltip("Seconds to expand from the start radius to the full radius, then to hold it, then to shrink away (unscaled).")]
            public float Expand;
            public float Hold;
            public float Fade;
            [Tooltip("Cube thickness (radial) and height in metres.")]
            public float Thickness;
            public float Height;
            [Tooltip("Colour while expanding, while holding, and while shrinking.")]
            public Color Flash;
            public Color Hot;
            public Color Cool;
        }

        private sealed class FlashShape
        {
            public Transform Transform;
            public MeshRenderer Renderer;
            public float Age;
            public float Grow;
            public float Fade;
            public Vector3 BaseScale;
            public bool Active;
        }

        private sealed class Ring
        {
            public BoxMeshBatch Batch;
            public bool Active;
            public Vector3 Center;
            public float Radius;
            public float StartRadius;
            public float Age;
            public RingStyle Style;
            public int Segments;
            public int ColorStep;
        }

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly Color LightWarm = new Color(1f, 0.85f, 0.6f);

        [SerializeField] private PixelCameraRig _cameraRig;
        [Tooltip("Unlit material for burst cubes (vertex colour). Falls back to a runtime URP Particles/Unlit material.")]
        [SerializeField] private Material _cubeMaterial;
        [Tooltip("Unlit material for flash shapes and rings (_BaseColor set per shape). Falls back to a runtime URP Unlit material.")]
        [SerializeField] private Material _flashMaterial;

        [Header("Profiles (for 20 px/m; tune by eye at 0.1 time scale, 1920x1080)")]
        [SerializeField] private Profile _muzzle = new Profile
        {
            Cubes = 4, CubeSize = new Vector2(0.1f, 0.16f), Speed = new Vector2(3f, 7f), Lifetime = new Vector2(0.08f, 0.16f),
            Hot = new Color(1f, 1f, 1f), Cool = new Color(0.37f, 1f, 0.94f),
            FlashSize = 0.42f, FlashGrow = 0.016f, FlashFade = 0.05f, FlashStretch = 1.8f, Trauma = 0.03f, HitStop = 0f,
            LightIntensity = 4f, LightRange = 4f, LightDuration = 0.06f, RingRadius = 0f,
        };
        [SerializeField] private Profile _bullet = new Profile
        {
            Cubes = 6, CubeSize = new Vector2(0.1f, 0.18f), Speed = new Vector2(2.5f, 5.5f), Lifetime = new Vector2(0.2f, 0.4f),
            Hot = new Color(0.85f, 1f, 0.97f), Cool = new Color(0.37f, 1f, 0.94f),
            FlashSize = 0.45f, FlashGrow = 0.016f, FlashFade = 0.06f, FlashStretch = 1f, Trauma = 0.04f, HitStop = 0f,
            LightIntensity = 0f, LightRange = 0f, LightDuration = 0f, RingRadius = 0f,
        };
        [SerializeField] private Profile _melee = new Profile
        {
            Cubes = 12, CubeSize = new Vector2(0.12f, 0.22f), Speed = new Vector2(3f, 7f), Lifetime = new Vector2(0.3f, 0.6f),
            Hot = new Color(1f, 0.95f, 0.8f), Cool = new Color(0.78f, 0.2f, 0.24f),
            FlashSize = 0.9f, FlashGrow = 0.016f, FlashFade = 0.08f, FlashStretch = 1f, Trauma = 0.8f, HitStop = 0.06f,
            LightIntensity = 0f, LightRange = 0f, LightDuration = 0f, RingRadius = 0f,
        };
        [SerializeField] private Profile _explosion = new Profile
        {
            Cubes = 26, CubeSize = new Vector2(0.16f, 0.3f), Speed = new Vector2(4f, 10f), Lifetime = new Vector2(0.45f, 1f),
            Hot = new Color(1f, 0.96f, 0.75f), Cool = new Color(0.35f, 0.12f, 0.1f),
            FlashSize = 1.2f, FlashGrow = 0.033f, FlashFade = 0.05f, FlashStretch = 1f, Trauma = 0.8f, HitStop = 0.1f,
            LightIntensity = 8f, LightRange = 9f, LightDuration = 0.2f, RingRadius = 2.5f,
        };

        [Header("Blast ring (explosions)")]
        [SerializeField] private RingStyle _blastRing = new RingStyle
        {
            Expand = 0.13f, Hold = 0.08f, Fade = 0.15f, Thickness = 0.2f, Height = 0.45f,
            Flash = new Color(1f, 1f, 0.95f), Hot = new Color(1f, 0.82f, 0.35f), Cool = new Color(0.8f, 0.3f, 0.12f),
        };
        [Tooltip("Distance between ring cubes along the circle, metres.")]
        [SerializeField, Min(0.1f)] private float _ringSpacing = 0.55f;
        [Tooltip("Layers a blast looks for the floor on (one downward ray per blast) to lay its ring.")]
        [SerializeField] private LayerMask _groundMask = ~0;

        [Header("Revive shockwave (K-12; the Devil's revive explosion)")]
        [Tooltip("Ring radius in metres when the event carries none (wide enough to sweep the whole view).")]
        [SerializeField, Min(1f)] private float _shockwaveRadius = 26f;
        [SerializeField] private RingStyle _shockwaveLead = new RingStyle
        {
            Expand = 0.38f, Hold = 0.02f, Fade = 0.1f, Thickness = 0.2f, Height = 0.5f,
            Flash = new Color(1f, 1f, 1f), Hot = new Color(1f, 0.95f, 0.9f), Cool = new Color(1f, 0.6f, 0.6f),
        };
        [SerializeField] private RingStyle _shockwaveMain = new RingStyle
        {
            Expand = 0.5f, Hold = 0.06f, Fade = 0.2f, Thickness = 0.4f, Height = 1f,
            Flash = new Color(1f, 0.45f, 0.45f), Hot = new Color(0.88f, 0.08f, 0.14f), Cool = new Color(0.45f, 0.03f, 0.08f),
        };
        [Tooltip("Cube burst of the shockwave (only the cube fields are used).")]
        [SerializeField] private Profile _shockwaveBurst = new Profile
        {
            Cubes = 40, CubeSize = new Vector2(0.18f, 0.34f), Speed = new Vector2(5f, 12f), Lifetime = new Vector2(0.5f, 1.1f),
            Hot = new Color(1f, 0.5f, 0.45f), Cool = new Color(0.4f, 0.02f, 0.06f),
        };
        [Tooltip("White core at Defne, metres.")]
        [SerializeField, Min(0f)] private float _shockwaveCoreSize = 2.2f;
        [SerializeField, Range(0f, 1f)] private float _shockwaveTrauma = 1f;
        [SerializeField, Min(0f)] private float _shockwaveHitStop = 0.08f;
        [SerializeField] private Color _shockwaveLightColor = new Color(1f, 0.35f, 0.3f);

        [Header("Kill flash")]
        [Tooltip("White pop at the body centre when something dies, metres.")]
        [SerializeField, Min(0f)] private float _killFlashSize = 0.8f;
        [Tooltip("Kill flash length in seconds (unscaled).")]
        [SerializeField, Min(0.016f)] private float _killFlashTime = 0.07f;
        [SerializeField] private Color _killFlashColor = Color.white;

        [Header("Shatter (death bursts)")]
        [Tooltip("Voxel edge length used to cut a dying model into cubes (metres). 0.24 m = about 5 texels.")]
        [SerializeField, Min(0.02f)] private float _shatterCell = 0.24f;
        [Tooltip("Most cubes one death may emit; larger models use coarser cubes.")]
        [SerializeField, Min(8)] private int _maxCubesPerShatter = 400;
        [Tooltip("Most cubes all shatters may emit in one frame; later kills that frame use coarser cubes.")]
        [SerializeField, Min(8)] private int _maxCubesPerFrame = 1500;
        [Tooltip("Shatter cube speed range (m/s).")]
        [SerializeField] private Vector2 _shatterSpeed = new Vector2(2.5f, 6.5f);
        [Tooltip("Shatter cube lifetime range (s): they bounce, settle, lie on the floor, then shrink away.")]
        [SerializeField] private Vector2 _shatterLifetime = new Vector2(2.2f, 3.2f);

        [Header("Limits")]
        [Tooltip("Hard cap on live death-burst cubes.")]
        [SerializeField, Min(16)] private int _maxCubes = 8000;
        [Tooltip("Hard cap on live spark cubes (hits, muzzle, blasts, dust).")]
        [SerializeField, Min(16)] private int _maxSparks = 3000;

        /// <summary>The scene's feedback router (one per scene).</summary>
        public static PixelImpactFeedback Instance { get; private set; }

        private const int MinCubesPerShatter = 16;
        private const int FlashCount = 10;
        private const int SmallRings = 4;
        private const int SmallRingCapacity = 48;
        private const int BigRings = 2;
        private const int BigRingCapacity = 120;

        private ParticleSystem _sparks;
        private ParticleSystem _debris;
        private int _shatterFrame = -1;
        private int _shatterCubesThisFrame;
        private FlashShape[] _flashes;
        private int _nextFlash;
        private Ring[] _rings;
        private Material _resolvedFlashMaterial;
        private MaterialPropertyBlock _block;
        private LightFlash _smallLight;
        private LightFlash _bigLight;
        private Light _smallLightComponent;
        private Light _bigLightComponent;

        /// <summary>Unlit flat material used for flashes and rings (_BaseColor through property blocks).</summary>
        public Material FlashMaterial => _resolvedFlashMaterial;

        private void Awake()
        {
            Instance = this;
            _block = new MaterialPropertyBlock();
            if (HitStop.Instance == null) gameObject.AddComponent<HitStop>();
            _resolvedFlashMaterial = _flashMaterial != null ? _flashMaterial : UnlitMaterial("Universal Render Pipeline/Unlit", "ImpactFlash");

            _sparks = BuildCubeSystem("ImpactCubes", _maxSparks, 1.2f, 2.5f, 0.5f, 0.3f, 0.5f,
                new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(0.6f, 0.85f), new Keyframe(1f, 0f)));
            // Debris: heavier, less drag so it flies, settles quickly (low bounce), full size until near the end.
            _debris = BuildCubeSystem("ShatterCubes", _maxCubes, 1.6f, 1.2f, 0.6f, 0.15f, 1f,
                new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(0.82f, 1f), new Keyframe(1f, 0f)));

            BuildFlashes(FlashCount);
            BuildRings();
            _smallLight = BuildLight("MuzzleLight", out _smallLightComponent);
            _bigLight = BuildLight("ExplosionLight", out _bigLightComponent);
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            if (_rings == null) return;
            for (int i = 0; i < _rings.Length; i++)
                if (_rings[i] != null && _rings[i].Batch != null) _rings[i].Batch.Destroy();
        }

        private void OnEnable() => ImpactBus.OnImpact += Handle;
        private void OnDisable() => ImpactBus.OnImpact -= Handle;

        private void Handle(ImpactEvent e)
        {
            switch (e.kind)
            {
                case ImpactKind.MuzzleFire: Play(_muzzle, e, _smallLight, _smallLightComponent, 0f); break;
                case ImpactKind.Bullet: Play(_bullet, e, null, null, 0f); break;
                case ImpactKind.Melee: Play(_melee, e, null, null, 0f); break;
                case ImpactKind.Explosion:
                    if (IsRevive(e)) PlayShockwave(e.position, e.radius);
                    else Play(_explosion, e, _bigLight, _bigLightComponent, BlastRadius(e, _explosion.RingRadius));
                    break;
            }
        }

        private void Play(in Profile p, in ImpactEvent e, LightFlash light, Light lightComponent, float ringRadius)
        {
            float intensity = Mathf.Clamp01(e.intensity <= 0f ? 1f : e.intensity);
            // A bigger blast (Contract 1's rocket) grows the core, cubes, light and shake with its real radius.
            float blastScale = p.RingRadius > 0f && ringRadius > 0f ? Mathf.Clamp(ringRadius / p.RingRadius, 0.6f, 1.6f) : 1f;

            // Textured fire/smoke (RealisticVfx) replaces the flash cube and cube burst of muzzle fire and rockets;
            // the light, shake and hitstop below stay here.
            bool textured = RealisticVfx.Instance != null && RealisticVfx.Instance.Covers(e);
            if (!textured)
            {
                if (p.FlashSize > 0f)
                {
                    Vector3 axis = e.kind == ImpactKind.MuzzleFire ? e.normal : e.direction;
                    float size = p.FlashSize * Mathf.Lerp(0.75f, 1f, intensity) * Mathf.Sqrt(blastScale);
                    SpawnFlash(e.position, size, p.FlashGrow, p.FlashFade, p.Hot, axis, p.FlashStretch);
                }
                EmitCubes(p, e, intensity, blastScale);
                if (ringRadius > 0f) SpawnRing(FloorPoint(e.position), ringRadius, _blastRing, 0.35f);
            }

            if (light != null && p.LightIntensity > 0f)
            {
                PlayLight(light, lightComponent, e.position, p.LightIntensity, p.LightRange * Mathf.Sqrt(blastScale),
                    p.LightDuration, LightWarm, Mathf.Lerp(0.5f, 1f, intensity));
            }

            // Enemy hits on Defne add no shake or freeze here: HurtFeedback reacts to damage that actually lands (a
            // dodged hit must not shake), and the freeze on getting hit read as lag (audit F9).
            bool shakes = e.instigatorIsLocalPlayer || e.kind == ImpactKind.Explosion;
            if (shakes && _cameraRig != null && p.Trauma > 0f)
                _cameraRig.AddTrauma(p.Trauma * intensity * Mathf.Lerp(1f, blastScale, 0.5f), e.position);

            // Medium hits (intensity ≥ 0.5, e.g. machete) get the profile's full pause: GDD §5 machete 0.06 s, rocket 0.1 s.
            if (e.instigatorIsLocalPlayer && p.HitStop > 0f && HitStop.Instance != null)
                HitStop.Instance.TriggerGlobal(p.HitStop * Mathf.Lerp(0.6f, 1f, Mathf.Clamp01(intensity * 2f)));
        }

        /// <summary>
        /// The Devil's revive shockwave (GDD §3 K-12, provisional D3: it clears every enemy): a white core at Defne, a
        /// fast white ring and a taller Devil-red ring of cubes that sweep out across the whole view, a red cube burst,
        /// a red light, full shake and a short freeze. <paramref name="radius"/> ≤ 0 uses the default sweep radius.
        /// </summary>
        public void PlayShockwave(Vector3 center, float radius)
        {
            float r = radius > 0f ? Mathf.Min(radius, _shockwaveRadius) : _shockwaveRadius;
            Vector3 floor = FloorPoint(center);
            if (_shockwaveCoreSize > 0f)
                SpawnFlash(floor + Vector3.up * 0.9f, _shockwaveCoreSize, 0.033f, 0.12f, _shockwaveLead.Flash, Vector3.zero, 1f);
            SpawnRing(floor, r, _shockwaveLead, 0.05f);
            SpawnRing(floor, r * 0.96f, _shockwaveMain, 0.04f);

            var burst = new ImpactEvent
            {
                kind = ImpactKind.Explosion,
                position = floor + Vector3.up * 0.8f,
                normal = Vector3.up,
                direction = Vector3.up,
                intensity = 1f,
            };
            EmitCubes(_shockwaveBurst, burst, 1f, 1f);
            PlayLight(_bigLight, _bigLightComponent, center, 10f, 14f, 0.35f, _shockwaveLightColor, 1f);

            if (_cameraRig != null) _cameraRig.AddTrauma(_shockwaveTrauma);
            if (_shockwaveHitStop > 0f && HitStop.Instance != null) HitStop.Instance.TriggerGlobal(_shockwaveHitStop);
        }

        /// <summary>A plain flash cube (no cubes, shake or freeze). <paramref name="duration"/> in seconds, unscaled.</summary>
        public void Flash(Vector3 position, float size, float duration, Color color)
        {
            if (size <= 0f) return;
            SpawnFlash(position, size, 0.016f, Mathf.Max(0.016f, duration), color, Vector3.zero, 1f);
        }

        /// <summary>The short white pop at a dying body's centre (audit F21). No shake, no freeze.</summary>
        public void KillFlash(Vector3 position) => Flash(position, _killFlashSize, _killFlashTime, _killFlashColor);

        /// <summary>
        /// A small puff of spark cubes (dust, scuffs) along <paramref name="direction"/> with some upward spread.
        /// No flash, shake or freeze. <paramref name="size"/> in metres, <paramref name="speed"/> in m/s.
        /// </summary>
        public void Puff(Vector3 position, Vector3 direction, int count, float size, float speed, Color color)
        {
            if (count <= 0) return;
            Vector3 baseDir = direction.sqrMagnitude > 1e-6f ? direction.normalized : Vector3.up;
            var emit = new ParticleSystem.EmitParams { applyShapeToPosition = false };
            for (int i = 0; i < count; i++)
            {
                Vector3 dir = (baseDir + Random.insideUnitSphere * 0.8f + Vector3.up * 0.4f).normalized;
                emit.position = position + Random.insideUnitSphere * 0.1f;
                emit.velocity = dir * (speed * Random.Range(0.6f, 1f));
                emit.startSize = size * Random.Range(0.8f, 1.1f);
                emit.startLifetime = Random.Range(0.25f, 0.4f);
                float shade = Random.Range(0.85f, 1.05f);
                emit.startColor = new Color(color.r * shade, color.g * shade, color.b * shade, 1f);
                emit.rotation3D = new Vector3(Random.Range(0f, 360f), Random.Range(0f, 360f), 0f);
                _sparks.Emit(emit, 1);
            }
        }

        private void EmitCubes(in Profile p, in ImpactEvent e, float intensity, float countScale)
        {
            int count = Mathf.Max(1, Mathf.RoundToInt(p.Cubes * Mathf.Lerp(0.6f, 1f, intensity) * countScale));
            Vector3 normal = e.normal.sqrMagnitude > 0.0001f ? e.normal.normalized : Vector3.up;
            // Bias the spray between the surface normal and the reflected travel direction, then add spread.
            Vector3 travel = e.direction.sqrMagnitude > 0.0001f ? e.direction.normalized : -normal;
            Vector3 baseDir = (normal + Vector3.Reflect(travel, normal)).normalized;
            if (e.kind == ImpactKind.MuzzleFire) baseDir = normal;

            var emit = new ParticleSystem.EmitParams { applyShapeToPosition = false };
            for (int i = 0; i < count; i++)
            {
                Vector3 dir = e.kind == ImpactKind.Explosion
                    ? (Random.onUnitSphere + Vector3.up * 0.8f).normalized
                    : (baseDir + Random.insideUnitSphere * 0.7f).normalized;
                float hotness = count > 1 ? (float)i / (count - 1) : 0f;
                emit.position = e.position + dir * 0.05f;
                emit.velocity = dir * Random.Range(p.Speed.x, p.Speed.y);
                emit.startSize = Random.Range(p.CubeSize.x, p.CubeSize.y);
                emit.startLifetime = Random.Range(p.Lifetime.x, p.Lifetime.y);
                emit.startColor = Color.Lerp(p.Hot, p.Cool, hotness * Random.Range(0.7f, 1f));
                emit.rotation3D = new Vector3(Random.Range(0f, 360f), Random.Range(0f, 360f), 0f);
                _sparks.Emit(emit, 1);
            }
        }

        /// <summary>
        /// Cuts each part (a unit cube scaled by its transform, as the placeholder models are built) into a grid of
        /// cubes in the part's colour and throws them away from <paramref name="point"/>, biased along
        /// <paramref name="direction"/>. Coarser cubes are used when a model or the frame would exceed its budget.
        /// The cubes bounce, settle on the floor, lie there and then shrink away.
        /// </summary>
        public void Shatter(Transform[] parts, Color[] colors, Vector3 point, Vector3 direction, float force = 1f)
        {
            if (parts == null || parts.Length == 0) return;
            if (_shatterFrame != Time.frameCount)
            {
                _shatterFrame = Time.frameCount;
                _shatterCubesThisFrame = 0;
            }

            // Every death still bursts: once the frame budget is spent, later kills get a few coarse cubes.
            int frameBudget = _maxCubesPerFrame - _shatterCubesThisFrame;
            int budget = Mathf.Clamp(frameBudget, MinCubesPerShatter, _maxCubesPerShatter);

            float volume = 0f;
            for (int i = 0; i < parts.Length; i++)
            {
                if (parts[i] == null) continue;
                Vector3 size = parts[i].lossyScale;
                volume += Mathf.Abs(size.x * size.y * size.z);
            }

            // Grow the cell until the model fits the budget (LOD by cube size instead of dropping parts).
            float cell = _shatterCell;
            while (volume / (cell * cell * cell) > budget) cell *= 1.26f; // ×2 volume per step

            Vector3 push = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector3.zero;
            var emit = new ParticleSystem.EmitParams { applyShapeToPosition = false };
            int emitted = 0;
            for (int i = 0; i < parts.Length && emitted < budget; i++)
            {
                Transform part = parts[i];
                if (part == null) continue;
                Vector3 size = part.lossyScale;
                float sx = Mathf.Abs(size.x), sy = Mathf.Abs(size.y), sz = Mathf.Abs(size.z);
                int nx = Mathf.Max(1, Mathf.RoundToInt(sx / cell));
                int ny = Mathf.Max(1, Mathf.RoundToInt(sy / cell));
                int nz = Mathf.Max(1, Mathf.RoundToInt(sz / cell));
                // Cube edge from the part's own cell volume: thin parts (eyes, horns) give smaller chunks, never
                // smaller than half a cell (still a few texels).
                float edge = Mathf.Clamp(Mathf.Pow((sx / nx) * (sy / ny) * (sz / nz), 1f / 3f), cell * 0.5f, cell);
                Color color = colors != null && i < colors.Length ? colors[i] : Color.white;

                for (int x = 0; x < nx && emitted < budget; x++)
                for (int y = 0; y < ny && emitted < budget; y++)
                for (int z = 0; z < nz && emitted < budget; z++)
                {
                    var local = new Vector3((x + 0.5f) / nx - 0.5f, (y + 0.5f) / ny - 0.5f, (z + 0.5f) / nz - 0.5f);
                    Vector3 pos = part.TransformPoint(local);
                    Vector3 away = pos - point;
                    away.y = Mathf.Abs(away.y) + 0.3f;
                    Vector3 dir = (away.normalized + push * 0.8f + Random.insideUnitSphere * 0.5f).normalized;

                    emit.position = pos;
                    emit.velocity = dir * (Random.Range(_shatterSpeed.x, _shatterSpeed.y) * force);
                    emit.startSize = edge * Random.Range(0.85f, 1f);
                    emit.startLifetime = Random.Range(_shatterLifetime.x, _shatterLifetime.y);
                    float shade = Random.Range(0.85f, 1.05f);
                    emit.startColor = new Color(color.r * shade, color.g * shade, color.b * shade, 1f);
                    emit.rotation3D = new Vector3(0f, Random.Range(0f, 360f), 0f);
                    _debris.Emit(emit, 1);
                    emitted++;
                }
            }

            _shatterCubesThisFrame += emitted;
        }

        /// <summary>Base colour of a renderer's shared material (placeholder flat materials).</summary>
        public static Color PartColor(Renderer renderer)
        {
            Material material = renderer != null ? renderer.sharedMaterial : null;
            return material != null && material.HasProperty(BaseColorId) ? material.GetColor(BaseColorId) : Color.gray;
        }

        // ---------------------------------------------------------------- shapes

        private void SpawnFlash(Vector3 position, float size, float grow, float fade, Color color, Vector3 axis, float stretch)
        {
            FlashShape f = _flashes[_nextFlash];
            _nextFlash = (_nextFlash + 1) % _flashes.Length;
            f.Active = true;
            f.Age = 0f;
            f.Grow = Mathf.Max(0.001f, grow);
            f.Fade = Mathf.Max(0.001f, fade);
            float length = size * (stretch > 0f ? stretch : 1f);
            f.BaseScale = new Vector3(size, size, length);

            Quaternion rotation;
            if (axis.sqrMagnitude > 1e-6f)
            {
                axis.Normalize();
                rotation = Quaternion.LookRotation(axis) * Quaternion.Euler(0f, 0f, Random.Range(0f, 90f));
                // A stretched flash (muzzle) starts at the point and blasts forward instead of reaching back into the gun.
                if (length > size) position += axis * ((length - size) * 0.5f);
            }
            else
            {
                rotation = Quaternion.Euler(0f, Random.Range(0f, 90f), 0f);
            }

            f.Transform.SetPositionAndRotation(position, rotation);
            f.Transform.localScale = f.BaseScale * 0.6f; // visible on frame 0
            _block.SetColor(BaseColorId, color);
            f.Renderer.SetPropertyBlock(_block);
            f.Renderer.enabled = true;
        }

        private void SpawnRing(Vector3 floor, float radius, in RingStyle style, float startFraction)
        {
            if (radius <= 0f) return;
            int wanted = Mathf.Max(8, Mathf.RoundToInt(2f * Mathf.PI * radius / _ringSpacing));
            Ring ring = AcquireRing(wanted);
            if (ring == null) return;

            ring.Segments = Mathf.Min(wanted, ring.Batch.Capacity);
            ring.Center = floor;
            ring.Radius = radius;
            ring.StartRadius = radius * Mathf.Clamp01(startFraction);
            ring.Style = style;
            ring.Style.Expand = Mathf.Max(0.01f, style.Expand);
            ring.Style.Fade = Mathf.Max(0.01f, style.Fade);
            ring.Age = 0f;
            ring.ColorStep = -1;
            ring.Active = true;
            UpdateRing(ring, 0f); // visible on frame 0
        }

        private Ring AcquireRing(int segments)
        {
            Ring best = null;
            // An idle ring that fits (the smallest such), else any idle ring, else the oldest.
            for (int i = 0; i < _rings.Length; i++)
            {
                Ring r = _rings[i];
                if (!r.Active && r.Batch.Capacity >= segments && (best == null || r.Batch.Capacity < best.Batch.Capacity)) best = r;
            }
            if (best != null) return best;
            for (int i = 0; i < _rings.Length; i++)
            {
                Ring r = _rings[i];
                if (!r.Active && (best == null || r.Batch.Capacity > best.Batch.Capacity)) best = r;
            }
            if (best != null) return best;
            for (int i = 0; i < _rings.Length; i++)
            {
                Ring r = _rings[i];
                if (best == null || r.Age > best.Age) best = r;
            }
            return best;
        }

        private void UpdateRing(Ring ring, float dt)
        {
            ring.Age += dt;
            RingStyle s = ring.Style;
            float t = ring.Age;
            float radius;
            float k;
            int step;
            if (t < s.Expand)
            {
                float x = t / s.Expand;
                radius = Mathf.Lerp(ring.StartRadius, ring.Radius, 1f - (1f - x) * (1f - x)); // fast out, eases in
                k = 1f;
                step = 0;
            }
            else if (t < s.Expand + s.Hold)
            {
                radius = ring.Radius;
                k = 1f;
                step = 1;
            }
            else
            {
                float x = (t - s.Expand - s.Hold) / s.Fade;
                if (x >= 1f)
                {
                    ring.Active = false;
                    ring.Batch.Hide();
                    return;
                }
                radius = ring.Radius;
                k = x < 0.34f ? 0.65f : (x < 0.67f ? 0.4f : 0.2f); // three hard steps, no soft fade (pixel look)
                step = 2;
            }

            if (step != ring.ColorStep)
            {
                ring.ColorStep = step;
                ring.Batch.SetColor(step == 0 ? s.Flash : (step == 1 ? s.Hot : s.Cool));
            }

            BoxMeshBatch batch = ring.Batch;
            int segments = ring.Segments;
            float angleStep = 2f * Mathf.PI / segments;
            float length = radius * angleStep * 0.62f * Mathf.Lerp(0.6f, 1f, k);
            float thickness = s.Thickness * Mathf.Lerp(0.5f, 1f, k);
            batch.Begin();
            for (int i = 0; i < segments; i++)
            {
                float a = (i + 0.5f) * angleStep;
                var radial = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                float h = s.Height * k * (0.75f + 0.25f * ((i * 5) % 3)); // jagged crown of cubes
                batch.AddBox(ring.Center + radial * radius + Vector3.up * (h * 0.5f), radial, new Vector3(length, h, thickness));
            }
            batch.End();
        }

        private Vector3 FloorPoint(Vector3 position)
        {
            if (Physics.Raycast(position + Vector3.up * 0.3f, Vector3.down, out RaycastHit hit, 8f, _groundMask,
                    QueryTriggerInteraction.Ignore))
                return new Vector3(position.x, hit.point.y + 0.02f, position.z);
            return position;
        }

        private static void PlayLight(LightFlash light, Light lightComponent, Vector3 position, float peak, float range,
            float duration, Color color, float scale)
        {
            if (light == null) return;
            light.transform.position = position + Vector3.up * 0.5f;
            light.peakIntensity = peak;
            light.duration = duration > 0f ? duration : 0.06f;
            light.SetRange(range > 0f ? range : 5f);
            if (lightComponent != null) lightComponent.color = color;
            light.Flash(scale);
        }

        /// <summary>The Devil's revive raises an Explosion from the DevilDirector object (K-12).</summary>
        private static bool IsRevive(in ImpactEvent e) =>
            e.instigator != null && e.instigator.TryGetComponent<DevilDirector>(out _);

        private static float BlastRadius(in ImpactEvent e, float fallback)
        {
            if (e.radius > 0f) return e.radius;
            // An explosion without a radius is the player's rocket: read the launcher's live radius, so Contract 1's
            // bigger blast shows a bigger ring without a hardcoded number (A-9 stays open).
            if (e.instigator != null)
            {
                RocketLauncher launcher = e.instigator.GetComponentInChildren<RocketLauncher>();
                if (launcher != null) return launcher.ExplosionRadius;
            }
            return fallback;
        }

        private void Update()
        {
            float dt = Time.unscaledDeltaTime;
            for (int i = 0; i < _flashes.Length; i++)
            {
                FlashShape f = _flashes[i];
                if (!f.Active) continue;
                f.Age += dt;
                float scale;
                if (f.Age < f.Grow) scale = Mathf.Lerp(0.6f, 1f, f.Age / f.Grow);
                else
                {
                    float k = (f.Age - f.Grow) / f.Fade;
                    if (k >= 1f)
                    {
                        f.Active = false;
                        f.Renderer.enabled = false;
                        continue;
                    }
                    scale = 1f - k * k; // eases out: holds big, then collapses
                }
                f.Transform.localScale = f.BaseScale * scale;
            }

            for (int i = 0; i < _rings.Length; i++)
                if (_rings[i].Active) UpdateRing(_rings[i], dt);
        }

        // ---------------------------------------------------------------- setup

        private ParticleSystem BuildCubeSystem(string objectName, int maxParticles, float gravity, float dragAmount,
            float dampen, float bounce, float radiusScale, AnimationCurve sizeCurve)
        {
            var go = new GameObject(objectName);
            go.transform.SetParent(transform, false);
            var system = go.AddComponent<ParticleSystem>();
            system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = system.main;
            main.loop = false;
            main.playOnAwake = false;
            main.duration = 1f;
            main.startLifetime = 0.5f;
            main.startSpeed = 0f;
            main.startSize3D = false;
            main.startSize = 0.1f;
            main.startRotation3D = true;
            main.gravityModifier = gravity;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;
            main.maxParticles = maxParticles;
            main.useUnscaledTime = false; // cubes freeze during hitstop, which sells the pause
            main.stopAction = ParticleSystemStopAction.None;

            var emission = system.emission;
            emission.enabled = false; // emitted by script only
            var shape = system.shape;
            shape.enabled = false;

            var drag = system.limitVelocityOverLifetime;
            drag.enabled = true;
            drag.limit = 100f;  // no speed cap: the module is only used for drag
            drag.dampen = 0f;
            drag.drag = dragAmount;
            drag.multiplyDragByParticleSize = false;
            drag.multiplyDragByParticleVelocity = false;

            var size = system.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, sizeCurve);

            var collision = system.collision;
            collision.enabled = true;
            collision.type = ParticleSystemCollisionType.World;
            collision.mode = ParticleSystemCollisionMode.Collision3D;
            collision.quality = ParticleSystemCollisionQuality.Medium;
            collision.dampen = dampen;
            collision.bounce = bounce;
            collision.lifetimeLoss = 0f;
            collision.radiusScale = radiusScale;

            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Mesh;
            renderer.mesh = CubeMesh();
            renderer.alignment = ParticleSystemRenderSpace.World;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            if (_cubeMaterial == null) _cubeMaterial = UnlitMaterial("Universal Render Pipeline/Particles/Unlit", "ImpactCubes");
            renderer.sharedMaterial = _cubeMaterial;
            return system;
        }

        private void BuildFlashes(int count)
        {
            Mesh cube = CubeMesh();
            _flashes = new FlashShape[count];
            for (int i = 0; i < count; i++)
            {
                var go = new GameObject($"ImpactFlash_{i}");
                go.transform.SetParent(transform, false);
                go.AddComponent<MeshFilter>().sharedMesh = cube;
                var mr = go.AddComponent<MeshRenderer>();
                mr.sharedMaterial = _resolvedFlashMaterial;
                mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                mr.receiveShadows = false;
                mr.enabled = false;
                _flashes[i] = new FlashShape { Transform = go.transform, Renderer = mr, BaseScale = Vector3.one };
            }
        }

        private void BuildRings()
        {
            _rings = new Ring[SmallRings + BigRings];
            for (int i = 0; i < _rings.Length; i++)
            {
                int capacity = i < SmallRings ? SmallRingCapacity : BigRingCapacity;
                _rings[i] = new Ring { Batch = new BoxMeshBatch($"ImpactRing_{i}", _resolvedFlashMaterial, capacity) };
            }
        }

        private LightFlash BuildLight(string objectName, out Light light)
        {
            var go = new GameObject(objectName);
            go.transform.SetParent(transform, false);
            light = go.AddComponent<Light>();
            light.type = LightType.Point;
            light.shadows = LightShadows.None;
            light.color = LightWarm;
            light.range = 5f;
            var flash = go.AddComponent<LightFlash>();
            flash.useUnscaledTime = true;
            return flash;
        }

        private static Material UnlitMaterial(string shaderName, string materialName)
        {
            Shader shader = Shader.Find(shaderName);
            if (shader == null)
            {
                Debug.LogError($"{nameof(PixelImpactFeedback)}: shader '{shaderName}' not found.");
                shader = Shader.Find("Universal Render Pipeline/Unlit");
            }
            return new Material(shader) { name = materialName };
        }

        private static Mesh _cubeMesh;

        private static Mesh CubeMesh()
        {
            if (_cubeMesh != null) return _cubeMesh;
            var temp = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _cubeMesh = temp.GetComponent<MeshFilter>().sharedMesh;
            Destroy(temp);
            return _cubeMesh;
        }
    }
}
