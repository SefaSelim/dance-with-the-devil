using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace DanceWithTheDevil
{
    /// <summary>
    /// The Devil's overlay (MAIN_GDD §8): the Devil leans in from the RIGHT screen edge, says lines, shows the
    /// contract card with a big bright EVET and a small dim HAYIR (K-2) and the cost's poetic name plus full effect
    /// (K-3). Display only: it raises <see cref="YesPressed"/>, <see cref="NoPressed"/>, <see cref="AdvancePressed"/>
    /// and <see cref="DevilDirector"/> decides. Keys: EVET = E / Enter / pad South; HAYIR = H / pad East;
    /// advance = Enter / Space / E / click / pad South. Each prompt ignores input for a short lock so a held
    /// fire/rocket key cannot pick for the player. Unscaled time.
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
        [Tooltip("Input ignored for this long after each line/card appears, seconds.")]
        [SerializeField, Min(0f)] private float _inputLock = 0.45f;
        [Tooltip("Black overlay alpha when fully in.")]
        [SerializeField, Range(0f, 1f)] private float _dimAlpha = 0.25f;

        private static readonly Vector2 DevilSize = new Vector2(170f, 300f);
        private static readonly Vector2 DevilInPosition = new Vector2(40f, -10f);   // partly past the edge
        private static readonly Vector2 DevilOutPosition = new Vector2(240f, -10f);

        private RectTransform _root;
        private Image _dim;
        private RectTransform _devil;
        private GameObject _speech;
        private Text _speechText;
        private GameObject _card;
        private Text _giveText;
        private Text _takeText;
        private GameObject _yes;
        private GameObject _no;

        private enum Mode { Hidden, Entering, Line, Card }
        private Mode _mode = Mode.Hidden;
        private bool _cardHasChoice;
        private float _enterTime;
        private float _promptTime;

        public event Action YesPressed;
        public event Action NoPressed;
        public event Action AdvancePressed;

        public float EnterDuration => UiKit.ReduceMotion ? 0f : _slideDuration;
        public bool IsVisible => _mode != Mode.Hidden;

        private void Awake() => Build();

        private void Start() => UiKit.EnsureEventSystem();

        /// <summary>Overlay fades in and the Devil slides in from the right edge.</summary>
        public void Enter()
        {
            _root.gameObject.SetActive(true);
            _speech.SetActive(false);
            _card.SetActive(false);
            _enterTime = 0f;
            _mode = Mode.Entering;
            _promptTime = 0f;
            ApplyEnter(EnterDuration <= 0f ? 1f : 0f);
        }

        public void ShowLine(string line)
        {
            if (_mode == Mode.Hidden) Enter();
            ApplyEnter(1f);
            _speech.SetActive(true);
            _speechText.text = line;
            _card.SetActive(false);
            _mode = Mode.Line;
            _promptTime = 0f;
        }

        public void ShowCard(DealDefinition deal, bool offersChoice)
        {
            if (_mode == Mode.Hidden) Enter();
            ApplyEnter(1f);
            _speech.SetActive(true);
            _speechText.text = deal.OfferLine;
            _giveText.text = "<color=#FFD24D><b>" + GameTexts.CardGive + "</b></color> " + deal.UpgradeText;
            _takeText.text = "<color=#FF6464><b>" + GameTexts.CardTake + " " + deal.CostName + "</b></color>\n"
                + GameTexts.CardCostVisible + " " + deal.CostVisibleText + "\n"
                + GameTexts.CardCostMechanical + " " + deal.CostMechanicalText;
            _yes.SetActive(offersChoice);
            _no.SetActive(offersChoice);
            _card.SetActive(true);
            _cardHasChoice = offersChoice;
            _mode = Mode.Card;
            _promptTime = 0f;
        }

        public void Hide()
        {
            _mode = Mode.Hidden;
            _root.gameObject.SetActive(false);
        }

        private void Update()
        {
            if (_mode == Mode.Hidden) return;
            float dt = Time.unscaledDeltaTime;
            _promptTime += dt;

            if (_mode == Mode.Entering)
            {
                _enterTime += dt;
                ApplyEnter(EnterDuration > 0f ? _enterTime / EnterDuration : 1f);
            }

            if (_promptTime < _inputLock) return;

            if (_mode == Mode.Card && _cardHasChoice)
            {
                Keyboard k = Keyboard.current;
                Gamepad g = Gamepad.current;
                if (UiKit.ConfirmPressed() || (k != null && k.eKey.wasPressedThisFrame)) RaiseYes();
                else if ((k != null && k.hKey.wasPressedThisFrame) || (g != null && g.buttonEast.wasPressedThisFrame)) RaiseNo();
                return;
            }

            if (UiKit.AdvancePressed())
            {
                _promptTime = 0f;
                AdvancePressed?.Invoke();
            }
        }

        private void RaiseYes()
        {
            if (_mode != Mode.Card || !_cardHasChoice || _promptTime < _inputLock) return;
            YesPressed?.Invoke();
        }

        private void RaiseNo()
        {
            if (_mode != Mode.Card || !_cardHasChoice || _promptTime < _inputLock) return;
            NoPressed?.Invoke();
        }

        private void ApplyEnter(float t)
        {
            float e = UiKit.EaseOut(t);
            _devil.anchoredPosition = Vector2.LerpUnclamped(DevilOutPosition, DevilInPosition, e);
            Color c = _dim.color;
            c.a = _dimAlpha * Mathf.Clamp01(t);
            _dim.color = c;
        }

        // ------------------------------------------------------------ build

        private void Build()
        {
            Font font = UiKit.ResolveFont(_font);
            _root = UiKit.CreateCanvas(transform, "DevilCanvas", _sortingOrder, true);

            _dim = UiKit.Stretch(_root, "Dim").gameObject.AddComponent<Image>();
            _dim.color = new Color(0f, 0f, 0f, 0f);
            _dim.raycastTarget = false;

            _devil = UiKit.Rect(_root, "Devil", new Vector2(1f, 0f), new Vector2(1f, 0f), DevilOutPosition, DevilSize);
            _devil.localRotation = Quaternion.Euler(0f, 0f, 6f); // leaning in over the edge
            if (_devilSprite != null)
            {
                Image art = UiKit.Stretch(_devil, "Art").gameObject.AddComponent<Image>();
                art.sprite = _devilSprite;
                art.preserveAspect = true;
                art.raycastTarget = false;
            }
            else
            {
                BuildPlaceholderDevil(_devil);
            }

            // Speech panel (top centre).
            Image speechBack = UiKit.Box(_root, "Speech", new Color(0f, 0f, 0f, 0.75f), new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f), new Vector2(-60f, -20f), new Vector2(430f, 56f));
            _speech = speechBack.gameObject;
            _speechText = UiKit.Label(speechBack.transform, "Line", font, 15, Color.white, TextAnchor.MiddleCenter,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(414f, 50f), FontStyle.Bold);
            UiKit.AddShadow(_speechText);

            // Contract card.
            Image card = UiKit.Framed(_root, "Card", UiKit.DevilRed, UiKit.Panel, new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f), new Vector2(-70f, -30f), new Vector2(370f, 200f));
            _card = card.transform.parent.gameObject;
            _giveText = UiKit.Label(card.transform, "Give", font, 11, Color.white, TextAnchor.UpperLeft,
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -8f), new Vector2(340f, 34f));
            _takeText = UiKit.Label(card.transform, "Take", font, 11, UiKit.Cream, TextAnchor.UpperLeft,
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -46f), new Vector2(340f, 70f));

            Image yes = UiKit.Box(card.transform, "Yes", new Color(0.95f, 0.22f, 0.15f, 1f), new Vector2(0.5f, 0f),
                new Vector2(0.5f, 0f), new Vector2(-40f, 10f), new Vector2(180f, 42f));
            UiKit.MakeButton(yes, new Color(1f, 0.75f, 0.6f, 1f)).onClick.AddListener(RaiseYes);
            Text yesLabel = UiKit.Label(yes.transform, "Label", font, 20, Color.white, TextAnchor.MiddleCenter,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(176f, 40f), FontStyle.Bold);
            yesLabel.text = GameTexts.Yes + "  " + GameTexts.YesKeys;
            UiKit.AddShadow(yesLabel);
            _yes = yes.gameObject;

            Image no = UiKit.Box(card.transform, "No", new Color(0.17f, 0.15f, 0.17f, 1f), new Vector2(0.5f, 0f),
                new Vector2(0.5f, 0f), new Vector2(120f, 12f), new Vector2(70f, 18f));
            UiKit.MakeButton(no, new Color(1.3f, 1.3f, 1.3f, 1f)).onClick.AddListener(RaiseNo);
            Text noLabel = UiKit.Label(no.transform, "Label", font, 9, UiKit.Dim, TextAnchor.MiddleCenter,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(68f, 16f));
            noLabel.text = GameTexts.No + " " + GameTexts.NoKeys;
            _no = no.gameObject;

            _root.gameObject.SetActive(false);
        }

        private static void BuildPlaceholderDevil(RectTransform root)
        {
            Color body = UiKit.DevilRed;
            Color head = new Color(0.86f, 0.16f, 0.16f, 1f);
            Color horn = new Color(0.22f, 0.03f, 0.05f, 1f);
            Color eye = new Color(1f, 0.86f, 0.1f, 1f);
            Vector2 a = Vector2.zero;
            Vector2 c = new Vector2(0.5f, 0.5f);
            UiKit.Box(root, "Body", body, a, c, new Vector2(100f, 90f), new Vector2(130f, 180f));
            UiKit.Box(root, "Hand", head, a, c, new Vector2(22f, 150f), new Vector2(30f, 16f));
            UiKit.Box(root, "Claw", horn, a, c, new Vector2(8f, 144f), new Vector2(6f, 8f));
            UiKit.Box(root, "Head", head, a, c, new Vector2(82f, 210f), new Vector2(100f, 90f));
            UiKit.Box(root, "Horn_L", horn, a, c, new Vector2(46f, 266f), new Vector2(16f, 38f)).rectTransform
                .localRotation = Quaternion.Euler(0f, 0f, 22f);
            UiKit.Box(root, "Horn_R", horn, a, c, new Vector2(118f, 266f), new Vector2(16f, 38f)).rectTransform
                .localRotation = Quaternion.Euler(0f, 0f, -22f);
            UiKit.Box(root, "Eye_L", eye, a, c, new Vector2(62f, 222f), new Vector2(20f, 9f));
            UiKit.Box(root, "Eye_R", eye, a, c, new Vector2(102f, 222f), new Vector2(20f, 9f));
            UiKit.Box(root, "Grin", horn, a, c, new Vector2(82f, 190f), new Vector2(48f, 6f));
        }
    }
}
