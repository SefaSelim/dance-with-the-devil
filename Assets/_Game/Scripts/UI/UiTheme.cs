using UnityEngine;

namespace DanceWithTheDevil
{
    /// <summary>
    /// The UI look in one asset (design/ux/main-menu.md §1): Synty Interface Dark Fantasy HUD frames and the Pirata One /
    /// Markazi fonts. Loaded from Resources/UiTheme by <see cref="UiKit.Theme"/>; when it is missing every screen falls
    /// back to the flat blocks and the built-in font. Built by Dance/UI/Build Theme.
    /// </summary>
    [CreateAssetMenu(menuName = "Dance/UI Theme", fileName = "UiTheme")]
    public sealed class UiTheme : ScriptableObject
    {
        [Header("Fonts (TTF, legacy uGUI Text)")]
        [Tooltip("Headings, buttons, the Devil's lines: bold labels of size 14 and up use it.")]
        public Font TitleFont;
        [Tooltip("Everything else.")]
        public Font BodyFont;
        [Tooltip("Markazi's small x-height reads smaller than the built-in font at the same size.")]
        public float BodyFontScale = 1.3f;
        public float TitleFontScale = 1.15f;

        [Header("Sprites (sliced)")]
        [Tooltip("Ornate frame over panels (transparent centre).")]
        public Sprite PanelSprite;
        [Tooltip("Pixels per canvas unit for PanelSprite's border: higher = thinner frame.")]
        public float PanelSpriteScale = 14f;
        [Tooltip("Plain filled plate under panel content; tinted by the panel colour.")]
        public Sprite PanelFillSprite;
        public float PanelFillSpriteScale = 14f;
        [Tooltip("Menu button plate.")]
        public Sprite ButtonSprite;
        public float ButtonSpriteScale = 10f;
        [Tooltip("Small square frame (slots, small boxes).")]
        public Sprite SlotSprite;
        public float SlotSpriteScale = 9f;
        [Tooltip("Ornament line under titles.")]
        public Sprite DividerSprite;
        [Tooltip("Soft red glow (menu backdrop).")]
        public Sprite GlowSprite;
        [Tooltip("Sigil ring behind Defne on the menu.")]
        public Sprite SigilSprite;
        [Tooltip("Edge tendrils (menu vignette).")]
        public Sprite VignetteSprite;

        [Header("Button prefabs (Synty, TextMesh Pro labels; fitted by UiKit.PrefabButton)")]
        [Tooltip("Main text button: menu entries, Devam, Tekrar dene, EVET (HUD_DarkFantasy_Button_WorldSpace_Item_01: " +
                 "a dark parchment bar with a centred label; UiKit moves its Button from the child \"Content\" to the root).")]
        public GameObject ButtonPrefab;
        [Tooltip("Small text button: Geri, Tam ekran, Ana menü, HAYIR (same minimal prefab, smaller label).")]
        public GameObject SmallButtonPrefab;
        [Tooltip("Thin outer frame drawn over every prefab button (SPR_DarkFantasy_Frame_Box_Small_01, border 90).")]
        public Sprite ButtonFrameSprite;
        [Tooltip("Sprite pixels per button-local unit of the frame border: higher = thinner frame.")]
        public float ButtonFrameScale = 6f;
        [Tooltip("Frame colour at rest; focus (selected or hovered) turns it and the label gold.")]
        public Color ButtonFrameColor = new Color(0.62f, 0.58f, 0.52f, 0.75f);

        [Header("Devil art")]
        [Tooltip("Hand-drawn Devil for the Devil screen (black keyed out; built by Dance/UI/Build Devil Art). " +
                 "DevilScreen uses it when its own sprite field is empty.")]
        public Sprite DevilSprite;

        [Tooltip("Panels smaller than this keep the flat frame (a 10-unit pip cannot carry an ornate border).")]
        public Vector2 MinFramedSize = new Vector2(40f, 30f);
    }
}
