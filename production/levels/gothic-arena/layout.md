# Gothic Arena Dressing Plan: "The Desecrated Nave"

Level: gameplay arena, Dance with the Devil. Theme: dark gothic ruin (desecrated cathedral courtyard / graveyard), the inside of an exhausted mind.
Estimated play time: wave-survival arena, no fixed route. This document covers the dressing only. Gameplay colliders, spawn points and warning plates are untouched.
Status: plan for the main session / editor script. Nothing here changes gameplay or the GDD (visual only).

## 0. Conventions (read first)

- World metres, y up, +Z north, +X east. Floor top y=0. Wall footprints (the collider volumes): N z 14..15, S z -15..-14, E x 16..17, W x -17..-16 (inner faces at +-14 and +-16).
- Yaw is Unity Y euler degrees. A piece's local +Z is assumed to be its FRONT. Yaw 0 faces +Z (north), 90 faces +X, 180 faces -Z, 270 faces -X.
- "Pivot right" wall pieces span local x -2.5..0. Resulting world span:
  - yaw 180: pivot is the WEST end, piece spans x pivot..pivot+2.5 (N and S walls).
  - yaw 270: pivot is the NORTH end, piece spans z pivot-L..pivot (E and W walls, L = 2.5 x scale).
- E and W walls use scale 0.96 so five pieces (2.4 m) fill 12 m exactly.
- S and W wall pieces face OUTWARD (yaw 180 / 270) because the camera (south-west) sees their outer face and top. N and E walls face INWARD (yaw 180 / 270) because the camera sees their inner faces.
- All y = 0 unless stated. Scale 1 unless stated. Pieces are assumed to have their pivot at the base (verify, see CONCERNS).
- Every dressing object has NO collider. Remove colliders from the instantiated prefabs.
- Parent groups: `GothicDressing/{Walls_N, Walls_E, Walls_S, Walls_W, Blocks_NW, Blocks_SE, Corners, Floor, Background, FX, Lights}`.
- Keep-clear interpretation: the 5-6 m ring, corridors, centre and entrance lanes get NO object. Dressing lives only in (a) the wall footprints, (b) the corner band (|x| >= 15 or |z| >= 13, low clutter <= 1.3 m on N/E corners, <= 0.6 m on S/W corners), (c) the top and faces of the L-blocks, (d) outside the walls. Floor decals are flat (y 0.02) and stay out of the 2.5 m entrance lane margins.

## 1. Zones and mood

```
                    N  (full-height wall, windows, buttresses = the "stage backdrop")
        Crypt silhouette beyond (9,20.5)        Topper spire (15.4,15.4)
   +--------------------------------------------------------------+
   | NW: Gargoyle + teal glow   [N gate: buttresses+fire]  NE: spire + violet   |
   |    [L_NW block, 1.5 m]                                       |
 W |                                                              | E
 (low half wall)   [W gate]        open centre       [E gate] (full wall, windows)
   |  ruined fence                                     fire      |
   |                                   [L_SE block 1.5 m]         |
   | SW: low ember pyre         [S gate: half pillars]  SE: Statue + altar, amber |
   +--------------------------------------------------------------+
                    S  (half wall, <= 1.5 m, camera side)
```

- Reading rule from the fixed camera: the player always sees N and E as a tall, lit, detailed backdrop, and S and W as a low ruined edge with a broken skyline. Tall mass behind the action, low mass in front.
- Entrance moments:
  - N gate (grandest): two buttress pillars with burning bowls on top, torches on the lane faces, warm pool on the floor, iron cemetery gates as a dark silhouette 4 m beyond the opening.
  - E gate: same language as N (buttresses, fire, torches), seen as a vertical frame.
  - S and W gates: low half pillars with hanging lanterns and a warm point light at 1.8 m. They read by light, not by height.
- Landmarks (one per quadrant, readable without colour, silhouette plus light hue):
  - NE: spire (Topper) plus crypt behind the wall, lavender-violet light. Tallest silhouette on screen.
  - NW: gargoyle on the outer corner of the L_NW block, cold teal-blue light.
  - SE: statue on the outer corner of the L_SE block plus an altar (tabernacle), warm amber light.
  - SW: low burning rubble/bone pyre in the corner (<= 0.6 m), dull amber glow and a thin smoke column. The only light source low on the south-west side.
- Pacing and sightlines: the fight happens in the dim centre. Brightest areas are the four entrance pools (enemy arrival is lit, therefore readable) and the N/E window shafts (cool, soft, never over a lane). The centre is lit only by moon plus a faint fill so characters (olive, red, magenta) are the most saturated things on screen.

## 2. Placement tables

### 2.1 North wall (full height, inward, yaw 180, z 14.6, scale 1). 20 objects

Joints align to 2.5 m pieces. Gap x -2.0..2.0 is the gate opening (lane stays 3 m clear plus buttress width).

| ID | Prefab | x | y | z | yaw | s | Purpose |
|---|---|---|---|---|---|---|---|
| A01 | SM_Bld_Base_Wall_01 | -17.0 | 0 | 14.6 | 180 | 1 | wall, west end |
| A02 | SM_Bld_Wall_Window_Tall_01 | -14.5 | 0 | 14.6 | 180 | 1 | window (centre x -13.25) |
| A03 | SM_Bld_Base_Wall_01 | -12.0 | 0 | 14.6 | 180 | 1 | wall |
| A04 | SM_Bld_Wall_Window_Tall_01 | -9.5 | 0 | 14.6 | 180 | 1 | window with shaft (centre -8.25) |
| A05 | SM_Bld_Wall_Ruin_01 | -7.0 | 0 | 14.6 | 180 | 1 | broken top, varies skyline |
| A06 | SM_Bld_Base_Wall_01 | -4.5 | 0 | 14.6 | 180 | 1 | wall, ends at -2.0 |
| A07 | SM_Bld_Base_Wall_01 | 2.0 | 0 | 14.6 | 180 | 1 | wall, starts at 2.0 |
| A08 | SM_Bld_Wall_Window_Tall_01 | 4.5 | 0 | 14.6 | 180 | 1 | window (centre 5.75) |
| A09 | SM_Bld_Base_Wall_01 | 7.0 | 0 | 14.6 | 180 | 1 | wall |
| A10 | SM_Bld_Wall_Window_Tall_01 | 9.5 | 0 | 14.6 | 180 | 1 | window with shaft (centre 10.75) |
| A11 | SM_Bld_Wall_Ruin_01 | 12.0 | 0 | 14.6 | 180 | 1 | broken top |
| A12 | SM_Bld_Base_Wall_01 | 14.5 | 0 | 14.6 | 180 | 1 | wall, east end (to 17.0) |
| A13 | SM_Bld_Pillar_01 | -12.0 | 0 | 14.35 | 180 | 1 | joint pillar (spans z 14.04..14.66) |
| A14 | SM_Bld_Pillar_01 | -7.0 | 0 | 14.35 | 180 | 1 | joint pillar |
| A15 | SM_Bld_Pillar_01 | 7.0 | 0 | 14.35 | 180 | 1 | joint pillar |
| A16 | SM_Bld_Pillar_01 | 12.0 | 0 | 14.35 | 180 | 1 | joint pillar |
| A17 | SM_Bld_Buttress_Pillar_01 | -2.25 | 0 | 14.5 | 180 | 1 | gate frame west (inner edge x -1.88) |
| A18 | SM_Bld_Buttress_Pillar_01 | 2.25 | 0 | 14.5 | 180 | 1 | gate frame east (inner edge x 1.88) |
| A19 | SM_Prop_Torch_01 | -1.85 | 2.2 | 14.5 | 90 | 1 | lane-side torch, faces +x |
| A20 | SM_Prop_Torch_02 | 1.85 | 2.2 | 14.5 | 270 | 1 | lane-side torch, faces -x |

### 2.2 East wall (full height, inward, yaw 270, x 16.55, scale 0.96). 18 objects

Pivot is the north end; piece spans z pivot-2.4..pivot. Gate opening z -2.0..2.0.

| ID | Prefab | x | y | z | yaw | s | Purpose |
|---|---|---|---|---|---|---|---|
| B01 | SM_Bld_Wall_Ruin_01 | 16.55 | 0 | -11.6 | 270 | 0.96 | spans -14.0..-11.6 |
| B02 | SM_Bld_Wall_Window_Tall_01 | 16.55 | 0 | -9.2 | 270 | 0.96 | window (centre z -10.4) |
| B03 | SM_Bld_Base_Wall_01 | 16.55 | 0 | -6.8 | 270 | 0.96 | wall |
| B04 | SM_Bld_Base_Wall_01 | 16.55 | 0 | -4.4 | 270 | 0.96 | wall |
| B05 | SM_Bld_Wall_Ruin_End_01 | 16.55 | 0 | -2.0 | 270 | 0.96 | gate-side end |
| B06 | SM_Bld_Wall_Ruin_End_01 | 16.55 | 0 | 4.4 | 270 | 0.96 | gate-side end (spans 2.0..4.4) |
| B07 | SM_Bld_Base_Wall_01 | 16.55 | 0 | 6.8 | 270 | 0.96 | wall |
| B08 | SM_Bld_Wall_Window_Tall_01 | 16.55 | 0 | 9.2 | 270 | 0.96 | window (centre z 8.0) |
| B09 | SM_Bld_Base_Wall_01 | 16.55 | 0 | 11.6 | 270 | 0.96 | wall |
| B10 | SM_Bld_Wall_Ruin_01 | 16.55 | 0 | 14.0 | 270 | 0.96 | broken top, NE corner |
| B11 | SM_Bld_Pillar_01 | 16.3 | 0 | -11.6 | 270 | 1 | window frame |
| B12 | SM_Bld_Pillar_01 | 16.3 | 0 | -9.2 | 270 | 1 | window frame |
| B13 | SM_Bld_Pillar_01 | 16.3 | 0 | 6.8 | 270 | 1 | window frame |
| B14 | SM_Bld_Pillar_01 | 16.3 | 0 | 9.2 | 270 | 1 | window frame |
| B15 | SM_Bld_Buttress_Pillar_01 | 16.5 | 0 | -2.25 | 270 | 1 | gate frame south |
| B16 | SM_Bld_Buttress_Pillar_01 | 16.5 | 0 | 2.25 | 270 | 1 | gate frame north |
| B17 | SM_Prop_Torch_01 | 16.5 | 2.2 | -1.85 | 0 | 1 | lane-side torch, faces +z |
| B18 | SM_Prop_Torch_02 | 16.5 | 2.2 | 1.85 | 180 | 1 | lane-side torch, faces -z |

### 2.3 South wall (half height <= 1.5 m, outward, yaw 180, z -14.5). 18 objects

| ID | Prefab | x | y | z | yaw | s | Purpose |
|---|---|---|---|---|---|---|---|
| C01-C05 | SM_Bld_Base_Wall_Half_01 | -17.0, -14.5, -12.0, -9.5, -7.0 | 0 | -14.5 | 180 | 1 | west run (to -4.5) |
| C06 | SM_Bld_Fence_Cemetary_01 | -3.25 | 0 | -14.5 | 180 | 0.75 | iron fence by the gate (h 1.43) |
| C07 | SM_Bld_Fence_Cemetary_01 | 3.25 | 0 | -14.5 | 180 | 0.75 | iron fence by the gate |
| C08-C12 | SM_Bld_Base_Wall_Half_01 | 4.5, 7.0, 9.5, 12.0, 14.5 | 0 | -14.5 | 180 | 1 | east run (to 17.0) |
| C13 | SM_Bld_Base_Pillar_Half_01 | -9.5 | 0 | -14.5 | 180 | 1 | rhythm pillar |
| C14 | SM_Bld_Base_Pillar_Half_01 | 9.5 | 0 | -14.5 | 180 | 1 | rhythm pillar |
| C15 | SM_Bld_Base_Pillar_Half_01 | -2.25 | 0 | -14.5 | 180 | 1 | gate post |
| C16 | SM_Bld_Base_Pillar_Half_01 | 2.25 | 0 | -14.5 | 180 | 1 | gate post |
| C17 | SM_Prop_Lantern_01 | -2.25 | 1.5 | -14.5 | 0 | 1 | gate lantern (tip ~2.0 m, accepted) |
| C18 | SM_Prop_Lantern_02 | 2.25 | 1.5 | -14.5 | 0 | 1 | gate lantern |

Rows C01-C05 and C08-C12 each expand to five objects (10 Half walls). Total 18.

### 2.4 West wall (half height, outward, yaw 270, x -16.5, scale 0.96). 16 objects

| ID | Prefab | x | y | z | yaw | s | Purpose |
|---|---|---|---|---|---|---|---|
| D01-D04 | SM_Bld_Base_Wall_Half_01 | -16.5 | 0 | -11.6, -9.2, -6.8, -4.4 | 270 | 0.96 | south run (spans -14.0..-4.4), h 1.44 |
| D05 | SM_Bld_Fence_Cemetary_01 | -16.5 | 0 | -3.2 | 270 | 0.75 | iron fence by gate |
| D06 | SM_Bld_Fence_Cemetary_01 | -16.5 | 0 | 3.2 | 270 | 0.75 | iron fence by gate |
| D07-D10 | SM_Bld_Base_Wall_Half_01 | -16.5 | 0 | 6.8, 9.2, 11.6, 14.0 | 270 | 0.96 | north run (spans 4.4..14.0) |
| D11 | SM_Bld_Base_Pillar_Half_01 | -16.5 | 0 | -9.2 | 270 | 1 | rhythm pillar |
| D12 | SM_Bld_Base_Pillar_Half_01 | -16.5 | 0 | 9.2 | 270 | 1 | rhythm pillar |
| D13 | SM_Bld_Base_Pillar_Half_01 | -16.5 | 0 | -2.25 | 270 | 1 | gate post |
| D14 | SM_Bld_Base_Pillar_Half_01 | -16.5 | 0 | 2.25 | 270 | 1 | gate post |
| D15 | SM_Prop_Lantern_01 | -16.5 | 1.5 | -2.25 | 0 | 1 | gate lantern |
| D16 | SM_Prop_Lantern_02 | -16.5 | 1.5 | 2.25 | 0 | 1 | gate lantern |

Rows D01-D04 and D07-D10 each expand to four objects. Total 16.

### 2.5 L-block dressing. 21 objects

The four collider blocks stay at their positions. KEEP their MeshRenderers visible and swap them to a dark masonry stone material (see CONCERNS); decals/greebles sit on the camera-facing (south and west) faces. Top y = 1.5. Only low props on top; the two tall landmarks sit on the outer corners where their occlusion shadow falls on wall and ring, not on the centre or corridors.

| ID | Prefab | x | y | z | yaw | s | Purpose |
|---|---|---|---|---|---|---|---|
| L01 | SM_Bld_Wall_Brick_Greeble_01 | -4.5 | 0 | 5.95 | 180 | 0.6 | NW block south face |
| L02 | SM_Bld_Wall_Brick_Greeble_01 | -8.5 | 0 | 1.95 | 180 | 0.6 | NW west-block south face |
| L03 | SM_Bld_Wall_Brick_Greeble_01 | -10.05 | 0 | 6.5 | 270 | 0.6 | NW block west face |
| L04 | SM_Bld_Wall_Brick_Greeble_01 | 6.0 | 0 | -9.05 | 180 | 0.6 | SE block south face |
| L05 | SM_Bld_Wall_Brick_Greeble_01 | 6.95 | 0 | -4.0 | 270 | 0.6 | SE east-block west face |
| L06 | SM_Prop_Gargoyle_01_Base | -9.0 | 1.5 | 8.0 | 225 | 1 | NW LANDMARK base (faces SW) |
| L07 | SM_Prop_Gargoyle_01 | -9.0 | 1.5 | 8.0 | 225 | 1 | NW LANDMARK (top ~2.75 m) |
| L08 | SM_Prop_Candelabra_01 | -7.4 | 1.5 | 8.2 | 0 | 1 | flame, 1.76 m |
| L09 | SM_Prop_Skull_Pile_01 | -5.2 | 1.5 | 8.0 | 30 | 1 | grim detail |
| L10 | SM_Prop_Book_Open | -6.2 | 1.5 | 7.2 | 210 | 1 | narrative prop (mind / contracts) |
| L11 | SM_Prop_Tomb_Stone_05 | -4.0 | 1.5 | 8.2 | 200 | 1 | low vertical accent |
| L12 | SM_Prop_Candle_03 | -3.2 | 1.5 | 6.6 | 0 | 1 | small flame |
| L13 | SM_Prop_Tomb_Stone_06 | -9.0 | 1.5 | 3.0 | 160 | 1 | west block accent |
| L14 | SM_Prop_Rubble_Pile_Burnt_03 | -8.5 | 1.5 | 4.6 | 60 | 0.7 | west block rubble |
| L15 | SM_Prop_Statue_01 | 9.0 | 1.5 | -8.0 | 315 | 1 | SE LANDMARK (faces NW, top ~3.9 m) |
| L16 | SM_Prop_Tabernacle_01 | 5.0 | 1.5 | -7.6 | 180 | 1 | altar, spans x 3.2..6.8 (top ~2.9) |
| L17 | SM_Prop_Candle_01 | 2.7 | 1.5 | -6.6 | 0 | 1 | small flame |
| L18 | SM_Prop_Candle_07 | 7.3 | 1.5 | -8.4 | 0 | 1 | small flame |
| L19 | SM_Prop_Skull_Pile_02 | 7.6 | 1.5 | -6.8 | 150 | 1 | detail |
| L20 | SM_Prop_Tomb_Stone_03 | 9.0 | 1.5 | -3.0 | 20 | 1 | east block north end |
| L21 | SM_Prop_Skull_Pile_01 | 8.2 | 1.5 | -4.8 | 80 | 1 | east block detail |

(21 rows, 21 objects.)

### 2.6 Corner vignettes. 13 objects

Band props only; S/W corners stay under 0.6 m.

| ID | Prefab | x | y | z | yaw | s | Purpose |
|---|---|---|---|---|---|---|---|
| K01 | SM_Bld_Topper_01 | 15.4 | 0 | 15.4 | 180 | 1 | NE LANDMARK spire (behind both walls) |
| K02 | SM_Prop_Tomb_Stone_02 | 15.3 | 0 | 13.2 | 215 | 1 | NE graveyard corner |
| K03 | SM_Prop_Tomb_Stone_04 | 14.3 | 0 | 13.6 | 190 | 0.9 | NE |
| K04 | SM_Env_Plant_Fern_01 | 15.6 | 0 | 12.4 | 40 | 1 | NE softening |
| K05 | SM_Prop_Tomb_Stone_01 | -15.3 | 0 | 13.2 | 140 | 1 | NW corner |
| K06 | SM_Prop_Bone_Pile_01 | -14.4 | 0 | 13.5 | 20 | 1 | NW |
| K07 | SM_Env_Rocks_Small_01 | -15.6 | 0 | 12.3 | 70 | 1 | NW |
| K08 | SM_Prop_Rubble_Pile_Burnt_02 | 15.2 | 0 | -13.3 | 15 | 0.7 | SE corner (0.38 m) |
| K09 | SM_Prop_Skull_Pile_02 | 14.2 | 0 | -13.6 | 70 | 1 | SE |
| K10 | SM_Prop_Body_Skeleton_01 | 15.4 | 0 | -12.5 | 100 | 1 | SE, lying body (verify height <= 0.4) |
| K11 | SM_Prop_Rubble_Pile_Burnt_01 | -14.9 | 0 | -13.1 | 45 | 0.8 | SW LANDMARK pyre base (0.43 m) |
| K12 | SM_Prop_Bone_Pile_02 | -15.5 | 0 | -12.2 | 20 | 1 | SW |
| K13 | SM_Prop_Skull_Pile_03 | -14.0 | 0 | -13.6 | 330 | 1 | SW |
Moss is listed in 2.7.

### 2.7 Floor treatment (flat, y as stated, no colliders). 14 objects

Orientation: if the Grunge/Moss decal prefabs are vertical quads, add euler X 90 (verify). Keep colours desaturated grey-brown; never reddish.

| ID | Prefab | x | y | z | yaw | s | Purpose |
|---|---|---|---|---|---|---|---|
| F01 | SM_Env_Grunge_01 | -12.0 | 0.02 | 10.5 | 20 | 1 | NW ring wear |
| F02 | SM_Env_Grunge_04 | -7.0 | 0.02 | 11.5 | 350 | 1 | north ring (clear of lane margin x +-4) |
| F03 | SM_Env_Grunge_07 | 6.5 | 0.02 | 10.5 | 340 | 1 | NE ring |
| F04 | SM_Env_Grunge_02 | 12.5 | 0.02 | 6.5 | 90 | 1 | east ring (z 4.6..8.4, clear of lane) |
| F05 | SM_Env_Grunge_10 | 12.5 | 0.02 | -7.5 | 70 | 1 | SE ring |
| F06 | SM_Env_Grunge_05 | 6.0 | 0.02 | -12.0 | 10 | 1 | south ring |
| F07 | SM_Env_Grunge_08 | -7.0 | 0.02 | -11.8 | 0 | 1 | south ring |
| F08 | SM_Env_Grunge_11 | -12.5 | 0.02 | -8.0 | 100 | 1 | SW ring |
| F09 | SM_Env_Moss_01 | 14.8 | 0.02 | 12.0 | 30 | 1 | NE corner |
| F10 | SM_Env_Moss_02 | -14.6 | 0.02 | 12.4 | 200 | 1 | NW corner |
| F11 | SM_Env_Moss_03 | 14.5 | 0.02 | -12.2 | 120 | 1 | SE corner |
| F12 | SM_Env_Ground_Dirt_Flat_01 | 13.5 | 0.01 | 9.5 | 30 | 0.8 | dirt patch NE (4x4) |
| F13 | SM_Env_Ground_Dirt_Flat_01 | -13.0 | 0.01 | -10.5 | 120 | 0.8 | dirt patch SW |
| F14 | SM_Env_Ground_Dirt_Flat_01 | 11.5 | 0.01 | -11.0 | 200 | 0.8 | dirt patch SE |

The centre 14x12 m, the corridors and all entrance lanes stay clean (existing floor material only). Cracked tiles (SM_Bld_Base_Floor_01) are NOT used: 2.5 m tiles do not divide the 34x30 floor and would add 100+ objects. Optional later: tile the floor material itself.

### 2.8 Background beyond the walls. 21 objects

Camera sees roughly 8 m beyond N/E and a little beyond S/W, so S/W get ground and a few rocks only.

| ID | Prefab | x | y | z | yaw | s | Purpose |
|---|---|---|---|---|---|---|---|
| G01 | SM_Env_Ground_Dirt_Flat_01 | -7 | -0.1 | 25 | 0 | 4 | N ground (20x20, x -17..3) |
| G02 | SM_Env_Ground_Dirt_Flat_01 | 13 | -0.1 | 25 | 90 | 4 | N ground (x 3..23) |
| G03 | SM_Env_Ground_Dirt_Flat_01 | 27 | -0.12 | -5 | 0 | 4 | E ground (z -15..5) |
| G04 | SM_Env_Ground_Dirt_Flat_01 | 27 | -0.12 | 15 | 180 | 4 | E ground (z 5..25) |
| G05 | SM_Env_Ground_Dirt_Flat_01 | -7 | -0.1 | -25 | 90 | 4 | S ground |
| G06 | SM_Env_Ground_Dirt_Flat_01 | 13 | -0.1 | -25 | 0 | 4 | S ground |
| G07 | SM_Env_Ground_Dirt_Flat_01 | -27 | -0.12 | -5 | 180 | 4 | W ground |
| G08 | SM_Env_Ground_Dirt_Flat_01 | -27 | -0.12 | 15 | 90 | 4 | W ground |
| G09 | SM_Prop_Crypt_01 | 9.0 | 0 | 20.5 | 180 | 1 | NE landmark body (door faces arena, 5.8 m) |
| G10 | SM_Bld_Gates_Cemetary_01 | 0 | 0 | 19.0 | 180 | 1 | N gate silhouette (4 m behind opening) |
| G11 | SM_Env_Rock_Cliff_01 | -13 | -0.5 | 28 | 180 | 1 | N backdrop |
| G12 | SM_Env_Rock_Cliff_02 | -4 | -0.5 | 28.5 | 180 | 1 | N backdrop |
| G13 | SM_Env_Rock_Cliff_03 | 5 | -0.5 | 28 | 180 | 1 | N backdrop |
| G14 | SM_Env_Rock_Cliff_04 | 14 | -0.5 | 28.5 | 180 | 1 | N backdrop |
| G15 | SM_Env_Rock_Cliff_05 | 27 | -0.5 | -4 | 270 | 1 | E backdrop |
| G16 | SM_Env_Rock_Cliff_06 | 27 | -0.5 | 10 | 270 | 1 | E backdrop |
| G17 | SM_Env_Tree_Dead_01 | -16.5 | 0 | 17.5 | 20 | 1.2 | NW outside, peeks over wall |
| G18 | SM_Env_Tree_Dead_02 | -7.0 | 0 | 20.0 | 100 | 1.2 | north outside |
| G19 | SM_Env_Tree_Dead_03 | 18.8 | 0 | 16.5 | 300 | 1.2 | NE outside |
| G20 | SM_Env_Rock_01 | 20.0 | 0 | 8.0 | 40 | 1.3 | E outside (not in gate lane) |
| G21 | SM_Env_Rock_01 | -21.0 | 0 | -9.0 | 150 | 1.3 | W outside |

Ground tiles overlap the existing floor only outside +-17 / +-15. Different y per row avoids z-fighting where tiles touch.

### 2.9 FX (particle prefabs). 14 objects, no meshes

| ID | Prefab | x | y | z | Notes |
|---|---|---|---|---|---|
| X01 | FX_Fire_01 | -2.25 | 3.05 | 14.5 | burning bowl on N buttress, scale 0.6 |
| X02 | FX_Fire_01 | 2.25 | 3.05 | 14.5 | same |
| X03 | FX_Fire_01 | 16.5 | 3.05 | -2.25 | E buttress, scale 0.6 |
| X04 | FX_Fire_01 | 16.5 | 3.05 | 2.25 | E buttress, scale 0.6 |
| X05 | FX_Fire_01 | -14.9 | 0.15 | -13.1 | SW pyre, scale 0.7 |
| X06 | FX_Smoke_Small_01 | -14.9 | 0.6 | -13.1 | SW pyre smoke |
| X07 | FX_Fire_Embers_01 | -14.9 | 0.4 | -13.1 | only embers in the scene, low rate, SW only |
| X08 | FX_SunBeam_Colored_01 | -8.25 | 3.2 | 14.0 | euler (60,180,0), colour #5B7FD6 alpha ~0.25 |
| X09 | FX_SunBeam_Colored_01 | 10.75 | 3.2 | 14.0 | euler (60,180,0), colour #7A62C8 alpha ~0.25 |
| X10 | FX_SunBeam_Colored_01 | 16.2 | 3.2 | -10.4 | euler (60,270,0), colour #4F8FC0 alpha ~0.25 |
| X11 | FX_Ash_01 | 0 | 7.0 | 0 | wide emitter covering arena, low density, pale grey |
| X12 | FX_Fog_01 | 0 | 0.3 | 17.5 | outside N, backdrop only |
| X13 | FX_Fog_01 | 19.5 | 0.3 | 0 | outside E, backdrop only |
| X14 | FX_Bats_01 | -6 | 6.0 | 19 | outside N, above wall |

Use FX_Fire_01 (no light). If any FX_Fire_Light_01 is used instead, delete its Light component so the budget below holds. Do not place FX_Fireflies_01 or Dust_Spots inside the arena (they read as projectiles).

Mesh tally: N 20 + E 18 + S 18 + W 16 = 72; blocks 21; corners 13; floor 14; background 21 = 141 mesh objects, plus 14 FX and 12 lights. (Slightly over the 140 target by one; drop G21 if needed.)

## 3. Lighting plan

Render: URP Forward+, 960x540 low-res, dark. Values are starting points; tune by eye in the pixel camera, keeping the ratios.

### 3.1 Global

| Item | Value |
|---|---|
| Directional "Moon" | colour #7F96C8, intensity 0.7, euler (50, 25, 0): light travels toward NNE, so the south-facing and west-facing faces the camera sees are lit and shadows fall away from the camera onto the N/E backdrop. Soft shadows ON (counts as the scene directional shadow; one cascade is enough), shadow strength 0.6 |
| Ambient | Flat colour #1E2438, intensity 1.0, no skybox contribution |
| Camera clear colour | #05060B |
| Fog (RenderSettings) | OFF (ortho camera). Atmosphere comes from particles only |
| Post (Volume, optional) | Vignette 0.30; Colour Adjustments post-exposure -0.2, saturation -10; Bloom threshold 1.0, intensity 0.3 (keeps emissive plates and fire punchy) |

### 3.2 Local point lights (12, the full budget)

Create each as a separate Light object under `GothicDressing/Lights`. Type Point, no cookies, Forward+ per-object limit is not an issue.

| ID | Where | Position (x,y,z) | Colour | Intensity | Range | Shadows |
|---|---|---|---|---|---|---|
| P01 | N entrance | (0, 2.6, 14.0) | #E0A060 | 3.0 | 7 | no |
| P02 | S entrance | (0, 1.8, -14.0) | #E0A060 | 3.0 | 7 | no |
| P03 | E entrance | (16.0, 2.6, 0) | #E0A060 | 3.0 | 7 | no |
| P04 | W entrance | (-16.0, 1.8, 0) | #E0A060 | 3.0 | 7 | no |
| P05 | NW landmark (gargoyle) | (-8.6, 2.4, 7.6) | #5A9CC8 | 3.5 | 8 | YES |
| P06 | NE landmark (spire/crypt) | (13.0, 2.6, 13.2) | #A89CE0 | 3.5 | 8 | YES |
| P07 | SE landmark (statue/altar) | (9.0, 2.4, -7.6) | #E2A460 | 3.0 | 7 | no |
| P08 | SW pyre | (-14.0, 1.2, -12.0) | #D89A58 | 2.5 | 6 | no |
| P09 | N window W | (-8.25, 3.0, 13.4) | #5B7FD6 | 2.5 | 7 | no |
| P10 | E window S | (15.6, 3.0, -10.4) | #4F8FC0 | 2.5 | 7 | no |
| P11 | Centre fill | (0, 6.0, 0) | #3A4A78 | 1.2 | 14 | no |
| P12 | N window E | (10.75, 3.0, 13.4) | #7A62C8 | 2.5 | 7 | no |

- Shadow-casting points: P05 and P06 only (the two tallest silhouettes). If the project counts the directional's shadows toward the "max 2" limit, switch P06 to no shadows.
- Warm firelight (hue about 33-38 degrees, saturation <= 0.55) pools only at the four entrances and at SE/SW. It is deliberately desaturated and lower than the warning plates, so the plates (bright red-orange, emissive) stay the brightest warm thing on screen.
- The centre stays dim: only moon, ambient and P11 reach it. Entrance pools end about 7 m from each door, leaving a dim 14x12 m middle.
- Window shafts end on the ring (about z 12 / x 14.5) away from all lanes; they are cool and never dominate the floor.

### 3.3 Emissives and materials

- Candle, candelabra, torch and lantern flames use their own prefab particles. Do not add extra lights for them.
- Wall glow material `mat_env_wall_glow_flat` and the warning plate material remain unchanged.

## 4. Readability rules for the pixel camera

1. Floor: albedo luminance <= 25 percent, saturation <= 20 percent, cool-grey tint about #2A2C36. Grunge/dirt decals darker than the floor or equal, never lighter. Characters must always be the highest-saturation, highest-contrast objects.
2. Hue reservation. NO red, orange, magenta or hot pink on any prop, light, FX or material: enemies (red/magenta), their shots and tells (`mat_enemy_*`), spawn warning plates (red-orange) and the Devil own those hues. Allowed accents: teal-blue, blue, violet, lavender, desaturated amber (hue 33-38 degrees, only fire).
3. Check these prefabs for red/orange tint after import and re-tint if needed: banners are deliberately not used (SM_Prop_Flag_Dark_01 omitted), Pyre_Stake, Gibbet and Chain omitted for the same reason and height; Body_Skeleton and Candle flames: amber only.
4. Embers (orange sparks) exist only in the SW corner and at low rate so they cannot be mistaken for bullets. No fireflies or dust specks inside the arena.
5. Occlusion budget from the camera (view dir toward NE): anything standing on the south or west side may not exceed 1.5 m (S and W wall, SW corner). Tall pieces exist only on N, E and the outer NW/SE block corners, where the hidden strip falls onto walls or the ring, never onto the centre or corridors. Statue (top 3.9 m) hides up to about 5.6 m NE of the SE corner (wall zone); gargoyle (top 2.75 m) up to about 3.9 m NE of the NW corner.
6. Silhouette clarity: windows and ruined wall tops vary the N/E skyline; keep the S/W skyline flat and low so the player's silhouette never merges with it.
7. Lane clarity: every entrance has a lit pool, a vertical frame (N/E) or a lantern pair (S/W), and nothing within 2.5 m of the lane. The cemetery gate silhouette (G10) sits 4 m behind the N opening so the spawn reads as "coming through a gate".

## 5. Narrative beats and audio (for coordination)

- Environmental story: a cathedral that has become a graveyard (tombs on the blocks, skulls, a book left open on the altar block). The contract/Devil theme is carried by the open book (L10) and the statue facing the centre (L15). No text. Narrative-director to confirm any extra meaning.
- Audio cues (for audio-director): low wind and distant bell bed; entrance fire crackle as 3D loops at P01-P04 positions; soft choir pad swells when the Devil card appears; dry stone/bone ambience at SW pyre. No change is required in code from this plan.

## 6. Pacing chart (intensity over a wave)

Intensity here means visual pressure, not difficulty: wave start = warm entrance pools flare (fire bowls, lanterns); mid-wave = arena readable, dim centre; Devil card moment = no dressing change. Dressing is static, so pacing relies on the four entrance pools telegraphing arrivals.

## 7. Implementation notes for the editor script

- Instantiate with PrefabUtility.InstantiatePrefab; strip all Collider components; set static (Contribute GI off, batching static on) for the wall/prop meshes.
- Spawn order: groups by section; log every created object under `GothicDressing` (parent at origin, scale 1).
- Hide the four outer-wall meshes (Wall_N/E/S/W) with their MeshRenderers off, keep the colliders. Keep the L-block meshes visible.
- After placement: downward raycast from every object's base (must hit floor or block top within 0.05 m except stated y offsets), view from the game camera and two others (top, from north-east) for floating/clipping.
- All changes must be logged in WORKLOG.md; no GDD change is required for this visual task.
