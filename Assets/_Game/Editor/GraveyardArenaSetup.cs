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
            ("South", 0.2f, -11.0f), ("West", -14.2f, -4.8f), ("WestNorth", -14.2f, 1.0f), ("NorthWest", -8.0f, 10.2f),
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
            OpenSpace(scene, log);
            StripClutterColliders(scene, log);
            PlaceEntrances(scene, log);
            BuildEdgeWalls(scene, log);
            SetCameraClamp(log);
            Bake(scene, log);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log(log.ToString());
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
                var (name, x, z) = Entrances[i];
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
                Vector2 a = Outline[i], b = Outline[(i + 1) % Outline.Length];
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
            so.FindProperty("_boundsMin").vector2Value = CameraMin;
            so.FindProperty("_boundsMax").vector2Value = CameraMax;
            so.ApplyModifiedProperties();
            log.Append("  camera clamp ").Append(CameraMin).Append(" .. ").Append(CameraMax).Append('\n');
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
            surface.center = BakeCentre - surface.transform.position;
            surface.size = BakeSize;
            surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
            surface.minRegionArea = 2f;
            surface.BuildNavMesh();

            string path = System.IO.Path.GetDirectoryName(scene.path) + "/" + scene.name + "/NavMesh-" + surface.gameObject.name + ".asset";
            if (AssetDatabase.LoadAssetAtPath<NavMeshData>(path) != null) AssetDatabase.DeleteAsset(path);
            AssetDatabase.CreateAsset(surface.navMeshData, path);
            EditorUtility.SetDirty(surface);
            NavMeshTriangulation tri = NavMesh.CalculateTriangulation();
            log.Append("  NavMesh baked in ").Append(BakeSize).Append(" at ").Append(BakeCentre).Append(": ")
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
