using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace DanceWithTheDevil
{
    /// <summary>
    /// Code-built uGUI helpers for the jam UI (no prefabs needed). Canvases are Screen Space Overlay and are fitted to
    /// the pixel camera's game image (<see cref="UiCanvasFit"/>): one canvas unit = one low-res texel, the scale
    /// follows the integer scale of the image and the content lives in a 640×360 "Frame" over the image, so the HUD
    /// never lands on the letterbox bars and its blocks have the world's pixel size.
    /// Text uses legacy uGUI Text, except the Synty button prefabs (<see cref="PrefabButton"/>), whose labels are
    /// TextMesh Pro. No synthesized italics (the built-in font has one face); font sizes are even numbers
    /// (10, 12, 14, 16, 20, 24).
    /// </summary>
    public static class UiKit
    {
        public static readonly Vector2 ReferenceResolution = new Vector2(640f, 360f);
        // Fallback layout when no rig is registered: the same rule as PixelCameraRig.UpdateLayout.
        private const float FallbackMinIntegerCoverage = 0.8f;

        /// <summary>Accessibility: skip slide/fade animations (no settings menu yet).</summary>
        public static bool ReduceMotion;
        /// <summary>Accessibility: multiplies every font size at build time (no settings menu yet).</summary>
        public static float TextScale = 1f;

        /// <summary>
        /// The pixel camera rig whose game image the UI canvases fit (registered by <see cref="DisplayGrade"/>).
        /// Null = the canvases compute the same integer-scaled, centred 640×360 rect themselves.
        /// </summary>
        public static PixelCameraRig DisplayRig { get; set; }

        /// <summary>Raised on every UI click/advance (audio "ui_click").</summary>
        public static event System.Action Clicked;

        public static void NotifyClick() => Clicked?.Invoke();

        // Palette (placeholder, GDD §13: dark cold base, cream edge, plum hair, petrol overalls, turquoise detail).
        public static readonly Color Ink = new Color(0.06f, 0.05f, 0.08f, 1f);
        public static readonly Color Panel = new Color(0.08f, 0.06f, 0.09f, 0.92f);
        public static readonly Color Cream = new Color(0.93f, 0.88f, 0.74f, 1f);
        public static readonly Color Dim = new Color(0.5f, 0.48f, 0.5f, 1f);
        public static readonly Color Light = new Color(0.74f, 0.72f, 0.74f, 1f);
        public static readonly Color Turquoise = new Color(0.25f, 0.8f, 0.75f, 1f);
        public static readonly Color DevilRed = new Color(0.78f, 0.1f, 0.12f, 1f);
        public static readonly Color CostRed = new Color(1f, 0.45f, 0.45f, 1f);
        public static readonly Color Gold = new Color(1f, 0.82f, 0.3f, 1f);
        public static readonly Color HealthRed = new Color(0.86f, 0.22f, 0.27f, 1f);

        private static Font _builtinFont;
        private static UiTheme _theme;
        private static bool _themeLoaded;

        /// <summary>The UI theme from Resources/UiTheme (Synty frames, Pirata One / Markazi), or null = flat look.</summary>
        public static UiTheme Theme
        {
            get
            {
                if (!_themeLoaded)
                {
                    _theme = Resources.Load<UiTheme>("UiTheme");
                    _themeLoaded = true;
                }
                return _theme;
            }
        }

        /// <summary>The theme's main text button prefab (Synty WorldSpace_Item_01), or null = code-built button.</summary>
        public static GameObject ButtonPrefab => Theme != null ? Theme.ButtonPrefab : null;

        /// <summary>The theme's small text button prefab (same minimal prefab), or null = code-built button.</summary>
        public static GameObject SmallButtonPrefab => Theme != null ? Theme.SmallButtonPrefab : null;

        /// <summary>Smallest prefab label, canvas units (one unit is about 3 px at 1080p, so about 18 px).</summary>
        public const float MinLabelUnits = 6f;

        /// <summary>The theme's heading font, else <see cref="ResolveFont"/>.</summary>
        public static Font TitleFont => Theme != null && Theme.TitleFont != null ? Theme.TitleFont : ResolveFont(null);

        /// <summary>The serialized font, else the theme's body font, else Unity's built-in LegacyRuntime font (all have the Turkish glyphs).</summary>
        public static Font ResolveFont(Font preferred)
        {
            if (preferred != null) return preferred;
            if (Theme != null && Theme.BodyFont != null) return Theme.BodyFont;
            if (_builtinFont == null) _builtinFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            return _builtinFont;
        }

        /// <summary>
        /// Screen-pixel rect of the game image: the registered rig's <see cref="PixelCameraRig.DisplayRect"/>, else
        /// the same integer-scaled, centred 640×360 fit computed here.
        /// </summary>
        public static Rect CurrentDisplayRect()
        {
            if (DisplayRig != null)
            {
                Rect r = DisplayRig.DisplayRect;
                if (r.width > 0f && r.height > 0f) return r;
            }
            float sw = Mathf.Max(1, Screen.width);
            float sh = Mathf.Max(1, Screen.height);
            float fit = Mathf.Min(sw / ReferenceResolution.x, sh / ReferenceResolution.y);
            int integerScale = Mathf.FloorToInt(fit);
            float scale = integerScale >= 1 && integerScale / fit >= FallbackMinIntegerCoverage ? integerScale : fit;
            float w = Mathf.Round(ReferenceResolution.x * scale);
            float h = Mathf.Round(ReferenceResolution.y * scale);
            return new Rect(Mathf.Floor((sw - w) * 0.5f), Mathf.Floor((sh - h) * 0.5f), w, h);
        }

        /// <summary>
        /// Creates an overlay canvas fitted to the game image and returns its 640×360 content frame (bottom-left
        /// pivot). Anchors inside the frame are relative to the game image. Hide the UI by deactivating the frame.
        /// </summary>
        public static RectTransform CreateCanvas(Transform parent, string name, int sortingOrder, bool interactive)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = sortingOrder;
            canvas.pixelPerfect = true;
            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
            scaler.referencePixelsPerUnit = 100f;
            if (interactive) go.AddComponent<GraphicRaycaster>();

            var frameGo = new GameObject("Frame", typeof(RectTransform));
            var frame = (RectTransform)frameGo.transform;
            frame.SetParent(go.transform, false);
            frame.anchorMin = Vector2.zero;
            frame.anchorMax = Vector2.zero;
            frame.pivot = Vector2.zero;
            frame.sizeDelta = ReferenceResolution;

            go.AddComponent<UiCanvasFit>().Initialize(scaler, frame);
            return frame;
        }

        public static RectTransform Rect(Transform parent, string name, Vector2 anchor, Vector2 pivot,
            Vector2 position, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = pivot;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            return rect;
        }

        public static RectTransform Stretch(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            return rect;
        }

        /// <summary>A flat colour block (Image without a sprite).</summary>
        public static Image Box(Transform parent, string name, Color color, Vector2 anchor, Vector2 pivot,
            Vector2 position, Vector2 size)
        {
            Image image = Rect(parent, name, anchor, pivot, position, size).gameObject.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        /// <summary>A block with a border: returns the inner block (child of the border block).</summary>
        public static Image Framed(Transform parent, string name, Color border, Color fill, Vector2 anchor,
            Vector2 pivot, Vector2 position, Vector2 size, float thickness = 2f)
        {
            UiTheme theme = Theme;
            bool ornate = theme != null && theme.PanelSprite != null
                && size.x >= theme.MinFramedSize.x && size.y >= theme.MinFramedSize.y;
            Image outer = Box(parent, name, border, anchor, pivot, position, size);
            if (!ornate)
            {
                Image inner = Box(outer.transform, "Fill", fill, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                    Vector2.zero, size - new Vector2(thickness * 2f, thickness * 2f));
                return inner;
            }

            // Ornate: the outer block carries the Synty frame (tinted toward the border colour, so state colours
            // still read) and the fill is a plate under it. The frame is the outer block's own sprite, drawn first,
            // so a "Rim" copy on top of the plate keeps the bevel visible. Callers keep using fill.transform.parent
            // as the border, as with the flat frame.
            Color tint = Color.Lerp(Color.white, border, 0.45f);
            SetSliced(outer, theme.PanelSprite, theme.PanelSpriteScale);
            outer.color = tint;
            float inset = Mathf.Max(thickness, 4f);
            Image plate = Box(outer.transform, "Fill", fill, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                Vector2.zero, size - new Vector2(inset * 2f, inset * 2f));
            if (theme.PanelFillSprite != null) SetSliced(plate, theme.PanelFillSprite, theme.PanelFillSpriteScale);
            Image rim = Box(outer.transform, "Rim", tint, Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero);
            Stretch(rim.rectTransform);
            SetSliced(rim, theme.PanelSprite, theme.PanelSpriteScale);
            outer.gameObject.AddComponent<UiRimTint>().Initialize(outer, rim);
            return plate;
        }

        /// <summary>Stretches a rect over its parent.</summary>
        public static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        /// <summary>9-slice sprite on an image; <paramref name="pixelsPerUnit"/> = sprite pixels per canvas unit of border.</summary>
        public static void SetSliced(Image image, Sprite sprite, float pixelsPerUnit)
        {
            image.sprite = sprite;
            image.type = sprite.border.sqrMagnitude > 0f ? Image.Type.Sliced : Image.Type.Simple;
            image.fillCenter = true;
            image.pixelsPerUnitMultiplier = Mathf.Max(0.01f, pixelsPerUnit);
        }

        /// <summary>
        /// A menu button on the theme's plate (a flat block without a theme), label in the title face, with automatic
        /// keyboard/gamepad navigation.
        /// </summary>
        public static Button ThemedButton(Transform parent, string name, string label, Vector2 anchor, Vector2 pivot,
            Vector2 position, Vector2 size, int fontSize, out Text text)
        {
            Button button = ThemedPlate(parent, name, anchor, pivot, position, size, out Image plate);
            text = Label(plate.transform, "Label", TitleFont, fontSize, Cream, TextAnchor.MiddleCenter,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 1f), size, FontStyle.Bold);
            text.text = label;
            AddShadow(text);
            return button;
        }

        /// <summary>The plate and Button of <see cref="ThemedButton"/>, without a label.</summary>
        private static Button ThemedPlate(Transform parent, string name, Vector2 anchor, Vector2 pivot,
            Vector2 position, Vector2 size, out Image plate)
        {
            UiTheme theme = Theme;
            bool themed = theme != null && theme.ButtonSprite != null;
            plate = Box(parent, name, themed ? Color.white : new Color(0.2f, 0.16f, 0.18f, 1f), anchor, pivot,
                position, size);
            if (themed) SetSliced(plate, theme.ButtonSprite, theme.ButtonSpriteScale);
            Button button = MakeButton(plate, new Color(1f, 0.55f, 0.5f, 1f));
            ColorBlock colors = button.colors;
            colors.normalColor = new Color(0.72f, 0.68f, 0.68f, 1f);
            colors.pressedColor = new Color(0.9f, 0.4f, 0.38f, 1f);
            button.colors = colors;
            button.navigation = new Navigation { mode = Navigation.Mode.Automatic };
            return button;
        }

        // ------------------------------------------------------------ Synty button prefabs (TextMesh Pro labels)

        /// <summary>
        /// A Synty button prefab used as-is: instantiated under <paramref name="parent"/> and fitted to
        /// <paramref name="size"/> (canvas units) by scaling the root uniformly to the requested height; the root keeps
        /// the prefab's own height and gets the matching width, so its layout groups and 9-slice borders stay as
        /// authored. Prefabs whose Button sits on a child (WorldSpace_Item_01: root > "Content") get it moved to the
        /// root, so the returned Button's transform is always the fitted root; the bar fills the root, the label is
        /// centred and a thin theme frame is drawn over it (gold on focus, see UiButtonFx).
        /// <paramref name="fontSize"/> is the label size in canvas units (0 = the prefab's own, scaled),
        /// never below <see cref="MinLabelUnits"/>. Clicks raise <see cref="Clicked"/>; navigation is Automatic;
        /// Animators run on unscaled time (the pause menu runs at timeScale 0). No prefab = the code-built themed
        /// plate with a TextMesh Pro label (TMP's default font).
        /// </summary>
        public static Button PrefabButton(Transform parent, GameObject prefab, string label, Vector2 anchor,
            Vector2 pivot, Vector2 position, Vector2 size, out TMP_Text text, float fontSize = 0f, string name = null)
        {
            Button button;
            if (prefab == null)
            {
                button = ThemedPlate(parent, name ?? "Button", anchor, pivot, position, size, out Image plate);
                text = FallbackLabel(plate.transform, label, size, fontSize);
                return button;
            }

            GameObject go = InstantiateFitted(parent, prefab, name, anchor, pivot, position, size, out float scale);
            AdaptPrefabLook(go);
            text = SetPrefabLabel(go, label, scale, fontSize);
            button = RootButton(go, text, true);
            // Focus = gold label (tint) + gold frame and a small punch (UiButtonFx).
            if (button.transition == Selectable.Transition.ColorTint)
            {
                ColorBlock colors = button.colors;
                colors.normalColor = Color.white;
                colors.highlightedColor = Gold;
                colors.selectedColor = Gold;
                colors.pressedColor = new Color(0.85f, 0.62f, 0.2f, 1f);
                colors.fadeDuration = 0.08f;
                button.colors = colors;
            }
            button.onClick.AddListener(NotifyClick);
            button.navigation = new Navigation { mode = Navigation.Mode.Automatic };
            AddFrame(go);
            return button;
        }

        /// <summary>
        /// A Synty button prefab driven by <see cref="UiHoldButton"/> (the Devil card's EVET / HAYIR), fitted like
        /// <see cref="PrefabButton"/>. The prefab's Button stays only for its hover/press visuals (Animator or tint):
        /// no click listener and navigation None, so it never fires a click or takes keyboard focus; the screen reads
        /// the pointer state from the returned component. No prefab = a plain block (tint it through
        /// <see cref="PlateImages"/>).
        /// </summary>
        public static UiHoldButton PrefabHoldButton(Transform parent, GameObject prefab, string label, Vector2 anchor,
            Vector2 pivot, Vector2 position, Vector2 size, out TMP_Text text, float fontSize = 0f, string name = null)
        {
            if (prefab == null)
            {
                Image block = Box(parent, name ?? "HoldButton", new Color(0.2f, 0.18f, 0.2f, 1f), anchor, pivot,
                    position, size);
                text = FallbackLabel(block.transform, label, size, fontSize);
                return MakeHoldButton(block);
            }

            GameObject go = InstantiateFitted(parent, prefab, name, anchor, pivot, position, size, out float scale);
            AdaptPrefabLook(go);
            text = SetPrefabLabel(go, label, scale, fontSize);
            // No new Button: the screen tints the label and plates itself; hover lights the frame (UiButtonFx).
            Button visuals = RootButton(go, text, false);
            if (visuals != null)
            {
                visuals.onClick.RemoveAllListeners();
                visuals.navigation = new Navigation { mode = Navigation.Mode.None };
            }
            AddFrame(go);
            return go.AddComponent<UiHoldButton>();
        }

        // The focus colour on the Synty plates (blue in the pack) as a deep ember red, the game's accent.
        private static readonly Color FocusPlate = new Color(0.62f, 0.16f, 0.14f, 1f);

        /// <summary>
        /// Fits the Synty prefab to this game: hides its gamepad key glyph ("A" button prompt, wrong for keyboard and
        /// mouse players) and recolours the blue highlight/selected plates to the ember red accent.
        /// </summary>
        private static void AdaptPrefabLook(GameObject root)
        {
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
            {
                if (t.name == "Input_KeyButton" || t.name.StartsWith("Input_DarkFantasy_KeyButton"))
                    t.gameObject.SetActive(false);
            }
            foreach (Image image in root.GetComponentsInChildren<Image>(true))
            {
                if (image.gameObject.name != "SPR_Background" || image.transform.parent == null) continue;
                string state = image.transform.parent.name;
                if (state == "Selected" || state == "Highlighted") image.color = FocusPlate;
            }
        }

        /// <summary>
        /// The tintable plates of a prefab button (its "SPR_Background" images, one per Animator state), else the
        /// root's own image (the code-built fallback block).
        /// </summary>
        public static Image[] PlateImages(GameObject root)
        {
            var plates = new System.Collections.Generic.List<Image>();
            foreach (Image image in root.GetComponentsInChildren<Image>(true))
                if (image.gameObject.name == "SPR_Background") plates.Add(image);
            if (plates.Count == 0 && root.TryGetComponent(out Image own)) plates.Add(own);
            return plates.ToArray();
        }

        /// <summary>
        /// The Button on the prefab root. A Button on a child (WorldSpace_Item_01 keeps it on "Content") is replaced by
        /// one on the root with the same transition and colours, targeting the label, so callers can position, select
        /// and link the returned component's transform as before. <paramref name="keep"/> false (hold buttons): any
        /// child Button is removed and none is added (returns the root's own Button if the prefab has one).
        /// </summary>
        private static Button RootButton(GameObject root, TMP_Text label, bool keep)
        {
            Button inner = null;
            foreach (Button b in root.GetComponentsInChildren<Button>(true))
            {
                if (b.gameObject == root) continue;
                inner = b;
                break;
            }
            Button button = root.GetComponent<Button>();
            if (button == null && keep)
            {
                button = root.AddComponent<Button>();
                if (inner != null)
                {
                    button.transition = inner.transition;
                    button.colors = inner.colors;
                    button.spriteState = inner.spriteState;
                    button.animationTriggers = inner.animationTriggers;
                    button.targetGraphic = inner.targetGraphic;
                }
            }
            if (inner != null)
            {
                inner.enabled = false; // stops it at once; the component goes at the end of the frame
                DestroyComponent(inner);
            }
            if (button != null && (button.targetGraphic == null || button.targetGraphic.gameObject == root))
                button.targetGraphic = label != null ? (Graphic)label : root.GetComponent<Graphic>();
            return button;
        }

        private static void DestroyComponent(Component component)
        {
            if (Application.isPlaying) Object.Destroy(component);
            else Object.DestroyImmediate(component);
        }

        /// <summary>
        /// Minimal look for layout-group prefabs (WorldSpace_Item_01): "Content" fills the root inside a small inset
        /// (so the parchment bar spans the button) with its label centred; the theme's thin frame (no centre) on top;
        /// <see cref="UiButtonFx"/> for the focus colour and punch.
        /// </summary>
        private static void AddFrame(GameObject root)
        {
            if (root.TryGetComponent(out HorizontalLayoutGroup layout))
            {
                layout.padding = new RectOffset(40, 40, 14, 14);
                layout.childAlignment = TextAnchor.MiddleCenter;
                layout.childControlWidth = true;
                layout.childControlHeight = true;
                layout.childForceExpandWidth = true;
                layout.childForceExpandHeight = true;
            }
            foreach (Image image in root.GetComponentsInChildren<Image>(true))
                if (image.gameObject != root) image.raycastTarget = false; // the root's clear block takes the pointer

            UiTheme theme = Theme;
            Image frame = null;
            if (theme != null && theme.ButtonFrameSprite != null)
            {
                RectTransform rect = Stretch(root.transform, "Frame");
                rect.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
                frame = rect.gameObject.AddComponent<Image>();
                SetSliced(frame, theme.ButtonFrameSprite, theme.ButtonFrameScale);
                frame.fillCenter = false;
                frame.raycastTarget = false;
                frame.color = theme.ButtonFrameColor;
            }
            Color normal = theme != null ? theme.ButtonFrameColor : Cream;
            root.AddComponent<UiButtonFx>().Initialize(frame, normal, Gold);
        }

        private static GameObject InstantiateFitted(Transform parent, GameObject prefab, string name, Vector2 anchor,
            Vector2 pivot, Vector2 position, Vector2 size, out float scale)
        {
            GameObject go = Object.Instantiate(prefab, parent, false);
            go.name = string.IsNullOrEmpty(name) ? prefab.name : name;
            var rect = (RectTransform)go.transform;
            float designHeight = Mathf.Max(1f, rect.sizeDelta.y);
            scale = Mathf.Max(0.001f, size.y / designHeight);
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = pivot;
            rect.anchoredPosition = position;
            rect.sizeDelta = new Vector2(size.x / scale, designHeight);
            rect.localScale = new Vector3(scale, scale, 1f);

            // Clicks and hover need a raycast target on the root (Simple_01 has none: its label does not raycast).
            Graphic hit = go.GetComponent<Graphic>();
            if (hit == null)
            {
                Image clear = go.AddComponent<Image>();
                clear.color = new Color(0f, 0f, 0f, 0f);
                hit = clear;
            }
            hit.raycastTarget = true;

            foreach (Animator animator in go.GetComponentsInChildren<Animator>(true))
                animator.updateMode = AnimatorUpdateMode.UnscaledTime;
            return go;
        }

        /// <summary>Sets the prefab's label (the first TMP text not under an inactive child) and its size.</summary>
        private static TMP_Text SetPrefabLabel(GameObject root, string label, float scale, float fontSize)
        {
            TMP_Text text = null;
            foreach (TMP_Text candidate in root.GetComponentsInChildren<TMP_Text>(true))
            {
                if (text == null) text = candidate;
                if (ActiveUnder(candidate.transform, root.transform))
                {
                    text = candidate;
                    break;
                }
            }
            if (text == null) return null;
            float units = fontSize > 0f ? fontSize : text.fontSize * scale;
            units = Mathf.Max(MinLabelUnits, units) * TextScale;
            text.enableAutoSizing = false;
            text.fontSize = units / scale;
            text.alignment = TextAlignmentOptions.Center;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.overflowMode = TextOverflowModes.Overflow;
            text.raycastTarget = false;
            text.text = label;
            return text;
        }

        private static bool ActiveUnder(Transform t, Transform root)
        {
            for (; t != null && t != root; t = t.parent)
                if (!t.gameObject.activeSelf) return false;
            return true;
        }

        /// <summary>Centred TMP label for the code-built fallback buttons (TMP's default font).</summary>
        private static TMP_Text FallbackLabel(Transform parent, string label, Vector2 size, float fontSize)
        {
            RectTransform rect = Stretch(parent, "Label");
            var text = rect.gameObject.AddComponent<TextMeshProUGUI>();
            text.fontSize = Mathf.Max(MinLabelUnits, fontSize > 0f ? fontSize : size.y * 0.5f) * TextScale;
            text.color = Cream;
            text.alignment = TextAlignmentOptions.Center;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.raycastTarget = false;
            text.text = label;
            return text;
        }

        public static Text Label(Transform parent, string name, Font font, int size, Color color, TextAnchor alignment,
            Vector2 anchor, Vector2 pivot, Vector2 position, Vector2 rectSize, FontStyle style = FontStyle.Normal)
        {
            Text text = Rect(parent, name, anchor, pivot, position, rectSize).gameObject.AddComponent<Text>();
            // Theme: bold labels of size 14+ (headings, buttons, the Devil's lines) take the title face; the theme's
            // faces replace synthesized bold. Each face is scaled to read like the built-in font at the same size.
            UiTheme theme = Theme;
            float scale = 1f;
            if (theme != null)
            {
                if (style == FontStyle.Bold && size >= 14 && theme.TitleFont != null
                    && (font == theme.BodyFont || font == theme.TitleFont))
                {
                    font = theme.TitleFont;
                }
                if (font == theme.TitleFont)
                {
                    scale = theme.TitleFontScale;
                    style = FontStyle.Normal;
                }
                else if (font == theme.BodyFont)
                {
                    scale = theme.BodyFontScale;
                    style = FontStyle.Normal; // Markazi SemiBold is already heavy
                }
            }
            text.font = font;
            text.fontSize = Mathf.Max(6, Mathf.RoundToInt(size * scale * TextScale));
            // The built-in font has a single face: italics would be synthesized (slanted smooth text). Never use them.
            text.fontStyle = style == FontStyle.Italic ? FontStyle.Normal : style == FontStyle.BoldAndItalic ? FontStyle.Bold : style;
            text.color = color;
            text.alignment = alignment;
            text.supportRichText = true;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.raycastTarget = false;
            return text;
        }

        public static void AddShadow(Graphic graphic)
        {
            var shadow = graphic.gameObject.AddComponent<Shadow>();
            shadow.effectColor = new Color(0f, 0f, 0f, 0.85f);
            shadow.effectDistance = new Vector2(1f, -1f);
        }

        /// <summary>Makes a block clickable. Colours: normal/highlighted/pressed tint the block.</summary>
        public static Button MakeButton(Image block, Color highlighted)
        {
            block.raycastTarget = true;
            var button = block.gameObject.AddComponent<Button>();
            ColorBlock colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = highlighted;
            colors.selectedColor = highlighted;
            colors.pressedColor = highlighted * 0.85f;
            colors.colorMultiplier = 1f;
            colors.fadeDuration = 0.05f;
            button.colors = colors;
            button.targetGraphic = block;
            button.onClick.AddListener(NotifyClick);
            var navigation = new Navigation { mode = Navigation.Mode.None };
            button.navigation = navigation;
            return button;
        }

        /// <summary>Makes a block report pointer hold/press (see <see cref="UiHoldButton"/>); no Button, no tint.</summary>
        public static UiHoldButton MakeHoldButton(Image block)
        {
            block.raycastTarget = true;
            return block.gameObject.AddComponent<UiHoldButton>();
        }

        /// <summary>Adds an EventSystem with the Input System UI module when the scene has none. Call from Start.</summary>
        public static void EnsureEventSystem()
        {
            if (EventSystem.current != null) return;
            var go = new GameObject("EventSystem", typeof(EventSystem));
            var module = go.AddComponent<InputSystemUIInputModule>();
            module.AssignDefaultActions();
        }

        // ------------------------------------------------------------ shared input (keyboard + gamepad)

        /// <summary>Confirm: Enter / keypad Enter / gamepad South.</summary>
        public static bool ConfirmPressed()
        {
            Keyboard k = Keyboard.current;
            Gamepad g = Gamepad.current;
            return (k != null && (k.enterKey.wasPressedThisFrame || k.numpadEnterKey.wasPressedThisFrame))
                || (g != null && g.buttonSouth.wasPressedThisFrame);
        }

        /// <summary>Confirm held: Enter / keypad Enter / gamepad South.</summary>
        public static bool ConfirmHeld()
        {
            Keyboard k = Keyboard.current;
            Gamepad g = Gamepad.current;
            return (k != null && (k.enterKey.isPressed || k.numpadEnterKey.isPressed))
                || (g != null && g.buttonSouth.isPressed);
        }

        /// <summary>
        /// Skip/advance a line: confirm only (Enter / keypad Enter / pad South). Never fire, dodge or rocket keys,
        /// which the player is usually mashing when they die. Clicks are handled by the screen's own blocks.
        /// </summary>
        public static bool AdvancePressed() => ConfirmPressed();

        // ------------------------------------------------------------ DOTween helpers (core API only, unscaled time)

        /// <summary>
        /// Pops a block in: scale from x rest to rest (ease out back) and alpha 0 to 1 through a CanvasGroup (added
        /// when missing), after <paramref name="delay"/> seconds, on unscaled time (pause and the Devil run at
        /// timeScale 0). Kills an earlier pop on the same rect. ReduceMotion = shown at once.
        /// Example: <c>panel.SetActive(true); UiKit.PopIn(panelRect);</c>
        /// </summary>
        public static void PopIn(RectTransform rect, float delay = 0f, float from = 0.85f, float duration = 0.22f,
            float restScale = 1f)
        {
            if (rect == null) return;
            if (!rect.TryGetComponent(out CanvasGroup group)) group = rect.gameObject.AddComponent<CanvasGroup>();
            DOTween.Kill(rect);
            Vector3 rest = new Vector3(restScale, restScale, 1f);
            if (ReduceMotion || duration <= 0f)
            {
                rect.localScale = rest;
                group.alpha = 1f;
                return;
            }
            rect.localScale = rest * from;
            group.alpha = 0f;
            Sequence s = DOTween.Sequence();
            s.Insert(0f, rect.DOScale(rest, duration).SetEase(Ease.OutBack));
            s.Insert(0f, DOTween.To(() => group.alpha, a => group.alpha = a, 1f, duration * 0.8f));
            s.SetDelay(delay).SetTarget(rect).SetUpdate(true).SetLink(rect.gameObject);
        }

        /// <summary>
        /// A short scale punch on unscaled time (skipped with ReduceMotion); restores the rest scale first.
        /// Example: <c>UiKit.Punch(label.transform, Vector3.one);</c>
        /// </summary>
        public static Tween Punch(Transform t, Vector3 restScale, float amount = 0.12f, float duration = 0.3f)
        {
            if (t == null) return null;
            DOTween.Kill(t);
            t.localScale = restScale;
            if (ReduceMotion) return null;
            return t.DOPunchScale(restScale * amount, duration, 6, 0.6f).SetTarget(t).SetUpdate(true)
                .SetLink(t.gameObject);
        }

        /// <summary>Ease-out cubic.</summary>
        public static float EaseOut(float t)
        {
            t = 1f - Mathf.Clamp01(t);
            return 1f - t * t * t;
        }
    }
}
