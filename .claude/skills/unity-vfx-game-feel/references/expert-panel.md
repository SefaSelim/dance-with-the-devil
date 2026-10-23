# Expert Panel — How should an AI build Unity VFX and game feel?

This is the record of the design discussion that produced this skill. Read it when you need the
*reasoning* behind a rule (for example, when a user pushes back on a choice and you need to
explain or adapt it). The rules themselves live in SKILL.md and the other references.

Panel:
- **Mara — Lead Unity VFX Artist** (shipped action games; thinks in shapes, timing and contrast)
- **Dev — Game Feel & Mechanics Expert** (combat/shooter feel; thinks in milliseconds, forces and player perception)
- **Ilya — Senior Technical Artist** (shaders, pipelines, tooling, performance budgets)

---

## Round 1 — Why does AI-generated VFX look bad?

**Mara:** I've reviewed a lot of AI output. The pattern is always the same: one Particle System,
`Default-ParticleSystem` material, Start Lifetime 5, Start Speed 5, Emission 10/s, a cone shape,
and maybe the colour changed to orange. That's not an effect, that's the component's factory
state with a tint. It loops forever, every particle is the same size, nothing fades, and it has
no relationship to the event it's supposed to sell.

**Dev:** And it's isolated. A muzzle flash with no recoil, no shake, no light, no sound hook is a
sticker. Feel comes from many systems firing *on the same frame*. AI tends to answer the
literal noun — "make a muzzle flash" — instead of the verb, which is "make firing feel powerful".

**Ilya:** There's also a tooling problem. The AI can't click the Inspector, so it writes prose like
"set Start Size to 0.5". The user does half of it, misses three fields, and the result is still
mostly defaults. If the AI writes an **Editor builder script** that constructs the whole prefab
through the API, every value is explicit, reviewable, and reproducible.

**Mara:** Agreed, but a builder script full of unexplained magic numbers is just a different kind
of bad. Numbers must come from reasoning — timing, scale, physics — or the AI will invent them.

**Consensus 1:** Failure modes are (a) defaults left in place, (b) single-layer effects,
(c) no timing design, (d) no coupling to feedback systems, (e) instructions instead of code,
(f) numbers with no rationale.

---

## Round 2 — Where should the AI start? Look, or numbers?

**Dev:** Start with the event. Who triggers it, how often, how heavy, how far from camera. A
pistol firing 6 times a second and a ship cannon firing every 8 seconds need opposite budgets.
Frequent events must be short, small and low-shake or the player gets fatigued; rare events can
be huge.

**Mara:** Then a **timing sheet**, before any particle exists. Pros block effects in three beats:
anticipation (often zero for gunfire), **impact frame**, and dissipation. The impact frame is the
brightest, largest, sharpest moment and it lasts 1–3 frames. Then a long, slow tail. Fast in,
slow out. Beginners do the reverse: everything fades in and out at the same speed.

**Ilya:** And write it in frames at 60 fps *and* milliseconds, because hitstop and timeScale make
"seconds" ambiguous. Also specify whether each piece runs on scaled or unscaled time.

**Dev:** One more thing on the read: what must the player understand? "I hit", "I got hit",
"this is dangerous", "this is a crit". The effect's job is that sentence. Spectacle that hides the
read — particles covering the enemy's tell — is a failure, not polish.

**Consensus 2:** Every effect starts with a brief (verb, weight 0–1, frequency, camera distance,
read) and a timing sheet. Only then are layers designed.

---

## Round 3 — Layering and contrast

**Mara:** Every good effect is a stack. For an impact I want at minimum:
1. **Core flash** — 1–3 frames, HDR white-hot, the biggest single shape.
2. **Primary shape** — the thing that defines the silhouette (a starburst, a fireball, the slash arc).
3. **Secondary detail** — sparks, debris, droplets; many, small, fast, with drag and gravity.
4. **Tertiary residue** — smoke, dust, embers; slow, long-lived, low contrast.
5. **Light** — a real light pulse so the world reacts.
6. **Persistent mark** — decal, scorch, crack.

And contrast between layers: big/medium/small, fast/slow, bright/dim, hard/soft. The ratio I
teach is roughly a few large elements, a dozen medium, dozens of small.

**Ilya:** Contrast also applies to blending. Additive for the hot core and sparks, alpha for smoke.
Smoke drawn additive turns into glowing fog — a classic beginner tell. I prefer one
"alpha-additive" (premultiplied) shader where a slider goes from additive to alpha-blended, so
the AI uses one material model and can't pick the wrong blend mode by accident.

**Dev:** Don't forget variance. Players notice repetition fast — identical muzzle flashes every shot
read as a looping sprite. Random rotation, random flipbook start frame, size jitter ±20–30 %,
occasional bigger flash. But variance inside a **tight envelope**; the effect must still read the
same.

**Mara:** And every particle needs a beginning and an end that are designed. Nothing pops in at
full opacity unless it's the impact flash, and nothing just vanishes. Shrink, fade, or erode.

**Consensus 3:** Mandatory layer stack per effect type, explicit contrast ratios, randomness
within ranges, designed birth and death for every element.

---

## Round 4 — Shuriken vs VFX Graph vs custom code

**Ilya:** VFX Graph is GPU, millions of particles, great for big ambient stuff. But it has no CPU
readback for gameplay, needs compute support, and for one-shot gameplay effects with a few dozen
particles, Shuriken is cheaper to set up and works in every pipeline. Also its API is fully
scriptable, which matters for builder scripts. VFX Graph assets are awkward to author from code.

**Mara:** So: Shuriken by default for gameplay one-shots, VFX Graph when the user is on HDRP or
asks for it, or the count is genuinely huge (thousands of sparks, rain, swarms).

**Dev:** And some things shouldn't be particles at all. Sword trails especially.

**Ilya:** Right. The built-in Trail Renderer adds one segment per frame. A sword swing that covers
150° in 4 frames gives you a polygonal, chopped trail no matter what you set Min Vertex Distance
to. Every serious solution samples the blade's base and tip each frame and builds a custom mesh,
**subdividing between samples with a Catmull-Rom spline**. UVs: U = age along the trail, V across
the blade. Then a shader that erodes by age rather than just fading. Or, for authored attacks,
a pre-modelled slash mesh with UV-scrolled textures, timed to animation events.

**Mara:** Muzzle flashes: camera-facing billboard plus a mesh-based "star" or cross of quads
aligned to the barrel so it reads from the side too. Lasts 1–3 frames. A flipbook played from a
random start frame. Plus a point light for 2–4 frames.

**Ilya:** Decals: URP's Decal Projector. One shared material with GPU instancing and an atlas, pick
a tile by UV offset. Hard cap with a ring buffer so you never accumulate unbounded projectors.
Decals don't project onto transparent surfaces, so the impact needs a fallback.

**Consensus 4:** Shuriken by default, VFX Graph by exception, custom mesh for blade trails,
URP Decal Projector with an atlas and pool for marks.

---

## Round 5 — Game feel mathematics

**Dev:** Camera shake: random-per-frame offsets look like a broken camera. Use Squirrel Eiserloh's
**trauma** model. Events add trauma in 0..1, trauma decays linearly, the shake amount is
trauma² (or ³), and the offset comes from smooth noise (Perlin) sampled per axis with different
seeds. Squaring makes small hits subtle and big hits dramatic, and the noise keeps it continuous.
Rotational shake matters more than translational in 3D; translation makes the world swim.

**Mara:** Separate shake from **kick**. Vlambeer kick the camera backwards against the shot
direction *and* add shake. Kick is directional and recovers on a spring; shake is
non-directional noise. Mixing them into one random shake loses the information "where the force
came from".

**Dev:** Recoil the same way: decompose it. Gun model kick (visual, springs back), camera pitch
climb (affects aim, recovers partly), and screen shake (cosmetic). Put them on separate
transforms so they don't fight the mouse-look script. For springs I want a **second-order
system** with frequency f, damping ζ and initial response r — that's three parameters a designer
can understand, versus "lerp speed 10" which is frame-rate dependent and has no overshoot.

**Ilya:** Note that `Vector3.Lerp(a, b, k * dt)` is not frame-rate independent. Use
`1 - exp(-k*dt)` for exponential smoothing, or the second-order integrator with a stability clamp.

**Dev:** **Hitstop**: freeze the attacker and target for roughly 40–120 ms on melee contact,
scaled by hit weight. Research and fighting-game practice both land in that band; well past
200 ms reads as a hang. Don't freeze UI and audio. If you must use `Time.timeScale`, run the
restore on unscaled realtime and protect against overlapping hitstops.

**Mara:** And everything triggered by one impact must fire **on the same frame**. A shake that
starts a frame after the flash feels mushy. So I want one event struct — position, normal,
direction, intensity, surface — and every layer subscribes to it.

**Dev:** Last: accessibility. Shake and flashes need a global multiplier the player can lower.
Bake that into the shake system, not as an afterthought.

**Consensus 5:** Trauma² + Perlin shake, separate directional kick, decomposed recoil,
second-order springs, short selective hitstop, single-frame event fan-out, accessibility scalars.

---

## Round 6 — Making the AI actually do it

**Ilya:** Prompts like "make it look professional" do nothing. The skill has to make the AI emit
artifacts that are hard to fake:
- an assumption block (pipeline, version, scale, camera),
- a timing sheet,
- a layer table with counts, lifetimes, speeds, sizes, colours, blend,
- a builder script that sets **every** module it uses explicitly,
- a **default-override audit** — a list of the factory defaults it changed and why,
- a frame-by-frame description of what the player sees on frames 0, 1, 2, 4, 8, 16, 30.

If it can't fill those in, it hasn't designed the effect.

**Mara:** Also textures. AI usually has no art assets, so it falls back to the default particle.
Ship a procedural texture baker: soft glow, streak, smoke noise, ring, slash gradient, bullet-hole
mask. Procedural textures aren't final art, but they're far better than a white blob and they
let the shape and timing work be judged.

**Dev:** And ship the runtime pieces — trauma shake, springs, hitstop, recoil, decal pool,
blade trail — as tested templates, so the AI wires them up instead of reinventing a worse
version every time.

**Ilya:** One shader that works in Built-in and URP: an unlit pass with no LightMode tag renders
in URP's SRPDefaultUnlit path, so a hand-written particle shader avoids the fragile business of
configuring URP's particle material keywords from script.

**Mara:** Performance guardrails too: max particles set deliberately, stop action on one-shots,
pooling, overdraw awareness (large alpha quads covering the screen are the expensive part, not
the particle count), and decal caps.

**Consensus 6 (final):** The skill is a workflow plus an output contract plus bundled code.
The AI must design before building, override defaults explicitly, justify numbers, wire feel
systems through one event, and self-review against a checklist drawn from community critique.

---

## Disagreements left open (adapt per project)

- **Shake on the player's own weapon fire.** Dev wants small trauma on every shot for arcade
  shooters; Mara argues realistic shooters should use camera recoil and weapon kick only, with
  shake reserved for incoming damage and explosions. → Ask, or infer from the stated genre.
- **Timescale hitstop vs selective freeze.** Global timeScale is simplest and fine for
  single-player; selective freeze is better for multiplayer or when other actors must keep moving.
- **Realism vs stylisation.** Mara's colour ramps assume a semi-realistic, bloom-heavy look.
  Stylised/toon projects want flat colours, hard edges, fewer smoke layers, and stepped
  (non-interpolated) flipbooks.
