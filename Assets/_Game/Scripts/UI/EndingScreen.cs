using System;
using System.Text;
using DG.Tweening;
using DG.Tweening.Core;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace DanceWithTheDevil
{
    /// <summary>
    /// Ending + results (MAIN_GDD §11): ending text, a large block portrait of Defne in the state the run left her
    /// (HAYIR at death 1: full colour; HAYIR later: hair grey, face parts gone, darker frame, by the contracts taken;
    /// always-yes: a grey shell that crumbles block by block), the "Aldıkların / Verdiklerin" ledger ("Defne'den"
    /// over Verdiklerin; the fixed empty-ledger line for HAYIR at death 1), "Küçük hayırların: N" (K-9), the small
    /// first-GDD score at the bottom and "Tekrar dene" (click, R, gamepad Start). Display only: raises
    /// <see cref="RestartPressed"/>. Unscaled time. Entrance (DOTween): title, portrait and ledger pop in, the ledger
    /// lines appear one by one (taken and given side by side), then the closing lines and the button; ReduceMotion =
    /// everything at once. Input is unaffected by the animation.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class EndingScreen : MonoBehaviour
    {
        [Tooltip("Font. Empty = built-in LegacyRuntime.")]
        [SerializeField] private Font _font;
        [Tooltip("Overlay canvas sorting order (above the Devil).")]
        [SerializeField] private int _sortingOrder = 200;
        [Tooltip("Input ignored for this long after showing, seconds (unscaled).")]
        [SerializeField, Min(0f)] private float _inputLock = 0.6f;

        private RectTransform _root;
        private Text _title, _body, _closing, _takenList, _givenList, _ledgerEmpty, _smallNos, _score;
        private GameObject _ledgerColumns;
        private Image _portraitBorder;
        private Image _portraitBack;
        private RectTransform _portraitHolder;
        private DefnePortrait _portraitView;
        private float _crumbleWait = -1f;
        private RectTransform _portraitRect, _ledgerRect, _retryRect;
        private float _retryScale = 1f;
        private readonly string[] _takenSteps = new string[8];
        private readonly string[] _givenSteps = new string[8];
        private int _ledgerLines;
        private int _ledgerShown;
        private float _ledgerProgress;
        private Tween _ledgerTween;
        private DOGetter<float> _ledgerGetter;
        private DOSetter<float> _ledgerSetter;
        private const string Hidden = "<color=#00000000>";
        private const string HiddenEnd = "</color>";
        private float _shownTime;
        private bool _visible;

        public event Action RestartPressed;
        public bool IsVisible => _visible;

        private void Awake() => Build();

        private void Start() => UiKit.EnsureEventSystem();

        public void Show(in RunSummary summary)
        {
            _title.text = summary.Ending switch
            {
                EndingKind.KeptYourself => GameTexts.KeptTitle,
                EndingKind.DiminishedButStanding => GameTexts.DiminishedTitle,
                _ => GameTexts.AlwaysYesTitle,
            };

            var sb = new StringBuilder(128);
            switch (summary.Ending)
            {
                case EndingKind.KeptYourself:
                    _body.text = GameTexts.KeptBody;
                    _closing.text = GameTexts.KeptClosing;
                    break;
                case EndingKind.DiminishedButStanding:
                    sb.AppendFormat(GameTexts.DiminishedYesCountFormat, summary.YesCount).Append(' ')
                        .Append(GameTexts.DiminishedGone).Append(' ');
                    for (int i = 0; i < summary.YesCount; i++)
                    {
                        if (i > 0) sb.Append(GameTexts.Separator);
                        sb.Append(summary.Taken[i].LedgerGivenName);
                    }
                    _body.text = sb.ToString();
                    _closing.text = GameTexts.DiminishedClosing;
                    break;
                default:
                    _body.text = "";
                    _closing.text = "";
                    break;
            }

            _ledgerLines = 0;
            bool empty = summary.YesCount == 0;
            _ledgerEmpty.gameObject.SetActive(empty);
            _ledgerColumns.SetActive(!empty);
            if (!empty)
            {
                var taken = new StringBuilder(128);
                var given = new StringBuilder(128);
                for (int i = 0; i < summary.YesCount; i++)
                {
                    DealDefinition deal = summary.Taken[i];
                    if (deal == null) continue;
                    taken.Append(deal.LedgerTaken).Append('\n');
                    given.Append(deal.LedgerGiven).Append('\n');
                }
                _takenList.text = taken.ToString();
                _givenList.text = given.ToString();
                PrepareLedgerSteps(_takenList.text, _givenList.text);
            }

            _smallNos.text = string.Format(GameTexts.SmallNosFormat, summary.SmallNoCount);
            _score.text = string.Format(GameTexts.ScoreFormat, summary.Score);
            BuildPortrait(summary);

            _root.gameObject.SetActive(true);
            _visible = true;
            _shownTime = 0f;
            PlayEntrance();
        }

        public void Hide()
        {
            _ledgerTween?.Kill();
            _ledgerTween = null;
            _visible = false;
            _root.gameObject.SetActive(false);
        }

        /// <summary>Defne as the run left her (GDD §11: full colour when HAYIR came at the first death).</summary>
        private void BuildPortrait(in RunSummary summary)
        {
            _portraitHolder.gameObject.SetActive(true);
            for (int c = _portraitHolder.childCount - 1; c >= 0; c--) Destroy(_portraitHolder.GetChild(c).gameObject);
            _portraitView = new DefnePortrait(_portraitHolder, 3f);
            _crumbleWait = -1f;
            switch (summary.Ending)
            {
                case EndingKind.KeptYourself:
                    _portraitView.ApplyCosts(0);
                    _portraitBorder.color = UiKit.Cream;
                    _portraitBack.color = new Color(0.12f, 0.24f, 0.28f, 1f);
                    break;
                case EndingKind.DiminishedButStanding:
                    _portraitView.ApplyCosts(summary.YesCount);
                    bool darkened = summary.YesCount >= 5; // contract 5 "Zamanın": the edges went dark
                    _portraitBorder.color = darkened ? new Color(0.22f, 0.2f, 0.23f, 1f) : UiKit.Dim;
                    _portraitBack.color = darkened ? new Color(0.04f, 0.06f, 0.07f, 1f) : new Color(0.09f, 0.15f, 0.17f, 1f);
                    break;
                default:
                    _portraitView.SetAllGrey();
                    _portraitBorder.color = UiKit.Dim;
                    _portraitBack.color = new Color(0.1f, 0.1f, 0.11f, 1f);
                    _crumbleWait = 0.6f; // the grey shell holds a beat, then falls apart
                    break;
            }
        }

        // ------------------------------------------------------------ entrance (DOTween, unscaled)

        /// <summary>
        /// Step k of the ledger: the first k lines shown, the rest transparent (the layout never jumps). Strings are
        /// built once per Show, never per frame.
        /// </summary>
        private void PrepareLedgerSteps(string taken, string given)
        {
            string[] t = taken.TrimEnd('\n').Split('\n');
            string[] g = given.TrimEnd('\n').Split('\n');
            int n = Mathf.Min(Mathf.Max(t.Length, g.Length), _takenSteps.Length - 1);
            for (int k = 0; k <= n; k++)
            {
                _takenSteps[k] = Step(t, k);
                _givenSteps[k] = Step(g, k);
            }
            _ledgerLines = n;
        }

        private static string Step(string[] lines, int shown)
        {
            var sb = new StringBuilder(128);
            for (int i = 0; i < lines.Length; i++)
            {
                if (i == shown) sb.Append(Hidden);
                sb.Append(lines[i]).Append('\n');
            }
            if (shown < lines.Length) sb.Append(HiddenEnd);
            return sb.ToString();
        }

        private void SetLedgerProgress(float v)
        {
            _ledgerProgress = v;
            int k = Mathf.Clamp(Mathf.FloorToInt(v + 0.001f), 0, _ledgerLines);
            if (k == _ledgerShown) return;
            _ledgerShown = k;
            _takenList.text = _takenSteps[k];
            _givenList.text = _givenSteps[k];
        }

        private void PlayEntrance()
        {
            _ledgerTween?.Kill();
            _ledgerTween = null;
            UiKit.PopIn(_title.rectTransform, 0f, 0.8f, 0.3f);
            UiKit.PopIn(_body.rectTransform, 0.08f, 0.95f, 0.25f);
            UiKit.PopIn(_portraitRect, 0.12f, 0.85f, 0.25f);
            UiKit.PopIn(_ledgerRect, 0.2f, 0.9f, 0.25f);
            float linesEnd = 0.45f;
            if (_ledgerLines > 0)
            {
                _ledgerShown = -1;
                if (UiKit.ReduceMotion)
                {
                    SetLedgerProgress(_ledgerLines);
                }
                else
                {
                    SetLedgerProgress(0f);
                    float per = 0.22f;
                    _ledgerTween = DOTween.To(_ledgerGetter, _ledgerSetter, _ledgerLines, per * _ledgerLines)
                        .SetEase(Ease.Linear).SetDelay(0.45f).SetUpdate(true).SetLink(gameObject);
                    linesEnd += per * _ledgerLines;
                }
            }
            UiKit.PopIn(_closing.rectTransform, linesEnd, 0.95f, 0.25f);
            UiKit.PopIn(_smallNos.rectTransform, linesEnd + 0.1f, 0.95f, 0.25f);
            UiKit.PopIn(_retryRect, linesEnd + 0.2f, 0.85f, 0.25f, _retryScale);
        }

        private void Update()
        {
            if (!_visible) return;
            float dt = Time.unscaledDeltaTime;
            _shownTime += dt;
            TickCrumble(dt);
            if (_shownTime < _inputLock) return;
            Keyboard k = Keyboard.current;
            Gamepad g = Gamepad.current;
            if ((k != null && k.rKey.wasPressedThisFrame) || (g != null && g.startButton.wasPressedThisFrame))
                RaiseRestart();
        }

        private void TickCrumble(float dt)
        {
            if (_portraitView == null) return;
            if (_crumbleWait >= 0f)
            {
                _crumbleWait -= dt;
                if (_crumbleWait >= 0f) return;
                _crumbleWait = -1f;
                if (UiKit.ReduceMotion)
                {
                    _portraitHolder.gameObject.SetActive(false); // no animation: the shell is simply gone
                    return;
                }
                _portraitView.StartCrumble(1.4f);
            }
            _portraitView.TickCrumble(dt);
        }

        private void RaiseRestart()
        {
            if (!_visible || _shownTime < _inputLock) return;
            RestartPressed?.Invoke();
        }

        private void Build()
        {
            Font font = UiKit.ResolveFont(_font);
            _root = UiKit.CreateCanvas(transform, "EndingCanvas", _sortingOrder, true);
            Image back = UiKit.Stretch(_root, "Back").gameObject.AddComponent<Image>();
            back.color = new Color(0.03f, 0.02f, 0.04f, 0.8f);
            back.raycastTarget = true; // blocks clicks to anything below

            Vector2 top = new Vector2(0.5f, 1f);
            _title = UiKit.Label(_root, "Title", font, 24, Color.white, TextAnchor.MiddleCenter, top, top,
                new Vector2(0f, -24f), new Vector2(560f, 30f), FontStyle.Bold);
            UiKit.AddShadow(_title);
            _body = UiKit.Label(_root, "Body", font, 12, UiKit.Cream, TextAnchor.UpperCenter, top, top,
                new Vector2(0f, -60f), new Vector2(520f, 36f));

            // Defne (left of the ledger).
            Image portrait = UiKit.Framed(_root, "Portrait", UiKit.Cream, new Color(0.12f, 0.24f, 0.28f, 1f), top, top,
                new Vector2(-250f, -100f), new Vector2(118f, 118f));
            _portraitBack = portrait;
            _portraitBorder = portrait.transform.parent.GetComponent<Image>();
            _portraitRect = _portraitBorder.rectTransform;
            _portraitHolder = UiKit.Stretch(portrait.transform, "Defne");

            // Ledger.
            Image ledger = UiKit.Framed(_root, "Ledger", UiKit.Cream, UiKit.Panel, top, top, new Vector2(56f, -100f),
                new Vector2(400f, 140f));
            _ledgerRect = (RectTransform)ledger.transform.parent;
            _ledgerGetter = () => _ledgerProgress;
            _ledgerSetter = SetLedgerProgress;
            _ledgerEmpty = UiKit.Label(ledger.transform, "Empty", font, 12, UiKit.Cream, TextAnchor.MiddleCenter,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(380f, 40f));
            _ledgerEmpty.text = GameTexts.LedgerEmpty;

            _ledgerColumns = UiKit.Stretch(ledger.transform, "Columns").gameObject;
            Transform cols = _ledgerColumns.transform;
            Vector2 tl = new Vector2(0f, 1f);
            Vector2 tr = new Vector2(1f, 1f);
            Text takenHeader = UiKit.Label(cols, "TakenHeader", font, 14, UiKit.Gold, TextAnchor.UpperLeft, tl, tl,
                new Vector2(12f, -16f), new Vector2(180f, 18f), FontStyle.Bold);
            takenHeader.text = GameTexts.LedgerTaken;
            _takenList = UiKit.Label(cols, "TakenList", font, 12, Color.white, TextAnchor.UpperLeft, tl, tl,
                new Vector2(12f, -36f), new Vector2(176f, 96f));
            Text note = UiKit.Label(cols, "GivenNote", font, 10, UiKit.Light, TextAnchor.UpperLeft, tr, tr,
                new Vector2(-12f, -3f), new Vector2(190f, 12f));
            note.text = GameTexts.LedgerGivenNote;
            Text givenHeader = UiKit.Label(cols, "GivenHeader", font, 14, UiKit.CostRed,
                TextAnchor.UpperLeft, tr, tr, new Vector2(-12f, -16f), new Vector2(190f, 18f), FontStyle.Bold);
            givenHeader.text = GameTexts.LedgerGiven;
            _givenList = UiKit.Label(cols, "GivenList", font, 12, Color.white, TextAnchor.UpperLeft, tr, tr,
                new Vector2(-12f, -36f), new Vector2(190f, 96f));

            _closing = UiKit.Label(_root, "Closing", font, 14, UiKit.Cream, TextAnchor.MiddleCenter, top, top,
                new Vector2(0f, -248f), new Vector2(520f, 20f));
            _smallNos = UiKit.Label(_root, "SmallNos", font, 12, UiKit.Turquoise, TextAnchor.MiddleCenter, top, top,
                new Vector2(0f, -270f), new Vector2(400f, 16f));

            // Minimal Synty button prefab. Navigation None: R / pad Start stay the only keys (a click must not make
            // Enter restart).
            Button retry = UiKit.PrefabButton(_root, UiKit.ButtonPrefab, GameTexts.Retry + "  " + GameTexts.RetryKeys,
                top, top, new Vector2(0f, -292f), new Vector2(160f, 28f), out _, 14f, "Retry");
            retry.navigation = new Navigation { mode = Navigation.Mode.None };
            _retryRect = (RectTransform)retry.transform;
            _retryScale = _retryRect.localScale.x;
            retry.onClick.AddListener(RaiseRestart);

            Vector2 bottom = new Vector2(0.5f, 0f);
            _score = UiKit.Label(_root, "Score", font, 10, UiKit.Light, TextAnchor.MiddleCenter, bottom, bottom,
                new Vector2(0f, 6f), new Vector2(300f, 12f));

            _root.gameObject.SetActive(false);
        }
    }
}
