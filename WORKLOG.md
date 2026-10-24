# Work Log

Every change to this project goes here — from people and from AI tools. Git shows *what* changed inside a file; this log records *who* changed it, *why*, and *where in the Unity Editor*. That matters most for changes git explains badly: edits to scenes, prefabs, and components; ProjectSettings; and local, uncommitted experiments.

## Rules

1. Add one entry per task or work session, **at the bottom** of the Entries section, before you commit. If nothing gets committed, add the entry anyway and mark it `uncommitted`.
2. Log local and uncommitted changes too. Update your entry when they get committed or thrown away.
3. For Unity Editor changes, give the full path: `scene or prefab › GameObject › component › fields (old → new)`. Example: `Assets/Scenes/SampleScene.unity › Player › Rigidbody › mass 1 → 2`.
4. Include component adds and removes, and every Rigidbody, collider, or physics change.
5. This log is append-only. Never edit or delete someone else's entry; to correct something, add a new entry.
6. Keep entries short and factual.

## Entry template

```
### YYYY-MM-DD — <person> (<tool: manual | Claude Code | Codex | Cursor | ...>) — branch <branch>
- Changed: <files, or scene › GameObject › component › fields>
- Why: <reason>
- Status: committed (<commit message>) | uncommitted | discarded
- Notes / risks: <optional>
```

## Entries

### 2026-10-23 — SefaSelim (Claude Code) — branch main
- Changed: added the Claude Code Game Studios toolkit to `.claude/` (agents, skills, rules, docs, scripts, hooks; Godot and Unreal specialists left out), `Docs/engine-reference/unity/`, `project.yaml`, `AGENTS.md`, `CLAUDE.md`, `MAIN_GDD.md` (empty skeleton) and this `WORKLOG.md`. `.gitignore` now keeps only personal Claude files out of git, so `.claude/` is shared.
- Why: the whole team uses the same AI agents, skills and project rules during the jam.
- Status: committed (Add Unity project at repo root and shared Claude Code toolkit)
- Notes / risks: the Unity project (Unity 6000.6.0f1, URP) moved from the `Dance with the Devil/` subfolder to the repo root, so the root is the project folder: re-add it in Unity Hub from the new path. The subfolder's own git repo (Unity Hub's "Initial check-in", remote `Dance-with-the-Devil-2026-10-23_22-13-27`) was dropped; this repo (`SefaSelim/dance-with-the-devil`) is the only one.

### 2026-10-24 — Burak (Claude Code) — branch claude/project-thread-yuc0gi
- Changed: Phase 0 of the implementation plan (setup and design).
  - `Packages/manifest.json`: added `com.coplaydev.unity-mcp` (MCP for Unity, git URL; installed by Burak in the Editor before this task), `com.unity.burst` 2.0.0, `com.unity.collections` 6.6.0, `com.unity.mathematics` 1.4.0 (were transitive, now explicit), `com.sandrofigo.voxreader` 5.0.6, and the OpenUPM scoped registry (scope `com.sandrofigo.voxreader`). `Packages/packages-lock.json` updated by Unity.
  - New folders `Assets/_Game/{Scripts/{Core,Player,Weapons,Enemies,Voxel,Devil,UI,Feel},Art/{Vox,Materials,Shaders},Audio,Prefabs,Scenes,Data,Editor,Tests/EditMode}`.
  - New assembly definitions: `Assets/_Game/Scripts/Game.Runtime.asmdef` (Burst, Collections, Mathematics, Input System, URP; unsafe allowed), `Assets/_Game/Editor/Game.Editor.asmdef` (Editor only, + VoxReader), `Assets/_Game/Tests/EditMode/Game.Tests.asmdef` (EditMode tests) with `PackageSmokeTests.cs`.
  - `project.yaml`: `engine` (Unity 6000.6.0f1, C#, URP, PhysX), `specialists` and `naming` blocks (setup-engine). `Docs/engine-reference/unity/VERSION.md`: pinned version fields.
  - `MAIN_GDD.md`: merged design written (vision and pillars, story, loop and death schedule, controls, weapons, five enemies, modules, six Devil contracts, arena, wave budget, endings and ledger, UI, look, scope) with 19 open questions (❓).
  - `ProjectSettings/PackageManagerSettings.asset` › m_ScopedRegistries ([] → package.openupm.com, scope com.sandrofigo.voxreader), written by Unity from the manifest.
  - `.vsconfig` (added by Unity/Visual Studio) committed as is.
- Why: Phase 0 of `IMPLEMENTATION_PLAN.md`; placeholder models for all art until final assets.
- Status: committed (Phase 0: packages, _Game folders and asmdefs, merged GDD)
- Notes / risks: no scene or prefab changes; the only ProjectSettings change is the scoped registry above. Unity MCP (CoplayDev) works over HTTP. Package resolution is clean (only "VoxReader installed without a signature" info). An MCP EditMode test run got stuck because the Editor was unfocused with an unsaved untitled scene open, which also held back the recompile; after Burak stopped it, VoxReader and Game.Tests compiled and the EditMode smoke tests passed 2/2 through MCP, with no console errors. No `.mcp.json` added: the MCP server is configured on Burak's machine.

### 2026-10-24 — Burak (Claude Code) — branch claude/project-thread-yuc0gi
- Changed: `MAIN_GDD.md` (jam deadline 2026-10-26 15:00 in §14 and A-19, Son güncelleme, Değişiklik geçmişi).
- Why: Burak gave the deliverable date.
- Status: committed (GDD: record jam deadline)

### 2026-10-24 — Burak (Claude Code) — branch claude/project-thread-yuc0gi
- Changed: `MAIN_GDD.md` §2, §3, §5, §8, §10, §11, §15: 13 open questions closed as decisions K-1…K-13 after Burak said "önerilerini uygula" (apply your recommendations); 9 stay open.
- Why: user design decision.
- Status: committed (GDD: apply recommended decisions)

### 2026-10-24 — Burak (Claude Code) — branch claude/project-thread-yuc0gi
- Changed: Phase 1 start (look and arena, placeholder art).
  - New `Assets/_Game/Scripts/Core/PixelCameraRig.cs`: game camera renders into a 642×362 RenderTexture (640×360 visible + 1-texel border, point filter, no MSAA), shown full screen by a runtime display camera + Screen Space Overlay RawImage with integer scaling, texel snapping, sub-texel uvRect offset and `ScreenPointToWorldRay` for mouse aim. With no target it holds a fixed focus point.
  - New `Assets/_Game/Editor/PlaceholderBuilder.cs`: menu `Dance/Placeholders/Rebuild All` builds the materials, prefabs and arena scene from the layout and palette tables (level-designer and art-director plans).
  - New assets: 17 flat materials `Assets/_Game/Art/Materials/mat_*_flat.mat` (URP Simple Lit), prefabs `Assets/_Game/Prefabs/char_defne_placeholder.prefab` (16 cubes) and `char_chaser_placeholder.prefab` (8 cubes), no colliders on the parts.
  - New scene `Assets/_Game/Scenes/Arena.unity`: Directional Light (soft shadows, rotation 50/75/0), flat ambient #3A3F5C, Arena (Floor 34×0.1×30 with BoxCollider; Wall_N/Wall_E 2 m and Wall_S/Wall_W 1 m with BoxCollider size (1,2,1) center (0,0.5,0); four L-block boxes 1.5 m with BoxColliders; cream edge strips without colliders; four entrance plates without colliders), SpawnPoints (4 empties), Player (Defne prefab at origin), Chaser_Preview (🧪), CameraRig › PixelCameraRig (ortho size 15, pitch 35, yaw 45, focus (1,0,1)) › GameCamera (Camera ortho, URP Base, post-processing off, AA none, AudioListener).
  - `Game.Runtime.asmdef` + `UnityEngine.UI`; `Game.Editor.asmdef` + URP core/universal runtime.
  - ProjectSettings: `EditorBuildSettings.asset` › scenes (Arena.unity added at index 0); `URPProjectSettings.asset` › m_ProjectSettingFolderPath added by Unity.
  - `MAIN_GDD.md` §9 and §13 status 🟡 and the built layout.
- Why: Phase 1 of the plan, minimum scope (K-11): RawImage upscale instead of a custom Render Graph pass.
- Status: committed (Phase 1: pixel camera rig, placeholder Defne/Chaser and block arena)
- Notes / risks: checked through MCP: console clean, play-mode screenshot, every wall/block/character base raycasts onto the floor at y 0.000, 0 collider overlaps. Not done yet in Phase 1: 1 px outline, toon shader, .vox importer and its EditMode test, panning jitter test (camera is fixed for now). `Assets/Scenes/aaaa.unity` is Burak's local scene and is not committed.

### 2026-10-24 — Burak (Claude Code) — branch claude/project-thread-yuc0gi
- Changed: Phase 2 (player, weapons, game feel).
  - New runtime code (gameplay-programmer agent): `Scripts/Core/{Damage,Health,TrainingDummy}.cs`, `Scripts/Player/{PlayerInputReader,PlayerController}.cs`, `Scripts/Weapons/{Projectile,ProjectilePool,Rifle,RocketLauncher,Machete,WeaponHub}.cs`. ProjectilePool: `ObjectPool` qualified as `UnityEngine.Pool.ObjectPool` (ambiguous with URP's).
  - Game feel (unity-vfx-game-feel skill): copied `ImpactEvent.cs`, `HitStop.cs`, `LightFlash.cs`, `PooledEffect.cs`, `SecondOrderDynamics.cs` into `Scripts/Feel/`; new `Scripts/Feel/PixelImpactFeedback.cs` (one ImpactEvent → flash cube, cube burst particle system, point light, camera trauma, global hitstop; profiles for muzzle, bullet, melee, explosion). `PixelCameraRig.cs`: border 1 → 4 texels, trauma shake as a uvRect offset (`AddTrauma`, `GlobalShakeScale`), no camera rotation.
  - New PlayMode test assembly `Tests/PlayMode/Game.Tests.PlayMode.asmdef` with `PlayerCombatTests.cs` (5 tests).
  - `PlaceholderBuilder.cs`: new materials `mat_vfx_{shot_player,rocket,machete}_flat` (Simple Lit, emissive) and `mat_vfx_{cubes,flash}_flat` (URP Particles/Unlit, URP Unlit); handles an already open Arena scene; wires the scene.
  - `Arena.unity` (rebuilt): Player › + CharacterController (center 0,0.9,0, height 1.8, radius 0.35, skin 0.05, step 0.3), Health (100, 0.6 s), PlayerInputReader, PlayerController (camera rig), Rifle/RocketLauncher (muzzle child at 0.37,1,0.45, projectile materials), Machete (MacheteSwing child with Blade cube), WeaponHub. Chaser_Preview removed; TrainingDummies › Dummy_0…3 (🧪, Chaser prefab + BoxCollider center 0,0.7,0 size 0.9,1.4,0.6, Health 30 / 0 s, TrainingDummy). New ImpactFeedback › PixelImpactFeedback (rig, cube and flash materials). `mat_chaser_eyes_flat.mat` re-saved by Unity.
  - `MAIN_GDD.md` §4, §5 status 🟡, feel notes, machete cooldown rule.
- Why: Phase 2 of the plan.
- Status: committed (Phase 2: player controller, rifle, rocket, machete and pixel game feel)
- Notes / risks: verified through MCP: console clean, PlayMode tests 5/5 passed, scripted shots in play mode (rifle −10 on a dummy, rocket killed dummies in 2.5 m), ~137 fps in the editor. Keyboard/mouse play was not driven by a script; Burak should try WASD, mouse, left click, E, F and Space. Player death does nothing yet (Phase 5 Devil flow). Sound hookup skipped: there are no SFX assets yet (Phase 6).
