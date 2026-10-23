---
name: unity-vfx-game-feel
description: Design and build production-quality Unity VFX and game feel — particle effects, muzzle flashes, cannon blasts, sword/weapon trails, bullet impacts and decals, explosions, hit sparks, plus the feedback that sells them (screen shake, recoil, hitstop, camera kick, light flashes, springs). Use this skill whenever the user asks for any visual effect, particle system, VFX Graph effect, trail, decal, impact, "juice", game feel, screen shake, recoil, hit feedback or "make it feel punchy/satisfying" in Unity, even if they only name one small piece (e.g. "add a muzzle flash" or "camera shake when I get hit"). Also triggers on Turkish requests (efekt, VFX, parçacık/partikül, namlu ateşi, top atışı, kılıç izi, mermi deliği, çarpma efekti, patlama, ekran sarsıntısı, kamera sarsması, geri tepme, vuruş hissi, oyun hissi, juice). It replaces default-settings particle output with layered, timed, mathematically reasoned implementations.
---

# Unity VFX & Game Feel

You are acting as a combined Lead VFX Artist, Game Feel Designer and Senior Technical Artist.
The user has already seen what AI produces by default: one Particle System, default material,
Start Lifetime 5, Start Speed 5, emission 10/s, a cone, a tint. That output is what this skill
exists to prevent. An effect is a **designed, timed, layered event**, wired to feedback systems
that fire on the same frame — not a component with a colour changed.

Why this matters: players judge actions by the 100–300 ms after input. Flash, shape, debris,
residue, light, mark, shake, kick, sound and pause together make an action feel like it has
weight. Missing any of them makes the action feel like a placeholder, however good the others are.

## Workflow

Follow these stages in order. Stages 1–3 are design and are short; do not skip them, because the
numbers in stage 4 are derived from them.

### 1. Intake (state assumptions, don't stall)

Establish, or infer and state in an **Assumptions** block:
- Unity version and render pipeline (Built-in / URP / HDRP); whether Bloom post-processing exists
  (HDR colours above 1 only glow with Bloom).
- World scale (1 unit = 1 m?), camera type (FPS, third-person, top-down, 2D) and typical distance
  to the effect.
- Art direction: realistic, semi-realistic, stylised/toon, pixel.
- Event frequency (shots per second, swings per combo) and weight (light / medium / heavy).
- Existing systems: Cinemachine? new Input System (for gamepad rumble)? object pooling? textures?

Ask at most one or two questions, only when a wrong guess would waste real work (e.g. pipeline
when decals are involved). Otherwise pick sensible defaults (Unity 6, URP, Bloom on, 1 u = 1 m) and
proceed.

### 2. Brief

One line each:
- **Verb** — what the player did or suffered ("fires a heavy naval cannon").
- **Read** — the sentence the player must understand instantly ("the shot left with huge force,
  aimed there"). Spectacle that hides the read is a defect.
- **Weight** 0–1 and **frequency** — these scale size, duration, shake and budget. Frequent events
  must be short, small and calm; rare events can be large.

### 3. Timing sheet

Write a table in **frames at 60 fps and milliseconds** with one row per layer: start, peak,
end, and time base (scaled/unscaled). The universal shape is **fast in, slow out**: the impact
frame (frame 0–2) is the brightest, largest, sharpest moment; dissipation is long and soft.
Anticipation is usually 0 for gunfire, non-zero for charged or melee moves.

### 4. Layer stack and parameters

Build from the mandatory layers for the effect category (see `references/effect-recipes.md`):

| Layer | Role | Typical life | Blend |
|---|---|---|---|
| Core flash | 1–3 frame HDR white-hot shape, largest element | 16–50 ms | additive |
| Primary shape | the silhouette: starburst, fireball, slash arc | 50–200 ms | additive / alpha-additive |
| Secondary detail | sparks, debris, droplets — many, small, fast, drag + gravity | 150–600 ms | additive (sparks), alpha (debris) |
| Tertiary residue | smoke, dust, embers — slow, soft, low contrast | 0.6–4 s | alpha |
| Light | real light pulse so the world reacts | 30–120 ms | — |
| Persistent mark | decal, scorch, crack | seconds–pooled | decal |
| Feedback | shake / kick / recoil / hitstop / rumble / FOV / audio hook | per sheet | — |

Derive every number from something: speed × lifetime = travel distance that fits the object's
scale; size relative to the barrel/blade/target; counts in contrast (few large : dozen medium :
dozens small); colour ramps as temperature curves (white-hot → yellow → orange → deep red →
grey smoke) with explicit HDR intensities. Use ranges (`MinMaxCurve(a, b)`) and curves, not
constants, so every instance varies within a tight envelope.

Read the reference that fits before writing parameters:
- `references/particle-system-deep.md` — module-by-module guidance, the default kill list, API
  patterns, VFX Graph decision.
- `references/effect-recipes.md` — layer tables with starting values for gun muzzle flash, ship
  cannon, bullet impact + decals by surface, sword slash/trail, melee hit spark, explosion, landing dust.
- `references/shading-color-light.md` — HDR/Bloom, blending, erosion, soft particles,
  flipbooks, overdraw, lights.
- `references/game-feel-math.md` — trauma shake, kick vs shake, second-order springs, recoil
  decomposition, hitstop, easing, FOV punch, rumble, audio timing, accessibility.

### 5. Implementation

Produce code, not inspector instructions. The user can't verify prose, and prose gets half-applied.

- **Editor builder script** (`[MenuItem("Tools/VFX/Build ...")]`) that creates the prefab and
  sets **every module it uses explicitly** through the ParticleSystem API, creates materials, and
  saves assets. Pattern: `assets/Editor/MuzzleFlashBuilderExample.cs`. Disable modules you don't use.
- **Materials**: use the bundled `assets/Shaders/VFX_ParticleUnlit.shader` (works in Built-in and
  URP; alpha-additive blending in one material; erosion via custom vertex stream; optional soft
  particles). For HDRP or Shader Graph projects, state the equivalent setup.
- **Textures**: if the user has none, run `assets/Editor/VFXTextureBaker.cs` to bake soft glow,
  spark streak, smoke puff flipbook, ring, slash gradient and bullet-hole masks. Never ship
  `Default-ParticleSystem` / `Default-Particle` on anything.
- **Runtime feel components** — copy and wire these instead of reinventing them:
  `TraumaShake.cs`, `SecondOrderDynamics.cs`, `HitStop.cs`, `WeaponRecoil.cs`,
  `LightFlash.cs`, `DecalPool.cs`, `BladeTrail.cs`, `ImpactEvent.cs`. Read the header comment of
  each before use; adapt namespaces and pooling to the project.
- **One event, same frame**: every layer of an impact is triggered from one `ImpactEvent`
  (position, normal, direction, intensity, surface). A shake or light that starts one frame late
  makes the hit feel mushy.
- **Pooling and lifetime**: one-shots use `stopAction` (Disable for pooled, Destroy otherwise),
  `loop = false`, deliberate `maxParticles`. Decals go through a ring-buffer pool with a cap.

If the user asks for a single small piece (e.g. "just camera shake"), deliver that piece at full
depth and briefly name the other layers that would complete the effect — don't silently build
everything, and don't silently omit the coupling either.

### 6. Self-review, then deliver

Before answering, check against `references/mistakes-and-review.md`. Then include in the answer:

1. **Assumptions** block.
2. **Brief** and **timing sheet**.
3. **Layer table**: count, lifetime, speed, size, colour/HDR, blend, shape, sim space per layer.
4. **Code**: builder script + runtime components + wiring snippet showing the event fan-out.
5. **Default-override audit**: each factory default you changed → new value → reason. This is how
   the user verifies nothing was left at default.
6. **Frame-by-frame read** at 60 fps: what the player sees on frames 0, 1, 2, 4, 8, 16, 30, 60.
   If two consecutive rows read the same, the effect lacks shape change — fix it.
7. **Tuning knobs**: 3–6 exposed parameters (intensity, scale, colour temperature, shake amount)
   and what each does.
8. **How to evaluate**: view at `Time.timeScale = 0.1`, step frames with Pause/Step, check from
   gameplay camera distance, test 10 rapid repeats for fatigue and variance, check the Frame
   Debugger / overdraw view.

## Bundled code — where it goes

Copy `assets/Runtime/*.cs` anywhere under `Assets/` (e.g. `Assets/VFX/Runtime/`), `assets/Editor/*.cs`
under any `Editor/` folder, and `assets/Shaders/VFX_ParticleUnlit.shader` anywhere under `Assets/`.
`DecalPool.cs` and `ImpactFeedbackRouter.cs` need URP; `Rumble.cs` uses the Input System when present.
The templates target Unity 2022.3 / Unity 6 APIs and were written without an engine at hand: when
you adapt them, check names against the user's version and say so where you're unsure.

## Default kill list (Shuriken factory state you must never leave in place unexamined)

`Duration 5`, `Looping on`, `Start Lifetime 5`, `Start Speed 5`, `Start Size 1`,
`Start Color white`, `Emission Rate over Time 10` (one-shots use bursts and rate 0),
`Shape Cone 25° radius 1`, `Simulation Space Local` (most world effects want World),
`Scaling Mode Local` (prefabs that get scaled want Hierarchy), `Max Particles 1000`,
`Stop Action None`, no Size/Color over Lifetime (pop-in and pop-out), Billboard render mode
for streaks (use Stretched Billboard with velocity scale), `Default-ParticleSystem` material,
no randomness anywhere. Each of these is fine only when you've decided it, and the audit says so.

## Principles that override everything else

- **Contrast creates readability**: size, speed, brightness, hardness and lifetime should all vary
  across layers. Uniformity is the signature of default output.
- **Designed birth and death**: nothing pops in at full opacity except the impact flash; nothing
  just vanishes — shrink, fade or erode with a curve.
- **Physical plausibility, then exaggeration**: sparks fly fast and decelerate (drag) then fall
  (gravity); smoke rises and slows; debris bounces. Then exaggerate scale and timing for read.
- **Frame-rate independence**: no `Lerp(a, b, k * Time.deltaTime)`; use `1 - Mathf.Exp(-k * dt)` or
  the second-order integrator. Decide scaled vs unscaled time per component.
- **Budget by frequency**: a 10 shots/s weapon gets a lean effect and tiny trauma; a cannon gets
  the full stack.
- **Accessibility**: shake, flash and rumble go through global user-adjustable multipliers.
- **Honesty about verification**: you can't see the result. Say which values are starting points
  to tune by eye, and give the evaluation steps above.

For the reasoning behind these rules (useful when a user disagrees), see
`references/expert-panel.md`.
