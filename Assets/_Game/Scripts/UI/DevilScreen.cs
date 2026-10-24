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
    /// Deliberate inputs only (combat keys never choose or skip): EVET = hold Enter / pad South, or hold the mouse
    /// on EVET, for <see cref="_yesHoldSeconds"/> (a bar fills); HAYIR = H / pad East / a click on HAYIR; lines =
    /// Enter / pad South / a click on the bubble (the first press finishes the typing). Presses that started before a
    /// prompt's input lock are ignored, and the choices fade in during the card lock. Unscaled time.
    /// Art: the serialized sprite, else the theme's hand-drawn Devil (UiTheme.DevilSprite, Dance/UI/Build Devil Art),
    /// else blocks. The drawn Devil is sized to the image aspect and fills the right side; EVET (with its stakes line)
    /// rides on his reaching fingertip (<see cref="_handPoint"/> in the art), following his lean-in and idle sway every
    /// frame; the idle sway calms while the pointer is on EVET. Motion (DOTween, unscaled): lean-in, card and EVET
    /// pop-in, bubble pop. ReduceMotion skips them.
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
        [Tooltip("EVET: how long Enter / pad South / the mouse on EVET must be held.")]
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
        [Tooltip("His reaching fingertip in the art, 0..1 from the bottom-left of the image (EVET sits there).")]
        [SerializeField] private Vector2 _handPoint = new Vector2(0.51f, 0.41f);
        [Tooltip("Height of the drawn Devil, canvas units (the game image is 360 high; a little is cut at the bottom).")]
        [SerializeField, Min(50f)] private float _artHeight = 400f;
        [Tooltip("Lean-in angle for the drawn Devil (the block Devil uses Lean Angle), degrees.")]
        [SerializeField, Range(0f, 10f)] private float _artLeanAngle = 3f;

        // Block Devil (no art): size, rest position (partly past the right edge), hand point (claws, left side).
        private static readonly Vector2 BlockDevilSize = new Vector2(210f, 330f);
        private static readonly Vector2 BlockDevilIn = new Vector2(50f, -20f);
        private static readonly Vector2 BlockHandPoint = new Vector2(0.02f, 0.47f);
        // Drawn Devil: his right edge a little past the frame, feet below it.
        private const float ArtRightOverhang = 20f;
        private const float ArtBottom = -30f;
        // EVET rests on the fingertip: bottom-centre of the button at the tip, 2 units up.
        private static readonly Vector2 YesOnHandOffset = new Vector2(0f, 2f);
        private static readonly Vector2 YesSize = new Vector2(140f, 36f);
        private static readonly Color YesColor = new Color(0.95f, 0.22f, 0.15f, 1f);
        private static readonly Color YesHover = new Color(1f, 0.42f, 0.3f, 1f);
        // HAYIR is a text button: dim label, brighter on hover.
        private static readonly Color NoColor = UiKit.Light;
        private static readonly Color NoHover = Color.white;
        private const string TransparentOpen = "<color=#00000000>";
        private const string TransparentClose = "</color>";

        private RectTransform _root;
        private Image _dim;
        private RectTransform _devil;
        private RectTransform _handAnchor;
        private RectTransform _yesAnchor;
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
        private RectTransform _yesFill;
        private float _yesFillWidth;

        private enum Mode { Hidden, Entering, Line, Card }
        private Mode _mode = Mode.Hidden;
        private bool _cardHasChoice;
        private float _promptShownAt;
        private float _clock;
        private float _idleWeight;
        private float _yesProgress;
        private bool _keyHoldArmed;
        private int _bubblePressSeen, _noPressSeen;

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
            _choices.SetActive(offersChoice);
            bool wasShown = _card.activeSelf;
            _card.SetActive(true);
            if (!wasShown)
            {
                UiKit.PopIn(_cardRect, 0f, 0.82f, 0.28f);
                if (offersChoice) UiKit.PopIn(_yesAnchor, 0.25f, 0.6f, 0.3f);
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
            _keyHoldArmed = false;
            ApplyYesProgress();
            // Presses already counted belong to the previous prompt.
            _bubblePressSeen = _bubbleButton.PressCount;
            _noPressSeen = _noButton.PressCount;
        }

        private void Update()
        {
            if (_mode == Mode.Hidden) return;
            float dt = Time.unscaledDeltaTime;
            _clock += dt;

            if (_mode != Mode.Entering)
            {
                // The sway calms while the pointer is on EVET, so it does not slide out from under the cursor.
                bool onYes = _yesButton != null && _choices.activeInHierarchy && (_yesButton.IsHovered || _yesButton.IsHeld);
                float target = UiKit.ReduceMotion ? 0f : onYes ? 0.2f : 1f;
                _idleWeight = Mathf.MoveTowards(_idleWeight, target, dt * 2f);
            }
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

            // HAYIR: H / pad East / a click on HAYIR.
            Keyboard k = Keyboard.current;
            Gamepad g = Gamepad.current;
            bool noClick = ConsumeClick(_noButton, ref _noPressSeen, _cardInputLock);
            bool noKey = unlocked && ((k != null && k.hKey.wasPressedThisFrame) || (g != null && g.buttonEast.wasPressedThisFrame));
            if (noClick || noKey)
            {
                UiKit.NotifyClick();
                RaiseNo();
                return;
            }

            // EVET: a fresh press after the lock, then held.
            if (unlocked && UiKit.ConfirmPressed()) _keyHoldArmed = true;
            if (!UiKit.ConfirmHeld()) _keyHoldArmed = false;
            bool mouseHold = _yesButton.IsHeld && _yesButton.PressedAt >= _promptShownAt + _cardInputLock;
            bool holding = unlocked && (_keyHoldArmed || mouseHold);
            _yesProgress = holding
                ? _yesProgress + dt / _yesHoldSeconds
                : Mathf.Max(0f, _yesProgress - dt * 4f);
            ApplyYesProgress();

            Color yesTint = _yesButton.IsHovered || holding ? YesHover : YesColor;
            for (int i = 0; i < _yesPlates.Length; i++) SetColor(_yesPlates[i], yesTint);
            if (_noLabel != null) SetColor(_noLabel, _noButton.IsHovered ? NoHover : NoColor);

            if (_yesProgress >= 1f)
            {
                _yesProgress = 0f;
                _keyHoldArmed = false;
                ApplyYesProgress();
                UiKit.NotifyClick();
                RaiseYes();
            }
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

        private void ApplyYesProgress()
        {
            float w = Mathf.Round(Mathf.Clamp01(_yesProgress) * _yesFillWidth);
            if (!Mathf.Approximately(_yesFill.sizeDelta.x, w)) _yesFill.sizeDelta = new Vector2(w, _yesFill.sizeDelta.y);
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
        /// stay crisp), the dim overlay, and EVET placed on his fingertip.
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
            if (_yesAnchor != null && _handAnchor != null) _yesAnchor.position = _handAnchor.position;
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
            Vector2 hand;
            if (sprite != null)
            {
                Rect r = sprite.rect;
                float aspect = r.height > 0f ? r.width / r.height : 0.75f;
                size = new Vector2(Mathf.Round(_artHeight * aspect), _artHeight);
                _devilIn = new Vector2(ArtRightOverhang, ArtBottom);
                _lean = _artLeanAngle;
                hand = _handPoint;
            }
            else
            {
                size = BlockDevilSize;
                _devilIn = BlockDevilIn;
                _lean = _leanAngle;
                hand = BlockHandPoint;
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
            // The fingertip: an empty point in the art's rect, so it follows the lean and sway.
            _handAnchor = UiKit.Rect(_devil, "Fingertip", hand, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);

            BuildSpeechBubble(font);
            BuildCard(font);
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

        private void BuildCard(Font font)
        {
            Vector2 mid = new Vector2(0.5f, 0.5f);
            Vector2 top = new Vector2(0.5f, 1f);
            Vector2 bottom = new Vector2(0.5f, 0f);

            // Left of the Devil so his reaching hand (and EVET on it) stays clear of the text.
            Image card = UiKit.Framed(_root, "Card", UiKit.DevilRed, UiKit.Panel, mid, mid, new Vector2(-100f, -54f),
                new Vector2(384f, 200f));
            _card = card.transform.parent.gameObject;
            _cardRect = (RectTransform)card.transform.parent;
            Transform c = card.transform;

            _giveText = UiKit.Label(c, "Give", font, 12, Color.white, TextAnchor.UpperLeft, top, top,
                new Vector2(0f, -6f), new Vector2(360f, 32f));
            _costTitle = UiKit.Label(c, "CostName", font, 16, UiKit.CostRed, TextAnchor.UpperLeft, top, top,
                new Vector2(0f, -40f), new Vector2(360f, 20f), FontStyle.Bold);
            UiKit.AddShadow(_costTitle);
            _takeText = UiKit.Label(c, "Take", font, 12, UiKit.Cream, TextAnchor.UpperLeft, top, top,
                new Vector2(0f, -62f), new Vector2(360f, 48f));

            RectTransform choices = UiKit.Stretch(c, "Choices");
            _choices = choices.gameObject;
            _choicesGroup = _choices.AddComponent<CanvasGroup>();

            // EVET: big and bright (K-2), hold to sign; a gold bar fills along its bottom. It rides on the Devil's
            // fingertip: an anchor in the card's choices (so it fades and hides with them) placed on the hand every
            // frame (ApplyPose); the stakes line sits just above the button.
            _yesAnchor = UiKit.Rect(choices, "YesOnHand", mid, mid, Vector2.zero, Vector2.zero);
            RectTransform yes = (RectTransform)UiKit.PrefabHoldButton(_yesAnchor, UiKit.ButtonPrefab,
                GameTexts.Yes + "\n<size=55%><color=#EDE0BD>" + GameTexts.YesKeys + "</color></size>", mid, bottom,
                YesOnHandOffset, YesSize, out TMP_Text yesLabel, 16f, "Yes").transform;
            _yesButton = yes.GetComponent<UiHoldButton>();
            if (yesLabel != null) yesLabel.color = Color.white;
            _yesPlates = UiKit.PlateImages(yes.gameObject);
            for (int i = 0; i < _yesPlates.Length; i++) _yesPlates[i].color = YesColor;
            // The fill lives under the (scaled) button root: sizes in its local units, outside its layout group.
            float s = Mathf.Max(0.001f, yes.localScale.x);
            float inset = 2f / s;
            _yesFillWidth = yes.sizeDelta.x - inset * 2f;
            Image yesFill = UiKit.Box(yes, "HoldFill", UiKit.Gold, Vector2.zero, Vector2.zero,
                new Vector2(inset, inset), new Vector2(0f, 4f / s));
            yesFill.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            _yesFill = yesFill.rectTransform;
            Text yesStakes = UiKit.Label(_yesAnchor, "YesStakes", font, 10, UiKit.Cream, TextAnchor.MiddleCenter,
                mid, bottom, YesOnHandOffset + new Vector2(0f, YesSize.y + 2f), new Vector2(220f, 14f));
            yesStakes.text = GameTexts.YesStakes;
            UiKit.AddShadow(yesStakes);

            // HAYIR: small and dim, but readable and always selectable (K-2). Synty small text button prefab.
            _noButton = UiKit.PrefabHoldButton(choices, UiKit.SmallButtonPrefab, GameTexts.No + " " + GameTexts.NoKeys,
                bottom, bottom, new Vector2(0f, 30f), new Vector2(96f, 20f), out _noLabel, 10f, "No");
            if (_noLabel != null) _noLabel.color = NoColor;
            Text noStakes = UiKit.Label(choices, "NoStakes", font, 10, UiKit.Light, TextAnchor.MiddleCenter,
                bottom, bottom, new Vector2(0f, 12f), new Vector2(220f, 14f));
            noStakes.text = GameTexts.NoStakes;
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
