using UnityEngine;
using UnityEngine.UI;

namespace DanceWithTheDevil
{
    /// <summary>
    /// The main menu's 2D stage (design/ux/main-menu.md §2), on the backdrop canvas built by
    /// MainMenuBuilder.BuildStage: the Doré engraving breathes (rest scale above 1, so it never shrinks below the view
    /// height), the drawing of Defne with the Devil breathes and sways a little around its bottom edge, the red glow
    /// behind them pulses like a fire, fog wisps drift sideways and pooled embers / ash / sparks float up and away.
    /// Everything runs on unscaled time with no allocations after Start. UiKit.ReduceMotion = a still picture (embers
    /// and fog hidden, glow steady).
    /// </summary>
    public sealed class MainMenuStage : MonoBehaviour
    {
        [Header("Engraving")]
        [SerializeField] private RectTransform _devil;
        [SerializeField] private float _breathScale = 0.025f;
        [SerializeField] private float _breathPeriod = 5f;

        [Header("Defne + Devil drawing")]
        [SerializeField] private RectTransform _characters;
        [SerializeField] private float _charBreath = 0.008f;
        [SerializeField] private float _charSwayDegrees = 0.45f;
        [SerializeField] private float _charPeriod = 6.5f;

        [Header("Glow")]
        [SerializeField] private Image _glow;
        [SerializeField] private float _glowPulse = 0.25f;
        [SerializeField] private float _glowPeriod = 3.7f;

        [Header("Embers (pooled UI squares)")]
        [SerializeField] private RectTransform _embersBack;
        [SerializeField] private RectTransform _embersFront;
        [SerializeField, Range(0, 120)] private int _backCount = 42;
        [SerializeField, Range(0, 60)] private int _frontCount = 12;

        [Header("Fog")]
        [SerializeField] private RectTransform _fogBack;
        [SerializeField] private RectTransform _fogFront;
        [SerializeField] private Sprite _softSprite;
        [SerializeField, Range(0, 8)] private int _backWisps = 4;
        [SerializeField, Range(0, 8)] private int _frontWisps = 2;

        private struct Ember
        {
            public RectTransform Rect;
            public Image Image;
            public Vector2 Pos, Vel;
            public float Age, Life, Phase, Wobble;
            public Color Color;
        }

        private struct Wisp
        {
            public RectTransform Rect;
            public Image Image;
            public float Speed, Phase, Alpha;
        }

        private static readonly Color[] Palette =
        {
            new Color(1f, 0.48f, 0.14f, 1f),   // ember orange
            new Color(1f, 0.7f, 0.3f, 1f),     // spark
            new Color(0.88f, 0.16f, 0.08f, 1f), // red cinder
            new Color(0.5f, 0.46f, 0.46f, 0.7f), // ash
        };

        private Vector3 _devilScale = Vector3.one;
        private Color _glowColor;
        private Vector3 _glowScale = Vector3.one;
        private Ember[] _back, _front;
        private Wisp[] _wispsBack, _wispsFront;
        private bool _stillShown;

        private void Start()
        {
            if (_devil != null) _devilScale = _devil.localScale;
            if (_glow != null)
            {
                _glowColor = _glow.color;
                _glowScale = _glow.rectTransform.localScale;
            }
            if (_softSprite == null && UiKit.Theme != null) _softSprite = UiKit.Theme.GlowSprite;
            _back = CreateEmbers(_embersBack, _backCount, false);
            _front = CreateEmbers(_embersFront, _frontCount, true);
            _wispsBack = CreateWisps(_fogBack, _backWisps, false);
            _wispsFront = CreateWisps(_fogFront, _frontWisps, true);
        }

        private void Update()
        {
            bool still = UiKit.ReduceMotion;
            if (still != _stillShown) ApplyStill(still);
            if (still) return;

            float t = Time.unscaledTime;
            float dt = Mathf.Min(Time.unscaledDeltaTime, 0.1f);
            const float Tau = 2f * Mathf.PI;
            if (_devil != null)
                _devil.localScale = _devilScale * (1f + Mathf.Sin(t * Tau / _breathPeriod) * _breathScale);
            if (_characters != null)
            {
                float b = 1f + Mathf.Sin(t * Tau / _charPeriod) * _charBreath;
                _characters.localScale = new Vector3(b, b, 1f);
                _characters.localRotation =
                    Quaternion.Euler(0f, 0f, Mathf.Sin(t * Tau / (_charPeriod * 1.7f) + 0.8f) * _charSwayDegrees);
            }
            if (_glow != null)
            {
                float n = Mathf.PerlinNoise(t * 1.3f, 0.37f) - 0.5f;
                float k = 1f + Mathf.Sin(t * Tau / _glowPeriod) * _glowPulse + n * 0.3f;
                Color c = _glowColor;
                c.a = Mathf.Clamp01(_glowColor.a * k);
                _glow.color = c;
                _glow.rectTransform.localScale = _glowScale * (1f + (k - 1f) * 0.06f);
            }
            TickEmbers(_back, _embersBack, dt, t, false);
            TickEmbers(_front, _embersFront, dt, t, true);
            TickWisps(_wispsBack, _fogBack, dt, t);
            TickWisps(_wispsFront, _fogFront, dt, t);
        }

        private void ApplyStill(bool still)
        {
            _stillShown = still;
            SetLayer(_embersBack, !still);
            SetLayer(_embersFront, !still);
            SetLayer(_fogBack, !still);
            SetLayer(_fogFront, !still);
            if (!still) return;
            if (_devil != null) _devil.localScale = _devilScale;
            if (_characters != null)
            {
                _characters.localScale = Vector3.one;
                _characters.localRotation = Quaternion.identity;
            }
            if (_glow != null)
            {
                _glow.color = _glowColor;
                _glow.rectTransform.localScale = _glowScale;
            }
        }

        private static void SetLayer(RectTransform layer, bool on)
        {
            if (layer != null) layer.gameObject.SetActive(on);
        }

        // ------------------------------------------------------------ embers

        private Ember[] CreateEmbers(RectTransform layer, int count, bool front)
        {
            if (layer == null || count <= 0) return null;
            var embers = new Ember[count];
            Vector2 size = LayerSize(layer);
            for (int i = 0; i < count; i++)
            {
                var go = new GameObject(front ? "EmberFront" : "Ember", typeof(RectTransform));
                var rect = (RectTransform)go.transform;
                rect.SetParent(layer, false);
                rect.anchorMin = rect.anchorMax = Vector2.zero;
                rect.pivot = new Vector2(0.5f, 0.5f);
                var image = go.AddComponent<Image>();
                image.raycastTarget = false;
                embers[i].Rect = rect;
                embers[i].Image = image;
                Respawn(ref embers[i], size, front);
                embers[i].Age = Random.Range(0f, embers[i].Life); // prewarm: already spread over the screen
                embers[i].Pos += embers[i].Vel * embers[i].Age;
            }
            return embers;
        }

        private static Vector2 LayerSize(RectTransform layer)
        {
            Vector2 s = layer.rect.size;
            return s.x > 1f && s.y > 1f ? s : new Vector2(1920f, 1080f);
        }

        private static void Respawn(ref Ember e, Vector2 size, bool front)
        {
            // Mostly from the lower half, biased to the right where the characters stand.
            float x = Random.value < 0.65f ? Random.Range(0.4f, 1f) : Random.Range(0f, 1f);
            e.Pos = new Vector2(x * size.x, Random.Range(-0.05f, 0.35f) * size.y);
            e.Vel = new Vector2(Random.Range(-18f, 34f), Random.Range(28f, 95f)) * (front ? 1.6f : 1f);
            e.Life = Random.Range(5f, 11f) / (front ? 1.4f : 1f);
            e.Age = 0f;
            e.Phase = Random.Range(0f, 10f);
            e.Wobble = Random.Range(6f, 22f);
            e.Color = Palette[Random.Range(0, Palette.Length)];
            float px = front ? Random.Range(6f, 12f) : Random.Range(2.5f, 7f);
            e.Rect.sizeDelta = new Vector2(px, px);
        }

        private static void TickEmbers(Ember[] embers, RectTransform layer, float dt, float t, bool front)
        {
            if (embers == null) return;
            Vector2 size = LayerSize(layer);
            float wind = Mathf.Sin(t * 0.21f) * 14f;
            for (int i = 0; i < embers.Length; i++)
            {
                ref Ember e = ref embers[i];
                e.Age += dt;
                if (e.Age >= e.Life || e.Pos.y > size.y + 20f) Respawn(ref e, size, front);
                e.Pos += (e.Vel + new Vector2(wind, 0f)) * dt;
                float wobble = Mathf.Sin(t * 1.3f + e.Phase) * e.Wobble;
                e.Rect.anchoredPosition = new Vector2(e.Pos.x + wobble, e.Pos.y);
                float u = e.Age / e.Life;
                float fade = Mathf.Clamp01(u / 0.15f) * Mathf.Clamp01((1f - u) / 0.4f);
                float flicker = 0.75f + 0.25f * Mathf.Sin(t * 9f + e.Phase * 3f);
                Color c = e.Color;
                c.a *= fade * flicker * (front ? 0.55f : 0.8f);
                e.Image.color = c;
            }
        }

        // ------------------------------------------------------------ fog

        private Wisp[] CreateWisps(RectTransform layer, int count, bool front)
        {
            if (layer == null || count <= 0 || _softSprite == null) return null;
            var wisps = new Wisp[count];
            Vector2 size = LayerSize(layer);
            for (int i = 0; i < count; i++)
            {
                var go = new GameObject(front ? "FogFront" : "Fog", typeof(RectTransform));
                var rect = (RectTransform)go.transform;
                rect.SetParent(layer, false);
                rect.anchorMin = rect.anchorMax = Vector2.zero;
                rect.pivot = new Vector2(0.5f, 0.5f);
                rect.sizeDelta = new Vector2(Random.Range(800f, 1200f), Random.Range(200f, 320f));
                float y = front ? Random.Range(0.02f, 0.18f) : Random.Range(0.12f, 0.6f);
                rect.anchoredPosition = new Vector2(Random.Range(0f, 1f) * size.x, y * size.y);
                var image = go.AddComponent<Image>();
                image.sprite = _softSprite;
                image.raycastTarget = false;
                Color c = front ? new Color(0.22f, 0.08f, 0.08f, 1f) : new Color(0.38f, 0.13f, 0.1f, 1f);
                c.a = 0f;
                image.color = c;
                wisps[i] = new Wisp
                {
                    Rect = rect,
                    Image = image,
                    Speed = Random.Range(8f, 22f) * (Random.value < 0.5f ? -1f : 1f),
                    Phase = Random.Range(0f, 10f),
                    Alpha = front ? Random.Range(0.08f, 0.12f) : Random.Range(0.06f, 0.1f),
                };
            }
            return wisps;
        }

        private static void TickWisps(Wisp[] wisps, RectTransform layer, float dt, float t)
        {
            if (wisps == null) return;
            float width = LayerSize(layer).x;
            for (int i = 0; i < wisps.Length; i++)
            {
                ref Wisp w = ref wisps[i];
                Vector2 p = w.Rect.anchoredPosition;
                p.x += w.Speed * dt;
                float half = w.Rect.sizeDelta.x * 0.5f;
                if (p.x > width + half) p.x = -half;
                else if (p.x < -half) p.x = width + half;
                w.Rect.anchoredPosition = p;
                Color c = w.Image.color;
                c.a = w.Alpha * (0.7f + 0.3f * Mathf.Sin(t * 0.25f + w.Phase));
                w.Image.color = c;
            }
        }

        /// <summary>Editor builder hook (MainMenuBuilder.BuildStage).</summary>
        public void Configure(RectTransform devil, RectTransform characters, Image glow, RectTransform fogBack,
            RectTransform fogFront, RectTransform embersBack, RectTransform embersFront, Sprite softSprite)
        {
            _devil = devil;
            _characters = characters;
            _glow = glow;
            _fogBack = fogBack;
            _fogFront = fogFront;
            _embersBack = embersBack;
            _embersFront = embersFront;
            _softSprite = softSprite;
        }
    }
}
