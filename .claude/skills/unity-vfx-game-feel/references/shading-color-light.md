# Shading, Colour & Light for VFX

## HDR and Bloom
- Values above 1.0 only glow if the camera renders HDR and a Bloom post-process is active
  (URP: Volume with Bloom, camera Post Processing enabled, URP asset HDR on). Check or state it.
- Bloom threshold ~0.9–1.1. The hottest element (core flash) should be the only thing far above
  threshold (×8–25); sparks ×3–8; fire body ×1.5–4; smoke ≤ 1 (never glows).
- Too many elements far above threshold = a blown-out white mess with no shape. Brightness
  contrast is as important as size contrast.
- In C#, HDR colours: `new Color(r, g, b) * intensity` or `Color.HSVToRGB(h, s, v, true)` with v > 1.
  Expose as `[ColorUsage(true, true)] public Color tint;`.

## Colour ramps
- Fire and hot sparks follow blackbody temperature: white → pale yellow → yellow-orange → orange-red
  → deep red → dark. Hue shifts **toward red as it cools while brightness falls**. A single orange
  tint across the whole life is the beginner tell.
- Smoke picks up the fire colour near its base (warm, faintly lit) and cools to desaturated
  grey-blue. Vary value slightly per particle.
- Magic/energy: core near-white of the hue, mid saturated hue, edges a shifted analogous hue
  (e.g. cyan core → blue → violet edge). Keep saturation highest in the mid-tones, not in the core.
- Readability against the environment: test on dark and bright backgrounds. Additive layers vanish
  on bright skies → add an alpha-blended darker layer (smoke, dark core outline) underneath.

## Blending
| Mode | Use | Pitfall |
|---|---|---|
| Additive | flashes, sparks, glows, energy | invisible on bright backgrounds; smoke in additive = glowing fog |
| Alpha blend | smoke, dust, debris, blood | needs correct sorting; large quads = overdraw |
| Alpha-additive (premultiplied, one shader) | fire bodies and anything transitioning from glow to smoke | requires premultiplied output: `rgb * a`, alpha scaled by (1 − additive) |
| Multiply | darkening scorch overlays, shadows | rarely right for particles |

The bundled `VFX_ParticleUnlit.shader` uses premultiplied alpha with an `_Additive` slider (0 = alpha
blend, 1 = additive), plus per-particle control via vertex colour alpha. One shader model → no wrong
blend-mode choices, and a fireball can go from additive to smoky by animating the slider or using
two materials.

## Erosion (dissolve) instead of fade
Fading alpha uniformly makes particles look like transparent stickers. Pros erode: the texture's
alpha/height map is thresholded by a value that rises over life:
`a = smoothstep(erosion, erosion + softness, tex.a) * vertexAlpha`.
The edges eat inward, breaking the silhouette as it dies. Drive `erosion` with Custom Data
(Custom1.x) over lifetime, so each particle can vary. Works best with textures that have soft
gradient alpha (smoke puffs, noise blobs), not hard-edged masks.

## Soft particles
Smoke and dust intersecting the ground or walls shows hard lines. Soft particles fade by the
difference between scene depth and particle depth. Needs the camera depth texture (URP: Depth
Texture on in the URP asset or camera). The bundled shader exposes `_SOFTPARTICLES_ON` and
`_SoftFade` (distance in metres, 0.2–1).

## Flipbooks
- Smoke/fire flipbooks (4×4, 8×8) make individual particles feel volumetric. With procedural
  textures only, emulate with rotation + erosion + noise scroll.
- Random start frame per particle; consecutive particles should not show the same frame.
- Frame blending for smooth realistic smoke; stepped for stylised.

## Textures (when the project has none)
`VFXTextureBaker.cs` creates: soft glow (gaussian), hard-core glow (sharp core + soft falloff),
spark streak (horizontal gradient, bright head), smoke puff 4×4 flipbook (fbm noise blobs with
gradient alpha for erosion), ring (thin annulus), slash gradient (for trails: bright leading edge,
noisy tail), muzzle star (n-pointed star with soft spokes), bullet-hole mask (dark centre, rough
rim, radial cracks), scorch (noisy dark blotch). Import: alpha is transparency, wrap Clamp (Repeat
for trail/noise textures), mipmaps on, compression normal quality.

## Lights
- A real light pulse is what makes the world acknowledge the event. Point light, no shadows
  (shadowed point lights are expensive), range scaled with event size, intensity curve:
  spike on frame 0, ease-out to 0 over 2–8 frames. Add slight random intensity per shot.
- URP: check the URP asset's per-object light limit (Forward) — many simultaneous flash lights get
  culled. Use the Forward+ path for many lights, or keep only the nearest N flashes (pool lights).
- Lights in the particle Lights module: good for ember clouds (ratio low, maxLights 2–4).

## Post-processing pulses (feedback layer)
Brief Volume weight pulses on big events: chromatic aberration 0.2–0.5 for 0.1 s, vignette
intensity +0.1–0.2, lens distortion small, exposure/colour flash on damage. Drive a dedicated
Volume's `weight` with a curve rather than editing profile values every frame. Respect the
flash-reduction accessibility setting.

## Target flash (hit confirm on the victim)
Set `_EmissionColor` / a custom `_FlashAmount` via `MaterialPropertyBlock` for 2–4 frames on hit,
easing out. Use property blocks so you don't instantiate materials per hit.

## Overdraw
Big alpha particles near camera cost more than many small sparks. Keep near-camera smoke sparse,
use erosion to make fewer particles look denser, cap `maxParticleSize`, and check the Overdraw view.
