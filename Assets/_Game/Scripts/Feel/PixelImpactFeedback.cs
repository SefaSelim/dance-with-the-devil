using UnityEngine;

namespace DanceWithTheDevil
{
    /// <summary>
    /// Fans every <see cref="ImpactEvent"/> out to all feel channels on the same frame: flash shape, cube burst,
    /// light, camera trauma and hitstop. Tuned for the 12 px/m pixel camera: shapes are chunky cubes, not soft
    /// sprites, and shake is an image offset in texels (see <see cref="PixelCameraRig.AddTrauma"/>).
    /// Builds its own particle system, flash meshes and lights at runtime, so the scene only needs this component.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PixelImpactFeedback : MonoBehaviour
    {
        [System.Serializable]
        private struct Profile
        {
            [Tooltip("Cubes emitted (scaled by event intensity, at least 1).")]
            public int Cubes;
            [Tooltip("Cube edge length range in metres.")]
            public Vector2 CubeSize;
            [Tooltip("Cube speed range in m/s.")]
            public Vector2 Speed;
            [Tooltip("Cube lifetime range in seconds.")]
            public Vector2 Lifetime;
            [Tooltip("Hot colour at birth (first cubes) and cool colour (last cubes).")]
            public Color Hot;
            public Color Cool;
            [Tooltip("Flash shape peak size in metres (0 = none).")]
            public float FlashSize;
            [Tooltip("Flash grow time and fade time in seconds (unscaled).")]
            public float FlashGrow;
            public float FlashFade;
            [Tooltip("Camera trauma added (0..1).")]
            public float Trauma;
            [Tooltip("Global hitstop in seconds (0 = none).")]
            public float HitStop;
            [Tooltip("Light peak intensity (0 = none) and range in metres.")]
            public float LightIntensity;
            public float LightRange;
        }

        private sealed class FlashShape
        {
            public Transform Transform;
            public MeshRenderer Renderer;
            public float Age;
            public float Grow;
            public float Fade;
            public float Size;
            public bool Active;
        }

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        [SerializeField] private PixelCameraRig _cameraRig;
        [Tooltip("Unlit material for burst cubes (vertex colour). Falls back to a runtime URP Particles/Unlit material.")]
        [SerializeField] private Material _cubeMaterial;
        [Tooltip("Unlit material for flash shapes (_BaseColor set per flash). Falls back to a runtime URP Unlit material.")]
        [SerializeField] private Material _flashMaterial;

        [Header("Profiles (starting values, tune by eye at 0.1 time scale)")]
        [SerializeField] private Profile _muzzle = new Profile
        {
            Cubes = 2, CubeSize = new Vector2(0.06f, 0.1f), Speed = new Vector2(3f, 6f), Lifetime = new Vector2(0.08f, 0.14f),
            Hot = new Color(1f, 1f, 1f), Cool = new Color(0.37f, 1f, 0.94f),
            FlashSize = 0.35f, FlashGrow = 0.016f, FlashFade = 0.033f, Trauma = 0.03f, HitStop = 0f,
            LightIntensity = 3f, LightRange = 4f,
        };
        [SerializeField] private Profile _bullet = new Profile
        {
            Cubes = 5, CubeSize = new Vector2(0.07f, 0.12f), Speed = new Vector2(2f, 4.5f), Lifetime = new Vector2(0.2f, 0.4f),
            Hot = new Color(1f, 1f, 1f), Cool = new Color(0.37f, 1f, 0.94f),
            FlashSize = 0.3f, FlashGrow = 0.016f, FlashFade = 0.05f, Trauma = 0.04f, HitStop = 0f,
            LightIntensity = 0f, LightRange = 0f,
        };
        [SerializeField] private Profile _melee = new Profile
        {
            Cubes = 10, CubeSize = new Vector2(0.08f, 0.16f), Speed = new Vector2(3f, 6.5f), Lifetime = new Vector2(0.3f, 0.55f),
            Hot = new Color(1f, 0.95f, 0.8f), Cool = new Color(0.78f, 0.2f, 0.24f),
            FlashSize = 0.7f, FlashGrow = 0.016f, FlashFade = 0.08f, Trauma = 0.3f, HitStop = 0.06f,
            LightIntensity = 0f, LightRange = 0f,
        };
        [SerializeField] private Profile _explosion = new Profile
        {
            Cubes = 32, CubeSize = new Vector2(0.12f, 0.3f), Speed = new Vector2(4f, 10f), Lifetime = new Vector2(0.4f, 0.9f),
            Hot = new Color(1f, 0.9f, 0.55f), Cool = new Color(0.3f, 0.12f, 0.1f),
            FlashSize = 5f, FlashGrow = 0.05f, FlashFade = 0.15f, Trauma = 0.8f, HitStop = 0.1f,
            LightIntensity = 8f, LightRange = 9f,
        };

        [Header("Shatter (death bursts)")]
        [Tooltip("Voxel edge length used to cut a dying model into cubes (metres).")]
        [SerializeField, Min(0.02f)] private float _shatterCell = 0.12f;
        [Tooltip("Most cubes one death may emit; larger models use coarser cubes.")]
        [SerializeField, Min(8)] private int _maxCubesPerShatter = 400;
        [Tooltip("Most cubes all shatters may emit in one frame; later kills that frame use coarser cubes.")]
        [SerializeField, Min(8)] private int _maxCubesPerFrame = 1500;
        [Tooltip("Shatter cube speed range (m/s).")]
        [SerializeField] private Vector2 _shatterSpeed = new Vector2(2f, 6f);
        [Tooltip("Shatter cube lifetime range (s): they bounce, settle, then shrink away.")]
        [SerializeField] private Vector2 _shatterLifetime = new Vector2(1.2f, 2.4f);

        [Header("Limits")]
        [Tooltip("Hard cap on live cubes from this router.")]
        [SerializeField, Min(16)] private int _maxCubes = 8000;

        /// <summary>The scene's feedback router (one per scene).</summary>
        public static PixelImpactFeedback Instance { get; private set; }

        private const int MinCubesPerShatter = 16;

        private ParticleSystem _cubes;
        private int _shatterFrame = -1;
        private int _shatterCubesThisFrame;
        private FlashShape[] _flashes;
        private int _nextFlash;
        private MaterialPropertyBlock _block;
        private LightFlash _smallLight;
        private LightFlash _bigLight;

        private void Awake()
        {
            Instance = this;
            _block = new MaterialPropertyBlock();
            if (HitStop.Instance == null) gameObject.AddComponent<HitStop>();
            BuildCubeSystem();
            BuildFlashes(6);
            _smallLight = BuildLight("MuzzleLight");
            _bigLight = BuildLight("ExplosionLight");
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
                case ImpactKind.MuzzleFire: Play(_muzzle, e, _smallLight); break;
                case ImpactKind.Bullet: Play(_bullet, e, null); break;
                case ImpactKind.Melee: Play(_melee, e, null); break;
                case ImpactKind.Explosion: Play(_explosion, e, _bigLight); break;
            }
        }

        private void Play(in Profile p, in ImpactEvent e, LightFlash light)
        {
            float intensity = Mathf.Clamp01(e.intensity <= 0f ? 1f : e.intensity);

            if (p.FlashSize > 0f) SpawnFlash(e.position, p.FlashSize * Mathf.Lerp(0.6f, 1f, intensity), p.FlashGrow, p.FlashFade, p.Hot);
            EmitCubes(p, e, intensity);

            if (light != null && p.LightIntensity > 0f)
            {
                light.transform.position = e.position + Vector3.up * 0.5f;
                light.peakIntensity = p.LightIntensity;
                light.GetComponent<Light>().range = p.LightRange;
                light.Flash(intensity);
            }

            // The fixed camera always sees the whole arena, so trauma has no distance falloff.
            if (_cameraRig != null && p.Trauma > 0f) _cameraRig.AddTrauma(p.Trauma * intensity);

            if (p.HitStop > 0f && HitStop.Instance != null) HitStop.Instance.TriggerGlobal(p.HitStop * Mathf.Lerp(0.5f, 1f, intensity));
        }

        private void EmitCubes(in Profile p, in ImpactEvent e, float intensity)
        {
            int count = Mathf.Max(1, Mathf.RoundToInt(p.Cubes * intensity));
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
                _cubes.Emit(emit, 1);
            }
        }

        /// <summary>
        /// Cuts each part (a unit cube scaled by its transform, as the placeholder models are built) into a grid of
        /// cubes in the part's colour and throws them away from <paramref name="point"/>, biased along
        /// <paramref name="direction"/>. Coarser cubes are used when a model or the frame would exceed its budget.
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
                Vector3 size = part.lossyScale;
                int nx = Mathf.Max(1, Mathf.RoundToInt(Mathf.Abs(size.x) / cell));
                int ny = Mathf.Max(1, Mathf.RoundToInt(Mathf.Abs(size.y) / cell));
                int nz = Mathf.Max(1, Mathf.RoundToInt(Mathf.Abs(size.z) / cell));
                Color color = i < colors.Length ? colors[i] : Color.white;

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
                    emit.startSize = cell * Random.Range(0.85f, 1f);
                    emit.startLifetime = Random.Range(_shatterLifetime.x, _shatterLifetime.y);
                    emit.startColor = color * Random.Range(0.85f, 1.05f);
                    emit.rotation3D = new Vector3(0f, Random.Range(0f, 360f), 0f);
                    _cubes.Emit(emit, 1);
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

        private void SpawnFlash(Vector3 position, float size, float grow, float fade, Color color)
        {
            FlashShape f = _flashes[_nextFlash];
            _nextFlash = (_nextFlash + 1) % _flashes.Length;
            f.Active = true;
            f.Age = 0f;
            f.Grow = Mathf.Max(0.001f, grow);
            f.Fade = Mathf.Max(0.001f, fade);
            f.Size = size;
            f.Transform.position = position;
            f.Transform.localScale = Vector3.one * (size * 0.5f); // visible on frame 0
            _block.SetColor(BaseColorId, color);
            f.Renderer.SetPropertyBlock(_block);
            f.Renderer.enabled = true;
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
                if (f.Age < f.Grow) scale = Mathf.Lerp(0.5f, 1f, f.Age / f.Grow);
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
                f.Transform.localScale = Vector3.one * (f.Size * scale);
            }
        }

        // ---------------------------------------------------------------- setup

        private void BuildCubeSystem()
        {
            var go = new GameObject("ImpactCubes");
            go.transform.SetParent(transform, false);
            _cubes = go.AddComponent<ParticleSystem>();
            _cubes.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = _cubes.main;
            main.loop = false;
            main.playOnAwake = false;
            main.duration = 1f;
            main.startLifetime = 0.5f;
            main.startSpeed = 0f;
            main.startSize3D = false;
            main.startSize = 0.1f;
            main.startRotation3D = true;
            main.gravityModifier = 1.2f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;
            main.maxParticles = _maxCubes;
            main.useUnscaledTime = false; // debris freezes during hitstop, which sells the pause
            main.stopAction = ParticleSystemStopAction.None;

            var emission = _cubes.emission;
            emission.enabled = false; // emitted by script only
            var shape = _cubes.shape;
            shape.enabled = false;

            var drag = _cubes.limitVelocityOverLifetime;
            drag.enabled = true;
            drag.drag = 2.5f;
            drag.multiplyDragByParticleSize = false;
            drag.multiplyDragByParticleVelocity = false;

            var size = _cubes.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
                new Keyframe(0f, 1f), new Keyframe(0.7f, 0.85f), new Keyframe(1f, 0f)));

            var collision = _cubes.collision;
            collision.enabled = true;
            collision.type = ParticleSystemCollisionType.World;
            collision.mode = ParticleSystemCollisionMode.Collision3D;
            collision.quality = ParticleSystemCollisionQuality.Medium;
            collision.dampen = 0.5f;
            collision.bounce = 0.3f;
            collision.lifetimeLoss = 0f;
            collision.radiusScale = 0.5f;

            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Mesh;
            renderer.mesh = CubeMesh();
            renderer.alignment = ParticleSystemRenderSpace.World;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.sharedMaterial = _cubeMaterial != null ? _cubeMaterial : UnlitMaterial("Universal Render Pipeline/Particles/Unlit", "ImpactCubes");
        }

        private void BuildFlashes(int count)
        {
            Material material = _flashMaterial != null ? _flashMaterial : UnlitMaterial("Universal Render Pipeline/Unlit", "ImpactFlash");
            Mesh cube = CubeMesh();
            _flashes = new FlashShape[count];
            for (int i = 0; i < count; i++)
            {
                var go = new GameObject($"ImpactFlash_{i}");
                go.transform.SetParent(transform, false);
                go.AddComponent<MeshFilter>().sharedMesh = cube;
                var mr = go.AddComponent<MeshRenderer>();
                mr.sharedMaterial = material;
                mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                mr.receiveShadows = false;
                mr.enabled = false;
                _flashes[i] = new FlashShape { Transform = go.transform, Renderer = mr };
            }
        }

        private LightFlash BuildLight(string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            var light = go.AddComponent<Light>();
            light.type = LightType.Point;
            light.shadows = LightShadows.None;
            light.color = new Color(1f, 0.85f, 0.6f);
            light.range = 5f;
            var flash = go.AddComponent<LightFlash>();
            flash.useUnscaledTime = true;
            return flash;
        }

        private static Material UnlitMaterial(string shaderName, string name)
        {
            Shader shader = Shader.Find(shaderName);
            if (shader == null)
            {
                Debug.LogError($"{nameof(PixelImpactFeedback)}: shader '{shaderName}' not found.");
                shader = Shader.Find("Universal Render Pipeline/Unlit");
            }
            return new Material(shader) { name = name };
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
