using UnityEngine;
using UnityEngine.UI;

namespace DanceWithTheDevil
{
    /// <summary>
    /// Combo readout on its own canvas, right side under the score (design: production/design/combo-haste.md §2):
    /// "KOMBO" caption, the count in the title font (grows a little per tier), the multiplier ("×1,5") and a draining
    /// 3 s bar that turns dim while the window is paused (empty arena, frozen).
    /// Feel (unscaled time, timing sheet in WORKLOG): every count punches the number (spring f 6 Hz, ζ 0.45);
    /// a tier-up pops it bigger (spring f 4 Hz, ζ 0.35), shifts its colour and wobbles it (±8°, 9 Hz, 0.4 s);
    /// a damage break jitters it red and drops it fast (0.35 s), a timeout greys it and lets it sink slowly (0.6 s).
    /// Hidden at 0 and whenever the Devil flow is not Playing. <see cref="UiKit.ReduceMotion"/> keeps colours and
    /// text but skips scale, wobble and drop. Display only: it never changes the combo.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ComboHud : MonoBehaviour
    {
        private const float BarWidth = 64f;
        private const float HitPunch = 0.18f;          // scale impulse per count (as an offset target kick)
        private const float TierPop = 0.6f;
        private const float MaxPunch = 0.7f;           // largest on-screen growth over the tier size
        private const float WobbleDegrees = 8f;
        private const float WobbleHz = 9f;
        private const float WobbleSeconds = 0.4f;
        private const float DamageBreakSeconds = 0.35f;
        private const float TimeoutBreakSeconds = 0.6f;

        [Tooltip("Combo source.")]
        [SerializeField] private ComboTracker _combo;
        [Tooltip("The readout hides while its State is not Playing. Empty = no check.")]
        [SerializeField] private DevilDirector _devil;
        [Tooltip("Sorting order of the combo canvas (the HUD uses 10).")]
        [SerializeField] private int _sortingOrder = 11;
        [Tooltip("Number colour per tier 0..4 (placeholders from the UI palette; art direction to confirm).")]
        [SerializeField] private Color[] _tierColors =
        {
            new Color(0.93f, 0.88f, 0.74f, 1f), // Cream
            new Color(0.93f, 0.88f, 0.74f, 1f),
            new Color(1f, 0.82f, 0.3f, 1f),     // Gold
            new Color(1f, 0.6f, 0.3f, 1f),
            new Color(1f, 0.45f, 0.45f, 1f),    // CostRed
        };
        [Tooltip("Extra number scale per tier (tier 4 = 1 + 4 × this).")]
        [SerializeField, Range(0f, 0.2f)] private float _scalePerTier = 0.08f;

        private RectTransform _frame;
        private RectTransform _group;
        private Text _caption;
        private Text _count;
        private RectTransform _countRect;
        private Text _multiplier;
        private Image _barBack;
        private RectTransform _barFill;
        private Image _barFillImage;

        private int _shownCount = -1;
        private int _shownTenths = -1;
        private int _tier;
        private Color _countColor;

        // Punch spring (scale offset), integrated on unscaled time.
        private float _punch;
        private float _punchVelocity;
        private float _punchFrequency = 6f;
        private float _punchDamping = 0.45f;
        private float _wobbleTime = float.PositiveInfinity;
        private float _colorFlash;                 // 1 → 0 white-hot flash on tier-up

        private bool _breaking;
        private ComboBreak _breakReason;
        private float _breakTime;

        private void Awake()
        {
            Build();
            _countColor = TierColor(0);
            _group.gameObject.SetActive(false);
        }

        private void OnEnable()
        {
            if (_combo == null) return;
            _combo.Changed += OnChanged;
            _combo.TierUp += OnTierUp;
            _combo.Broken += OnBroken;
        }

        private void OnDisable()
        {
            if (_combo == null) return;
            _combo.Changed -= OnChanged;
            _combo.TierUp -= OnTierUp;
            _combo.Broken -= OnBroken;
        }

        /// <summary>Code-built setups.</summary>
        public void Configure(ComboTracker combo, DevilDirector devil)
        {
            OnDisable();
            _combo = combo;
            _devil = devil;
            if (isActiveAndEnabled) OnEnable();
        }

        // ------------------------------------------------------------ events

        private void OnChanged(int count)
        {
            if (count <= 0) return; // the break handler owns the drop to 0
            _breaking = false;
            _group.anchoredPosition = Vector2.zero;
            _group.localRotation = Quaternion.identity;
            SetAlpha(1f);
            _caption.text = ComboTexts.Title;
            if (!UiKit.ReduceMotion)
            {
                // Velocity kick: overshoot and settle shaped by the spring (fast in, slow out).
                _punchVelocity += HitPunch * 2f * Mathf.PI * _punchFrequency;
                _punchFrequency = Mathf.Max(_punchFrequency, 6f);
            }
        }

        private void OnTierUp(int tier)
        {
            _tier = tier;
            _colorFlash = 1f;
            if (UiKit.ReduceMotion) return;
            _punchFrequency = 4f;
            _punchDamping = 0.35f;
            _punchVelocity += TierPop * 2f * Mathf.PI * _punchFrequency;
            _wobbleTime = 0f;
        }

        private void OnBroken(ComboBreak reason, int lost)
        {
            _breaking = true;
            _breakReason = reason;
            _breakTime = 0f;
            _tier = 0;
            _wobbleTime = float.PositiveInfinity;
            _caption.text = reason == ComboBreak.Damage ? ComboTexts.BrokenByDamage : ComboTexts.BrokenByTimeout;
            _count.text = lost.ToString();
            _shownCount = -1;
        }

        // ------------------------------------------------------------ per frame

        private void LateUpdate()
        {
            if (_combo == null) return;
            bool playing = _devil == null || _devil.State == DevilState.Playing;
            bool visible = playing && (_combo.Count > 0 || _breaking);
            if (_group.gameObject.activeSelf != visible) _group.gameObject.SetActive(visible);
            if (!visible)
            {
                _breaking = false;
                return;
            }

            float dt = Mathf.Min(Time.unscaledDeltaTime, 0.05f);
            if (_breaking) UpdateBreak(dt);
            else UpdateLive(dt);
        }

        private void UpdateLive(float dt)
        {
            int count = _combo.Count;
            if (count != _shownCount)
            {
                _shownCount = count;
                _count.text = count.ToString();
            }
            int tenths = _combo.MultiplierTenths;
            if (tenths != _shownTenths)
            {
                _shownTenths = tenths;
                _multiplier.text = string.Format(ComboTexts.MultiplierFormat, tenths / 10, tenths % 10);
            }
            _tier = _combo.Tier;

            // Colour: tier colour, white-hot on the tier-up frame easing back over ~6 frames.
            _colorFlash = Mathf.Max(0f, _colorFlash - dt * 10f);
            Color target = TierColor(_tier);
            _countColor = Color.Lerp(target, Color.white, _colorFlash);
            _count.color = _countColor;
            _multiplier.color = Color.Lerp(UiKit.Gold, Color.white, _colorFlash);

            // Bar: shrinks toward its right end; dim and steady while paused.
            _barFill.sizeDelta = new Vector2(BarWidth * _combo.TimeLeft01, _barFill.sizeDelta.y);
            _barFillImage.color = _combo.TimerPaused ? UiKit.Dim : target;

            float baseScale = 1f + _scalePerTier * _tier;
            if (UiKit.ReduceMotion)
            {
                _punch = 0f;
                _punchVelocity = 0f;
                _countRect.localScale = Vector3.one * baseScale;
                _countRect.localRotation = Quaternion.identity;
                return;
            }

            StepSpring(dt);
            // Capped: at 10 hits/s the kicks pile up, and an uncapped number grew off the screen edge over the score.
            float s = baseScale * (1f + Mathf.Clamp(_punch, -0.3f, MaxPunch));
            _countRect.localScale = new Vector3(s, s, 1f);

            if (_wobbleTime < WobbleSeconds)
            {
                _wobbleTime += dt;
                float fade = 1f - Mathf.Clamp01(_wobbleTime / WobbleSeconds);
                float angle = WobbleDegrees * fade * fade * Mathf.Sin(_wobbleTime * WobbleHz * 2f * Mathf.PI);
                _countRect.localRotation = Quaternion.Euler(0f, 0f, angle);
            }
            else
            {
                _countRect.localRotation = Quaternion.identity;
                _punchFrequency = 6f;
                _punchDamping = 0.45f;
            }
        }

        private void UpdateBreak(float dt)
        {
            bool damage = _breakReason == ComboBreak.Damage;
            float duration = damage ? DamageBreakSeconds : TimeoutBreakSeconds;
            _breakTime += dt;
            float t = Mathf.Clamp01(_breakTime / duration);
            float easeIn = t * t;

            Color color = damage ? Color.Lerp(Color.white, UiKit.HealthRed, Mathf.Clamp01(t * 6f)) : UiKit.Dim;
            _count.color = color;
            _barFill.sizeDelta = new Vector2(0f, _barFill.sizeDelta.y);
            SetAlpha(1f - easeIn);

            if (!UiKit.ReduceMotion)
            {
                float drop = (damage ? 18f : 10f) * easeIn;
                float jitter = damage ? Mathf.Sin(_breakTime * 60f) * 3f * (1f - t) : 0f;
                _group.anchoredPosition = new Vector2(Mathf.Round(jitter), -Mathf.Round(drop));
                float tilt = damage ? -12f * easeIn : 0f;
                _countRect.localRotation = Quaternion.Euler(0f, 0f, tilt);
                float s = damage ? 1f + 0.15f * (1f - t) : 1f - 0.15f * t;
                _countRect.localScale = new Vector3(s, s, 1f);
            }

            if (t >= 1f)
            {
                _breaking = false;
                _group.anchoredPosition = Vector2.zero;
                _countRect.localRotation = Quaternion.identity;
                _countRect.localScale = Vector3.one;
                SetAlpha(1f);
                _caption.text = ComboTexts.Title;
                _group.gameObject.SetActive(false);
            }
        }

        // Semi-implicit spring toward 0 (scale offset), stable for dt ≤ 0.05.
        private void StepSpring(float dt)
        {
            float w = 2f * Mathf.PI * _punchFrequency;
            _punchVelocity += (-w * w * _punch - 2f * _punchDamping * w * _punchVelocity) * dt;
            _punch += _punchVelocity * dt;
            if (Mathf.Abs(_punch) < 1e-4f && Mathf.Abs(_punchVelocity) < 1e-3f)
            {
                _punch = 0f;
                _punchVelocity = 0f;
            }
        }

        private Color TierColor(int tier)
        {
            if (_tierColors == null || _tierColors.Length == 0) return UiKit.Cream;
            return _tierColors[Mathf.Clamp(tier, 0, _tierColors.Length - 1)];
        }

        private void SetAlpha(float a)
        {
            SetTextAlpha(_caption, a);
            SetTextAlpha(_count, a);
            SetTextAlpha(_multiplier, a);
            Color back = _barBack.color;
            back.a = 0.8f * a;
            _barBack.color = back;
            Color fill = _barFillImage.color;
            fill.a = a;
            _barFillImage.color = fill;
        }

        private static void SetTextAlpha(Text text, float a)
        {
            Color c = text.color;
            c.a = a;
            text.color = c;
        }

        // ------------------------------------------------------------ build

        private void Build()
        {
            Font body = UiKit.ResolveFont(null);
            Font title = UiKit.TitleFont;
            Vector2 tr = new Vector2(1f, 1f);
            Vector2 right = new Vector2(1f, 0.5f);

            _frame = UiKit.CreateCanvas(transform, "ComboCanvas", _sortingOrder, false);
            // Under the score (score: top-right at (-8,-8), 160×16). 80 wide keeps clear of the 460-wide offer banner.
            RectTransform anchor = UiKit.Rect(_frame, "Combo", tr, tr, new Vector2(-8f, -28f), new Vector2(80f, 58f));
            _group = UiKit.Rect(anchor, "Group", tr, tr, Vector2.zero, new Vector2(80f, 58f));

            _caption = UiKit.Label(_group, "Caption", body, 10, UiKit.Light, TextAnchor.MiddleRight, tr, tr,
                Vector2.zero, new Vector2(80f, 12f), FontStyle.Bold);
            _caption.text = ComboTexts.Title;
            UiKit.AddShadow(_caption);

            _count = UiKit.Label(_group, "Count", title, 24, UiKit.Cream, TextAnchor.MiddleRight, tr, right,
                new Vector2(0f, -26f), new Vector2(80f, 28f));
            _count.horizontalOverflow = HorizontalWrapMode.Overflow;
            _countRect = _count.rectTransform; // pivot on the right edge: punches grow leftward, away from the screen edge
            UiKit.AddShadow(_count);

            _multiplier = UiKit.Label(_group, "Multiplier", body, 12, UiKit.Gold, TextAnchor.MiddleRight, tr, tr,
                new Vector2(0f, -40f), new Vector2(80f, 14f), FontStyle.Bold);
            UiKit.AddShadow(_multiplier);

            _barBack = UiKit.Box(_group, "Bar", new Color(UiKit.Ink.r, UiKit.Ink.g, UiKit.Ink.b, 0.8f), tr, tr,
                new Vector2(0f, -55f), new Vector2(BarWidth + 2f, 4f));
            _barFillImage = UiKit.Box(_barBack.rectTransform, "Fill", UiKit.Cream, right, right,
                new Vector2(-1f, 0f), new Vector2(BarWidth, 2f));
            _barFill = _barFillImage.rectTransform;
        }
    }
}
