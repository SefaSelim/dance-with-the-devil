# Common Mistakes & Self-Review Checklist

Drawn from recurring critique on realtimevfx.com, r/Unity3D / r/gamedev feedback threads, Polycount,
GDC game-feel talks (Juice It or Lose It; The Art of Screenshake; Juicing Your Cameras With Math) and
the panel discussion. Run this before delivering.

## Mistakes reviewers point out again and again

**Timing and motion**
- Everything appears and disappears instantly — no designed fade-in/out, erosion or shrink.
- Uniform timing: every layer has the same lifetime, so the effect has no beat structure.
- Slow attack: the effect ramps up instead of hitting peak on the impact frame.
- Unnatural motion: constant velocity (no drag), sparks that float, smoke that moves linearly.
- No ending: the effect just stops; there's no residue or settle.
- Arrival vs departure: converging energy should coalesce inward, departing energy expands outward.

**Shape and contrast**
- Single layer; "a thing on top of a thing" — layers that don't feel like one event.
- Same size/speed/brightness for every particle; no large-medium-small contrast.
- Too many outward particles thrown in the player's face; distracting, hides gameplay.
- Effect too large or too long for how often it fires (fatigue).
- Flat look: no depth layers (front sparks, mid body, back smoke), or camera-facing quads exposed by
  rotating trails/edges.
- Spawn volume too big — sparks appear from empty space instead of the source point.
- Missing velocity-stretch on fast particles (they look like floating dots).

**Shading and colour**
- Default particle material / white blob texture.
- One flat tint over the whole life; no temperature ramp; HDR everywhere so nothing has shape.
- Additive smoke (glowing fog); additive effects invisible on bright backgrounds.
- Sorting errors: orb behind ground, smoke over flash.
- Hard intersection lines with geometry (no soft particles).

**Implementation**
- Built-in TrailRenderer on a fast sword swing → chopped polygonal trail.
- Looping left on; effects never stop; Instantiate/Destroy every shot (GC spikes); no pooling.
- Decals accumulate forever, z-fight, float off moving objects, or try to project on glass.
- `Lerp(a, b, k * dt)` smoothing; random-per-frame shake; shake applied to the camera transform the
  player-controller also writes.
- Hitstop via `timeScale = 0` that freezes UI/audio, is never restored correctly, or breaks when two
  hits overlap.
- Feedback channels firing on different frames.
- Effects that don't scale with the prefab (Scaling Mode Local).
- Effects that ignore the shooter's velocity (smoke shooting backwards from a moving ship).

**Design**
- Spectacle hides the read (can't see the enemy's tell or the hit location).
- Only success has juice; failures (getting hit, blocks, empty clip) have none.
- No accessibility scaling for shake/flash.

## Self-review checklist (answer each before delivering)

Design
- [ ] Assumptions stated (pipeline, version, scale, camera, style, Bloom).
- [ ] Brief has verb, read, weight, frequency.
- [ ] Timing sheet in frames + ms; impact frame is the peak; fast-in slow-out.

Layers and parameters
- [ ] All mandatory layers for this category present, or omission justified.
- [ ] Contrast in size, speed, brightness and lifetime across layers.
- [ ] Every lifetime/speed/size is a range, and its value is explained (scale, distance, physics).
- [ ] Size over Lifetime and Color over Lifetime (with alpha) on every visible layer.
- [ ] Drag on anything that's launched; gravity where physical; noise only on soft layers.
- [ ] Random rotation / flipbook start on round elements.
- [ ] Correct simulation space per layer (World for left-behind, Local for glued).
- [ ] HDR intensities set and consistent with Bloom; only the core is far above threshold.
- [ ] Blend mode correct per layer (additive glow, alpha smoke).

Implementation
- [ ] Builder script sets every used module explicitly; unused modules disabled.
- [ ] No default material, no default texture.
- [ ] One-shots: loop off, bursts with rate 0, stopAction set, maxParticles deliberate, pooled if frequent.
- [ ] Fast blade trails use the custom mesh trail, not TrailRenderer.
- [ ] Decals pooled with a cap, oriented into the surface, random rotation/size, atlas variant.
- [ ] Feedback wired from one ImpactEvent in the same frame; camera effects on separate pivots.
- [ ] Smoothing/springs frame-rate independent; scaled vs unscaled time decided per component.
- [ ] Shake/flash/rumble go through accessibility multipliers.
- [ ] API names and signatures checked against the target Unity version; anything unsure flagged.

Delivery
- [ ] Default-override audit included.
- [ ] Frame-by-frame read (0, 1, 2, 4, 8, 16, 30, 60) shows a changing shape every row.
- [ ] Tuning knobs listed.
- [ ] Evaluation steps given (0.1 timeScale, frame step, gameplay distance, 10× repeat, overdraw).
