# Particle System (Shuriken) — Deep Reference

Contents
1. Shuriken vs VFX Graph vs custom mesh
2. Module-by-module: what professionals set
3. API patterns (builder scripts)
4. Custom vertex streams
5. Sub-emitters, collision, lights, trails
6. Performance guardrails

---

## 1. Choosing the tool

| Need | Use |
|---|---|
| Gameplay one-shots (muzzle, impact, hit spark, small explosion), up to a few hundred particles, any pipeline, collisions with world, lights module, full C# control | **Shuriken (ParticleSystem)** — default |
| Thousands–millions of particles, ambient (rain, swarms, magic fields), HDRP, GPU events, signed-distance-field interactions | **VFX Graph** (requires compute support; poor CPU readback; harder to author from code) |
| Weapon/blade trails on fast animations | **Custom mesh trail** (`BladeTrail.cs`) or authored slash mesh — not TrailRenderer |
| Projectile/vehicle ribbon at moderate speed | `TrailRenderer` or Shuriken Trails module is fine |
| Persistent surface marks | URP/HDRP **Decal Projector** via pool |

If the user is on VFX Graph, apply the same design (layers, timing sheet, curves, HDR), expressed
as Spawn (Single Burst / Periodic Burst), Initialize (Set Lifetime Random, Set Velocity Random),
Update (Linear Drag, Gravity, Turbulence, Collide with Depth Buffer), Output (Quad / Mesh,
Size over Life, Color over Life with HDR gradient, Flipbook Player). Trigger from code with
`VisualEffect.SendEvent("OnImpact", attr)` using a `VFXEventAttribute` to pass position/direction.

## 2. Module-by-module

### Main
- `duration`: match the longest layer of a one-shot (e.g. 0.3–1.0). Irrelevant for bursts at t=0
  beyond determining when the system "ends".
- `loop = false` for every event-driven effect. `playOnAwake` true for spawned prefabs, false for
  attached effects you `Play()` from code (e.g. muzzle flash on the gun).
- `startLifetime`: from the timing sheet; always a range (`MinMaxCurve(min, max)`), typically ±20–40 %.
- `startSpeed`: travel distance ÷ lifetime, then account for drag. Sparks 6–25 m/s, smoke 0.3–2 m/s,
  debris 3–10 m/s, muzzle core ~0 (it's a flash, it doesn't travel).
- `startSize`: relative to the source object. Range always. For streaks use stretched billboards
  and small sizes (0.02–0.06 m) — length comes from velocity.
- `startRotation` (radians!): random 0–2π for anything round (smoke, flash, glow) — kills repetition.
- `startColor`: usually white × HDR in the material; colour ramps belong in Color over Lifetime.
  Use `MinMaxGradient(colorA, colorB)` for per-particle variance (e.g. yellow↔orange sparks).
- `gravityModifier`: sparks 0.5–1.5, debris 1–2, smoke negative (-0.05 to -0.2) or 0 with
  upward velocity, flashes 0.
- `simulationSpace = World` for anything that should stay where it was emitted while the emitter
  moves (smoke from a moving gun, sparks). `Local` only for things glued to the emitter (muzzle
  core flash, aura on a character).
- `scalingMode = Hierarchy` so scaling the prefab scales the effect.
- `maxParticles`: a deliberate number slightly above the burst totals.
- `stopAction`: `Disable` (pooled) / `Destroy` (unpooled one-shots) / `Callback` (return to pool).
- `cullingMode`: `AlwaysSimulate` for short one-shots (avoids a paused effect resuming late),
  `Automatic`/`Pause` for long ambient loops.
- `useUnscaledTime`: true only for effects that must keep animating during hitstop/pause (UI, the
  hit flash itself if hitstop uses timeScale and you want the flash visible). Decide per layer.

### Emission
- One-shots: `rateOverTime = 0`, bursts at t=0 (and staggered bursts for secondary pops:
  e.g. t=0 big, t=0.03 small).
- `Burst(time, MinMaxCurve(min,max))` with a count range, `cycleCount`, `repeatInterval` for
  crackle.
- Moving emitters (trails of smoke from projectiles): `rateOverDistance`, not over time, so density
  is speed-independent.

### Shape
- Pick the shape from physics: muzzle sparks → narrow cone (5–15°) along barrel; impact sparks →
  hemisphere or cone aligned to surface normal, biased toward the reflection direction; explosion
  → sphere; dust ring on landing → circle/edge with radial velocity; slash sparks → edge along
  the blade.
- `radius` small (0.01–0.1) for point events — a default radius of 1 m makes sparks appear from
  nowhere.
- `radiusThickness = 0` to emit from the surface only (rings, shells).
- `randomDirectionAmount` / `sphericalDirectionAmount` small (0.05–0.2) to break perfect cones.

### Velocity over Lifetime / Limit Velocity (drag)
- Real-feeling deceleration comes from **Limit Velocity over Lifetime** with `drag` (e.g. 2–6
  for sparks) or `dampen` (0.1–0.3) with a limit curve decaying to near zero — it gives the
  "explode then hang" motion pros use on smoke and fireballs.
- `radial` velocity for expanding rings; `orbital` for swirls; `speedModifier` curve for ease-out.

### Noise
- Smoke and embers: `strength` 0.2–1.0, `frequency` 0.3–1.5, `scrollSpeed` 0.2–1, `damping` on,
  `quality` Medium. Avoid on sparks (they should be ballistic) except a tiny amount.

### Size over Lifetime
- The single biggest quality lever. Shapes:
  - **Flash/pop**: 0 → 1 in first 10 % (ease-out), then hold/slow shrink.
  - **Spark**: 1 → 0 with ease-in (stays visible, then dies).
  - **Smoke**: 0.4 → 1 → 1.4 (ease-out growth, keeps expanding as it fades).
  - **Shockwave ring**: 0 → 1 ease-out (quick expand), alpha fades over the same time.
- Use `AnimationCurve` keyframes with tangents, not linear.

### Color over Lifetime
- Gradient with explicit alpha keys. Fade in over first 5–10 % (except core flash), fade out over
  the last 30–60 %.
- Temperature ramp for fire/sparks: white (HDR 6–20) → yellow (4–8) → orange (2–4) → deep red
  (≤1) → transparent. HDR intensity comes from the material colour or `startColor` with
  `ColorUsage(true, true)`; gradients themselves clamp to LDR in Shuriken, so put HDR
  multiplier in the material/vertex colour and the ramp shape in the gradient.
- Smoke: warm, slightly lit grey near the source → cool desaturated grey; alpha low (0.2–0.5 peak).

### Rotation over Lifetime
- Smoke: slow random (±10–45 °/s). Debris: fast random. Flash: 0.

### Texture Sheet Animation
- Flipbooks: set `numTilesX/Y`, `frameOverTime` curve. For variety, use random `startFrame` and a
  partial range so consecutive particles show different segments (muzzle flash trick).
- Stylised projects: disable frame blending (stepped).
- Enable flipbook blending in the material only for smooth realistic smoke (needs `UV2` +
  `AnimBlend` streams).

### Renderer
- `renderMode`: Billboard (round things), **Stretch** (sparks, rain, tracers — set `velocityScale`
  0.02–0.1 and `lengthScale` 1–3; `cameraVelocityScale` 0 usually), Horizontal Billboard
  (ground rings, decal-like puffs), Mesh (debris chunks, muzzle cross quads, shockwave rings that
  shouldn't face camera).
- `alignment`: View for billboards; Local/World for meshes that must keep orientation (muzzle
  star along barrel).
- `sortMode`: OldestInFront / YoungestInFront for layered smoke; Distance for big volumes.
- `sortingFudge`: push smoke behind flash (positive = rendered earlier/behind).
- `maxParticleSize`: raise from default 0.5 (fraction of screen) only for screen-filling effects
  close to camera; otherwise the cap silently shrinks near particles.
- `minParticleSize`: small non-zero (0.001) for distant sparks to avoid flicker.
- Shadow casting off for additive layers.

### Inherit Velocity
- Muzzle smoke and sparks on moving ships/vehicles: `mode = Initial`, multiplier 0.5–1, otherwise
  smoke appears to shoot backwards when the shooter moves.

## 3. API patterns (builder scripts)

Module properties are structs that write through to the system; assign to a local and set fields:

```csharp
var ps = go.AddComponent<ParticleSystem>();
var main = ps.main;
main.duration = 0.5f;
main.loop = false;
main.startLifetime = new ParticleSystem.MinMaxCurve(0.12f, 0.2f);
main.startSpeed = new ParticleSystem.MinMaxCurve(8f, 18f);
main.startSize = new ParticleSystem.MinMaxCurve(0.02f, 0.05f);
main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
main.gravityModifier = 0.8f;
main.simulationSpace = ParticleSystemSimulationSpace.World;
main.scalingMode = ParticleSystemScalingMode.Hierarchy;
main.maxParticles = 64;
main.stopAction = ParticleSystemStopAction.Disable;

var em = ps.emission;
em.rateOverTime = 0f;
em.SetBursts(new[] { new ParticleSystem.Burst(0f, new ParticleSystem.MinMaxCurve(10, 16)) });

var sol = ps.sizeOverLifetime;
sol.enabled = true;
sol.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
    new Keyframe(0f, 1f, 0f, 0f), new Keyframe(1f, 0f, -2f, 0f)));

var r = go.GetComponent<ParticleSystemRenderer>();
r.renderMode = ParticleSystemRenderMode.Stretch;
r.velocityScale = 0.04f;
r.lengthScale = 1.5f;
r.sharedMaterial = mat;
```

- `AddComponent<ParticleSystem>()` also adds a `ParticleSystemRenderer`, enables the Shape module
  and sets a default emission rate — your builder must override them.
- Child systems play with the parent when created under it and `ps.Play(true)` is called on the root.
- Prefab saving: `PrefabUtility.SaveAsPrefabAsset(root, path)` then `Object.DestroyImmediate(root)`.
- Create material assets with `AssetDatabase.CreateAsset(mat, path)` **before** assigning them to
  the renderer that's saved into the prefab, or the prefab will reference a non-persistent material.
- Gradients: `new Gradient { colorKeys = ..., alphaKeys = ... }` or `SetKeys(colorKeys, alphaKeys)`;
  max 8 keys each.
- Angles in the API are radians (`startRotation`, rotation over lifetime); shape `angle` and `arc`
  are degrees.

## 4. Custom vertex streams

Use them to pass per-particle data to the shader (erosion, random seed, lifetime fraction):

```csharp
r.SetActiveVertexStreams(new List<ParticleSystemVertexStream> {
    ParticleSystemVertexStream.Position,
    ParticleSystemVertexStream.Color,
    ParticleSystemVertexStream.UV,                // TEXCOORD0.xy
    ParticleSystemVertexStream.Custom1X,          // TEXCOORD0.z  -> erosion
    ParticleSystemVertexStream.StableRandomX,     // TEXCOORD0.w  -> per-particle seed
});
var cd = ps.customData;
cd.enabled = true;
cd.SetMode(ParticleSystemCustomData.Custom1, ParticleSystemCustomDataMode.Vector);
cd.SetVectorComponentCount(ParticleSystemCustomData.Custom1, 1);
cd.SetVector(ParticleSystemCustomData.Custom1, 0,
    new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 0f, 1f, 1f))); // erode 0→1 over life
```

The bundled `VFX_ParticleUnlit.shader` reads exactly this layout. If you change stream order, update
the shader. The renderer Inspector shows the packing in brackets next to each stream.

## 5. Sub-emitters, collision, lights, trails

- **Sub-emitters**: sparks that spawn a tiny flash on death, debris that leaves smoke on collision.
  `ps.subEmitters.AddSubEmitter(child, ParticleSystemSubEmitterType.Collision, ParticleSystemSubEmitterProperties.InheritNothing)`.
  The child must have its own emission disabled or set to 0 rate/bursts.
- **Collision**: `type = World`, `mode = Collision3D`, `quality = Medium/Low`, `bounce` 0.2–0.5,
  `dampen` 0.3–0.6, `lifetimeLoss` 0.2–0.5. Use for debris and sparks that should skitter. Costly at
  high counts — keep to secondary layers with < 50 particles.
- **Lights module**: a Light prefab (point, no shadows), `ratio` small, `maxLights` 1–4,
  `useParticleColor` true, intensity curve over lifetime. Good for spark clouds and embers. For a
  single muzzle/explosion flash, a dedicated `Light` driven by `LightFlash.cs` is more controllable.
- **Trails module**: for tracer-like sparks; `lifetime` 0.05–0.2, `widthOverTrail` taper 1→0,
  `dieWithParticles` true, `renderer.trailMaterial` set (additive).

## 6. Performance guardrails

- Particle count matters less than **overdraw**: large alpha-blended quads near the camera are the
  real cost. Prefer fewer, well-shaped smoke particles; shrink near-camera layers; use
  `maxParticleSize`.
- Pool frequent effects (gunfire, impacts). Instantiate/Destroy per shot causes GC and spikes.
- `stopAction`, finite `duration`, and `loop = false` on every one-shot; leaked looping systems
  are a common performance bug.
- Collision, noise (High quality) and lights module are the expensive modules; use them on small
  sub-layers.
- GPU instancing on mesh particles with the same material; atlas textures so layers share a material.
- Test in the Frame Debugger and Scene view Overdraw mode; profile with many simultaneous effects
  (e.g. 20 impacts on screen), not one.
