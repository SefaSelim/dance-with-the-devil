# Effect Recipes — Layer Tables With Starting Values

Values assume 1 unit = 1 m, 60 fps, semi-realistic style with Bloom. Treat every number as a
**starting point inside a reasoned range**, then scale by weight, camera distance and art style.
Stylised projects: fewer smoke layers, flat colour ramps, harder edges, stepped flipbooks,
larger shape exaggeration.

Contents
1. Small-arms muzzle flash
2. Ship / naval cannon blast
3. Bullet impact + decal (by surface)
4. Sword slash + blade trail
5. Melee hit spark / hit confirm
6. Explosion (grenade-scale)
7. Landing / footstep dust
8. Scaling rules

---

## 1. Small-arms muzzle flash (pistol / rifle)

Brief: frequent (2–12/s), weight 0.2–0.5. Read: "I fired, from here, in that direction".
Must be lean — it repeats constantly.

| Layer | Count | Life | Speed | Size | Colour / HDR | Render | Notes |
|---|---|---|---|---|---|---|---|
| Core flash (front) | 1 | 0.03–0.05 s (2–3 fr) | 0 | 0.15–0.3 | white-yellow ×8–15 | Billboard, Local | random rotation, random flipbook segment |
| Side star | 1 | same | 0 | 0.25–0.45 long | yellow-orange ×6–10 | Mesh (cross of 2–3 quads along barrel), Local | reads from side view; scale X/Y jitter ±25 % |
| Sparks | 3–8 | 0.08–0.18 s | 10–25 | 0.015–0.03 | yellow→orange ×4 | Stretch, velScale 0.03, World | cone 6–12°, drag 2–4, gravity 0.5 |
| Smoke wisp | 1–3 | 0.4–1.0 s | 0.5–1.5 | 0.1→0.4 | grey α 0.15–0.3 | Billboard alpha, World | inherit velocity, noise; fewer when firing fast |
| Light | 1 | 0.03–0.06 s | — | range 4–8 m | warm, intensity spike then ~0 | point, no shadows | `LightFlash` |
| Feedback | — | — | — | — | — | — | weapon kick + camera recoil; trauma 0.03–0.1 only for arcade feel |

Notes: brass casing ejection (mesh particle with collision, or rigidbody pool) is part of the
feel for realistic shooters. Heat haze (distortion) is optional and expensive.

## 2. Ship / naval cannon blast

Brief: rare (every few seconds), weight 0.8–1. Read: "a massive force left the ship, aimed there".
Scale is large: barrel ~0.2–0.4 m bore, plume travels several metres.

| Layer | Count | Life | Speed | Size | Colour / HDR | Render | Notes |
|---|---|---|---|---|---|---|---|
| Core flash | 1–2 | 0.05–0.08 s | 0 | 1.5–3 | white ×15–25 | Billboard | biggest element, frame 0–3 |
| Fireball | 4–8 | 0.15–0.35 s | 6–14 forward | 0.6→1.6 | yellow→orange→red ×6→1 | alpha-additive, World | cone 10–20°, Limit Velocity drag 4–6 → "blast then hang" |
| Directional smoke plume | 10–20 | 2–5 s | 4–12 then dragged | 0.8→3.5 | warm grey → cool grey α 0.4→0 | alpha, World | drag + noise + slight lift; erosion out; inherit ship velocity |
| Side vent smoke ring | 6–10 | 1–2 s | radial 2–4 | 0.6→2 | grey | alpha | circle shape around barrel, perpendicular to shot |
| Sparks / burning wadding | 15–30 | 0.4–1.2 s | 8–20 | 0.03–0.08 | orange ×4 → red | Stretch + Trails module | gravity 1, collision with water off (die on y=water) |
| Shockwave | 1 | 0.12–0.2 s | 0 | 0→4 | white α 0.3 | Mesh ring or distortion | ease-out expand |
| Water ring / spray (if near water) | 1 + 10–20 | 0.3–1 s | radial | — | white | Horizontal billboard + droplets | sells contact with the sea |
| Light | 1 | 0.08–0.15 s | — | range 15–30 m | warm, very bright | point | lights the deck/sails |
| Feedback | — | — | — | — | — | — | trauma by distance falloff (0.5–0.8 at deck); ship recoil impulse `AddForceAtPosition(-dir * k, muzzlePos)`; roll spring; rumble low+high motor 0.15 s |

Audio timing: distant observers hear the boom `distance / 343` s after the flash. Applying this
delay for NPC ships far away makes naval combat read as big; never delay the player's own shot.

## 3. Bullet impact + decal

Brief: very frequent, weight 0.1–0.3 per hit. Read: "it hit here, and the surface is X".
Surface identity is the whole point — pick layers by surface:

| Surface | Primary | Secondary | Residue | Decal |
|---|---|---|---|---|
| Metal | bright spark burst (HDR ×6–12), reflected | 6–15 sparks, stretch, gravity 1, drag 2, optional collision bounce | tiny dark smoke | dented bright-rim hole, small |
| Concrete / stone | dust puff (alpha, beige/grey) | 4–10 chunks (mesh, gravity 2, collision) + few dim sparks | dust lingers 1–2 s | chipped crater, cracks |
| Wood | splinter flash (low HDR) | splinter mesh particles, stretched | light dust | dark hole with fibrous edge |
| Dirt / sand | upward dirt spray (alpha, brown) | clumps with gravity 2 | dust | soft dark smudge |
| Water | vertical splash column | droplets, gravity 1.5 | ring ripple (horizontal billboard) | none |
| Flesh / creature | follow the project's content rating | — | — | — |
| Glass | shard sparkle | shard meshes | — | crack decal on mesh, not projector if transparent |

Directions: primary along the surface normal; sparks biased toward `Vector3.Reflect(incomingDir, normal)`
blended with the normal (e.g. `normalize(lerp(normal, reflect, 0.6))`); align the effect with
`Quaternion.LookRotation(normal)`.

Decal rules (URP Decal Projector):
- Size 0.05–0.15 m for bullets, random ±20 %; random rotation around the normal (0–360°).
- Projector depth ~0.1–0.3 m, oriented so it projects **into** the surface
  (`rotation = Quaternion.LookRotation(-normal, randomUp)`), positioned slightly outside the surface.
- One material with GPU instancing; atlas 2×2 or 4×4 variants, select by `uvScale` + `uvBias`.
- Cap with ring buffer (64–256 depending on platform); fade the oldest via `fadeFactor`
  before reuse. `DecalPool.cs` implements this.
- Decals don't project onto transparent surfaces → glass/water need particles only or a mesh crack.
- Moving targets: parent the projector to the hit transform, or skip the decal.
- Requires a Decal Renderer Feature on the URP renderer. DBuffer technique supports normals/
  smoothness blending; Screen Space is cheaper and works on mobile/deferred.

## 4. Sword slash + blade trail

Brief: frequent within combos, weight 0.3–0.8 per swing. Read: "the blade swept this arc, fast".

| Layer | Implementation | Timing | Notes |
|---|---|---|---|
| Blade trail | `BladeTrail.cs` custom mesh: sample base+tip each frame, Catmull-Rom subdivide 4–8 steps between samples | active during swing window via animation events; trail time 0.08–0.2 s | U = age, V = base→tip; colour/alpha strongest at blade, fades with age; erosion by age; HDR rim near the tip |
| Slash arc (authored) | Optional mesh arc with scrolling gradient texture, spawned on the active frames | 3–8 frames | for heavy/special attacks; aligns with swing plane |
| Air distortion | Optional refractive arc | same as slash | skip on mobile |
| Swing sparks / glints | Few stretched particles from blade edge (shape: edge/mesh) | during fastest frames | only for magical/fire blades |
| On hit | → melee hit spark (recipe 5) + hitstop + shake + target flash | contact frame | the swing itself shouldn't shake the camera; the hit should |

Trail shaping: width doesn't need to taper if you erode; pros often keep constant width and
erode with a gradient/noise height map driven by age (clean, "carved" look). Avoid UV
recalculation swimming: keep U tied to age, not to world distance, or texture swims.

## 5. Melee hit spark / hit confirm

Brief: weight 0.3–1 per hit, triggered on contact frame. Read: "I connected, and this hard".

| Layer | Count | Life | Notes |
|---|---|---|---|
| Hit flash (star/cross) | 1–2 | 2–4 frames | HDR white ×10–20, size by weight, oriented to swing plane |
| Directional slash streak | 1 | 4–8 frames | stretched quad along swing direction |
| Sparks / shards | 6–20 | 0.15–0.4 s | cone along swing direction + normal, drag 3, gravity 0.8 |
| Ring / shockwave | 0–1 | 0.1–0.15 s | heavy hits only |
| Target flash | — | 2–4 frames | material emission/white tint on the target (MaterialPropertyBlock), then ease out |
| Feedback | — | — | hitstop 40–120 ms by weight (attacker + target), trauma 0.15–0.5, target knockback squash, rumble short |

Hit effects should play on **unscaled time** if hitstop uses `timeScale`, or they'll freeze too.

## 6. Explosion (grenade-scale, radius ~4 m)

| Layer | Count | Life | Notes |
|---|---|---|---|
| Flash | 1 | 3–5 frames | HDR ×20, size 3–5 m |
| Fireball | 6–12 | 0.3–0.8 s | sphere, radial speed 6–12 with strong drag, size growth, flipbook/erosion, ramp to dark |
| Smoke column | 8–16 | 3–6 s | spawns slightly delayed (0.05–0.15 s), rises, noise, darker |
| Debris | 10–30 | 1–2 s | mesh, collision, gravity 2, smoke trail sub-emitter on a few |
| Sparks / embers | 20–60 | 0.5–2 s | stretch, gravity, lights module on 2–4 |
| Ground ring dust | 1 + 10 | 0.5–1.5 s | horizontal ring, radial |
| Shockwave | 1 | 0.15–0.25 s | distortion or bright ring |
| Light | 1 | 0.1–0.3 s | range 15–25 m, intensity curve with flicker |
| Scorch decal | 1 | pooled | 2–4 m |
| Feedback | — | — | trauma by distance (inverse-square-ish falloff clamped), FOV punch, slight time dilation for very big ones, rumble, audio with distance delay |

## 7. Landing / footstep dust

Ring of 6–12 horizontal-billboard puffs from a circle shape, radial speed 1–3 with drag, life 0.4–0.8 s,
alpha low; scale by fall velocity. Feedback: camera dip on a spring (`SecondOrderDynamics`, f≈3,
ζ≈0.5), squash-and-stretch on the character root, tiny trauma only for heavy falls.

## 8. Scaling rules

- **Weight → numbers**: size ∝ weight^0.5–1, trauma ∝ weight² (the shake squares again on output),
  hitstop ∝ weight (40 → 120 ms), counts ∝ weight, residue lifetime ∝ weight.
- **Frequency → budget**: effective visual "energy per second" should stay roughly constant. If fire
  rate doubles, shrink smoke and trauma per shot.
- **Camera distance**: first-person muzzle effects are seen at 0.5–1 m and must not fill the screen;
  reduce size/brightness relative to third-person, and raise `minParticleSize` for far ones.
- **Style**: realistic → more residue, subtle flashes; arcade → bigger flash, more shake, shorter
  residue; toon → flat colours, outlines/hard edges, stepped animation, held key poses for 2–3 frames.
