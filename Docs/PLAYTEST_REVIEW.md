# Dance with the Devil — Review, Test & Improvement Log

This file follows the work on branch `claude/project-thread-yuc0gi` (Burak's Claude Code session) commit by
commit. Each commit is pulled into a separate worktree (`C:\Users\Selim\Desktop\DwtD-review`), compiled and tested
in Unity 6000.6.0f1 batchmode, and checked against the user's instructions and `Docs/IMPLEMENTATION_PLAN.md`.
Fixes are tried on a local branch only (`review/fixes`), never pushed onto the working branch, and written down
here so the working session can apply them.

Sections:
1. Deviations from the user's instructions
2. Per-commit review and test results
3. Bugs and fixes (what, which commit, why, how it was approached and solved)
4. Improvement ideas for the theme
5. Research: pixel destruction (look and performance)
6. Research: game feel
7. Research: custom character pipeline
8. (reserved)
9. Deep research: audio, Devil presentation, enemy AI, delivery
10. Deep research: pixel-art rendering in URP 17
11. Deep research: destruction implementation details
12. Final polish prompt

---

## 1. Deviations from the user's instructions

What the user asked for (their own words, summarised) vs the **final pushed state (4105a49, also merged to
`main`)**.

| # | User asked for | Final branch state | Severity |
|---|---|---|---|
| D-1 | Destruction is the core: an enemy hit by a rocket **breaks into its pixels from the exact point it was hit**, pixels scatter, the rest flies apart as whole pieces; **the whole environment is destructible**; optimised (Jobs etc., research the best way). | Decision K-11 ("Minimum" scope) kept destruction as optional. Implemented: `ShatterOnDeath` + `PixelImpactFeedback.Shatter` cut every cube part of a dying model into equal coloured cubes in one frame (one Shuriken ParticleSystem, World collision, shrink out in 1.2–2.4 s). **Not** from the hit point outward, **no** chunks, **no** micro-pixels, debris does **not** stay, **no** Jobs/Burst/indirect draw, **no** environment destruction at all (`Scripts/Voxel` is empty, walls are plain BoxColliders). | **Critical** — the user's headline request is missing. Sections 5 and 11 hold the full design to build it. |
| D-2 | "Find the models on the internet from free assets; if nothing fits, get the effect with a shader." Devil reference PNGs from the internet. | **No external asset at all.** `CREDITS.md`: every model is placeholder cubes (`Dance/Placeholders/Rebuild All`), the Devil is "coloured blocks", every sound and the music are synthesized by code (`Dance/Audio/Bake All`). VoxReader is installed but unused (no `.vox` importer). | **High** |
| D-3 | Look like the reference: "3D, slightly pixelated", chunky pixel brawler, earthy browns; the heroine is a woman in that style. | Low-res RT + point upscale + texel snap ✅. No outline, no toon shading, no palette; colours are the cold placeholder set; Defne is 16 untextured boxes. | **High** (first impression for jam judges) |
| D-4 | Enemies get much harder over time, **different enemy types**. | 3 of 5 types (Chaser, Runner, Shooter). Imp and shielded Brute were cut by K-11; their budget is replaced by Chasers/Runners/Shooters. | Medium |
| D-5 | Every phase pushed and **tested with Unity MCP**; bugs found in tests fixed. | Tested through CoplayDev MCP on Burak's machine (PlayMode 18/18, EditMode 5/5 — confirmed independently here in batchmode). Nobody played it by hand or listened to it (WORKLOG). No `.mcp.json` in the repo; MCP package unpinned (`#main`). | Low |
| D-6 | A linear story behind the theme; upgrades shrink, costs grow, the player dies weakened and realises "no" was right; NO at death unlocks the true ending. | Implemented: 6 contracts, every visible cost, 3 endings, ledger, "Psst" intro, NO lines. Cost #4 is a stand-in (4 parts hidden instantly, 2 of them hair, nothing falls). Always-YES ending fires at death **7** (provisional A-4), GDD §11 says death 6. | OK (two small gaps) |
| D-7 | Rifle, machete, rocket launcher; perfect game feel. | All three weapons, muzzle flash, light flash, hitstop, trauma shake. Missing from the research checklist (section 6): directional kick, spread, recoil, shell casings, rocket trail/ring/anticipation, machete smear, enemy squash/knockback on hit, player-hurt shake, low-HP feedback, HP ghost bar. | Medium |

---

## 2. Per-commit review and test results

### d27780a — Phase 0: packages, _Game folders and asmdefs, merged GDD
- Packages: Burst 2.0.0, Collections 6.6.0, Mathematics 1.4.0, VoxReader 5.0.6 (OpenUPM scoped registry), MCP for Unity
  (CoplayDev, git `#main`).
- Empty folders were committed as `.meta` files only (`Data`, `Scripts/Devil`, `Scripts/Enemies`, `Scripts/UI`,
  `Scripts/Voxel`). On a fresh clone Unity warns "A meta data file exists but its folder can't be found" for each.
  Fix: add a `.gitkeep` in each, or delete the orphan metas until the folder is used.
- `ProjectSettings.asset` gained `APP_UI_EDITOR_ONLY` (from the AI Assistant package). Harmless.

### f2c0ce1, 5f2c777, 6aded88 — WORKLOG / GDD decisions
- GDD is thorough and well structured. K-1…K-13 recorded. Note D-1 above (K-11 scope).
- Open questions that block later phases: A-4 (6th death), A-9 (rocket ×2 meaning), A-11b (modules at death),
  A-12 (inner walls breakable — should be **yes**, see D-1).

### f791524 — Phase 1: pixel camera rig, placeholder Defne/Chaser, block arena
- `PixelCameraRig`: 640×360 (+4 texel border) RT, point filter, integer upscale with letterbox, texel snapping with
  sub-texel uvRect offset, shake as an image offset. Solid implementation of the t3ssel8r idea.
- Missing from the Phase 1 plan: 1 px outline, toon shader, `.vox` importer + EditMode test, panning-jitter test.

### 3b00fb4 — Phase 2: player controller, rifle, rocket, machete, pixel game feel
- Code quality is good: no per-frame allocation in weapons, pooled projectiles, cached delegates, sphere-cast
  bullets with pierce, one `ImpactEvent` bus.
- Bugs found: see section 3 (B-1 … B-5).
- Test status: see "Test runs" below.

### 36c3e0b — Phase 3: shatter dying targets into coloured cubes
- `ShatterOnDeath` reads each part's `_BaseColor`, `PixelImpactFeedback.Shatter` sizes cubes from part volume and
  emits up to 400 per death (cap 1500 per frame; later kills that frame get 16 coarse cubes). Session's MCP stress
  test: 100 kills in one frame → 2988 cubes, 9.6 ms that frame, editor ~113 fps after.
- Gaps vs the user's request: see D-1. Perf notes: up to 400 separate `Emit()` calls per death (0.2–0.5 ms main
  thread) — one `SetParticles` per death is cheaper; World collision is the main cost as counts grow.
- Bug: thin parts round up to more cubes than the volume estimate, the budget runs out, and the parts listed last
  (often head/legs) never burst (B-11).

### 1fbbfcd — Phase 4: Chaser, Runner, Shooter, wave director
- NavMeshAgent enemies, pooled, telegraphs, 0/4/8 s sub-groups, spawn safety (≥ 9 m, shift entrance), shooters from
  ≤ 2 entrances per 3 s, active cap 3–9. Window budgets match GDD §10 after the imp/brute conversion.
- Bot measurement (stand-and-shoot, 12 deaths in 5 min, first at 1:07) → schedule not balanced; needs human tests.
- Bugs: B-6 (off-NavMesh spawn blocks the cap), B-7 (unstable sort shuffles the interleave), B-8 (every pooled
  shooter prewarms its own 16-projectile pool on first shot → hitch), B-9 (shooter line of sight sees through walls
  when enemies fill the 16-hit buffer; everything is on layer 0 with `~0` masks), B-10 (RunBot + Devil flow both
  react to the player's death if RunBot is enabled).

### 8905ce2 — Phase 5: Devil contracts, costs, endings, HUD, whisper modules
- Death → `HitStop.CancelAll()` → timeScale 0 → Devil slides in → card (big bright EVET [E], small dim HAYIR [H]) →
  revive with 6 m shockwave / ending. **B-2 is fixed here** (CancelAll before pausing; HitStop refuses to start at
  timeScale 0). Module rules match GDD §7 exactly. Restart reloads the scene and resets statics.
- Bugs: B-12 (E = rocket key = YES key), B-13 (enemy bullets survive the revive shockwave; after contract 5 the very
  revive has no i-frames → instant re-death), B-14 (contract 5 vignette darkens the spawn warnings), B-15 (HUD reject
  indicator guesses the cooldown from raw right-clicks), B-16 (Devil shockwave kills give +100 score each),
  B-17 (stale contract-2 audio flags for one frame with domain reload off), B-18 (R restart only on the ending
  screen; `RestartPressed` is read nowhere), ledger joined on one line (GDD asks one per line).

### a947561 — Phase 6: synthesized SFX, two-stem music, audio hooks
- `AudioBaker` writes ~30 WAVs + a D-minor 90 BPM two-stem loop from code; `GameAudio` pools AudioSources per bus
  (no AudioMixer asset). Contract 2 mutes voice + fades melody (K-8) ✅. Nobody has listened to it yet.
- Note: no mixer means no DevilFreeze low-pass snapshot (section 9.1); per-sound pan flag is ignored.

### 6aa61b4, 4105a49 — Phase 7: tutorial hints, HUD fixes, QA checklist, Windows build
- First-minute hints ✅, `Docs/QA_SMOKE_CHECKLIST.md` (good, Turkish, blocking items), Windows build 79 MB,
  ran 25 s without errors. Build settings re-serialized by Unity (PC_RPAsset prefiltering, GraphicsSettings).
- Not done (session's own list): build frame-time measurement, human playtest, listening pass, death-schedule
  balance, outline + toon shader, `.vox` importer, imp and brute.

### Test runs (Unity 6000.6.0f1 batchmode, review worktree)

| Commit | EditMode | PlayMode | Compile | Notes |
|---|---|---|---|---|
| 3b00fb4 | ✅ 2/2 | ✅ 5/5 | ✅ no errors | matches the session's MCP result |
| 4105a49 (final) | ✅ 5/5 | ✅ 18/18 | ✅ no errors | 6× "Failed to create agent because it is not close enough to the NavMesh" — from `EnemyWaveTests` (test scene has no NavMesh), test noise only |
| review/fixes 817b796 | ✅ 5/5 | ✅ 18/18 | ✅ no errors | after the fixes in section 3 |

---

## 3. Bugs and fixes

Fixes were made on the local branch `review/fixes` in the review worktree (2 commits on top of 4105a49: `ced4cff`,
`817b796`), tested in batchmode (EditMode 5/5, PlayMode 18/18), and exported as **`Docs/review-fixes.patch`**.
They are **not pushed**. Apply with `git am Docs/review-fixes.patch` on the working branch.

| # | Introduced | Bug | Why it happens | Status |
|---|---|---|---|---|
| B-1 | 3b00fb4 | **Bullets and rockets miss what the cursor points at, most visibly up close.** | `WeaponHub` fires from `MuzzlePosition` (0.37 m right, 0.45 m forward of the body) along `AimDirection`, the body-centre→cursor direction. The shot flies parallel to the cursor line, 0.37 m beside it; an enemy (capsule r 0.35) under the cursor's edge at 2 m is missed (bullet r 0.12). | **fixed** (`ced4cff`) |
| B-2 | 3b00fb4 | A hitstop active when the Devil pauses could unpause the game. | `HitStop` restores its saved timeScale with unscaled time. | **fixed by the session in 8905ce2** (`CancelAll()` before the pause; no hitstop starts at timeScale 0) |
| B-3 | 3b00fb4 | Impact/shatter cubes use Shuriken World collision. | CPU raycasts per particle; won't scale to the micro-pixel destruction. | design — replace with section 5/11 system |
| B-4 | 3b00fb4 | Rocket explosion damages through walls. | `OverlapSphere` with no line-of-sight. Inner walls can't break yet, so the wall stays but the damage goes through. | **fixed** (`817b796`) |
| B-5 | 3b00fb4 | Misplaced XML doc in `PixelCameraRig`. | Editing slip. | **fixed** (`817b796`) |
| B-6 | 1fbbfcd | An enemy spawned off the NavMesh never moves but counts toward the active cap for the whole run; with cap 3 the first window can stall. | ±0.75 m jitter with no `NavMesh.SamplePosition`; the agent fails to attach, `HasNavMesh` stays false. | **fixed** (`817b796`, snap to NavMesh within 1.5 m) |
| B-7 | 1fbbfcd | Interleaved enemy types in a sub-group get shuffled (shooters back-to-back). | `List.Sort` is unstable above 16 items; later windows have 21–23 + backlog. | **fixed** (`817b796`, stable insertion sort) |
| B-8 | 1fbbfcd | Hitch when a fresh pooled shooter fires its first shot. | Each shooter owns a `ProjectilePool` that prewarms 16 `CreatePrimitive` cubes on first use. | open — one shared enemy-orb pool in the scene |
| B-9 | 1fbbfcd | Shooter thinks it has line of sight through a wall when enemies fill the line. | `RaycastNonAlloc` (16 slots, unsorted, mask `~0`); enemy capsules crowd out the wall hit. The orb still stops at the wall. | open — physics layers: Player, Enemy, Wall, Prop, Debris; real masks everywhere |
| B-10 | 1fbbfcd | RunBot revives the player inside the same `Died` event the Devil uses. | Two owners of the death flow. | open (RunBot is off in the scene) — guard or document |
| B-11 | 36c3e0b | Thin parts eat the cube budget; the last parts (head/legs) never burst. | Cube size estimated from volume, but each axis rounds up to ≥ 1 cube. | open — count cubes with the same rounding when choosing size |
| B-12 | 8905ce2 | **A panicked rocket tap signs a contract.** | E is rocket, skip-line and YES; only a 0.45 s lock guards the card. | **fixed** (`817b796`: E counts as YES only after it stayed up 0.4 s while the card is shown; Enter / click / gamepad unchanged) |
| B-13 | 8905ce2 | Instant re-death after revive, especially after contract 5. | Shockwave kills enemies but not their bullets frozen in the air; contract 5 removes i-frames already on its own revive. | open — despawn enemy projectiles within 6 m on revive, keep ≥ 0.2 s grace even after contract 5 (GDD decision) |
| B-14 | 8905ce2 | Contract 5 darkens the spawn warnings (GDD §8 says they draw on top). | Vignette applied to the whole game image. | open — draw warnings on an ungraded overlay |
| B-15 | 8905ce2 | HUD reject indicator shows a cooldown that didn't start (dodging, dead, on the Devil card). | HUD guesses from raw right-click. | open — read `PlayerModules.RejectReady` |
| B-16 | 8905ce2 | Dying earns score (+100 per enemy killed by the Devil's shockwave). | `KillWithin` feeds `ScoreKeeper`. | open |
| B-17 | 8905ce2 | Editor only: contract-2 audio state from the last play session leaks for a frame. | Domain reload off; statics reset in Awake order. | open — reset run statics in `[RuntimeInitializeOnLoadMethod(SubsystemRegistration)]` |
| B-18 | 8905ce2 | R does nothing during play (GDD §4 lists it; A-15 provisional = ending screen only). | `RestartPressed` read nowhere. | open (design) |

### How the fixes were approached
- **B-1** — Confirmed by reading `WeaponHub` + scene muzzle offset (0.37, 1, 0.45) and checking the geometry
  (capsule r 0.35 + bullet r 0.12 < 0.37 + margin). Fix: aim each weapon from its own muzzle to
  `PlayerController.AimPoint` (flattened); fall back to the body aim when the cursor is within 1 m of the muzzle or
  behind it, so the line can't swing wildly. Triple Shot spreads around the given direction, so it follows for free.
- **B-4** — Reused the machete's pattern (`IsBlockedByWall`): a raycast from the blast point (nudged off the
  surface by the normal) to each target's collider centre; any non-damageable, non-owner collider in between
  shields it. The target is only marked "damaged" after the check, so a second collider of the same enemy with a
  clear line still counts.
- **B-6** — `NavMesh.SamplePosition(position, 1.5 m)` after the jitter; the original point is kept if the sample
  fails (tests run without a NavMesh). Despawning off-mesh enemies was rejected: it would break the wave tests.
- **B-7** — The queue is short (≤ ~40), so a stable insertion sort costs nothing and keeps the per-group type
  interleave. A sequence field was the alternative.
- **B-12** — Raising the lock alone doesn't stop repeated taps (the key is released between taps). The fix arms E
  only after it has been *continuously up* for 0.4 s while the card is shown; any press resets the timer.
- All fixes: compiled clean; EditMode 5/5, PlayMode 18/18 (`review/fixes` 817b796). Not covered by a new test:
  B-1, B-4 and B-12 deserve PlayMode tests (cursor-hit at 2 m; enemy behind a wall survives a rocket; E-tap on the
  card does not raise `YesPressed`).

---

## 4. Improvement ideas for the theme

References: Inscryption (a jam game whose bargains trade your body for an edge and nudge you to stop playing,
[essay](https://agardenofstories.substack.com/p/inscryption)), Slay the Spire's Neow bargains and visible curses,
DDLC's short fourth-wall moments, and dark-pattern research (preselected options, "decline" as a link,
confirmshaming: [taxonomy](https://arxiv.org/pdf/2402.16760), [study](https://arxiv.org/pdf/2104.12653)).
The contract card itself can **teach the dark patterns that make people say yes** — that is the theme in mechanics.
Tone: accepting is understandable, never stupid; the villain is the pressure, not the player.

Cheap additions, ordered by value / cost (none of them changes the GDD's decided rules; each needs a user OK
before it goes into `MAIN_GDD.md`):
1. **EVET is pre-selected and focused**; Enter / click-anywhere accepts. HAYIR needs a deliberate click. (Fits K-2.)
2. **HAYIR is a small grey text link**, not a button; it gets slightly smaller each death but never unreadable,
   and always works.
3. **Confirmshaming copy on HAYIR**, escalating: "Hayır, arkadaşlarını korumak istemiyorum." — readable as
   manipulation, not as mocking the player.
4. **"Bir şey olmaz" refrain**: the Devil ends every offer with it; the true ending answers it ("Oldu.").
5. **A countdown that is fake pressure**: a ring timer on the card runs out and nothing happens; the offer just
   waits. The urgency was invented.
6. **After HAYIR, the Devil's pity line** ("Senin için yaptıklarımdan sonra mı?") — then silence; nothing bad happens.
7. **The Devil remembers**: "Geçen sefer ne çabuk evet demiştin." — track accept count and decision time (cheap).
8. **Fine print**: the mechanical cost line is small and low-contrast, readable on hover/zoom (K-3 still holds: it
   is shown, just designed the way real contracts are).
9. **The Devil leans further in each death**; his overlay covers a little more screen edge (pairs with cost #5).
10. **Combat offers arrive at bad moments** (mid-wave, when overwhelmed) — pressure arrives when you're tired.
11. **Costs live on Defne's body** (already in the GDD) — add a slower idle and a hunched pose after cost 4–5.
12. **Ending stats**: deals accepted, total seconds spent deciding, "Hayır deme sayın: 1. Yetti."
13. **Post-credits (true ending)**: Defne in full colour at base power, an empty monitor edge, one line:
    "Hayır diyebilirsin." No moralising toward players who accepted everything.
14. **Title / menu**: the menu's "Başla" button is the Devil's first offer in disguise (pre-selected), with a tiny
    "Hayır" that quits — a 2-second foreshadow.

## 5. Research: pixel destruction (look and performance)

Goal from the user: **very small pixels spray out of the exact hit point, and the rest of the body flies apart as
whole chunks**, with the feel of a 2D pixel game whose world shatters (Broforce / Noita style), without frame drops.
Numbers marked [est] are engineering estimates, not from a source.

### 5.1 What makes Broforce / Noita / Teardown destruction feel good
- **Noita** simulates almost every pixel; rigid bodies are pixel groups that are re-shaped (and can split into
  several bodies) when pixels are destroyed ([80.lv](https://80.lv/articles/noita-a-game-based-on-falling-sand-simulation/);
  primary source: GDC 2019 "Exploring the Tech and Design of Noita", Petri Purho,
  [talk list](https://noita.wiki.gg/wiki/Official_Interviews_and_Videos)). That "pixels + whole chunks" mix is
  exactly the user's request.
- **Teardown**: every voxel volume has an 8-bit palette; broken pieces inherit the parent palette
  ([Voxagon blog](https://blog.voxagon.se/)). Do the same: chunks and pixels index the enemy's own palette.
- **Vlambeer screenshake**: one Nuclear Throne pistol shot kicks the camera 6 px opposite the aim and adds to a
  fast-decaying shake ([summary](https://infovore.org/?p=5275)).
- **Hitstop**: best felt around 0.1–0.2 s; slow-motion 0.2–0.4 s
  ([SciTePress 2024](https://www.scitepress.org/Papers/2024/124614/124614.pdf)); shake the victim during the freeze
  ([Sakurai](https://sourcegaming.info/2015/11/11/thoughts-on-hitstop-sakurais-famitsu-column-vol-490-1/)).

**Kill timeline to aim for [est]:**
| Time | What happens |
|---|---|
| Frame 0 | 1-frame white silhouette on the enemy; impact pixels spawn |
| Hitstop | 50 ms normal kill, 100–150 ms elite, 200 ms + 0.3 s slow-mo on player death |
| Shake | trauma decaying over ~0.25 s + 2–4 low-res-pixel kick along the bullet direction |
| Impact pixels | 40–150 per hit, 300–800 per death; 4–12 m/s in a 60–90° cone around the bullet/reflected direction + ~20% isotropic; life 0.4–1.2 s, randomised |
| Chunks | 3–8 per death, each 15–30% of the body; 2–5 m/s away from the hit, 5–15 rad/s spin; bounce 1–2 times, then lie on the floor 3–6 s (lingering debris is a big part of the genre's feel) |
| Colour | first 2–3 frames hot (white → yellow), then the body colour or a darker blood/material shade |
| Extras | 2–4 dithered smoke puffs (rise + grow over 0.5 s); ~5% of pixels are emissive embers with longer life |
| Sound | three layers: instant "crack", mid "crunch", tail clatter (one ticking sample on the first 3–5 chunk bounces, with cooldown) |

### 5.2 Technique: tiny pixels at the hit + whole chunks for the rest
**Import time (editor script, recommended for the jam):**
1. Keep every enemy (and Defne) as a voxel grid (`.vox`, e.g. 16×24×16).
2. Pick K = 4–8 seed voxels (spread out, biased toward head/limbs), assign each voxel to its nearest seed
   (voxel Voronoi), flood-fill each cell (6-connectivity) and merge stray islands into a neighbour, so every
   chunk is one connected piece.
3. Greedy-mesh each chunk as its own sub-mesh, pivot at its centre of mass; store a voxel → chunk table.

**Runtime, on death:**
1. Hit point → model space; take every voxel within r = 2–3 voxels of it.
2. Turn those into impact pixels and **subdivide each voxel into 2×2×2 (3×3×3 for rockets) micro-cubes** with the
   voxel colour — that is what makes the pixels "very small", smaller than the model's own voxels.
3. Remove the carved voxels from their chunks; if a chunk lost > 50%, turn the rest of it into pixels too.
4. Optional: Burst flood fill + re-greedy-mesh only the touched chunks (< 0.1 ms on < 10k voxels).

**Chunk physics:** no PhysX. One Burst job: rigid integration + ground plane / arena heightfield using a few OBB
corner points; restitution 0.3, friction 0.6, angular damping on the ground; settled after < 0.2 m/s for 0.3 s →
static buffer → shrink out. Cap 256 live chunks, recycle oldest. Draw with pooled `MeshRenderer`s or
`RenderMeshInstanced` per chunk mesh. (If PhysX is needed: one BoxCollider per chunk, cap ~64.)

### 5.3 Performance with very many tiny pixels
- Burst SoA + indirect draw is comfortable at 20k–50k live pixels; upload ≈ 32 B/pixel/frame (≈1.6 MB at 50k) [est].
- **Draw pixels as camera-facing quads, not cubes.** At ~1 texel per pixel a quad looks identical to a cube:
  `Graphics.RenderPrimitivesIndirect` with 6 vertices per instance and positions read from a `StructuredBuffer` by
  `SV_InstanceID` ([docs](https://docs.unity3d.com/6000.0/Documentation/ScriptReference/Graphics.RenderPrimitivesIndirect.html)).
  2 triangles instead of 12, no mesh. Keep real cubes only for "big pixels" > 2 texels and for chunks.
- 100k+ pixels: move the simulation to a compute shader (dead-list/alive-list append buffers,
  `GraphicsBuffer.CopyCount` into the indirect args, no CPU readback; floor at y = 0 + optional heightfield).
  ~0.1–0.3 ms GPU for 200k [est]. For the jam, Burst up to ~30k is the lower-risk path.
- Budget [est]: pixels ~0.5 ms CPU + 0.2 ms GPU, chunks ~0.1 ms.

### 5.4 Pixel-art-correct particles under the low-res camera
- In the particle vertex shader snap the centre to the texel grid (clip → texel coords → `floor + 0.5`) and expand
  the quad by whole texels → no shimmer. Size pixels in texels (1/2/3), not metres.
- **Fade with a 4×4 Bayer ordered-dither `clip()`, not alpha**: pixels stay opaque, write depth, need no sorting.
  Shrink in steps at end of life (3 → 2 → 1 texel).
- Colour from a palette LUT by the voxel's palette index; never blend.
- The project's `PixelCameraRig` already snaps the camera in texels, so this matches
  ([Texel Splatting](https://sotaverified.org/papers/260314587), [UPixelator docs](https://github.com/Radivarig/UPixelator_Documentation)).

### 5.5 URP / indirect-draw pitfalls
- `RenderParams.worldBounds` culls the whole batch (shadow casters too): set it to the arena bounds.
- Custom buffer-reading shaders need `DepthOnly` (and `DepthNormals` for an outline/SSAO) passes, else the pixels
  vanish from depth — the planned depth/normal outline would then ignore them. Shadows off for 1-texel pixels, on
  for chunks.
- SRP Batcher doesn't apply to indirect draws (expected). Calling `Graphics.Render*Indirect` from `Update` lands in
  URP's normal passes; only use a Render Graph pass if custom ordering is needed
  ([URP RG compute](https://docs.unity3d.com/6000.6/Documentation/Manual/urp/render-graph-compute-shader.html)).
- An unlit palette shader with a half-Lambert term from the main light is cheaper than URP Lit and fits the style.

### 5.6 Recommended architecture for this project
| Part | Choice |
|---|---|
| Import | `.vox` → greedy-meshed chunk sub-meshes + voxel→chunk table (editor script, VoxReader is already installed) |
| Death | carve sphere at the hit into 2×2×2 micro-pixels → pixel ring buffer; rest → chunks with impulse from the hit; white flash + hitstop + shake + layered sound in the same `ImpactEvent` |
| Pixels | Burst SoA ring buffer, cap 32k, `RenderPrimitivesIndirect` texel-snapped quads, dither fade, no shadows |
| Chunks | Burst rigid job, ground/heightfield collision, cap 256, settle → static → shrink, shadows on |
| Environment | same chunk/pixel path per prop; inner walls carve + re-mesh (Burst greedy), outer wall unbreakable |
| Bullet hits on living enemies | a few micro-pixels from the hit point every shot (chip damage visible on the body, optional voxel removal) — sells "everything is made of pixels" before the kill |

## 6. Research: game feel

Sources: Jan Willem Nijman, "The Art of Screenshake"
([summary](https://infovore.org/?p=5275), [recreation devlog](https://dkliao.itch.io/the-art-of-screenshake-recreation/devlog));
Squirrel Eiserloh, "Juicing Your Cameras With Math", GDC 2016
([summary](https://www.gamedeveloper.com/programming/video-sprucing-up-cameras-with-math)); Jonasson & Purho,
"Juice it or lose it"; hitstop ([Capcom SFV](https://game.capcom.com/cfn/sfv/column/131545),
[Unity primer](https://uhiyama-lab.com/en/notes/unity/unity-game-feel-hit-feedback/)). Values marked [rec] are
starting points to tune; 60 fps frames ("f").

**Nijman's order of effects** (each one is cheap; together they make the game): muzzle flash → faster bullets →
less accuracy → impact effect → hit reaction → enemy knockback → **permanence** (debris/casings stay) → camera lerp
→ screenshake → hit pause → weapon recoil.

| Area | Target | Current branch (3b00fb4) |
|---|---|---|
| Rifle hitstop | none, or 1f on a kill (auto-fire must not stutter) | none ✅ |
| Machete hitstop | 4–6f per hit, 8f on a kill | 0.06 s ✅ (add kill variant) |
| Rocket hitstop | 6–8f + 1–2f full-screen flash | 0.1 s ✅, no screen flash ⬜ |
| Shake | trauma² model; rifle +0.08/shot, machete hit +0.2, rocket +0.6, player hurt +0.4; decay 1.5–2.5/s | trauma² ✅, rifle 0.03×0.15 (too low to read), decay 1.6 ✅, no player-hurt shake ⬜ |
| Directional kick | 0.1–0.2 m (2–4 low-res px) opposite the aim per shot, springs back in ~80 ms (Nuclear Throne: 6 px) | ⬜ only random noise shake; add a directional component |
| Camera lead toward aim | 20–30% of cursor offset, cap ~3 m, smoothed, option to turn off | ⬜ (fixed camera now) |
| Muzzle flash | 1–2f, random rotation/scale 0.8–1.2, point light 2f | ✅ flash + light |
| Bullets | 2–3× real size, stretched along velocity, ±3–5° spread | stretched ✅; **no spread** ⬜ (Nijman: less accuracy = more feel) |
| Enemy hit | 2–3f white flash (MaterialPropertyBlock), squash 1.15→1 over 100 ms, knockback 0.3–0.5 m rifle / 1–1.5 m machete | dummy flash ✅, no squash ⬜, no knockback for rifle/machete ⬜ |
| Death | shatter + impulse along hit direction + **debris that stays** | ⬜ (section 5) |
| Player recoil | 0.05 m per rifle shot, 0.5–1 m push back from the rocket | ⬜ |
| Shell casings | sideways ~3 m/s, persist, pool ~200 | ⬜ (cheap, high value: permanence) |
| Rocket | 100–150 ms anticipation (gun lifts + charge sound), smoke trail, ring 0→4 m over 0.25 s, chromatic pulse 0→0.8→0 over 0.3 s | ⬜ trail, ring, chromatic; launch is instant |
| Machete | 2–3f arc smear mesh, 120–160°, 2-px trail, hit on active frames | cube sweeping the arc; damage applied on frame 0 before the visual reaches the target ⬜ |
| Dash | 0.15–0.2 s, i-frames, 3 afterimages, ±3% zoom punch, whoosh | i-frames ✅, no afterimages/zoom ⬜ |
| Low HP (< 25%) | red vignette pulsing ~1.2 Hz, low-pass music, heartbeat | ⬜ |
| HP bar | instant drop + white "ghost" chunk that catches up after 0.4 s over 0.3 s | ⬜ (no HUD yet) |
| Damage numbers | skip for a narrative game, or only on kills | — |
| Audio | 3 layers (transient/body/tail), 3–6 variants, pitch 0.95–1.05, ≤ 4 voices per sound, duck music 3 dB on rocket | ⬜ (no audio yet) |

## 7. Research: custom character pipeline

How the team should build its own characters so they animate **and** shatter.

**Recommended: part-based rigid voxel characters (jam-friendly).**
- One `.vox` per part from MagicaVoxel: head, torso, upper/lower arm L/R, upper/lower leg L/R, hands, weapon.
- In Unity each part is a child of an empty "joint" transform; animate joint rotations (Animator clips or
  procedural code). Voxels stay exact (no skinning bend) and **shatter is trivial**: every part already has its
  voxel list; at death read each part's `localToWorldMatrix` and spawn pixels/chunks from it.
- The current placeholders (16 cubes for Defne, 8 for the Chaser) already use this hierarchy — keep the joint
  names stable so placeholder → final art is a drop-in.

**Alternative: skinned voxel mesh** (only if the team wants Mixamo animations): import `.vox` into Blender with the
[MagicaVoxel extension](https://extensions.blender.org/add-ons/blender-magicavoxel/) (Blender 4.2+), rig by hand or
with Mixamo (humanoid only, single mesh, T-pose FBX; download animations "without skin"). Weight each voxel 100%
to one bone so cubes never bend. For shattering, bake per voxel: centre, colour, dominant bone index; at death use
`bone.localToWorldMatrix × bindpose`, or `SkinnedMeshRenderer.BakeMesh` once per death.
Mixamo needs an Adobe account login (a person must do that step).

**Conventions to agree on now [rec]:**
| Item | Convention |
|---|---|
| Scale | 16 voxels per metre (1 voxel = 0.0625 m); characters 24–32 voxels tall (Defne ≈ 28 = 1.75 m). At the camera's 12 px/m that is ~21 screen texels tall, so exaggerate heads/hands for readability. Bigger alternative: 10 voxels/m so 1 voxel ≈ 1.2 texels (cleaner pixel look). Pick one and keep it for every model. |
| Palette | one shared 32–64 colour palette for all characters; **fixed indices** for skin, hair, outfit, accent and a "cost tint" slot, so contract costs (colour drain, grey) can recolour by index |
| Pivots | each part's pivot at its joint; root at the feet centre; +Z forward |
| Naming | `CHR_Defne_UpperArm_L.vox`, `ENM_Imp_Head.vox` |
| Import | VoxReader (installed) → editor importer that writes mesh + voxel list (+ chunk table, section 5.2). Fallback: MagicaVoxel OBJ export + baked voxel list. |

**2D Devil overlay:**
- Draw in layers (head, jaw, eyes, brows, hands, horns); save as **.psb** for Unity's PSD Importer
  (Sprite Mode Multiple + Character Rig) and rig in the Skinning Editor
  ([2D Animation docs](https://docs.unity3d.com/Packages/com.unity.2d.animation@9.0/manual/ex-psd-importer.html)).
- For the jam, simple tweens on separate sprites (lean in, sway, blink, jaw) are faster than bones and usually enough.
- Resolution: author at target screen height (≈ 1100–1400 px tall for a 1080p lean-in). If the Devil should match
  the pixel look, author pixel art at 1/3 size and integer-scale it; otherwise keep it crisp as a deliberate
  "outside the monitor" contrast (fits K-1: the Devil talks to the player, not to Defne).
- Keep the sprite swap to one field on `DevilPresenter` so the team's drawings replace the placeholder in one step.

## 9. Deep research: audio, Devil presentation, enemy AI, delivery

### 9.1 Audio (Unity 6)
- **Audio Random Container (ARC)**, new in Unity 6 ([manual](https://docs.unity3d.com/6000.0/Documentation/Manual/AudioRandomContainer.html),
  [reference](https://docs.unity3d.com/6000.0/Documentation/Manual/AudioRandomContainer-UI.html)): a playlist asset
  with random volume/pitch ranges (pitch in cents), Shuffle/Random/Sequential, "Avoid Repeating Last = N", Manual or
  Automatic trigger (Pulse mode suits an automatic rifle). Use: assign to `audioSource.resource`, set Manual, call
  `Play()`. Suggested: ±100–200 cents, −2…0 dB, avoid-repeat 2. Unverified: polyphony, `PlayOneShot`, Web support →
  pool AudioSources, one ARC voice each.
- **Mixer**: Master › Music (Stem_Base, Stem_Perc, Stem_Melody, Stem_Defne) · SFX (Player, Enemies, Destruction) ·
  Voice_Defne · UI · Devil. Snapshots: Gameplay, **DevilFreeze** (low-pass ~800 Hz on Music+SFX, Music −8 dB, Devil
  untouched), Grey. `snapshot.TransitionTo(0.25f)` in, ~0.6 s out. Narrative costs = exposed volume params
  (`mixer.SetFloat("Stem_Melody_Vol", -80f)`) — **cost #2 (voice + one music layer) is two SetFloat calls**.
- **Stems in sync**: one looping AudioSource per stem, identical lengths, `PlayScheduled(AudioSettings.dspTime + 0.2)`
  on all; add/remove layers only by volume fades, never Stop/Play
  ([PlayScheduled](https://docs.unity3d.com/6000.6/Documentation/ScriptReference/AudioSource.PlayScheduled.html)).
- **Voices**: ~24 pooled sources; per-sound caps (4 voxel breaks, 3 enemy shots), 30–50 ms per-sound cooldown;
  priority high for Devil / player hurt, low for debris.
- **Pixel shatter sound**: transient (0–20 ms click/glass tick) + body (bitcrushed crunch, ±3 semitones) + tail
  (2–5 tiny "tink" grains over 50–300 ms, one per falling voxel, pitch dropping). **Cost #4 (Defne's face) = tail
  grains only, no transient, quiet reverb — sad, not punchy.**
- **Web build caveat (verified, [docs](https://docs.unity3d.com/6000.0/Documentation/Manual/webgl-audio.html))**: on
  Web the AudioMixer only does volume — no low-pass, no effects, no duck. Looping AAC can click at the loop point
  (use WAV source with ≥1024 samples of leading silence). Audio needs a user click first → "click to start" screen.
  On Web, fake the freeze with a volume duck + music pitch 0.9.
- Packs: Kenney Impact / Interface (CC0), Sonniss GDC bundle (royalty-free, no AI training), Freesound with the CC0
  filter ("voxel break", "glass shatter", "debris", "bitcrush").

### 9.2 The Devil overlay
- Reference: Inscryption began as a Ludum Dare jam game ("Sacrifices Must Be Made"); its appeal is the antagonist
  who comments on your choices ([Game Developer](https://www.gamedeveloper.com/design/how-game-jam-sacrifices-became-inscryption)).
- **UI system: uGUI**, Screen Space Overlay — Unity still recommends uGUI for runtime UI with keyframed animation
  ([comparison 6000.6](https://docs.unity.com/en-us/engine/6000.6/manual/uitoolkits/ui-system-compare)).
- **Staging**: layers = dark vignette, body, head, eyes, card hand. Slide in 0.35–0.45 s with OutBack (overshoot
  ~1.2), head lags the body ~60 ms. Idle breathing scale.y ±1.5% at 0.25 Hz. **Pupils follow the cursor and widen
  when it hovers HAYIR** (he's nervous); on HAYIR hover he leans back ~20 px, on EVET hover he leans in and the
  vignette tightens.
- **Typewriter + voice blips**: one blip per 2nd letter at 40–60 chars/s, ≥30 ms apart, skip spaces/punctuation;
  Devil blips = ARC of 4–6 low "hm/ah" grains at ~−500 cents ±150; slower, lower blip for the cost words
  ([Animalese](https://nookipedia.com/wiki/Animalese)).
- **Contract timing**: lean-in 0.4 s → text 1.5–2.5 s (click completes it) → buttons fade in 0.3 s after → **input
  lock 0.5 s** so a shooting click never lands on EVET by accident. Thematic variant (only if the game shows it
  openly): EVET where the player was just clicking + default focus; HAYIR on its own key with a 0.4 s hold —
  saying no literally takes effort.
- Accessibility: text speed (slow/normal/fast/instant), click-to-complete, toggles for shake and desaturation
  pulses, captions; never colour alone (grey cost also needs the missing-photo cue).

### 9.3 Enemy AI and waves
- **Attack tokens** (DOOM 2016; TLOU2 melee): an enemy must hold a token to attack; others circle/reposition
  ([summary](https://www.gamedeveloper.com/design/cyber-demons-the-ai-of-doom-2016-)). Suggested at once: melee
  (Chaser+Runner) 3, Shooter 2, Imp 1, Brute 1; release on attack end, death, or 3 s timeout. This keeps 60
  enemies readable and is what makes "fair but overwhelming" deaths possible on the GDD schedule.
- **Telegraphs**: human reaction ≈ 240 ms ([Dark Souls 3 analysis](https://www.gamedeveloper.com/game-platforms/anatomy-of-an-enemy-attack-in-dark-souls-3));
  GDD values (Shooter 0.8 s, Imp 0.7 s) are good; Runner ≥ 0.35 s, Brute charge ~0.6 s, spawn marker 0.8–1.0 s +
  sound, never within 3 m of the player. Every telegraph = animation + sound + VFX. First-time players need slower
  timings early.
- **Movement for 60 enemies**: no NavMeshAgent crowd (avoidance cost/stutter reports). Per enemy a small state
  machine (Spawn → Approach → WaitToken → Telegraph → Attack → Recover → Stagger) + steering (seek + separation +
  orbit while waiting); separation via spatial hash in one Burst `IJobParallelFor` (plain C# O(n²) at 60 enemies
  ≈ 3,600 checks may already be fine — profile). Obstacles: `NavMesh.CalculatePath` every 0.25–0.5 s and steer
  along corners. Knockback = impulse added to steering, decays over ~0.15 s.

### 9.4 Delivery
- **Windows build is the main build** (full mixer, compute, indirect draw). WebGL2 has no compute shaders → the
  debris system needs a CPU/ParticleSystem fallback if a Web build is wanted. Unity 6.6 WebGPU is reported
  production-ready by third parties but not default; Unity's own page is unclear
  ([web graphics APIs](https://docs.unity3d.com/6000.6/Documentation/Manual/web-graphics-apis-intro.html)) — optional.
- Web settings if used: Gzip + Decompression Fallback (itch.io Brotli header issues), strip shader variants,
  music Compressed In Memory, SFX Decompress On Load, click-to-start, 1280×720 viewport + fullscreen button.
- **Jam triage**: T−24 h feature freeze + full playable upload (Web + Windows); T−12 h only crashes, soft-locks,
  unreadable moments, then polish audio → Devil timing → end screen; T−3 h final build tested in a clean browser /
  fresh Windows folder; itch page with controls, content notes, credits and CC0 attributions.
  Deadline is **2026-10-26 15:00** → freeze ~2026-10-25 15:00.
- **Playtest checklist**: contract understood without help? each cost noticed within 5 s? 5 enemy types readable in
  greyscale (the final state is grey)? telegraphs readable at peak count? can an always-NO player finish a run?
  60 fps for the whole 5 min? stems still in sync at 5 min? Devil overlay never eats combat input? restart resets
  desaturation, mixer and the HUD photo?

## 10. Deep research: pixel-art rendering in URP 17 (Render Graph)

The branch already has the low-res RT + texel snap (`PixelCameraRig`). Missing pieces: outline, toon shading,
palette, the warm earthy look, desaturation costs, pixel UI.

### 10.1 Render Graph pass (Unity 6000.6)
- Compatibility Mode was removed for custom passes in 6.4: `Execute` / `OnCameraSetup` passes do not run
  ([upgrade guide 6.4](https://docs.unity3d.com/6000.4/Documentation/Manual/UpgradeGuideUnity64.html)). Use
  `RecordRenderGraph`.
- Pattern ([write a render pass 6000.6](https://docs.unity.com/en-us/engine/6000.6/manual/render-pipelines/universal-render-pipeline/customizing-urp/render-graph/write-render-pass.md),
  [blit](https://docs.unity3d.com/6000.3/Documentation/Manual/urp/render-graph-blit.html)): in the pass constructor
  `ConfigureInput(ScriptableRenderPassInput.Depth | ScriptableRenderPassInput.Normal)`; in `RecordRenderGraph` get
  `UniversalResourceData`, create a destination texture from the active colour desc, then
  `RenderGraphUtils.AddBlitPass(rg, new BlitMaterialParameters(src, dst, mat, 0), "PixelOutline", returnBuilder: true)`,
  `UseTexture(cameraDepthTexture)` + `UseTexture(cameraNormalsTexture)` on the returned builder, and finally
  `resourceData.cameraColor = dst` (no copy-back). Shader: Assets › Create › Shader › SRP Blit Shader; sample
  `_CameraDepthTexture` / `_CameraNormalsTexture`. (Check `AddBlitPass` overloads against Core 17.x API.)
- The pass runs on the **game camera** (the one rendering into the 648×368 RT), so it works at low resolution for free.

### 10.2 t3ssel8r-style outlines (from three.js `RenderPixelatedPass`, MIT, which credits t3ssel8r)
[source](https://raw.githubusercontent.com/mrdoob/three.js/dev/examples/jsm/postprocessing/RenderPixelatedPass.js)
- **Outer line (depth):** sum `clamp(depthNeighbour − depthCentre, 0, 1)` over 4 neighbours →
  `smoothstep(0.01, 0.02, sum)`. Only positive differences count, so the line lands on the near object's pixels —
  never double thick.
- **Inner highlight (normals):** per neighbour `nd = dot(nC − nN, bias)` (bias = light-facing direction),
  `nInd = smoothstep(−0.01, 0.01, nd)`, `dInd = saturate(sign((dN − dC)·0.25 + 0.0025))`,
  `contrib = (1 − dot(nC, nN))·dInd·nInd`; `step(0.1, Σ)`.
- **Combine:** depth edge darkens (`1 − 0.4·edge`), normal edge brightens (`1 + 0.3·edge`); never both. Use the
  palette's darkest brown instead of black for the outline. Orthographic: raw depth is already linear (flip under
  `UNITY_REVERSED_Z`). This makes voxel edges pop at 12 px/m.
- Avoid Kode80 PixelRender (non-commercial licence, 2016).

### 10.3 Toon shading (URP 17)
- `GetMainLight(TransformWorldToShadowCoord(posWS))` with
  `#pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN`; 2–3 hard
  bands (`floor(saturate(ndl)·steps)/(steps−1)`), shadow colour = base × warm shadow tint (not grey), hard `step`
  shadows, optional stepped rim.
- **Forward+ pitfall (since 6.1):** `_FORWARD_PLUS` → `#pragma multi_compile _ _CLUSTER_LIGHT_LOOP`, guard
  `#if USE_CLUSTER_LIGHT_LOOP`; `_ADDITIONAL_LIGHTS` is ignored under Forward+ and `LIGHT_LOOP_BEGIN` needs a variable
  named `inputData`. Old tutorials silently drop point lights — this matters for the muzzle/explosion point lights
  already in `PixelImpactFeedback`
  ([docs](https://docs.unity3d.com/6000.6/Documentation/Manual/urp/use-built-in-shader-methods-additional-lights-fplus.html)).
- Keep every material property in `CBUFFER_START(UnityPerMaterial)` (SRP Batcher). Add `ShadowCaster` and
  **`DepthNormals`** passes, or the outline pass sees nothing.

### 10.4 Palette (the earthy reference look)
- Recommended: **palette-snap blit after the outline pass** — 1×N palette texture, nearest colour in OKLab (keeps
  browns from shifting hue), optional 4×4 Bayer dither indexed by the low-res pixel position (locked to the texel
  grid). ~7 M ALU ops at 640×360×32 — trivial. Faster: a 32³ nearest-index LUT.
- Palettes: **Endesga 32** (warm ramp #733e39 #b86f50 #c28569 #e4a672 #ead4aa #be4a2f #d77643 #feae34),
  **Resurrect 64** (muted, orange-heavy — closest to the reference picture)
  ([Lospec](https://lospec.com/palette-list/resurrect-64)). The current placeholder palette is "dark cold"; the
  reference is warm — switch now so art and effects are tuned against the right colours.

### 10.5 Desaturation costs
- **World drain (cost #1):** global `_WorldSaturation` applied inside the palette pass **before** the snap (a URP
  ColorAdjustments volume after the snap would be re-snapped to colours). Add 3–4 neutral greys to the palette so
  greys survive the snap. UI and the Devil are on a Screen Space Overlay canvas → untouched automatically.
- **Grey Defne (cost #6) while the world keeps its (drained) colour:** `_Saturation` in the character toon shader's
  `UnityPerMaterial`, set on her material instance (not a MaterialPropertyBlock — that breaks SRP Batcher for the
  renderer). If the world pass would also grey her, write a stencil bit in her shader and skip it in the palette pass.

### 10.6 Pixel UI with Turkish glyphs
- **Public Pixel** (GGBotNet, CC0, 8×8, Turkish explicitly supported) — best verified pick
  ([itch](https://ggbot.itch.io/public-pixel-font)). m5x7 / Pixel Operator are CC0 but ğ ş ı İ coverage unverified.
- TextMesh Pro: atlas `RASTER_HINTED` (or `RASTER` if glyphs garble), sampling size = native pixel size, padding
  0–1, `TextMeshPro/Bitmap` shader, point filter, font sizes in whole multiples; custom characters include
  "ğĞşŞıİöÖüÜçÇ".
- Canvas Scaler **Constant Pixel Size** with `scaleFactor` set from code to the same integer scale as the game RT
  (`PixelCameraRig` already computes it); Pixel Perfect on. Not Scale With Screen Size (fractional → blurry).

### 10.7 Debris in the outline
Indirect draws are picked up by every URP pass whose LightMode the shader has → the debris shader needs a
`DepthNormals` pass with the same indirect vertex fetch (and `ShadowCaster` for chunks). Check in the Frame
Debugger that the draws appear under DepthNormalPrepass; set `worldBounds` to the arena.

## 11. Deep research: destruction implementation details (verified against the local 6000.6.0f1 install)

### 11.1 Indirect draw
- `Graphics.RenderMeshIndirect(RenderParams, Mesh, GraphicsBuffer commandBuffer, int commandCount = 1, int startCommand = 0)`
  and `Graphics.RenderPrimitivesIndirect(RenderParams, MeshTopology, GraphicsBuffer, int, int)`; the RenderParams
  argument is `ref` in 6.0 docs, `in` in 6.3+ — call without modifier, add `ref` only if the compiler asks.
- `new RenderParams(material)`; always set `worldBounds` (culling); also `shadowCastingMode`, `receiveShadows`,
  `layer`, `renderingLayerMask`, `matProps`.
- Args (from `UnityIndirect.cginc`): `IndirectDrawArgs` 16 B (vertexCountPerInstance, instanceCount, startVertex,
  startInstance), `IndirectDrawIndexedArgs` 20 B. Buffer: `new GraphicsBuffer(Target.IndirectArguments, n, GraphicsBuffer.IndirectDrawIndexedArgs.size)`.
- Shader: plain HLSL include, works inside `HLSLPROGRAM` with URP `Core.hlsl`:
  `#define UNITY_INDIRECT_DRAW_ARGS IndirectDrawIndexedArgs` + `#include "UnityIndirect.cginc"`; in the vertex shader
  `InitIndirectDrawArgs(0)` and **always** `GetIndirectInstanceID(svInstanceID)` (it subtracts startInstance on
  Vulkan). Read `StructuredBuffer<float4> _PosScale` (xyz + size) and `StructuredBuffer<uint> _Color` (RGBA8).
  Don't use `TransformObjectToWorld` / `UNITY_SETUP_INSTANCE_ID` here.
- Passes: UniversalForward + **ShadowCaster** (`_LightDirection`, `ApplyShadowBias`, `ApplyShadowClamping`, as URP's
  `ShadowCasterPass.hlsl`) + **DepthOnly** + **DepthNormals** (needed for the outline, SSAO, depth priming).
  Known forum trouble with instance IDs under `Core.hlsl` is avoided by the `SV_InstanceID` + include route.

### 11.2 Uploading buffers
- Start with `SetData(nativeArray, 0, 0, aliveCount)` (upload only the alive range).
  `LockBufferForWrite` is not guaranteed zero-copy, is write-only/write-combined, warns on D3D11 if locked more
  than once a frame, and one report measured BRG ~10% slower on the GPU with it. Try a triple-buffered Lock only
  after profiling. Lock/unlock on the main thread after `Complete()`.

### 11.3 Job flow
- `Update`: `handle = Compact.Schedule(Integrate.ScheduleParallel(count, 256, dep)); JobHandle.ScheduleBatchedJobs();`
  → `LateUpdate`: `handle.Complete()` → `SetData` → draw.
- Random in jobs: `Unity.Mathematics.Random.CreateFromIndex((uint)(frame * 0x9E3779B9u ^ i))` — never copy one Random
  into a job without writing it back (replays the same numbers every frame; brg-shooter has this bug).
- Removing dead particles: parallel job writes `alive[i]`, then one `IJob` does an in-place stable compaction
  (`w = 0; for r: if alive[r] copy r→w++`) — O(n), fast in Burst. If using a dead-index list, **sort it descending
  before swap-remove** (brg-shooter's `DebrisRecyclingJob` doesn't and can drop live particles).

### 11.4 Rigid chunks without PhysX
- `com.unity.physics` 6.6.0 depends on Entities (Unity Companion License); its core can be stepped manually
  (`PhysicsWorld`, `Simulation.StepImmediate`) but it is heavy for debris. **Write a small Burst integrator:**
  semi-implicit Euler; orientation `q = normalize(mul(q, quaternion(float4(0.5·dt·ωlocal, 1))))` (same as Unity
  Physics' `Integrator`); per body test 8 OBB corners against the floor, impulse
  `j = −(1+e)·vn / (invM + dot(rn, invIworld·rn))` with `rn = cross(r, up)`, friction clamped to μ·j, positional
  correction `p.y −= d·0.8`; `invIworld = R·invIlocal·Rᵀ`; sleep after N frames under ε. One body per parallel index.

### 11.5 Greedy meshing (MeshData, Burst)
- `Mesh.AllocateWritableMeshData(n)` → in a Burst job `SetVertexBufferParams` / `GetVertexData<T>` /
  `SetIndexBufferParams` / `GetIndexData<int>` / `SetSubMesh(0, desc, DontRecalculateBounds | DontValidateIndices)` →
  `Mesh.ApplyAndDisposeWritableMeshData` ([aras-p gist](https://gist.github.com/aras-p/f0eeec6abcf9690b6b5fb83376076456)).
- MIT repos to borrow from: [Seremonik/Unity-Voxel-Engine](https://github.com/Seremonik/Unity-Voxel-Engine)
  (`Runtime/Scripts/BinaryMeshGenerator.cs`, binary greedy meshing with 64-bit masks, Burst IJob),
  [Gamengineer314/UnityVoxelEngine](https://github.com/Gamengineer314/UnityVoxelEngine)
  (`Assets/Voxels/Scripts/Rendering/MeshGenerator.cs`, `Editor/VoxImporter.cs`, indirect rendering + GPU culling),
  [cgerikj/binary-greedy-meshing](https://github.com/cgerikj/binary-greedy-meshing) (C++ reference). None writes
  MeshData directly — add that step.

### 11.6 VoxReader 5.0.6 (installed on the branch)
- `VoxReader.VoxReader.Read(path | byte[])` → `IVoxFile { Models, Palette, Chunks }`; `IModel { Name, Id, IsCopy,
  GlobalPosition, GlobalRotation (Matrix3), GlobalSize, Voxel[] Voxels }`; `Voxel { LocalPosition, GlobalPosition,
  Color, ColorIndex }` (VoxReader's own Vector3/Color types, not Unity's).
- No group API: global positions are already composed (frame 0 only). Palette indices are already corrected
  (use `Voxel.Color`, don't subtract 1 again; possible bug on palette slot 255 — avoid using it).
- **Axes:** data is MagicaVoxel Z-up → Unity `(x, z, y)`; mirror one axis if the model looks flipped. Global
  positions are centred on size/2 (can be negative).

### 11.7 Voxel pre-fracture (no ready implementation exists)
Import time per model: K seeds (biased to hit zones / limbs) → multi-source BFS label (grid-connected cells) →
6-connected flood fill per label → merge chunks < M voxels into a neighbour → store per chunk voxel list, centre
of mass, local AABB (as the OBB) and greedy mesh. Runtime: voxels within R of the hit → micro-particles; every other
chunk → rigid body with velocity away from the hit + random spin. Environment: after each carve, flood fill from
anchored voxels (ground); unreached components detach as chunks (cheap "structural" collapse, Teardown-like).

### 11.8 brg-shooter ([repo](https://github.com/Unity-Technologies/brg-shooter), Unity Companion License)
`BRG_Debris.cs`: 16,384 capacity, 1,024 cubes per explosion, gravity 16, no bounce (snaps to floor cell), job chain
PhysicsUpdate (IJobFor, batch 256) → BackgroundDigger → DebrisRecycling (buggy swap-remove) → DebrisGeneration;
landing/death events via a capped `Interlocked.Add` list (≤ 256 per frame). Borrow the job-chain shape and the capped
event list (good for "tink" sounds on chunk bounces); skip the BRG/UBO windowing — plain indirect draw is enough on PC.

## 12. Final polish prompt

Paste everything inside the block into the working Claude Code session (on the up-to-date working branch).

```text
You are polishing "Dance with the Devil" (Women Game Jam, theme "Hayır demeyi öğrenmelisin") for delivery on
2026-10-26 15:00 (UTC+3). Feature freeze: 2026-10-25 15:00; after that only crash/soft-lock/readability fixes.

READ FIRST (in this order): AGENTS.md (binding rules: pick .claude agents/skills first, WORKLOG + MAIN_GDD updates,
report every Scene/Prefab/ProjectSettings change), MAIN_GDD.md, the last WORKLOG.md entries,
Docs/PLAYTEST_REVIEW.md (full review: deviations §1, bugs §3, research §5–§11), Docs/QA_SMOKE_CHECKLIST.md.

USER DECISION THAT OVERRIDES K-11: destruction is NOT optional. The user's headline request is: an enemy hit by a
rocket breaks into its pixels from the exact point it was hit, tiny pixels spray out and scatter, the rest of the
body flies apart as whole pieces; the whole environment is destructible; it must stay smooth (Jobs/Burst etc.).
Record this in MAIN_GDD.md §14 (move destruction + environment breaking + Defne's voxel loss into Minimum) and in
§15 as a user decision, then build it. Also the user asked for free internet assets (models, Devil reference PNGs)
instead of placeholder cubes, a warm earthy pixel look like their reference, and "perfect game feel".

WORK IN THIS ORDER. After each step: compile clean, run EditMode + PlayMode tests through MCP, play-mode check with
screenshots, update WORKLOG.md (and MAIN_GDD.md when design changes), commit, push.

0. Apply the reviewed fixes: `git am Docs/review-fixes.patch` (B-1 aim from muzzle to cursor, B-4 walls shield
   rockets, B-5, B-6 NavMesh-snapped spawns, B-7 stable spawn queue, B-12 E key can't sign a contract by accident).
   Add PlayMode tests for B-1, B-4, B-12 (PLAYTEST_REVIEW §3 "How the fixes were approached").

1. Physics layers + masks (B-9): Player, Enemy, EnemyProjectile, Wall, Prop, Debris. Replace every `~0` mask.

2. Voxel destruction system (agents: unity-dots-specialist, engine-programmer, performance-analyst; skills:
   unity-vfx-game-feel, perf-profile, code-review). Follow PLAYTEST_REVIEW §5.2, §5.6, §11:
   - Editor importer (VoxReader is installed; Z-up → Unity (x,z,y)) and a voxelizer for the current cube-part
     placeholders, so every enemy/Defne/prop has a voxel list + palette.
   - Import-time pre-fracture: 4–8 seeds, voxel Voronoi + 6-connected flood fill, greedy-meshed chunk sub-meshes.
   - Runtime: voxels within r = 2–3 voxels of the hit point → micro-pixels (each voxel → 2×2×2, rockets 3×3×3) in a
     Burst SoA ring buffer (cap 32k), drawn with Graphics.RenderPrimitivesIndirect / RenderMeshIndirect, texel-
     snapped, Bayer-dither fade, DepthNormals pass; remaining chunks → Burst rigid integrator (8-corner floor
     contacts, restitution 0.3, friction 0.6, sleep → static → shrink after 3–6 s, cap 256).
   - Kill timeline (§5.1): 1-frame white silhouette, hitstop 50 ms (elite 100–150, player death 200 + 0.3 s slow-mo),
     directional kick 2–4 texels, 300–800 pixels per death, 3–8 chunks, hot colour first 2–3 frames, smoke puffs,
     ~5% embers, chunk-bounce "tink" sounds via a capped event list.
   - Bullet hits on living enemies chip a few micro-pixels off the body.
   - Environment: inner L walls and props carve a sphere on rocket/machete/imp hits, re-mesh with a Burst greedy
     job (MeshData API), detached islands (flood fill from the floor) fall as chunks; outer wall stays unbreakable.
     Re-test Echo bounce and the 4 m passages (A-12).
   - Defne cost #4: real voxels fall off her face/body (not hidden hair parts); Devil revive shockwave pixelates.
   - Perf gate: 100 kills in one frame + full arena explosion ≤ 16.6 ms in a Windows build, 0 GC per frame.
   - Keep the Shuriken path only as a WebGL fallback if a Web build is made.

3. Assets and look (agents: art-director, technical-artist, unity-shader-specialist; skills: asset-audit,
   art-bible). Download CC0 assets into Assets/_Game/ThirdParty/<pack>/ and list each in CREDITS.md:
   kooow Voxel Creature Pack 1 + 55 Voxel Creatures (OpenGameArt), padadu Voxel Pack 03 Modern Weapons (itch.io),
   Felix Strefter Dungeon Voxel Assets (itch.io), Nerveona Demon/Devil PNG (OpenGameArt) as the Devil placeholder.
   Build Defne as a part-based voxel model per PLAYTEST_REVIEW §7 conventions (16 vox/m, ~28 voxels tall, joints
   as pivots, shared palette with fixed indices for skin/hair/outfit/accent/cost tint). Then PLAYTEST_REVIEW §10:
   Render Graph outline pass (depth outer line + normal inner highlight, palette dark brown), toon shader
   (2–3 bands, warm shadow tint, `_CLUSTER_LIGHT_LOOP` for Forward+), palette-snap pass (Resurrect 64 or
   Endesga 32, OKLab nearest, world saturation applied before the snap), pixel font with Turkish glyphs
   (Public Pixel, CC0) and Constant Pixel Size canvases at the RT's integer scale.

4. Game feel pass (skill: unity-vfx-game-feel; checklist PLAYTEST_REVIEW §6 table): directional kick + ±3–5°
   spread + player recoil on the rifle, shell casings that stay (pool 200), rocket anticipation 100–150 ms +
   smoke trail + shockwave ring + chromatic pulse, machete smear + kill hitstop + 1–1.5 m knockback, enemy hit
   squash 1.15→1 + knockback, player-hurt shake + red flash, low-HP vignette/heartbeat, HP ghost bar, dash
   afterimages. Keep a global shake slider (accessibility).

5. Devil and theme (agents: narrative-director, writer, ux-designer; skills: ux-design, team-narrative). Ask the
   user before adding any of PLAYTEST_REVIEW §4 ideas to the GDD; recommended first: EVET pre-focused, HAYIR as a
   small grey link that always works, fake countdown that runs out with nothing happening, "Bir şey olmaz"
   refrain, pity line after HAYIR, ending stats ("Hayır deme sayın: 1. Yetti."), true-ending post-credits line.
   Staging per §9.2: OutBack lean-in, breathing idle, pupils follow the cursor and widen over HAYIR, typewriter
   with low voice blips, buttons 0.3 s after text. Fix B-13 (clear enemy bullets on revive, keep a short grace),
   B-14 (warnings above the vignette), B-15, B-16, ledger one item per line, settle A-4 with the user (ending at
   death 6 vs 7).

6. Enemies (agents: ai-programmer, game-designer; skills: team-combat, balance-check): add the exploding imp and
   the shielded brute (GDD §6) now that the environment breaks; attack tokens (melee 3, shooter 2, imp 1, brute 1);
   shared enemy-orb pool (B-8); balance the death schedule with two human playtests (QA checklist §4).

7. Audio (skill: team-audio): an AudioMixer with buses + DevilFreeze snapshot (low-pass ~800 Hz, music −8 dB),
   contract 2 via exposed volumes; layered pixel-shatter sound (transient + body + tail grains), ARC for
   variation; listening pass; replace weak synthesized sounds with Kenney CC0 packs if needed.

8. Delivery (skills: team-polish, smoke-check, team-qa): run Docs/QA_SMOKE_CHECKLIST.md by hand, measure frame
   time in the Windows build, fix B-17/B-18, final Windows build tested from a clean folder, itch.io page with
   controls, content notes, CREDITS.

RULES: never close an open GDD question (❓) by guessing — ask the user. Never push to main without the user's OK.
Report every Scene, Prefab and ProjectSettings change. If time runs short, cut in this order: optional Devil
theme extras → imp/brute → audio mixer polish → environment carving of props (keep inner walls) — never cut the
enemy pixel shatter, the Devil contract → cost → ending chain, or the warm pixel look.
```
