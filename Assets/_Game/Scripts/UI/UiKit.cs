using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace DanceWithTheDevil
{
    /// <summary>
    /// Code-built uGUI helpers for the jam UI (no prefabs needed). Canvases are Screen Space Overlay with a
    /// 640×360 reference resolution, the same grid as the pixel camera, so blocks stay chunky.
    /// Text uses legacy uGUI Text: TextMesh Pro ships in com.unity.ugui 2.6 but its Essential Resources (default
    /// SDF font) are not imported and Game.Runtime does not reference Unity.TextMeshPro.
    /// </summary>
    public static class UiKit
    {
        public static readonly Vector2 ReferenceResolution = new Vector2(640f, 360f);

        /// <summary>Accessibility: skip slide/fade animations (no settings menu yet).</summary>
        public static bool ReduceMotion;
        /// <summary>Accessibility: multiplies every font size at build time (no settings menu yet).</summary>
        public static float TextScale = 1f;

        // Palette (placeholder, GDD §13: dark cold base, cream edge, plum hair, petrol overalls, turquoise detail).
        public static readonly Color Ink = new Color(0.06f, 0.05f, 0.08f, 1f);
        public static readonly Color Panel = new Color(0.08f, 0.06f, 0.09f, 0.92f);
        public static readonly Color Cream = new Color(0.93f, 0.88f, 0.74f, 1f);
        public static readonly Color Dim = new Color(0.5f, 0.48f, 0.5f, 1f);
        public static readonly Color Turquoise = new Color(0.25f, 0.8f, 0.75f, 1f);
        public static readonly Color DevilRed = new Color(0.78f, 0.1f, 0.12f, 1f);
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

        public static RectTransform CreateCanvas(Transform parent, string name, int sortingOrder, bool interactive)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = sortingOrder;
            canvas.pixelPerfect = true;
            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = ReferenceResolution;
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
            if (interactive) go.AddComponent<GraphicRaycaster>();
            return (RectTransform)go.transform;
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

        /// <summary>A block with a 2-unit border: returns the inner block (child of the border block).</summary>
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
            text.fontStyle = style;
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
            var navigation = new Navigation { mode = Navigation.Mode.None };
            button.navigation = navigation;
            return button;
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

        /// <summary>Skip/advance a line: confirm, Space, E, left click.</summary>
        public static bool AdvancePressed()
        {
            if (ConfirmPressed()) return true;
            Keyboard k = Keyboard.current;
            Mouse m = Mouse.current;
            return (k != null && (k.spaceKey.wasPressedThisFrame || k.eKey.wasPressedThisFrame))
                || (m != null && m.leftButton.wasPressedThisFrame);
        }

        /// <summary>Ease-out cubic.</summary>
        public static float EaseOut(float t)
        {
            t = 1f - Mathf.Clamp01(t);
            return 1f - t * t * t;
        }
    }
}
