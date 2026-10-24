using UnityEngine;

namespace DanceWithTheDevil
{
    /// <summary>
    /// Owns the display grade material (shader DanceWithTheDevil/UI/DisplayGrade) on the pixel camera's RawImage and
    /// tweens saturation (death freeze, contract 1) and vignette (contract 5) on unscaled time. Also registers the rig
    /// as <see cref="UiKit.DisplayRig"/> so the UI canvases follow its game image.
    /// Screen bending: up to 4 concurrent shockwave rings (<see cref="Shockwave(Vector3, float)"/>) anchored to world
    /// points, and the rig's roll and zoom kick (<see cref="PixelCameraRig.Roll"/>, <see cref="PixelCameraRig.Zoom"/>),
    /// all applied in the shader inside the visible window. Scaled by <see cref="PixelCameraRig.GlobalShakeScale"/>,
    /// off with <see cref="UiKit.ReduceMotion"/>. Runs after the rig (order 1000) so it uses this frame's window.
    /// </summary>
    [DefaultExecutionOrder(1100)]
    [DisallowMultipleComponent]
    public sealed class DisplayGrade : MonoBehaviour
    {
        private static readonly int SaturationId = Shader.PropertyToID("_Saturation");
        private static readonly int VignetteId = Shader.PropertyToID("_Vignette");
        private static readonly int UvRectId = Shader.PropertyToID("_UvRect");
        private static readonly int KickId = Shader.PropertyToID("_Kick");
        private static readonly int WavesId = Shader.PropertyToID("_Waves");
        private static readonly int WaveStrengthId = Shader.PropertyToID("_WaveStrength");

        /// <summary>Most shockwave rings at once.</summary>
        public const int MaxWaves = 4;

        [Tooltip("Pixel camera rig whose display image gets the grade.")]
        [SerializeField] private PixelCameraRig _rig;
        [Tooltip("Assets/_Game/Art/Shaders/UI_DisplayGrade.shader.")]
        [SerializeField] private Shader _shader;

        [Header("Shockwave warp")]
        [Tooltip("Peak displacement at strength 1, as a fraction of the image height (0.022 ≈ 12 texels at 540).")]
        [SerializeField, Range(0f, 0.08f)] private float _warpAmplitude = 0.022f;
        [Tooltip("Ring width as a fraction of its radius (clamped to 0.02–0.12 of the image height).")]
        [SerializeField, Range(0.05f, 0.8f)] private float _warpWidth = 0.3f;

        private Material _material;
        private Tween _saturation = new Tween(1f);
        private Tween _vignette = new Tween(0f);

        private struct Wave
        {
            public Vector3 World;
            public float Age, Duration, Radius, Amplitude;
            public bool Active;
            public float Remaining => Active ? Amplitude * Sq(1f - Mathf.Clamp01(Age / Duration)) : 0f;
        }

        private readonly Wave[] _waves = new Wave[MaxWaves];
        private readonly Vector4[] _waveData = new Vector4[MaxWaves];
        private readonly Vector4[] _waveStrength = new Vector4[MaxWaves];

        private struct Tween
        {
            public float Value, From, To, Elapsed, Duration;
            public Tween(float v) { Value = From = To = v; Elapsed = Duration = 0f; }
            public bool Active => Value != To;

            public void Start(float to, float duration)
            {
                From = Value;
                To = Mathf.Clamp01(to);
                Elapsed = 0f;
                Duration = UiKit.ReduceMotion ? 0f : Mathf.Max(0f, duration);
                if (Duration <= 0f) Value = To;
            }

            public void Tick(float dt)
            {
                Elapsed += dt;
                Value = Duration <= 0f ? To : Mathf.Lerp(From, To, Mathf.Clamp01(Elapsed / Duration));
            }
        }

        /// <summary>The scene's display grade (null when none).</summary>
        public static DisplayGrade Instance { get; private set; }

        public float Saturation => _saturation.Value;
        public float Vignette => _vignette.Value;
        public Material Material => _material;
        /// <summary>Running shockwave rings (for tests).</summary>
        public int ActiveWaves
        {
            get
            {
                int n = 0;
                for (int i = 0; i < MaxWaves; i++) if (_waves[i].Active) n++;
                return n;
            }
        }

        private void Awake()
        {
            Instance = this;
            // The UI canvases fit themselves to this rig's game image (UiKit.CreateCanvas / UiCanvasFit).
            if (_rig != null) UiKit.DisplayRig = _rig;
            if (_shader == null || !_shader.isSupported)
            {
                Debug.LogWarning($"{nameof(DisplayGrade)}: grade shader missing or unsupported; no desaturation/vignette/warp.", this);
                return;
            }
            _material = new Material(_shader) { name = "DisplayGrade (runtime)" };
            Push();
            if (_rig != null)
            {
                _rig.DisplayMaterial = _material;
                _rig.ApplyRollZoom = true;
            }
        }

        // Aim (PixelCameraRig.ScreenToGameViewport) includes roll/zoom only while this shader draws them.
        private void OnEnable()
        {
            if (_rig != null && _material != null && _rig.DisplayMaterial == _material) _rig.ApplyRollZoom = true;
        }

        private void OnDisable()
        {
            if (_rig != null) _rig.ApplyRollZoom = false;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            if (_rig != null && UiKit.DisplayRig == _rig) UiKit.DisplayRig = null;
            if (_material == null) return;
            if (_rig != null && _rig.DisplayMaterial == _material)
            {
                _rig.DisplayMaterial = null;
                _rig.ApplyRollZoom = false;
            }
            Destroy(_material);
        }

        public void SetSaturation(float target, float duration)
        {
            _saturation.Start(target, duration);
            Push();
        }

        public void SetVignette(float target, float duration)
        {
            _vignette.Start(target, duration);
            Push();
        }

        /// <summary>
        /// A screen shockwave ring at <paramref name="worldPos"/>. <paramref name="strength"/> ~0.3 for a kill, 1 for a
        /// rocket, 1.6 for the Devil's revive; radius 1.5 + 6 × strength metres, 0.25 + 0.35 × strength seconds.
        /// Safe to call when the scene has no DisplayGrade. Example: <c>DisplayGrade.Shockwave(blastPos, 1f);</c>
        /// </summary>
        public static void Shockwave(Vector3 worldPos, float strength)
        {
            float s = Mathf.Max(0f, strength);
            Shockwave(worldPos, s, 1.5f + 6f * s, Mathf.Clamp(0.25f + 0.35f * s, 0.2f, 1f));
        }

        /// <summary>
        /// A screen shockwave ring with an explicit final <paramref name="radiusMetres"/> (world) and
        /// <paramref name="duration"/> (seconds, unscaled). When 4 rings run, it replaces the weakest one only if it is
        /// stronger. Example: <c>DisplayGrade.Shockwave(defne.position, 1.6f, 14f, 0.9f);</c>
        /// </summary>
        public static void Shockwave(Vector3 worldPos, float strength, float radiusMetres, float duration)
        {
            if (Instance != null) Instance.AddWave(worldPos, strength, radiusMetres, duration);
        }

        private void AddWave(Vector3 world, float strength, float radiusMetres, float duration)
        {
            if (_material == null || _rig == null || UiKit.ReduceMotion) return;
            float amplitude = _warpAmplitude * Mathf.Max(0f, strength) * Mathf.Clamp(PixelCameraRig.GlobalShakeScale, 0f, 1.5f);
            if (amplitude <= 1e-5f || radiusMetres <= 0f) return;

            int slot = -1;
            float weakest = float.PositiveInfinity;
            for (int i = 0; i < MaxWaves; i++)
            {
                float r = _waves[i].Remaining;
                if (r < weakest)
                {
                    weakest = r;
                    slot = i;
                }
            }
            if (slot < 0 || (_waves[slot].Active && weakest > amplitude)) return;

            _waves[slot] = new Wave
            {
                World = world,
                Age = 0f,
                Duration = Mathf.Max(0.05f, duration),
                Radius = radiusMetres / Mathf.Max(0.1f, _rig.ViewHeightMetres), // fraction of the image height
                Amplitude = amplitude,
                Active = true,
            };
        }

        private void LateUpdate()
        {
            float dt = Time.unscaledDeltaTime;
            if (_saturation.Active) _saturation.Tick(dt);
            if (_vignette.Active) _vignette.Tick(dt);

            bool reduce = UiKit.ReduceMotion;
            for (int i = 0; i < MaxWaves; i++)
            {
                ref Wave w = ref _waves[i];
                if (w.Active)
                {
                    w.Age += dt;
                    if (w.Age >= w.Duration || reduce) w.Active = false;
                }
                if (!w.Active || _rig == null)
                {
                    _waveData[i] = Vector4.zero;
                    _waveStrength[i] = Vector4.zero;
                    continue;
                }
                float t = Mathf.Clamp01(w.Age / w.Duration);
                float radius = w.Radius * (1f - Sq(1f - t) * (1f - t)); // ease-out cubic: fast out, slow settle
                float width = Mathf.Clamp(w.Radius * _warpWidth, 0.02f, 0.12f) * Mathf.Lerp(0.6f, 1f, t);
                Vector2 c = _rig.WorldToDisplay01(w.World);
                _waveData[i] = new Vector4(c.x, c.y, radius, width);
                _waveStrength[i] = new Vector4(w.Remaining, 0f, 0f, 0f);
            }
            Push();
        }

        private void Push()
        {
            if (_material == null) return;
            _material.SetFloat(SaturationId, _saturation.Value);
            _material.SetFloat(VignetteId, _vignette.Value);
            if (_rig == null) return;
            Rect r = _rig.DisplayUvRect;
            _material.SetVector(UvRectId, new Vector4(r.x, r.y, r.width, r.height));
            float zoom = Mathf.Max(0.01f, _rig.Zoom);
            float roll = _rig.Roll;
            _material.SetVector(KickId, new Vector4(Mathf.Cos(roll) / zoom, Mathf.Sin(roll) / zoom, _rig.DisplayAspect, 0f));
            _material.SetVectorArray(WavesId, _waveData);
            _material.SetVectorArray(WaveStrengthId, _waveStrength);
        }

        private static float Sq(float x) => x * x;
    }
}
