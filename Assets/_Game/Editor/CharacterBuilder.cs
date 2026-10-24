using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace DanceWithTheDevil.Editor
{
    /// <summary>
    /// Builds the imported Defne (Assets/Character: Tripo model with a Mixamo rig + the Basic Shooter Pack clips) and
    /// her weapons (Assets/Low Poly Weapons VOL.1: AK74 rifle, RPG7 launcher and its rocket) into
    /// <c>Assets/_Game/Prefabs/char_defne.prefab</c>, plus the modelled bullet and rocket projectiles.
    /// Menu <c>Dance/Character/Build Defne</c>; <see cref="PlaceholderBuilder.BuildArenaScene"/> uses the prefab when
    /// it exists (the cube placeholder stays the fallback). Layout of the prefab:
    /// char_defne › Model (container; Density and ModelPunch scale/move it) › Body (the FBX, Animator, scaled to
    /// <see cref="TargetHeight"/>) › … › RightHand › WeaponSocket › Rifle / Launcher (each +Z along the barrel, with a
    /// Muzzle child), Spine2 › LauncherBack, and ShatterProxy boxes on the bones (face loss and crumble).
    /// The hand/weapon offsets are constants below; tune them with <c>Dance/Character/Capture Previews</c>.
    /// </summary>
    public static class CharacterBuilder
    {
        private const string CharacterFolder = "Assets/Character";
        private const string ModelPath = CharacterFolder + "/female+soldier+3d+model/tripo_convert_55258fc8-a657-4678-b9ec-3829ec42687a.fbx";
        private const string BaseColorPath = CharacterFolder + "/female+soldier+3d+model/tripo_convert_55258fc8-a657-4678-b9ec-3829ec42687a.fbm/female_soldier_3d_model_basecolor.PNG";
        private const string ClipFolder = CharacterFolder + "/Basic Shooter Pack";
        private const string WeaponFolder = "Assets/Low Poly Weapons VOL.1";
        private const string RiflePrefabPath = WeaponFolder + "/Prefabs/AK74.prefab";
        private const string LauncherPrefabPath = WeaponFolder + "/Prefabs/RPG7.prefab";
        private const string WeaponTexturePath = WeaponFolder + "/Textures_Guns.png";

        private const string AnimationFolder = "Assets/_Game/Art/Animation";
        private const string MaterialsFolder = "Assets/_Game/Art/Materials";
        private const string PrefabsFolder = "Assets/_Game/Prefabs";
        public const string DefnePrefabPath = PrefabsFolder + "/char_defne.prefab";
        public const string BulletPrefabPath = PrefabsFolder + "/proj_bullet.prefab";
        public const string RocketPrefabPath = PrefabsFolder + "/proj_rocket.prefab";
        private const string ControllerPath = AnimationFolder + "/Defne.controller";
        private const string UpperBodyMaskPath = AnimationFolder + "/Defne_UpperBody.mask";

        /// <summary>Defne's standing height in metres (the cube placeholder was about 1.8 m with hair).</summary>
        private const float TargetHeight = 1.75f;

        // Weapon placement in the WeaponSocket (child of the right hand bone), tuned with the previews.
        private static readonly Vector3 SocketPosition = new Vector3(0f, 0.08f, 0.03f);
        private static readonly Vector3 SocketEuler = new Vector3(0f, 0f, 0f);
        private const float GripDrop = 0.03f;
        // The vertex-mass test puts the AK74's stock (few, large faces) forward: turn it round (checked in the previews).
        private const bool RifleFlip = true;        // grip a little below the hand bone (palm), metres
        private const float RifleLength = 0.82f;     // AK74 overall length in metres
        private const float LauncherLength = 0.95f;  // RPG7 tube length in metres
        private const float RocketLength = 0.42f;    // flying rocket length in metres

        private static readonly string[] LoopClips =
        {
            "rifle aiming idle", "rifle run", "run backwards", "strafe left", "strafe right", "strafe",
            "strafe (2)", "walking", "walking backwards",
        };

        private static readonly Color SkinColor = new Color(0.85f, 0.6f, 0.55f);
        private static readonly Color HairColor = new Color(0.2f, 0.12f, 0.12f);
        private static readonly Color OutfitColor = new Color(0.25f, 0.24f, 0.19f);
        private static readonly Color PouchColor = new Color(0.55f, 0.43f, 0.32f);
        private static readonly Color BootColor = new Color(0.22f, 0.15f, 0.14f);

        [MenuItem("Dance/Character/Build Defne")]
        public static void BuildAll()
        {
            EnsureFolder(AnimationFolder);
            EnsureFolder(MaterialsFolder);
            EnsureFolder(PrefabsFolder);

            ConfigureCharacterImport();
            ConfigureClipImports();
            Material bodyMaterial = BuildBodyMaterial();
            Material weaponMaterial = BuildWeaponMaterial();
            AnimatorController controller = BuildController();
            BuildDefnePrefab(controller, bodyMaterial, weaponMaterial);
            BuildBulletPrefab();
            BuildRocketPrefab(weaponMaterial);
            AssetDatabase.SaveAssets();
            Debug.Log("CharacterBuilder: Defne, her weapons and the projectiles are built.");
        }

        /// <summary>Logs each clip's root travel (average speed), to tell in-place clips from travelling ones.</summary>
        [MenuItem("Dance/Character/Log Clip Root Motion")]
        public static void LogClipRootMotion()
        {
            foreach (string path in ClipPaths())
            {
                AnimationClip clip = LoadClip(Path.GetFileNameWithoutExtension(path));
                if (clip == null) continue;
                Debug.Log($"CharacterBuilder clip '{clip.name}': length {clip.length:F2} s, averageSpeed {clip.averageSpeed}, " +
                          $"apparentSpeed {clip.apparentSpeed:F3}, humanMotion {clip.humanMotion}");
            }
        }

        /// <summary>Logs the top of the model's transform hierarchy (scales and positions) and the avatar's scale.</summary>
        [MenuItem("Dance/Character/Log Model Hierarchy")]
        public static void LogModelHierarchy()
        {
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
            LogTransform(model.transform, 0);
            var clipModel = AssetDatabase.LoadAssetAtPath<GameObject>(ClipFolder + "/rifle aiming idle.fbx");
            if (clipModel != null) LogTransform(clipModel.transform, 0);
            var clipAvatar = AssetDatabase.LoadAssetAtPath<Avatar>(ClipFolder + "/rifle aiming idle.fbx");
            Debug.Log($"CharacterBuilder clip avatar: {(clipAvatar != null ? clipAvatar.name : "none")}, human {clipAvatar != null && clipAvatar.isHuman}");
            var avatar = AssetDatabase.LoadAssetAtPath<Avatar>(ModelPath);
            Debug.Log($"CharacterBuilder avatar: {(avatar != null ? avatar.name : "none")}, human {avatar != null && avatar.isHuman}, " +
                      $"importer scale {((ModelImporter)AssetImporter.GetAtPath(ModelPath)).globalScale}");
        }

        /// <summary>Diagnostics: hips height from the raw FBX and from char_defne, both driven by the controller.</summary>
        public static void LogPoseExperiment()
        {
            var controller = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(ControllerPath);
            var raw = (GameObject)Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath));
            Animator a = raw.GetComponent<Animator>();
            if (a == null) a = raw.AddComponent<Animator>();
            a.avatar = AssetDatabase.LoadAssetAtPath<Avatar>(ModelPath);
            a.runtimeAnimatorController = controller;
            LogHips("raw fbx", a);
            Object.DestroyImmediate(raw);

            var prefab = (GameObject)Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(DefnePrefabPath));
            Animator b = prefab.GetComponentInChildren<Animator>();
            Debug.Log($"CharacterBuilder experiment prefab avatar {(b.avatar != null ? b.avatar.name : "none")}, " +
                      $"controller {(b.runtimeAnimatorController != null ? b.runtimeAnimatorController.name : "none")}, isHuman {b.isHuman}");
            LogHips("prefab", b);
            Object.DestroyImmediate(prefab);

            foreach (Object asset in AssetDatabase.LoadAllAssetsAtPath(ClipFolder + "/rifle aiming idle.fbx"))
                if (asset is AnimationClip clip)
                    Debug.Log($"CharacterBuilder experiment clip {clip.name}: humanMotion {clip.humanMotion}, legacy {clip.legacy}");
        }

        private static void LogHips(string label, Animator animator)
        {
            animator.Rebind();
            for (int i = 0; i < 12; i++) animator.Update(1f / 30f);
            Transform hips = animator.GetBoneTransform(HumanBodyBones.Hips);
            Debug.Log($"CharacterBuilder experiment {label}: isHuman {animator.isHuman}, hips " +
                      $"{(hips != null ? animator.transform.InverseTransformPoint(hips.position).ToString("F3") : "none")}");
        }

        private static void LogTransform(Transform t, int depth)
        {
            if (depth > 2) return;
            Debug.Log($"CharacterBuilder node {new string('-', depth * 2)}{t.name}: pos {t.localPosition:F3} rot {t.localEulerAngles:F1} scale {t.localScale:F3}");
            for (int i = 0; i < t.childCount && i < 6; i++) LogTransform(t.GetChild(i), depth + 1);
        }

        /// <summary>Batchmode entry: builds Defne and writes the preview PNGs.</summary>
        public static void BuildAndPreview()
        {
            BuildAll();
            CapturePreviews();
        }

        /// <summary>Batchmode entry: builds Defne, then rebuilds the arena scene with her.</summary>
        public static void BuildAllAndArena()
        {
            BuildAll();
            // Batchmode starts in an unsaved untitled scene, which blocks the builder's additive scene: open the arena.
            const string arenaPath = "Assets/_Game/Scenes/Arena.unity";
            if (Application.isBatchMode && AssetDatabase.LoadAssetAtPath<SceneAsset>(arenaPath) != null)
                EditorSceneManager.OpenScene(arenaPath, OpenSceneMode.Single);
            PlaceholderBuilder.BuildArenaScene();
        }

        // ------------------------------------------------------------------ imports

        private static void ConfigureCharacterImport()
        {
            var importer = (ModelImporter)AssetImporter.GetAtPath(ModelPath);
            bool changed = importer.animationType != ModelImporterAnimationType.Human
                           || importer.avatarSetup != ModelImporterAvatarSetup.CreateFromThisModel
                           || importer.importAnimation
                           || importer.materialImportMode != ModelImporterMaterialImportMode.None;
            importer.animationType = ModelImporterAnimationType.Human;
            importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            importer.importAnimation = false;
            importer.importCameras = false;
            importer.importLights = false;
            importer.materialImportMode = ModelImporterMaterialImportMode.None;
            if (changed) importer.SaveAndReimport();

            // Size in the importer, not on the GameObject: a humanoid Animator places the hips from the avatar's
            // scale, so a scaled Body transform leaves her floating metres above her feet.
            var instance = (GameObject)Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath));
            float height = WorldBounds(instance).size.y;
            Object.DestroyImmediate(instance);
            if (height > 0.01f && Mathf.Abs(height - TargetHeight) > TargetHeight * 0.02f)
            {
                importer.globalScale *= TargetHeight / height;
                importer.SaveAndReimport();
                Debug.Log($"CharacterBuilder: model height {height:F2} m → import scale {importer.globalScale:F4}.");
            }

            RefreshAvatarSkeleton(importer);
        }

        /// <summary>
        /// Rewrites the avatar's reference skeleton (the T-pose the humanoid retargeting measures against) from the
        /// model as it imports now. The stored one was in centimetres (hips at 85.9) while the model is in metres,
        /// so every clip put her hips about 86 m above her feet. Bone mapping (humanDescription.human) is kept.
        /// </summary>
        private static void RefreshAvatarSkeleton(ModelImporter importer)
        {
            var instance = (GameObject)Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath));
            try
            {
                Transform[] transforms = instance.GetComponentsInChildren<Transform>(true);
                var skeleton = new SkeletonBone[transforms.Length];
                for (int i = 0; i < transforms.Length; i++)
                {
                    Transform t = transforms[i];
                    skeleton[i] = new SkeletonBone
                    {
                        name = t.name,
                        position = t.localPosition,
                        rotation = t.localRotation,
                        scale = t.localScale,
                    };
                }

                HumanDescription description = importer.humanDescription;
                Vector3 storedHips = Vector3.zero;
                foreach (SkeletonBone bone in description.skeleton)
                    if (bone.name == "mixamorig:Hips") storedHips = bone.position;
                Transform hips = FindDeep(instance.transform, "mixamorig:Hips");
                if (hips != null && Mathf.Abs(storedHips.y - hips.localPosition.y) < 0.01f) return; // already current

                description.skeleton = skeleton;
                importer.humanDescription = description;
                importer.SaveAndReimport();
                Debug.Log($"CharacterBuilder: avatar skeleton refreshed (hips {storedHips.y:F2} → " +
                          $"{(hips != null ? hips.localPosition.y : 0f):F2}).");
            }
            finally
            {
                Object.DestroyImmediate(instance);
            }

            var texture = (TextureImporter)AssetImporter.GetAtPath(BaseColorPath);
            if (texture != null && texture.maxTextureSize != 1024)
            {
                texture.maxTextureSize = 1024; // the game camera draws Defne about 35 px tall
                texture.SaveAndReimport();
            }
        }

        private static void ConfigureClipImports()
        {
            foreach (string path in ClipPaths())
            {
                var importer = (ModelImporter)AssetImporter.GetAtPath(path);
                string clipName = Path.GetFileNameWithoutExtension(path);
                bool loop = System.Array.IndexOf(LoopClips, clipName) >= 0;

                importer.animationType = ModelImporterAnimationType.Human;
                // Each clip keeps its own Mixamo avatar: copying Defne's avatar onto the clip files (CopyFromOther)
                // misplaced the hips by metres, because the clip skeletons are in different units than her rig.
                importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
                importer.sourceAvatar = null;
                importer.importAnimation = true;
                importer.materialImportMode = ModelImporterMaterialImportMode.None;

                ModelImporterClipAnimation[] clips = importer.clipAnimations;
                if (clips == null || clips.Length == 0) clips = importer.defaultClipAnimations;
                for (int i = 0; i < clips.Length; i++)
                {
                    ModelImporterClipAnimation clip = clips[i];
                    clip.name = clipName;
                    clip.loopTime = loop;
                    clip.loopPose = loop;
                    // In place: the CharacterController moves the body, the clips only animate it.
                    clip.lockRootRotation = true;
                    clip.lockRootHeightY = true;
                    clip.lockRootPositionXZ = false; // not baked: applyRootMotion off discards any travel
                    clip.keepOriginalOrientation = true;
                    // Height from the feet: "Original" reads the clip file's raw hips height (centimetres in these
                    // Mixamo files), which put her hips 85 m up (DefneModelTests caught it).
                    clip.keepOriginalPositionY = false;
                    clip.heightFromFeet = true;
                    // Root XZ follows the centre of mass: the clips travel (Mixamo, not "in place"), and with
                    // applyRootMotion off the Animator drops that travel and keeps the body over the root.
                    clip.keepOriginalPositionXZ = false;
                    clips[i] = clip;
                }
                importer.clipAnimations = clips;
                importer.SaveAndReimport();
            }
        }

        private static IEnumerable<string> ClipPaths()
        {
            foreach (string guid in AssetDatabase.FindAssets("t:Model", new[] { ClipFolder }))
                yield return AssetDatabase.GUIDToAssetPath(guid);
        }

        private static AnimationClip LoadClip(string clipName)
        {
            string path = ClipFolder + "/" + clipName + ".fbx";
            foreach (Object asset in AssetDatabase.LoadAllAssetsAtPath(path))
                if (asset is AnimationClip clip && !clip.name.StartsWith("__preview__")) return clip;
            Debug.LogWarning($"CharacterBuilder: no clip in {path}.");
            return null;
        }

        // ------------------------------------------------------------------ materials

        private static Material BuildBodyMaterial()
        {
            string path = MaterialsFolder + "/mat_defne_skinned.mat";
            Shader shader = AssetDatabase.LoadAssetAtPath<Shader>("Assets/_Game/Art/Shaders/Char_Skinned.shader");
            Material material = LoadOrCreateMaterial(path, shader);
            material.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(BaseColorPath));
            EditorUtility.SetDirty(material);
            return material;
        }

        private static Material BuildWeaponMaterial()
        {
            // The pack's Gun_MAT uses the Built-in Standard shader (pink in URP): a URP copy with its atlas.
            string path = MaterialsFolder + "/mat_weapons_lowpoly.mat";
            Material material = LoadOrCreateMaterial(path, Shader.Find("Universal Render Pipeline/Simple Lit"));
            material.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(WeaponTexturePath));
            material.SetColor("_BaseColor", Color.white);
            EditorUtility.SetDirty(material);
            return material;
        }

        private static Material BuildBulletMaterial()
        {
            string path = MaterialsFolder + "/mat_bullet_brass.mat";
            Material material = LoadOrCreateMaterial(path, Shader.Find("Universal Render Pipeline/Simple Lit"));
            material.SetColor("_BaseColor", new Color(0.95f, 0.72f, 0.3f));
            material.EnableKeyword("_EMISSION");
            material.SetColor("_EmissionColor", new Color(1f, 0.62f, 0.2f) * 1.2f); // hot tip reads at night
            material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            EditorUtility.SetDirty(material);
            return material;
        }

        /// <summary>Transparent additive unlit material for tracer and smoke trails.</summary>
        private static Material BuildTrailMaterial(string name, bool additive)
        {
            string path = MaterialsFolder + "/" + name + ".mat";
            Material material = LoadOrCreateMaterial(path, Shader.Find("Universal Render Pipeline/Particles/Unlit"));
            material.SetFloat("_Surface", 1f);
            material.SetFloat("_Blend", additive ? 2f : 0f); // 2 = additive, 0 = alpha
            material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            material.SetFloat("_DstBlend", additive ? (float)BlendMode.One : (float)BlendMode.OneMinusSrcAlpha);
            material.SetFloat("_ZWrite", 0f);
            material.SetOverrideTag("RenderType", "Transparent");
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.SetFloat("_SrcBlendAlpha", 1f);
            material.SetFloat("_DstBlendAlpha", additive ? 1f : (float)BlendMode.OneMinusSrcAlpha);
            material.renderQueue = (int)RenderQueue.Transparent;
            material.SetColor("_BaseColor", Color.white);
            EditorUtility.SetDirty(material);
            return material;
        }

        private static Material LoadOrCreateMaterial(string path, Shader shader)
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, path);
            }
            else if (material.shader != shader)
            {
                material.shader = shader;
            }
            return material;
        }

        // ------------------------------------------------------------------ animator

        private static AnimatorController BuildController()
        {
            AvatarMask mask = BuildUpperBodyMask();
            if (AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath) != null)
                AssetDatabase.DeleteAsset(ControllerPath);
            AnimatorController controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
            controller.AddParameter("MoveX", AnimatorControllerParameterType.Float);
            controller.AddParameter("MoveZ", AnimatorControllerParameterType.Float);
            controller.AddParameter("Speed", AnimatorControllerParameterType.Float);
            controller.AddParameter("Hit", AnimatorControllerParameterType.Trigger);

            // Base layer: one 2D blend tree on the velocity in the body's frame (the body faces the aim).
            AnimatorStateMachine root = controller.layers[0].stateMachine;
            AnimatorState locomotion = controller.CreateBlendTreeInController("Locomotion", out BlendTree tree, 0);
            tree.blendType = BlendTreeType.FreeformDirectional2D;
            tree.blendParameter = "MoveX";
            tree.blendParameterY = "MoveZ";
            AddMotion(tree, "rifle aiming idle", 0f, 0f);
            AddMotion(tree, "walking", 0f, 0.45f);
            AddMotion(tree, "rifle run", 0f, 1f);
            AddMotion(tree, "walking backwards", 0f, -0.45f);
            AddMotion(tree, "run backwards", 0f, -1f);
            // Measured root travel (Log Clip Root Motion): "strafe (2)" runs left, "strafe" runs right,
            // "strafe left"/"strafe right" are the slower walking strafes.
            AddMotion(tree, "strafe left", -0.45f, 0f);
            AddMotion(tree, "strafe (2)", -1f, 0f);
            AddMotion(tree, "strafe right", 0.45f, 0f);
            AddMotion(tree, "strafe", 1f, 0f);
            root.defaultState = locomotion;

            // Hit layer (upper body only): plays the hit reaction over the legs' locomotion on the "Hit" trigger.
            var hitLayer = new AnimatorControllerLayer
            {
                name = "Hit",
                defaultWeight = 1f,
                avatarMask = mask,
                blendingMode = AnimatorLayerBlendingMode.Override,
                stateMachine = new AnimatorStateMachine { name = "Hit", hideFlags = HideFlags.HideInHierarchy },
            };
            AssetDatabase.AddObjectToAsset(hitLayer.stateMachine, controller);
            AnimatorState empty = hitLayer.stateMachine.AddState("Empty");
            AnimatorState hit = hitLayer.stateMachine.AddState("HitReaction");
            hit.motion = LoadClip("hit reaction");
            hit.speed = 1.6f; // a quick flinch, not a stagger
            hitLayer.stateMachine.defaultState = empty;

            AnimatorStateTransition toHit = hitLayer.stateMachine.AddAnyStateTransition(hit);
            toHit.AddCondition(AnimatorConditionMode.If, 0f, "Hit");
            toHit.duration = 0.05f;
            toHit.hasExitTime = false;
            toHit.interruptionSource = TransitionInterruptionSource.None;
            toHit.canTransitionToSelf = true;
            AnimatorStateTransition back = hit.AddTransition(empty);
            back.hasExitTime = true;
            back.exitTime = 0.7f;
            back.duration = 0.15f;
            controller.AddLayer(hitLayer);

            EditorUtility.SetDirty(controller);
            return controller;
        }

        private static void AddMotion(BlendTree tree, string clipName, float x, float z)
        {
            AnimationClip clip = LoadClip(clipName);
            if (clip != null) tree.AddChild(clip, new Vector2(x, z));
        }

        private static AvatarMask BuildUpperBodyMask()
        {
            var mask = AssetDatabase.LoadAssetAtPath<AvatarMask>(UpperBodyMaskPath);
            if (mask == null)
            {
                mask = new AvatarMask();
                AssetDatabase.CreateAsset(mask, UpperBodyMaskPath);
            }
            for (int i = 0; i < (int)AvatarMaskBodyPart.LastBodyPart; i++)
            {
                var part = (AvatarMaskBodyPart)i;
                bool upper = part == AvatarMaskBodyPart.Body || part == AvatarMaskBodyPart.Head
                             || part == AvatarMaskBodyPart.LeftArm || part == AvatarMaskBodyPart.RightArm
                             || part == AvatarMaskBodyPart.LeftFingers || part == AvatarMaskBodyPart.RightFingers;
                mask.SetHumanoidBodyPartActive(part, upper);
            }
            EditorUtility.SetDirty(mask);
            return mask;
        }

        // ------------------------------------------------------------------ prefab

        private static void BuildDefnePrefab(AnimatorController controller, Material bodyMaterial, Material weaponMaterial)
        {
            var root = new GameObject("char_defne");
            var model = new GameObject("Model");
            model.transform.SetParent(root.transform, false);

            var source = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
            var body = (GameObject)PrefabUtility.InstantiatePrefab(source);
            body.name = "Body";
            body.transform.SetParent(model.transform, false);

            foreach (SkinnedMeshRenderer smr in body.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                var materials = new Material[smr.sharedMaterials.Length];
                for (int i = 0; i < materials.Length; i++) materials[i] = bodyMaterial;
                smr.sharedMaterials = materials;
                smr.shadowCastingMode = ShadowCastingMode.On;
                smr.updateWhenOffscreen = true; // the follow camera is close; avoid popping on bound updates
            }

            // Unscaled, at the root: the import scale sets her height and the humanoid Animator puts her feet on
            // the root (see ConfigureCharacterImport).
            body.transform.localScale = Vector3.one;
            body.transform.localPosition = Vector3.zero;

            var animator = body.GetComponent<Animator>();
            if (animator == null) animator = body.AddComponent<Animator>();
            animator.runtimeAnimatorController = controller;
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            animator.updateMode = AnimatorUpdateMode.Normal;
            animator.Rebind();

            Transform rightHand = Bone(animator, HumanBodyBones.RightHand, "mixamorig:RightHand");
            Transform spine = Bone(animator, HumanBodyBones.UpperChest, "mixamorig:Spine2")
                              ?? Bone(animator, HumanBodyBones.Chest, "mixamorig:Spine1");

            var socket = new GameObject("WeaponSocket").transform;
            socket.SetParent(rightHand, false);
            SetWorldScaleOne(socket);
            socket.localPosition = Divide(SocketPosition, rightHand.lossyScale);
            socket.localRotation = Quaternion.Euler(SocketEuler);

            // The socket is fitted to the aiming pose at run time (CharacterAnimator, first animated frame): sampling
            // a clip here and saving that pose into the prefab left her hips 85 m up once the Animator ran.

            Transform rifle = BuildWeapon("Rifle", RiflePrefabPath, RifleLength, socket, weaponMaterial, "Muzzle", null, RifleFlip);
            Transform launcher = BuildWeapon("Launcher", LauncherPrefabPath, LauncherLength, socket, weaponMaterial, "Muzzle", null, false);
            launcher.gameObject.SetActive(false);
            if (spine != null)
            {
                var back = new GameObject("LauncherBackSocket").transform;
                back.SetParent(spine, false);
                SetWorldScaleOne(back);
                back.localPosition = Divide(new Vector3(0f, -0.08f, -0.17f), spine.lossyScale);
                back.localRotation = Quaternion.Euler(0f, 0f, 35f) * Quaternion.Euler(-90f, 0f, 0f);
                BuildWeapon("LauncherBack", LauncherPrefabPath, LauncherLength, back, weaponMaterial, null, null, false);
            }

            BuildProxies(animator);

            PrefabUtility.SaveAsPrefabAsset(root, DefnePrefabPath);
            Object.DestroyImmediate(root);
        }

        /// <summary>
        /// Instantiates a weapon prefab under <paramref name="parent"/>, re-oriented so its longest axis is +Z
        /// (muzzle end forward, the end farther from the mesh's centre of mass), scaled to <paramref name="length"/>,
        /// with a Muzzle child at the front end.
        /// </summary>
        private static Transform BuildWeapon(string name, string prefabPath, float length, Transform parent,
            Material material, string muzzleName, string hideChild, bool flip)
        {
            var holder = new GameObject(name).transform;
            holder.SetParent(parent, false);
            holder.localPosition = Vector3.zero;
            holder.localRotation = Quaternion.identity;
            holder.localScale = Vector3.one;

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            PrefabUtility.UnpackPrefabInstance(instance, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            instance.name = name + "_Mesh";
            Transform mesh = instance.transform;
            mesh.SetParent(holder, false);
            foreach (Renderer r in instance.GetComponentsInChildren<Renderer>(true))
            {
                r.sharedMaterial = material;
                r.shadowCastingMode = ShadowCastingMode.On;
            }
            foreach (Collider c in instance.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(c);
            if (!string.IsNullOrEmpty(hideChild))
            {
                Transform hidden = FindDeep(mesh, hideChild);
                if (hidden != null) hidden.gameObject.SetActive(false);
            }

            OrientAlongZ(holder, mesh, length, flip);

            if (!string.IsNullOrEmpty(muzzleName))
            {
                Bounds local = LocalBounds(holder, mesh.gameObject);
                var muzzle = new GameObject(muzzleName).transform;
                muzzle.SetParent(holder, false);
                muzzle.localPosition = new Vector3(local.center.x, local.center.y + local.extents.y * 0.35f, local.max.z);
                muzzle.localRotation = Quaternion.identity;
            }
            return holder;
        }

        /// <summary>Rotates and scales <paramref name="mesh"/> inside <paramref name="holder"/>: longest axis on +Z,
        /// the grip (the heavier half) behind the holder's origin, length as given.</summary>
        private static void OrientAlongZ(Transform holder, Transform mesh, float length, bool flip = false)
        {
            mesh.localRotation = Quaternion.identity;
            mesh.localScale = Vector3.one;
            mesh.localPosition = Vector3.zero;
            Bounds b = LocalBounds(holder, mesh.gameObject);
            Vector3 size = b.size;
            if (size.x >= size.y && size.x >= size.z) mesh.localRotation = Quaternion.Euler(0f, -90f, 0f);
            else if (size.y >= size.x && size.y >= size.z) mesh.localRotation = Quaternion.Euler(90f, 0f, 0f);

            b = LocalBounds(holder, mesh.gameObject);
            float scale = length / Mathf.Max(0.001f, b.size.z);
            mesh.localScale = Vector3.one * scale;
            b = LocalBounds(holder, mesh.gameObject);

            // Muzzle forward: the barrel end is the thinner one. Compare the vertex mass in each half.
            if ((MassCentreZ(holder, mesh.gameObject) > b.center.z) != flip)
            {
                mesh.localRotation = Quaternion.Euler(0f, 180f, 0f) * mesh.localRotation;
                b = LocalBounds(holder, mesh.gameObject);
            }
            // Grip at the origin: one third of the length from the back.
            mesh.localPosition -= new Vector3(b.center.x, b.center.y, b.min.z + length / 3f);
        }

        private static float MassCentreZ(Transform space, GameObject go)
        {
            float sum = 0f;
            int count = 0;
            foreach (MeshFilter filter in go.GetComponentsInChildren<MeshFilter>(true))
            {
                // The RPG7's loaded warhead is the heaviest end of the launcher but sits at its muzzle: ignore it.
                if (filter.sharedMesh == null || filter.name == "Rocket") continue;
                Matrix4x4 m = space.worldToLocalMatrix * filter.transform.localToWorldMatrix;
                Vector3[] vertices = filter.sharedMesh.vertices;
                for (int i = 0; i < vertices.Length; i++) sum += m.MultiplyPoint3x4(vertices[i]).z;
                count += vertices.Length;
            }
            return count > 0 ? sum / count : 0f;
        }

        private static void BuildProxies(Animator animator)
        {
            var proxyRoot = new List<(HumanBodyBones bone, string name, Vector3 size, Vector3 offset, Color color, bool face)>
            {
                (HumanBodyBones.Head, "Proxy_Head", new Vector3(0.22f, 0.24f, 0.24f), new Vector3(0f, 0.08f, 0f), HairColor, false),
                (HumanBodyBones.Head, "Face_Fringe", new Vector3(0.14f, 0.06f, 0.06f), new Vector3(0f, 0.15f, 0.1f), HairColor, true),
                (HumanBodyBones.Head, "Face_Eye_L", new Vector3(0.06f, 0.05f, 0.05f), new Vector3(-0.04f, 0.09f, 0.11f), SkinColor, true),
                (HumanBodyBones.Head, "Face_Eye_R", new Vector3(0.06f, 0.05f, 0.05f), new Vector3(0.04f, 0.09f, 0.11f), SkinColor, true),
                (HumanBodyBones.Head, "Face_Jaw", new Vector3(0.12f, 0.06f, 0.08f), new Vector3(0f, 0.02f, 0.09f), SkinColor, true),
                (HumanBodyBones.Chest, "Proxy_Chest", new Vector3(0.36f, 0.32f, 0.22f), new Vector3(0f, 0.1f, 0f), OutfitColor, false),
                (HumanBodyBones.Hips, "Proxy_Hips", new Vector3(0.34f, 0.2f, 0.22f), Vector3.zero, PouchColor, false),
                (HumanBodyBones.LeftUpperArm, "Proxy_UpperArm_L", new Vector3(0.1f, 0.26f, 0.1f), new Vector3(0f, 0.13f, 0f), OutfitColor, false),
                (HumanBodyBones.RightUpperArm, "Proxy_UpperArm_R", new Vector3(0.1f, 0.26f, 0.1f), new Vector3(0f, 0.13f, 0f), OutfitColor, false),
                (HumanBodyBones.LeftLowerArm, "Proxy_LowerArm_L", new Vector3(0.08f, 0.24f, 0.08f), new Vector3(0f, 0.12f, 0f), SkinColor, false),
                (HumanBodyBones.RightLowerArm, "Proxy_LowerArm_R", new Vector3(0.08f, 0.24f, 0.08f), new Vector3(0f, 0.12f, 0f), SkinColor, false),
                (HumanBodyBones.LeftUpperLeg, "Proxy_UpperLeg_L", new Vector3(0.14f, 0.4f, 0.14f), new Vector3(0f, 0.2f, 0f), OutfitColor, false),
                (HumanBodyBones.RightUpperLeg, "Proxy_UpperLeg_R", new Vector3(0.14f, 0.4f, 0.14f), new Vector3(0f, 0.2f, 0f), OutfitColor, false),
                (HumanBodyBones.LeftLowerLeg, "Proxy_LowerLeg_L", new Vector3(0.12f, 0.38f, 0.12f), new Vector3(0f, 0.19f, 0f), BootColor, false),
                (HumanBodyBones.RightLowerLeg, "Proxy_LowerLeg_R", new Vector3(0.12f, 0.38f, 0.12f), new Vector3(0f, 0.19f, 0f), BootColor, false),
            };
            foreach (var p in proxyRoot)
            {
                Transform bone = animator.GetBoneTransform(p.bone);
                if (bone == null)
                {
                    Debug.LogWarning($"CharacterBuilder: no {p.bone} bone for {p.name}.");
                    continue;
                }
                var proxy = new GameObject(p.name).transform;
                proxy.SetParent(bone, false);
                proxy.localRotation = Quaternion.identity;
                Vector3 lossy = bone.lossyScale;
                proxy.localPosition = Divide(p.offset, lossy);
                proxy.localScale = Divide(p.size, lossy);
                proxy.gameObject.AddComponent<ShatterProxy>().Configure(p.color, p.face);
            }
        }

        // ------------------------------------------------------------------ projectiles

        private static void BuildBulletPrefab()
        {
            var root = new GameObject("proj_bullet");
            var projectile = root.AddComponent<Projectile>();

            // A brass round, about 2.5 times real size so it reads at 20 px per metre.
            var visual = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            visual.name = "Visual";
            Object.DestroyImmediate(visual.GetComponent<Collider>());
            visual.transform.SetParent(root.transform, false);
            visual.transform.localRotation = Quaternion.Euler(90f, 0f, 0f); // capsule along +Z
            visual.transform.localScale = new Vector3(0.035f, 0.06f, 0.035f);
            var meshRenderer = visual.GetComponent<MeshRenderer>();
            meshRenderer.sharedMaterial = BuildBulletMaterial();
            meshRenderer.shadowCastingMode = ShadowCastingMode.Off;

            // Tracer streak behind it.
            var trail = root.AddComponent<TrailRenderer>();
            trail.sharedMaterial = BuildTrailMaterial("mat_bullet_tracer", additive: true);
            trail.time = 0.05f;
            trail.minVertexDistance = 0.05f;
            trail.widthCurve = new AnimationCurve(new Keyframe(0f, 0.05f), new Keyframe(1f, 0f));
            trail.colorGradient = Gradient(new Color(1f, 0.95f, 0.75f, 1f), new Color(1f, 0.55f, 0.15f, 0f));
            trail.shadowCastingMode = ShadowCastingMode.Off;
            trail.receiveShadows = false;
            trail.alignment = LineAlignment.View;

            var so = new SerializedObject(projectile);
            so.FindProperty("_visual").objectReferenceValue = visual.transform;
            so.FindProperty("_stretchVisual").boolValue = false;
            so.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAsset(root, BulletPrefabPath);
            Object.DestroyImmediate(root);
        }

        private static void BuildRocketPrefab(Material weaponMaterial)
        {
            var root = new GameObject("proj_rocket");
            var projectile = root.AddComponent<Projectile>();

            // The RPG7's own warhead mesh ("Rocket" child of the pack prefab), nose forward.
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(LauncherPrefabPath);
            Transform rocketSource = prefab != null ? FindDeep(prefab.transform, "Rocket") : null;
            var visual = new GameObject("Visual").transform;
            visual.SetParent(root.transform, false);
            if (rocketSource != null && rocketSource.TryGetComponent(out MeshFilter sourceFilter))
            {
                var meshGo = new GameObject("Rocket_Mesh");
                meshGo.transform.SetParent(visual, false);
                meshGo.AddComponent<MeshFilter>().sharedMesh = sourceFilter.sharedMesh;
                var mr = meshGo.AddComponent<MeshRenderer>();
                mr.sharedMaterial = weaponMaterial;
                mr.shadowCastingMode = ShadowCastingMode.Off;
                OrientAlongZ(visual, meshGo.transform, RocketLength);
                // OrientAlongZ puts the heavy end back (the warhead is the heavy end): turn the nose forward.
                meshGo.transform.localRotation = Quaternion.Euler(0f, 180f, 0f) * meshGo.transform.localRotation;
                Bounds b = LocalBounds(visual, meshGo);
                meshGo.transform.localPosition -= new Vector3(b.center.x, b.center.y, b.max.z);
            }

            var smoke = root.AddComponent<TrailRenderer>();
            smoke.sharedMaterial = BuildTrailMaterial("mat_rocket_smoke", additive: false);
            smoke.time = 0.45f;
            smoke.minVertexDistance = 0.1f;
            smoke.widthCurve = new AnimationCurve(new Keyframe(0f, 0.12f), new Keyframe(1f, 0.32f));
            smoke.colorGradient = Gradient(new Color(1f, 0.75f, 0.4f, 0.9f), new Color(0.35f, 0.33f, 0.35f, 0f));
            smoke.shadowCastingMode = ShadowCastingMode.Off;
            smoke.receiveShadows = false;
            smoke.alignment = LineAlignment.View;

            var so = new SerializedObject(projectile);
            so.FindProperty("_visual").objectReferenceValue = visual;
            so.FindProperty("_stretchVisual").boolValue = false;
            so.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAsset(root, RocketPrefabPath);
            Object.DestroyImmediate(root);
        }

        private static Gradient Gradient(Color start, Color end)
        {
            var gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(start, 0f), new GradientColorKey(end, 1f) },
                new[] { new GradientAlphaKey(start.a, 0f), new GradientAlphaKey(end.a, 1f) });
            return gradient;
        }

        // ------------------------------------------------------------------ previews

        /// <summary>
        /// Renders char_defne in a few poses and angles to PNGs under Logs/CharacterPreviews (git-ignored; Temp is wiped on quit) (batchmode-safe): the
        /// game's isometric view and a front/side close-up, standing and mid-run, rifle and launcher.
        /// </summary>
        [MenuItem("Dance/Character/Capture Previews")]
        public static void CapturePreviews()
        {
            string folder = Path.Combine(Directory.GetCurrentDirectory(), "Logs", "CharacterPreviews");
            Directory.CreateDirectory(folder);
            // Batchmode starts in an unsaved untitled scene, which blocks additive scenes: replace it there.
            Scene previous = SceneManager.GetActiveScene();
            NewSceneMode mode = Application.isBatchMode ? NewSceneMode.Single : NewSceneMode.Additive;
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, mode);
            SceneManager.SetActiveScene(scene);
            try
            {
                var lightGo = new GameObject("Light");
                var light = lightGo.AddComponent<Light>();
                light.type = LightType.Directional;
                light.shadows = LightShadows.Hard;
                lightGo.transform.rotation = Quaternion.Euler(50f, 75f, 0f);
                RenderSettings.ambientMode = AmbientMode.Flat;
                RenderSettings.ambientLight = new Color(0.35f, 0.33f, 0.4f);

                var floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
                floor.transform.localScale = Vector3.one * 0.5f;

                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(DefnePrefabPath);
                var defne = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
                var animator = defne.GetComponentInChildren<Animator>();
                Transform model = defne.transform.Find("Model");

                var camGo = new GameObject("PreviewCamera");
                var cam = camGo.AddComponent<Camera>();
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = new Color(0.08f, 0.07f, 0.1f);
                cam.orthographic = true;
                var rt = new RenderTexture(512, 512, 24) { antiAliasing = 1 };
                cam.targetTexture = rt;

                Capture(cam, animator, Vector2.zero, Quaternion.Euler(10f, 180f, 0f), 1.1f, folder, "idle_front");
                Capture(cam, animator, Vector2.zero, Quaternion.Euler(10f, 90f, 0f), 1.1f, folder, "idle_side");
                Capture(cam, animator, new Vector2(0f, 1f), Quaternion.Euler(35f, 225f, 0f), 1.4f, folder, "run_iso");
                Capture(cam, animator, new Vector2(-1f, 0f), Quaternion.Euler(35f, 225f, 0f), 1.4f, folder, "strafe_left_iso");
                Capture(cam, animator, new Vector2(0f, -1f), Quaternion.Euler(35f, 225f, 0f), 1.4f, folder, "back_iso");
                Capture(cam, animator, Vector2.zero, Quaternion.Euler(35f, 225f, 0f), 1.4f, folder, "idle_iso");

                Transform socket = FindDeep(model, "WeaponSocket");
                if (socket != null)
                {
                    PoseAnimator(animator, Vector2.zero);
                    // Side-on to the barrel, above it: the muzzle must be the end away from the hands.
                    Quaternion side = Quaternion.LookRotation(Vector3.Cross(Vector3.up, socket.forward).normalized + Vector3.down * 0.3f);
                    CaptureAt(cam, socket.position + socket.forward * 0.25f, side, 0.45f, folder, "rifle_closeup");
                }

                Transform launcher = FindDeep(model, "Launcher");
                Transform rifle = FindDeep(model, "Rifle");
                if (launcher != null && rifle != null)
                {
                    launcher.gameObject.SetActive(true);
                    rifle.gameObject.SetActive(false);
                    Capture(cam, animator, Vector2.zero, Quaternion.Euler(10f, 90f, 0f), 1.1f, folder, "launcher_side");
                }

                cam.targetTexture = null;
                Object.DestroyImmediate(rt);
                Debug.Log($"CharacterBuilder: previews written to {folder}");
            }
            finally
            {
                if (mode == NewSceneMode.Additive)
                {
                    if (previous.IsValid()) SceneManager.SetActiveScene(previous);
                    EditorSceneManager.CloseScene(scene, true);
                }
            }
        }


        /// <summary>
        /// Samples a clip onto the body without letting the clip's root position/rotation move the body object
        /// (the clips keep their root travel unbaked; in game applyRootMotion is off and discards it).
        /// </summary>
        private static void SamplePose(AnimationClip clip, GameObject body, float time)
        {
            Transform t = body.transform;
            Vector3 position = t.localPosition;
            Quaternion rotation = t.localRotation;
            clip.SampleAnimation(body, time);
            t.localPosition = position;
            t.localRotation = rotation;
        }

        private static void CaptureAt(Camera cam, Vector3 focus, Quaternion view, float size, string folder, string fileName)
        {
            cam.transform.rotation = view;
            cam.transform.position = focus - view * Vector3.forward * 10f;
            cam.orthographicSize = size;
            Save(cam, folder, fileName);
        }

        /// <summary>
        /// Runs the Animator as the game does (controller, parameters, Update steps), so the pose is what play mode
        /// shows. Edit-mode SampleAnimation misplaces the hips of a scaled humanoid, so it is not used for previews.
        /// </summary>
        private static void PoseAnimator(Animator animator, Vector2 move)
        {
            if (animator == null) return;
            animator.Rebind();
            animator.SetFloat("MoveX", move.x);
            animator.SetFloat("MoveZ", move.y);
            animator.SetFloat("Speed", move.magnitude);
            for (int i = 0; i < 12; i++) animator.Update(1f / 30f);
            animator.transform.localRotation = Quaternion.identity;
        }

        private static void Capture(Camera cam, Animator animator, Vector2 move,
            Quaternion view, float size, string folder, string fileName)
        {
            Vector3 bodyPosition = animator != null ? animator.transform.localPosition : Vector3.zero;
            PoseAnimator(animator, move);
            if (animator != null) animator.transform.localPosition = bodyPosition;
            Vector3 focus = new Vector3(0f, 1f, 0f);
            if (animator != null)
            {
                Transform hips = animator.GetBoneTransform(HumanBodyBones.Hips);
                Debug.Log($"CharacterBuilder preview {fileName}: body {animator.transform.position}, " +
                          $"hips {(hips != null ? hips.position.ToString() : "none")}");
            }
            cam.transform.rotation = view;
            cam.transform.position = focus - view * Vector3.forward * 10f;
            cam.orthographicSize = size;
            Save(cam, folder, fileName);
        }

        private static void Save(Camera cam, string folder, string fileName)
        {
            cam.Render();
            RenderTexture.active = cam.targetTexture;
            var texture = new Texture2D(cam.targetTexture.width, cam.targetTexture.height, TextureFormat.RGB24, false);
            texture.ReadPixels(new Rect(0, 0, texture.width, texture.height), 0, 0);
            texture.Apply();
            RenderTexture.active = null;
            File.WriteAllBytes(Path.Combine(folder, fileName + ".png"), texture.EncodeToPNG());
            Object.DestroyImmediate(texture);
        }

        // ------------------------------------------------------------------ helpers

        private static Transform Bone(Animator animator, HumanBodyBones bone, string fallbackName)
        {
            Transform t = animator != null && animator.avatar != null && animator.avatar.isHuman
                ? animator.GetBoneTransform(bone)
                : null;
            return t != null ? t : FindDeep(animator != null ? animator.transform : null, fallbackName);
        }

        private static void SetWorldScaleOne(Transform t)
        {
            Vector3 parent = t.parent != null ? t.parent.lossyScale : Vector3.one;
            t.localScale = Divide(Vector3.one, parent);
        }

        private static Vector3 Divide(Vector3 a, Vector3 b) =>
            new Vector3(a.x / NonZero(b.x), a.y / NonZero(b.y), a.z / NonZero(b.z));

        private static float NonZero(float v) => Mathf.Abs(v) < 1e-6f ? 1e-6f : v;

        private static Bounds WorldBounds(GameObject go)
        {
            Renderer[] renderers = go.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0) return new Bounds(go.transform.position, Vector3.zero);
            Bounds b = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) b.Encapsulate(renderers[i].bounds);
            return b;
        }

        /// <summary>Bounds of every mesh vertex under <paramref name="go"/> in <paramref name="space"/>'s local frame.</summary>
        private static Bounds LocalBounds(Transform space, GameObject go)
        {
            bool any = false;
            var b = new Bounds();
            foreach (MeshFilter filter in go.GetComponentsInChildren<MeshFilter>(true))
            {
                if (filter.sharedMesh == null) continue;
                Matrix4x4 m = space.worldToLocalMatrix * filter.transform.localToWorldMatrix;
                Bounds mb = filter.sharedMesh.bounds;
                Vector3 c = mb.center;
                Vector3 e = mb.extents;
                for (int i = 0; i < 8; i++)
                {
                    var corner = new Vector3(
                        c.x + ((i & 1) == 0 ? -e.x : e.x),
                        c.y + ((i & 2) == 0 ? -e.y : e.y),
                        c.z + ((i & 4) == 0 ? -e.z : e.z));
                    Vector3 p = m.MultiplyPoint3x4(corner);
                    if (!any)
                    {
                        b = new Bounds(p, Vector3.zero);
                        any = true;
                    }
                    else b.Encapsulate(p);
                }
            }
            return b;
        }

        private static Transform FindDeep(Transform parent, string childName)
        {
            if (parent == null) return null;
            for (int i = 0; i < parent.childCount; i++)
            {
                Transform child = parent.GetChild(i);
                if (child.name == childName) return child;
                Transform found = FindDeep(child, childName);
                if (found != null) return found;
            }
            return null;
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }
    }
}
