using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace DanceWithTheDevil
{
    /// <summary>
    /// Camera and post-processing feedback for the combo (one event, same frame: every layer fires from the LateUpdate of
    /// the frame the count changed, before <see cref="PixelCameraRig"/> (order 1000) applies the shake).
    /// <list type="bullet">
    /// <item>Count: trauma 0.01 + 0.012 × tier once per frame with counted hits (rifle 6-10 hits/s never builds up:
    /// tier 4 adds 0.058 per hit, the rig decays 0.16 per 0.1 s).</item>
    /// <item>Tier-up: trauma 0.25 + 0.08 × tier, and a global Volume pulse (Bloom, ChromaticAberration, LensDistortion,
    /// ColorAdjustments postExposure/saturation) whose weight rises in 2 frames and falls (1 - u)² over 0.35 s, unscaled.
    /// Optional global hitstop (default 0: the approved design says no hitstop on tier-up; ≤ 0.05 s, never extends a
    /// running weapon hitstop).</item>
    /// <item>Sustained glow: a second Volume whose weight eases (half-life 0.4 s) toward 0.5 at tier 3 and 1 at tier 4.</item>
    /// </list>
    /// Accessibility: shake goes through <see cref="PixelCameraRig.GlobalShakeScale"/>; pulse and glow weights through
    /// <see cref="FlashScale"/>; lens distortion is also scaled by the shake setting and is off with
    /// <see cref="UiKit.ReduceMotion"/>. The base look (calm bloom, no vignette, no tonemapping) is the scene's
    /// ArenaVolumeProfile; the pulse and glow profiles are built here at runtime and destroyed with the component.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ComboFeel : MonoBehaviour
    {
        /// <summary>Accessibility multiplier for full-screen flashes/pulses (0 = off).</summary>
        public static float FlashScale = 1f;

        [SerializeField] private ComboTracker _combo;
        [SerializeField] private PixelCameraRig _rig;

        [Header("Per count (shake)")]
        [SerializeField, Range(0f, 0.1f)] private float _hitTrauma = 0.01f;
        [SerializeField, Range(0f, 0.05f)] private float _hitTraumaPerTier = 0.012f;

        [Header("Tier-up")]
        [SerializeField, Range(0f, 1f)] private float _tierTrauma = 0.25f;
        [SerializeField, Range(0f, 0.2f)] private float _tierTraumaPerTier = 0.08f;
        [Tooltip("Global hitstop on tier-up, seconds. The approved design has none (0); keep ≤ 0.05 if enabled.")]
        [SerializeField, Range(0f, 0.05f)] private float _tierHitStop = 0f;
        [SerializeField, Min(0.001f)] private float _pulseAttack = 0.033f;
        [SerializeField, Min(0.01f)] private float _pulseDecay = 0.35f;
        [Tooltip("Pulse weight per tier 1..4 (later tiers hit harder).")]
        [SerializeField] private float[] _pulseWeightByTier = { 0.55f, 0.7f, 0.85f, 1f };

        [Header("Pulse profile (values at weight 1)")]
        [SerializeField, Min(0f)] private float _pulseBloom = 1.6f;
        [SerializeField, Range(0f, 1f)] private float _pulseChromatic = 0.35f;
        [SerializeField, Range(-1f, 1f)] private float _pulseDistortion = -0.12f;
        [SerializeField, Range(-2f, 2f)] private float _pulseExposure = 0.3f;
        [SerializeField, Range(-100f, 100f)] private float _pulseSaturation = 15f;

        [Header("Sustained glow (values at weight 1)")]
        [SerializeField, Min(0f)] private float _glowBloom = 0.8f;
        [SerializeField, Range(-100f, 100f)] private float _glowSaturation = 8f;
        [SerializeField, Min(0.01f)] private float _glowHalfLife = 0.4f;
        [Tooltip("Glow weight per tier 0..4.")]
        [SerializeField] private float[] _glowWeightByTier = { 0f, 0f, 0f, 0.5f, 1f };

        private Volume _pulseVolume;
        private Volume _glowVolume;
        private VolumeProfile _pulseProfile;
        private VolumeProfile _glowProfile;
        private LensDistortion _distortion;

        private int _lastCount;
        private int _hitsThisFrame;
        private int _tierUpThisFrame;
        private float _pulseTime = float.PositiveInfinity;
        private float _pulsePeak;
        private float _glowWeight;

        private void Awake()
        {
            _pulseProfile = ScriptableObject.CreateInstance<VolumeProfile>();
            _pulseProfile.name = "ComboPulse (runtime)";
            _pulseProfile.Add<Bloom>(false).intensity.Override(_pulseBloom);
            _pulseProfile.Add<ChromaticAberration>(false).intensity.Override(_pulseChromatic);
            _distortion = _pulseProfile.Add<LensDistortion>(false);
            _distortion.intensity.Override(0f);
            ColorAdjustments pulseColor = _pulseProfile.Add<ColorAdjustments>(false);
            pulseColor.postExposure.Override(_pulseExposure);
            pulseColor.saturation.Override(_pulseSaturation);

            _glowProfile = ScriptableObject.CreateInstance<VolumeProfile>();
            _glowProfile.name = "ComboGlow (runtime)";
            _glowProfile.Add<Bloom>(false).intensity.Override(_glowBloom);
            _glowProfile.Add<ColorAdjustments>(false).saturation.Override(_glowSaturation);

            _glowVolume = CreateVolume("ComboGlowVolume", _glowProfile, 5f);
            _pulseVolume = CreateVolume("ComboPulseVolume", _pulseProfile, 10f);
        }

        private void OnDestroy()
        {
            if (_pulseProfile != null) Destroy(_pulseProfile);
            if (_glowProfile != null) Destroy(_glowProfile);
        }

        private void OnEnable()
        {
            if (_combo == null) return;
            _combo.Changed += OnChanged;
            _combo.TierUp += OnTierUp;
        }

        private void OnDisable()
        {
            if (_combo != null)
            {
                _combo.Changed -= OnChanged;
                _combo.TierUp -= OnTierUp;
            }
            if (_pulseVolume != null) _pulseVolume.weight = 0f;
            if (_glowVolume != null) _glowVolume.weight = 0f;
            _pulseTime = float.PositiveInfinity;
            _glowWeight = 0f;
        }

        /// <summary>Code-built setups.</summary>
        public void Configure(ComboTracker combo, PixelCameraRig rig)
        {
            OnDisable();
            _combo = combo;
            _rig = rig;
            if (isActiveAndEnabled) OnEnable();
        }

        private Volume CreateVolume(string name, VolumeProfile profile, float priority)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            go.layer = 0; // Default: inside the camera's default volume layer mask
            Volume volume = go.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.priority = priority;
            volume.weight = 0f;
            volume.sharedProfile = profile;
            return volume;
        }

        private void OnChanged(int count)
        {
            if (count > _lastCount) _hitsThisFrame += count - _lastCount;
            _lastCount = count;
        }

        private void OnTierUp(int tier)
        {
            if (tier > _tierUpThisFrame) _tierUpThisFrame = tier;
        }

        private void LateUpdate()
        {
            int tier = _combo != null ? _combo.Tier : 0;

            if (_tierUpThisFrame > 0)
            {
                int t = _tierUpThisFrame;
                if (_rig != null) _rig.AddTrauma(_tierTrauma + _tierTraumaPerTier * t);
                StartPulse(t);
                // Never extend or stack on a weapon hitstop that is already running.
                if (_tierHitStop > 0f && HitStop.Instance != null && !HitStop.Instance.IsGlobalActive)
                    HitStop.Instance.TriggerGlobal(Mathf.Min(_tierHitStop, 0.05f));
            }
            else if (_hitsThisFrame > 0 && _rig != null)
            {
                _rig.AddTrauma(_hitTrauma + _hitTraumaPerTier * tier);
            }
            _hitsThisFrame = 0;
            _tierUpThisFrame = 0;

            float dt = Time.unscaledDeltaTime;
            float flash = Mathf.Clamp01(FlashScale);

            // Pulse envelope: linear attack over ~2 frames, then (1 - u)^2 decay (fast in, slow out).
            float pulse = 0f;
            if (_pulseTime < _pulseAttack + _pulseDecay)
            {
                _pulseTime += dt;
                pulse = _pulseTime < _pulseAttack
                    ? _pulseTime / _pulseAttack
                    : Sq(1f - Mathf.Clamp01((_pulseTime - _pulseAttack) / _pulseDecay));
            }
            if (_pulseVolume != null) _pulseVolume.weight = pulse * _pulsePeak * flash;

            // Glow: frame-rate independent ease toward the tier's weight.
            float target = Pick(_glowWeightByTier, tier);
            _glowWeight = Mathf.Lerp(_glowWeight, target, 1f - Mathf.Exp(-0.6931472f / _glowHalfLife * dt));
            if (_glowVolume != null) _glowVolume.weight = _glowWeight * flash;
        }

        private void StartPulse(int tier)
        {
            _pulseTime = 0f;
            _pulsePeak = Pick(_pulseWeightByTier, tier - 1);
            float distortion = UiKit.ReduceMotion ? 0f : _pulseDistortion * Mathf.Clamp01(PixelCameraRig.GlobalShakeScale);
            if (_distortion != null) _distortion.intensity.Override(distortion);
        }

        private static float Pick(float[] values, int index)
        {
            if (values == null || values.Length == 0) return 0f;
            return values[Mathf.Clamp(index, 0, values.Length - 1)];
        }

        private static float Sq(float x) => x * x;
    }
}
