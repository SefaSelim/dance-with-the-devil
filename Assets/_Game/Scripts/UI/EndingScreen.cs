using System;
using System.Text;
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
    /// <see cref="RestartPressed"/>. Unscaled time.
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
            }

            _smallNos.text = string.Format(GameTexts.SmallNosFormat, summary.SmallNoCount);
            _score.text = string.Format(GameTexts.ScoreFormat, summary.Score);
            BuildPortrait(summary);

            _root.gameObject.SetActive(true);
            _visible = true;
            _shownTime = 0f;
        }

        public void Hide()
        {
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
            _portraitHolder = UiKit.Stretch(portrait.transform, "Defne");

            // Ledger.
            Image ledger = UiKit.Framed(_root, "Ledger", UiKit.Cream, UiKit.Panel, top, top, new Vector2(56f, -100f),
                new Vector2(400f, 140f));
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

            Image retry = UiKit.Box(_root, "Retry", UiKit.Cream, top, top, new Vector2(0f, -292f), new Vector2(160f, 28f));
            UiKit.MakeButton(retry, new Color(1f, 0.85f, 0.6f, 1f)).onClick.AddListener(RaiseRestart);
            Text retryLabel = UiKit.Label(retry.transform, "Label", font, 14, UiKit.Ink, TextAnchor.MiddleCenter,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(156f, 26f), FontStyle.Bold);
            retryLabel.text = GameTexts.Retry + "  " + GameTexts.RetryKeys;

            Vector2 bottom = new Vector2(0.5f, 0f);
            _score = UiKit.Label(_root, "Score", font, 10, UiKit.Light, TextAnchor.MiddleCenter, bottom, bottom,
                new Vector2(0f, 6f), new Vector2(300f, 12f));

            _root.gameObject.SetActive(false);
        }
    }
}
