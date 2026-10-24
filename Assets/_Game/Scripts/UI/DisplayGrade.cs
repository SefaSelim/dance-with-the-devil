using UnityEngine;

namespace DanceWithTheDevil
{
    /// <summary>
    /// Owns the display grade material (shader DanceWithTheDevil/UI/DisplayGrade) on the pixel camera's RawImage and
    /// tweens saturation (death freeze, contract 1) and vignette (contract 5) on unscaled time. Also registers the rig
    /// as <see cref="UiKit.DisplayRig"/> so the UI canvases follow its game image.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class DisplayGrade : MonoBehaviour
    {
        private static readonly int SaturationId = Shader.PropertyToID("_Saturation");
        private static readonly int VignetteId = Shader.PropertyToID("_Vignette");

        [Tooltip("Pixel camera rig whose display image gets the grade.")]
        [SerializeField] private PixelCameraRig _rig;
        [Tooltip("Assets/_Game/Art/Shaders/UI_DisplayGrade.shader.")]
        [SerializeField] private Shader _shader;

        private Material _material;
        private Tween _saturation = new Tween(1f);
        private Tween _vignette = new Tween(0f);

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

        public float Saturation => _saturation.Value;
        public float Vignette => _vignette.Value;
        public Material Material => _material;

        private void Awake()
        {
            // The UI canvases fit themselves to this rig's game image (UiKit.CreateCanvas / UiCanvasFit).
            if (_rig != null) UiKit.DisplayRig = _rig;
            if (_shader == null || !_shader.isSupported)
            {
                Debug.LogWarning($"{nameof(DisplayGrade)}: grade shader missing or unsupported; no desaturation/vignette.", this);
                return;
            }
            _material = new Material(_shader) { name = "DisplayGrade (runtime)" };
            Push();
            if (_rig != null) _rig.DisplayMaterial = _material;
        }

        private void OnDestroy()
        {
            if (_rig != null && UiKit.DisplayRig == _rig) UiKit.DisplayRig = null;
            if (_material == null) return;
            if (_rig != null && _rig.DisplayMaterial == _material) _rig.DisplayMaterial = null;
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

        private void Update()
        {
            if (!_saturation.Active && !_vignette.Active) return;
            float dt = Time.unscaledDeltaTime;
            if (_saturation.Active) _saturation.Tick(dt);
            if (_vignette.Active) _vignette.Tick(dt);
            Push();
        }

        private void Push()
        {
            if (_material == null) return;
            _material.SetFloat(SaturationId, _saturation.Value);
            _material.SetFloat(VignetteId, _vignette.Value);
        }
    }
}
