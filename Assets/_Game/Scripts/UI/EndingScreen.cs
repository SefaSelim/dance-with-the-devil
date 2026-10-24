using System;
using System.Text;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace DanceWithTheDevil
{
    /// <summary>
    /// Ending + results (MAIN_GDD §11): ending text, the "Aldıkların / Verdiklerin" ledger ("Defne'den" over
    /// Verdiklerin; the fixed empty-ledger line for HAYIR at death 1), "Küçük hayırların: N" (K-9), the small
    /// first-GDD score at the bottom and "Tekrar dene" (click, R, gamepad Start). Display only: raises
    /// <see cref="RestartPressed"/>.
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

            _root.gameObject.SetActive(true);
            _visible = true;
            _shownTime = 0f;
        }

        public void Hide()
        {
            _visible = false;
            _root.gameObject.SetActive(false);
        }

        private void Update()
        {
            if (!_visible) return;
            _shownTime += Time.unscaledDeltaTime;
            if (_shownTime < _inputLock) return;
            Keyboard k = Keyboard.current;
            Gamepad g = Gamepad.current;
            if ((k != null && k.rKey.wasPressedThisFrame) || (g != null && g.startButton.wasPressedThisFrame))
                RaiseRestart();
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
            back.color = new Color(0.03f, 0.02f, 0.04f, 0.88f);
            back.raycastTarget = true; // blocks clicks to anything below

            Vector2 top = new Vector2(0.5f, 1f);
            _title = UiKit.Label(_root, "Title", font, 22, Color.white, TextAnchor.MiddleCenter, top, top,
                new Vector2(0f, -24f), new Vector2(560f, 30f), FontStyle.Bold);
            UiKit.AddShadow(_title);
            _body = UiKit.Label(_root, "Body", font, 12, UiKit.Cream, TextAnchor.UpperCenter, top, top,
                new Vector2(0f, -60f), new Vector2(520f, 36f));

            // Ledger.
            Image ledger = UiKit.Framed(_root, "Ledger", UiKit.Cream, UiKit.Panel, top, top, new Vector2(0f, -100f),
                new Vector2(480f, 140f));
            _ledgerEmpty = UiKit.Label(ledger.transform, "Empty", font, 12, UiKit.Cream, TextAnchor.MiddleCenter,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(460f, 40f));
            _ledgerEmpty.text = GameTexts.LedgerEmpty;

            _ledgerColumns = UiKit.Stretch(ledger.transform, "Columns").gameObject;
            Transform cols = _ledgerColumns.transform;
            Vector2 tl = new Vector2(0f, 1f);
            Vector2 tr = new Vector2(1f, 1f);
            Text takenHeader = UiKit.Label(cols, "TakenHeader", font, 14, UiKit.Gold, TextAnchor.UpperLeft, tl, tl,
                new Vector2(12f, -16f), new Vector2(220f, 18f), FontStyle.Bold);
            takenHeader.text = GameTexts.LedgerTaken;
            _takenList = UiKit.Label(cols, "TakenList", font, 11, Color.white, TextAnchor.UpperLeft, tl, tl,
                new Vector2(12f, -36f), new Vector2(210f, 96f));
            Text note = UiKit.Label(cols, "GivenNote", font, 9, UiKit.Dim, TextAnchor.UpperLeft, tr, tr,
                new Vector2(-12f, -4f), new Vector2(222f, 12f), FontStyle.Italic);
            note.text = GameTexts.LedgerGivenNote;
            Text givenHeader = UiKit.Label(cols, "GivenHeader", font, 14, new Color(1f, 0.45f, 0.45f, 1f),
                TextAnchor.UpperLeft, tr, tr, new Vector2(-12f, -16f), new Vector2(222f, 18f), FontStyle.Bold);
            givenHeader.text = GameTexts.LedgerGiven;
            _givenList = UiKit.Label(cols, "GivenList", font, 11, Color.white, TextAnchor.UpperLeft, tr, tr,
                new Vector2(-12f, -36f), new Vector2(222f, 96f));

            _closing = UiKit.Label(_root, "Closing", font, 13, UiKit.Cream, TextAnchor.MiddleCenter, top, top,
                new Vector2(0f, -248f), new Vector2(520f, 20f), FontStyle.Italic);
            _smallNos = UiKit.Label(_root, "SmallNos", font, 11, UiKit.Turquoise, TextAnchor.MiddleCenter, top, top,
                new Vector2(0f, -270f), new Vector2(400f, 16f));

            Image retry = UiKit.Box(_root, "Retry", UiKit.Cream, top, top, new Vector2(0f, -292f), new Vector2(160f, 28f));
            UiKit.MakeButton(retry, new Color(1f, 0.85f, 0.6f, 1f)).onClick.AddListener(RaiseRestart);
            Text retryLabel = UiKit.Label(retry.transform, "Label", font, 14, UiKit.Ink, TextAnchor.MiddleCenter,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(156f, 26f), FontStyle.Bold);
            retryLabel.text = GameTexts.Retry + "  " + GameTexts.RetryKeys;

            Vector2 bottom = new Vector2(0.5f, 0f);
            _score = UiKit.Label(_root, "Score", font, 9, UiKit.Dim, TextAnchor.MiddleCenter, bottom, bottom,
                new Vector2(0f, 6f), new Vector2(300f, 12f));

            _root.gameObject.SetActive(false);
        }
    }
}
