using System;
using DG.Tweening;
using DG.Tweening.Core;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace DanceWithTheDevil
{
    /// <summary>
    /// The Devil's overlay (MAIN_GDD §8): the Devil leans in from the RIGHT edge of the game image, sways while he
    /// waits, blinks, and his mouth moves while his line types out in a speech bubble next to his head. The contract
    /// card has a big bright EVET and a small dim HAYIR (K-2), the cost's poetic name plus full effect (K-3) and one
    /// plain line of stakes under each choice (EVET: you come back, the cost stays; HAYIR: the run ends).
    /// Display only: it raises <see cref="YesPressed"/>, <see cref="NoPressed"/>, <see cref="AdvancePressed"/> and
    /// <see cref="DevilDirector"/> decides.
    /// Deliberate inputs only (combat keys never choose or skip): both choices must be HELD for
    /// <see cref="_yesHoldSeconds"/>: EVET = Enter / pad South / the mouse on EVET; HAYIR = H / pad East / the mouse
    /// on HAYIR. A light fill sweeps over the whole button left to right while held and drains back on release
    /// (gold on EVET, pale on HAYIR). Lines = Enter / pad South / a click on the bubble (the first press finishes the
    /// typing). Presses that started before a prompt's input lock are ignored, and the choices fade in during the card
    /// lock. Unscaled time.
    /// Layout: the card sits on the right side, bottom; the Devil stands at the right edge drawn OVER the card's right
    /// part, so the card's text and buttons keep to a left column (<see cref="CardColumnWidth"/>) that wraps before
    /// him. Bottom row of the card: small HAYIR on the left, big EVET on the right (static), a stakes line under each.
    /// Art: the serialized sprite, else the theme's hand-drawn Devil (UiTheme.DevilSprite, Dance/UI/Build Devil Art),
    /// else blocks. The drawn Devil is sized to the image aspect and fills the right side. Motion (DOTween, unscaled):
    /// lean-in, idle sway, card and EVET pop-in, bubble pop. ReduceMotion skips them.
    /// No monitor frame is drawn (open design question).
    /// TODO(Burak): should right click (the in-combat "no") also mean HAYIR on the card (K-2)? Not decided.
    /// TODO(Burak): monitor frame around the game image for the Devil to lean over (GDD §3, §13)? Not decided.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class DevilScreen : MonoBehaviour
    {
        [Tooltip("Devil art. Empty = placeholder built from coloured blocks.")]
        [SerializeField] private Sprite _devilSprite;
        [Tooltip("Font. Empty = built-in LegacyRuntime.")]
        [SerializeField] private Font _font;
        [Tooltip("Overlay canvas sorting order (above the HUD).")]
        [SerializeField] private int _sortingOrder = 100;
        [Tooltip("Devil slide-in time, seconds (unscaled).")]
        [SerializeField, Min(0f)] private float _slideDuration = 0.4f;
        [Tooltip("Black overlay alpha when fully in.")]
        [SerializeField, Range(0f, 1f)] private float _dimAlpha = 0.25f;

        [Header("Input (unscaled seconds)")]
        [Tooltip("Lines: presses are ignored for this long after a line appears.")]
        [SerializeField, Min(0f)] private float _lineInputLock = 0.5f;
        [Tooltip("Card: presses are ignored for this long after the card appears; the choices fade in meanwhile.")]
        [SerializeField, Min(0f)] private float _cardInputLock = 0.9f;
        [Tooltip("EVET and HAYIR: how long the key / pad button / the mouse on the button must be held.")]
        [SerializeField, Min(0.05f)] private float _yesHoldSeconds = 0.6f;

        [Header("Devil animation")]
        [Tooltip("Typewriter speed, characters per second (0 = whole line at once).")]
        [SerializeField, Min(0f)] private float _charsPerSecond = 45f;
        [Tooltip("Lean-in angle once he is in, degrees.")]
        [SerializeField, Range(0f, 20f)] private float _leanAngle = 9f;
        [Tooltip("Idle sway amplitude, degrees.")]
        [SerializeField, Range(0f, 6f)] private float _swayDegrees = 2f;
        [Tooltip("Idle bob amplitude, canvas units (texels).")]
        [SerializeField, Range(0f, 6f)] private float _bobUnits = 2f;

        [Header("Drawn Devil")]
        [Tooltip("Height of the drawn Devil, canvas units (the game image is 360 high; a little is cut at the bottom).")]
        [SerializeField, Min(50f)] private float _artHeight = 400f;
        [Tooltip("Lean-in angle for the drawn Devil (the block Devil uses Lean Angle), degrees.")]
        [SerializeField, Range(0f, 10f)] private float _artLeanAngle = 3f;

        // Block Devil (no art): size, rest position (partly past the right edge).
        private static readonly Vector2 BlockDevilSize = new Vector2(210f, 330f);
        private static readonly Vector2 BlockDevilIn = new Vector2(50f, -20f);
        // Drawn Devil: his right edge a little past the frame, feet below it.
        private const float ArtRightOverhang = 20f;
        private const float ArtBottom = -30f;
        // Card: bottom-right of the 640×360 frame (x 116..616, y 6..206). The drawn Devil's figure starts near x 463,
        // so the text and buttons keep to a left column ending near x 454 (CardPad + CardColumnWidth inside the plate).
        private static readonly Vector2 CardSize = new Vector2(500f, 200f);
        private static readonly Vector2 CardCentre = new Vector2(-24f - 250f, 6f + 100f); // from the bottom-right corner
        private const float CardPad = 8f;
        private const float CardColumnWidth = 330f;
        private const float CardLineGap = 3f;
        // Bottom row: HAYIR small on the left, EVET big on the right of the column; stakes lines under them.
        private static readonly Vector2 YesSize = new Vector2(140f, 34f);
        private static readonly Vector2 NoSize = new Vector2(100f, 28f);
        private const float ButtonRowY = 17f;
        private const float StakesY = 4f;
        private static readonly Color YesColor = new Color(0.95f, 0.22f, 0.15f, 1f);
        private static readonly Color YesHover = new Color(1f, 0.42f, 0.3f, 1f);
        // HAYIR is a text button: dim label, brighter on hover / hold.
        private static readonly Color NoColor = UiKit.Light;
        private static readonly Color NoHover = Color.white;
        // Hold fills over the whole button (semi-transparent, so the label still reads through).
        private static readonly Color YesFillColor = new Color(1f, 0.84f, 0.32f, 0.5f);
        private static readonly Color NoFillColor = new Color(0.9f, 0.9f, 0.93f, 0.4f);
        private const float HoldDrainRate = 4f;
        private static Sprite _fillSprite;
        private const string TransparentOpen = "<color=#00000000>";
        private const string TransparentClose = "</color>";

        private RectTransform _root;
        private Image _dim;
        private RectTransform _devil;
        private RectTransform _yesRect;
        private float _yesScale = 1f;
        private RectTransform _cardRect;
        private RectTransform _speechRect;
        private Vector2 _devilIn, _devilOut;
        private float _lean;
        private float _enter;
        private Tween _enterTween;
        private DOGetter<float> _enterGetter;
        private DOSetter<float> _enterSetter;
        private RectTransform _eyeL, _eyeR, _mouth;
        private GameObject _speech;
        private Text _speechText;
        private Text _continueText;
        private UiHoldButton _bubbleButton;
        private GameObject _card;
        private Text _giveText;
        private Text _costTitle;
        private Text _takeText;
        private GameObject _choices;
        private CanvasGroup _choicesGroup;
        private Image[] _yesPlates;
        private TMP_Text _noLabel;
        private UiHoldButton _yesButton, _noButton;
        private Image _yesFill, _noFill;

        private enum Mode { Hidden, Entering, Line, Card }
        private Mode _mode = Mode.Hidden;
        private bool _cardHasChoice;
        private float _promptShownAt;
        private float _clock;
        private float _idleWeight;
        private float _yesProgress, _noProgress;
        private bool _keyHoldArmed, _noKeyHoldArmed;
        private int _bubblePressSeen;

        // Typewriter (prefix strings are built once per line, never per frame).
        private string _line = "";
        private string[] _steps = new string[0];
        private int _stepCount;
        private int _shownStep = -1;
        private float _typed;
        private bool _typing;

        // Blink and mouth.
        private float _blinkTimer = 2f;
        private float _blinkLeft;
        private float _shownEyeHeight = -1f;
        private float _shownMouthHeight = -1f;

        public event Action YesPressed;
        public event Action NoPressed;
        public event Action AdvancePressed;

        public float EnterDuration => UiKit.ReduceMotion ? 0f : _slideDuration;
        public bool IsVisible => _mode != Mode.Hidden;
        /// <summary>True while the Devil's current line is still typing out.</summary>
        public bool IsTyping => _typing;
        /// <summary>0..1 progress of the EVET hold.</summary>
        public float YesHoldProgress => _yesProgress;
        /// <summary>0..1 progress of the HAYIR hold.</summary>
        public float NoHoldProgress => _noProgress;

        private void Awake()
        {
            _enterGetter = () => _enter;
            _enterSetter = v => _enter = v;
            Build();
        }

        private void OnDisable()
        {
            _enterTween?.Kill();
            _enterTween = null;
        }

        private void Start() => UiKit.EnsureEventSystem();

        /// <summary>Overlay fades in and the Devil leans in from the right edge.</summary>
        public void Enter()
        {
            _root.gameObject.SetActive(true);
            _speech.SetActive(false);
            _card.SetActive(false);
            _idleWeight = 0f;
            _mode = Mode.Entering;
            _promptShownAt = Time.unscaledTime;
            _typing = false;
            _enterTween?.Kill();
            _enterTween = null;
            if (EnterDuration <= 0f)
            {
                _enter = 1f;
            }
            else
            {
                _enter = 0f;
                _enterTween = DOTween.To(_enterGetter, _enterSetter, 1f, EnterDuration).SetEase(Ease.OutCubic)
                    .SetUpdate(true).SetLink(gameObject);
            }
            ApplyPose();
        }

        /// <summary>Ends the lean-in at once (a line or the card arrived).</summary>
        private void FinishEnter()
        {
            _enterTween?.Kill();
            _enterTween = null;
            _enter = 1f;
        }

        /// <summary>The speech bubble pops when it appears (not again while it stays up).</summary>
        private void ShowSpeech()
        {
            if (_speech.activeSelf) return;
            _speech.SetActive(true);
            UiKit.PopIn(_speechRect, 0f, 0.9f, 0.18f);
        }

        public void ShowLine(string line)
        {
            if (_mode == Mode.Hidden) Enter();
            FinishEnter();
            ShowSpeech();
            _card.SetActive(false);
            _mode = Mode.Line;
            StartTyping(line);
            ResetPrompt();
        }

        public void ShowCard(DealDefinition deal, bool offersChoice)
        {
            if (_mode == Mode.Hidden) Enter();
            FinishEnter();
            ShowSpeech();
            _giveText.text = "<color=#FFD24D><b>" + GameTexts.CardGive + "</b></color> " + deal.UpgradeText;
            _costTitle.text = GameTexts.CardTake + " " + deal.CostName;
            _takeText.text = GameTexts.CardCostVisible + " " + deal.CostVisibleText + "\n"
                + GameTexts.CardCostMechanical + " " + deal.CostMechanicalText;
            StackCardText();
            _choices.SetActive(offersChoice);
            bool wasShown = _card.activeSelf;
            _card.SetActive(true);
            if (!wasShown)
            {
                UiKit.PopIn(_cardRect, 0f, 0.82f, 0.28f);
                if (offersChoice) UiKit.PopIn(_yesRect, 0.25f, 0.6f, 0.3f, _yesScale);
            }
            _cardHasChoice = offersChoice;
            _mode = Mode.Card;
            StartTyping(deal.OfferLine);
            ResetPrompt();
            _choicesGroup.alpha = UiKit.ReduceMotion ? 1f : 0.25f;
        }

        public void Hide()
        {
            _mode = Mode.Hidden;
            _typing = false;
            _enterTween?.Kill();
            _enterTween = null;
            _speech.SetActive(false);
            _card.SetActive(false);
            _root.gameObject.SetActive(false);
        }

        private void ResetPrompt()
        {
            _promptShownAt = Time.unscaledTime;
            _yesProgress = 0f;
            _noProgress = 0f;
            _keyHoldArmed = false;
            _noKeyHoldArmed = false;
            ApplyHoldProgress();
            // Presses already counted belong to the previous prompt.
            _bubblePressSeen = _bubbleButton.PressCount;
        }

        private void Update()
        {
            if (_mode == Mode.Hidden) return;
            float dt = Time.unscaledDeltaTime;
            _clock += dt;

            if (_mode != Mode.Entering)
                _idleWeight = Mathf.MoveTowards(_idleWeight, UiKit.ReduceMotion ? 0f : 1f, dt * 2f);
            ApplyPose();
            TickTyping(dt);
            AnimateFace(dt);

            if (_mode == Mode.Line) HandleLineInput(_lineInputLock);
            else if (_mode == Mode.Card)
            {
                if (_cardHasChoice) HandleChoiceInput(dt);
                else HandleLineInput(_cardInputLock);
            }
        }

        // ------------------------------------------------------------ input

        private bool Unlocked(float lockSeconds) => Time.unscaledTime - _promptShownAt >= lockSeconds;

        /// <summary>True once per new press on <paramref name="button"/> that started after the lock.</summary>
        private bool ConsumeClick(UiHoldButton button, ref int seen, float lockSeconds)
        {
            if (button.PressCount == seen) return false;
            seen = button.PressCount;
            return button.PressedAt >= _promptShownAt + lockSeconds;
        }

        private void HandleLineInput(float lockSeconds)
        {
            bool clicked = ConsumeClick(_bubbleButton, ref _bubblePressSeen, lockSeconds);
            bool key = Unlocked(lockSeconds) && UiKit.AdvancePressed();
            if (!clicked && !key) return;
            if (_typing)
            {
                FinishTyping();
                return;
            }
            _promptShownAt = Time.unscaledTime;
            UiKit.NotifyClick();
            AdvancePressed?.Invoke();
        }

        private void HandleChoiceInput(float dt)
        {
            float since = Time.unscaledTime - _promptShownAt;
            bool unlocked = since >= _cardInputLock;
            float alpha = unlocked || UiKit.ReduceMotion || _cardInputLock <= 0f
                ? 1f
                : Mathf.Lerp(0.25f, 1f, since / _cardInputLock);
            if (!Mathf.Approximately(_choicesGroup.alpha, alpha)) _choicesGroup.alpha = alpha;

            // The bubble click only finishes the typing here; it never chooses.
            if (ConsumeClick(_bubbleButton, ref _bubblePressSeen, 0f) && _typing) FinishTyping();

            // EVET: Enter / pad South, a fresh press after the lock, then held; or the mouse held on EVET.
            if (unlocked && UiKit.ConfirmPressed()) _keyHoldArmed = true;
            if (!UiKit.ConfirmHeld()) _keyHoldArmed = false;
            bool yesMouse = _yesButton.IsHeld && _yesButton.PressedAt >= _promptShownAt + _cardInputLock;
            bool holdingYes = unlocked && (_keyHoldArmed || yesMouse);

            // HAYIR: H / pad East, a fresh press after the lock, then held; or the mouse held on HAYIR.
            Keyboard k = Keyboard.current;
            Gamepad g = Gamepad.current;
            if (unlocked && ((k != null && k.hKey.wasPressedThisFrame) || (g != null && g.buttonEast.wasPressedThisFrame)))
                _noKeyHoldArmed = true;
            if (!((k != null && k.hKey.isPressed) || (g != null && g.buttonEast.isPressed))) _noKeyHoldArmed = false;
            bool noMouse = _noButton.IsHeld && _noButton.PressedAt >= _promptShownAt + _cardInputLock;
            bool holdingNo = unlocked && (_noKeyHoldArmed || noMouse);

            _yesProgress = StepHold(_yesProgress, holdingYes, dt);
            _noProgress = StepHold(_noProgress, holdingNo, dt);
            ApplyHoldProgress();

            Color yesTint = _yesButton.IsHovered || holdingYes ? YesHover : YesColor;
            for (int i = 0; i < _yesPlates.Length; i++) SetColor(_yesPlates[i], yesTint);
            if (_noLabel != null) SetColor(_noLabel, _noButton.IsHovered || holdingNo ? NoHover : NoColor);

            if (_yesProgress >= 1f)
            {
                ClearHolds();
                UiKit.NotifyClick();
                RaiseYes();
            }
            else if (_noProgress >= 1f)
            {
                ClearHolds();
                UiKit.NotifyClick();
                RaiseNo();
            }
        }

        private float StepHold(float progress, bool holding, float dt) => holding
            ? progress + dt / _yesHoldSeconds
            : Mathf.Max(0f, progress - dt * HoldDrainRate);

        private void ClearHolds()
        {
            _yesProgress = 0f;
            _noProgress = 0f;
            _keyHoldArmed = false;
            _noKeyHoldArmed = false;
            ApplyHoldProgress();
        }

        private void RaiseYes()
        {
            if (_mode != Mode.Card || !_cardHasChoice) return;
            YesPressed?.Invoke();
        }

        private void RaiseNo()
        {
            if (_mode != Mode.Card || !_cardHasChoice) return;
            NoPressed?.Invoke();
        }

        private void ApplyHoldProgress()
        {
            SetFill(_yesFill, _yesProgress);
            SetFill(_noFill, _noProgress);
        }

        private static void SetFill(Image fill, float progress)
        {
            if (fill == null) return;
            float amount = Mathf.Clamp01(progress);
            if (!Mathf.Approximately(fill.fillAmount, amount)) fill.fillAmount = amount;
            bool show = amount > 0f;
            if (fill.enabled != show) fill.enabled = show;
        }

        /// <summary>
        /// A light overlay over the whole button that fills left to right with the hold (Image.Type.Filled needs a
        /// sprite: a shared one over the built-in white texture). Sits under the label when the label is a direct
        /// child, else on top (semi-transparent, so the label still reads).
        /// </summary>
        private static Image AddHoldFill(RectTransform button, Color color, TMP_Text label)
        {
            if (_fillSprite == null)
            {
                Texture2D white = Texture2D.whiteTexture;
                _fillSprite = Sprite.Create(white, new Rect(0f, 0f, white.width, white.height), new Vector2(0.5f, 0.5f));
                _fillSprite.name = "HoldFillSprite";
            }
            RectTransform rect = UiKit.Stretch(button, "HoldFill");
            rect.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            Image fill = rect.gameObject.AddComponent<Image>();
            fill.sprite = _fillSprite;
            fill.color = color;
            fill.raycastTarget = false;
            fill.type = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Horizontal;
            fill.fillOrigin = (int)Image.OriginHorizontal.Left;
            fill.fillAmount = 0f;
            fill.enabled = false;
            if (label != null && label.transform.parent == button) rect.SetSiblingIndex(label.transform.GetSiblingIndex());
            else rect.SetAsLastSibling();
            return fill;
        }

        private static void SetColor(Graphic graphic, Color color)
        {
            if (graphic.color != color) graphic.color = color;
        }

        // ------------------------------------------------------------ typewriter

        private void StartTyping(string line)
        {
            _line = line ?? "";
            int n = _line.Length;
            if (UiKit.ReduceMotion || _charsPerSecond <= 0f || n == 0)
            {
                _typing = false;
                _speechText.text = _line;
                UpdateContinueHint();
                return;
            }
            if (_steps.Length < n) _steps = new string[Mathf.Max(n, 128)];
            // Step i shows i+1 characters; the rest is transparent so the wrap never jumps while typing.
            for (int i = 0; i < n - 1; i++)
                _steps[i] = _line.Substring(0, i + 1) + TransparentOpen + _line.Substring(i + 1) + TransparentClose;
            _steps[n - 1] = _line;
            _stepCount = n;
            _typed = 0f;
            _shownStep = -1;
            _typing = true;
            _speechText.text = TransparentOpen + _line + TransparentClose;
            UpdateContinueHint();
        }

        private void TickTyping(float dt)
        {
            if (!_typing) return;
            _typed += dt * _charsPerSecond;
            int step = Mathf.Min(_stepCount, Mathf.FloorToInt(_typed)) - 1;
            if (step >= 0 && step != _shownStep)
            {
                _shownStep = step;
                _speechText.text = _steps[step];
            }
            if (step >= _stepCount - 1) StopTyping();
        }

        private void FinishTyping()
        {
            if (!_typing) return;
            _speechText.text = _line;
            StopTyping();
        }

        private void StopTyping()
        {
            _typing = false;
            UpdateContinueHint();
            for (int i = 0; i < _stepCount; i++) _steps[i] = null; // let the strings go
            _stepCount = 0;
        }

        /// <summary>"[Enter] devam" shows once the line has typed out, on lines and on the no-choice card.</summary>
        private void UpdateContinueHint()
        {
            _continueText.enabled = !_typing && (_mode == Mode.Line || (_mode == Mode.Card && !_cardHasChoice));
        }

        // ------------------------------------------------------------ Devil motion

        /// <summary>
        /// Lean-in (<see cref="_enter"/>, tweened 0 to 1) plus the slow idle sway and bob (whole texels, so the blocks
        /// stay crisp) and the dim overlay. The card and its buttons never move with him.
        /// </summary>
        private void ApplyPose()
        {
            float e = _enter;
            float sway = Mathf.Sin(_clock * 1.3f) * _swayDegrees * _idleWeight;
            float bob = Mathf.Round(Mathf.Sin(_clock * 2.1f) * _bobUnits * _idleWeight);
            _devil.anchoredPosition = Vector2.LerpUnclamped(_devilOut, _devilIn, e) + new Vector2(0f, bob);
            _devil.localRotation = Quaternion.Euler(0f, 0f, _lean * e + sway);
            Color c = _dim.color;
            c.a = _dimAlpha * Mathf.Clamp01(e);
            _dim.color = c;
        }

        /// <summary>Blinks every few seconds; the mouth moves while the line types.</summary>
        private void AnimateFace(float dt)
        {
            if (_eyeL == null) return; // sprite Devil: no block face

            float eyeHeight = 10f;
            if (_blinkLeft > 0f)
            {
                _blinkLeft -= dt;
                eyeHeight = 2f;
            }
            else
            {
                _blinkTimer -= dt;
                if (_blinkTimer <= 0f)
                {
                    _blinkLeft = 0.12f;
                    _blinkTimer = UnityEngine.Random.Range(2f, 4.5f);
                }
            }
            if (!Mathf.Approximately(eyeHeight, _shownEyeHeight))
            {
                _shownEyeHeight = eyeHeight;
                _eyeL.sizeDelta = new Vector2(_eyeL.sizeDelta.x, eyeHeight);
                _eyeR.sizeDelta = new Vector2(_eyeR.sizeDelta.x, eyeHeight);
            }

            float mouth = _typing && !UiKit.ReduceMotion
                ? 4f + 2f * Mathf.Round(4f * Mathf.Abs(Mathf.Sin(_clock * 16f)))
                : 5f;
            if (!Mathf.Approximately(mouth, _shownMouthHeight))
            {
                _shownMouthHeight = mouth;
                _mouth.sizeDelta = new Vector2(_mouth.sizeDelta.x, mouth);
            }
        }

        // ------------------------------------------------------------ build

        private void Build()
        {
            Font font = UiKit.ResolveFont(_font);
            _root = UiKit.CreateCanvas(transform, "DevilCanvas", _sortingOrder, true);

            _dim = UiKit.Stretch(_root, "Dim").gameObject.AddComponent<Image>();
            _dim.color = new Color(0f, 0f, 0f, 0f);
            _dim.raycastTarget = false;

            Sprite sprite = _devilSprite != null ? _devilSprite : UiKit.Theme != null ? UiKit.Theme.DevilSprite : null;
            Vector2 size;
            if (sprite != null)
            {
                Rect r = sprite.rect;
                float aspect = r.height > 0f ? r.width / r.height : 0.75f;
                size = new Vector2(Mathf.Round(_artHeight * aspect), _artHeight);
                _devilIn = new Vector2(ArtRightOverhang, ArtBottom);
                _lean = _artLeanAngle;
            }
            else
            {
                size = BlockDevilSize;
                _devilIn = BlockDevilIn;
                _lean = _leanAngle;
            }
            _devilOut = _devilIn + new Vector2(size.x * 0.75f + 40f, 0f);

            _devil = UiKit.Rect(_root, "Devil", new Vector2(1f, 0f), new Vector2(1f, 0f), _devilOut, size);
            if (sprite != null)
            {
                Image art = UiKit.Stretch(_devil, "Art").gameObject.AddComponent<Image>();
                art.sprite = sprite;
                art.preserveAspect = true;
                art.raycastTarget = false;
            }
            else
            {
                BuildPlaceholderDevil(_devil);
            }
            BuildSpeechBubble(font);
            BuildCard(font);
            // Draw order: card, then the Devil over its right part, then the speech bubble on top.
            _devil.SetAsLastSibling();
            _speechRect.SetAsLastSibling();
            _root.gameObject.SetActive(false);
        }

        /// <summary>Speech bubble beside his head, with a tail pointing at his mouth.</summary>
        private void BuildSpeechBubble(Font font)
        {
            Vector2 br = new Vector2(1f, 0f);
            // Left of his head (the drawn Devil's face is near x 0.68, y 0.75 of the art), above the card.
            Image fill = UiKit.Framed(_root, "Speech", UiKit.Cream, new Color(0.04f, 0.03f, 0.05f, 0.95f), br,
                new Vector2(1f, 0.5f), new Vector2(-150f, 268f), new Vector2(300f, 66f), 1f);
            Image border = fill.transform.parent.GetComponent<Image>();
            _speech = border.gameObject;
            _speechRect = border.rectTransform;
            _bubbleButton = UiKit.MakeHoldButton(border);

            // Tail: a cream diamond with the bubble colour inside, on the right edge.
            Vector2 right = new Vector2(1f, 0.5f);
            Image tail = UiKit.Box(border.transform, "Tail", UiKit.Cream, right, new Vector2(0.5f, 0.5f),
                new Vector2(0f, -12f), new Vector2(12f, 12f));
            tail.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 45f);
            Image tailFill = UiKit.Box(border.transform, "TailFill", fill.color, right, new Vector2(0.5f, 0.5f),
                new Vector2(-1f, -12f), new Vector2(10f, 10f));
            tailFill.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 45f);

            Vector2 mid = new Vector2(0.5f, 0.5f);
            _speechText = UiKit.Label(border.transform, "Line", font, 14, Color.white, TextAnchor.MiddleCenter,
                mid, mid, new Vector2(0f, 4f), new Vector2(284f, 50f), FontStyle.Bold);
            UiKit.AddShadow(_speechText);
            _continueText = UiKit.Label(border.transform, "Continue", font, 10, UiKit.Light, TextAnchor.LowerRight,
                br, br, new Vector2(-6f, 3f), new Vector2(120f, 12f));
            _continueText.text = GameTexts.LineContinue;
        }

        /// <summary>
        /// Stacks the card's three text blocks by their real wrapped height, so a long promise that wraps to three lines
        /// pushes the cost lines down instead of being drawn under them (seen in the 2026-10-25 captures).
        /// </summary>
        private void StackCardText()
        {
            float y = -6f;
            y = PlaceBlock(_giveText, y);
            y = PlaceBlock(_costTitle, y);
            PlaceBlock(_takeText, y);
        }

        private static float PlaceBlock(Text text, float top)
        {
            RectTransform rect = text.rectTransform;
            float height = Mathf.Ceil(text.preferredHeight);
            rect.anchoredPosition = new Vector2(rect.anchoredPosition.x, top);
            rect.sizeDelta = new Vector2(rect.sizeDelta.x, height);
            return top - height - CardLineGap;
        }

        private void BuildCard(Font font)
        {
            Vector2 mid = new Vector2(0.5f, 0.5f);
            Vector2 tl = new Vector2(0f, 1f);
            Vector2 bl = Vector2.zero;
            Vector2 br = new Vector2(1f, 0f);
            Vector2 bottom = new Vector2(0.5f, 0f);

            // Right side, bottom; the Devil is drawn over its right part (Build), so everything inside keeps to the
            // left column [CardPad, CardPad + CardColumnWidth] of the plate and wraps before him.
            Image card = UiKit.Framed(_root, "Card", UiKit.DevilRed, UiKit.Panel, br, mid, CardCentre, CardSize);
            _card = card.transform.parent.gameObject;
            _cardRect = (RectTransform)card.transform.parent;
            Transform c = card.transform;

            _giveText = UiKit.Label(c, "Give", font, 12, Color.white, TextAnchor.UpperLeft, tl, tl,
                new Vector2(CardPad, -6f), new Vector2(CardColumnWidth, 32f));
            _costTitle = UiKit.Label(c, "CostName", font, 16, UiKit.CostRed, TextAnchor.UpperLeft, tl, tl,
                new Vector2(CardPad, -40f), new Vector2(CardColumnWidth, 20f), FontStyle.Bold);
            UiKit.AddShadow(_costTitle);
            _takeText = UiKit.Label(c, "Take", font, 12, UiKit.Cream, TextAnchor.UpperLeft, tl, tl,
                new Vector2(CardPad, -62f), new Vector2(CardColumnWidth, 54f));

            RectTransform choices = UiKit.Stretch(c, "Choices");
            _choices = choices.gameObject;
            _choicesGroup = _choices.AddComponent<CanvasGroup>();

            // Bottom row. EVET (right of the column): big and bright (K-2), static, held to sign; a gold fill sweeps
            // over the whole button while held.
            float columnRight = CardPad + CardColumnWidth;
            RectTransform yes = (RectTransform)UiKit.PrefabHoldButton(choices, UiKit.ButtonPrefab,
                GameTexts.Yes + "\n<size=55%><color=#EDE0BD>" + GameTexts.YesKeys + "</color></size>", bl, br,
                new Vector2(columnRight, ButtonRowY), YesSize, out TMP_Text yesLabel, 16f, "Yes").transform;
            _yesRect = yes;
            _yesScale = yes.localScale.x;
            _yesButton = yes.GetComponent<UiHoldButton>();
            if (yesLabel != null) yesLabel.color = Color.white;
            _yesPlates = UiKit.PlateImages(yes.gameObject);
            for (int i = 0; i < _yesPlates.Length; i++) _yesPlates[i].color = YesColor;
            _yesFill = AddHoldFill(yes, YesFillColor, yesLabel);
            Text yesStakes = UiKit.Label(choices, "YesStakes", font, 10, UiKit.Cream, TextAnchor.MiddleCenter, bl,
                bottom, new Vector2(columnRight - YesSize.x * 0.5f, StakesY), new Vector2(150f, 12f));
            yesStakes.text = GameTexts.YesStakes;
            UiKit.AddShadow(yesStakes);

            // HAYIR (left of the column): small and dim, but readable and always selectable (K-2); held like EVET,
            // with a pale fill. Synty small text button prefab.
            RectTransform no = (RectTransform)UiKit.PrefabHoldButton(choices, UiKit.SmallButtonPrefab,
                GameTexts.No + "\n<size=75%>" + GameTexts.NoKeys + "</size>", bl, bl, new Vector2(CardPad, ButtonRowY),
                NoSize, out _noLabel, 11f, "No").transform;
            _noButton = no.GetComponent<UiHoldButton>();
            if (_noLabel != null) _noLabel.color = NoColor;
            _noFill = AddHoldFill(no, NoFillColor, _noLabel);
            Text noStakes = UiKit.Label(choices, "NoStakes", font, 10, UiKit.Light, TextAnchor.MiddleCenter, bl,
                bottom, new Vector2(CardPad + NoSize.x * 0.5f, StakesY), new Vector2(NoSize.x, 12f));
            noStakes.text = GameTexts.NoStakes;
            UiKit.AddShadow(noStakes);
        }

        /// <summary>Placeholder Devil from blocks (team drawing replaces it, GDD §13). Keeps eye/mouth refs to animate.</summary>
        private void BuildPlaceholderDevil(RectTransform root)
        {
            Color body = UiKit.DevilRed;
            Color bodyDark = new Color(0.62f, 0.07f, 0.1f, 1f);
            Color head = new Color(0.86f, 0.16f, 0.16f, 1f);
            Color horn = new Color(0.22f, 0.03f, 0.05f, 1f);
            Color eye = new Color(1f, 0.86f, 0.1f, 1f);
            Vector2 a = Vector2.zero;
            Vector2 c = new Vector2(0.5f, 0.5f);

            UiKit.Box(root, "Body", body, a, c, new Vector2(120f, 100f), new Vector2(150f, 210f));
            UiKit.Box(root, "Lapel", bodyDark, a, c, new Vector2(110f, 160f), new Vector2(18f, 60f));
            UiKit.Box(root, "Arm", bodyDark, a, c, new Vector2(52f, 150f), new Vector2(84f, 24f));
            UiKit.Box(root, "Hand", head, a, c, new Vector2(4f, 154f), new Vector2(26f, 20f));
            UiKit.Box(root, "Claw_Up", horn, a, c, new Vector2(-12f, 161f), new Vector2(8f, 6f));
            UiKit.Box(root, "Claw_Down", horn, a, c, new Vector2(-12f, 148f), new Vector2(8f, 6f));
            UiKit.Box(root, "Neck", bodyDark, a, c, new Vector2(100f, 196f), new Vector2(40f, 14f));
            UiKit.Box(root, "Head", head, a, c, new Vector2(100f, 245f), new Vector2(112f, 96f));
            UiKit.Box(root, "Ear", head, a, c, new Vector2(40f, 250f), new Vector2(12f, 24f));
            UiKit.Box(root, "Horn_L", horn, a, c, new Vector2(64f, 306f), new Vector2(16f, 44f)).rectTransform
                .localRotation = Quaternion.Euler(0f, 0f, 22f);
            UiKit.Box(root, "Horn_R", horn, a, c, new Vector2(136f, 306f), new Vector2(16f, 44f)).rectTransform
                .localRotation = Quaternion.Euler(0f, 0f, -22f);
            UiKit.Box(root, "Brow_L", horn, a, c, new Vector2(78f, 272f), new Vector2(28f, 5f)).rectTransform
                .localRotation = Quaternion.Euler(0f, 0f, -14f);
            UiKit.Box(root, "Brow_R", horn, a, c, new Vector2(122f, 272f), new Vector2(28f, 5f)).rectTransform
                .localRotation = Quaternion.Euler(0f, 0f, 14f);

            _eyeL = UiKit.Box(root, "Eye_L", eye, a, c, new Vector2(78f, 258f), new Vector2(22f, 10f)).rectTransform;
            _eyeR = UiKit.Box(root, "Eye_R", eye, a, c, new Vector2(122f, 258f), new Vector2(22f, 10f)).rectTransform;
            AddPupil(_eyeL);
            AddPupil(_eyeR);

            _mouth = UiKit.Box(root, "Mouth", new Color(0.15f, 0.02f, 0.04f, 1f), a, new Vector2(0.5f, 1f),
                new Vector2(100f, 225f), new Vector2(60f, 5f)).rectTransform;
            UiKit.Box(_mouth, "Teeth", UiKit.Cream, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                Vector2.zero, new Vector2(52f, 2f));
            UiKit.Box(root, "Goatee", horn, a, c, new Vector2(100f, 205f), new Vector2(14f, 10f));
        }

        /// <summary>A pupil looking left (at the card), stretched to the eye's height so blinking closes it too.</summary>
        private static void AddPupil(RectTransform eye)
        {
            Image pupil = UiKit.Box(eye, "Pupil", UiKit.Ink, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(-5f, 0f), new Vector2(6f, 0f));
            RectTransform r = pupil.rectTransform;
            r.anchorMin = new Vector2(0.5f, 0f);
            r.anchorMax = new Vector2(0.5f, 1f);
            r.sizeDelta = new Vector2(6f, -2f);
            r.anchoredPosition = new Vector2(-5f, 0f);
        }
    }
}
