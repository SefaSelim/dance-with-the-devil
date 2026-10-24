using System;
using UnityEngine;
using UnityEngine.UI;

namespace DanceWithTheDevil
{
    /// <summary>
    /// In-run HUD (MAIN_GDD §12). Top: HP bar whose width scales with max HP (contract 2 "can çubuğu kısalır") with
    /// numbers, remaining run time (5:00 countdown from WaveDirector.Elapsed), score. Top-left: Defne's small framed
    /// portrait (hidden by contract 3). Bottom: three fixed module slots, rocket charges, dodge charges and the
    /// reject-ready pip. While a whisper offer exists: its whisper line plus one-line advantage and cost.
    /// Display only; texts are rewritten only when their value changes (no per-frame allocations).
    /// Missing references are resolved from the player's GameObject; anything still missing is just not shown.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class GameHud : MonoBehaviour
    {
        private const int MaxPips = 4;
        private const int ModuleSlots = 3;

        [Header("References")]
        [Tooltip("Player health. Other player components are taken from its GameObject when empty.")]
        [SerializeField] private Health _playerHealth;
        [SerializeField] private PlayerController _controller;
        [SerializeField] private RocketLauncher _rocket;
        [SerializeField] private PlayerInputReader _input;
        [Tooltip("Whisper modules (optional).")]
        [SerializeField] private PlayerModules _modules;
        [SerializeField] private WaveDirector _waves;
        [SerializeField] private ScoreKeeper _score;

        [Header("Look")]
        [Tooltip("Portrait art. Empty = placeholder face built from blocks.")]
        [SerializeField] private Sprite _portraitSprite;
        [Tooltip("Font. Empty = built-in LegacyRuntime.")]
        [SerializeField] private Font _font;
        [Tooltip("Overlay canvas sorting order (display is -1000).")]
        [SerializeField] private int _sortingOrder = 10;
        [Tooltip("HP bar width per hit point, canvas units (100 HP = 200).")]
        [SerializeField, Min(0.1f)] private float _unitsPerHealth = 2f;
        [Tooltip("Run length for the countdown when there is no wave director, seconds.")]
        [SerializeField, Min(1f)] private float _runLength = 300f;
        [Tooltip("Reject cooldown shown by the pip (GDD §7: 0.65 s). Approximation until PlayerModules exposes readiness.")]
        [SerializeField, Min(0f)] private float _rejectCooldown = 0.65f;

        private RectTransform _root;
        private GameObject _portrait;
        private RectTransform _hpBack;
        private RectTransform _hpFill;
        private Text _hpText;
        private Text _timeText;
        private Text _scoreText;
        private GameObject _offer;
        private Text _offerWhisper;
        private Text _offerPlus;
        private Text _offerMinus;
        private readonly Image[] _slotFill = new Image[ModuleSlots];
        private readonly Text[] _slotLabel = new Text[ModuleSlots];
        private readonly bool[] _slotState = new bool[ModuleSlots];
        private Pips _rocketPips, _dodgePips, _rejectPips;

        private string[] _timeStrings;
        private int _shownSeconds = -1;
        private int _shownScore = -1;
        private int _shownHp = -1;
        private int _shownMaxHp = -1;
        private float _rejectReadyTime;
        private bool _subscribed;
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
            _timeStrings = new string[Mathf.CeilToInt(_runLength) + 1];
            Build();
            for (int i = 0; i < ModuleSlots; i++) ApplySlot(i, false, true);
        }

        private void OnEnable() => Subscribe(true);
        private void OnDisable() => Subscribe(false);

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
            ResolveReferences();
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
            if (_playerHealth == null) return;
            GameObject p = _playerHealth.gameObject;
            if (_controller == null) _controller = p.GetComponent<PlayerController>();
            if (_rocket == null) _rocket = p.GetComponentInChildren<RocketLauncher>(true);
            if (_input == null) _input = p.GetComponentInChildren<PlayerInputReader>(true);
            if (_modules == null) _modules = p.GetComponentInChildren<PlayerModules>(true);
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
            _offerWhisper.text = "<b>" + GameTexts.ModuleName(type) + "</b>  <i>" + GameTexts.ModuleWhisper(type) + "</i>";
            _offerPlus.text = "+ " + GameTexts.ModuleAdvantage(type);
            _offerMinus.text = "− " + GameTexts.ModuleCost(type);
            _offer.SetActive(true);
        }

        private void OnOfferClosed(ModuleType type) => _offer.SetActive(false);

        private void Update()
        {
            UpdateHealth();
            UpdateTime();
            UpdateScore();
            UpdateSlots();
            UpdatePips();
        }

        private void UpdateHealth()
        {
            if (_playerHealth == null) return;
            int max = Mathf.RoundToInt(_playerHealth.MaxHealth);
            int hp = Mathf.CeilToInt(_playerHealth.Current);
            if (max == _shownMaxHp && hp == _shownHp) return;
            if (max != _shownMaxHp)
                _hpBack.sizeDelta = new Vector2(max * _unitsPerHealth + 4f, _hpBack.sizeDelta.y);
            _shownMaxHp = max;
            _shownHp = hp;
            _hpFill.sizeDelta = new Vector2(Mathf.Max(0f, hp) * _unitsPerHealth, _hpFill.sizeDelta.y);
            _hpText.text = hp + "/" + max;
        }

        private void UpdateTime()
        {
            float length = _waves != null ? _waves.SpawnEndTime : _runLength;
            float elapsed = _waves != null ? _waves.Elapsed : 0f;
            int seconds = Mathf.Clamp(Mathf.CeilToInt(length - elapsed), 0, _timeStrings.Length - 1);
            if (seconds == _shownSeconds) return;
            _shownSeconds = seconds;
            if (_timeStrings[seconds] == null) _timeStrings[seconds] = (seconds / 60) + ":" + (seconds % 60).ToString("00");
            _timeText.text = _timeStrings[seconds];
        }

        private void UpdateScore()
        {
            int score = _score != null ? _score.Score : 0;
            if (score == _shownScore) return;
            _shownScore = score;
            _scoreText.text = string.Format(GameTexts.HudScoreFormat, score);
        }

        private void UpdateSlots()
        {
            if (_modules == null) return;
            for (int i = 0; i < ModuleSlots; i++) ApplySlot(i, _modules.IsEquipped((ModuleType)i), false);
        }

        private void ApplySlot(int i, bool equipped, bool force)
        {
            if (!force && _slotState[i] == equipped) return;
            _slotState[i] = equipped;
            _slotFill[i].color = equipped ? UiKit.Turquoise : UiKit.Ink;
            _slotLabel[i].color = equipped ? UiKit.Ink : UiKit.Dim;
            _slotLabel[i].fontStyle = equipped ? FontStyle.Bold : FontStyle.Normal;
        }

        private void UpdatePips()
        {
            if (_rocket != null) SetPips(ref _rocketPips, _rocket.MaxCharges, _rocket.Charges);
            if (_controller != null) SetPips(ref _dodgePips, _controller.MaxDodgeCharges, _controller.DodgeCharges);

            // Approximation: the module system owns the real cooldown; replace with its readiness when exposed.
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

        // ------------------------------------------------------------ build

        private void Build()
        {
            Font font = UiKit.ResolveFont(_font);
            _root = UiKit.CreateCanvas(transform, "HudCanvas", _sortingOrder, false);
            Vector2 tl = new Vector2(0f, 1f);
            Vector2 mid = new Vector2(0.5f, 0.5f);

            // Portrait (top-left).
            Image portrait = UiKit.Framed(_root, "Portrait", UiKit.Cream, new Color(0.12f, 0.24f, 0.28f, 1f), tl, tl,
                new Vector2(6f, -6f), new Vector2(40f, 40f));
            _portrait = portrait.transform.parent.gameObject;
            if (_portraitSprite != null)
            {
                Image art = UiKit.Stretch(portrait.transform, "Art").gameObject.AddComponent<Image>();
                art.sprite = _portraitSprite;
                art.preserveAspect = true;
                art.raycastTarget = false;
            }
            else
            {
                Transform f = portrait.transform;
                Color hair = new Color(0.45f, 0.18f, 0.4f, 1f);
                UiKit.Box(f, "Face", new Color(0.95f, 0.78f, 0.62f, 1f), mid, mid, new Vector2(0f, -4f), new Vector2(20f, 20f));
                UiKit.Box(f, "Hair_Top", hair, mid, mid, new Vector2(0f, 9f), new Vector2(28f, 10f));
                UiKit.Box(f, "Hair_L", hair, mid, mid, new Vector2(-12f, 0f), new Vector2(6f, 18f));
                UiKit.Box(f, "Hair_R", hair, mid, mid, new Vector2(12f, 0f), new Vector2(6f, 18f));
                UiKit.Box(f, "Glasses_L", UiKit.Ink, mid, mid, new Vector2(-5f, -3f), new Vector2(7f, 5f));
                UiKit.Box(f, "Glasses_R", UiKit.Ink, mid, mid, new Vector2(5f, -3f), new Vector2(7f, 5f));
                UiKit.Box(f, "Overalls", new Color(0.1f, 0.35f, 0.4f, 1f), mid, mid, new Vector2(0f, -16f), new Vector2(26f, 4f));
            }

            // HP bar.
            Image hpBack = UiKit.Box(_root, "HpBar", UiKit.Ink, tl, tl, new Vector2(52f, -8f), new Vector2(204f, 14f));
            _hpBack = hpBack.rectTransform;
            _hpFill = UiKit.Box(_hpBack, "Fill", UiKit.HealthRed, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
                new Vector2(2f, 0f), new Vector2(200f, 10f)).rectTransform;
            _hpText = UiKit.Label(_hpBack, "Numbers", font, 11, Color.white, TextAnchor.MiddleLeft,
                new Vector2(1f, 0.5f), new Vector2(0f, 0.5f), new Vector2(6f, 0f), new Vector2(70f, 14f), FontStyle.Bold);
            UiKit.AddShadow(_hpText);

            // Time (top centre) and score (top right).
            Vector2 top = new Vector2(0.5f, 1f);
            _timeText = UiKit.Label(_root, "Time", font, 18, Color.white, TextAnchor.MiddleCenter, top, top,
                new Vector2(0f, -4f), new Vector2(90f, 22f), FontStyle.Bold);
            UiKit.AddShadow(_timeText);
            Vector2 tr = new Vector2(1f, 1f);
            _scoreText = UiKit.Label(_root, "Score", font, 12, UiKit.Cream, TextAnchor.MiddleRight, tr, tr,
                new Vector2(-8f, -8f), new Vector2(160f, 16f), FontStyle.Bold);
            UiKit.AddShadow(_scoreText);

            // Offer whisper (near the top, only while an offer exists).
            Image offer = UiKit.Box(_root, "Offer", new Color(0.25f, 0.03f, 0.06f, 0.8f), top, top,
                new Vector2(0f, -30f), new Vector2(460f, 46f));
            _offer = offer.gameObject;
            _offerWhisper = UiKit.Label(offer.transform, "Whisper", font, 11, UiKit.Cream, TextAnchor.UpperCenter,
                top, top, new Vector2(0f, -3f), new Vector2(450f, 14f));
            _offerPlus = UiKit.Label(offer.transform, "Plus", font, 10, new Color(0.55f, 1f, 0.6f, 1f),
                TextAnchor.UpperCenter, top, top, new Vector2(0f, -17f), new Vector2(450f, 13f));
            _offerMinus = UiKit.Label(offer.transform, "Minus", font, 10, new Color(1f, 0.55f, 0.55f, 1f),
                TextAnchor.UpperCenter, top, top, new Vector2(0f, -30f), new Vector2(450f, 13f));
            _offer.SetActive(false);

            // Module slots (bottom centre, fixed order = ModuleType).
            Vector2 bottom = new Vector2(0.5f, 0f);
            for (int i = 0; i < ModuleSlots; i++)
            {
                Image fill = UiKit.Framed(_root, "Slot_" + (ModuleType)i, UiKit.Cream, UiKit.Ink, bottom, bottom,
                    new Vector2((i - 1) * 84f, 8f), new Vector2(78f, 20f));
                _slotFill[i] = fill;
                _slotLabel[i] = UiKit.Label(fill.transform, "Name", font, 10, UiKit.Dim, TextAnchor.MiddleCenter,
                    mid, mid, Vector2.zero, new Vector2(74f, 16f));
                _slotLabel[i].text = GameTexts.ModuleName((ModuleType)i);
            }

            // Charges (bottom left).
            _rocketPips = BuildPipRow(font, GameTexts.HudRocket, 44f);
            _dodgePips = BuildPipRow(font, GameTexts.HudDodge, 26f);
            _rejectPips = BuildPipRow(font, GameTexts.HudReject, 8f);
        }

        private Pips BuildPipRow(Font font, string label, float y)
        {
            Vector2 bl = Vector2.zero;
            Text text = UiKit.Label(_root, "Label_" + label, font, 9, UiKit.Cream, TextAnchor.MiddleLeft, bl, bl,
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
            return pips;
        }
    }
}
