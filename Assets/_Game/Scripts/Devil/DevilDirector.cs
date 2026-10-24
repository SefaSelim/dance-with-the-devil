using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace DanceWithTheDevil
{
    /// <summary>
    /// The death → Devil → contract → EVET/HAYIR chain (MAIN_GDD §3, §8, §11; K-2, K-3, K-10, K-12).
    /// On the player's death: hitstop cancelled, time frozen, wave clock paused, screen desaturates, the Devil
    /// slides in from the right (first time: "Psst..."), then the contract card.
    /// EVET: deal applied (DealEffects + visible costs), revive at the new max, 2 s invulnerability (0 from
    /// contract 5 on), the shockwave kills every living enemy (D3, provisional; K-12's 6 m radius when
    /// <see cref="_shockwaveClearsAll"/> is off), saturation back to the run's base, time runs again. Contract 1's
    /// colour cost is played out on screen: the colour comes back first, then visibly drains past the base and
    /// settles at it. Lines wait for the Devil's typewriter before their auto-advance timer starts.
    /// HAYIR: the NO line, then the ending/results.
    /// PROVISIONAL (open question A-4): death 6 shows contract 6 without a choice, Defne turns grey, controls get
    /// 0.25 s delay and play goes on; the next death is the always-yes ending (line, grey shell crumbles, end card).
    /// Works without any UI assigned (tests): lines auto-advance and <see cref="ChooseYes"/>/<see cref="ChooseNo"/>
    /// decide. Runs on unscaled time.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class DevilDirector : MonoBehaviour
    {
        [Header("Player")]
        [Tooltip("Player health (its Died starts the flow). Other player components are found on its GameObject.")]
        [SerializeField] private Health _playerHealth;
        [Tooltip("Whisper modules, for the 'Küçük hayırların' count (optional).")]
        [SerializeField] private PlayerModules _modules;
        [Tooltip("Defne's visible costs (optional; found on the player when empty).")]
        [SerializeField] private DefneCostVisuals _defne;

        [Header("World")]
        [SerializeField] private WaveDirector _waves;
        [SerializeField] private ScoreKeeper _score;
        [Tooltip("Display saturation/vignette (optional).")]
        [SerializeField] private DisplayGrade _grade;

        [Header("UI (optional)")]
        [SerializeField] private DevilScreen _screen;
        [SerializeField] private EndingScreen _endingScreen;
        [SerializeField] private GameHud _hud;

        [Header("Deals")]
        [Tooltip("Contracts in order. Empty = the six GDD §8 defaults built in memory.")]
        [SerializeField] private DealDefinition[] _deals = Array.Empty<DealDefinition>();

        [Header("Timing (unscaled seconds)")]
        [SerializeField, Min(0f)] private float _desaturateDuration = 0.5f;
        [SerializeField, Min(0f)] private float _restoreDuration = 0.4f;
        [Tooltip("Lines auto-advance after this long if the player does nothing.")]
        [SerializeField, Min(0.1f)] private float _lineAutoAdvance = 4f;
        [Tooltip("Contract 6 card (no choice) auto-advances after this long.")]
        [SerializeField, Min(0.1f)] private float _forcedCardAutoAdvance = 4.5f;
        [Tooltip("Always-yes ending: time for the cubes before the end card.")]
        [SerializeField, Min(0f)] private float _crumbleDuration = 1.8f;

        [Header("Revive (K-12)")]
        [SerializeField, Min(0f)] private float _reviveInvulnerability = DealEffects.ReviveInvulnerabilitySeconds;
        [Tooltip("Shockwave radius when it does not clear every enemy (K-12: 6 m).")]
        [SerializeField, Min(0f)] private float _shockwaveRadius = 6f;
        [Tooltip("D3 (provisional): the revive shockwave clears every living enemy. Off = only within the radius.")]
        [SerializeField] private bool _shockwaveClearsAll = true;

        [Header("Visible costs")]
        [Tooltip("World saturation after contract 1 (GDD: −30%).")]
        [SerializeField, Range(0f, 1f)] private float _contractOneSaturation = 0.7f;
        [Tooltip("Contract 1: seconds the colour is shown fully back before it drains (unscaled).")]
        [SerializeField, Min(0f)] private float _contractOneColourHold = 0.5f;
        [Tooltip("Contract 1: the drain overshoots to this saturation so the loss is seen, then settles at the base.")]
        [SerializeField, Range(0f, 1f)] private float _contractOneOvershoot = 0.45f;
        [Tooltip("Contract 1: seconds of the visible drain, then of the settle back to the base (unscaled).")]
        [SerializeField, Min(0f)] private float _contractOneDrainDuration = 1.2f;
        [SerializeField, Min(0f)] private float _contractOneSettleDuration = 0.8f;
        [Tooltip("Edge vignette after contract 5. Spawn warnings are in the world image, so keep the centre clear.")]
        [SerializeField, Range(0f, 1f)] private float _contractFiveVignette = 0.6f;

        private readonly List<DealDefinition> _taken = new List<DealDefinition>(6);
        private DealDefinition[] _ownedDeals;
        private DealTargets _targets;
        private Action<DamageInfo> _onDied;
        private bool _subscribed;
        private bool _metDevil;
        private bool _advanceRequested;
        private int _choice; // 0 none, 1 yes, -1 no
        private float _baseSaturation = 1f;
        private float _baseVignette;
        private Coroutine _colourDrain;

        public DevilState State { get; private set; } = DevilState.Playing;
        public EndingKind Ending { get; private set; } = EndingKind.None;
        public int Deaths { get; private set; }
        public int DealsTaken => _taken.Count;
        public IReadOnlyList<DealDefinition> TakenDeals => _taken;
        public IReadOnlyList<DealDefinition> Deals => _deals;
        /// <summary>The contract on the card (Offer/Forced), else null.</summary>
        public DealDefinition CurrentDeal { get; private set; }

        public event Action<DevilState> StateChanged;
        /// <summary>Raised when EVET (true) or HAYIR (false) is taken on a card with a choice (audio).</summary>
        public event Action<bool> ContractChosen;

        private void Awake()
        {
            _onDied = OnPlayerDied;
            Enemy.SpeedMultiplier = 1f; // run-wide statics start clean (also without domain reload)
            AudioCosts.Reset();
            if (_deals == null || _deals.Length == 0)
            {
                _ownedDeals = DealLibrary.CreateDefaults();
                _deals = _ownedDeals;
            }
            Resolve();
        }

        private void OnEnable()
        {
            Subscribe(true);
            if (_screen != null)
            {
                _screen.YesPressed += ChooseYes;
                _screen.NoPressed += ChooseNo;
                _screen.AdvancePressed += Advance;
            }
            if (_endingScreen != null) _endingScreen.RestartPressed += Restart;
        }

        private void OnDisable()
        {
            Subscribe(false);
            if (_screen != null)
            {
                _screen.YesPressed -= ChooseYes;
                _screen.NoPressed -= ChooseNo;
                _screen.AdvancePressed -= Advance;
            }
            if (_endingScreen != null) _endingScreen.RestartPressed -= Restart;
        }

        private void OnDestroy()
        {
            if (_ownedDeals == null) return;
            for (int i = 0; i < _ownedDeals.Length; i++)
                if (_ownedDeals[i] != null) Destroy(_ownedDeals[i]);
        }

        /// <summary>Code-built setups (tests): sets the player (and optional waves) and re-subscribes.</summary>
        public void Configure(Health playerHealth, WaveDirector waves = null)
        {
            Subscribe(false);
            _playerHealth = playerHealth;
            if (waves != null) _waves = waves;
            Resolve();
            if (isActiveAndEnabled) Subscribe(true);
        }

        /// <summary>EVET on the card. Ignored unless the card offers a choice.</summary>
        public void ChooseYes()
        {
            if (State != DevilState.Offer || _choice != 0) return;
            _choice = 1;
            ContractChosen?.Invoke(true);
        }

        /// <summary>HAYIR on the card. Ignored unless the card offers a choice.</summary>
        public void ChooseNo()
        {
            if (State != DevilState.Offer || _choice != 0) return;
            _choice = -1;
            ContractChosen?.Invoke(false);
        }

        /// <summary>Skips the current wait (slide-in, a line, the forced card, the crumble).</summary>
        public void Advance() => _advanceRequested = true;

        /// <summary>New run: resets statics and reloads the active scene.</summary>
        public void Restart() => RunRestart.ReloadActiveScene();

        // ------------------------------------------------------------ flow

        private void OnPlayerDied(DamageInfo info)
        {
            if (State != DevilState.Playing) return;
            StartCoroutine(DeathSequence());
        }

        private IEnumerator DeathSequence()
        {
            Deaths++;
            if (_colourDrain != null)
            {
                StopCoroutine(_colourDrain);
                _colourDrain = null;
            }
            SetState(DevilState.Freezing);
            if (HitStop.Instance != null) HitStop.Instance.CancelAll();
            Time.timeScale = 0f;
            if (_waves != null) _waves.Paused = true;
            if (_grade != null) _grade.SetSaturation(0f, _desaturateDuration);
            if (_screen != null) _screen.Enter();
            float enter = Mathf.Max(_desaturateDuration, _screen != null ? _screen.EnterDuration : 0f);
            yield return Wait(enter);

            // Every contract taken: the next death ends the run (A-4 provisional).
            if (_taken.Count >= _deals.Length)
            {
                yield return AlwaysYesEnding();
                yield break;
            }

            DealDefinition deal = _deals[_taken.Count];
            if (!_metDevil && _taken.Count == 0)
            {
                _metDevil = true;
                yield return Speak(GameTexts.DevilFirstAppearance);
            }

            CurrentDeal = deal;
            if (deal.OffersChoice)
            {
                _choice = 0;
                SetState(DevilState.Offer);
                if (_screen != null) _screen.ShowCard(deal, true);
                while (_choice == 0) yield return null;
                if (_choice < 0)
                {
                    CurrentDeal = null;
                    yield return NoEnding();
                    yield break;
                }
            }
            else
            {
                SetState(DevilState.Forced);
                if (_screen != null) _screen.ShowCard(deal, false);
                yield return WaitAfterTyping(_forcedCardAutoAdvance);
            }

            CurrentDeal = null;
            Accept(deal);
        }

        private void Accept(DealDefinition deal)
        {
            _taken.Add(deal);
            DealEffects.Apply(deal, _targets);
            _reviveInvulnerability = DealEffects.ReviveInvulnerabilityAfter(deal, _reviveInvulnerability);
            ApplyVisibleCost(deal.Number);
            if (_screen != null) _screen.Hide();

            Time.timeScale = 1f;
            if (_waves != null) _waves.Paused = false;
            if (_grade != null)
            {
                if (deal.Number == 1) _colourDrain = StartCoroutine(ContractOneDrain());
                else _grade.SetSaturation(_baseSaturation, _restoreDuration);
            }

            Vector3 position = transform.position;
            if (_playerHealth != null)
            {
                _playerHealth.Revive();
                if (_reviveInvulnerability > 0f) _playerHealth.SetInvulnerable(_reviveInvulnerability);
                position = _playerHealth.transform.position;
            }

            // Revive shockwave from the Devil: every living enemy (D3, provisional), else K-12's radius.
            if (_waves != null)
            {
                if (_shockwaveClearsAll) _waves.KillAll();
                else _waves.KillWithin(position, _shockwaveRadius);
            }
            ImpactBus.Raise(new ImpactEvent
            {
                kind = ImpactKind.Explosion,
                position = position + Vector3.up * 0.5f,
                normal = Vector3.up,
                direction = Vector3.up,
                intensity = 1f,
                radius = _shockwaveClearsAll ? 0f : _shockwaveRadius, // 0 = the feedback's full-view sweep
                surface = SurfaceType.Default,
                instigator = gameObject,
                instigatorIsLocalPlayer = true,
            });

            SetState(DevilState.Playing);
        }

        private void ApplyVisibleCost(int number)
        {
            switch (number)
            {
                case 1:
                    _baseSaturation = _contractOneSaturation;
                    if (_defne != null) _defne.DrainHair();
                    break;
                case 2:
                    // Audio flags set by DealEffects; the HP bar shortens with max HP in the HUD.
                    break;
                case 3:
                    if (_hud != null) _hud.SetPortraitVisible(false);
                    break;
                case 4:
                    if (_defne != null) _defne.RemoveFaceParts();
                    break;
                case 5:
                    _baseVignette = _contractFiveVignette;
                    if (_grade != null) _grade.SetVignette(_baseVignette, _restoreDuration);
                    break;
                case 6:
                    if (_defne != null) _defne.TurnAllGrey();
                    break;
            }
        }

        /// <summary>
        /// Contract 1 "Rengin" on screen: full colour back, a beat, a visible drain past the base, then it settles at
        /// the base (the permanent −30% stays as the GDD says; only the moment of loss is made visible).
        /// </summary>
        private IEnumerator ContractOneDrain()
        {
            _grade.SetSaturation(1f, _restoreDuration);
            yield return Delay(_restoreDuration + _contractOneColourHold);
            _grade.SetSaturation(Mathf.Min(_contractOneOvershoot, _baseSaturation), _contractOneDrainDuration);
            yield return Delay(_contractOneDrainDuration);
            _grade.SetSaturation(_baseSaturation, _contractOneSettleDuration);
            _colourDrain = null;
        }

        /// <summary>Unscaled wait that cannot be skipped.</summary>
        private static IEnumerator Delay(float seconds)
        {
            float t = 0f;
            while (t < seconds)
            {
                t += Time.unscaledDeltaTime;
                yield return null;
            }
        }

        private IEnumerator NoEnding()
        {
            bool first = _taken.Count == 0;
            Ending = first ? EndingKind.KeptYourself : EndingKind.DiminishedButStanding;
            yield return Speak(first ? GameTexts.DevilNoAtFirstDeath : GameTexts.DevilNoLater);
            if (_screen != null) _screen.Hide();
            // HAYIR at death 1: Defne keeps her colours, so the world comes back in full colour.
            if (first && _grade != null) _grade.SetSaturation(1f, _restoreDuration);
            ShowEnding();
        }

        private IEnumerator AlwaysYesEnding()
        {
            Ending = EndingKind.AlwaysYes;
            yield return Speak(GameTexts.DevilAlwaysYes);
            if (_screen != null) _screen.Hide();

            SetState(DevilState.Crumbling);
            if (_waves != null)
            {
                _waves.StopSpawning();
                _waves.Paused = true;
            }
            Enemy.SpeedMultiplier = 0f; // enemies hold still while the shell falls (reset on restart)
            Time.timeScale = 1f;        // the cube particles simulate on scaled time
            if (_defne != null && _playerHealth != null)
                _defne.Crumble(_playerHealth.transform.position + Vector3.up);
            yield return Wait(_crumbleDuration);
            ShowEnding();
        }

        private void ShowEnding()
        {
            SetState(DevilState.Ended);
            if (_endingScreen == null) return;
            int smallNos = _modules != null ? _modules.SmallNoCount : 0;
            int score = _score != null ? _score.Score : 0;
            _endingScreen.Show(new RunSummary(Ending, _taken, smallNos, score));
        }

        private IEnumerator Speak(string line)
        {
            SetState(DevilState.Speaking);
            if (_screen != null) _screen.ShowLine(line);
            yield return WaitAfterTyping(_lineAutoAdvance);
        }

        /// <summary>Like <see cref="Wait"/>, but the clock only runs once the Devil's line has typed out.</summary>
        private IEnumerator WaitAfterTyping(float seconds)
        {
            _advanceRequested = false;
            float t = 0f;
            while (t < seconds && !_advanceRequested)
            {
                if (_screen == null || !_screen.IsTyping) t += Time.unscaledDeltaTime;
                yield return null;
            }
            _advanceRequested = false;
        }

        /// <summary>Waits unscaled seconds or until <see cref="Advance"/>.</summary>
        private IEnumerator Wait(float seconds)
        {
            _advanceRequested = false;
            float t = 0f;
            while (t < seconds && !_advanceRequested)
            {
                t += Time.unscaledDeltaTime;
                yield return null;
            }
            _advanceRequested = false;
        }

        private void SetState(DevilState state)
        {
            if (State == state) return;
            State = state;
            StateChanged?.Invoke(state);
        }

        private void Resolve()
        {
            _targets = DealTargets.FromPlayer(_playerHealth);
            if (_playerHealth == null) return;
            if (_modules == null) _modules = _playerHealth.GetComponentInChildren<PlayerModules>(true);
            if (_defne == null) _defne = _playerHealth.GetComponent<DefneCostVisuals>();
        }

        private void Subscribe(bool on)
        {
            if (_playerHealth == null || on == _subscribed) return;
            _subscribed = on;
            if (on) _playerHealth.Died += _onDied;
            else _playerHealth.Died -= _onDied;
        }
    }
}
