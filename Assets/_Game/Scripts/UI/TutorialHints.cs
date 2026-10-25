using System;
using UnityEngine;
using UnityEngine.UI;

namespace DanceWithTheDevil
{
    /// <summary>
    /// First-minute contextual hints (MAIN_GDD §12; first GDD §11 "İlk dakika önerisi"). One line at a time,
    /// bottom-centre above the module slots; never pauses play and never forces a choice.
    /// Sequence (each shown once per run):
    ///  1. Move/aim/fire: from the start until the player has moved AND fired once.
    ///  2. Offer: from the first module offer until it is accepted, rejected or expires.
    ///  3. Eject: after the first accept, for <see cref="_ejectHintDuration"/> s or until the first eject.
    ///  4. Rocket/machete/dodge: once, the first calm moment after <see cref="_rocketIdleDelay"/> s of play, unless
    ///     the player has already used both the rocket and the machete; for <see cref="_rocketHintDuration"/> s (or
    ///     until both have been used). The machete (F) has no other teacher, so a rocket fired early no longer
    ///     cancels this hint.
    ///  5. Slots full (K-23): whenever an offer is up while all three module slots are taken (not only once).
    /// Priority when several apply: slots full > offer > eject > move/fire > rocket; a pre-empted move/fire hint comes back.
    /// Hidden while the Devil screen or an ending is up (DevilDirector.State != Playing). Gameplay timers run on
    /// scaled time, fades on unscaled time (skipped with UiKit.ReduceMotion). No per-frame allocations.
    /// Missing references are resolved from the player's GameObject; anything still missing skips its hint.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class TutorialHints : MonoBehaviour
    {
        [Header("References")]
        [Tooltip("Player health. Input, modules, rifle and rocket are taken from its GameObject when empty.")]
        [SerializeField] private Health _playerHealth;
        [SerializeField] private PlayerInputReader _input;
        [SerializeField] private PlayerModules _modules;
        [SerializeField] private Rifle _rifle;
        [SerializeField] private RocketLauncher _rocket;
        [Tooltip("Hints hide while its State is not Playing. Empty = searched in parents; null = never hidden.")]
        [SerializeField] private DevilDirector _devil;

        [Header("Look")]
        [Tooltip("Font. Empty = built-in LegacyRuntime.")]
        [SerializeField] private Font _font;
        [Tooltip("Overlay canvas sorting order (HUD 10, intro 20, Devil 100).")]
        [SerializeField] private int _sortingOrder = 15;
        [Tooltip("Fade in/out seconds (unscaled).")]
        [SerializeField, Min(0f)] private float _fade = 0.3f;

        [Header("Timing (game seconds)")]
        [Tooltip("Eject hint length after the first accept.")]
        [SerializeField, Min(0.1f)] private float _ejectHintDuration = 6f;
        [Tooltip("Play time before the rocket/machete/dodge hint may appear.")]
        [SerializeField, Min(0f)] private float _rocketIdleDelay = 20f;
        [Tooltip("Rocket hint length.")]
        [SerializeField, Min(0.1f)] private float _rocketHintDuration = 5f;

        private enum Hint { None, MoveFire, Offer, Eject, Rocket, SlotsFull }

        private RectTransform _root;
        private CanvasGroup _group;
        private Text _text;

        private Hint _shown = Hint.None;
        private float _alpha;

        // Progress flags (once per run).
        private bool _moved, _fired, _basicDone;
        private bool _offerSeen, _offerOpen;
        private bool _acceptSeen, _ejectDone;
        private float _ejectTimeLeft;
        private bool _rocketFired, _macheteSwung, _rocketHintStarted;
        private float _rocketTimeLeft;
        private float _playTime;

        private bool _subscribed;
        private Action<ModuleType> _onOfferAppeared, _onOfferClosed, _onAccepted, _onEjected;
        private Action<Rifle, bool> _onRifleFired;
        private Action<RocketLauncher> _onRocketLaunched;
        private Action<Machete> _onSwing;

        private void Awake()
        {
            _onOfferAppeared = OnOfferAppeared;
            _onOfferClosed = OnOfferClosed;
            _onAccepted = OnAccepted;
            _onEjected = OnEjected;
            _onRifleFired = OnRifleFired;
            _onRocketLaunched = OnRocketLaunched;
            _onSwing = OnSwing;
            ResolveReferences();
            Build();
        }

        private void OnEnable() => Subscribe(true);
        private void OnDisable() => Subscribe(false);

        /// <summary>Code-built setups (tests): sets the player and the Devil and re-subscribes.</summary>
        public void Configure(Health playerHealth, DevilDirector devil)
        {
            Subscribe(false);
            _playerHealth = playerHealth;
            _devil = devil;
            _input = null;
            _modules = null;
            _rifle = null;
            _rocket = null;
            ResolveReferences();
            if (isActiveAndEnabled) Subscribe(true);
        }

        private void ResolveReferences()
        {
            if (_devil == null) _devil = GetComponentInParent<DevilDirector>();
            if (_playerHealth == null) return;
            GameObject p = _playerHealth.gameObject;
            if (_input == null) _input = p.GetComponentInChildren<PlayerInputReader>(true);
            if (_modules == null) _modules = p.GetComponentInChildren<PlayerModules>(true);
            if (_rifle == null) _rifle = p.GetComponentInChildren<Rifle>(true);
            if (_rocket == null) _rocket = p.GetComponentInChildren<RocketLauncher>(true);
        }

        private void Subscribe(bool on)
        {
            if (on == _subscribed) return;
            _subscribed = on;
            if (on)
            {
                Rifle.Fired += _onRifleFired;
                RocketLauncher.Launched += _onRocketLaunched;
                Machete.SwingStarted += _onSwing;
                if (_modules == null) return;
                _modules.OfferAppeared += _onOfferAppeared;
                _modules.Accepted += _onAccepted;
                _modules.Rejected += _onOfferClosed;
                _modules.OfferExpired += _onOfferClosed;
                _modules.Ejected += _onEjected;
            }
            else
            {
                Rifle.Fired -= _onRifleFired;
                RocketLauncher.Launched -= _onRocketLaunched;
                Machete.SwingStarted -= _onSwing;
                if (_modules == null) return;
                _modules.OfferAppeared -= _onOfferAppeared;
                _modules.Accepted -= _onAccepted;
                _modules.Rejected -= _onOfferClosed;
                _modules.OfferExpired -= _onOfferClosed;
                _modules.Ejected -= _onEjected;
            }
        }

        // ------------------------------------------------------------ events

        private void OnRifleFired(Rifle rifle, bool triple)
        {
            if (_rifle == null || rifle == _rifle) _fired = true;
        }

        private void OnRocketLaunched(RocketLauncher launcher)
        {
            if (_rocket != null && launcher != _rocket) return;
            _rocketFired = true;
            if (_macheteSwung) _rocketTimeLeft = 0f; // using both ends the hint
        }

        private void OnSwing(Machete machete)
        {
            _macheteSwung = true;
            if (_rocketFired) _rocketTimeLeft = 0f;
        }

        private void OnOfferAppeared(ModuleType type)
        {
            if (_offerSeen) return;
            _offerSeen = true;
            _offerOpen = true;
        }

        private void OnOfferClosed(ModuleType type) => _offerOpen = false;

        private void OnAccepted(ModuleType type)
        {
            _offerOpen = false;
            if (_acceptSeen) return;
            _acceptSeen = true;
            _ejectTimeLeft = _ejectHintDuration;
        }

        private void OnEjected(ModuleType type)
        {
            _ejectDone = true;
            _ejectTimeLeft = 0f;
        }

        // ------------------------------------------------------------ update

        private void Update()
        {
            bool playing = _devil == null || _devil.State == DevilState.Playing;
            if (!playing)
            {
                // Devil screen or ending: hide at once; nothing counts down (time is frozen there anyway).
                if (_root.gameObject.activeSelf) _root.gameObject.SetActive(false);
                return;
            }
            if (!_root.gameObject.activeSelf) _root.gameObject.SetActive(true);

            Track(Time.deltaTime);
            Present(Desired(), Time.unscaledDeltaTime);
        }

        private void Track(float dt)
        {
            _playTime += dt;
            if (!_moved && _input != null && _input.MoveInput.sqrMagnitude > 0.01f) _moved = true;
            if (!_basicDone && ((_moved && _fired) || _input == null)) _basicDone = true;
            if (_ejectTimeLeft > 0f) _ejectTimeLeft = _ejectDone ? 0f : _ejectTimeLeft - dt;

            if (!_rocketHintStarted && !(_rocketFired && _macheteSwung) && _rocket != null && _playTime >= _rocketIdleDelay
                && _rocket.Charges > 0 && !_offerOpen && _ejectTimeLeft <= 0f && _basicDone)
            {
                _rocketHintStarted = true;
                _rocketTimeLeft = _rocketHintDuration;
            }
            else if (_rocketTimeLeft > 0f)
            {
                _rocketTimeLeft -= dt;
            }
        }

        private Hint Desired()
        {
            // Every time, not only in the first minute: the way to take an offer with full slots (K-23).
            ModuleOffer offer = _modules != null ? _modules.Current : null;
            if (offer != null && offer.SlotsFull && !offer.IsEquippedDuplicate) return Hint.SlotsFull;
            if (_offerOpen) return Hint.Offer;
            if (_ejectTimeLeft > 0f) return Hint.Eject;
            if (!_basicDone) return Hint.MoveFire;
            if (_rocketTimeLeft > 0f) return Hint.Rocket;
            return Hint.None;
        }

        /// <summary>Fades the current line out, swaps the text, fades the new one in. One line at a time.</summary>
        private void Present(Hint desired, float unscaledDt)
        {
            float step = UiKit.ReduceMotion || _fade <= 0f ? 1f : unscaledDt / _fade;
            if (desired != _shown)
            {
                _alpha = Mathf.Max(0f, _alpha - step);
                if (_alpha <= 0f)
                {
                    _shown = desired;
                    if (desired != Hint.None) _text.text = TextFor(desired);
                }
            }
            else if (_shown != Hint.None)
            {
                _alpha = Mathf.Min(1f, _alpha + step);
            }
            if (!Mathf.Approximately(_group.alpha, _alpha)) _group.alpha = _alpha;
        }

        private static string TextFor(Hint hint) => hint switch
        {
            Hint.MoveFire => GameTexts.HintMoveFire,
            Hint.Offer => GameTexts.HintOffer,
            Hint.Eject => GameTexts.HintEject,
            Hint.SlotsFull => GameTexts.HintSlotsFull,
            _ => GameTexts.HintRocket,
        };

        // ------------------------------------------------------------ build

        private void Build()
        {
            Font font = UiKit.ResolveFont(_font);
            _root = UiKit.CreateCanvas(transform, "HintCanvas", _sortingOrder, false);
            Vector2 bottom = new Vector2(0.5f, 0f);
            // Above the HUD's module slots (y 8..28) and the Q eject label (y 26..38); narrow enough to clear the
            // charge pips at the bottom left.
            Image panel = UiKit.Box(_root, "Hint", new Color(0.06f, 0.05f, 0.08f, 0.75f), bottom, bottom,
                new Vector2(0f, 44f), new Vector2(330f, 18f));
            _group = panel.gameObject.AddComponent<CanvasGroup>();
            _group.alpha = 0f;
            _group.interactable = false;
            _group.blocksRaycasts = false;
            _text = UiKit.Label(panel.transform, "Text", font, 12, UiKit.Cream, TextAnchor.MiddleCenter,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(324f, 16f), FontStyle.Bold);
            UiKit.AddShadow(_text);
        }
    }
}
