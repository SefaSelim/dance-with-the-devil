# Game Feel Mathematics

Contents
1. The feedback stack and same-frame rule
2. Camera shake: trauma model
3. Kick vs shake
4. Springs: second-order dynamics
5. Frame-rate-independent smoothing
6. Recoil decomposition
7. Hitstop / hit pause
8. Easing and squash-and-stretch
9. FOV punch, time dilation, rumble, audio
10. Accessibility and fatigue

---

## 1. The feedback stack and the same-frame rule

Impact feel is the sum of many small channels arriving together: visual flash, particles, light,
camera (shake/kick/FOV), object response (recoil, knockback, squash), time (hitstop/dilation),
audio, haptics, UI (hit marker, damage number). Each channel alone is subtle; together they read as
weight. The single most common integration bug is channels arriving on different frames (shake
in `LateUpdate` next frame, light in a coroutine after `yield return null`). Fan out from one
`ImpactEvent` in the same call. Apply camera offsets in `LateUpdate` of the same frame, after
the camera's follow logic.

Also invest in **failure feedback** as much as success: getting hit, a blocked attack, a dry fire.
If only successes have juice, failures feel like bugs.

## 2. Camera shake — trauma model (Squirrel Eiserloh, GDC 2016 "Juicing Your Cameras With Math")

```
trauma  ∈ [0,1]; events: trauma = clamp01(trauma + amount)
trauma -= decayPerSecond * dt                 (linear decay, e.g. 1.0–1.8 /s)
shake   = trauma ^ exponent                   (exponent 2 or 3)
yaw     = maxYaw   * shake * noise(seed0, t * frequency)    noise mapped to [-1, 1]
pitch   = maxPitch * shake * noise(seed1, t * frequency)
roll    = maxRoll  * shake * noise(seed2, t * frequency)
offset  = maxOffset * shake * noise(seed3..5, t * frequency)   (small or zero in 3D)
```

- Squaring makes small hits subtle and large hits dramatic, and makes the decay feel like it
  "settles" (fast at first, gentle at the end).
- **Perlin noise, not Random.Range per frame**: random per-frame offsets are discontinuous and read as
  a broken camera; smooth noise reads as a physical camera being jostled. `Mathf.PerlinNoise` returns
  ~[0,1]; remap `* 2 - 1`. Use different seeds per axis.
- Frequency 15–30 Hz for violent impacts, 5–10 Hz for heavy rumbles/earthquakes.
- **Rotation over translation in 3D**: rotational shake (degrees) gives the jolt; translational shake
  moves the camera through the world and can clip geometry. 3D starting limits: yaw/pitch 2–6°, roll
  2–8°, offset 0–0.1 m. 2D: offset in pixels/units and roll only.
- Apply to a **child transform of the camera rig** (or a Cinemachine Impulse/extension) so it never
  accumulates into the real camera position or fights mouse-look.
- Distance falloff for world events: `amount * clamp01(1 - d / radius)^2`, or inverse-square clamped.
- Starting trauma amounts: own pistol shot 0–0.05 (arcade only), taking light damage 0.2–0.3, heavy
  melee hit landed 0.3–0.5, nearby explosion 0.6–1.0.
- With Cinemachine: `CinemachineImpulseSource.GenerateImpulseWithForce(force)` / `GenerateImpulse(velocity)`
  plus an Impulse Listener on the camera. Configure the source's signal shape and time envelope
  (attack ~0, sustain short, decay longer) and use channel masks to separate "player damage" from
  "world explosions". The trauma script is still useful for 2D, non-Cinemachine rigs and precise control.

## 3. Kick vs shake

- **Kick** is directional: a displacement/rotation opposite the force (camera pushed back from a gun,
  tilted away from the side you were hit), recovering on a spring. It tells the player *where the
  force came from*.
- **Shake** is non-directional noise. It tells the player *how violent* it was.
- Use both for big events; kick alone for frequent events (each gunshot), shake for incoming damage
  and explosions. Vlambeer's approach for a single pistol shot: kick the camera back a few pixels
  against the aim direction and add a small amount of shake that decays quickly.

## 4. Springs — second-order dynamics

Designer-facing parameters (from t3ssel8r's "Giving Personality to Procedural Animations"):
- `f` frequency (Hz): speed of response / oscillation.
- `ζ` damping: 0 = oscillates forever, 0 < ζ < 1 = overshoot and settle (bouncy), 1 = critically damped
  (fastest without overshoot), > 1 = sluggish.
- `r` initial response: 0 = eases in, 1 = immediate, > 1 = overshoots at start, < 0 = anticipates
  (moves the wrong way first — great for mechanical "wind-up").

```
k1 = ζ / (π f)
k2 = 1 / (2π f)^2
k3 = r ζ / (2π f)
each step (dt = T, target x, target velocity xd ≈ (x - xPrev)/T):
  k2s = max(k2, T*T/2 + T*k1/2, T*k1)      // stability clamp for large dt
  y  += T * yd
  yd += T * (x + k3*xd - y - k1*yd) / k2s
```

Typical presets:
| Use | f | ζ | r |
|---|---|---|---|
| Weapon kick recovery (snappy) | 6–10 | 0.35–0.6 | 2–4 |
| Camera recoil recovery | 3–6 | 0.7–1.0 | 0–1 |
| Landing camera dip | 2.5–4 | 0.4–0.6 | 0 |
| UI pop / button | 4–6 | 0.3–0.5 | 2 |
| Heavy ship roll after broadside | 0.4–0.8 | 0.15–0.3 | 0 |
| Smooth follow, no overshoot | 1.5–3 | 1 | 0 |

Impulses: to "kick" a spring, add to its velocity (`yd += impulse`) or offset its target briefly.
Adding velocity gives natural overshoot and recovery shaped by f/ζ. `SecondOrderDynamics.cs` provides
float/Vector3 versions with `AddVelocity`.

## 5. Frame-rate-independent smoothing

- `Lerp(current, target, k * dt)` is frame-rate dependent and wrong for large dt.
- Use `current = Lerp(current, target, 1 - exp(-λ * dt))`, where λ = ln(2)/halfLife. Half-life is a
  designer-friendly unit ("halves the remaining distance every 0.05 s").
- `Vector3.SmoothDamp` is fine (critically damped approx) when no overshoot is wanted.

## 6. Recoil decomposition (shooters)

Separate transforms, separate meanings:
1. **Weapon visual kick** (viewmodel): position back (−Z 0.02–0.08 m), pitch up (2–8°), small random
   roll/yaw (±1–3°). Spring with high f and some overshoot. Pure cosmetic.
2. **Camera recoil (aim punch)**: pitch up 0.3–2° per shot, yaw from a **pattern** (designed or
   seeded curve) plus small random spread; accumulates during sustained fire; recovers toward the
   **pre-spray aim** (partial or full, recovery spring f 3–6) after firing stops. This affects aim and
   is a balance parameter.
3. **Screen shake**: optional small trauma; cosmetic.
4. **Spread / bloom**: gameplay accuracy, separate from visuals.

Hierarchy: `CameraRoot (mouse look) → RecoilPivot (camera recoil) → ShakePivot (shake) → Camera`
and `Camera → WeaponSway → WeaponKick → WeaponModel`. Each script owns one transform; nothing fights.

Recoil pattern: an array or AnimationCurve of (yaw, pitch) per shot index; multiply by a per-shot
random factor 0.85–1.15; reset shot index after a delay without firing (≈ 2–3× fire interval).

## 7. Hitstop / hit pause

- Duration: melee 40–120 ms (≈ 3–7 frames @ 60), scaled by hit weight; heavy finishers up to
  ~150–200 ms. A subjective study on 3D action games found ~0.1–0.2 s optimal for knock-away hits;
  beyond that it reads as a hang. Fighting games commonly use a handful to ~10+ frames.
- What freezes: attacker and target animations (Animator.speed = 0 or a per-actor time scale), their
  physics and particles. What keeps running: UI, audio, camera shake (shake *during* hitstop sells it),
  hit flash on unscaled time.
- Global `Time.timeScale` freeze is acceptable for single-player prototypes. Then: restore with
  `WaitForSecondsRealtime`, store and restore the *previous* scale (don't force 1 — you may be in slow-mo
  or paused), merge overlapping hitstops by extending the end time, never start hitstop while paused.
- Variants: partial slow (timeScale 0.05–0.2 instead of 0), and target-only shake during stop
  (offset the victim's model ±0.02–0.05 m at 30 Hz).
- `HitStop.cs` implements global and selective (per-Animator) modes.

## 8. Easing and squash-and-stretch

- Impact elements: ease-out (fast start, slow end): `1 - (1 - t)^3`, or exponential.
- Dying elements: ease-in to zero (`t^2`) so they stay readable then vanish.
- Overshoot (`easeOutBack`) for pops and UI.
- Squash-and-stretch on characters/props: preserve volume — scale Y by `s`, X/Z by `1/sqrt(s)`.
  Landing squash s = 0.8–0.9 for 2–4 frames, then spring back. Projectile stretch along velocity.

## 9. FOV punch, time dilation, rumble, audio

- **FOV punch**: +2–6° (or −2–4° for a "zoom-in" thump) on big shots/explosions, recover on a
  spring (f ≈ 4–6, ζ ≈ 0.6). Keep FPS FOV punches small; they cause discomfort.
- **Time dilation**: for kills/finishers, timeScale 0.2–0.4 for 0.2–0.6 s real time with ease back.
  Scale `Time.fixedDeltaTime` with timeScale if physics must stay smooth
  (`fixedDeltaTime = baseFixed * timeScale`), restore after.
- **Rumble** (Input System): `Gamepad.current?.SetMotorSpeeds(low, high)` then
  `ResetHaptics()` after 0.05–0.25 s on unscaled time. Low motor = heavy/body, high motor = sharp/buzz.
  Guard null, respect a user setting, and reset on pause/focus loss.
- **Audio**: fire the sound on the same frame as the flash; randomise pitch ±3–8 % and volume
  ±1–2 dB; layer transient + body + tail; never delay the player's own action sound. Distant world
  events can delay by distance / 343 m/s.

## 10. Accessibility and fatigue

- Global multipliers: `ShakeScale`, `FlashScale`, `RumbleScale` (0–1), applied inside the systems.
- Avoid large full-screen flashes repeating faster than ~3 per second (photosensitivity guidance).
- Fatigue: the player sees frequent effects thousands of times. If an effect is still noticeable as
  "an effect" after a minute of play, it's too strong for its frequency. Test with 30–60 s of
  continuous use.
- Clamp trauma accumulation from many simultaneous events (it's already clamped to 1; also consider
  per-source cooldowns so 20 bullets hitting a wall nearby don't max it out).
