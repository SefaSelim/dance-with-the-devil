using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace DanceWithTheDevil.Editor
{
    /// <summary>
    /// Dresses the GDD §9 block arena as a dark gothic ruin with Synty POLYGON Dark Fantasy prefabs, following the
    /// level-designer plan (production/levels/gothic-arena/layout.md): full-height ruined walls with windows on the
    /// N/E (the backdrop the camera faces), half walls and cemetery fences on the S/W (camera side, ≤ 1.5 m), half
    /// walls wrapped around the two L blocks, a cracked stone floor, one landmark per quadrant, ground and cliffs
    /// beyond the walls, fire, ember and bat particles and a 12-point-light moonlit rig.
    /// Visual only: every dressing object loses its colliders, the gameplay blocks keep theirs (their own meshes are
    /// hidden or recoloured), so paths, shots, bounces and the NavMesh do not change.
    /// Placement is bounds-based (a piece is moved and stretched until its renderer bounds fill the target span),
    /// so it does not depend on each prefab's pivot.
    /// </summary>
    public static class GothicArenaBuilder
    {
        private const string ArenaScenePath = "Assets/_Game/Scenes/Arena.unity";
        private const string Root = "GothicDressing";
        private const string Pf = "Assets/PolygonDarkFantasy/Prefabs/";
        private const string CapMaterialPath = "Assets/_Game/Art/Materials/mat_env_gothic_cap.mat";

        // Wall lines: visual wall centre lines (collider inner faces are at ±14 z and ±16 x).
        private const float NorthZ = 14.2f;
        private const float SouthZ = -14.2f;
        private const float EastX = 16.2f;
        private const float WestX = -16.2f;
        private const float Gate = 2f; // half width of each entrance opening

        private static Dictionary<string, string> _paths;

        [MenuItem("Dance/Arena/Rebuild Gothic Dressing")]
        public static void RebuildInOpenArena()
        {
            Scene scene = SceneManager.GetSceneByPath(ArenaScenePath);
            if (!scene.isLoaded) scene = EditorSceneManager.OpenScene(ArenaScenePath, OpenSceneMode.Single);
            SceneManager.SetActiveScene(scene);
            Dress(scene);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        /// <summary>Builds (or rebuilds) the dressing in the arena scene. Called by PlaceholderBuilder.BuildArenaScene.</summary>
        public static void Dress(Scene scene)
        {
            IndexPrefabs();
            foreach (GameObject go in scene.GetRootGameObjects())
                if (go.name == Root) Object.DestroyImmediate(go);
            var rootGo = new GameObject(Root);
            SceneManager.MoveGameObjectToScene(rootGo, scene);

            Transform arena = FindRoot(scene, "Arena");
            HidePlaceholderLook(scene, arena);
            Lighting(scene, rootGo.transform);

            BuildFloor(rootGo.transform);
            BuildNorthWall(Group(rootGo.transform, "Walls_N"));
            BuildEastWall(Group(rootGo.transform, "Walls_E"));
            BuildSouthWall(Group(rootGo.transform, "Walls_S"));
            BuildWestWall(Group(rootGo.transform, "Walls_W"));
            BuildBlocks(Group(rootGo.transform, "Blocks"));
            BuildCorners(Group(rootGo.transform, "Corners"));
            BuildBackground(Group(rootGo.transform, "Background"));
            BuildFx(Group(rootGo.transform, "FX"));

            int count = 0;
            foreach (Transform t in rootGo.GetComponentsInChildren<Transform>(true)) count++;
            Debug.Log($"GothicArenaBuilder: dressing built ({count} transforms).");
        }

        // ------------------------------------------------------------------ placeholder look off

        private static void HidePlaceholderLook(Scene scene, Transform arena)
        {
            if (arena == null) return;
            // Outer walls and the base floor: keep the colliders, hide the cube meshes and their edge/glow strips.
            foreach (string name in new[] { "Wall_N", "Wall_E", "Wall_S", "Wall_W", "Floor" })
            {
                Transform t = arena.Find(name);
                if (t == null) continue;
                foreach (var r in t.GetComponentsInChildren<Renderer>(true)) r.enabled = false;
            }
            // L blocks: their sides get wrapped in half walls; the top shows as a dark stone cap; edge strips off.
            Material cap = CapMaterial();
            foreach (string name in new[] { "L_NW_North", "L_NW_West", "L_SE_South", "L_SE_East" })
            {
                Transform t = arena.Find(name);
                if (t == null) continue;
                foreach (var r in t.GetComponentsInChildren<Renderer>(true))
                {
                    if (r.transform == t) r.sharedMaterials = new[] { cap };
                    else r.enabled = false;
                }
            }
            foreach (string name in new[] { "FloorTiles", "Props" })
            {
                Transform t = arena.Find(name);
                if (t != null) Object.DestroyImmediate(t.gameObject);
            }
            // Old warm entrance lights: replaced by the plan's lights.
            Transform entrances = arena.Find("Entrances");
            if (entrances != null)
            {
                var lights = new List<GameObject>();
                foreach (var l in entrances.GetComponentsInChildren<Light>(true)) lights.Add(l.gameObject);
                foreach (var go in lights) Object.DestroyImmediate(go);
            }
        }

        private static Material CapMaterial()
        {
            var mat = AssetDatabase.LoadAssetAtPath<Material>(CapMaterialPath);
            if (mat == null)
            {
                mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(mat, CapMaterialPath);
            }
            ColorUtility.TryParseHtmlString("#2E2C33", out Color c);
            mat.SetColor("_BaseColor", c);
            mat.SetFloat("_Smoothness", 0.1f);
            EditorUtility.SetDirty(mat);
            return mat;
        }

        // ------------------------------------------------------------------ lighting (plan §3)

        private static void Lighting(Scene scene, Transform root)
        {
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = Hex("#2A3150");
            RenderSettings.fog = false;
            foreach (GameObject go in scene.GetRootGameObjects())
            {
                var sun = go.GetComponent<Light>();
                if (sun == null || sun.type != LightType.Directional) continue;
                go.name = "Moon";
                sun.color = Hex("#8FA3D2");
                sun.intensity = 0.85f;
                sun.shadows = LightShadows.Soft;
                sun.shadowStrength = 0.6f;
                go.transform.rotation = Quaternion.Euler(50f, 25f, 0f);
            }

            Transform lights = Group(root, "Lights");
            // Warm, desaturated entrance pools (enemy arrivals read by light).
            Point(lights, "P01_N_Entrance", new Vector3(0f, 2.6f, 13.6f), "#E0A060", 3f, 7f, false);
            Point(lights, "P02_S_Entrance", new Vector3(0f, 1.8f, -13.6f), "#E0A060", 3f, 7f, false);
            Point(lights, "P03_E_Entrance", new Vector3(15.6f, 2.6f, 0f), "#E0A060", 3f, 7f, false);
            Point(lights, "P04_W_Entrance", new Vector3(-15.6f, 1.8f, 0f), "#E0A060", 3f, 7f, false);
            // Landmarks.
            Point(lights, "P05_NW_Gargoyle", new Vector3(-8.6f, 2.6f, 7.0f), "#5A9CC8", 3.5f, 8f, true);
            Point(lights, "P06_NE_Spire", new Vector3(13f, 2.8f, 12.8f), "#A89CE0", 3.5f, 8f, true);
            Point(lights, "P07_SE_Statue", new Vector3(8.6f, 2.6f, -7.0f), "#E2A460", 3f, 7f, false);
            Point(lights, "P08_SW_Pyre", new Vector3(-14f, 1.2f, -12f), "#D89A58", 2.5f, 6f, false);
            // Cool window light and a faint centre fill.
            Point(lights, "P09_N_Window_W", new Vector3(-8.25f, 3f, 13.2f), "#5B7FD6", 2.5f, 7f, false);
            Point(lights, "P10_E_Window_S", new Vector3(15.4f, 3f, -9.2f), "#4F8FC0", 2.5f, 7f, false);
            Point(lights, "P11_Centre_Fill", new Vector3(0f, 6f, 0f), "#3A4A78", 1.4f, 14f, false);
            Point(lights, "P12_N_Window_E", new Vector3(10.75f, 3f, 13.2f), "#7A62C8", 2.5f, 7f, false);
        }

        private static void Point(Transform parent, string name, Vector3 position, string hex, float intensity,
            float range, bool shadows)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = position;
            var light = go.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = Hex(hex);
            light.intensity = intensity;
            light.range = range;
            light.shadows = shadows ? LightShadows.Soft : LightShadows.None;
        }

        // ------------------------------------------------------------------ floor

        private static void BuildFloor(Transform root)
        {
            Transform floor = Group(root, "Floor");
            // Cracked stone tiles over the walled area (x -17..17, z -15..15), 2.5 m each, top flush with y = 0.
            const float tile = 2.5f;
            for (float x = -17.5f; x < 17f; x += tile)
            {
                for (float z = -15f; z < 15f; z += tile)
                {
                    GameObject t = Spawn("SM_Bld_Base_Floor_01", floor);
                    if (t == null) return;
                    int yaw = ((int)((x + 20f) / tile) * 7 + (int)((z + 20f) / tile) * 3) % 4 * 90;
                    t.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
                    FitBox(t, new Vector3(x + tile * 0.5f, 0f, z + tile * 0.5f), new Vector3(tile, 0f, tile), bottom: 0f, top: true);
                    foreach (var r in t.GetComponentsInChildren<Renderer>()) r.shadowCastingMode = ShadowCastingMode.Off;
                }
            }
            // Wear, moss and dirt in the ring and corners (never in the centre, corridors or entrance lanes).
            Decal(floor, "SM_Env_Grunge_01", -12f, 10.5f, 20f);
            Decal(floor, "SM_Env_Grunge_04", -7f, 11.5f, 350f);
            Decal(floor, "SM_Env_Grunge_07", 6.5f, 10.5f, 340f);
            Decal(floor, "SM_Env_Grunge_02", 12.5f, 6.5f, 90f);
            Decal(floor, "SM_Env_Grunge_10", 12.5f, -7.5f, 70f);
            Decal(floor, "SM_Env_Grunge_05", 6f, -12f, 10f);
            Decal(floor, "SM_Env_Grunge_08", -7f, -11.8f, 0f);
            Decal(floor, "SM_Env_Grunge_11", -12.5f, -8f, 100f);
            Decal(floor, "SM_Env_Moss_01", 14.8f, 12f, 30f);
            Decal(floor, "SM_Env_Moss_02", -14.6f, 12.4f, 200f);
            Decal(floor, "SM_Env_Moss_03", 14.5f, -12.2f, 120f);
        }

        private static void Decal(Transform parent, string prefab, float x, float z, float yaw)
        {
            GameObject go = Spawn(prefab, parent);
            if (go == null) return;
            go.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            PlaceOn(go, new Vector3(x, 0.015f, z));
            foreach (var r in go.GetComponentsInChildren<Renderer>()) r.shadowCastingMode = ShadowCastingMode.Off;
        }

        // ------------------------------------------------------------------ outer walls (plan §2.1–2.4)

        private static readonly string[] NorthRunWest =
            { "SM_Bld_Base_Wall_01", "SM_Bld_Wall_Window_Tall_01", "SM_Bld_Base_Wall_01", "SM_Bld_Wall_Window_Tall_01", "SM_Bld_Base_Wall_01", "SM_Bld_Base_Wall_01" };
        private static readonly string[] NorthRunEast =
            { "SM_Bld_Base_Wall_01", "SM_Bld_Wall_Window_Tall_01", "SM_Bld_Base_Wall_01", "SM_Bld_Wall_Window_Tall_01", "SM_Bld_Base_Wall_01", "SM_Bld_Base_Wall_01" };
        private static readonly string[] EastRunSouth =
            { "SM_Bld_Base_Wall_01", "SM_Bld_Wall_Window_Tall_01", "SM_Bld_Base_Wall_01", "SM_Bld_Base_Wall_01", "SM_Bld_Base_Wall_01" };
        private static readonly string[] EastRunNorth =
            { "SM_Bld_Base_Wall_01", "SM_Bld_Base_Wall_01", "SM_Bld_Wall_Window_Tall_01", "SM_Bld_Base_Wall_01", "SM_Bld_Base_Wall_01" };

        private static void BuildNorthWall(Transform g)
        {
            // West run x -17..-2 (west to east), east run x 2..17 (east to west, so the ruin sits near the corner).
            Run(g, NorthRunWest, -17f, -Gate, NorthZ, alongX: true, yaw: 180f);
            Run(g, Reverse(NorthRunEast), Gate, 17f, NorthZ, alongX: true, yaw: 180f);
            foreach (float x in new[] { -12f, -7f, 7f, 12f, -14.5f, 14.5f })
                Pillar(g, "SM_Bld_Pillar_01", new Vector3(x, 0f, NorthZ - 0.05f), 180f);
            Pillar(g, "SM_Bld_Buttress_Pillar_01", new Vector3(-Gate - 0.3f, 0f, NorthZ - 0.1f), 180f);
            Pillar(g, "SM_Bld_Buttress_Pillar_01", new Vector3(Gate + 0.3f, 0f, NorthZ - 0.1f), 180f);
        }

        private static void BuildEastWall(Transform g)
        {
            Run(g, EastRunSouth, -14f, -Gate, EastX, alongX: false, yaw: 270f);
            Run(g, EastRunNorth, Gate, 14f, EastX, alongX: false, yaw: 270f);
            foreach (float z in new[] { -9.2f, -6.8f, 6.8f, 9.2f })
                Pillar(g, "SM_Bld_Pillar_01", new Vector3(EastX - 0.05f, 0f, z), 270f);
            Pillar(g, "SM_Bld_Buttress_Pillar_01", new Vector3(EastX - 0.1f, 0f, -Gate - 0.3f), 270f);
            Pillar(g, "SM_Bld_Buttress_Pillar_01", new Vector3(EastX - 0.1f, 0f, Gate + 0.3f), 270f);
        }

        private static void BuildSouthWall(Transform g)
        {
            Run(g, Repeat("SM_Bld_Base_Wall_Half_01", 5), -17f, -4.5f, SouthZ, alongX: true, yaw: 180f);
            Run(g, Repeat("SM_Bld_Base_Wall_Half_01", 5), 4.5f, 17f, SouthZ, alongX: true, yaw: 180f);
            Fence(g, -4.5f, -Gate, SouthZ, alongX: true, yaw: 180f);
            Fence(g, Gate, 4.5f, SouthZ, alongX: true, yaw: 180f);
            foreach (float x in new[] { -9.5f, 9.5f, -14.5f, 14.5f, -4.5f, 4.5f })
                Pillar(g, "SM_Bld_Base_Pillar_Half_01", new Vector3(x, 0f, SouthZ), 0f);
            Pillar(g, "SM_Bld_Base_Pillar_Half_01", new Vector3(-Gate - 0.2f, 0f, SouthZ), 0f);
            Pillar(g, "SM_Bld_Base_Pillar_Half_01", new Vector3(Gate + 0.2f, 0f, SouthZ), 0f);
            Prop(g, "SM_Prop_Lantern_02", -Gate - 0.2f, 1.51f, SouthZ, 0f);
            Prop(g, "SM_Prop_Lantern_02", Gate + 0.2f, 1.51f, SouthZ, 0f);
        }

        private static void BuildWestWall(Transform g)
        {
            Run(g, Repeat("SM_Bld_Base_Wall_Half_01", 4), -14f, -4.4f, WestX, alongX: false, yaw: 270f);
            Run(g, Repeat("SM_Bld_Base_Wall_Half_01", 4), 4.4f, 14f, WestX, alongX: false, yaw: 270f);
            Fence(g, -4.4f, -Gate, WestX, alongX: false, yaw: 270f);
            Fence(g, Gate, 4.4f, WestX, alongX: false, yaw: 270f);
            foreach (float z in new[] { -9.2f, 9.2f, -4.4f, 4.4f })
                Pillar(g, "SM_Bld_Base_Pillar_Half_01", new Vector3(WestX, 0f, z), 0f);
            Pillar(g, "SM_Bld_Base_Pillar_Half_01", new Vector3(WestX, 0f, -Gate - 0.2f), 0f);
            Pillar(g, "SM_Bld_Base_Pillar_Half_01", new Vector3(WestX, 0f, Gate + 0.2f), 0f);
            Prop(g, "SM_Prop_Lantern_02", WestX, 1.51f, -Gate - 0.2f, 0f);
            Prop(g, "SM_Prop_Lantern_02", WestX, 1.51f, Gate + 0.2f, 0f);
        }

        private static void Fence(Transform g, float from, float to, float line, bool alongX, float yaw)
        {
            GameObject f = Spawn("SM_Bld_Fence_Cemetary_01", g);
            if (f == null) return;
            f.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            f.transform.localScale = new Vector3(1f, 0.75f, 1f); // 1.43 m: under the 1.5 m camera-side limit
            FitSpan(f, from, to, line, alongX, 0f);
        }

        /// <summary>A row of wall pieces filling from..to on one line, each stretched to an equal share.</summary>
        private static void Run(Transform g, string[] pieces, float from, float to, float line, bool alongX, float yaw)
        {
            float step = (to - from) / pieces.Length;
            for (int i = 0; i < pieces.Length; i++)
            {
                GameObject w = Spawn(pieces[i], g);
                if (w == null) continue;
                w.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
                FitSpan(w, from + step * i, from + step * (i + 1), line, alongX, 0f);
            }
        }

        private static void Pillar(Transform g, string prefab, Vector3 at, float yaw)
        {
            GameObject p = Spawn(prefab, g);
            if (p == null) return;
            p.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            PlaceOn(p, at);
        }

        // ------------------------------------------------------------------ L blocks (plan §2.5)

        private static readonly (Vector2 a, Vector2 b)[] NorthWestOutline =
        {
            (new Vector2(-10f, 9f), new Vector2(-2f, 9f)), (new Vector2(-2f, 9f), new Vector2(-2f, 6f)),
            (new Vector2(-2f, 6f), new Vector2(-7f, 6f)), (new Vector2(-7f, 6f), new Vector2(-7f, 2f)),
            (new Vector2(-7f, 2f), new Vector2(-10f, 2f)), (new Vector2(-10f, 2f), new Vector2(-10f, 9f)),
        };

        private static readonly (Vector2 a, Vector2 b)[] SouthEastOutline =
        {
            (new Vector2(2f, -9f), new Vector2(10f, -9f)), (new Vector2(10f, -9f), new Vector2(10f, -2f)),
            (new Vector2(10f, -2f), new Vector2(7f, -2f)), (new Vector2(7f, -2f), new Vector2(7f, -6f)),
            (new Vector2(7f, -6f), new Vector2(2f, -6f)), (new Vector2(2f, -6f), new Vector2(2f, -9f)),
        };

        private static void BuildBlocks(Transform g)
        {
            Wrap(g, NorthWestOutline, new Vector2(-6f, 5.5f));
            Wrap(g, SouthEastOutline, new Vector2(6f, -5.5f));
            // Stone slabs on the block tops (the gameplay cubes underneath keep their colliders).
            TopTiles(g, new Rect(-10f, 6f, 8f, 3f));
            TopTiles(g, new Rect(-10f, 2f, 3f, 4f));
            TopTiles(g, new Rect(2f, -9f, 8f, 3f));
            TopTiles(g, new Rect(7f, -6f, 3f, 4f));

            // NW landmark: gargoyle on the outer corner, candles, skulls and an open book on the block top (y 1.5).
            Prop(g, "SM_Prop_Gargoyle_Statue_01", -8.8f, 1.5f, 8.1f, 225f);
            Prop(g, "SM_Prop_Candelabra_01", -7.0f, 1.5f, 8.3f, 0f);
            Prop(g, "SM_Prop_Skull_Pile_01", -5.2f, 1.5f, 8.1f, 30f);
            Prop(g, "SM_Prop_Book_01_Open_01", -6.2f, 1.5f, 7.0f, 210f);
            Prop(g, "SM_Prop_Tomb_Stone_05", -3.6f, 1.5f, 7.5f, 90f);
            Prop(g, "SM_Prop_Candle_03", -2.6f, 1.5f, 6.5f, 0f);
            Prop(g, "SM_Prop_Tomb_Stone_06", -8.5f, 1.5f, 3.6f, 0f);
            Prop(g, "SM_Prop_Rubble_Pile_Burnt_03", -8.6f, 1.5f, 5.4f, 60f, 0.6f);

            // SE landmark: statue on the outer corner facing the centre, the altar, candles and skulls.
            Prop(g, "SM_Prop_Statue_01", 8.8f, 1.5f, -8.0f, 315f);
            Prop(g, "SM_Prop_Tabernacle_01", 4.6f, 1.5f, -7.7f, 180f);
            Prop(g, "SM_Prop_Candle_01", 2.6f, 1.5f, -6.6f, 0f);
            Prop(g, "SM_Prop_Candle_07", 6.8f, 1.5f, -6.6f, 0f);
            Prop(g, "SM_Prop_Skull_Pile_02", 7.6f, 1.5f, -6.7f, 150f);
            Prop(g, "SM_Prop_Tomb_Stone_03", 8.5f, 1.5f, -2.9f, 20f);
            Prop(g, "SM_Prop_Skull_Pile_01", 8.4f, 1.5f, -4.8f, 80f);
        }

        private static void TopTiles(Transform g, Rect rect)
        {
            int nx = Mathf.Max(1, Mathf.RoundToInt(rect.width / 2.5f));
            int nz = Mathf.Max(1, Mathf.RoundToInt(rect.height / 2.5f));
            float w = rect.width / nx;
            float d = rect.height / nz;
            for (int i = 0; i < nx; i++)
            {
                for (int j = 0; j < nz; j++)
                {
                    GameObject t = Spawn("SM_Bld_Base_Floor_01", g);
                    if (t == null) return;
                    t.transform.localScale = new Vector3(w / 2.5f, 1f, d / 2.5f);
                    FitBox(t, new Vector3(rect.x + w * (i + 0.5f), 0f, rect.y + d * (j + 0.5f)), Vector3.zero, 1.505f, top: true);
                }
            }
        }

        /// <summary>Half walls (1.5 m, the block height) around an L block's outline, outer face flush with the collider.</summary>
        private static void Wrap(Transform g, (Vector2 a, Vector2 b)[] outline, Vector2 inside)
        {
            foreach (var (a, b) in outline)
            {
                bool alongX = Mathf.Abs(a.y - b.y) < 0.01f;
                float from = alongX ? Mathf.Min(a.x, b.x) : Mathf.Min(a.y, b.y);
                float to = alongX ? Mathf.Max(a.x, b.x) : Mathf.Max(a.y, b.y);
                float lineCoord = alongX ? a.y : a.x;
                // Shift the wall inward by half its thickness so its outer face sits on the collider face.
                float inwardSign = alongX ? Mathf.Sign(inside.y - lineCoord) : Mathf.Sign(inside.x - lineCoord);
                // 0.1 (not the full 0.115 half thickness) keeps the face 1.5 cm proud of the block, so they never z-fight.
                float line = lineCoord + inwardSign * 0.1f;
                int count = Mathf.Max(1, Mathf.RoundToInt((to - from) / 2.5f));
                float yaw = alongX ? (inwardSign > 0f ? 0f : 180f) : (inwardSign > 0f ? 90f : 270f);
                Run(g, Repeat("SM_Bld_Base_Wall_Half_01", count), from, to, line, alongX, yaw);
                Pillar(g, "SM_Bld_Base_Pillar_Half_01", new Vector3(a.x, 0f, a.y), 0f);
            }
        }

        // ------------------------------------------------------------------ corners (plan §2.6)

        private static void BuildCorners(Transform g)
        {
            Prop(g, "SM_Bld_Topper_01", 15.6f, 0f, 15.6f, 180f);
            Prop(g, "SM_Prop_Tomb_Stone_02", 15.2f, 0f, 12.9f, 215f);
            Prop(g, "SM_Prop_Tomb_Stone_04", 14.0f, 0f, 13.3f, 190f, 0.9f);
            Prop(g, "SM_Env_Plant_Fern_01", 15.3f, 0f, 11.6f, 40f);
            Prop(g, "SM_Prop_Tomb_Stone_01", -15.2f, 0f, 12.9f, 140f);
            Prop(g, "SM_Prop_Bone_Pile_01", -14.0f, 0f, 13.2f, 20f);
            Prop(g, "SM_Env_Rocks_Small_01", -15.0f, 0f, 11.4f, 70f, 0.7f);
            Prop(g, "SM_Prop_Rubble_Pile_Burnt_02", 14.8f, 0f, -12.9f, 15f, 0.6f);
            Prop(g, "SM_Prop_Skull_Pile_02", 13.5f, 0f, -13.3f, 70f);
            Prop(g, "SM_Prop_Body_Skeleton_01", 15.3f, 0f, -11.4f, 100f);
            Prop(g, "SM_Prop_Rubble_Pile_Burnt_01", -14.8f, 0f, -12.9f, 45f, 0.7f); // SW pyre base
            Prop(g, "SM_Prop_Bone_Pile_02", -15.3f, 0f, -11.3f, 20f);
            Prop(g, "SM_Prop_Skull_Pile_03", -13.4f, 0f, -13.4f, 330f, 0.8f);
        }

        // ------------------------------------------------------------------ beyond the walls (plan §2.8)

        private static void BuildBackground(Transform g)
        {
            // Dirt ground ring outside the walls (20 m squares), a little under the arena floor.
            foreach (var (x, z) in new[] { (-10f, 25f), (10f, 25f), (27f, -5f), (27f, 15f), (-10f, -25f), (10f, -25f),
                         (-27f, -5f), (-27f, 15f), (27f, 25f), (-27f, 25f), (27f, -25f), (-27f, -25f) })
            {
                GameObject d = Spawn("SM_Env_Ground_Dirt_Flat_01", g);
                if (d == null) continue;
                d.transform.localScale = new Vector3(4f, 1f, 4f);
                // Each square a few mm lower than the last, so overlaps never z-fight.
                PlaceOn(d, new Vector3(x, -0.12f - 0.004f * g.childCount, z));
            }
            Prop(g, "SM_Prop_Crypt_01", 9f, -0.05f, 21f, 180f);
            Prop(g, "SM_Bld_Gates_Cemetary_01", 0f, -0.05f, 18.5f, 180f);
            Prop(g, "SM_Env_Rock_Cliff_01", -13f, -1.5f, 29f, 180f);
            Prop(g, "SM_Env_Rock_Cliff_02", -3f, -1.5f, 31f, 180f);
            Prop(g, "SM_Env_Rock_Cliff_03", 7f, -1.5f, 30f, 180f);
            Prop(g, "SM_Env_Rock_Cliff_04", 17f, -1.5f, 30f, 180f);
            Prop(g, "SM_Env_Rock_Cliff_05", 28f, -1f, -4f, 270f);
            Prop(g, "SM_Env_Rock_Cliff_06", 28f, -1f, 10f, 270f);
            Prop(g, "SM_Env_Tree_Dead_01", -16.5f, -0.1f, 18f, 20f, 1.2f);
            Prop(g, "SM_Env_Tree_Dead_02", -7f, -0.1f, 20f, 100f, 1.1f);
            Prop(g, "SM_Env_Tree_Dead_03", 20f, -0.1f, 17f, 300f);
            Prop(g, "SM_Env_Rock_01", 20.5f, -0.1f, 8f, 40f, 1.3f);
            Prop(g, "SM_Env_Rock_01", -21f, -0.1f, -9f, 150f, 1.3f);
        }

        // ------------------------------------------------------------------ particles (plan §2.9)

        // FX_Ash_01 and FX_Fog_01 are left out: under the low-res orthographic game camera their big soft quads
        // read as grey slabs over the whole arena (seen in the 2026-10-24 captures).
        private static void BuildFx(Transform g)
        {
            Fx(g, "FX_Fire_01", new Vector3(-Gate - 0.3f, 3.0f, NorthZ - 0.1f), 0.6f);
            Fx(g, "FX_Fire_01", new Vector3(Gate + 0.3f, 3.0f, NorthZ - 0.1f), 0.6f);
            Fx(g, "FX_Fire_01", new Vector3(EastX - 0.1f, 3.0f, -Gate - 0.3f), 0.6f);
            Fx(g, "FX_Fire_01", new Vector3(EastX - 0.1f, 3.0f, Gate + 0.3f), 0.6f);
            Fx(g, "FX_Fire_01", new Vector3(-14.8f, 0.2f, -12.9f), 0.7f); // SW pyre
            Fx(g, "FX_Fire_Embers_01", new Vector3(-14.8f, 0.4f, -12.9f), 0.6f);
            Fx(g, "FX_Bats_01", new Vector3(-6f, 6f, 20f), 1f);
        }

        private static void Fx(Transform g, string prefab, Vector3 at, float scale)
        {
            GameObject go = Spawn(prefab, g);
            if (go == null) return;
            go.transform.position = at;
            go.transform.localScale = Vector3.one * scale;
            // Particle-only prefabs: drop any light so the 12-light budget holds.
            foreach (var l in go.GetComponentsInChildren<Light>(true)) Object.DestroyImmediate(l);
        }

        // ------------------------------------------------------------------ placement helpers

        private static void Prop(Transform g, string prefab, float x, float y, float z, float yaw, float scale = 1f)
        {
            GameObject go = Spawn(prefab, g);
            if (go == null) return;
            go.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            go.transform.localScale = Vector3.one * scale;
            PlaceOn(go, new Vector3(x, y, z));
        }

        /// <summary>Moves the object so its renderer bounds' bottom-centre lands on <paramref name="at"/>.</summary>
        private static void PlaceOn(GameObject go, Vector3 at)
        {
            if (!TryBounds(go, out Bounds b))
            {
                go.transform.position = at;
                return;
            }
            go.transform.position += new Vector3(at.x - b.center.x, at.y - b.min.y, at.z - b.center.z);
        }

        /// <summary>Stretches a wall piece along its long axis to fill from..to and centres it on the line.</summary>
        private static void FitSpan(GameObject go, float from, float to, float line, bool alongX, float bottom)
        {
            Vector3 axis = alongX ? Vector3.right : Vector3.forward;
            if (!TryBounds(go, out Bounds b)) return;
            float length = alongX ? b.size.x : b.size.z;
            if (length > 0.01f)
            {
                float factor = (to - from) / length;
                Vector3 s = go.transform.localScale;
                // Scale the local axis that lies along the span.
                if (Mathf.Abs(Vector3.Dot(go.transform.right, axis)) > 0.7f) s.x *= factor;
                else s.z *= factor;
                go.transform.localScale = s;
                TryBounds(go, out b);
            }
            float mid = (from + to) * 0.5f;
            Vector3 target = alongX ? new Vector3(mid, bottom, line) : new Vector3(line, bottom, mid);
            go.transform.position += new Vector3(target.x - b.center.x, target.y - b.min.y, target.z - b.center.z);
        }

        /// <summary>Fits a flat tile into a box footprint (x/z size), its top (or bottom) at <paramref name="bottom"/>.</summary>
        private static void FitBox(GameObject go, Vector3 center, Vector3 size, float bottom, bool top)
        {
            if (!TryBounds(go, out Bounds b)) return;
            float y = top ? bottom - b.max.y : bottom - b.min.y;
            go.transform.position += new Vector3(center.x - b.center.x, y, center.z - b.center.z);
        }

        private static bool TryBounds(GameObject go, out Bounds bounds)
        {
            bounds = default;
            bool any = false;
            foreach (var r in go.GetComponentsInChildren<Renderer>(true))
            {
                if (r is ParticleSystemRenderer || r is TrailRenderer || r is LineRenderer) continue;
                if (!any) { bounds = r.bounds; any = true; }
                else bounds.Encapsulate(r.bounds);
            }
            return any;
        }

        /// <summary>Instantiates a Polygon prefab under the group, unpacked, without colliders, batching-static.</summary>
        private static GameObject Spawn(string prefab, Transform parent)
        {
            if (!_paths.TryGetValue(prefab, out string path))
            {
                Debug.LogWarning("GothicArenaBuilder: prefab not found: " + prefab);
                return null;
            }
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            var go = (GameObject)PrefabUtility.InstantiatePrefab(asset, parent.gameObject.scene);
            PrefabUtility.UnpackPrefabInstance(go, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            go.transform.SetParent(parent, true);
            foreach (var c in go.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(c);
            bool particles = go.GetComponentInChildren<ParticleSystem>(true) != null;
            foreach (Transform t in go.GetComponentsInChildren<Transform>(true))
            {
                if (particles && t.GetComponent<ParticleSystem>() != null) continue;
                GameObjectUtility.SetStaticEditorFlags(t.gameObject, StaticEditorFlags.BatchingStatic);
            }
            return go;
        }

        private static void IndexPrefabs()
        {
            _paths = new Dictionary<string, string>();
            foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { Pf.TrimEnd('/') }))
            {
                string p = AssetDatabase.GUIDToAssetPath(guid);
                _paths[System.IO.Path.GetFileNameWithoutExtension(p)] = p;
            }
        }

        private static Transform Group(Transform parent, string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            return go.transform;
        }

        private static Transform FindRoot(Scene scene, string name)
        {
            foreach (GameObject go in scene.GetRootGameObjects())
                if (go.name == name) return go.transform;
            return null;
        }

        private static string[] Repeat(string prefab, int count)
        {
            var a = new string[count];
            for (int i = 0; i < count; i++) a[i] = prefab;
            return a;
        }

        private static string[] Reverse(string[] a)
        {
            var r = (string[])a.Clone();
            System.Array.Reverse(r);
            return r;
        }

        private static Color Hex(string hex)
        {
            ColorUtility.TryParseHtmlString(hex, out Color c);
            return c;
        }
    }
}
