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
- Left column: title, subtitle, 4 buttons (Synty button prefab, see §3b), keyboard/gamepad navigation, first button
  selected; hover/select = prefab's selected state + small slide right; click sound through `UiKit.NotifyClick`.
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

## 3b. Buttons = Synty prefabs (2026-10-24, SefaSelim: prefabs used as-is, TextMesh Pro labels)
- `UiTheme.ButtonPrefab` = `HUD_DarkFantasy_Button_DialogueResponse_01` (Animator states: selected = lit plate,
  hover = arrow + highlight, press pop): menu Başla / Ayarlar / Emeği Geçenler / Çıkış, pause Devam, ending Tekrar dene,
  Devil card EVET.
- `UiTheme.SmallButtonPrefab` = `HUD_DarkFantasy_Button_Simple_01` (text only): Geri, Tam ekran value, pause Ana menü,
  Devil card HAYIR.
- Built by `UiKit.PrefabButton` / `PrefabHoldButton`: root scaled uniformly to the button height (1080p-authored prefab
  in the 640×360 frame), label in canvas units (min 6 = ~18 px at 1080p), Animators on unscaled time (pause).
  No theme/prefab = the code-built plate.
- Small button: highlight/select tint raised to Gold (the prefab's 4% tint did not show keyboard focus); a transparent
  hit area is added (its label does not raycast).
- EVET (K-2): prefab plates tinted bright red, gold hold bar along the bottom, word + smaller key hint in one label;
  hold logic unchanged (`UiHoldButton`, the prefab Button never clicks or takes focus). HAYIR: small text button, dim
  label (brighter on hover).
- Pause and ending buttons keep navigation None: their keys (Enter = Devam, R = Tekrar dene) stay as before.

## 3c. Minimal buttons, motion and world popups (2026-10-25, SefaSelim)

- Buttons: `HUD_DarkFantasy_Button_WorldSpace_Item_01` for main and small buttons (dark 50% parchment bar, centred
  Markazi label) under a thin `Frame_Box_Small_01` frame. Focus (keyboard/pad selection or mouse hover) = gold frame +
  gold label + a small scale punch. `UiKit` moves the prefab's Button from "Content" to the root.
- Motion (DOTween, unscaled time, all skipped with ReduceMotion): menu column pops in item by item; settings, credits
  and pause panels pop in (scale 0.85 → 1 + fade); the Devil leans in, his bubble and card pop, EVET pops on his hand;
  the ending screen pops in and the ledger lines appear one by one; the offer banner slides in.
- Devil screen: the hand-drawn Devil fills the right side (sized to the image aspect); EVET (with its stakes line)
  rides on his reaching fingertip and follows his lean and sway (the sway calms while the pointer is on EVET); the card
  moved left; HAYIR stays small and dim at the card's bottom centre.
- HUD: the top-right combo counter is gone. Combo shows as world popups over Defne ("KOMBO ×N" every 5, bigger
  "KOMBO 25!" with the multiplier at each tier) and "KRİTİK! N" where a crit lands (max 12 on screen, priority
  crit > tier > combo); the score line shows the live multiplier ("Puan 1200 ×1,5") above ×1,0. The offer banner is a
  small 232×40 corner banner under the score (one-line whisper, "+"/"−" lines that shrink to fit, lifetime bar). Five
  module slots (62×20, spacing 68) fit between the pips and "Küçük hayırlar".
- Menu backdrop: the engraving sits on a screen-space canvas behind the 3D stage, full view height for any aspect,
  feathered edges (no visible rectangle).

## 3d. 2D menu stage and the drawn ending portrait (2026-10-25, SefaSelim)

- Menu: no 3D stage any more (Defne model, plinth, sigil, candelabra and lights removed; the camera only clears).
  One screen-space camera canvas behind the menu UI (1920×1080, height matched): Doré engraving (dim red, breathing) →
  red glow behind the characters (pulsing) → fog wisps → back embers → `devil_player.png` (Defne with the Devil, black
  keyed, full height, bottom-anchored at 64% of the width: clear of the left column at 4:3, 16:9 and 21:9, breathes
  ±0.8% and sways ±0.45° around its bottom edge) → front embers → low fog. Embers = ~54 pooled UI squares (orange,
  spark, red cinder, ash) rising with wind and wobble. ReduceMotion = still picture, no embers or fog.
- Ending: Defne's drawn portrait (`defne_portrait.png`, square crop, dark vignette) replaces the block portrait when
  present. Costs: contract 1 Rengi = half drained (grey copy at 55%), 4 Yüzü = dark veil over the eyes, 5 Zamanı =
  darker frame and image, 6 Kendisi = fully grey; always-yes = grey shell that falls apart as 8×8 tiles
  (ReduceMotion: it just disappears).
- Ending layout: every block stacks by its real wrapped height; the ledger is 504 wide and sized to its rows (row =
  the taller of its two cells, 11 pt); "Küçük hayırların" and "Bir daha dene" sit below the panels; if it still would
  not fit above the score line the row font shrinks by up to 2, then the content scales down from the top.

## 4. Accessibility (minimum)
Keyboard + mouse + gamepad navigation on the menu; screen shake slider (0 = off) feeds `PixelCameraRig.GlobalShakeScale`;
no colour-only meaning added.
