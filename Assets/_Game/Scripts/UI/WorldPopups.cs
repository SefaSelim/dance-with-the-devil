using System;
using System.Collections.Generic;
using DG.Tweening;
using DG.Tweening.Core;
using UnityEngine;
using UnityEngine.UI;

namespace DanceWithTheDevil
{
    /// <summary>
    /// Short text popups placed in the world (replace the top-right combo counter): "KRİTİK! 34" where a critical hit
    /// landed, "KOMBO ×N" over Defne every <see cref="_comboStep"/> combo, and a bigger "KOMBO 25!" with the new
    /// multiplier at each tier. Own overlay canvas (<see cref="UiKit.CreateCanvas"/>); each popup is re-placed every
    /// frame from its world point through <see cref="PixelCameraRig.WorldToDisplay01"/> × the 640×360 frame, floats
    /// up, pops with a DOTween punch and fades (unscaled time). Pool of <see cref="MaxPopups"/>: when full, a new popup
    /// replaces the oldest one of the lowest priority (crit &gt; tier-up &gt; combo) or is dropped.
    /// Hidden while paused or while the Devil flow is not Playing. ReduceMotion: no pop, rise or punch, only the fade.
    /// Display only: it reads the combo and enemy damage, never changes them.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class WorldPopups : MonoBehaviour
    {
        public const int MaxPopups = 12;

        private enum Kind { Combo = 0, Tier = 1, Crit = 2 }

        [Header("References (empty = found in the scene)")]
        [SerializeField] private ComboTracker _combo;
        [SerializeField] private WaveDirector _waves;
        [SerializeField] private DevilDirector _devil;
        [Tooltip("Game camera rig (WorldToDisplay01). Empty = UiKit.DisplayRig.")]
        [SerializeField] private PixelCameraRig _rig;

        [Header("Look")]
        [SerializeField] private int _sortingOrder = 12;
        private float _lastCritTime = -1f;
        [Tooltip("A combo popup every this many counts (tiers have their own).")]
        [SerializeField, Min(1)] private int _comboStep = ComboCounter.CountPerStep;
        [Tooltip("Rise over the popup's life, canvas units.")]
        [SerializeField, Min(0f)] private float _riseUnits = 24f;
        [Tooltip("Combo popups: metres above Defne's root.")]
        [SerializeField] private float _overHead = 2.4f;
        [Tooltip("Crit popups: metres above the hit point.")]
        [SerializeField] private float _overHit = 0.5f;
        [SerializeField] private Color _critColor = new Color(1f, 0.82f, 0.3f, 1f);
        [SerializeField] private Color _comboColor = new Color(0.93f, 0.88f, 0.74f, 1f);
        [Tooltip("Tier popup colour per tier 1..4.")]
        [SerializeField] private Color[] _tierColors =
        {
            new Color(0.93f, 0.88f, 0.74f, 1f),
            new Color(1f, 0.82f, 0.3f, 1f),
            new Color(1f, 0.6f, 0.3f, 1f),
            new Color(1f, 0.45f, 0.45f, 1f),
        };

        private sealed class Popup
        {
            public RectTransform Rect;
            public Text Label;
            public CanvasGroup Group;
            public Transform Follow;
            public Vector3 World;
            public float Rise;
            public int Priority;
            public float Born;
            public bool Active;
            public Sequence Tween;
            public DOGetter<float> GetRise, GetAlpha;
            public DOSetter<float> SetRise, SetAlpha;
            public TweenCallback Done;
        }

        private readonly Popup[] _pool = new Popup[MaxPopups];
        private readonly Dictionary<Enemy, Action<DamageInfo, float>> _handlers = new Dictionary<Enemy, Action<DamageInfo, float>>();
        private readonly HashSet<Enemy> _hooked = new HashSet<Enemy>();
        private readonly List<Enemy> _scratch = new List<Enemy>(32);
        private RectTransform _frame;
        private Action<int> _onChanged;
        private Action<int> _onTierUp;
        private Action<Enemy> _onSpawned;
        private bool _subscribed;
        private float _titleScale = 1f;

        private void Awake()
        {
            _onChanged = OnComboChanged;
            _onTierUp = OnTierUp;
            _onSpawned = Hook;
            UiTheme theme = UiKit.Theme;
            if (theme != null && theme.TitleFont != null) _titleScale = theme.TitleFontScale;
            Build();
        }

        private void OnEnable() => Subscribe();

        private void OnDisable()
        {
            Unsubscribe();
            for (int i = 0; i < _pool.Length; i++) Release(_pool[i]);
        }

        /// <summary>Code-built setups (tests). Re-subscribes.</summary>
        public void Configure(ComboTracker combo, WaveDirector waves, DevilDirector devil, PixelCameraRig rig)
        {
            Unsubscribe();
            _combo = combo;
            _waves = waves;
            _devil = devil;
            _rig = rig;
            if (isActiveAndEnabled) Subscribe();
        }

        // ------------------------------------------------------------ hooks

        private void Subscribe()
        {
            if (_subscribed || _onChanged == null) return;
            if (_combo == null) _combo = FindAnyObjectByType<ComboTracker>();
            if (_waves == null) _waves = FindAnyObjectByType<WaveDirector>();
            if (_devil == null) _devil = FindAnyObjectByType<DevilDirector>();
            if (_combo == null && _waves == null) return;
            _subscribed = true;
            if (_combo != null)
            {
                _combo.Changed += _onChanged;
                _combo.TierUp += _onTierUp;
            }
            if (_waves != null)
            {
                _waves.EnemySpawned += _onSpawned;
                IReadOnlyList<Enemy> active = _waves.ActiveEnemies;
                for (int i = 0; i < active.Count; i++) Hook(active[i]);
            }
        }

        private void Unsubscribe()
        {
            if (!_subscribed) return;
            _subscribed = false;
            if (_combo != null)
            {
                _combo.Changed -= _onChanged;
                _combo.TierUp -= _onTierUp;
            }
            if (_waves != null) _waves.EnemySpawned -= _onSpawned;
            _scratch.Clear();
            _scratch.AddRange(_hooked);
            for (int i = 0; i < _scratch.Count; i++) Unhook(_scratch[i]);
            _scratch.Clear();
            _hooked.Clear();
        }

        private void Hook(Enemy enemy)
        {
            if (enemy == null || enemy.Health == null || !_hooked.Add(enemy)) return;
            if (!_handlers.TryGetValue(enemy, out Action<DamageInfo, float> handler))
            {
                handler = (info, remaining) => OnEnemyDamaged(enemy, info, remaining);
                _handlers.Add(enemy, handler);
            }
            enemy.Health.Damaged += handler;
        }

        private void Unhook(Enemy enemy)
        {
            if (enemy == null) return;
            _hooked.Remove(enemy);
            if (enemy.Health != null && _handlers.TryGetValue(enemy, out Action<DamageInfo, float> handler))
                enemy.Health.Damaged -= handler;
        }

        private void OnEnemyDamaged(Enemy enemy, DamageInfo info, float remaining)
        {
            if (info.IsCrit && info.Amount > 0f)
            {
                Vector3 at = info.Point.sqrMagnitude > 0.0001f ? info.Point : enemy.transform.position + Vector3.up;
                at += Vector3.up * _overHit + Vector3.right * UnityEngine.Random.Range(-0.3f, 0.3f);
                string text = string.Format(ComboTexts.PopupCritFormat, Mathf.RoundToInt(info.Amount));
                Spawn(Kind.Crit, text, _critColor, 14, null, at);
            }
            if (remaining <= 0f) Unhook(enemy);
        }

        private void OnComboChanged(int count)
        {
            if (count <= 0 || count % _comboStep != 0) return;
            // A count that starts a tier gets the bigger tier popup instead (TierUp follows this event).
            if (ComboCounter.TierFor(count) > ComboCounter.TierFor(count - 1)) return;
            Spawn(Kind.Combo, string.Format(ComboTexts.PopupComboFormat, count), _comboColor, 12, PlayerAnchor(),
                Vector3.zero);
        }

        private void OnTierUp(int tier)
        {
            int count = _combo != null ? _combo.Count : 0;
            int tenths = _combo != null ? _combo.MultiplierTenths : 10;
            Color color = _tierColors != null && _tierColors.Length > 0
                ? _tierColors[Mathf.Clamp(tier - 1, 0, _tierColors.Length - 1)]
                : UiKit.Gold;
            Spawn(Kind.Tier, string.Format(ComboTexts.PopupTierFormat, count, tenths / 10, tenths % 10), color, 20,
                PlayerAnchor(), Vector3.zero);
        }

        private Transform PlayerAnchor() => _waves != null ? _waves.Player : null;

        // ------------------------------------------------------------ popups

        private void Spawn(Kind kind, string text, Color color, int size, Transform follow, Vector3 world)
        {
            int priority = (int)kind;
            if (follow != null)
            {
                // One popup over Defne at a time: stacked "N KOMBO" lines over her head were unreadable (captures
                // 2026-10-25). A fresh tier shout keeps its spot; anything else replaces the older one.
                for (int i = 0; i < _pool.Length; i++)
                {
                    Popup other = _pool[i];
                    if (!other.Active || other.Follow == null) continue;
                    if (other.Priority > priority && Time.unscaledTime - other.Born < 0.8f) return;
                    Release(other);
                }
            }
            else if (kind == Kind.Crit)
            {
                // Crits: at most one new popup per 0.2 s and three alive (crit-difficulty.md popups rule).
                if (Time.unscaledTime - _lastCritTime < 0.2f) return;
                int alive = 0;
                for (int i = 0; i < _pool.Length; i++)
                    if (_pool[i].Active && _pool[i].Priority == priority) alive++;
                if (alive >= 3) return;
                _lastCritTime = Time.unscaledTime;
            }
            Popup p = Acquire(priority);
            if (p == null) return;

            p.Active = true;
            p.Priority = priority;
            p.Born = Time.unscaledTime;
            p.Follow = follow;
            p.World = follow != null ? Vector3.up * _overHead : world; // offset when following
            p.Rise = 0f;
            p.Label.text = text;
            p.Label.color = color;
            p.Label.fontSize = Mathf.Max(6, Mathf.RoundToInt(size * _titleScale * UiKit.TextScale));
            p.Rect.SetAsLastSibling();
            p.Rect.gameObject.SetActive(true);
            p.Group.alpha = 1f;
            p.Rect.localScale = Vector3.one;
            Place(p);

            float life = kind == Kind.Tier ? 1.5f : kind == Kind.Crit ? 0.9f : 1.0f;
            Sequence s = DOTween.Sequence();
            if (!UiKit.ReduceMotion)
            {
                float start = kind == Kind.Tier ? 0.4f : 0.6f;
                p.Rect.localScale = new Vector3(start, start, 1f);
                s.Insert(0f, p.Rect.DOScale(1f, 0.16f).SetEase(Ease.OutBack));
                s.Insert(0.16f, p.Rect.DOPunchScale(Vector3.one * (kind == Kind.Tier ? 0.3f : 0.15f), 0.3f, 7, 0.5f));
                s.Insert(0f, DOTween.To(p.GetRise, p.SetRise, _riseUnits * (kind == Kind.Tier ? 0.7f : 1f), life)
                    .SetEase(Ease.OutCubic));
            }
            s.Insert(life * 0.6f, DOTween.To(p.GetAlpha, p.SetAlpha, 0f, life * 0.4f));
            s.SetUpdate(true).SetLink(gameObject).OnComplete(p.Done);
            p.Tween = s;
        }

        /// <summary>A free popup, else the oldest one of the lowest priority not above <paramref name="priority"/>.</summary>
        private Popup Acquire(int priority)
        {
            Popup victim = null;
            for (int i = 0; i < _pool.Length; i++)
            {
                Popup p = _pool[i];
                if (!p.Active) return p;
                if (p.Priority > priority) continue;
                if (victim == null || p.Priority < victim.Priority
                    || (p.Priority == victim.Priority && p.Born < victim.Born))
                    victim = p;
            }
            if (victim != null) Release(victim);
            return victim;
        }

        private static void Release(Popup p)
        {
            if (p == null) return;
            p.Tween?.Kill();
            p.Tween = null;
            p.Active = false;
            p.Follow = null;
            if (p.Rect != null) p.Rect.gameObject.SetActive(false);
        }

        private void LateUpdate()
        {
            if (!_subscribed) Subscribe();
            bool visible = !RunPause.IsPaused && (_devil == null || _devil.State == DevilState.Playing);
            if (_frame.gameObject.activeSelf != visible) _frame.gameObject.SetActive(visible);
            if (!visible) return;
            if (_rig == null) _rig = UiKit.DisplayRig;
            for (int i = 0; i < _pool.Length; i++)
                if (_pool[i].Active) Place(_pool[i]);
        }

        private void Place(Popup p)
        {
            Vector3 world = p.Follow != null ? p.Follow.position + p.World : p.World;
            Vector2 v;
            if (_rig != null)
            {
                v = _rig.WorldToDisplay01(world);
            }
            else
            {
                Camera cam = Camera.main;
                if (cam == null) return;
                Vector3 vp = cam.WorldToViewportPoint(world);
                v = new Vector2(vp.x, vp.y);
            }
            Vector2 frame = UiKit.ReferenceResolution;
            p.Rect.anchoredPosition = new Vector2(Mathf.Round(v.x * frame.x), Mathf.Round(v.y * frame.y + p.Rise));
        }

        private void Build()
        {
            _frame = UiKit.CreateCanvas(transform, "PopupCanvas", _sortingOrder, false);
            Font font = UiKit.TitleFont;
            for (int i = 0; i < _pool.Length; i++)
            {
                Text label = UiKit.Label(_frame, "Popup_" + i, font, 14, Color.white, TextAnchor.MiddleCenter,
                    Vector2.zero, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(200f, 48f), FontStyle.Bold);
                label.horizontalOverflow = HorizontalWrapMode.Overflow;
                var outline = label.gameObject.AddComponent<Outline>();
                outline.effectColor = new Color(0f, 0f, 0f, 0.9f);
                outline.effectDistance = new Vector2(1f, -1f);
                UiKit.AddShadow(label);
                var p = new Popup
                {
                    Rect = label.rectTransform,
                    Label = label,
                    Group = label.gameObject.AddComponent<CanvasGroup>(),
                };
                p.Group.blocksRaycasts = false;
                p.Group.interactable = false;
                p.GetRise = () => p.Rise;
                p.SetRise = v => p.Rise = v;
                p.GetAlpha = () => p.Group.alpha;
                p.SetAlpha = v => p.Group.alpha = v;
                p.Done = () => Release(p);
                label.gameObject.SetActive(false);
                _pool[i] = p;
            }
        }
    }
}
