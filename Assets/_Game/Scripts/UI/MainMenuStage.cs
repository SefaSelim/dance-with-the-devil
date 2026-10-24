using UnityEngine;

namespace DanceWithTheDevil
{
    /// <summary>
    /// The main menu's 3D stage (design/ux/main-menu.md §2): Defne sways slowly on her plinth (±8°), the Devil engraving
    /// behind her breathes and his red back light flickers like a candle. All motion is skipped with UiKit.ReduceMotion.
    /// </summary>
    public sealed class MainMenuStage : MonoBehaviour
    {
        [SerializeField] private Transform _defne;
        [SerializeField] private float _swayDegrees = 8f;
        [SerializeField] private float _swayPeriod = 9f;
        [SerializeField] private Transform _devil;
        [SerializeField] private float _breathScale = 0.025f;
        [SerializeField] private float _breathPeriod = 5f;
        [SerializeField] private Light _devilLight;
        [SerializeField] private float _flicker = 0.18f;
        [SerializeField] private Transform _sigil;
        [SerializeField] private float _sigilDegreesPerSecond = 4f;

        private float _baseYaw;
        private Vector3 _devilScale;
        private float _lightIntensity;

        private void Start()
        {
            if (_defne != null) _baseYaw = _defne.localEulerAngles.y;
            if (_devil != null) _devilScale = _devil.localScale;
            if (_devilLight != null) _lightIntensity = _devilLight.intensity;
        }

        private void Update()
        {
            if (UiKit.ReduceMotion) return;
            float t = Time.unscaledTime;
            if (_defne != null)
            {
                float yaw = _baseYaw + Mathf.Sin(t * 2f * Mathf.PI / _swayPeriod) * _swayDegrees;
                _defne.localRotation = Quaternion.Euler(0f, yaw, 0f);
            }
            if (_devil != null)
            {
                float s = 1f + Mathf.Sin(t * 2f * Mathf.PI / _breathPeriod) * _breathScale;
                _devil.localScale = _devilScale * s;
            }
            if (_devilLight != null)
            {
                float n = Mathf.PerlinNoise(t * 3f, 0.37f) - 0.5f;
                _devilLight.intensity = _lightIntensity * (1f + n * 2f * _flicker);
            }
            if (_sigil != null) _sigil.Rotate(0f, 0f, _sigilDegreesPerSecond * Time.unscaledDeltaTime, Space.Self);
        }

        /// <summary>Editor builder hook.</summary>
        public void Configure(Transform defne, Transform devil, Light devilLight, Transform sigil)
        {
            _defne = defne;
            _devil = devil;
            _devilLight = devilLight;
            _sigil = sigil;
        }
    }
}
