using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace DanceWithTheDevil.Editor
{
    /// <summary>
    /// Builds the Phase 1 placeholder content (flat materials, cube-part characters, the GDD §9 block arena)
    /// from the layout and palette tables, so the scene can be rebuilt instead of hand-edited.
    /// Final art replaces the prefabs and materials; the arena scene keeps its layout.
    /// </summary>
    public static class PlaceholderBuilder
    {
        private const string MaterialsFolder = "Assets/_Game/Art/Materials";
        private const string PrefabsFolder = "Assets/_Game/Prefabs";
        private const string ArenaScenePath = "Assets/_Game/Scenes/Arena.unity";
        private const string DefnePrefabPath = PrefabsFolder + "/char_defne_placeholder.prefab";
        private const string ChaserPrefabPath = PrefabsFolder + "/char_chaser_placeholder.prefab";
        private const string SimpleLitShader = "Universal Render Pipeline/Simple Lit";

        private struct Part
        {
            public string Name;
            public string Material;
            public Vector3 Center;
            public Vector3 Size;

            public Part(string name, string material, Vector3 center, Vector3 size)
            {
                Name = name;
                Material = material;
                Center = center;
                Size = size;
            }
        }

        // Palette (art-director placeholder spec). Emissive entries glow.
        private static readonly (string name, string hex, bool emissive)[] Palette =
        {
            ("env_floor", "#34313F", false),
            ("env_wall_outer", "#5C5873", false),
            ("env_wall_inner", "#6E6078", false),
            ("env_wall_edge", "#F2E6B8", false),
            ("env_entrance_plate", "#4E2A2E", false),
            ("env_entrance_stripe", "#8A4A3A", false),
            ("defne_hair", "#B4488F", false),
            ("defne_skin", "#D9A07A", false),
            ("defne_glasses", "#1A1A22", false),
            ("defne_overalls", "#2A7F8E", false),
            ("defne_overalls_shade", "#236D7A", false),
            ("defne_boots", "#2B2024", false),
            ("defne_turquoise", "#3FE0D0", false),
            ("chaser_body", "#C8323C", false),
            ("chaser_hips", "#7A1E2A", false),
            ("chaser_horns", "#F0D9A0", false),
            ("chaser_eyes", "#FFE14D", true),
            ("vfx_shot_player", "#5FFFF0", true),
            ("vfx_rocket", "#FFD08A", true),
            ("vfx_machete", "#F2F2F2", true),
        };

        // Unlit materials for the impact feedback router (shader, colour).
        private static readonly (string name, string shader)[] UnlitMaterials =
        {
            ("vfx_cubes", "Universal Render Pipeline/Particles/Unlit"),
            ("vfx_flash", "Universal Render Pipeline/Unlit"),
        };

        private static readonly Part[] DefneParts =
        {
            new Part("Boot_L", "defne_boots", new Vector3(-0.13f, 0.08f, 0.03f), new Vector3(0.24f, 0.16f, 0.34f)),
            new Part("Boot_R", "defne_boots", new Vector3(0.13f, 0.08f, 0.03f), new Vector3(0.24f, 0.16f, 0.34f)),
            new Part("Leg_L", "defne_overalls_shade", new Vector3(-0.13f, 0.41f, 0f), new Vector3(0.20f, 0.50f, 0.22f)),
            new Part("Leg_R", "defne_overalls_shade", new Vector3(0.13f, 0.41f, 0f), new Vector3(0.20f, 0.50f, 0.22f)),
            new Part("Torso", "defne_overalls", new Vector3(0f, 0.95f, 0f), new Vector3(0.56f, 0.58f, 0.34f)),
            new Part("Arm_L", "defne_skin", new Vector3(-0.37f, 0.96f, 0f), new Vector3(0.16f, 0.52f, 0.18f)),
            new Part("Arm_R", "defne_skin", new Vector3(0.37f, 0.96f, 0f), new Vector3(0.16f, 0.52f, 0.18f)),
            new Part("Head", "defne_skin", new Vector3(0f, 1.44f, 0f), new Vector3(0.44f, 0.40f, 0.40f)),
            new Part("Hair_Crown", "defne_hair", new Vector3(0f, 1.70f, -0.03f), new Vector3(0.50f, 0.20f, 0.44f)),
            new Part("Hair_Side_L", "defne_hair", new Vector3(-0.27f, 1.52f, -0.05f), new Vector3(0.16f, 0.34f, 0.34f)),
            new Part("Hair_Side_R", "defne_hair", new Vector3(0.28f, 1.49f, -0.04f), new Vector3(0.18f, 0.30f, 0.32f)),
            new Part("Hair_Back", "defne_hair", new Vector3(0f, 1.48f, -0.25f), new Vector3(0.48f, 0.40f, 0.18f)),
            new Part("Hair_Fringe", "defne_hair", new Vector3(-0.08f, 1.60f, 0.19f), new Vector3(0.24f, 0.12f, 0.10f)),
            new Part("Glasses_L", "defne_glasses", new Vector3(-0.10f, 1.46f, 0.21f), new Vector3(0.14f, 0.10f, 0.04f)),
            new Part("Glasses_R", "defne_glasses", new Vector3(0.10f, 1.46f, 0.21f), new Vector3(0.14f, 0.10f, 0.04f)),
            new Part("Detail_Bib", "defne_turquoise", new Vector3(0f, 1.10f, 0.18f), new Vector3(0.24f, 0.14f, 0.04f)),
        };

        private static readonly Part[] ChaserParts =
        {
            new Part("Hips", "chaser_hips", new Vector3(0f, 0.20f, 0f), new Vector3(0.50f, 0.40f, 0.30f)),
            new Part("Body", "chaser_body", new Vector3(0f, 0.68f, -0.02f), new Vector3(0.70f, 0.56f, 0.50f)),
            new Part("Head", "chaser_body", new Vector3(0f, 1.04f, 0.16f), new Vector3(0.42f, 0.32f, 0.36f)),
            new Part("Eyes", "chaser_eyes", new Vector3(0f, 1.07f, 0.35f), new Vector3(0.30f, 0.06f, 0.04f)),
            new Part("Horn_L", "chaser_horns", new Vector3(-0.16f, 1.31f, 0.12f), new Vector3(0.08f, 0.22f, 0.08f)),
            new Part("Horn_R", "chaser_horns", new Vector3(0.16f, 1.31f, 0.12f), new Vector3(0.08f, 0.22f, 0.08f)),
            new Part("Arm_L", "chaser_body", new Vector3(-0.45f, 0.62f, 0.06f), new Vector3(0.18f, 0.62f, 0.20f)),
            new Part("Arm_R", "chaser_body", new Vector3(0.45f, 0.62f, 0.06f), new Vector3(0.18f, 0.62f, 0.20f)),
        };

        // Arena layout (level-designer plan, GDD §9): play area x -16..16, z -14..14, floor top at y = 0.
        private static readonly Part[] ArenaBlocks =
        {
            new Part("Floor", "env_floor", new Vector3(0f, -0.05f, 0f), new Vector3(34f, 0.1f, 30f)),
            new Part("Wall_N", "env_wall_outer", new Vector3(0f, 1f, 14.5f), new Vector3(34f, 2f, 1f)),
            new Part("Wall_E", "env_wall_outer", new Vector3(16.5f, 1f, 0f), new Vector3(1f, 2f, 28f)),
            new Part("Wall_S", "env_wall_outer", new Vector3(0f, 0.5f, -14.5f), new Vector3(34f, 1f, 1f)),
            new Part("Wall_W", "env_wall_outer", new Vector3(-16.5f, 0.5f, 0f), new Vector3(1f, 1f, 28f)),
            new Part("L_NW_North", "env_wall_inner", new Vector3(-6f, 0.75f, 7.5f), new Vector3(8f, 1.5f, 3f)),
            new Part("L_NW_West", "env_wall_inner", new Vector3(-8.5f, 0.75f, 4f), new Vector3(3f, 1.5f, 4f)),
            new Part("L_SE_South", "env_wall_inner", new Vector3(6f, 0.75f, -7.5f), new Vector3(8f, 1.5f, 3f)),
            new Part("L_SE_East", "env_wall_inner", new Vector3(8.5f, 0.75f, -4f), new Vector3(3f, 1.5f, 4f)),
        };

        private static readonly (string name, Vector3 plate, Vector3 plateSize, Vector3 spawn)[] Entrances =
        {
            ("North", new Vector3(0f, 0.01f, 13.25f), new Vector3(3f, 0.02f, 1.5f), new Vector3(0f, 0f, 12f)),
            ("South", new Vector3(0f, 0.01f, -13.25f), new Vector3(3f, 0.02f, 1.5f), new Vector3(0f, 0f, -12f)),
            ("East", new Vector3(15.25f, 0.01f, 0f), new Vector3(1.5f, 0.02f, 3f), new Vector3(14f, 0f, 0f)),
            ("West", new Vector3(-15.25f, 0.01f, 0f), new Vector3(1.5f, 0.02f, 3f), new Vector3(-14f, 0f, 0f)),
        };

        [MenuItem("Dance/Placeholders/Rebuild All")]
        public static void RebuildAll()
        {
            BuildMaterials();
            BuildCharacterPrefabs();
            BuildArenaScene();
        }

        [MenuItem("Dance/Placeholders/Build Materials")]
        public static void BuildMaterials()
        {
            EnsureFolder(MaterialsFolder);
            Shader shader = Shader.Find(SimpleLitShader);
            if (shader == null)
            {
                Debug.LogError($"PlaceholderBuilder: shader '{SimpleLitShader}' not found.");
                return;
            }

            foreach (var (name, hex, emissive) in Palette)
            {
                string path = $"{MaterialsFolder}/mat_{name}_flat.mat";
                var material = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (material == null)
                {
                    material = new Material(shader);
                    AssetDatabase.CreateAsset(material, path);
                }

                material.shader = shader;
                ColorUtility.TryParseHtmlString(hex, out Color color);
                material.SetColor("_BaseColor", color);
                material.SetFloat("_Smoothness", 0f);
                material.SetColor("_SpecColor", Color.black);
                if (emissive)
                {
                    material.EnableKeyword("_EMISSION");
                    material.SetColor("_EmissionColor", color);
                    material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
                }
                else
                {
                    material.DisableKeyword("_EMISSION");
                    material.SetColor("_EmissionColor", Color.black);
                }

                EditorUtility.SetDirty(material);
            }

            foreach (var (name, shaderName) in UnlitMaterials)
            {
                string path = $"{MaterialsFolder}/mat_{name}_flat.mat";
                Shader unlit = Shader.Find(shaderName);
                if (unlit == null)
                {
                    Debug.LogError($"PlaceholderBuilder: shader '{shaderName}' not found.");
                    continue;
                }
                var material = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (material == null)
                {
                    material = new Material(unlit);
                    AssetDatabase.CreateAsset(material, path);
                }
                material.shader = unlit;
                material.SetColor("_BaseColor", Color.white);
                EditorUtility.SetDirty(material);
            }

            AssetDatabase.SaveAssets();
        }

        [MenuItem("Dance/Placeholders/Build Character Prefabs")]
        public static void BuildCharacterPrefabs()
        {
            EnsureFolder(PrefabsFolder);
            SaveCharacterPrefab("char_defne_placeholder", DefneParts, DefnePrefabPath);
            SaveCharacterPrefab("char_chaser_placeholder", ChaserParts, ChaserPrefabPath);
            AssetDatabase.SaveAssets();
        }

        [MenuItem("Dance/Placeholders/Build Arena Scene")]
        public static void BuildArenaScene()
        {
            EnsureFolder("Assets/_Game/Scenes");

            // Build in an additive scene so whatever the user has open stays untouched.
            // The arena is generated, so an open copy of it is discarded and rebuilt.
            Scene previousActive = SceneManager.GetActiveScene();
            Scene existing = SceneManager.GetSceneByPath(ArenaScenePath);
            bool reopenArena = existing.isLoaded;
            Scene scene;
            if (existing.isLoaded && SceneManager.sceneCount == 1)
            {
                scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                previousActive = default;
            }
            else
            {
                if (existing.isLoaded)
                {
                    if (previousActive == existing) previousActive = default;
                    EditorSceneManager.CloseScene(existing, true);
                }
                scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            }
            SceneManager.SetActiveScene(scene);

            ColorUtility.TryParseHtmlString("#3A3F5C", out Color ambient);
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = ambient;
            RenderSettings.skybox = null;

            var lightGo = new GameObject("Directional Light");
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            ColorUtility.TryParseHtmlString("#FFF2E0", out Color lightColor);
            light.color = lightColor;
            light.intensity = 1f;
            light.shadows = LightShadows.Soft;
            lightGo.transform.rotation = Quaternion.Euler(50f, 75f, 0f);

            var arena = new GameObject("Arena");
            arena.isStatic = true;
            foreach (Part block in ArenaBlocks)
            {
                GameObject go = CreateBlock(block, arena.transform, keepCollider: true);
                go.isStatic = true;
                if (block.Name.StartsWith("Wall") || block.Name.StartsWith("L_"))
                {
                    AddEdgeStrip(go.transform, block, arena.transform);
                }
            }

            // Near walls are 1 m tall so the camera sees past them, but bounce like the 2 m far walls.
            foreach (string near in new[] { "Wall_S", "Wall_W" })
            {
                var box = arena.transform.Find(near).GetComponent<BoxCollider>();
                box.size = new Vector3(1f, 2f, 1f);
                box.center = new Vector3(0f, 0.5f, 0f);
            }

            var entrances = new GameObject("Entrances");
            entrances.transform.SetParent(arena.transform, false);
            var spawns = new GameObject("SpawnPoints");
            foreach (var (name, plate, plateSize, spawn) in Entrances)
            {
                var plateGo = CreateBlock(new Part($"Entrance_{name}", "env_entrance_plate", plate, plateSize),
                    entrances.transform, keepCollider: false);
                plateGo.isStatic = true;
                AddEntranceStripe(plateGo.transform, plate, plateSize, entrances.transform);

                var spawnGo = new GameObject($"Spawn_{name}");
                spawnGo.transform.SetParent(spawns.transform, false);
                spawnGo.transform.position = spawn;
            }

            PixelCameraRig rig = BuildCameraRig();

            var defnePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(DefnePrefabPath);
            var player = (GameObject)PrefabUtility.InstantiatePrefab(defnePrefab, scene);
            player.name = "Player";
            player.transform.SetPositionAndRotation(Vector3.zero, Quaternion.Euler(0f, 180f, 0f));
            SetUpPlayer(player, rig);

            // Test targets only (🧪): shot, rocketed and slashed in Phase 2 until real enemies spawn in Phase 4.
            var chaserPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(ChaserPrefabPath);
            var dummies = new GameObject("TrainingDummies");
            Vector3[] dummyPositions = { new Vector3(3f, 0f, 5f), new Vector3(-4f, 0f, -4f), new Vector3(6f, 0f, 1f), new Vector3(4.5f, 0f, 5.5f) };
            for (int i = 0; i < dummyPositions.Length; i++)
            {
                var dummy = (GameObject)PrefabUtility.InstantiatePrefab(chaserPrefab, scene);
                dummy.name = $"Dummy_{i}";
                dummy.transform.SetParent(dummies.transform, false);
                dummy.transform.SetPositionAndRotation(dummyPositions[i], Quaternion.Euler(0f, 210f, 0f));
                var box = dummy.AddComponent<BoxCollider>();
                box.center = new Vector3(0f, 0.7f, 0f);
                box.size = new Vector3(0.9f, 1.4f, 0.6f);
                var health = dummy.AddComponent<Health>();
                var hso = new SerializedObject(health);
                hso.FindProperty("_maxHealth").floatValue = 30f;
                hso.FindProperty("_invulnerabilityAfterHit").floatValue = 0f;
                hso.ApplyModifiedPropertiesWithoutUndo();
                dummy.AddComponent<TrainingDummy>();
                dummy.AddComponent<ShatterOnDeath>();
            }

            var feel = new GameObject("ImpactFeedback");
            var router = feel.AddComponent<PixelImpactFeedback>();
            var rso = new SerializedObject(router);
            rso.FindProperty("_cameraRig").objectReferenceValue = rig;
            rso.FindProperty("_cubeMaterial").objectReferenceValue = LoadMaterial("vfx_cubes");
            rso.FindProperty("_flashMaterial").objectReferenceValue = LoadMaterial("vfx_flash");
            rso.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.SaveScene(scene, ArenaScenePath);
            if (!reopenArena && previousActive.IsValid() && previousActive.isLoaded)
            {
                SceneManager.SetActiveScene(previousActive);
                EditorSceneManager.CloseScene(scene, true);
            }

            AddArenaToBuildSettings();
            Debug.Log($"PlaceholderBuilder: arena scene saved to {ArenaScenePath}.");
        }

        // Player components live on the scene instance so the character prefab stays art-only.
        private static void SetUpPlayer(GameObject player, PixelCameraRig rig)
        {
            var controller = player.AddComponent<CharacterController>();
            controller.center = new Vector3(0f, 0.9f, 0f);
            controller.height = 1.8f;
            controller.radius = 0.35f;
            controller.skinWidth = 0.05f;
            controller.stepOffset = 0.3f;

            player.AddComponent<Health>();
            player.AddComponent<PlayerInputReader>();
            var pc = player.AddComponent<PlayerController>();
            var pso = new SerializedObject(pc);
            pso.FindProperty("_cameraRig").objectReferenceValue = rig;
            pso.ApplyModifiedPropertiesWithoutUndo();

            var muzzle = new GameObject("Muzzle").transform;
            muzzle.SetParent(player.transform, false);
            muzzle.localPosition = new Vector3(0.37f, 1.0f, 0.45f);

            var rifle = player.AddComponent<Rifle>();
            var rocket = player.AddComponent<RocketLauncher>();
            var machete = player.AddComponent<Machete>();
            player.AddComponent<WeaponHub>();

            // Swing visual: a pivot at chest height turned through the arc by the machete, blade reaching 2 m.
            var swing = new GameObject("MacheteSwing").transform;
            swing.SetParent(player.transform, false);
            swing.localPosition = new Vector3(0f, 1.0f, 0f);
            CreateBlock(new Part("Blade", "vfx_machete", new Vector3(0f, 0f, 1.1f), new Vector3(0.14f, 0.06f, 1.8f)), swing, keepCollider: false);
            var mso = new SerializedObject(machete);
            mso.FindProperty("_swingVisual").objectReferenceValue = swing;
            mso.ApplyModifiedPropertiesWithoutUndo();

            var riso = new SerializedObject(rifle);
            riso.FindProperty("_muzzle").objectReferenceValue = muzzle;
            riso.FindProperty("_projectileMaterial").objectReferenceValue = LoadMaterial("vfx_shot_player");
            riso.ApplyModifiedPropertiesWithoutUndo();
            var roso = new SerializedObject(rocket);
            roso.FindProperty("_muzzle").objectReferenceValue = muzzle;
            roso.FindProperty("_projectileMaterial").objectReferenceValue = LoadMaterial("vfx_rocket");
            roso.ApplyModifiedPropertiesWithoutUndo();
        }

        private static PixelCameraRig BuildCameraRig()
        {
            var rigGo = new GameObject("CameraRig");
            var cameraGo = new GameObject("GameCamera");
            cameraGo.transform.SetParent(rigGo.transform, false);

            var camera = cameraGo.AddComponent<Camera>();
            camera.orthographic = true;
            camera.nearClipPlane = 0.3f;
            camera.farClipPlane = 150f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            ColorUtility.TryParseHtmlString("#0E0B16", out Color clear);
            camera.backgroundColor = clear;
            camera.allowMSAA = false;
            camera.allowHDR = false;
            var data = cameraGo.AddComponent<UniversalAdditionalCameraData>();
            data.renderType = CameraRenderType.Base;
            data.renderPostProcessing = false;
            data.antialiasing = AntialiasingMode.None;
            cameraGo.AddComponent<AudioListener>();

            var rig = rigGo.AddComponent<PixelCameraRig>();
            var so = new SerializedObject(rig);
            so.FindProperty("_gameCamera").objectReferenceValue = camera;
            // GDD §9: a fixed camera shows the whole arena. Size 15 at 360 px = 12 px per metre.
            so.FindProperty("_orthographicSize").floatValue = 15f;
            so.FindProperty("_pitch").floatValue = 35f;
            so.FindProperty("_yaw").floatValue = 45f;
            so.FindProperty("_focusOffset").vector3Value = new Vector3(1f, 0f, 1f);
            so.ApplyModifiedPropertiesWithoutUndo();

            // Preview pose for edit mode; the rig drives the camera at runtime.
            Quaternion rotation = Quaternion.Euler(35f, 45f, 0f);
            cameraGo.transform.SetPositionAndRotation(new Vector3(1f, 0f, 1f) - rotation * Vector3.forward * 50f, rotation);
            camera.orthographicSize = 15f;
            return rig;
        }

        private static void SaveCharacterPrefab(string name, Part[] parts, string path)
        {
            var root = new GameObject(name);
            var model = new GameObject("Model");
            model.transform.SetParent(root.transform, false);
            foreach (Part part in parts)
            {
                CreateBlock(part, model.transform, keepCollider: false);
            }

            PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
        }

        private static GameObject CreateBlock(Part part, Transform parent, bool keepCollider)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = part.Name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = part.Center;
            go.transform.localScale = part.Size;
            go.GetComponent<MeshRenderer>().sharedMaterial = LoadMaterial(part.Material);
            if (!keepCollider) Object.DestroyImmediate(go.GetComponent<Collider>());
            return go;
        }

        // GDD §9: every bounce surface carries the same bright edge strip on its top.
        // Thin walls get a full cap; thick blocks get a rim so their top keeps the block colour.
        private static void AddEdgeStrip(Transform block, Part part, Transform parent)
        {
            const float height = 0.08f;
            const float overhang = 0.02f;
            const float rim = 0.3f;
            float y = part.Center.y + part.Size.y * 0.5f + height * 0.5f;
            float sx = part.Size.x + overhang;
            float sz = part.Size.z + overhang;
            Vector3 c = part.Center;

            var pieces = new List<(Vector3 center, Vector3 size)>();
            if (Mathf.Min(part.Size.x, part.Size.z) <= 1f)
            {
                pieces.Add((new Vector3(c.x, y, c.z), new Vector3(sx, height, sz)));
            }
            else
            {
                pieces.Add((new Vector3(c.x, y, c.z + (sz - rim) * 0.5f), new Vector3(sx, height, rim)));
                pieces.Add((new Vector3(c.x, y, c.z - (sz - rim) * 0.5f), new Vector3(sx, height, rim)));
                pieces.Add((new Vector3(c.x + (sx - rim) * 0.5f, y, c.z), new Vector3(rim, height, sz - 2f * rim)));
                pieces.Add((new Vector3(c.x - (sx - rim) * 0.5f, y, c.z), new Vector3(rim, height, sz - 2f * rim)));
            }

            for (int i = 0; i < pieces.Count; i++)
            {
                var strip = CreateBlock(new Part($"{part.Name}_Edge{i}", "env_wall_edge", pieces[i].center, pieces[i].size),
                    parent, keepCollider: false);
                strip.isStatic = true;
                strip.transform.SetParent(block, true);
            }
        }

        // Inward-pointing stripe so entrances read by shape, not only colour.
        private static void AddEntranceStripe(Transform plate, Vector3 plateCenter, Vector3 plateSize, Transform parent)
        {
            bool alongX = plateSize.x > plateSize.z;
            var size = alongX
                ? new Vector3(plateSize.x * 0.6f, 0.02f, plateSize.z * 0.25f)
                : new Vector3(plateSize.x * 0.25f, 0.02f, plateSize.z * 0.6f);
            var center = new Vector3(plateCenter.x, plateCenter.y + plateSize.y * 0.5f + size.y * 0.5f, plateCenter.z);
            var stripe = CreateBlock(new Part("Stripe", "env_entrance_stripe", center, size), parent, keepCollider: false);
            stripe.isStatic = true;
            stripe.transform.SetParent(plate, true);
        }

        private static Material LoadMaterial(string name)
        {
            return AssetDatabase.LoadAssetAtPath<Material>($"{MaterialsFolder}/mat_{name}_flat.mat");
        }

        private static void AddArenaToBuildSettings()
        {
            List<EditorBuildSettingsScene> scenes = EditorBuildSettings.scenes.ToList();
            if (scenes.Any(s => s.path == ArenaScenePath)) return;
            scenes.Insert(0, new EditorBuildSettingsScene(ArenaScenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = System.IO.Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, System.IO.Path.GetFileName(path));
        }
    }
}
