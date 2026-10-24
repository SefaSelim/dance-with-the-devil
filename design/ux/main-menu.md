# UX Spec — Main Menu + HUD restyle (Dance with the Devil)

Status: Approved by SefaSelim's request 2026-10-24 (no review gates: "bana soru sorma"). Terse density.
Assets: `Assets/Synty/InterfaceDarkFantasyHUD` (sprites, fonts Pirata One + Markazi, both with Turkish glyphs).
UI system: uGUI built in code through `UiKit` (as today); look comes from one `UiTheme` asset.

## 1. Theme contract (shared by HUD and menu)
- `UiTheme` ScriptableObject at `Assets/_Game/Resources/UiTheme.asset`, loaded with `Resources.Load<UiTheme>("UiTheme")`.
- Fields: `TitleFont` (Pirata One TTF), `BodyFont` (Markazi SemiBold TTF), `PanelSprite` (9-slice frame),
  `PanelFillSprite`, `ButtonSprite`, `ButtonHighlightSprite`, `BarFrameSprite`, `BarFillSprite`, `SlotSprite`,
  `DividerSprite`, `VignetteSprite`.
- `UiKit.ResolveFont` → theme body font; `UiKit.TitleFont` → theme title font; `UiKit.Framed` and button blocks use the
  theme sprites (sliced) when present, the flat colours otherwise (no theme = today's look).
- Palette stays: Cream text, DevilRed for the Devil/costs, Gold accents; panels darker and warmer (aged iron/parchment).

## 2. Main menu (new scene `MainMenu`, build index 0; `Arena` becomes 1)
Wireframe (1920×1080 reference, scales):
```
┌──────────────────────────────────────────────────────────────┐
│ [Devil engraving, full height, right-of-centre, red-tinted, vignette]   │
│                                   ┌───────────────┐          │
│   BİR ŞEY OLMAZ  (title, Pirata)  │               │          │
│   Dance with the Devil (sub)      │  Defne 3D,    │          │
│                                   │  idle anim,   │          │
│   ▸ Başla                         │  rim/key lit  │          │
│     Ayarlar                       │               │          │
│     Emeği Geçenler                └───────────────┘          │
│     Çıkış                                                    │
│                      "Hayır demeyi öğrenmelisin."  (footer)  │
└──────────────────────────────────────────────────────────────┘
```
- Left column: title, subtitle, 4 buttons (Synty button sprite, Markazi), keyboard/gamepad navigation, first button
  selected; hover/select = highlight sprite + small slide right; click sound through `UiKit.NotifyClick`.
- Centre-right: Defne (`char_defne.prefab`) standing on a dark plinth, aiming idle, slow turntable sway (±8°), lit by a cold
  key light and a warm red back light; Devil engraving behind her on a world-space quad, multiplied red, slow breathing scale.
- Background: dark gradient + Synty vignette + slow ash particles.
- **Başla** → fade to black (0.4 s) → load `Arena`.
- **Ayarlar** (panel, Synty frame): Ana ses, Müzik, Efektler (sliders 0–100), Ekran sarsıntısı (slider 0–100),
  Tam ekran (toggle). Saved in PlayerPrefs, applied immediately and at game start. "Geri" closes.
- **Emeği Geçenler** (panel): team line + CREDITS.md summary (assets and licences). "Geri" closes.
- **Çıkış** → `Application.Quit()` (stops play mode in the Editor).
- Esc closes an open panel; in the game, the pause menu's "Çık" returns to the main menu.

## 3. HUD restyle (GameHud and the other code-built screens)
- Same layout and data as today (health top-left, timer top-centre, score/verdiklerin, module slots + reject/dodge/rocket
  readiness bottom, Devil card, ending screen, pause).
- Health: Synty bar frame + red fill; lost-max segment kept. Panels/cards: Synty frame. Slots: Synty slot sprite.
- Fonts: body Markazi, headings and the Devil's lines Pirata One.
- Readability: text ≥ 18 px at 1080p, every state still has a non-colour cue (icons/labels as today).

## 4. Accessibility (minimum)
Keyboard + mouse + gamepad navigation on the menu; screen shake slider (0 = off) feeds `PixelCameraRig.GlobalShakeScale`;
no colour-only meaning added.
