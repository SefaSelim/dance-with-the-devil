# Level: Gothic Arena — "The Desecrated Nave"

Status: built (2026-10-24, SefaSelim with Claude Code). Visual dressing only; the GDD §9 block layout, colliders,
bounces, spawns and NavMesh are unchanged. Design source: `MAIN_GDD.md` §9.
Working plan from the level-designer agent: `production/levels/gothic-arena/layout.md` (placement tables, lighting
rig, readability rules). Builder: `Assets/_Game/Editor/GothicArenaBuilder.cs`, menu **Dance/Arena/Rebuild Gothic
Dressing**; also runs at the end of `Dance/Placeholders/Build Arena Scene`.

## Overview
A cathedral courtyard turned graveyard, the inside of an exhausted mind. Tall lit backdrop on the N/E (what the
camera faces), low ruined edge on the S/W (camera side, ≤ 1.5 m). The fight happens in the dim, empty centre.

## Zones and landmarks
| Quadrant | Landmark | Light |
|---|---|---|
| NW | Gargoyle statue on the L block's outer corner, candelabra, skulls, open book, tomb slabs | cold teal-blue, shadowed |
| NE | Spire (Topper) in the corner behind both walls, crypt beyond the N wall, tombstones and fern | lavender, shadowed |
| SE | Statue facing the centre, altar (tabernacle with a small pentagram), candles, skulls | desaturated amber |
| SW | Low burning rubble/bone pyre with embers | dull amber |

Entrances: N/E framed by buttress pillars with fire bowls; S/W by half pillars with lanterns; each has a warm pool.

## As built vs the plan
- Ruin wall pieces were swapped for plain stone walls (their plaster face read as beige panels in the captures).
- Torches on the buttresses were dropped (no clean mounting face); fire bowls carry the gate read.
- FX_Ash_01 and FX_Fog_01 were dropped: in the low-res orthographic camera they read as grey slabs over the arena.
- L blocks: wrapped in 1.5 m half walls (outer face 1.5 cm proud of the collider) with stone floor tiles on top,
  instead of a recoloured cube.
- Floor: Synty cracked stone tiles over the whole walled area (168 tiles) instead of the placeholder checker.
- Sunbeam FX not used; window light comes from three cool point lights.

## Fixes after the first build (2026-10-24 18:55)
- Entrances are closed: the wall colliders run through every opening, so an open arch read as an invisible wall. N/E:
  closed iron cemetery gate (`SM_Bld_Gates_Cemetary_02`) between the buttresses; S/W: a low fence. A short paved path
  leads to each gate outside. Enemies still appear on the plates in front of the gates.
- S/W (camera-side) walls, pillars and fences are 1 m high (were 1.5 m): at 1.5 m they hid most of Defne when she stood
  against them (camera pitch 35°). Lanterns sit on the 1 m pillar tops.
- Nothing visible stands where the player can walk: the N/E wall line moved back to 14.35 / 16.35 so the windows,
  pillars and buttresses end on the collider faces; L-block corner pillars sit inside the block footprint.
- Tall tombstones in the NE/NW corners (walk-through, no collider) became flat grave slabs.
- Floor tiles no longer stick out past the S/W walls; block-top tiles sit above the half-wall caps (no bright edge lines).

## Checks (2026-10-24)
373 dressing objects, 0 colliders; every object rests on y 0, the block tops (1.5) or a pillar top (1.51), except
the crypt and cemetery gate, sunk 5 cm into the outside ground on purpose. Nothing low stands in the centre 14×12 m
except the L blocks' own wall cladding. EditMode 5/5, PlayMode 22/22 after the change.

## Open
- Wall see-through when the S/W wall hides Defne is still not built (GDD §9 line, not needed at 1.5 m).
- Pentagram on the altar is red; it is static and small, but watch it in playtests against the red-hue rule.
