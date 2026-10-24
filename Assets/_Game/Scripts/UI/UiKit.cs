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
    /// Text uses legacy uGUI Text: TextMesh Pro ships in com.unity.ugui 2.6 but its Essential Resources (default
    /// SDF font) are not imported and Game.Runtime does not reference Unity.TextMeshPro. No synthesized italics
    /// (the built-in font has one face); font sizes are even numbers (10, 12, 14, 16, 20, 24).
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

        /// <summary>The serialized font, else Unity's built-in LegacyRuntime font (has the Turkish glyphs).</summary>
        public static Font ResolveFont(Font preferred)
        {
            if (preferred != null) return preferred;
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
            Image outer = Box(parent, name, border, anchor, pivot, position, size);
            Image inner = Box(outer.transform, "Fill", fill, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                Vector2.zero, size - new Vector2(thickness * 2f, thickness * 2f));
            return inner;
        }

        public static Text Label(Transform parent, string name, Font font, int size, Color color, TextAnchor alignment,
            Vector2 anchor, Vector2 pivot, Vector2 position, Vector2 rectSize, FontStyle style = FontStyle.Normal)
        {
            Text text = Rect(parent, name, anchor, pivot, position, rectSize).gameObject.AddComponent<Text>();
            text.font = font;
            text.fontSize = Mathf.Max(6, Mathf.RoundToInt(size * TextScale));
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

        /// <summary>Ease-out cubic.</summary>
        public static float EaseOut(float t)
        {
            t = 1f - Mathf.Clamp01(t);
            return 1f - t * t * t;
        }
    }
}
