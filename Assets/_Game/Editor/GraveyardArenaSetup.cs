using System.Collections.Generic;
using System.Text;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;

namespace DanceWithTheDevil.Editor
{
    /// <summary>
    /// Menu <c>Dance/Arena/Fit Graveyard Map</c>: fits the run onto the Synty graveyard map SefaSelim placed in
    /// Arena.unity (plan: production/levels/graveyard-arena/layout.md). On the open Arena scene it:
    /// <list type="bullet">
    /// <item>opens a fighting space: removes the props inside a 5.5 × 4.5 m ellipse round Defne's start, the middle
    /// crypt group, and the fence pieces, statues, stones and ruins in the six entrance gaps (N, S, SE, E gate, wide W);</item>
    /// <item>removes the colliders of small clutter (corpses, characters, braziers, books, flat tomb slabs, logs) and
    /// of any foliage; the Ground floor patches keep theirs (they are the floor);</item>
    /// <item>puts eight entrances on the plateau edge (spawn point, floor marker, warning), wires them into the
    /// WaveDirector, adds invisible walls round the plateau, sets the camera clamp;</item>
    /// <item>re-bakes the NavMesh on the scene's NavMeshSurface, limited to the plateau volume (not the ocean,
    /// cliffs, stairs or the background), and saves the scene.</item>
    /// </list>
    /// Objects are found by name and position (the map's names repeat), so running it again finds nothing left to
    /// remove and only re-places the entrances and re-bakes. Logs every change.
    /// </summary>
    public static class GraveyardArenaSetup
    {
        private const string ScenePath = "Assets/_Game/Scenes/Arena.unity";
        private const float EllipseX = 5.5f, EllipseZ = 4.5f;
        private const float MatchRadius = 0.8f;

        // (name part, x, z): removed to open the centre and the entrance gaps (layout.md §2).
        private static readonly (string name, float x, float z)[] Remove =
        {
            // middle crypt group, at Defne's start
            ("SM_Prop_Crypt_01", -0.6f, -2.8f), ("Crypt_01_Glass_01", -0.6f, -2.8f), ("Crypt_Door_01", 1.3f, -2.8f),
            ("Base_Floor_Combined_01", -0.6f, -2.8f), ("Tomb_01", -0.7f, -2.8f), ("Tomb_01_Lid", -0.7f, -2.8f),
            // gap N
            ("Fence_Cemetary_01", 1.5f, 8.5f), ("Fence_Cemetary_02", -0.9f, 8.5f), ("Fence_Pillar", 2.8f, 8.6f),
            ("Statue_02", 2.8f, 7.7f), ("Tomb_Stone_03", 0.9f, 7.5f), ("Tomb_Stone_Basic_01", 1.0f, 6.1f),
            ("Ruin_04", 3.2f, 10.5f), ("Tomb_02", -1.7f, 5.7f), ("Tomb_02_Lid", -1.7f, 5.7f),
            // gap S
            ("Fence_Cemetary_01", -1.0f, -9.3f), ("Fence_Cemetary_02", 1.4f, -9.3f), ("Tomb_Stone_03", -2.2f, -8.4f),
            ("Tree_Trunk_01", -1.3f, -11.6f),
            // gap SE
            ("Fence_Cemetary_01", 6.6f, -9.3f), ("Fence_Cemetary_01", 9.0f, -9.3f), ("Ruin_04", 7.2f, -10.9f),
            ("Tomb_Stone_05", 8.8f, -8.1f), ("Tomb_Stone_02", 8.1f, -6.5f),
            // gap E (gate)
            ("Gates_Cemetary_01", 10.5f, -0.3f), ("Fence_Cemetary_01", 10.6f, -3.2f),
            // gap W
            ("Fence_Cemetary_01", -12.4f, -2.8f), ("Fence_Cemetary_01", -12.4f, -0.2f), ("Fence_Cemetary_02", -12.4f, -5.3f),
            ("Fence_Pillar", -12.3f, -1.8f), ("Statue_01", -11.6f, -0.6f), ("Tree_Dead_01", -14.4f, -1.5f),
            ("Ruin_04", -9.2f, -5.3f),
        };

        // Collider-free clutter (layout.md §3): names containing these lose every collider.
        private static readonly string[] ClutterNames =
        {
            "Tomb_Stone_05", "Tomb_Stone_06", "Tree_Trunk_01", "Body_", "Skeleton", "Corpse", "Brazier", "Book",
            "Flag", "Meat", "Candle", "Helmet", "Sword", "Bone", "Skull",
        };
        private static readonly string[] ClutterCategories = { "Corpses", "Characters", "Lights", "Miscellaneous", "Foliage", "Cover" };

        // Eight entrances on the plateau edge (layout.md §5).
        private static readonly (string name, float x, float z)[] Entrances =
        {
            ("North", -0.5f, 11.0f), ("NorthEast", 12.0f, 9.0f), ("East", 13.2f, 0.2f), ("SouthEast", 7.8f, -11.6f),
            ("South", 0.2f, -11.0f), ("West", -14.2f, -5.8f), ("WestNorth", -14.2f, 1.0f), ("NorthWest", -5.5f, 11.17f),
        };

        // Walkable plateau outline, clockwise (layout.md §1): invisible walls sit 0.5 m outside each edge.
        private static readonly Vector2[] Outline =
        {
            new Vector2(-16f, 8.5f), new Vector2(-10f, 11.8f), new Vector2(11.5f, 11.8f), new Vector2(14.5f, 9f),
            new Vector2(15f, 3f), new Vector2(15f, -5f), new Vector2(12.5f, -10.5f), new Vector2(9f, -13.5f),
            new Vector2(-8f, -13.5f), new Vector2(-12f, -11f), new Vector2(-16f, -8.5f),
        };

        private static readonly Vector3 BakeCentre = new Vector3(-0.5f, 1f, -0.8f);
        private static readonly Vector3 BakeSize = new Vector3(33f, 4f, 28f);
        private static readonly Vector2 CameraMin = new Vector2(-8f, -6f), CameraMax = new Vector2(8f, 6f);

        // Fit Graveyard Map + Flatten: the map is scaled up by this about the origin (characters keep their size),
        // and every gameplay coordinate above is multiplied by the map's current scale (the "Arena" root's).
        private const float MapScale = 1.5f;
        private static readonly string[] MapRoots = { "Arena", "Props", "Lighting", "Particles" };
        private const string FloorTilePrefab = "SM_Bld_Base_Floor_01";
        // The map's own fog sheet (FX_Fog_01): kept as a faint trace.
        private const float FogAlpha = 0.03f;
        private const int FogMaxParticles = 60;
        private static float s_scale = 1f;
        private static Vector2 S(Vector2 v) => v * s_scale;

        [MenuItem("Dance/Arena/Fit Graveyard Map")]
        public static void Apply()
        {
            var scene = EditorSceneManager.GetActiveScene();
            if (scene.path != ScenePath)
            {
                Debug.LogError("GraveyardArenaSetup: open " + ScenePath + " first.");
                return;
            }
            var log = new StringBuilder("GraveyardArenaSetup:\n");
            s_scale = CurrentScale(scene);
            OpenSpace(scene, log);
            StripClutterColliders(scene, log);
            PlaceEntrances(scene, log);
            BuildEdgeWalls(scene, log);
            SetCameraClamp(log);
            Bake(scene, log);
            SnapEntrances(scene, log);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log(log.ToString());
        }

        /// <summary>
        /// Menu <c>Dance/Arena/Flatten And Scale Graveyard</c> (after Fit Graveyard Map): scales the map up by
        /// <see cref="MapScale"/> about the origin, replaces the plateau's bumpy ground patches with the old flat
        /// cracked-stone floor (SM_Bld_Base_Floor_01 tiles, top at y 0, with colliders), sets every prop, light and
        /// effect on the plateau down onto it, thins the map's fog to a trace, then re-places the entrances, edge
        /// walls, camera clamp and NavMesh for the new size. Runs once: a map already scaled is only refitted.
        /// </summary>
        [MenuItem("Dance/Arena/Flatten And Scale Graveyard")]
        public static void FlattenAndScale()
        {
            var scene = EditorSceneManager.GetActiveScene();
            if (scene.path != ScenePath)
            {
                Debug.LogError("GraveyardArenaSetup: open " + ScenePath + " first.");
                return;
            }
            var log = new StringBuilder("GraveyardArenaSetup (flatten + scale):\n");
            if (Mathf.Approximately(CurrentScale(scene), 1f)) ScaleMap(scene, log);
            s_scale = CurrentScale(scene);
            Physics.SyncTransforms();
            Flatten(scene, log);
            ThinFog(scene, log);
            Physics.SyncTransforms();
            PlaceEntrances(scene, log);
            BuildEdgeWalls(scene, log);
            SetCameraClamp(log);
            Bake(scene, log);
            SnapEntrances(scene, log);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log(log.ToString());
        }

        private static float CurrentScale(UnityEngine.SceneManagement.Scene scene)
        {
            GameObject arena = Root(scene, "Arena");
            return arena != null ? arena.transform.localScale.x : 1f;
        }

        // ------------------------------------------------------------------ scale, flatten, fog

        private static void ScaleMap(UnityEngine.SceneManagement.Scene scene, StringBuilder log)
        {
            foreach (string name in MapRoots)
            {
                GameObject root = Root(scene, name);
                if (root == null) continue;
                Undo.RecordObject(root.transform, "Scale map");
                root.transform.position = Vector3.zero; // scale about the origin (they sat at y 0 or 0.1)
                root.transform.localScale = Vector3.one * MapScale;
            }
            // The entrance plates and warnings live under Arena but keep their size (they mark a gameplay spot).
            GameObject arena = Root(scene, "Arena");
            if (arena != null)
                foreach (Transform t in arena.GetComponentsInChildren<Transform>(true))
                    if (t.name.StartsWith("Entrance_") || t.name.StartsWith("Warning_"))
                    {
                        Undo.RecordObject(t, "Marker size");
                        t.localScale /= MapScale;
                    }
            // Light reach grows with the map; the effects scale with their roots.
            foreach (Light light in Object.FindObjectsByType<Light>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (light.type == LightType.Directional || !UnderMap(light.transform, scene)) continue;
                Undo.RecordObject(light, "Light range");
                light.range *= MapScale;
            }
            log.Append("  map scaled ×").Append(MapScale).Append(" (").Append(string.Join(", ", MapRoots)).Append(")\n");
        }

        private static bool UnderMap(Transform t, UnityEngine.SceneManagement.Scene scene)
        {
            Transform root = t.root;
            foreach (string name in MapRoots)
                if (root.name == name) return true;
            return false;
        }

        /// <summary>
        /// Plateau floor: the ground patches whose centre is inside the play outline go; flat floor tiles go in their
        /// place, top at y 0; the props, buildings, trees, foliage, lights and effects standing on the plateau are
        /// lowered (or raised) by the old ground height under them, so they stand on the new floor.
        /// </summary>
        private static void Flatten(UnityEngine.SceneManagement.Scene scene, StringBuilder log)
        {
            GameObject arena = Root(scene, "Arena");
            Transform ground = arena != null ? arena.transform.Find("Environment/Ground") : null;
            if (ground == null)
            {
                log.Append("  flatten: Arena/Environment/Ground missing (already flat?)\n");
            }
            else
            {
                // 1. Units standing on the plateau: drop them by the old ground height under their pivot.
                var units = new List<Transform>();
                foreach (string category in new[] { "Buildings", "Trees", "Cover", "Foliage" })
                {
                    Transform group = arena.transform.Find("Environment/" + category);
                    if (group != null) foreach (Transform t in group) units.Add(t);
                }
                foreach (string rootName in new[] { "Props", "Lighting", "Particles" })
                {
                    GameObject root = Root(scene, rootName);
                    if (root == null) continue;
                    foreach (Transform child in root.transform)
                    {
                        if (child.childCount > 0 && PrefabUtility.GetOutermostPrefabInstanceRoot(child.gameObject) != child.gameObject)
                            foreach (Transform t in child) units.Add(t); // a category group (Tombs, Corpses…)
                        else units.Add(child);
                    }
                }
                int moved = 0;
                foreach (Transform t in units)
                {
                    Vector3 p = t.position;
                    if (!InsideOutline(new Vector2(p.x, p.z))) continue;
                    if (!GroundHeight(ground, p, out float height)) continue;
                    Undo.RecordObject(t, "Flatten");
                    t.position = new Vector3(p.x, p.y - height, p.z);
                    moved++;
                }

                // 2. The bumpy ground patches on the plateau go.
                int removed = 0;
                var doomed = new List<GameObject>();
                foreach (Transform patch in ground)
                {
                    Renderer r = patch.GetComponentInChildren<Renderer>();
                    Vector3 c = r != null ? r.bounds.center : patch.position;
                    if (InsideOutline(new Vector2(c.x, c.z))) doomed.Add(patch.gameObject);
                }
                foreach (GameObject go in doomed)
                {
                    Undo.DestroyObjectImmediate(go);
                    removed++;
                }
                log.Append("  flatten: ").Append(moved).Append(" objects set onto y 0, ").Append(removed).Append(" ground patches removed\n");
            }
            LayFloor(scene, log);
        }

        private static bool GroundHeight(Transform ground, Vector3 at, out float height)
        {
            height = 0f;
            bool found = false;
            foreach (RaycastHit hit in Physics.RaycastAll(new Vector3(at.x, at.y + 4f, at.z), Vector3.down, 10f, ~0,
                         QueryTriggerInteraction.Ignore))
            {
                if (!hit.transform.IsChildOf(ground)) continue;
                if (!found || hit.point.y > height) height = hit.point.y;
                found = true;
            }
            if (found) height = Mathf.Clamp(height, -1f, 1.5f);
            return found;
        }

        /// <summary>The old arena floor: SM_Bld_Base_Floor_01 tiles over the play outline, top at y 0, each with a box collider.</summary>
        private static void LayFloor(UnityEngine.SceneManagement.Scene scene, StringBuilder log)
        {
            GameObject old = Root(scene, "Floor");
            if (old != null) Undo.DestroyObjectImmediate(old);
            string[] guids = AssetDatabase.FindAssets(FloorTilePrefab + " t:Prefab");
            GameObject prefab = null;
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (System.IO.Path.GetFileNameWithoutExtension(path) == FloorTilePrefab)
                    prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            }
            if (prefab == null)
            {
                log.Append("  floor: prefab ").Append(FloorTilePrefab).Append(" not found\n");
                return;
            }
            var root = new GameObject("Floor");
            Undo.RegisterCreatedObjectUndo(root, "Floor");
            root.isStatic = true;

            Vector2 min = new Vector2(float.MaxValue, float.MaxValue), max = new Vector2(float.MinValue, float.MinValue);
            foreach (Vector2 v in Outline)
            {
                min = Vector2.Min(min, S(v));
                max = Vector2.Max(max, S(v));
            }
            const float tile = 2.5f;
            int count = 0;
            for (float x = Mathf.Floor(min.x / tile) * tile; x < max.x; x += tile)
            {
                for (float z = Mathf.Floor(min.y / tile) * tile; z < max.y; z += tile)
                {
                    Vector2 centre = new Vector2(x + tile * 0.5f, z + tile * 0.5f);
                    if (!InsideOutline(centre, 0.8f)) continue;
                    var t = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
                    t.transform.SetParent(root.transform, false);
                    int i = Mathf.RoundToInt(x / tile), j = Mathf.RoundToInt(z / tile);
                    t.transform.rotation = Quaternion.Euler(0f, ((i * 7 + j * 3) & 1) * 180f, 0f);
                    foreach (Collider c in t.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(c);
                    Bounds b = RendererBounds(t);
                    t.transform.position += new Vector3(centre.x - b.center.x, -b.max.y, centre.y - b.center.z);
                    var box = t.AddComponent<BoxCollider>();
                    box.center = t.transform.InverseTransformPoint(new Vector3(centre.x, -0.25f, centre.y));
                    box.size = new Vector3(tile / t.transform.lossyScale.x, 0.5f / t.transform.lossyScale.y, tile / t.transform.lossyScale.z);
                    foreach (Renderer r in t.GetComponentsInChildren<Renderer>())
                        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    t.isStatic = true;
                    count++;
                }
            }
            log.Append("  floor: ").Append(count).Append(" flat tiles (").Append(FloorTilePrefab).Append("), top y 0\n");
        }

        private static Bounds RendererBounds(GameObject go)
        {
            Renderer[] renderers = go.GetComponentsInChildren<Renderer>();
            Bounds b = renderers.Length > 0 ? renderers[0].bounds : new Bounds(go.transform.position, Vector3.zero);
            foreach (Renderer r in renderers) b.Encapsulate(r.bounds);
            return b;
        }

        private static void ThinFog(UnityEngine.SceneManagement.Scene scene, StringBuilder log)
        {
            foreach (ParticleSystem ps in Object.FindObjectsByType<ParticleSystem>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (!ps.name.StartsWith("FX_Fog")) continue;
                Undo.RecordObject(ps, "Fog");
                ParticleSystem.MainModule main = ps.main;
                Color c = main.startColor.color;
                main.startColor = new Color(c.r, c.g, c.b, FogAlpha);
                main.maxParticles = FogMaxParticles;
                log.Append("  fog ").Append(ps.name).Append(": alpha ").Append(c.a.ToString("F2")).Append(" → ").Append(FogAlpha)
                    .Append(", max particles → ").Append(FogMaxParticles).Append('\n');
            }
        }

        /// <summary>Point-in-polygon on the (scaled) play outline, optionally grown by <paramref name="grow"/> metres.</summary>
        private static bool InsideOutline(Vector2 p, float grow = 0f)
        {
            bool inside = false;
            for (int i = 0, j = Outline.Length - 1; i < Outline.Length; j = i++)
            {
                Vector2 a = S(Outline[i]), b = S(Outline[j]);
                if ((a.y > p.y) != (b.y > p.y) && p.x < (b.x - a.x) * (p.y - a.y) / (b.y - a.y) + a.x) inside = !inside;
            }
            if (inside || grow <= 0f) return inside;
            for (int i = 0, j = Outline.Length - 1; i < Outline.Length; j = i++)
            {
                Vector2 a = S(Outline[i]), b = S(Outline[j]);
                Vector2 ab = b - a;
                float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / Mathf.Max(1e-6f, ab.sqrMagnitude));
                if ((a + ab * t - p).magnitude <= grow) return true;
            }
            return false;
        }

        /// <summary>
        /// After the bake: each spawn goes to the nearest NavMesh point at least 10.5 m (× scale) out with a complete
        /// path to the start, and its floor plate and warning are set on the floor under it.
        /// </summary>
        private static void SnapEntrances(UnityEngine.SceneManagement.Scene scene, StringBuilder log)
        {
            var waves = Object.FindAnyObjectByType<WaveDirector>();
            GameObject arena = Root(scene, "Arena");
            if (waves == null || arena == null) return;
            var so = new SerializedObject(waves);
            SerializedProperty list = so.FindProperty("_spawnPoints");
            float minOut = 10.5f * s_scale;
            for (int i = 0; i < list.arraySize; i++)
            {
                var spawn = list.GetArrayElementAtIndex(i).objectReferenceValue as Transform;
                if (spawn == null) continue;
                Vector3 want = spawn.position;
                Vector3 best = want;
                float bestDistance = float.MaxValue;
                for (float dx = -3f; dx <= 3f; dx += 0.5f)
                    for (float dz = -3f; dz <= 3f; dz += 0.5f)
                    {
                        var q = new Vector3(want.x + dx, 0.3f, want.z + dz);
                        if (!NavMesh.SamplePosition(q, out NavMeshHit hit, 0.4f, NavMesh.AllAreas)) continue;
                        if (new Vector2(hit.position.x, hit.position.z).magnitude < minOut) continue;
                        var path = new NavMeshPath();
                        if (!NavMesh.CalculatePath(hit.position, Vector3.zero, NavMesh.AllAreas, path)
                            || path.status != NavMeshPathStatus.PathComplete) continue;
                        float d = new Vector2(dx, dz).magnitude;
                        if (d < bestDistance)
                        {
                            bestDistance = d;
                            best = hit.position;
                        }
                    }
                if (bestDistance == float.MaxValue)
                {
                    log.Append("  WARNING: no reachable point near ").Append(spawn.name).Append('\n');
                    continue;
                }
                Quaternion facing = Quaternion.LookRotation(new Vector3(best.x, 0f, best.z).normalized, Vector3.up);
                Undo.RecordObject(spawn, "Snap");
                spawn.SetPositionAndRotation(best, facing);
                string name = spawn.name.Substring("Spawn_".Length);
                float floor = FloorHeight(best.x, best.z);
                foreach (string prefix in new[] { "Entrance_", "Warning_" })
                {
                    GameObject marker = FindChild(arena.transform, prefix + name);
                    if (marker == null) continue;
                    Undo.RecordObject(marker.transform, "Snap");
                    marker.transform.SetPositionAndRotation(new Vector3(best.x, floor + (prefix == "Entrance_" ? 0.02f : 0.03f), best.z), facing);
                }
                log.Append("  ").Append(spawn.name).Append(" → ").Append(best.ToString("F2")).Append(" (moved ")
                    .Append(bestDistance.ToString("F1")).Append(" m)\n");
            }
        }

        // ------------------------------------------------------------------ open space

        private static void OpenSpace(UnityEngine.SceneManagement.Scene scene, StringBuilder log)
        {
            var doomed = new HashSet<GameObject>();
            List<Renderer> renderers = MapRenderers(scene);
            foreach (var (name, x, z) in Remove)
            {
                Renderer best = null;
                float bestDistance = MatchRadius;
                foreach (Renderer r in renderers)
                {
                    if (r == null || !r.name.Contains(name)) continue;
                    Vector3 c = r.bounds.center;
                    float d = new Vector2(c.x - x, c.z - z).magnitude;
                    if (d < bestDistance)
                    {
                        bestDistance = d;
                        best = r;
                    }
                }
                if (best != null) doomed.Add(Owner(best.gameObject));
            }

            // Centre ellipse: every Props object (tombs, corpses, statues, lights…) whose bounds centre is inside.
            GameObject props = Root(scene, "Props");
            if (props != null)
                foreach (Renderer r in props.GetComponentsInChildren<Renderer>(true))
                {
                    Vector3 c = r.bounds.center;
                    if ((c.x / EllipseX) * (c.x / EllipseX) + (c.z / EllipseZ) * (c.z / EllipseZ) <= 1f)
                        doomed.Add(Owner(r.gameObject));
                }

            foreach (GameObject go in doomed)
            {
                if (go == null) continue;
                log.Append("  removed ").Append(go.name).Append(" at ").Append(go.transform.position.ToString("F1")).Append('\n');
                Undo.DestroyObjectImmediate(go);
            }
            log.Append("  open space: ").Append(doomed.Count).Append(" objects removed\n");
        }

        private static void StripClutterColliders(UnityEngine.SceneManagement.Scene scene, StringBuilder log)
        {
            int count = 0;
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                if (root.name != "Props" && root.name != "Arena") continue;
                foreach (Collider c in root.GetComponentsInChildren<Collider>(true))
                {
                    if (c == null || !IsClutter(c.transform)) continue;
                    Undo.DestroyObjectImmediate(c);
                    count++;
                }
            }
            log.Append("  clutter colliders removed: ").Append(count).Append('\n');
        }

        private static bool IsClutter(Transform t)
        {
            foreach (string n in ClutterNames)
                if (t.name.Contains(n)) return true;
            for (Transform p = t; p != null; p = p.parent)
                foreach (string category in ClutterCategories)
                    if (p.name == category) return true;
            return false;
        }

        // ------------------------------------------------------------------ entrances

        private static void PlaceEntrances(UnityEngine.SceneManagement.Scene scene, StringBuilder log)
        {
            var waves = Object.FindAnyObjectByType<WaveDirector>();
            GameObject spawnRoot = Root(scene, "SpawnPoints");
            GameObject arena = Root(scene, "Arena");
            if (waves == null || spawnRoot == null || arena == null)
            {
                log.Append("  entrances: WaveDirector, SpawnPoints or Arena missing\n");
                return;
            }
            Transform markerRoot = arena.transform.Find("Entrances");
            GameObject markerTemplate = FindChild(arena.transform, "Entrance_North");
            GameObject warningTemplate = FindChild(arena.transform, "Warning_North");

            var spawns = new Transform[Entrances.Length];
            var warnings = new GameObject[Entrances.Length];
            var keep = new HashSet<GameObject>();
            for (int i = 0; i < Entrances.Length; i++)
            {
                var (name, ex, ez) = Entrances[i];
                float x = ex * s_scale, z = ez * s_scale;
                float y = FloorHeight(x, z);
                Vector3 position = new Vector3(x, y, z);
                Vector3 outward = new Vector3(x, 0f, z).normalized;
                Quaternion facing = Quaternion.LookRotation(outward, Vector3.up);

                Transform spawn = spawnRoot.transform.Find("Spawn_" + name);
                if (spawn == null)
                {
                    spawn = new GameObject("Spawn_" + name).transform;
                    Undo.RegisterCreatedObjectUndo(spawn.gameObject, "Spawn");
                    spawn.SetParent(spawnRoot.transform, false);
                }
                spawn.SetPositionAndRotation(position, facing);
                spawns[i] = spawn;
                keep.Add(spawn.gameObject);

                if (markerTemplate != null)
                {
                    GameObject marker = FindChild(arena.transform, "Entrance_" + name) ?? Clone(markerTemplate, "Entrance_" + name, markerRoot);
                    marker.transform.SetPositionAndRotation(position + Vector3.up * 0.02f, facing);
                    keep.Add(marker);
                }
                if (warningTemplate != null)
                {
                    GameObject warning = FindChild(arena.transform, "Warning_" + name) ?? Clone(warningTemplate, "Warning_" + name, warningTemplate.transform.parent);
                    warning.transform.SetPositionAndRotation(position + Vector3.up * 0.03f, facing);
                    warning.SetActive(false);
                    warnings[i] = warning;
                    keep.Add(warning);
                }
                log.Append("  entrance ").Append(name).Append(" at ").Append(position.ToString("F2")).Append('\n');
            }

            // Old entrances that are no longer in the list (Spawn_West etc. are reused by name; others go).
            foreach (Transform t in spawnRoot.GetComponentsInChildren<Transform>(true))
                if (t != spawnRoot.transform && t.name.StartsWith("Spawn_") && !keep.Contains(t.gameObject)) Undo.DestroyObjectImmediate(t.gameObject);
            foreach (Transform t in arena.GetComponentsInChildren<Transform>(true))
                if ((t.name.StartsWith("Entrance_") || t.name.StartsWith("Warning_")) && !keep.Contains(t.gameObject))
                    Undo.DestroyObjectImmediate(t.gameObject);

            var so = new SerializedObject(waves);
            SerializedProperty spawnList = so.FindProperty("_spawnPoints");
            SerializedProperty warningList = so.FindProperty("_warnings");
            spawnList.arraySize = spawns.Length;
            warningList.arraySize = warnings.Length;
            for (int i = 0; i < spawns.Length; i++)
            {
                spawnList.GetArrayElementAtIndex(i).objectReferenceValue = spawns[i];
                warningList.GetArrayElementAtIndex(i).objectReferenceValue = warnings[i];
            }
            so.ApplyModifiedProperties();
        }

        private static GameObject Clone(GameObject template, string name, Transform parent)
        {
            GameObject copy = Object.Instantiate(template, parent);
            copy.name = name;
            Undo.RegisterCreatedObjectUndo(copy, "Entrance");
            return copy;
        }

        // ------------------------------------------------------------------ edge, camera, bake

        private static void BuildEdgeWalls(UnityEngine.SceneManagement.Scene scene, StringBuilder log)
        {
            GameObject old = Root(scene, "PlayBounds");
            if (old != null) Undo.DestroyObjectImmediate(old);
            var root = new GameObject("PlayBounds");
            Undo.RegisterCreatedObjectUndo(root, "PlayBounds");
            for (int i = 0; i < Outline.Length; i++)
            {
                Vector2 a = S(Outline[i]), b = S(Outline[(i + 1) % Outline.Length]);
                Vector2 along = b - a;
                Vector2 outward = new Vector2(-along.y, along.x).normalized; // clockwise outline: left normal points out
                Vector2 mid = (a + b) * 0.5f + outward * 0.5f;
                var wall = new GameObject("Edge_" + i);
                wall.transform.SetParent(root.transform, false);
                wall.transform.SetPositionAndRotation(new Vector3(mid.x, 1.5f, mid.y),
                    Quaternion.LookRotation(new Vector3(along.x, 0f, along.y), Vector3.up));
                var box = wall.AddComponent<BoxCollider>();
                box.size = new Vector3(1f, 3f, along.magnitude + 1f);
                wall.isStatic = true;
            }
            log.Append("  edge walls: ").Append(Outline.Length).Append('\n');
        }

        private static void SetCameraClamp(StringBuilder log)
        {
            var rig = Object.FindAnyObjectByType<PixelCameraRig>();
            if (rig == null) return;
            var so = new SerializedObject(rig);
            so.FindProperty("_boundsMin").vector2Value = S(CameraMin);
            so.FindProperty("_boundsMax").vector2Value = S(CameraMax);
            so.ApplyModifiedProperties();
            log.Append("  camera clamp ").Append(S(CameraMin)).Append(" .. ").Append(S(CameraMax)).Append('\n');

            var bot = Object.FindAnyObjectByType<RunBot>(FindObjectsInactive.Include);
            if (bot != null)
            {
                var botSo = new SerializedObject(bot);
                botSo.FindProperty("_arenaHalfExtents").vector2Value = new Vector2(13f, 11f) * s_scale;
                botSo.ApplyModifiedProperties();
            }
        }

        private static void Bake(UnityEngine.SceneManagement.Scene scene, StringBuilder log)
        {
            NavMeshSurface surface = Object.FindAnyObjectByType<NavMeshSurface>();
            if (surface == null)
            {
                log.Append("  no NavMeshSurface in the scene\n");
                return;
            }
            Undo.RecordObject(surface, "Bake volume");
            surface.collectObjects = CollectObjects.Volume;
            // The surface sits on the (scaled) Arena root, but its volume ignores the transform's scale: world metres,
            // offset from the root's position in its rotation.
            Vector3 centre = new Vector3(BakeCentre.x * s_scale, BakeCentre.y, BakeCentre.z * s_scale);
            Vector3 size = new Vector3(BakeSize.x * s_scale, BakeSize.y, BakeSize.z * s_scale);
            surface.center = Quaternion.Inverse(surface.transform.rotation) * (centre - surface.transform.position);
            surface.size = size;
            surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
            surface.minRegionArea = 2f;
            surface.BuildNavMesh();

            string path = System.IO.Path.GetDirectoryName(scene.path) + "/" + scene.name + "/NavMesh-" + surface.gameObject.name + ".asset";
            if (AssetDatabase.LoadAssetAtPath<NavMeshData>(path) != null) AssetDatabase.DeleteAsset(path);
            AssetDatabase.CreateAsset(surface.navMeshData, path);
            EditorUtility.SetDirty(surface);
            NavMeshTriangulation tri = NavMesh.CalculateTriangulation();
            log.Append("  NavMesh baked in ").Append(size).Append(" at ").Append(centre).Append(": ")
                .Append(tri.vertices.Length).Append(" vertices → ").Append(path).Append('\n');
        }

        // ------------------------------------------------------------------ helpers

        private static List<Renderer> MapRenderers(UnityEngine.SceneManagement.Scene scene)
        {
            var list = new List<Renderer>(1024);
            foreach (GameObject root in scene.GetRootGameObjects())
                if (root.name == "Props" || root.name == "Arena") list.AddRange(root.GetComponentsInChildren<Renderer>(true));
            return list;
        }

        /// <summary>The outermost prefab instance holding <paramref name="go"/> (a mesh child goes with its prop).</summary>
        private static GameObject Owner(GameObject go)
        {
            GameObject root = PrefabUtility.GetOutermostPrefabInstanceRoot(go);
            // Every map prop is its own small prefab; never climb to a big group by mistake.
            return root != null && root.GetComponentsInChildren<Renderer>(true).Length <= 8 ? root : go;
        }

        private static GameObject Root(UnityEngine.SceneManagement.Scene scene, string name)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
                if (root.name == name) return root;
            return null;
        }

        private static GameObject FindChild(Transform parent, string name)
        {
            foreach (Transform t in parent.GetComponentsInChildren<Transform>(true))
                if (t.name == name) return t.gameObject;
            return null;
        }

        /// <summary>Top of the floor at x/z: the highest hit under 1.2 m (above that it is a prop), else 0.</summary>
        private static float FloorHeight(float x, float z)
        {
            float best = float.NegativeInfinity;
            foreach (RaycastHit hit in Physics.RaycastAll(new Vector3(x, 6f, z), Vector3.down, 12f, ~0, QueryTriggerInteraction.Ignore))
                if (hit.collider.GetComponentInParent<Enemy>() == null && hit.collider.GetComponentInParent<PlayerController>() == null
                    && hit.point.y < 1.2f && hit.point.y > best)
                    best = hit.point.y;
            return float.IsNegativeInfinity(best) ? 0f : best;
        }
    }
}
