# Graveyard Arena: fit-the-gameplay plan (level-designer)

Source: `Logs/map_survey.txt` (height grid + renderer list), `MAIN_GDD.md` §9. Plan only; the main session does the Unity work.
All coordinates are world x/z (metres). Plateau top is y 0..0.6. Verify every number in the Editor (section 6); the survey is a 2 m grid.

Level name: Graveyard on the Cliff. Theme: dark gothic cemetery. Target: ~15 x 12 m clear fighting core, 8 entrances, bullet-stopping cover only at the rim.
Pacing intent (per-encounter, not game-wide): centre = open dodge space; rim of tombs/lids/fence = cover and dead ends; lanes = long sightlines for Shooters.

## 1. Playable area

The survey shows the real plateau is larger than the fence (fence is only x -12.5..10.6, z -9.5..8.6 = 23 x 18 m, too tight and too tomb-dense for 32 enemies, and spawns would have to be inside it).

Decision: the grassy ring outside the fence is playable; the fence stays as an INNER obstacle ring (it blocks bullets at 1.0 m: 1.9 m tall, pillars 2.4 m) with openings cut into it (section 2).

Walkable polygon (clockwise, edges taken from where the grid drops to -1.5 or lower, 0.5-1 m safety margin):
(-16.0, 8.5) (-10.0, 11.8) (11.5, 11.8) (14.5, 9.0) (15.0, 3.0) (15.0, -5.0) (12.5, -10.5) (9.0, -13.5) (-8.0, -13.5) (-12.0, -11.0) (-16.0, -8.5).
About 30 x 25 m bounding box, ~600 m2.
- NE/NW/SW corners are ocean or cliff (grid z=12: x<=-12 and x>=14 are ocean; x=-18 slopes to -1.5).
- East stairs (Rock_Stairs_02 at x 14..16, z -4..-12) and the SW dock/stairs (x -20..-10, z -14..-16) are OFF the polygon: no spawns, no NavMesh.
- Start: Defne stays at (0, 0, 0). It is free: Dirt_Flat_01 (7) is the floor there. Only the crypt south of her moves (section 2).

Edge protection: the cliff Rocks do not form a closed wall, so add 11 invisible BoxColliders (height 3, thickness 1, centred 0.5 m outside each polygon edge) on the same layer as the existing arena walls. This keeps the player on the plateau; NavMesh only protects enemies.

Camera focus clamp (starting values): x -8..8, z -6..6, so the viewport (~23 m wide, 13 m ortho height) stays mostly over the plateau and background cliffs. The ocean will show a little at the corners; this map is a vista, so accept it, but the main session must walk the camera to the four clamp corners and check no black void or missing Background shows. Read the existing clamp component first and only change its numbers.

## 2. Open space (what to delete or move)

Centre rule: an ellipse around the origin, semi-axes 5.5 m (x) by 4.5 m (z) (about 11 x 9 m, matching the GDD ~12 x 10 m open centre). Inside it delete the whole GameObject (visual and collider) of every object under root Props: Tombs (Tomb_01/02, lids, Tomb_Stone_*), Corpses, Characters, Lights, Miscellaneous, Statues. Ground, Foliage, Cover stay.
Known hits: Tomb_01_(2) (-1.7,1.8) + lid; Tomb_02_(3) (-1.7,4.1) + lid (0.93 inside); Tomb_Stone_01_(1), _02_(2), Basic_02_(1), Body_Skeleton_03 (2,0), Sword_03 (1.4,1.7), helmet (1.1,3.3), Sword_03_(2) (-1.6,4.1), Tomb_Stone_05 (-6.3,1.6) is outside (x 6.3 > 5.5) so it stays.

Crypt that blocks the centre: SM_Prop_Crypt_01_(1) at (-0.6,-2.8) (footprint x -3.2..2, z -5.2..-0.4), touching Defne's start. MOVE it, do not delete (keeps 3 crypts):
- Move as a group: Crypt_01_(1), its Crypt_01_Glass_01, Crypt_Door_01, Base_Floor_Combined_01 (2), and the sarcophagus inside it (Tomb_01_(7) + Tomb_01_Lid_01 at (-0.7,-2.8)).
- New centre (-5.2, 0, -6.6) (offset (-4.6, 0, -3.8)); the door (+x side) then faces the south lane. New footprint x -7.75..-2.65, z -8.95..-4.25, its nearest corner is 1.12 outside the ellipse.
- Delete what is under the new footprint (+0.5 m): Tomb_Stone_03_(4) (-6.2,-6.4), Tomb_Stone_04 (-6.5,-7.8), Statue_People_Damaged_01 (-4.4,-8.6), _02 (-2.9,-8.2), Tomb_Stone_Basic_02 (-2.4,-7.0), Book_01 (-5.5,-6.2), helmet_(2) (-5.8,-6.5), Dirt_Small_01_(27)/(28) props only if they poke through (they are floor patches: keep).
- Camera caution: the crypt is 5.8 m tall. If the camera looks from the south-west, this spot can hide Defne. Check from the play camera; if it hides her, mirror the move to (+5.2, -6.6) instead.
- The two other crypts stay: Crypt_01 (-8.7,6.1) NW and Crypt_02 (7.0,4.5) NE are both 4+ m outside the ellipse and become the two big cover/landmark masses in the north half.

Lanes: 3.6 m wide (|offset|<1.8 from the line spawn -> origin) from every entrance to the ellipse edge. Delete or shift any Props object (Tombs, Statues, Tomb_Stones taller than 0.6 m, Ruins, Trees < 2 m trunk offset) whose centre lies in a lane. Fixed list:
- Gap N (fence): delete Fence_Cemetary_01_(4) (1.5,8.5), Fence_Cemetary_02_(2) (-0.9,8.5), Pillar_(1) (2.8,8.6); also Statue_02 (2.8,7.7), Tomb_Stone_03_(2) (0.9,7.5), Tomb_Stone_Basic_01 (1.0,6.1), SM_Bld_Ruin_04 (3.2,10.5). Opening x -2.2..3.2 (5.4 m). Tomb_02_(1) (-1.7,5.7) + lid also go (lane |x|<1.8).
- Gap S (fence): delete Fence_Cemetary_01_(16) (-1.0,-9.3), Fence_Cemetary_02_(7) (1.4,-9.3); also Tomb_Stone_03 (-2.2,-8.4), Tree_Trunk_01 (-1.3,-11.6). Opening x -2.25..2.65 (4.9 m).
- Gap SE (south fence): delete Fence_Cemetary_01_(18) (6.6,-9.3), _(19) (9.0,-9.3), SM_Bld_Ruin_04_(2) (7.2,-10.9), Tomb_Stone_05_(3) (8.8,-8.1), Tomb_Stone_02_(3) (8.1,-6.5). Opening x 5.45..10.25 (4.8 m).
- Gap E (gate): SM_Bld_Gates_Cemetary_01 (10.5,-0.3) is a closed mesh-collider gate between fence ends at z -1.95 and 1.25. Delete the gate collider and its object (or the whole object if it is a closed door model). Move Brazier_01_(3) (13.5,-1.3) to (13.5,-3.0). Opening z -1.95..1.25 (3.2 m, widen by deleting Fence_Cemetary_01 (22) (10.6,-3.2) -> z -4.45..1.25 = 5.7 m; recommended).
- Gap W (wide, "broken wall"): delete Fence_Cemetary_01_(10) (-12.4,-2.8), _(11) (-12.4,-0.2), _02_(5) (-12.4,-5.3), Pillar_(4) (-12.3,-1.8), Statue_01 (-11.6,-0.6), Tree_Dead_01 (-14.4,-1.5), SM_Bld_Ruin_04_(1) (-9.2,-5.3). Opening z -6.55..1.05 (7.6 m). The Well (-10,2) stays (it only touches z 0.15..3.85).
- NE and NW get no fence gap (crypts sit right behind them); those enemies run round the ring into the N/E/W gaps (10-15 m of approach).
- Tree_Dead_03 (-3.9,7.0): trunk is within 1-3 m of the N lane. If its trunk collider is inside |x|<2.2 after the check, delete it; otherwise keep (landmark).

Keeps the graveyard look: fence stays on 70% of its length, 3 crypts, well, big tombs on the rim, all dead trees except the ones listed.

Rim cover to keep (bullet-stoppers, not in any lane, outside the ellipse): Tomb_01/02 + lids at (-5.9,-2.3), (5.6,-4.1), (8.8,-2.0), (8.8,-3.8), (8.8,7.4), (5.7,1.6), (5.7,7.5), (-8.9,-4.1), (-8.6,6.1), (0.3,-5.8); Statue_People_01 (-3.9,7.8), Statue_People_03 (3.2,-8.5). About 12 pieces, 1.2-1.8 m high.

## 3. Colliders

Key fact: Ground "Grass_Small_01/Dirt_Small_01/Dirt_Flat_01" are NOT grass blades. They are the floor mesh patches (4-8 m wide, 0.1-1.2 m thick, overlapping). Removing their colliders leaves holes. Real blades are in Foliage (no colliders already).

| Category / name | Collider | Why |
|---|---|---|
| Ground (all 122) | KEEP | only floor; includes ring and mounds. Do not touch |
| Buildings: Base_Floor_Combined (3) | KEEP | floor under crypts (Box+Mesh) |
| Buildings: Fence + Pillars (not deleted ones) | KEEP | bullet-blocking inner wall |
| Buildings: Ruin_04/05 left | KEEP | 1.9 m walls, rim cover |
| Tombs: Crypts (3), Crypt_Door/Boarded | KEEP | big blockers |
| Tombs: Tomb_01/02 + Lid kept above | KEEP | 1.2-1.8 m cover |
| Tombs: Tomb_Stone_01/03/04 and Basic_* (0.6-2 m) | KEEP only outside the ellipse and lanes; delete inside | cover posts |
| Tombs: Tomb_Stone_05/06 (flat slabs, 0.3-0.4 m) | REMOVE | step-height bumps, snag nav |
| Statues, Well + Insert | KEEP (those not deleted) | tall |
| Trees: Tree_Dead_* (5 mesh colliders each) | KEEP | trunks block; branches are above 1.8 m agent height so the bake ignores them |
| Trees: Tree_Trunk_01 (3) (0.5-1.7 m logs) | REMOVE | snag |
| Rocks (138 colliders) | KEEP | rim; some are the only floor at the ring edge, e.g. (-16,0..4) Rock_Cliff_05, (14,-6) Rock_01 |
| Rocks: Cliff_Basalt, Basalt, Rock_Stairs_02, Background, Clouds, OceanPlane | KEEP collider, mark Not Walkable (section 4) | outside the play area |
| Corpses (53), Characters (8), Lights (braziers), Miscellaneous (Book, Flag, Meat_Bucket) | REMOVE | small, no play value; Lights: also check bullets do not hit emitters |
| Storage, Stage_Wooden, Cage (x -25..-21, z -16..-10, y -16) | leave | far below, outside the volume |
| Foliage, Cover, Clouds | confirm none (script: any Collider in these = remove) | |

Hole risk: (1) never disable a collider on a Ground piece; (2) rim Rocks above can be the floor where ring ground is thin (west x -16, east x 14 z -6..-4, north (0,12)); (3) Dirt_Small_01_(30) is already off, fine; (4) mounds at east (x 11..15, z -2.5..3, up to 1.2 m thick) might be steeper than slope 40: look at the baked mesh there.

## 4. NavMesh

Old `Arena_NavMesh.asset` is deleted in git status; re-bake in the scene's existing NavMesh component (use the existing pattern; do not swap to runtime-built).
- Volume: centre (-0.5, 1.0, -0.8), size (33, 4, 28) -> x -17..16, z -14.8..13.2, y -1..3. Drops the ocean, deep cliff, stairs and dock (y < -1.5) automatically.
- Collect objects: Volume; Use Geometry: Physics Colliders (so removed colliders drop out of the bake).
- Agent: radius 0.45, height 1.8, step height 0.4 (bumps 0..0.6 are mostly 0.1-0.3), max slope 40, drop/jump 0. If the Runner (1.265 m collider) clips, keep the same radius, do not add a new agent type.
- Min region area 2 (kills tomb-top slivers).
- Add NavMeshModifier (Not Walkable) to: Rock_Stairs_02_*, Cliff_Basalt_*, Basalt_0*, Background, OceanPlane, Stage_Wooden, Cage_Large; do NOT mark Rocks wholesale (hole risk above).
- Tomb lids (1.4 m centre) and tombs are tall obstacles: the bake cuts them out; no carving needed (all static).
- After bake: look at the blue mesh, any island not connected to the origin region is fine. Any hole in the ring or between Dirt_Flat tiles needs a collider put back.

## 5. Entrances (8 spawn points)

Reuse the existing Spawn_North/South/East/West + Warning_* + Entrance_* (their old spots at z +-13.3 and x +-15.3 hang over the cliff or ocean) and clone four more. Warning marker = plate 3 x 1.5 on the ground at the spawn, long side across the lane, stripe pointing at the origin; keep the 3 m width (a pack of 6 fits with +-1.2 m jitter).

| # | Name | Spawn (x, z) | Dist to origin | Funnels into | Note |
|---|---|---|---|---|---|
| 1 | Spawn_N | (-0.5, 11.0) | 11.0 | Gap N | ground (11)/(12) grass tile |
| 2 | Spawn_NE | (12.0, 9.0) | 15.0 | ring -> N or E gap | (12,8) Rock_Cliff y 0.1 |
| 3 | Spawn_E | (13.2, 0.2) | 13.2 | Gap E | mounds: y -0.1..-0.5 |
| 4 | Spawn_SE | (7.8, -11.6) | 14.0 | Gap SE | Grass_Small_01_(36) y 0.3 |
| 5 | Spawn_S | (0.2, -11.0) | 11.0 | Gap S | Grass_Small_01_(33)/(51) |
| 6 | Spawn_W1 | (-14.2, -4.8) | 15.0 | Gap W | near Tree_Dead_03_(1) (-16,-3.7): check trunk |
| 7 | Spawn_W2 | (-14.2, 1.0) | 14.2 | Gap W | Grass_Small_01_(47) |
| 8 | Spawn_NW | (-8.0, 10.2) | 13.0 | ring -> N or W gap | Grass_Small_01_(46) |

All are 11 m or more from the origin, so the WaveDirector min distance (never read here; confirm in the component) is satisfied at the start; after the player moves to the edge the director will refuse the near ones as designed. No spawn is within 3 m of the east stairs (14..16, -4..-12) or the SW dock. Marker yaw: face the origin. Nudge any point by up to 0.5 m if SamplePosition fails.

Eight spawns sit around the compass, with two on the west for the widest gap. Do not weight the director toward NE/NW; they are the long-approach entrances (good for Shooter drip-in).

## 6. Risks and numeric checks

Checks (main session, after placing, in this order):
1. Downward raycast from (x, 5, z) at each of the 8 spawns, each marker centre, and Defne's start; the hit must be a Ground/Rock collider and y between -0.6 and 0.9. Log y.
2. NavMesh.SamplePosition(point, out hit, 1.0, AllAreas) at each spawn and marker; must succeed with |hit - point| < 0.5.
3. NavMesh.CalculatePath from each spawn to (0,0,0): status Complete, length < 35 m. Do the same from each of the 6 fence openings' centres, and from 4 random points on the polygon edge.
4. Lane check: Physics.SphereCast (radius 0.45, from the spawn to 5.5 m from the origin) for each of the 8 lanes with the Default + wall layers: no hit. Raycast at y 1.0 from each spawn to the origin: log which objects stop bullets (expected: only at the gaps' rim cover; the clean central ellipse must be hit-free).
5. Origin: NavMesh.SamplePosition at (0,0,0) and a 5.5 x 4.5 ellipse sampled at 16 points: all on the mesh.
6. No collider overlaps Defne's capsule at the start, none under the moved crypt.
7. Edge: raycast the 11 invisible walls, then walk Defne into every polygon corner (no fall).
8. View: look at the plateau from at least 3 camera angles (play camera, top, one low side) for floating/clipping on the moved crypt, deleted-fence ends (leave capped pillars or place a tomb/brazier at each end so the gap looks deliberate) and moved braziers.

Risks:
- Fence 1.9 m and pillar 2.4 m near the camera can hide Defne on the near sides: put the surviving south/west fence and pillars on the existing wall-fade system or lower them to ~1.0 m on the camera side (bullets then pass them; decide with the user).
- Crypt tall facade may also hide her (see section 2).
- Mesh colliders on non-convex props are fine for static bake but cost for 32 enemies' raycasts; if profiling is bad, replace the 3 crypts and fence with box colliders.
- The Gates object may be one mesh whose archway belongs in the look; if so keep it as visual only and cap with a Not-Walkable-free gap.
- NE and NW enemies spend 10-15 m outside the fence; Shooters (keep 8-11 m distance) may stand outside and shoot the fence. If that looks bad, add two more gaps (NE corner, NW corner) later; do not decide it by guessing: playtest.
- Foliage may carry blade meshes that look like cover but have no collision; players will not notice since 1.0 m bullets ignore it.
- GDD §9 says "no outside shown". This map shows ocean and cliffs by design; the GDD text should be updated by the main session with the user's decision (this plan does not change it).

Open for the user (not decided here): (a) lower the camera-side fence, (b) two extra gaps NE/NW, (c) whether the E gate stays as a visual.
