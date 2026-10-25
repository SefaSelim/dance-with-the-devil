using System;
using System.Text;
using DG.Tweening;
using DG.Tweening.Core;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using Random = UnityEngine.Random;

namespace DanceWithTheDevil
{
    /// <summary>
    /// Ending + results (MAIN_GDD §11): ending text, Defne's portrait in the state the run left her, the
    /// "Aldıkların / Verdiklerin" ledger ("Defne'den" over Verdiklerin; the fixed empty-ledger line for HAYIR at death 1),
    /// "Küçük hayırların: N" (K-9), the small first-GDD score at the bottom and "Tekrar dene" (click, R, gamepad Start).
    /// Display only: raises <see cref="RestartPressed"/>. Unscaled time.
    /// <para>Portrait: the drawn Defne (own sprite field, else <see cref="UiTheme.PortraitSprite"/>) with the costs on
    /// top: contract 1 "Rengi" drains half the colour (grey copy over it), contract 4 "Yüzü" veils the face, contract 5
    /// "Zamanı" darkens frame and image, contract 6 "Kendisi" fully grey; always-yes = a grey shell that falls apart tile
    /// by tile. No drawn sprite = the old block portrait (<see cref="DefnePortrait"/>).</para>
    /// <para>Layout: every block is stacked by its real wrapped height (title, body, portrait + ledger side by side,
    /// closing, small nos, retry), the ledger panel is sized to its rows (each row = the taller of its two cells); when
    /// the stack would still reach the score line the row font shrinks, then the whole content scales down.</para>
    /// Entrance (DOTween): title, portrait and ledger pop in, the ledger rows appear one by one, then the closing lines
    /// and the button; ReduceMotion = everything at once. Input is unaffected by the animation.
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
        [Tooltip("Defne's drawn portrait. Empty = UiTheme.PortraitSprite (Dance/UI/Build Devil Art); none = block portrait.")]
        [SerializeField] private Sprite _portraitSprite;
        [Tooltip("Grey copy of the portrait (colour costs). Empty = UiTheme.PortraitGreySprite.")]
        [SerializeField] private Sprite _portraitGreySprite;

        // Layout (640×360 frame, content y measured down from the top).
        private const float ContentWidth = 640f;
        private const float ContentHeight = 360f;
        private const float ScoreReserve = 22f;
        private const float PortraitSize = 104f;
        private const float PortraitX = -308f;
        private const float LedgerX = -196f;
        private const float LedgerWidth = 504f;
        private const float TakenX = 10f;
        private const float TakenWidth = 150f;
        private const float GivenX = 172f;
        private const float RowsTop = -34f;
        private const float RowGap = 3f;
        private const float RetryHeight = 28f;
        private const int MaxRows = 8;
        private const int TileGrid = 8;

        private RectTransform _root, _content;
        private Text _title, _body, _closing, _ledgerEmpty, _smallNos, _score;
        private GameObject _ledgerColumns;
        private Image _portraitBorder;
        private Image _portraitBack;
        private RectTransform _portraitHolder;
        private DefnePortrait _portraitView;
        private float _crumbleWait = -1f;
        private RectTransform _portraitRect, _ledgerRect, _ledgerFill, _retryRect;
        private float _ledgerInsetY;
        private float _retryScale = 1f;

        private readonly RectTransform[] _rows = new RectTransform[MaxRows];
        private readonly Text[] _rowTaken = new Text[MaxRows];
        private readonly Text[] _rowGiven = new Text[MaxRows];
        private int _rowBaseFont;
        private int _rowCount;
        private int _ledgerShown;
        private float _ledgerProgress;
        private Tween _ledgerTween;
        private DOGetter<float> _ledgerGetter;
        private DOSetter<float> _ledgerSetter;

        // Drawn portrait.
        private Sprite _artSprite, _artGreySprite;
        private RectTransform _artRoot, _tileRoot;
        private Image _art, _artGrey, _faceVeil;
        private RectTransform[] _tiles;
        private Image[] _tileImages;
        private Sprite[] _tileSprites;
        private Vector2[] _tileBase, _tileVelocity;
        private float[] _tileDelay, _tileSpin, _tileAngle;
        private float _tileTime;
        private bool _tilesCrumbling;

        private float _shownTime;
        private bool _visible;

        public event Action RestartPressed;
        public bool IsVisible => _visible;

        private void Awake() => Build();

        private void Start() => UiKit.EnsureEventSystem();

        private void OnDestroy()
        {
            if (_tileSprites == null) return;
            for (int i = 0; i < _tileSprites.Length; i++)
                if (_tileSprites[i] != null) Destroy(_tileSprites[i]);
        }

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

            _rowCount = 0;
            bool empty = summary.YesCount == 0;
            _ledgerEmpty.gameObject.SetActive(empty);
            _ledgerColumns.SetActive(!empty);
            if (!empty)
            {
                for (int i = 0; i < summary.YesCount && _rowCount < MaxRows; i++)
                {
                    DealDefinition deal = summary.Taken[i];
                    if (deal == null) continue;
                    _rowTaken[_rowCount].text = deal.LedgerTaken;
                    _rowGiven[_rowCount].text = deal.LedgerGiven;
                    _rowCount++;
                }
            }
            for (int i = 0; i < MaxRows; i++) _rows[i].gameObject.SetActive(i < _rowCount);

            _smallNos.text = string.Format(GameTexts.SmallNosFormat, summary.SmallNoCount);
            _score.text = string.Format(GameTexts.ScoreFormat, summary.Score);

            _root.gameObject.SetActive(true);
            FitLayout();
            BuildPortrait(summary);
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

        // ------------------------------------------------------------ layout

        /// <summary>Stacks everything by its real height; shrinks the row font, then the whole content, to fit.</summary>
        private void FitLayout()
        {
            _content.localScale = Vector3.one;
            float limit = -(ContentHeight - ScoreReserve);
            float bottom = 0f;
            for (int shrink = 0; shrink <= 2; shrink++)
            {
                SetRowFont(Mathf.Max(6, _rowBaseFont - shrink));
                bottom = Layout();
                if (bottom >= limit) return;
            }
            float s = Mathf.Clamp(limit / bottom, 0.6f, 1f);
            _content.localScale = new Vector3(s, s, 1f);
        }

        private void SetRowFont(int size)
        {
            for (int i = 0; i < MaxRows; i++)
            {
                _rowTaken[i].fontSize = size;
                _rowGiven[i].fontSize = size;
            }
        }

        /// <summary>Places every block top to bottom; returns the content bottom (negative, frame units).</summary>
        private float Layout()
        {
            float y = -10f;
            PlaceText(_title, y, 30f);
            y -= 34f;
            if (_body.text.Length > 0)
            {
                float h = Preferred(_body);
                PlaceText(_body, y, h);
                y -= h + 8f;
            }
            float panelsTop = y;
            _portraitRect.anchoredPosition = new Vector2(PortraitX, panelsTop);

            float ledgerHeight = LayoutLedger();
            _ledgerRect.anchoredPosition = new Vector2(LedgerX, panelsTop);
            _ledgerRect.sizeDelta = new Vector2(LedgerWidth, ledgerHeight);
            _ledgerFill.sizeDelta = new Vector2(_ledgerFill.sizeDelta.x, ledgerHeight - _ledgerInsetY);

            y = panelsTop - Mathf.Max(PortraitSize, ledgerHeight) - 10f;
            if (_closing.text.Length > 0)
            {
                float h = Preferred(_closing);
                PlaceText(_closing, y, h);
                y -= h + 4f;
            }
            PlaceText(_smallNos, y, 16f);
            y -= 22f;
            _retryRect.anchoredPosition = new Vector2(0f, y);
            return y - RetryHeight;
        }

        /// <summary>Rows stacked by the taller cell; returns the ledger's outer height.</summary>
        private float LayoutLedger()
        {
            if (_rowCount == 0) return Mathf.Max(56f, Preferred(_ledgerEmpty) + 24f + _ledgerInsetY);
            float y = RowsTop;
            for (int i = 0; i < _rowCount; i++)
            {
                float h = Mathf.Max(Preferred(_rowTaken[i]), Preferred(_rowGiven[i]));
                RectTransform row = _rows[i];
                row.anchoredPosition = new Vector2(0f, y);
                row.sizeDelta = new Vector2(row.sizeDelta.x, h);
                SetHeight(_rowTaken[i].rectTransform, h);
                SetHeight(_rowGiven[i].rectTransform, h);
                y -= h + RowGap;
            }
            return -y + 6f + _ledgerInsetY;
        }

        private static float Preferred(Text text) => Mathf.Ceil(text.preferredHeight);

        private static void PlaceText(Text text, float top, float height)
        {
            RectTransform rect = text.rectTransform;
            rect.anchoredPosition = new Vector2(rect.anchoredPosition.x, top);
            SetHeight(rect, height);
        }

        private static void SetHeight(RectTransform rect, float height) =>
            rect.sizeDelta = new Vector2(rect.sizeDelta.x, height);

        // ------------------------------------------------------------ portrait

        /// <summary>Defne as the run left her (GDD §11: full colour when HAYIR came at the first death).</summary>
        private void BuildPortrait(in RunSummary summary)
        {
            _crumbleWait = -1f;
            _tilesCrumbling = false;
            bool drawn = _artSprite != null;
            _artRoot.gameObject.SetActive(drawn);
            _portraitHolder.gameObject.SetActive(!drawn);
            if (!drawn)
            {
                for (int c = _portraitHolder.childCount - 1; c >= 0; c--) Destroy(_portraitHolder.GetChild(c).gameObject);
                _portraitView = new DefnePortrait(_portraitHolder, 3f);
            }
            else _portraitView = null;

            switch (summary.Ending)
            {
                case EndingKind.KeptYourself:
                    _portraitView?.ApplyCosts(0);
                    ShowArt(0f, false, Color.white, false);
                    _portraitBorder.color = UiKit.Cream;
                    _portraitBack.color = new Color(0.12f, 0.24f, 0.28f, 1f);
                    break;
                case EndingKind.DiminishedButStanding:
                    int n = summary.YesCount;
                    _portraitView?.ApplyCosts(n);
                    bool darkened = n >= 5; // contract 5 "Zamanı": the edges went dark
                    float grey = n >= 6 ? 1f : n >= 1 ? 0.55f : 0f; // 1 "Rengi" drains, 6 "Kendisi" all grey
                    ShowArt(grey, n >= 4, darkened ? new Color(0.72f, 0.72f, 0.76f, 1f) : Color.white, false);
                    _portraitBorder.color = darkened ? new Color(0.22f, 0.2f, 0.23f, 1f) : UiKit.Dim;
                    _portraitBack.color = darkened ? new Color(0.04f, 0.06f, 0.07f, 1f) : new Color(0.09f, 0.15f, 0.17f, 1f);
                    break;
                default:
                    _portraitView?.SetAllGrey();
                    ShowArt(1f, false, Color.white, true);
                    _portraitBorder.color = UiKit.Dim;
                    _portraitBack.color = new Color(0.1f, 0.1f, 0.11f, 1f);
                    _crumbleWait = 0.6f; // the grey shell holds a beat, then falls apart
                    break;
            }
        }

        private void ShowArt(float greyAmount, bool faceVeiled, Color tint, bool asTiles)
        {
            if (_artSprite == null) return;
            bool tiles = asTiles && _tiles != null;
            _art.gameObject.SetActive(!tiles);
            _tileRoot.gameObject.SetActive(tiles);
            _art.color = tint;
            bool hasGrey = _artGrey != null;
            if (hasGrey)
            {
                _artGrey.gameObject.SetActive(!tiles && greyAmount > 0f);
                _artGrey.color = new Color(tint.r, tint.g, tint.b, greyAmount);
            }
            else if (greyAmount > 0f)
            {
                // No grey copy: a cool, dim tint stands in for the drained colour.
                float k = Mathf.Lerp(1f, 0.6f, greyAmount);
                _art.color = new Color(tint.r * k, tint.g * k, tint.b * k * 1.05f, 1f);
            }
            _faceVeil.gameObject.SetActive(faceVeiled && !tiles);
            if (tiles) ResetTiles();
        }

        private void ResetTiles()
        {
            for (int i = 0; i < _tiles.Length; i++)
            {
                _tiles[i].anchoredPosition = _tileBase[i];
                _tiles[i].localRotation = Quaternion.identity;
                _tileImages[i].enabled = true;
                _tileImages[i].color = _artGreySprite != null ? Color.white : new Color(0.55f, 0.55f, 0.58f, 1f);
            }
        }

        // ------------------------------------------------------------ entrance (DOTween, unscaled)

        private void SetLedgerProgress(float v)
        {
            _ledgerProgress = v;
            int k = Mathf.Clamp(Mathf.FloorToInt(v + 0.001f), 0, _rowCount);
            if (k == _ledgerShown) return;
            for (int i = 0; i < _rowCount; i++)
            {
                bool on = i < k;
                bool was = _rows[i].gameObject.activeSelf;
                _rows[i].gameObject.SetActive(on);
                if (on && !was && _ledgerShown >= 0) UiKit.PopIn(_rows[i], 0f, 0.94f, 0.16f);
            }
            _ledgerShown = k;
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
            if (_rowCount > 0)
            {
                _ledgerShown = -1;
                if (UiKit.ReduceMotion)
                {
                    SetLedgerProgress(_rowCount);
                }
                else
                {
                    SetLedgerProgress(0f);
                    float per = 0.22f;
                    _ledgerTween = DOTween.To(_ledgerGetter, _ledgerSetter, _rowCount, per * _rowCount)
                        .SetEase(Ease.Linear).SetDelay(0.45f).SetUpdate(true).SetLink(gameObject);
                    linesEnd += per * _rowCount;
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
            if (_crumbleWait >= 0f)
            {
                _crumbleWait -= dt;
                if (_crumbleWait >= 0f) return;
                _crumbleWait = -1f;
                if (UiKit.ReduceMotion)
                {
                    // No animation: the shell is simply gone.
                    _portraitHolder.gameObject.SetActive(false);
                    _artRoot.gameObject.SetActive(false);
                    return;
                }
                if (_portraitView != null) _portraitView.StartCrumble(1.4f);
                else StartTileCrumble(1.4f);
            }
            if (_portraitView != null) _portraitView.TickCrumble(dt);
            else if (_tilesCrumbling) TickTiles(dt);
        }

        private void StartTileCrumble(float spread)
        {
            if (_tiles == null || !_tileRoot.gameObject.activeSelf)
            {
                _art.gameObject.SetActive(false);
                return;
            }
            _tilesCrumbling = true;
            _tileTime = 0f;
            for (int i = 0; i < _tiles.Length; i++)
            {
                _tileDelay[i] = Random.Range(0f, spread);
                _tileVelocity[i] = new Vector2(Random.Range(-75f, 75f), Random.Range(30f, 135f));
                _tileSpin[i] = Random.Range(-240f, 240f);
                _tileAngle[i] = 0f;
            }
        }

        private void TickTiles(float dt)
        {
            _tileTime += dt;
            const float gravity = 780f;
            bool any = false;
            for (int i = 0; i < _tiles.Length; i++)
            {
                Image img = _tileImages[i];
                if (!img.enabled) continue;
                float t = _tileTime - _tileDelay[i];
                if (t <= 0f)
                {
                    any = true;
                    continue;
                }
                Vector2 v = _tileVelocity[i];
                _tiles[i].anchoredPosition = _tileBase[i] + new Vector2(v.x * t, v.y * t - 0.5f * gravity * t * t);
                _tileAngle[i] += _tileSpin[i] * dt;
                _tiles[i].localRotation = Quaternion.Euler(0f, 0f, _tileAngle[i]);
                Color c = img.color;
                c.a = Mathf.Clamp01(1f - (t - 0.5f) / 0.6f);
                img.color = c;
                if (c.a <= 0f) img.enabled = false;
                else any = true;
            }
            _tilesCrumbling = any;
        }

        private void RaiseRestart()
        {
            if (!_visible || _shownTime < _inputLock) return;
            RestartPressed?.Invoke();
        }

        // ------------------------------------------------------------ build

        private void Build()
        {
            Font font = UiKit.ResolveFont(_font);
            _root = UiKit.CreateCanvas(transform, "EndingCanvas", _sortingOrder, true);
            Image back = UiKit.Stretch(_root, "Back").gameObject.AddComponent<Image>();
            back.color = new Color(0.03f, 0.02f, 0.04f, 0.8f);
            back.raycastTarget = true; // blocks clicks to anything below

            Vector2 top = new Vector2(0.5f, 1f);
            Vector2 tl = new Vector2(0f, 1f);
            // Everything but the score lives in a content rect that can scale down from the top edge.
            _content = UiKit.Rect(_root, "Content", top, top, Vector2.zero, new Vector2(ContentWidth, ContentHeight));

            _title = UiKit.Label(_content, "Title", font, 24, Color.white, TextAnchor.MiddleCenter, top, top,
                new Vector2(0f, -10f), new Vector2(600f, 30f), FontStyle.Bold);
            UiKit.AddShadow(_title);
            _body = UiKit.Label(_content, "Body", font, 12, UiKit.Cream, TextAnchor.UpperCenter, top, top,
                new Vector2(0f, -44f), new Vector2(560f, 16f));

            // Defne (left of the ledger).
            Image portrait = UiKit.Framed(_content, "Portrait", UiKit.Cream, new Color(0.12f, 0.24f, 0.28f, 1f), top, tl,
                new Vector2(PortraitX, -80f), new Vector2(PortraitSize, PortraitSize));
            _portraitBack = portrait;
            _portraitBorder = portrait.transform.parent.GetComponent<Image>();
            _portraitRect = _portraitBorder.rectTransform;
            _portraitHolder = UiKit.Stretch(portrait.transform, "Defne");
            BuildDrawnPortrait(portrait.transform);

            // Ledger (height set per Show by Layout).
            Image ledger = UiKit.Framed(_content, "Ledger", UiKit.Cream, UiKit.Panel, top, tl, new Vector2(LedgerX, -80f),
                new Vector2(LedgerWidth, 140f));
            _ledgerRect = (RectTransform)ledger.transform.parent;
            _ledgerFill = ledger.rectTransform;
            _ledgerInsetY = _ledgerRect.sizeDelta.y - _ledgerFill.sizeDelta.y;
            float fillWidth = _ledgerFill.sizeDelta.x;
            float givenWidth = fillWidth - GivenX - 10f;
            _ledgerGetter = () => _ledgerProgress;
            _ledgerSetter = SetLedgerProgress;
            _ledgerEmpty = UiKit.Label(ledger.transform, "Empty", font, 12, UiKit.Cream, TextAnchor.MiddleCenter,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(fillWidth - 24f, 40f));
            _ledgerEmpty.text = GameTexts.LedgerEmpty;

            _ledgerColumns = UiKit.Stretch(ledger.transform, "Columns").gameObject;
            Transform cols = _ledgerColumns.transform;
            Text takenHeader = UiKit.Label(cols, "TakenHeader", font, 14, UiKit.Gold, TextAnchor.UpperLeft, tl, tl,
                new Vector2(TakenX, -14f), new Vector2(TakenWidth, 18f), FontStyle.Bold);
            takenHeader.text = GameTexts.LedgerTaken;
            Text note = UiKit.Label(cols, "GivenNote", font, 10, UiKit.Light, TextAnchor.UpperLeft, tl, tl,
                new Vector2(GivenX, -3f), new Vector2(givenWidth, 12f));
            note.text = GameTexts.LedgerGivenNote;
            Text givenHeader = UiKit.Label(cols, "GivenHeader", font, 14, UiKit.CostRed, TextAnchor.UpperLeft, tl, tl,
                new Vector2(GivenX, -14f), new Vector2(givenWidth, 18f), FontStyle.Bold);
            givenHeader.text = GameTexts.LedgerGiven;
            for (int i = 0; i < MaxRows; i++)
            {
                RectTransform row = UiKit.Rect(cols, "Row" + i, tl, tl, new Vector2(0f, RowsTop), new Vector2(fillWidth, 16f));
                _rows[i] = row;
                _rowTaken[i] = UiKit.Label(row, "Taken", font, 11, Color.white, TextAnchor.UpperLeft, tl, tl,
                    new Vector2(TakenX, 0f), new Vector2(TakenWidth, 16f));
                _rowGiven[i] = UiKit.Label(row, "Given", font, 11, Color.white, TextAnchor.UpperLeft, tl, tl,
                    new Vector2(GivenX, 0f), new Vector2(givenWidth, 16f));
                row.gameObject.SetActive(false);
            }
            _rowBaseFont = _rowTaken[0].fontSize;

            _closing = UiKit.Label(_content, "Closing", font, 14, UiKit.Cream, TextAnchor.UpperCenter, top, top,
                new Vector2(0f, -248f), new Vector2(560f, 20f));
            _smallNos = UiKit.Label(_content, "SmallNos", font, 12, UiKit.Turquoise, TextAnchor.MiddleCenter, top, top,
                new Vector2(0f, -270f), new Vector2(400f, 16f));

            // Minimal Synty button prefab. Navigation None: R / pad Start stay the only keys (a click must not make
            // Enter restart).
            Button retry = UiKit.PrefabButton(_content, UiKit.ButtonPrefab, GameTexts.Retry + "  " + GameTexts.RetryKeys,
                top, top, new Vector2(0f, -292f), new Vector2(160f, RetryHeight), out _, 14f, "Retry");
            retry.navigation = new Navigation { mode = Navigation.Mode.None };
            _retryRect = (RectTransform)retry.transform;
            _retryScale = _retryRect.localScale.x;
            retry.onClick.AddListener(RaiseRestart);

            Vector2 bottom = new Vector2(0.5f, 0f);
            _score = UiKit.Label(_root, "Score", font, 10, UiKit.Light, TextAnchor.MiddleCenter, bottom, bottom,
                new Vector2(0f, 6f), new Vector2(300f, 12f));

            _root.gameObject.SetActive(false);
        }

        /// <summary>The drawn portrait layers (colour, grey copy, face veil, crumble tiles); idle without a sprite.</summary>
        private void BuildDrawnPortrait(Transform fill)
        {
            UiTheme theme = UiKit.Theme;
            _artSprite = _portraitSprite != null ? _portraitSprite : theme != null ? theme.PortraitSprite : null;
            _artGreySprite = _portraitGreySprite != null ? _portraitGreySprite
                : theme != null ? theme.PortraitGreySprite : null;
            _artRoot = UiKit.Stretch(fill, "Drawn");
            if (_artSprite == null)
            {
                _artRoot.gameObject.SetActive(false);
                return;
            }
            Vector2 mid = new Vector2(0.5f, 0.5f);
            float side = ((RectTransform)fill).sizeDelta.x;

            _art = UiKit.Box(_artRoot, "Colour", Color.white, mid, mid, Vector2.zero, new Vector2(side, side));
            _art.sprite = _artSprite;
            _art.preserveAspect = true;
            if (_artGreySprite != null)
            {
                _artGrey = UiKit.Box(_artRoot, "Grey", Color.white, mid, mid, Vector2.zero, new Vector2(side, side));
                _artGrey.sprite = _artGreySprite;
                _artGrey.preserveAspect = true;
            }
            // Contract 4 "Yüzü": a dark smudge over the eyes and glasses (the face centre sits ~9% above the crop centre).
            Sprite soft = theme != null ? theme.GlowSprite : null;
            _faceVeil = UiKit.Box(_artRoot, "FaceVeil", new Color(0.03f, 0.02f, 0.04f, soft != null ? 0.92f : 0.6f),
                mid, mid, new Vector2(0.04f * side, 0.1f * side), new Vector2(0.62f * side, 0.36f * side));
            _faceVeil.sprite = soft;

            // Always-yes: the grey shell as an 8×8 grid of tiles that fall apart.
            _tileRoot = UiKit.Stretch(_artRoot, "Tiles");
            Sprite source = _artGreySprite != null ? _artGreySprite : _artSprite;
            if (source.texture == null) return;
            int count = TileGrid * TileGrid;
            _tiles = new RectTransform[count];
            _tileImages = new Image[count];
            _tileSprites = new Sprite[count];
            _tileBase = new Vector2[count];
            _tileVelocity = new Vector2[count];
            _tileDelay = new float[count];
            _tileSpin = new float[count];
            _tileAngle = new float[count];
            Rect r = source.textureRect;
            float tw = r.width / TileGrid, th = r.height / TileGrid;
            float cell = side / TileGrid;
            for (int gy = 0; gy < TileGrid; gy++)
            for (int gx = 0; gx < TileGrid; gx++)
            {
                int i = gy * TileGrid + gx;
                _tileSprites[i] = Sprite.Create(source.texture, new Rect(r.x + gx * tw, r.y + gy * th, tw, th),
                    mid, source.pixelsPerUnit, 0, SpriteMeshType.FullRect);
                _tileBase[i] = new Vector2((gx + 0.5f) * cell - side * 0.5f, (gy + 0.5f) * cell - side * 0.5f);
                _tileImages[i] = UiKit.Box(_tileRoot, "Tile" + i, Color.white, mid, mid, _tileBase[i],
                    new Vector2(cell + 0.5f, cell + 0.5f));
                _tileImages[i].sprite = _tileSprites[i];
                _tiles[i] = _tileImages[i].rectTransform;
            }
            _tileRoot.gameObject.SetActive(false);
        }
    }
}
