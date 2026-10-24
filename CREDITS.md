# Credits

## Team

The Dance with the Devil team, Women Game Jam 2026.

## Third-party code and tools

| What | Source | Licence | Where |
|---|---|---|---|
| VoxReader | Sandro Figo, OpenUPM `com.sandrofigo.voxreader` | MIT | Package (`.vox` reading) |
| MCP for Unity | CoplayDev, `com.coplaydev.unity-mcp` | MIT | Editor-only tooling |
| Claude Code Game Studios toolkit | `.claude/` (release 1.1.3) | MIT, see `.claude/CCGS-LICENSE` | Development tooling |
| Game-feel helpers (`ImpactEvent`, `HitStop`, `LightFlash`, `PooledEffect`, `SecondOrderDynamics`) | `unity-vfx-game-feel` skill in `.claude/skills/` | Same as the toolkit | `Assets/_Game/Scripts/Feel/` |

## Art and audio

- Defne's model: `Assets/Character/female+soldier+3d+model` (a Tripo AI model with a Mixamo rig), added by the team.
  Licence/terms: to be confirmed by the team (Tripo export terms).
- Defne's animations: `Assets/Character/Basic Shooter Pack` (Adobe Mixamo animation pack), added by the team. Mixamo
  animations are free to use in games under Adobe's terms.
- Weapons (AK74, RPG7 and its rocket): `Assets/Low Poly Weapons VOL.1`, added by the team. Source and licence: to be
  confirmed by the team (looks like a free Unity Asset Store pack; the Asset Store EULA allows use in games).
- Enemies: Synty Studios POLYGON - Dark Fantasy characters (SM_Chr_Demon_Male_01, SM_Chr_Skeleton_01,
  SM_Chr_Witch_Female_01), built by `Dance/Enemies/Build Enemy Models`; they move with the Basic Shooter Pack clips until
  the team's animation pack is added. Cube enemies remain as the fallback when the Synty pack is not imported.
- Buttons: Synty INTERFACE - Dark Fantasy HUD button prefabs (DialogueResponse_01, Button_Simple_01) with TextMesh Pro.
- TextMesh Pro Essential Resources (LiberationSans SDF, OFL), from Unity's com.unity.ugui package.
- Arena dressing (walls, floor, props, particles) and the main menu's candelabra: Synty Studios POLYGON - Dark Fantasy,
  `Assets/PolygonDarkFantasy`, added by the team (Synty / Unity Asset Store licence: use in games allowed, no
  redistribution of the source assets).
- UI frames, bars and buttons: Synty Studios INTERFACE - Dark Fantasy HUD, `Assets/Synty/InterfaceDarkFantasyHUD`, added
  by the team (same licence terms).
- Fonts: Pirata One (Rodrigo Fuenzalida, Nicolas Massi) and Markazi Text (Borna Izadpanah, Florian Runge), SIL Open Font
  Licence 1.1, shipped inside the Synty HUD pack (`Fonts/*/OFL.txt`).
- Particle textures (fire, smoke, sparks, dirt, scorch): Kenney Particle Pack 1.1 by Kenney Vleugels (kenney.nl), CC0,
  `Assets/_Game/ThirdParty/KenneyParticlePack` (converted to white + alpha, see its License.txt). Particle shader
  `VFX_ParticleUnlit` from the `unity-vfx-game-feel` skill (toolkit licence).
- All sound effects and the music loop are synthesized by `Dance/Audio/Bake All` (no external assets).
- The Devil on the Devil screen: hand-drawn by the team (`Assets/Devil-handdraw.jpeg`; keyed copy
  `Assets/_Game/Art/UI/devil_handdraw.png` made by `Dance/UI/Build Devil Art`).
- DOTween (Demigiant, free version), `Assets/Plugins/Demigiant/DOTween`: UI tweens. Free for commercial use under
  Demigiant's licence (dotween.demigiant.com/license.php).
- Main menu Devil (placeholder): Gustave Doré, illustration of Satan for Milton's *Paradise Lost* (1866), public domain,
  via Wikimedia Commons; `Assets/_Game/Art/UI/devil_placeholder_dore.jpg`.
