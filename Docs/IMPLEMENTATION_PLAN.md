# Bir Şey Olmaz / Dance with the Devil: Implementation Plan

## Context
For Women Game Jam (theme: "You must learn to say no"), the team wrote a story-less laser/module arena GDD (`Downloads/Bir_Sey_Olmaz_GDD.md`). The user then described a new vision on top of it:
- An isometric 3D shooter with a voxel, slightly pixelated look.
- An assault rifle, a machete and a rocket launcher.
- Everything that gets hit, enemies and the environment alike, bursts into its pixels and scatters.
- Each time the player dies, the Devil leans in from the edge of the monitor and offers a deal: "I give you an upgrade, I take something from you."
- The deals get steadily worse. In the end the player realises that saying "no" was the right choice.

**User decisions:**
- **The two designs merge.** The GDD's in-combat accept/reject/eject mechanic stays, and those offers now come from the Devil.
- **Saying "No" at death is possible and unlocks the true ending.**
- **A run lasts about 5 minutes.**

**Project state:**
- The project is currently an empty URP template: Unity 6000.6.0f1, Input System, Linear colour space.
- Burst, Collections and Mathematics are in the PackageCache as dependencies. VFX Graph is not installed.
- A Unity MCP relay ships with `com.unity.ai.assistant` (`%USERPROFILE%\.unity\relay\relay_win.exe --mcp`). It is not connected yet. Unity marks it deprecated; the fallback is the `unity:unity-cli` skill.

---

## 1. Merged design (written into MAIN_GDD.md in Phase 0)

**Loop (~5 min):**
1. The arena follows GDD §9: 32×28 m, two L-shaped walls, four entrances.
2. Waves get harder quickly, so the player inevitably dies.
3. Time freezes and the screen desaturates. **The Devil leans in from the right edge of the screen, outside the monitor frame.**
4. **The contract:** "I give you X, I take Y."
5. **YES:** the player revives, a pixelating shockwave from the Devil blows up nearby enemies, and the player gets the upgrade plus a permanent cost.
6. **NO:** the run ends and leads to the true ending.

**Death schedule:**
- Difficulty forces the deaths. Enemy health stays fixed; numbers and the type mix grow. The GDD §10 budget logic is kept.
- Target deaths at about 0:50, 1:40, 2:30, 3:15 and 4:00, which gives 5 contracts.
- At the 6th death the Devil **only takes** ("Last deal: whatever you have left").

**Small offers (GDD §6–7, kept as written):**
- During combat the Devil's "whispers" arrive as floating modules: Triple Shot, Echo, Density.
- Accept by holding contact for 0.45 s. Reject with a right-click push, which throws the module and makes it explode. Eject with Q + right-click.
- These are **reversible** yeses. Death contracts are **irreversible**.
- That contrast is the theme's mechanical lesson: "You learned the small no's but never questioned the big yeses."

**Contract curve:** upgrades shrink while costs grow. The curve is fixed and authored for the story, not random.

| # | Devil's promise (upgrade) | Cost (debuff), visible and mechanical |
|---|---|---|
| 1 | Rocket damage/radius ×2, fire rate +40% | **Your colour**: the world desaturates 30%, and the colour drains from the character's hair |
| 2 | Machete wave (forward strike), +1 dodge charge | **Your voice**: the character's sounds and one music layer go silent; the health bar shrinks (max HP −20) |
| 3 | Piercing rifle rounds | **Your friends**: the small photo/portrait on the HUD disappears; enemies are 15% faster |
| 4 | +10% damage | **Your face**: voxels permanently fall off the character model (real voxel removal); max HP −20 |
| 5 | +1 rocket ammo | **Your time**: no invulnerability after reviving, dodge cooldown ×2, the screen edges darken |
| 6 | — | **Yourself**: the character turns grey and controls lag, leading to an inevitable end |

**Endings (narrative-director + writer):**
- **No at the 1st death:** "You lost, but you stayed yourself." The shortest and warmest ending, with the full-colour character.
- **No at deaths 2–5:** she is as diminished as the number of yeses she gave, but still standing. What she lost is listed.
- **Always yes (6th death):**
  - The Devil says: "Nothing happened, right?"
  - The grey shell crumbles.
  - The end card reads "Saying no was also an option", with "Try again" to start a new run.
- **Results screen:** instead of a score it shows a **"What you gave / What you got"** ledger. The GDD §10 score stays at the bottom.

**Story frame (proposal):**
- **The heroine is Defne**, as described in GDD §13: big blocky curly hair, round glasses, petrol-coloured overalls, turquoise details.
- She says yes to everything every night, telling herself "nothing will happen" ("bir şey olmaz").
- The arena is the inside of her exhaustion, and the enemies are the piled-up demands (demons / voxel creatures).
- The Devil speaks to **the player**, not the character, from the edge of the monitor. The player gives the yeses; Defne pays the price.
- The `writer` agent writes a one-line intro and the Devil's lines, in Turkish.

**Controls (adapted from GDD §5):**

| Input | Action |
|---|---|
| WASD | Move |
| Mouse | Aim |
| Left click | Rifle |
| E / middle click | Rocket (cooldown/charges) |
| F | Machete (close strike, usable any time) |
| Space | Dodge |
| Right click | Reject push |
| Q + right click | Eject |
| R | Restart |

**Enemies (GDD §8 plus three new types):**
- Chaser and Shooter keep the GDD values.
- **Runner**: fast, low health.
- **Exploding imp**: closes in and explodes, also breaking the environment.
- **Shielded brute**: armoured from the front; beat it with the rocket, from behind, or with the machete.
- Visuals: voxel demons and creatures.

## 2. Technical architecture (from research)

### Look (Phase 1)
- An orthographic isometric camera renders to a low-resolution render texture (640×360, point filtering, MSAA off).
- A URP 17 **Render Graph** `ScriptableRendererFeature` upscales it.
- t3ssel8r-style texel snapping plus a sub-texel offset stops jitter while the camera pans.
- A 1 px outline compares depth and normals at low resolution.
- A flat-shaded toon-lit voxel shader.
- No Compatibility-mode APIs (`Execute`/`OnCameraSetup`).

### Voxel data pipeline
- **VoxReader** (MIT, OpenUPM `com.sandrofigo.voxreader`) reads `.vox` files.
- Our own `ScriptedImporter` produces:
  - a greedy mesh for display;
  - a `VoxelModel` ScriptableObject with the surface-shell voxel list (position + colour), ready at load time.
- Characters are **part-based** voxels (head, torso, arms and legs separate) with procedural animation: walk bob, aiming, recoil.
- No Mixamo account or login is needed, and breaking individual parts becomes easy.

### Destruction (Phase 3): Burst Jobs + `Graphics.RenderMeshIndirect`

**`VoxelDebrisSystem`, a single scene service:**
- Struct-of-arrays `NativeArray`s: pos, vel, size, packed colour, age, state.
- Fixed capacity, swap-remove, zero runtime allocation.

**Each frame:**
1. An `IJobParallelFor` (Burst) applies gravity and ground-plane bounce/friction, and checks a 2D occupancy grid (`NativeArray<byte>`) for the arena walls. No PhysX.
2. Settled cubes move to a separate **static instance buffer** that is not simulated. The shader shrinks them out over 3–6 s.
3. A second job writes matrix + colour into a `GraphicsBuffer`.
4. One `RenderMeshIndirect` call draws everything:
   - a shared 24-vertex cube;
   - an instanced unlit/simple-lit shader;
   - shadows off for debris;
   - wide `worldBounds`.

**Budgets:**
- 8,000 dynamic plus 20,000 static cubes.
- At most 400 cubes per kill and 1,500 spawned per frame; the excess is queued to later frames.
- A ring buffer recycles the oldest cubes first.

**LOD from the impact point:**
- Inside the impact radius cubes break at 1×1×1. Outside it they merge into 2³ or 4³ cubes with the averaged colour.
- Velocity is a radial push from the impact point, plus the projectile direction, plus spread.
- The rocket uses a large radius, the machete a planar slice push, the bullet a small hole.

**Environment:**
- Swap at the prop level. Each wall or prop has a greedy mesh and a voxel grid.
- On a hit a sphere is carved out and the removed voxels go to the debris system.
- The prop is re-meshed by a Burst greedy-mesh job, at most once per frame (`Mesh.MeshData`). Its collider is a simple box set.
- Small props turn entirely into debris.
- The outer boundary wall can't be destroyed, so the GDD bounce boundary is kept.
- The inner L walls and props can be destroyed. **How much of the inner wall blocks can break gets tested for balance in Phase 4.**

**VFX Graph** is used only for cosmetic sparks and smoke. It is optional; ParticleSystem is enough.

### Game feel
- Use the `.claude/skills/unity-vfx-game-feel` skill and its bundled scripts, copied to `Assets/_Game/Feel/`:
  - camera and impact: `TraumaShake`, `HitStop`, `ImpactEvent`/`ImpactFeedbackRouter`, `SecondOrderDynamics`;
  - weapon: `WeaponRecoil`, `MuzzleFlash`, `BladeTrail`;
  - effects: `LightFlash`, `DecalPool`, `PooledEffect`, `VFX_ParticleUnlit.shader`.
- Every hit fires a single `ImpactEvent`. On the same frame it triggers VFX, debris, light, shake, hitstop and sound.

### Folder structure
- `Assets/_Game/{Scripts/{Core,Player,Weapons,Enemies,Voxel,Devil,UI,Feel},Art/{Vox,Materials,Shaders},Audio,Prefabs,Scenes,Data}`
- Assembly definitions: `Game.Runtime`, `Game.Editor`, `Game.Tests`.

## 3. Assets (free, licences checked)

| Need | Source | Notes |
|---|---|---|
| Weapons | **padadu Voxel Pack 03 – Modern Weapons** (CC0) | ak47, rpg_32. The machete and rocket projectile are generated `.vox` files from code, since there is no free CC0 machete. |
| Enemies | **kooow Voxel Creature Pack 1** (CC0) + **55 Voxel Creatures** (CC0) | Pack 1 includes "blue daemon", goblin and others. |
| Environment | **Felix Strefter Dungeon Voxel Assets** (CC0, .vox/.glb) | Recoloured brown-red. |
| Crates | **Kenney Blaster Kit** (CC0) | |
| Heroine | **Kenney Mini/Blocky Characters** (CC0) | If they have no suitable woman, an editor script generates a **part-based voxel Defne** from the GDD description. That is the likely main path, not a fallback. |
| Devil reference PNG | **Nerveona Demon/Devil** (CC0) | The team will replace it with their own drawing; the `DevilPresenter` sprite is swapped from one field. |
| SFX | **Kenney Impact / Sci-fi / RPG Audio** (CC0) | |
| Music | **"Dark Things Loop"** (CC0) | |

- Downloads go under `Assets/_Game/ThirdParty/<pack>/`.
- Every pack's source and licence goes into `CREDITS.md`.

## 4. Phases

Every phase:
1. works on its own branch;
2. is tested through MCP, and bugs are fixed in the same phase;
3. is pushed, merged into main and tagged `vX.Y.0`;
4. updates WORKLOG and MAIN_GDD.

### Phase 0: Setup and design

**Scope:**
- Unity MCP connection (`.mcp.json`, relay `--project-path`).
- Packages: Burst, Collections, Mathematics, VoxReader.
- Folders and asmdefs; pin Unity 6000.6 in `project.yaml` via `setup-engine`.
- Write the merged GDD into `MAIN_GDD.md`: story, contract table, endings and lines.

**Agents / skills:**
- `creative-director`: theme and pillar check.
- `game-designer` + `systems-designer`: merged rules and contract curve.
- `narrative-director` + `writer`: story, lines and endings.
- `producer` + `scope-check`: 5-minute scope.
- `setup-engine` skill.

**Exit test:**
- MCP: compiles with no console errors and packages resolve.
- Open GDD ❓ questions are listed.

### Phase 1: Look, voxel pipeline and arena

**Scope:**
- Pixel render feature with snapping and outline, the isometric camera, the toon voxel shader.
- Download the assets; build the `.vox` importer and the `VoxelModel` SO.
- The Defne voxel model.
- The GDD §9 block arena, with destructible voxel inner walls.

**Agents / skills:**
- `unity-shader-specialist` + `technical-artist`: pixel pass and shader.
- `art-director` + `art-bible` skill: palette and readability.
- `unity-specialist`: Render Graph API check.
- `level-designer`: arena layout.
- `asset-audit` skill.

**Exit test:**
- MCP: a play-mode screenshot shows crisp pixels and no jitter while panning; the console is clean.
- EditMode test: the importer's voxel count and colours are correct.

### Phase 2: Player, weapons and game feel

**Scope:**
- Movement, aiming and dodge using the GDD §5 values.
- Rifle at 6 shots/s; rocket with charges/cooldown and area damage; machete arc strike.
- Muzzle flash, trail, recoil, hitstop, trauma shake, light flash, decals.
- Health, damage and invulnerability.

**Agents / skills:**
- `gameplay-programmer`: controller and weapons.
- `unity-vfx-game-feel` skill: the design → timing → layer process for each weapon.
- `technical-artist`.
- `sound-designer`: hooking up placeholder SFX.

**Exit test:**
- MCP: trigger firing, rockets and the machete from a script in play mode; check the console, measure FPS, take a screenshot sequence.
- PlayMode test: damage, cooldowns, dodge i-frames.

### Phase 3: Voxel destruction

**Scope:**
- `VoxelDebrisSystem` (Burst + RenderMeshIndirect).
- Impact LOD and moving settled cubes to the static buffer.
- Environment carving with Burst greedy remesh.
- The enemy death burst, wired to `ImpactEvent`.

**Agents / skills:**
- `unity-dots-specialist`: Jobs, Burst, NativeArray.
- `engine-programmer`: render buffers, mesh API.
- `performance-analyst` + `perf-profile` skill: budgets.
- `unity-vfx-game-feel`: how the explosions feel.
- `code-review` skill.

**Exit test:**
- MCP stress test: blow up 100 enemies at once with a rocket. Profiler frame time must stay ≤16.6 ms with 0 GC alloc.
- EditMode tests: pool capacity and ring buffer, greedy mesh correctness.

### Phase 4: Enemies and waves

**Scope:**
- Five enemy types, keeping the GDD §8 attack tells.
- Spawn safety: at least 9 m from the player, with a 0.8 s warning marker.
- A wave budget manager and a difficulty curve that forces the death schedule.
- NavMesh (`ai.navigation` is already installed).

**Agents / skills:**
- `ai-programmer`: behaviours and NavMesh.
- `game-designer`: enemy types.
- `systems-designer` + `balance-check` skill: death timing at ~0:50 / 1:40 / …
- `team-combat` skill: design → implement → validate.
- `qa-tester`.

**Exit test:**
- MCP: an automated "bot player" (stands still and shoots) simulates runs; death times are logged.
- The console is clean.

### Phase 5: Devil, contracts and story

**Scope:**
- In-combat Devil modules with accept/reject/eject, exactly as GDD §6–7.
- Death freezes time. `DevilPresenter` brings the PNG in, leaning from the edge of the monitor.
- The contract card: upgrade/cost and YES/NO.
- Data for six contracts (`DealDefinition` SO).
- Visible costs: desaturation, a muted audio layer, a removed HUD element, **voxels falling off Defne**.
- The endings, the "What you gave" ledger and the intro text.

**Agents / skills:**
- `systems-designer`: contract data.
- `narrative-director` + `writer`: lines and ending texts.
- `ui-programmer` + `unity-ui-specialist` + `ux-design` skill: contract screen and HUD.
- `unity:ui` skill: choosing the UI system.
- `team-narrative` skill.

**Exit test:**
- MCP: pick each death/contract branch from a script (YES×6, NO@1, NO@3). Check the right ending plays, with screenshots showing the debuffs applied.
- PlayMode test: module accept/reject/eject rules.

### Phase 6: Audio, music and art pass

**Scope:**
- SFX and music integration.
- AudioMixer, including muting a layer for cost #2.
- Final art placement, the Devil PNG placeholder, `CREDITS.md`.

**Agents / skills:**
- `team-audio` skill: `audio-director`, `sound-designer`.
- `unity:audio-setup-mixers`.
- `art-director`, `asset-audit`.

**Exit test:**
- MCP: the console is clean and the mixer groups are correct.
- The team gets a manual listening checklist.

### Phase 7: Polish, performance and delivery

**Scope:**
- Tutorial hints (GDD §11), readability, performance, debugging, a Windows build.

**Agents / skills:**
- `team-polish` skill, `perf-profile`.
- `team-qa` + `smoke-check`.
- `bug-report` / `bug-triage`, `code-review`.

**Exit test:**
- MCP: three full runs (always yes / early no / late no).
- The build is made and run, and a performance report is written.

### Parallel work
- Phase 2 (player and weapons) and the core of Phase 3 (`VoxelDebrisSystem`, tested with fake impacts) touch different files, so they can run in parallel.
- Phase 5's UI can start while Phase 4 is finishing.
- The main session does the Unity Editor work; the agents produce plans and code, per the AGENTS.md rule.

## 5. Verification (every phase)

1. **Unity MCP (relay):**
   - After compiling, read the console.
   - Enter play mode and run the scenario with a test script (`Game.Tests` plus C# run through MCP).
   - Take a screenshot, read the console and frame time, then exit.
   - If anything fails, fix it in the same phase and run it again.
2. **Test Framework:**
   - EditMode: importer, greedy mesh, debris pool, contract curve, damage maths.
   - PlayMode: weapon cooldowns, module rules, contract branches, endings.
3. **Performance gate (Phase 3 onward):** a stress scene must stay ≤16.6 ms with 0 runtime GC.
4. **Push:** `git push origin <branch>` → merge into main → tag. Turning old branches into tags needs the user's approval, per the branch-to-tag rule.

## 6. Manual steps for the team
- The first time, with the Unity Editor open, **accept the MCP connection in the Editor's approval window** (Project Settings › AI › MCP).
- After packages are imported, wait for the Editor to recompile. Restart Unity if needed.
- The team supplies the final Devil drawings and, if wanted, Defne's final model. Each placeholder is swapped from one place.

## 7. Risks
- **Unity MCP is deprecated.** If it doesn't work, the same tests run through `unity:unity-cli`.
- **The Render Graph pixel pass may hit API changes in Unity 6000.6.** It will be checked against `Docs/engine-reference/unity` with `unity-specialist`.
- **Six contracts may be too tight for 5 minutes.** `balance-check` will tune the death intervals; if needed we drop to 5 contracts.
- **Dominant strategies:** the all-yes / all-no dominant-strategy test from GDD §15 is kept.
