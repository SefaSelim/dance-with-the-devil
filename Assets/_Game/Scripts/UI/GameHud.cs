using System;
using DG.Tweening;
using DG.Tweening.Core;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace DanceWithTheDevil
{
    /// <summary>
    /// In-run HUD (MAIN_GDD §12), fitted to the game image (<see cref="UiKit.CreateCanvas"/>).
    /// Top: HP bar drawn for the base 100 HP, with the part lost to contracts shown as a dark "lost" segment; the
    /// elapsed run time (the run has no end time: D2, K-4); score with the live combo multiplier ("Puan 1200 ×1,5",
    /// shown above ×1,0; the combo itself pops up in the world, see WorldPopups). Top-left: Defne's small block portrait (hair
    /// greys after contract 1, hidden by contract 3) and the "Verdiklerin" strip, one icon per cost taken (the
    /// newest one pulses). Bottom: one fixed slot per module type (ModuleCatalog.Count; the row narrows from six on) (empty = a dim socket with no name until that module has
    /// been offered once; equipped = the module's own colour; while Q is held the highlighted slot grows, turns gold
    /// and "Sağ tık: sök" shows above it), "Küçük hayırlar: N", rocket / machete / dodge / reject pips (reject reads
    /// PlayerModules' real cooldown). While a whisper offer exists (and the Devil is not up): a small banner in the
    /// top-right corner under the score (slides in): its name in the module's colour with the whisper on one line,
    /// small "+" advantage and "−" cost lines, a shrinking lifetime bar and "TAKILI" left of it on a duplicate.
    /// Aim reticle: a chunky pixel crosshair at the mouse during play; the OS cursor is hidden and confined to the
    /// window during play and released on the Devil, ending and pause screens. Adds a <see cref="PauseOverlay"/>
    /// (Esc) when the GameObject has none. The whole HUD hides while the ending screen is visible.
    /// Display only; texts are rewritten only when their value changes (no per-frame allocations).
    /// Missing references are resolved from the player's GameObject; anything still missing is just not shown.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class GameHud : MonoBehaviour
    {
        private const int MaxPips = 4;
        private const int ModuleSlots = ModuleCatalog.Count;
        private const int MaxCostIcons = 6;
        private const int TimeCacheSeconds = 3600;
        // The slot row fits ±170 at 640×360 whatever the module count: the pip rows (label + 4 pips) end at −180 and
        // "Küçük hayırlar" (140 wide) starts right of the last highlighted slot and must end before +320.
        // Five slots: spacing 68, centres 0/±68/±136, extents ±167 (±171 highlighted), "Küçük hayırlar" +175..+315.
        // Six slots: spacing 56, centres ±28/±84/±140, extents ±165 (±169 highlighted), "Küçük hayırlar" +173..+313.
        private const float SlotSpacing = ModuleSlots <= 5 ? 68f : 336f / ModuleSlots;
        private static readonly Vector2 SlotSize = new Vector2(SlotSpacing - 6f, 20f);
        private static readonly Vector2 SlotSizeHighlighted = new Vector2(SlotSpacing + 2f, 26f);
        // Offer banner: top-right corner under the score line.
        private static readonly Vector2 OfferSize = new Vector2(232f, 40f);
        private static readonly Vector2 OfferHome = new Vector2(-6f, -27f);
        private const float OfferSlide = 40f;
        private const float OfferTimerWidth = 228f;

        private static readonly Color SlotEmptyFill = new Color(0.07f, 0.06f, 0.09f, 0.85f);
        private static readonly Color SlotEmptyBorder = new Color(0.3f, 0.28f, 0.32f, 1f);
        private static readonly Color LostHealth = new Color(0.2f, 0.05f, 0.08f, 1f);
        private static readonly Color CostIconBorder = new Color(0.6f, 0.14f, 0.17f, 1f);
        private static readonly Color IconGrey = new Color(0.55f, 0.55f, 0.57f, 1f);

        [Header("References")]
        [Tooltip("Player health. Other player components are taken from its GameObject when empty.")]
        [SerializeField] private Health _playerHealth;
        [SerializeField] private PlayerController _controller;
        [SerializeField] private RocketLauncher _rocket;
        [SerializeField] private PlayerInputReader _input;
        [Tooltip("Whisper modules (optional).")]
        [SerializeField] private PlayerModules _modules;
        [Tooltip("Machete, for the 'Pala [F]' pip. Empty = taken from the player.")]
        [SerializeField] private Machete _machete;
        [SerializeField] private WaveDirector _waves;
        [SerializeField] private ScoreKeeper _score;
        [Tooltip("Combo, for the multiplier on the score line. Empty = found in the scene.")]
        [SerializeField] private ComboTracker _combo;
        [Tooltip("The offer banner hides while its State is not Playing. Empty = searched in parents; null = no check.")]
        [SerializeField] private DevilDirector _devil;
        [Tooltip("The whole HUD hides while it is visible. Empty = taken from this GameObject.")]
        [SerializeField] private EndingScreen _ending;

        [Header("Look")]
        [Tooltip("Portrait art. Empty = placeholder face built from blocks.")]
        [SerializeField] private Sprite _portraitSprite;
        [Tooltip("Font. Empty = built-in LegacyRuntime.")]
        [SerializeField] private Font _font;
        [Tooltip("Overlay canvas sorting order (display is -1000).")]
        [SerializeField] private int _sortingOrder = 10;
        [Tooltip("HP bar width per hit point, canvas units (100 HP = 200).")]
        [SerializeField, Min(0.1f)] private float _unitsPerHealth = 2f;
        [Tooltip("Reject cooldown for the pip when there is no PlayerModules (GDD §7: 0.65 s).")]
        [SerializeField, Min(0f)] private float _rejectCooldown = 0.65f;
        [Tooltip("Seconds a newly taken cost icon pulses in the 'Verdiklerin' strip.")]
        [SerializeField, Min(0f)] private float _costPulseDuration = 1.6f;

        [Header("Aim and cursor")]
        [Tooltip("Sorting order of the aim reticle canvas (above hints and intro, below the Devil screen).")]
        [SerializeField] private int _reticleSortingOrder = 90;
        [Tooltip("Hide the OS cursor and confine it to the window during play (released on Devil, ending and pause).")]
        [SerializeField] private bool _manageCursor = true;

        private RectTransform _root;
        private RectTransform _reticleRoot;
        private RectTransform _reticle;
        private GameObject _portrait;
        private DefnePortrait _portraitView;
        // Drawn portrait (UiTheme.PortraitSprite) and its cost layers: grey copy (contract 1 half, 6 full), face veil
        // (contract 4) and a darker tint (contract 5), as on the ending screen.
        private Image _portraitArt;
        private Image _portraitGrey;
        private Image _portraitVeil;
        private RectTransform _hpBack;
        private RectTransform _hpFill;
        private RectTransform _hpLost;
        private Text _hpText;
        private Text _timeText;
        private Text _scoreText;
        private GameObject _offer;
        private RectTransform _offerRect;
        private CanvasGroup _offerGroup;
        private Tween _offerTween;
        private DOGetter<Vector2> _offerPosGetter;
        private DOSetter<Vector2> _offerPosSetter;
        private Text _offerWhisper;
        private Text _offerPlus;
        private Text _offerMinus;
        private GameObject _offerEquipped;
        private RectTransform _offerTimer;
        private readonly RectTransform[] _slotRect = new RectTransform[ModuleSlots];
        private readonly Image[] _slotBorder = new Image[ModuleSlots];
        private readonly Image[] _slotFill = new Image[ModuleSlots];
        private readonly Text[] _slotLabel = new Text[ModuleSlots];
        private readonly int[] _slotShown = new int[ModuleSlots];
        private readonly bool[] _offered = new bool[ModuleSlots];
        private readonly string[] _moduleHex = new string[ModuleSlots];
        private readonly string[] _ejectStrings = new string[ModuleSlots];
        private Text _ejectLabel;
        private Text _smallNosText;
        private GameObject _costStrip;
        private readonly RectTransform[] _costIcons = new RectTransform[MaxCostIcons];
        private readonly Image[] _costIconBorder = new Image[MaxCostIcons];
        private readonly Image[] _costIconFill = new Image[MaxCostIcons];
        private Pips _rocketPips, _machetePips, _dodgePips, _rejectPips;
        private PauseOverlay _pause;

        private readonly string[] _timeStrings = new string[TimeCacheSeconds];
        private int _shownSeconds = -1;
        private int _shownScore = -1;
        private int _shownTenths = -1;
        private int _shownHp = -1;
        private int _shownMaxHp = -1;
        private int _shownSmallNos = -1;
        private int _shownTaken;
        private int _ejectShown = -2;
        private int _pulseIndex = -1;
        private float _pulseTime;
        private float _rejectReadyTime;
        private bool _offerOpen;
        private bool _subscribed;
        private bool _cursorCaptured;
        private bool _reticleShown;
        private Action<ModuleType> _onOfferAppeared;
        private Action<ModuleType> _onOfferClosed;

        private struct Pips
        {
            public GameObject[] Frames;
            public Image[] Fills;
            public int ShownMax, ShownFull;
        }

        private void Awake()
        {
            _onOfferAppeared = OnOfferAppeared;
            _onOfferClosed = OnOfferClosed;
            ResolveReferences();
            for (int i = 0; i < ModuleSlots; i++)
            {
                var type = (ModuleType)i;
                _moduleHex[i] = ColorUtility.ToHtmlStringRGB(ModuleCatalog.ColorOf(type));
                _ejectStrings[i] = GameTexts.ModuleName(type) + GameTexts.Separator + GameTexts.HudEjectHint;
                _slotShown[i] = -1;
            }
            Build();
            UpdateSlots();

            _pause = GetComponent<PauseOverlay>();
            if (_pause == null) _pause = gameObject.AddComponent<PauseOverlay>();
            _pause.Configure(_devil, _ending, _input);
        }

        private void OnEnable() => Subscribe(true);

        private void OnDisable()
        {
            Subscribe(false);
            ReleaseCursor();
        }

        private void OnDestroy() => ReleaseCursor();

        /// <summary>Code-built setups: sets the player and resolves its components.</summary>
        public void Configure(Health playerHealth, WaveDirector waves, ScoreKeeper score)
        {
            Subscribe(false);
            _playerHealth = playerHealth;
            _waves = waves;
            _score = score;
            _controller = null;
            _rocket = null;
            _input = null;
            _modules = null;
            _machete = null;
            ResolveReferences();
            if (_pause != null) _pause.Configure(_devil, _ending, _input);
            if (isActiveAndEnabled) Subscribe(true);
        }

        /// <summary>Contract 3 "Arkadaşların": the portrait disappears.</summary>
        public void SetPortraitVisible(bool visible)
        {
            if (_portrait != null) _portrait.SetActive(visible);
        }

        public bool PortraitVisible => _portrait != null && _portrait.activeSelf;

        private void ResolveReferences()
        {
            if (_devil == null) _devil = GetComponentInParent<DevilDirector>();
            if (_ending == null) _ending = GetComponent<EndingScreen>();
            if (_combo == null) _combo = FindAnyObjectByType<ComboTracker>();
            if (_playerHealth == null) return;
            GameObject p = _playerHealth.gameObject;
            if (_controller == null) _controller = p.GetComponent<PlayerController>();
            if (_rocket == null) _rocket = p.GetComponentInChildren<RocketLauncher>(true);
            if (_input == null) _input = p.GetComponentInChildren<PlayerInputReader>(true);
            if (_modules == null) _modules = p.GetComponentInChildren<PlayerModules>(true);
            if (_machete == null) _machete = p.GetComponentInChildren<Machete>(true);
        }

        private void Subscribe(bool on)
        {
            if (_modules == null || on == _subscribed) return;
            _subscribed = on;
            if (on)
            {
                _modules.OfferAppeared += _onOfferAppeared;
                _modules.Accepted += _onOfferClosed;
                _modules.Rejected += _onOfferClosed;
                _modules.OfferExpired += _onOfferClosed;
            }
            else
            {
                _modules.OfferAppeared -= _onOfferAppeared;
                _modules.Accepted -= _onOfferClosed;
                _modules.Rejected -= _onOfferClosed;
                _modules.OfferExpired -= _onOfferClosed;
            }
        }

        private void OnOfferAppeared(ModuleType type)
        {
            int i = (int)type;
            _offered[i] = true;
            _offerWhisper.text = "<color=#" + _moduleHex[i] + "><b>" + GameTexts.ModuleName(type) + "</b></color>  "
                + GameTexts.ModuleWhisper(type);
            _offerPlus.text = "+ " + GameTexts.ModuleAdvantage(type);
            _offerMinus.text = "− " + GameTexts.ModuleCost(type);
            _offerEquipped.SetActive(false);
            _offerOpen = true;
            UpdateVisibility();
        }

        private void OnOfferClosed(ModuleType type)
        {
            _offerOpen = false;
            UpdateVisibility();
        }

        private void Update()
        {
            bool hudVisible = UpdateVisibility();
            bool playing = _devil == null || _devil.State == DevilState.Playing;
            UpdateHealth();
            UpdateTime();
            UpdateScore();
            UpdateSlots();
            UpdatePips();
            UpdateOffer();
            UpdateCosts();
            UpdateSmallNos();
            UpdateAim(hudVisible && playing && !RunPause.IsPaused);
        }

        /// <summary>
        /// The whole HUD hides behind the ending screen; the offer banner hides while the Devil is up so it never
        /// shows behind his line. Returns whether the HUD is visible.
        /// </summary>
        private bool UpdateVisibility()
        {
            bool hudVisible = _ending == null || !_ending.IsVisible;
            if (_root.gameObject.activeSelf != hudVisible) _root.gameObject.SetActive(hudVisible);
            bool playing = _devil == null || _devil.State == DevilState.Playing;
            bool offerVisible = _offerOpen && playing;
            if (_offer.activeSelf != offerVisible)
            {
                _offer.SetActive(offerVisible);
                if (offerVisible) SlideOfferIn();
            }
            return hudVisible;
        }

        private void UpdateHealth()
        {
            if (_playerHealth == null) return;
            int max = Mathf.RoundToInt(_playerHealth.MaxHealth);
            int hp = Mathf.CeilToInt(_playerHealth.Current);
            if (max == _shownMaxHp && hp == _shownHp) return;
            if (max != _shownMaxHp)
            {
                // The bar keeps its base length; what contracts took stays visible as a dark "lost" segment.
                float baseMax = Mathf.Max(DealEffects.BaseMaxHealth, max);
                _hpBack.sizeDelta = new Vector2(baseMax * _unitsPerHealth + 4f, _hpBack.sizeDelta.y);
                float lost = Mathf.Max(0f, baseMax - max);
                _hpLost.anchoredPosition = new Vector2(2f + max * _unitsPerHealth, 0f);
                _hpLost.sizeDelta = new Vector2(lost * _unitsPerHealth, _hpLost.sizeDelta.y);
                _hpLost.gameObject.SetActive(lost > 0f);
            }
            _shownMaxHp = max;
            _shownHp = hp;
            _hpFill.sizeDelta = new Vector2(Mathf.Max(0f, hp) * _unitsPerHealth, _hpFill.sizeDelta.y);
            _hpText.text = hp + "/" + max;
        }

        /// <summary>Elapsed run time, m:ss (D2: the run has no end time; the waves never stop).</summary>
        private void UpdateTime()
        {
            float elapsed = _waves != null ? _waves.Elapsed : Time.timeSinceLevelLoad;
            int seconds = Mathf.Clamp(Mathf.FloorToInt(elapsed), 0, TimeCacheSeconds - 1);
            if (seconds == _shownSeconds) return;
            _shownSeconds = seconds;
            if (_timeStrings[seconds] == null) _timeStrings[seconds] = (seconds / 60) + ":" + (seconds % 60).ToString("00");
            _timeText.text = _timeStrings[seconds];
        }

        private void UpdateScore()
        {
            int score = _score != null ? _score.Score : 0;
            int tenths = _combo != null ? _combo.MultiplierTenths : 10;
            if (score == _shownScore && tenths == _shownTenths) return;
            _shownScore = score;
            _shownTenths = tenths;
            string line = string.Format(GameTexts.HudScoreFormat, score);
            _scoreText.text = tenths > 10
                ? string.Format(ComboTexts.ScoreWithMultiplierFormat, line, tenths / 10, tenths % 10)
                : line;
        }

        private void UpdateSmallNos()
        {
            int count = _modules != null ? _modules.SmallNoCount : 0;
            if (count == _shownSmallNos) return;
            _shownSmallNos = count;
            _smallNosText.text = string.Format(GameTexts.HudSmallNosFormat, count);
        }

        // ------------------------------------------------------------ module slots

        private void UpdateSlots()
        {
            int highlighted = -1;
            bool selecting = _modules != null && _modules.SelectionOpen;
            if (selecting)
            {
                ModuleType? h = _modules.Highlighted;
                if (h.HasValue) highlighted = (int)h.Value;
            }

            for (int i = 0; i < ModuleSlots; i++)
            {
                bool equipped = _modules != null && _modules.IsEquipped((ModuleType)i);
                int state = equipped ? 2 : _offered[i] ? 1 : 0;
                bool lit = equipped && highlighted == i;
                int code = state * 2 + (lit ? 1 : 0);
                if (code != _slotShown[i]) ApplySlot(i, state, lit, code);
            }

            int eject = selecting ? highlighted : -2;
            if (eject == _ejectShown) return;
            _ejectShown = eject;
            bool show = eject >= 0;
            if (_ejectLabel.gameObject.activeSelf != show) _ejectLabel.gameObject.SetActive(show);
            if (!show) return;
            _ejectLabel.text = _ejectStrings[eject];
            _ejectLabel.rectTransform.anchoredPosition = new Vector2(SlotX(eject), 32f);
        }

        /// <summary>Centre x of slot <paramref name="i"/>, slots centred on the bottom middle.</summary>
        private static float SlotX(int i) => (i - (ModuleSlots - 1) * 0.5f) * SlotSpacing;

        /// <summary>0 = empty, never offered (no name); 1 = empty, offered before; 2 = equipped (module colour).</summary>
        private void ApplySlot(int i, int state, bool highlighted, int code)
        {
            _slotShown[i] = code;
            var type = (ModuleType)i;
            Color border, fill, labelColor;
            string label;
            FontStyle style = FontStyle.Normal;
            switch (state)
            {
                case 2:
                    border = highlighted ? UiKit.Gold : UiKit.Cream;
                    fill = ModuleCatalog.ColorOf(type);
                    label = GameTexts.ModuleName(type);
                    labelColor = UiKit.Ink;
                    style = FontStyle.Bold;
                    break;
                case 1:
                    border = SlotEmptyBorder;
                    fill = SlotEmptyFill;
                    label = GameTexts.ModuleName(type);
                    labelColor = UiKit.Dim;
                    break;
                default:
                    border = SlotEmptyBorder;
                    fill = SlotEmptyFill;
                    label = GameTexts.HudEmptySlot;
                    labelColor = SlotEmptyBorder;
                    break;
            }
            _slotBorder[i].color = border;
            _slotFill[i].color = fill;
            _slotLabel[i].text = label;
            _slotLabel[i].color = labelColor;
            _slotLabel[i].fontStyle = style;
            Vector2 size = highlighted ? SlotSizeHighlighted : SlotSize;
            float thickness = highlighted ? 2f : 1f;
            _slotRect[i].sizeDelta = size;
            _slotFill[i].rectTransform.sizeDelta = size - new Vector2(thickness * 2f, thickness * 2f);
        }

        // ------------------------------------------------------------ pips

        private void UpdatePips()
        {
            if (_rocket != null) SetPips(ref _rocketPips, _rocket.MaxCharges, _rocket.Charges);
            if (_machete != null) SetPips(ref _machetePips, 1, _machete.IsReady ? 1 : 0);
            if (_controller != null) SetPips(ref _dodgePips, _controller.MaxDodgeCharges, _controller.DodgeCharges);

            if (_modules != null)
            {
                SetPips(ref _rejectPips, 1, _modules.RejectReady ? 1 : 0);
                return;
            }
            // No module system: approximate with the GDD cooldown.
            float now = Time.time;
            bool ready = now >= _rejectReadyTime;
            if (ready && _input != null && _input.RejectPressed) _rejectReadyTime = now + _rejectCooldown;
            SetPips(ref _rejectPips, 1, now >= _rejectReadyTime ? 1 : 0);
        }

        private static void SetPips(ref Pips pips, int max, int full)
        {
            max = Mathf.Clamp(max, 0, MaxPips);
            full = Mathf.Clamp(full, 0, max);
            if (max == pips.ShownMax && full == pips.ShownFull) return;
            pips.ShownMax = max;
            pips.ShownFull = full;
            for (int i = 0; i < MaxPips; i++)
            {
                pips.Frames[i].SetActive(i < max);
                pips.Fills[i].color = i < full ? UiKit.Gold : UiKit.Ink; // empty = hollow cream frame
            }
        }

        // ------------------------------------------------------------ offer banner

        /// <summary>The banner slides in from the right and fades in (DOTween, unscaled); ReduceMotion = in place.</summary>
        private void SlideOfferIn()
        {
            _offerTween?.Kill();
            _offerTween = null;
            if (UiKit.ReduceMotion)
            {
                _offerRect.anchoredPosition = OfferHome;
                _offerGroup.alpha = 1f;
                return;
            }
            _offerRect.anchoredPosition = OfferHome + new Vector2(OfferSlide, 0f);
            _offerGroup.alpha = 0f;
            Sequence s = DOTween.Sequence();
            s.Insert(0f, DOTween.To(_offerPosGetter, _offerPosSetter, OfferHome, 0.28f).SetEase(Ease.OutCubic));
            s.Insert(0f, DOTween.To(() => _offerGroup.alpha, a => _offerGroup.alpha = a, 1f, 0.2f));
            s.SetUpdate(true).SetLink(_offer);
            _offerTween = s;
        }

        private void UpdateOffer()
        {
            if (!_offerOpen || _modules == null) return;
            ModuleOffer offer = _modules.Current;
            if (offer == null) return;
            bool duplicate = offer.IsEquippedDuplicate;
            if (_offerEquipped.activeSelf != duplicate) _offerEquipped.SetActive(duplicate);
            float t = offer.Lifetime > 0f ? Mathf.Clamp01(offer.TimeLeft / offer.Lifetime) : 0f;
            float w = Mathf.Round(t * OfferTimerWidth);
            if (!Mathf.Approximately(_offerTimer.sizeDelta.x, w)) _offerTimer.sizeDelta = new Vector2(w, _offerTimer.sizeDelta.y);
        }

        // ------------------------------------------------------------ costs ("Verdiklerin")

        private void UpdateCosts()
        {
            int taken = _devil != null ? _devil.DealsTaken : 0;
            if (taken != _shownTaken)
            {
                if (taken < _shownTaken)
                {
                    for (int i = 0; i < MaxCostIcons; i++) _costIcons[i].gameObject.SetActive(false);
                    _shownTaken = 0;
                }
                int end = Mathf.Min(taken, MaxCostIcons);
                for (int i = _shownTaken; i < end; i++)
                {
                    DealDefinition deal = i < _devil.TakenDeals.Count ? _devil.TakenDeals[i] : null;
                    ShowCostIcon(i, deal != null ? deal.Number : i + 1);
                    _pulseIndex = i;
                    _pulseTime = 0f;
                }
                _shownTaken = taken;
                if (_costStrip.activeSelf != taken > 0) _costStrip.SetActive(taken > 0);
                if (_portraitView != null) _portraitView.ApplyCosts(taken);
                ApplyDrawnPortraitCosts(taken);
            }

            if (_pulseIndex < 0) return;
            _pulseTime += Time.unscaledDeltaTime;
            float k = _costPulseDuration > 0f ? Mathf.Clamp01(_pulseTime / _costPulseDuration) : 1f;
            float s = UiKit.ReduceMotion ? 1f : 1f + 1.5f * (1f - k) * (1f - k);
            _costIcons[_pulseIndex].localScale = new Vector3(s, s, 1f);
            bool flash = !UiKit.ReduceMotion && k < 1f && Mathf.Repeat(_pulseTime, 0.24f) < 0.12f;
            _costIconBorder[_pulseIndex].color = flash ? Color.white : CostIconBorder;
            if (k >= 1f)
            {
                _costIcons[_pulseIndex].localScale = Vector3.one;
                _costIconBorder[_pulseIndex].color = CostIconBorder;
                _pulseIndex = -1;
            }
        }

        /// <summary>Builds the block icon of contract <paramref name="number"/>'s cost into slot <paramref name="index"/>.</summary>
        private void ShowCostIcon(int index, int number)
        {
            RectTransform icon = _costIcons[index];
            icon.gameObject.SetActive(true);
            Transform f = _costIconFill[index].transform;
            for (int c = f.childCount - 1; c >= 0; c--) Destroy(f.GetChild(c).gameObject);

            Vector2 m = new Vector2(0.5f, 0.5f);
            Color cream = UiKit.Cream;
            switch (number)
            {
                case 1: // Rengi: grey hair over a face
                    UiKit.Box(f, "Hair", IconGrey, m, m, new Vector2(0f, 3f), new Vector2(10f, 5f));
                    UiKit.Box(f, "Face", new Color(0.95f, 0.78f, 0.62f, 1f), m, m, new Vector2(0f, -2f), new Vector2(6f, 6f));
                    break;
                case 2: // Sesi: a speaker, struck through
                    UiKit.Box(f, "Box", cream, m, m, new Vector2(-4f, 0f), new Vector2(3f, 4f));
                    UiKit.Box(f, "Cone", cream, m, m, new Vector2(-1f, 0f), new Vector2(3f, 8f));
                    UiKit.Box(f, "Slash", UiKit.HealthRed, m, m, new Vector2(1f, 0f), new Vector2(2f, 14f))
                        .rectTransform.localRotation = Quaternion.Euler(0f, 0f, 45f);
                    break;
                case 3: // Arkadaşları: two faded figures
                    UiKit.Box(f, "Head_L", IconGrey, m, m, new Vector2(-3f, 2f), new Vector2(3f, 3f));
                    UiKit.Box(f, "Body_L", IconGrey, m, m, new Vector2(-3f, -3f), new Vector2(4f, 5f));
                    UiKit.Box(f, "Head_R", IconGrey, m, m, new Vector2(3f, 2f), new Vector2(3f, 3f));
                    UiKit.Box(f, "Body_R", IconGrey, m, m, new Vector2(3f, -3f), new Vector2(4f, 5f));
                    break;
                case 4: // Yüzü: the glasses that fell off
                    UiKit.Box(f, "Lens_L", cream, m, m, new Vector2(-3f, 0f), new Vector2(4f, 3f));
                    UiKit.Box(f, "Lens_R", cream, m, m, new Vector2(3f, 0f), new Vector2(4f, 3f));
                    UiKit.Box(f, "Bridge", cream, m, m, Vector2.zero, new Vector2(2f, 1f));
                    break;
                case 5: // Zamanı: an hourglass
                    UiKit.Box(f, "Top", cream, m, m, new Vector2(0f, 5f), new Vector2(8f, 2f));
                    UiKit.Box(f, "Upper", cream, m, m, new Vector2(0f, 3f), new Vector2(6f, 2f));
                    UiKit.Box(f, "Neck", cream, m, m, Vector2.zero, new Vector2(2f, 4f));
                    UiKit.Box(f, "Lower", cream, m, m, new Vector2(0f, -3f), new Vector2(6f, 2f));
                    UiKit.Box(f, "Bottom", cream, m, m, new Vector2(0f, -5f), new Vector2(8f, 2f));
                    break;
                default: // Kendisi: a grey figure
                    UiKit.Box(f, "Head", IconGrey, m, m, new Vector2(0f, 3f), new Vector2(4f, 4f));
                    UiKit.Box(f, "Body", IconGrey, m, m, new Vector2(0f, -3f), new Vector2(6f, 6f));
                    break;
            }
        }

        // ------------------------------------------------------------ aim reticle and cursor

        private void UpdateAim(bool gameplay)
        {
            if (_manageCursor && gameplay != _cursorCaptured)
            {
                _cursorCaptured = gameplay;
                Cursor.visible = !gameplay;
                Cursor.lockState = gameplay ? CursorLockMode.Confined : CursorLockMode.None;
            }

            Mouse mouse = Mouse.current;
            bool show = gameplay && mouse != null;
            if (show != _reticleShown)
            {
                _reticleShown = show;
                _reticleRoot.gameObject.SetActive(show);
            }
            if (!show) return;
            Vector2 screen = mouse.position.ReadValue();
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(_reticleRoot, screen, null, out Vector2 local))
                _reticle.anchoredPosition = new Vector2(Mathf.Floor(local.x) + 0.5f, Mathf.Floor(local.y) + 0.5f);
        }

        private void ReleaseCursor()
        {
            if (!_cursorCaptured) return;
            _cursorCaptured = false;
            Cursor.visible = true;
            Cursor.lockState = CursorLockMode.None;
        }

        // ------------------------------------------------------------ build

        private void Build()
        {
            Font font = UiKit.ResolveFont(_font);
            _root = UiKit.CreateCanvas(transform, "HudCanvas", _sortingOrder, false);
            Vector2 tl = new Vector2(0f, 1f);
            Vector2 mid = new Vector2(0.5f, 0.5f);
            Vector2 top = new Vector2(0.5f, 1f);
            Vector2 tr = new Vector2(1f, 1f);
            Vector2 bottom = new Vector2(0.5f, 0f);
            Vector2 left = new Vector2(0f, 0.5f);

            // Portrait (top-left).
            Image portrait = UiKit.Framed(_root, "Portrait", UiKit.Cream, new Color(0.12f, 0.24f, 0.28f, 1f), tl, tl,
                new Vector2(6f, -6f), new Vector2(40f, 40f));
            _portrait = portrait.transform.parent.gameObject;
            UiTheme theme = UiKit.Theme;
            Sprite drawn = _portraitSprite != null ? _portraitSprite : theme != null ? theme.PortraitSprite : null;
            if (drawn != null)
            {
                _portraitArt = PortraitLayer(portrait.transform, "Art", drawn, Color.white);
                Sprite grey = theme != null ? theme.PortraitGreySprite : null;
                if (grey != null) _portraitGrey = PortraitLayer(portrait.transform, "Grey", grey, new Color(1f, 1f, 1f, 0f));
                // Contract 4 "Yüzü": a dark smudge over the eyes and glasses (face centre ~9% above the crop centre).
                Vector2 m = new Vector2(0.5f, 0.5f);
                Vector2 side = ((RectTransform)portrait.transform).sizeDelta;
                _portraitVeil = UiKit.Box(portrait.transform, "FaceVeil", new Color(0.03f, 0.02f, 0.04f, 0.92f), m, m,
                    new Vector2(0.04f * side.x, 0.1f * side.y), new Vector2(0.62f * side.x, 0.36f * side.y));
                if (theme != null && theme.GlowSprite != null) _portraitVeil.sprite = theme.GlowSprite;
                _portraitVeil.gameObject.SetActive(false);
            }
            else
            {
                _portraitView = new DefnePortrait(portrait.transform, 1f);
            }

            // HP bar: base length; fill = current HP; dark segment = max HP lost to contracts; a tick every 20 HP.
            float baseWidth = DealEffects.BaseMaxHealth * _unitsPerHealth;
            Image hpBack = UiKit.Box(_root, "HpBar", UiKit.Ink, tl, tl, new Vector2(52f, -8f), new Vector2(baseWidth + 4f, 14f));
            _hpBack = hpBack.rectTransform;
            _hpFill = UiKit.Box(_hpBack, "Fill", UiKit.HealthRed, left, left, new Vector2(2f, 0f),
                new Vector2(baseWidth, 10f)).rectTransform;
            _hpLost = UiKit.Box(_hpBack, "Lost", LostHealth, left, left, new Vector2(2f + baseWidth, 0f),
                new Vector2(0f, 10f)).rectTransform;
            UiKit.Box(_hpLost, "Edge", UiKit.DevilRed, left, left, Vector2.zero, new Vector2(1f, 10f));
            _hpLost.gameObject.SetActive(false);
            for (int k = 1; k < 5; k++)
            {
                UiKit.Box(_hpBack, "Tick", new Color(0f, 0f, 0f, 0.45f), left, mid,
                    new Vector2(2f + 20f * k * _unitsPerHealth, 0f), new Vector2(1f, 10f));
            }
            // Numbers sit inside the bar's right end so they never reach the centred timer.
            _hpText = UiKit.Label(_hpBack, "Numbers", font, 10, Color.white, TextAnchor.MiddleRight,
                new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-4f, 0f), new Vector2(70f, 14f), FontStyle.Bold);
            UiKit.AddShadow(_hpText);

            // Costs taken ("Verdiklerin"), under the HP bar; hidden until the first contract.
            RectTransform strip = UiKit.Rect(_root, "Given", tl, tl, Vector2.zero, Vector2.zero);
            _costStrip = strip.gameObject;
            Text given = UiKit.Label(strip, "Label", font, 10, UiKit.CostRed, TextAnchor.MiddleLeft, tl, left,
                new Vector2(52f, -32f), new Vector2(68f, 14f), FontStyle.Bold);
            given.text = GameTexts.HudGiven;
            UiKit.AddShadow(given);
            for (int i = 0; i < MaxCostIcons; i++)
            {
                Image fill = UiKit.Framed(strip, "Cost_" + i, CostIconBorder, UiKit.Ink, tl, mid,
                    new Vector2(128f + i * 18f, -32f), new Vector2(16f, 16f), 1f);
                _costIconFill[i] = fill;
                _costIconBorder[i] = fill.transform.parent.GetComponent<Image>();
                _costIcons[i] = _costIconBorder[i].rectTransform;
                _costIcons[i].gameObject.SetActive(false);
            }
            _costStrip.SetActive(false);

            // Elapsed time (top centre) and score (top right).
            _timeText = UiKit.Label(_root, "Time", font, 16, Color.white, TextAnchor.MiddleCenter, top, top,
                new Vector2(0f, -5f), new Vector2(90f, 20f), FontStyle.Bold);
            UiKit.AddShadow(_timeText);
            _scoreText = UiKit.Label(_root, "Score", font, 12, UiKit.Cream, TextAnchor.MiddleRight, tr, tr,
                new Vector2(-8f, -8f), new Vector2(160f, 16f), FontStyle.Bold);
            UiKit.AddShadow(_scoreText);

            // Offer whisper: a small banner in the top-right corner under the score, only while an offer exists.
            Image offer = UiKit.Box(_root, "Offer", new Color(0.25f, 0.03f, 0.06f, 0.85f), tr, tr, OfferHome, OfferSize);
            _offer = offer.gameObject;
            _offerRect = offer.rectTransform;
            _offerGroup = _offer.AddComponent<CanvasGroup>();
            _offerGroup.blocksRaycasts = false;
            _offerPosGetter = () => _offerRect.anchoredPosition;
            _offerPosSetter = v => _offerRect.anchoredPosition = v;
            _offerWhisper = FitLine(UiKit.Label(offer.transform, "Whisper", font, 10, UiKit.Cream, TextAnchor.UpperLeft,
                tl, tl, new Vector2(5f, -2f), new Vector2(OfferSize.x - 10f, 13f)));
            _offerPlus = FitLine(UiKit.Label(offer.transform, "Plus", font, 8, new Color(0.55f, 1f, 0.6f, 1f),
                TextAnchor.UpperLeft, tl, tl, new Vector2(5f, -15f), new Vector2(OfferSize.x - 10f, 11f)));
            _offerMinus = FitLine(UiKit.Label(offer.transform, "Minus", font, 8, new Color(1f, 0.55f, 0.55f, 1f),
                TextAnchor.UpperLeft, tl, tl, new Vector2(5f, -26f), new Vector2(OfferSize.x - 10f, 11f)));
            Text equipped = UiKit.Label(offer.transform, "Equipped", font, 10, UiKit.Gold, TextAnchor.MiddleRight,
                left, new Vector2(1f, 0.5f), new Vector2(-4f, 0f), new Vector2(60f, 12f), FontStyle.Bold);
            equipped.text = GameTexts.HudEquippedDuplicate;
            UiKit.AddShadow(equipped);
            _offerEquipped = equipped.gameObject;
            _offerEquipped.SetActive(false);
            _offerTimer = UiKit.Box(offer.transform, "Lifetime", new Color(0.93f, 0.88f, 0.74f, 0.7f), Vector2.zero,
                Vector2.zero, new Vector2(2f, 1f), new Vector2(OfferTimerWidth, 2f)).rectTransform;
            _offer.SetActive(false);

            // Module slots (bottom centre, fixed order = ModuleType).
            for (int i = 0; i < ModuleSlots; i++)
            {
                Image fill = UiKit.Framed(_root, "Slot_" + (ModuleType)i, SlotEmptyBorder, SlotEmptyFill, bottom, bottom,
                    new Vector2(SlotX(i), 8f), SlotSize, 1f);
                _slotFill[i] = fill;
                _slotBorder[i] = fill.transform.parent.GetComponent<Image>();
                _slotRect[i] = _slotBorder[i].rectTransform;
                _slotLabel[i] = FitLine(UiKit.Label(fill.transform, "Name", font, 10, UiKit.Dim, TextAnchor.MiddleCenter,
                    mid, mid, Vector2.zero, new Vector2(SlotSize.x - 4f, 16f)));
            }
            _ejectLabel = UiKit.Label(_root, "EjectHint", font, 10, UiKit.Gold, TextAnchor.MiddleCenter, bottom, bottom,
                new Vector2(0f, 32f), new Vector2(170f, 12f), FontStyle.Bold);
            UiKit.AddShadow(_ejectLabel);
            _ejectLabel.gameObject.SetActive(false);
            _smallNosText = UiKit.Label(_root, "SmallNos", font, 10, UiKit.Turquoise, TextAnchor.MiddleLeft, bottom,
                left, new Vector2(SlotX(ModuleSlots - 1) + SlotSizeHighlighted.x * 0.5f + 4f, 18f), new Vector2(140f, 14f));
            UiKit.AddShadow(_smallNosText);

            // Charges (bottom left).
            _rocketPips = BuildPipRow(font, GameTexts.HudRocket, 62f);
            _machetePips = BuildPipRow(font, GameTexts.HudMachete, 44f);
            _dodgePips = BuildPipRow(font, GameTexts.HudDodge, 26f);
            _rejectPips = BuildPipRow(font, GameTexts.HudReject, 8f);

            BuildReticle();
        }

        /// <summary>One line that shrinks (best fit, down to about 70%) instead of wrapping or spilling out.</summary>
        private static Text FitLine(Text text)
        {
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            text.resizeTextForBestFit = true;
            text.resizeTextMaxSize = text.fontSize;
            text.resizeTextMinSize = Mathf.Max(6, Mathf.RoundToInt(text.fontSize * 0.7f));
            return text;
        }

        private static Image PortraitLayer(Transform parent, string name, Sprite sprite, Color color)
        {
            Image image = UiKit.Stretch(parent, name).gameObject.AddComponent<Image>();
            image.sprite = sprite;
            image.color = color;
            image.preserveAspect = true;
            image.raycastTarget = false;
            return image;
        }

        /// <summary>The drawn portrait shows the costs paid so far (1 half grey, 4 face veil, 5 darker, 6 all grey).</summary>
        private void ApplyDrawnPortraitCosts(int taken)
        {
            if (_portraitArt == null) return;
            float dark = taken >= 5 ? 0.72f : 1f;
            _portraitArt.color = new Color(dark, dark, dark, 1f);
            if (_portraitGrey != null)
            {
                float grey = taken >= 6 ? 1f : taken >= 1 ? 0.5f : 0f;
                _portraitGrey.color = new Color(dark, dark, dark, grey);
            }
            if (_portraitVeil != null) _portraitVeil.gameObject.SetActive(taken >= 4);
        }

        private Pips BuildPipRow(Font font, string label, float y)
        {
            Vector2 bl = Vector2.zero;
            Text text = UiKit.Label(_root, "Label_" + label, font, 10, UiKit.Cream, TextAnchor.MiddleLeft, bl, bl,
                new Vector2(8f, y), new Vector2(84f, 12f));
            text.text = label;
            UiKit.AddShadow(text);
            var pips = new Pips { Frames = new GameObject[MaxPips], Fills = new Image[MaxPips], ShownMax = -1, ShownFull = -1 };
            for (int i = 0; i < MaxPips; i++)
            {
                Image fill = UiKit.Framed(_root, "Pip", UiKit.Cream, UiKit.Gold, bl, bl,
                    new Vector2(94f + i * 12f, y + 1f), new Vector2(10f, 10f));
                pips.Frames[i] = fill.transform.parent.gameObject;
                pips.Fills[i] = fill;
            }
            SetPips(ref pips, 0, 0); // shown once its source reports
            return pips;
        }

        /// <summary>Chunky pixel crosshair (1-texel cream lines with an ink outline) on its own canvas.</summary>
        private void BuildReticle()
        {
            _reticleRoot = UiKit.CreateCanvas(transform, "ReticleCanvas", _reticleSortingOrder, false);
            Vector2 mid = new Vector2(0.5f, 0.5f);
            _reticle = UiKit.Rect(_reticleRoot, "Reticle", Vector2.zero, mid, Vector2.zero, Vector2.one);
            // Outline first, then the lines on top.
            ReticleBlock(UiKit.Ink, new Vector2(0f, 3.5f), new Vector2(3f, 5f));
            ReticleBlock(UiKit.Ink, new Vector2(0f, -3.5f), new Vector2(3f, 5f));
            ReticleBlock(UiKit.Ink, new Vector2(-3.5f, 0f), new Vector2(5f, 3f));
            ReticleBlock(UiKit.Ink, new Vector2(3.5f, 0f), new Vector2(5f, 3f));
            ReticleBlock(UiKit.Ink, Vector2.zero, new Vector2(3f, 3f));
            ReticleBlock(UiKit.Cream, new Vector2(0f, 3.5f), new Vector2(1f, 3f));
            ReticleBlock(UiKit.Cream, new Vector2(0f, -3.5f), new Vector2(1f, 3f));
            ReticleBlock(UiKit.Cream, new Vector2(-3.5f, 0f), new Vector2(3f, 1f));
            ReticleBlock(UiKit.Cream, new Vector2(3.5f, 0f), new Vector2(3f, 1f));
            ReticleBlock(UiKit.HealthRed, Vector2.zero, new Vector2(1f, 1f));
            _reticleRoot.gameObject.SetActive(false);
            _reticleShown = false;
        }

        private void ReticleBlock(Color color, Vector2 position, Vector2 size)
        {
            Vector2 mid = new Vector2(0.5f, 0.5f);
            UiKit.Box(_reticle, "Block", color, mid, mid, position, size);
        }
    }
}
