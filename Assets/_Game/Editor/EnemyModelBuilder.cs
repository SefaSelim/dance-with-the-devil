using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.Rendering;

namespace DanceWithTheDevil.Editor
{
    /// <summary>
    /// Puts the Synty POLYGON Dark Fantasy characters on the three enemies (GDD §6, §13), gameplay unchanged:
    /// Chaser = SM_Chr_Demon_Male_01, Runner = SM_Chr_Skeleton_01, Shooter = SM_Chr_Witch_Female_01.
    /// Per type it keeps an <see cref="EnemyAnimSet"/> (<c>Assets/_Game/Data/anim_enemy_{type}.asset</c>, swappable
    /// clip slots, first filled with the humanoid Basic Shooter Pack clips), builds an AnimatorController from it
    /// (<c>Art/Animation/Enemy_{Type}.controller</c>: 2D locomotion blend tree on MoveX/MoveZ, Tell/Attack/Death states
    /// only for filled slots, upper-body Hit layer) and a textured material from Char_Skinned with a strong identity
    /// tint and rim (<c>Art/Materials/mat_enemy_{type}_skinned.mat</c>).
    /// <see cref="PlaceholderBuilder"/>'s enemy prefab build calls <see cref="BuildBody"/> when the Synty pack is
    /// imported: enemy_x › Model (scale as before) › Body (the unpacked Synty character, other bodies stripped,
    /// Animator, root motion off, scaled to the old cube height) with yellow eye cubes and <see cref="ShatterProxy"/>
    /// boxes on the bones in the type colours. Without the pack the cube enemies stay.
    /// Menu <c>Dance/Enemies/Build Enemy Models</c> rebuilds the three enemy prefabs (same paths and GUIDs, so the
    /// arena's pools keep pointing at them).
    /// </summary>
    public static class EnemyModelBuilder
    {
        private const string SyntyCharacters = "Assets/PolygonDarkFantasy/Prefabs/Characters";
        private const string AtlasFallbackPath = "Assets/PolygonDarkFantasy/Textures/Alts/PolygonDarkFantasy_Texture_01_A.png";
        private const string ClipFolder = "Assets/Character/Basic Shooter Pack";
        private const string AnimationFolder = "Assets/_Game/Art/Animation";
        private const string MaterialsFolder = "Assets/_Game/Art/Materials";
        private const string DataFolder = "Assets/_Game/Data";
        private const string SkinnedShaderPath = "Assets/_Game/Art/Shaders/Char_Skinned.shader";
        private const string EyeMaterialPath = MaterialsFolder + "/mat_chaser_eyes_flat.mat";
        private const string UpperBodyMaskPath = AnimationFolder + "/Enemy_UpperBody.mask";

        // Proxy and eye sizes are given for a 1.8 m body and scale with the enemy's height.
        private const float ReferenceHeight = 1.8f;
        // Walk sits at 45% of the run in the blend tree (as Defne's).
        private const float WalkBlend = 0.45f;

        private static readonly Color EyeColor = Hex("#FFE14D");

        private readonly struct Spec
        {
            public readonly EnemyType Type;
            public readonly string Key;
            public readonly string Synty;
            public readonly Color Identity;
            public readonly float RunSpeed;
            public readonly float Playback;
            public readonly float TintStrength;

            public Spec(EnemyType type, string key, string synty, string hex, float runSpeed, float playback, float tint)
            {
                Type = type;
                Key = key;
                Synty = synty;
                Identity = Hex(hex);
                RunSpeed = runSpeed;
                Playback = playback;
                TintStrength = tint;
            }

            public string Title => char.ToUpperInvariant(Key[0]) + Key.Substring(1);
            public string SyntyPath => $"{SyntyCharacters}/{Synty}.prefab";
        }

        // Identity colours from GDD §13. Run speed (m/s at the full Run clip) and playback: the Chaser (4.2 m/s) runs,
        // the Shooter (3 m/s) walks briskly, the Runner (7.5 m/s) runs a faster clip.
        private static readonly Spec[] Specs =
        {
            new Spec(EnemyType.Chaser, "chaser", "SM_Chr_Demon_Male_01", "#E8343F", 4.2f, 1.1f, 0.6f),
            new Spec(EnemyType.Runner, "runner", "SM_Chr_Skeleton_01", "#FF6A45", 7.5f, 1.35f, 0.55f),
            new Spec(EnemyType.Shooter, "shooter", "SM_Chr_Witch_Female_01", "#C83C78", 5f, 1f, 0.6f),
        };

        [MenuItem("Dance/Enemies/Build Enemy Models")]
        public static void BuildAll()
        {
            PlaceholderBuilder.BuildEnemyPrefabs();
            AssetDatabase.SaveAssets();
            Debug.Log("EnemyModelBuilder: enemy prefabs rebuilt (Synty bodies where the pack is imported).");
        }

        /// <summary>Overwrites the three anim sets with the default Basic Shooter Pack clips, then rebuilds.</summary>
        [MenuItem("Dance/Enemies/Reset Anim Sets To Defaults")]
        public static void ResetAnimSets()
        {
            foreach (Spec spec in Specs) AssetDatabase.DeleteAsset(AnimSetPath(spec));
            BuildAll();
        }

        /// <summary>True when this type's Synty character and the skinned shader are in the project.</summary>
        public static bool HasModel(EnemyType type)
        {
            Spec spec = SpecFor(type);
            return AssetDatabase.LoadAssetAtPath<GameObject>(spec.SyntyPath) != null
                   && AssetDatabase.LoadAssetAtPath<Shader>(SkinnedShaderPath) != null;
        }

        /// <summary>
        /// Builds the Synty body under <paramref name="model"/> (already at its final scale), standing
        /// <paramref name="height"/> metres tall in the model's local units, with eyes and shatter proxies.
        /// </summary>
        public static void BuildBody(Transform model, EnemyType type, float height)
        {
            Spec spec = SpecFor(type);
            EnsureFolder(AnimationFolder);
            EnsureFolder(MaterialsFolder);
            EnsureFolder(DataFolder);

            EnemyAnimSet set = LoadOrCreateAnimSet(spec);
            AnimatorController controller = BuildController(spec, set);

            var source = AssetDatabase.LoadAssetAtPath<GameObject>(spec.SyntyPath);
            var body = (GameObject)PrefabUtility.InstantiatePrefab(source);
            PrefabUtility.UnpackPrefabInstance(body, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            body.name = "Body";
            body.transform.SetParent(model, false);
            body.transform.localPosition = Vector3.zero;
            body.transform.localRotation = Quaternion.identity;
            body.transform.localScale = Vector3.one;

            StripInactiveParts(body);
            Material material = BuildMaterial(spec, FindAtlas(body));
            foreach (Renderer r in body.GetComponentsInChildren<Renderer>(true))
            {
                var materials = new Material[r.sharedMaterials.Length];
                for (int i = 0; i < materials.Length; i++) materials[i] = material;
                r.sharedMaterials = materials;
                r.shadowCastingMode = ShadowCastingMode.On;
            }

            var animator = body.GetComponent<Animator>();
            if (animator == null) animator = body.AddComponent<Animator>();
            animator.runtimeAnimatorController = controller;
            animator.applyRootMotion = false; // the NavMeshAgent moves the enemy; clips only animate it
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate; // bones (proxies) stay current off-screen
            animator.updateMode = AnimatorUpdateMode.Normal; // scaled time: hitstop and pause freeze the pose
            if (animator.avatar == null || !animator.avatar.isHuman)
                Debug.LogWarning($"EnemyModelBuilder: {spec.Synty} has no humanoid avatar; the clips will not play.");

            // Scale the body to the old cube height (in world terms, Model scale included).
            float targetHeight = height * Mathf.Abs(model.lossyScale.y);
            List<Vector3> vertices = BakedVertices(body);
            Bounds bounds = WorldBounds(vertices);
            if (bounds.size.y > 0.01f)
            {
                body.transform.localScale = Vector3.one * (targetHeight / bounds.size.y);
                for (int i = 0; i < vertices.Count; i++)
                    vertices[i] = body.transform.position + (vertices[i] - body.transform.position) * (targetHeight / bounds.size.y);
                bounds = WorldBounds(vertices);
            }
            if (Mathf.Abs(bounds.min.y - model.position.y) > 0.05f * targetHeight)
                Debug.LogWarning($"EnemyModelBuilder: {spec.Synty} feet {bounds.min.y - model.position.y:F2} m off the root in the bind pose " +
                                 "(the humanoid Animator puts the feet down when it plays).");

            animator.Rebind();
            float k = targetHeight / ReferenceHeight;
            BuildProxies(animator, spec, k);
            BakeProxyPalettes(body, material); // before the eyes: they keep their flat yellow
            BuildEyes(animator, vertices, k);
        }

        /// <summary>Root settings that come from the anim set (after the root components exist).</summary>
        public static void ConfigureRoot(GameObject root, EnemyType type)
        {
            EnemyAnimSet set = LoadOrCreateAnimSet(SpecFor(type));
            var characterAnimator = root.GetComponent<CharacterAnimator>();
            if (characterAnimator == null) return;
            var so = new SerializedObject(characterAnimator);
            SerializedProperty run = so.FindProperty("_animRunSpeed");
            if (run != null) run.floatValue = set.RunSpeed;
            else Debug.LogWarning("EnemyModelBuilder: CharacterAnimator has no '_animRunSpeed'.");
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // ------------------------------------------------------------------ anim sets and controllers

        private static string AnimSetPath(Spec spec) => $"{DataFolder}/anim_enemy_{spec.Key}.asset";

        /// <summary>
        /// Loads the type's anim set, or creates it with the default clips. An existing set is never overwritten (the
        /// team's clips stay), only empty required slots (Idle, Walk, Run) are filled.
        /// </summary>
        private static EnemyAnimSet LoadOrCreateAnimSet(Spec spec)
        {
            string path = AnimSetPath(spec);
            var set = AssetDatabase.LoadAssetAtPath<EnemyAnimSet>(path);
            if (set == null)
            {
                EnsureFolder(DataFolder);
                set = ScriptableObject.CreateInstance<EnemyAnimSet>();
                set.WalkBack = LoadClip("walking backwards");
                set.RunBack = LoadClip("run backwards");
                // Measured root travel (CharacterBuilder): "strafe left/right" walk, "strafe (2)" runs left, "strafe" runs right.
                set.StrafeLeft = LoadClip("strafe left");
                set.StrafeRight = LoadClip("strafe right");
                set.StrafeLeftRun = LoadClip("strafe (2)");
                set.StrafeRightRun = LoadClip("strafe");
                set.Hit = LoadClip("hit reaction");
                // The pack has no melee or death clips: Chaser/Runner tell and lunge stay procedural.
                if (spec.Type == EnemyType.Shooter)
                {
                    set.Attack = LoadClip("firing rifle");
                    set.AttackSpeed = 1.5f;
                }
                set.RunSpeed = spec.RunSpeed;
                set.PlaybackSpeed = spec.Playback;
                AssetDatabase.CreateAsset(set, path);
            }
            if (set.Idle == null) set.Idle = LoadClip("rifle aiming idle");
            if (set.Walk == null) set.Walk = LoadClip("walking");
            if (set.Run == null) set.Run = LoadClip("rifle run");
            EditorUtility.SetDirty(set);
            return set;
        }

        private static AnimatorController BuildController(Spec spec, EnemyAnimSet set)
        {
            AvatarMask mask = BuildUpperBodyMask();
            string path = $"{AnimationFolder}/Enemy_{spec.Title}.controller";
            if (AssetDatabase.LoadAssetAtPath<AnimatorController>(path) != null) AssetDatabase.DeleteAsset(path);
            AnimatorController controller = AnimatorController.CreateAnimatorControllerAtPath(path);
            // Every parameter always exists, so CharacterAnimator and the brains never set a missing one.
            controller.AddParameter("MoveX", AnimatorControllerParameterType.Float);
            controller.AddParameter("MoveZ", AnimatorControllerParameterType.Float);
            controller.AddParameter("Speed", AnimatorControllerParameterType.Float);
            controller.AddParameter("Hit", AnimatorControllerParameterType.Trigger);
            controller.AddParameter(EnemyBodyAnimator.TellParameter, AnimatorControllerParameterType.Bool);
            controller.AddParameter(EnemyBodyAnimator.AttackParameter, AnimatorControllerParameterType.Trigger);
            controller.AddParameter(EnemyBodyAnimator.DieParameter, AnimatorControllerParameterType.Trigger);

            // Base layer: 2D blend tree on the velocity in the body's frame (CharacterAnimator feeds it).
            AnimatorStateMachine root = controller.layers[0].stateMachine;
            AnimatorState locomotion = controller.CreateBlendTreeInController("Locomotion", out BlendTree tree, 0);
            tree.blendType = BlendTreeType.FreeformDirectional2D;
            tree.blendParameter = "MoveX";
            tree.blendParameterY = "MoveZ";
            AddMotion(tree, set.Idle, 0f, 0f);
            AddMotion(tree, set.Walk, 0f, WalkBlend);
            AddMotion(tree, set.Run, 0f, 1f);
            AddMotion(tree, set.WalkBack, 0f, -WalkBlend);
            AddMotion(tree, set.RunBack, 0f, -1f);
            AddMotion(tree, set.StrafeLeft, -WalkBlend, 0f);
            AddMotion(tree, set.StrafeLeftRun, -1f, 0f);
            AddMotion(tree, set.StrafeRight, WalkBlend, 0f);
            AddMotion(tree, set.StrafeRightRun, 1f, 0f);
            locomotion.speed = set.PlaybackSpeed;
            root.defaultState = locomotion;

            if (set.Tell != null)
            {
                AnimatorState tell = root.AddState(EnemyBodyAnimator.TellState);
                tell.motion = set.Tell;
                AnimatorStateTransition to = root.AddAnyStateTransition(tell);
                to.AddCondition(AnimatorConditionMode.If, 0f, EnemyBodyAnimator.TellParameter);
                to.hasExitTime = false;
                to.duration = 0.08f;
                to.canTransitionToSelf = false;
                AnimatorStateTransition back = tell.AddTransition(locomotion);
                back.AddCondition(AnimatorConditionMode.IfNot, 0f, EnemyBodyAnimator.TellParameter);
                back.hasExitTime = false;
                back.duration = 0.1f;
            }

            if (set.Attack != null)
            {
                AnimatorState attack = root.AddState(EnemyBodyAnimator.AttackState);
                attack.motion = set.Attack;
                attack.speed = set.AttackSpeed;
                AnimatorStateTransition to = root.AddAnyStateTransition(attack);
                to.AddCondition(AnimatorConditionMode.If, 0f, EnemyBodyAnimator.AttackParameter);
                to.hasExitTime = false;
                to.duration = 0.05f;
                to.canTransitionToSelf = true;
                AnimatorStateTransition back = attack.AddTransition(locomotion);
                back.hasExitTime = true;
                back.exitTime = 0.9f;
                back.duration = 0.1f;
            }

            if (set.Death != null)
            {
                // Reserved: nothing fires "Die" yet (the body hides and bursts into cubes on death).
                AnimatorState death = root.AddState("Death");
                death.motion = set.Death;
                AnimatorStateTransition to = root.AddAnyStateTransition(death);
                to.AddCondition(AnimatorConditionMode.If, 0f, EnemyBodyAnimator.DieParameter);
                to.hasExitTime = false;
                to.duration = 0.05f;
                to.canTransitionToSelf = false;
            }

            if (set.Hit != null)
            {
                // Hit layer (upper body only): the hit reaction over the legs' locomotion on the "Hit" trigger.
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
                hit.motion = set.Hit;
                hit.speed = set.HitSpeed;
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
            }

            EditorUtility.SetDirty(controller);
            return controller;
        }

        private static void AddMotion(BlendTree tree, AnimationClip clip, float x, float z)
        {
            if (clip == null) return;
            if (!clip.humanMotion)
                Debug.LogWarning($"EnemyModelBuilder: clip '{clip.name}' is not Humanoid (set its Rig to Humanoid to retarget it).");
            tree.AddChild(clip, new Vector2(x, z));
        }

        private static AnimationClip LoadClip(string clipName)
        {
            string path = ClipFolder + "/" + clipName + ".fbx";
            foreach (Object asset in AssetDatabase.LoadAllAssetsAtPath(path))
                if (asset is AnimationClip clip && !clip.name.StartsWith("__preview__")) return clip;
            Debug.LogWarning($"EnemyModelBuilder: no clip in {path}.");
            return null;
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

        // ------------------------------------------------------------------ body

        /// <summary>Each Synty prefab carries every body of the pack with only its own active: drop the others.</summary>
        private static void StripInactiveParts(GameObject body)
        {
            var doomed = new List<GameObject>();
            foreach (Renderer r in body.GetComponentsInChildren<Renderer>(true))
            {
                if (r.gameObject.activeInHierarchy) continue;
                if (r.transform.childCount == 0) doomed.Add(r.gameObject);
                else Object.DestroyImmediate(r); // keeps any bones under it
            }
            foreach (GameObject go in doomed) Object.DestroyImmediate(go);
            foreach (Collider c in body.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(c);
            foreach (Rigidbody rb in body.GetComponentsInChildren<Rigidbody>(true)) Object.DestroyImmediate(rb);
        }

        private static Texture FindAtlas(GameObject body)
        {
            foreach (Renderer r in body.GetComponentsInChildren<Renderer>(true))
            {
                Material m = r.sharedMaterial;
                if (m == null) continue;
                Texture t = m.HasProperty("_BaseMap") ? m.GetTexture("_BaseMap") : m.mainTexture;
                if (t != null) return t;
            }
            return AssetDatabase.LoadAssetAtPath<Texture2D>(AtlasFallbackPath);
        }

        /// <summary>
        /// GDD §13: enemies are the most saturated, readable things on screen. The shared atlas is recoloured toward
        /// the type colour (texture detail kept) and lifted like Defne's (brightness 1.3, ambient 0.35, no rim: the
        /// 0.9 rim and 1.6 brightness made them look shiny, 2026-10-25).
        /// </summary>
        private static Material BuildMaterial(Spec spec, Texture atlas)
        {
            string path = $"{MaterialsFolder}/mat_enemy_{spec.Key}_skinned.mat";
            Shader shader = AssetDatabase.LoadAssetAtPath<Shader>(SkinnedShaderPath);
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
            material.SetTexture("_BaseMap", atlas);
            material.SetColor("_BaseColor", Color.white);
            material.SetFloat("_FlashAmount", 0f);
            material.SetFloat("_Saturation", 1f);
            material.SetColor("_TintColor", spec.Identity);
            material.SetFloat("_TintStrength", spec.TintStrength);
            material.SetFloat("_Brightness", 1.3f);
            material.SetFloat("_AmbientStrength", 0.35f);
            material.SetColor("_RimColor", Color.Lerp(spec.Identity, Color.white, 0.35f));
            material.SetFloat("_RimStrength", 0f);
            material.SetFloat("_RimThreshold", 0.55f);
            material.enableInstancing = true;
            EditorUtility.SetDirty(material);
            return material;
        }

        /// <summary>World-space vertices of every skinned and static mesh of the body in its stored (bind) pose.</summary>
        private static List<Vector3> BakedVertices(GameObject body)
        {
            var result = new List<Vector3>();
            var mesh = new Mesh();
            foreach (SkinnedMeshRenderer smr in body.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                if (smr.sharedMesh == null) continue;
                smr.BakeMesh(mesh, true);
                Matrix4x4 m = Matrix4x4.TRS(smr.transform.position, smr.transform.rotation, Vector3.one);
                foreach (Vector3 v in mesh.vertices) result.Add(m.MultiplyPoint3x4(v));
            }
            foreach (MeshFilter filter in body.GetComponentsInChildren<MeshFilter>(true))
            {
                if (filter.sharedMesh == null) continue;
                Matrix4x4 m = filter.transform.localToWorldMatrix;
                foreach (Vector3 v in filter.sharedMesh.vertices) result.Add(m.MultiplyPoint3x4(v));
            }
            Object.DestroyImmediate(mesh);
            return result;
        }

        private static Bounds WorldBounds(List<Vector3> points)
        {
            if (points.Count == 0) return new Bounds();
            var b = new Bounds(points[0], Vector3.zero);
            for (int i = 1; i < points.Count; i++) b.Encapsulate(points[i]);
            return b;
        }

        /// <summary>Flat facing of the bind pose (heels to toes), +Z when the toes are missing.</summary>
        private static Vector3 BodyForward(Animator animator)
        {
            Transform foot = animator.GetBoneTransform(HumanBodyBones.LeftFoot);
            Transform toes = animator.GetBoneTransform(HumanBodyBones.LeftToes);
            if (foot == null || toes == null) return Vector3.forward;
            Vector3 f = toes.position - foot.position;
            f.y = 0f;
            return f.sqrMagnitude > 1e-6f ? f.normalized : Vector3.forward;
        }

        /// <summary>
        /// Invisible boxes on the bones that the death burst reads (<see cref="ShatterOnDeath"/>): limbs span from a bone
        /// to the next, so they fit any rig's bone axes. Body in the identity colour, forearms and legs darker.
        /// </summary>
        private static void BuildProxies(Animator animator, Spec spec, float k)
        {
            Color main = spec.Identity;
            Color dark = Color.Lerp(spec.Identity, Color.black, 0.45f);
            Vector3 up = Vector3.up;

            Transform head = animator.GetBoneTransform(HumanBodyBones.Head);
            if (head != null) AddBox(head, "Proxy_Head", head.position + up * (0.1f * k), Quaternion.identity, new Vector3(0.24f, 0.24f, 0.24f) * k, main);

            Transform hips = animator.GetBoneTransform(HumanBodyBones.Hips);
            if (hips != null) AddBox(hips, "Proxy_Hips", hips.position, Quaternion.identity, new Vector3(0.34f, 0.2f, 0.22f) * k, dark);

            Transform chest = animator.GetBoneTransform(HumanBodyBones.Chest) ?? animator.GetBoneTransform(HumanBodyBones.Spine);
            Transform neck = animator.GetBoneTransform(HumanBodyBones.Neck) ?? head;
            AddSegment(chest, neck, "Proxy_Chest", new Vector2(0.36f, 0.22f) * k, main);

            AddSegment(animator, HumanBodyBones.LeftUpperArm, HumanBodyBones.LeftLowerArm, "Proxy_UpperArm_L", 0.1f * k, main);
            AddSegment(animator, HumanBodyBones.RightUpperArm, HumanBodyBones.RightLowerArm, "Proxy_UpperArm_R", 0.1f * k, main);
            AddSegment(animator, HumanBodyBones.LeftLowerArm, HumanBodyBones.LeftHand, "Proxy_LowerArm_L", 0.08f * k, dark);
            AddSegment(animator, HumanBodyBones.RightLowerArm, HumanBodyBones.RightHand, "Proxy_LowerArm_R", 0.08f * k, dark);
            AddSegment(animator, HumanBodyBones.LeftUpperLeg, HumanBodyBones.LeftLowerLeg, "Proxy_UpperLeg_L", 0.14f * k, main);
            AddSegment(animator, HumanBodyBones.RightUpperLeg, HumanBodyBones.RightLowerLeg, "Proxy_UpperLeg_R", 0.14f * k, main);
            AddSegment(animator, HumanBodyBones.LeftLowerLeg, HumanBodyBones.LeftFoot, "Proxy_LowerLeg_L", 0.12f * k, dark);
            AddSegment(animator, HumanBodyBones.RightLowerLeg, HumanBodyBones.RightFoot, "Proxy_LowerLeg_R", 0.12f * k, dark);
        }

        // ------------------------------------------------------------------ proxy palettes

        /// <summary>
        /// Gives every proxy box a small palette of the body's real on-screen colours (death pixels and hit chips):
        /// each mesh vertex (bind pose, world) goes to the nearest box, its atlas colour is read through a readable copy
        /// of the atlas (the Synty atlas is not readable at runtime) and recoloured exactly like Char_Skinned does
        /// (identity tint by luma, then brightness; in linear space), then the colours are bucketed and the 6 most
        /// common buckets (≥ 3%) kept with their counts as weights.
        /// </summary>
        private static void BakeProxyPalettes(GameObject body, Material material)
        {
            ShatterProxy[] proxies = body.GetComponentsInChildren<ShatterProxy>(true);
            Texture atlas = material != null && material.HasProperty("_BaseMap") ? material.GetTexture("_BaseMap") : null;
            if (proxies.Length == 0 || atlas == null)
            {
                Debug.LogWarning("EnemyModelBuilder: no proxies or atlas; death pixels use the flat proxy colours.");
                return;
            }
            Texture2D readable = ReadableCopy(atlas, 1024);
            if (readable == null) return;

            bool linear = QualitySettings.activeColorSpace == ColorSpace.Linear;
            Color tint = material.GetColor("_TintColor");
            if (linear) tint = tint.linear;
            float strength = material.GetFloat("_TintStrength");
            float brightness = material.GetFloat("_Brightness");

            var buckets = new Dictionary<int, Vector4>[proxies.Length]; // key → (sum r, g, b, count) in gamma space
            for (int i = 0; i < proxies.Length; i++) buckets[i] = new Dictionary<int, Vector4>();

            var mesh = new Mesh();
            var verts = new List<Vector3>();
            var uvs = new List<Vector2>();
            int samples = 0;
            foreach (Renderer r in body.GetComponentsInChildren<Renderer>(true))
            {
                Matrix4x4 m;
                if (r is SkinnedMeshRenderer smr && smr.sharedMesh != null)
                {
                    smr.BakeMesh(mesh, true);
                    m = Matrix4x4.TRS(smr.transform.position, smr.transform.rotation, Vector3.one);
                }
                else if (r.TryGetComponent(out MeshFilter filter) && filter.sharedMesh != null)
                {
                    mesh.Clear();
                    mesh.vertices = filter.sharedMesh.vertices;
                    mesh.uv = filter.sharedMesh.uv;
                    m = filter.transform.localToWorldMatrix;
                }
                else continue;

                mesh.GetVertices(verts);
                mesh.GetUVs(0, uvs);
                if (uvs.Count != verts.Count) continue;
                for (int v = 0; v < verts.Count; v++)
                {
                    int proxy = NearestProxy(proxies, m.MultiplyPoint3x4(verts[v]));
                    if (proxy < 0) continue;
                    Color c = readable.GetPixelBilinear(Mathf.Repeat(uvs[v].x, 1f), Mathf.Repeat(uvs[v].y, 1f));
                    Color shown = Recolour(linear ? c.linear : c, tint, strength, brightness);
                    if (linear) shown = shown.gamma;
                    shown.r = Mathf.Clamp01(shown.r);
                    shown.g = Mathf.Clamp01(shown.g);
                    shown.b = Mathf.Clamp01(shown.b);
                    int key = Mathf.RoundToInt(shown.r * 7f) * 64 + Mathf.RoundToInt(shown.g * 7f) * 8 + Mathf.RoundToInt(shown.b * 7f);
                    buckets[proxy].TryGetValue(key, out Vector4 sum);
                    buckets[proxy][key] = sum + new Vector4(shown.r, shown.g, shown.b, 1f);
                    samples++;
                }
            }
            Object.DestroyImmediate(mesh);
            Object.DestroyImmediate(readable);

            int baked = 0;
            for (int i = 0; i < proxies.Length; i++)
            {
                var list = new List<Vector4>(buckets[i].Values);
                if (list.Count == 0) continue;
                list.Sort((a, b) => b.w.CompareTo(a.w));
                float total = 0f;
                foreach (Vector4 e in list) total += e.w;
                var colors = new List<Color>();
                var weights = new List<float>();
                for (int j = 0; j < list.Count && colors.Count < ShatterProxy.MaxPalette; j++)
                {
                    Vector4 e = list[j];
                    if (colors.Count > 0 && e.w < total * 0.03f) break;
                    colors.Add(new Color(e.x / e.w, e.y / e.w, e.z / e.w, 1f));
                    weights.Add(e.w);
                }
                proxies[i].SetPalette(colors.ToArray(), weights.ToArray());
                baked++;
            }
            Debug.Log($"EnemyModelBuilder: baked palettes for {baked}/{proxies.Length} proxies of {body.transform.root.name} from {samples} vertices.");
        }

        // Char_Skinned's albedo path: tinted = tint × saturate(luma × 1.6 + 0.15); lerp by strength; × brightness.
        private static Color Recolour(Color albedo, Color tint, float strength, float brightness)
        {
            float grey = albedo.r * 0.299f + albedo.g * 0.587f + albedo.b * 0.114f;
            Color tinted = tint * Mathf.Clamp01(grey * 1.6f + 0.15f);
            Color c = Color.Lerp(albedo, tinted, strength) * brightness;
            c.a = 1f;
            return c;
        }

        private static int NearestProxy(ShatterProxy[] proxies, Vector3 world)
        {
            int best = -1;
            float bestDistance = float.PositiveInfinity;
            for (int i = 0; i < proxies.Length; i++)
            {
                Transform t = proxies[i].transform;
                Vector3 local = t.InverseTransformPoint(world);
                var clamped = new Vector3(Mathf.Clamp(local.x, -0.5f, 0.5f), Mathf.Clamp(local.y, -0.5f, 0.5f), Mathf.Clamp(local.z, -0.5f, 0.5f));
                float d = (t.TransformPoint(clamped) - world).sqrMagnitude;
                if (d < bestDistance)
                {
                    bestDistance = d;
                    best = i;
                }
            }
            return best;
        }

        /// <summary>A CPU-readable copy of a (non-readable) texture through a blit (≤ <paramref name="maxSize"/>).</summary>
        private static Texture2D ReadableCopy(Texture source, int maxSize)
        {
            int w = Mathf.Min(source.width, maxSize), h = Mathf.Min(source.height, maxSize);
            RenderTexture rt = RenderTexture.GetTemporary(w, h, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            RenderTexture previous = RenderTexture.active;
            Graphics.Blit(source, rt);
            RenderTexture.active = rt;
            var copy = new Texture2D(w, h, TextureFormat.RGBA32, false, false);
            copy.ReadPixels(new Rect(0, 0, w, h), 0, 0);
            copy.Apply(false);
            RenderTexture.active = previous;
            RenderTexture.ReleaseTemporary(rt);

            // A blit without a GPU (batch mode -nographics) gives black: fall back to flat colours.
            Color centre = copy.GetPixel(w / 2, h / 2);
            Color corner = copy.GetPixel(w / 4, h / 4);
            if (centre.maxColorComponent <= 0f && corner.maxColorComponent <= 0f)
            {
                Debug.LogWarning("EnemyModelBuilder: atlas copy is black (no GPU?); death pixels use the flat proxy colours.");
                Object.DestroyImmediate(copy);
                return null;
            }
            return copy;
        }

        private static void AddSegment(Animator animator, HumanBodyBones from, HumanBodyBones to, string name, float width, Color color)
        {
            AddSegment(animator.GetBoneTransform(from), animator.GetBoneTransform(to), name, new Vector2(width, width), color);
        }

        private static void AddSegment(Transform from, Transform to, string name, Vector2 width, Color color)
        {
            if (from == null || to == null)
            {
                Debug.LogWarning($"EnemyModelBuilder: missing bone for {name}.");
                return;
            }
            Vector3 span = to.position - from.position;
            if (span.sqrMagnitude < 1e-6f) return;
            AddBox(from, name, (from.position + to.position) * 0.5f, Quaternion.FromToRotation(Vector3.up, span),
                new Vector3(width.x, span.magnitude, width.y), color);
        }

        private static void AddBox(Transform bone, string name, Vector3 position, Quaternion rotation, Vector3 size, Color color)
        {
            var proxy = new GameObject(name).transform;
            proxy.SetParent(bone, false);
            proxy.SetPositionAndRotation(position, rotation);
            proxy.localScale = Divide(size, bone.lossyScale);
            proxy.gameObject.AddComponent<ShatterProxy>().Configure(color, false);
        }

        /// <summary>
        /// Two small yellow eye cubes on the face (GDD §13: yellow eyes on every enemy), on the head bone. The face front
        /// is read from the mesh at eye height, so they sit on the skin for each character. They flash and hide with the
        /// body (Enemy) and burst yellow (ShatterProxy).
        /// </summary>
        private static void BuildEyes(Animator animator, List<Vector3> vertices, float k)
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(EyeMaterialPath);
            Transform head = animator.GetBoneTransform(HumanBodyBones.Head);
            if (material == null || head == null)
            {
                Debug.LogWarning("EnemyModelBuilder: no eye material or head bone; enemy built without eye cubes.");
                return;
            }

            Vector3 forward = BodyForward(animator);
            Vector3 right = Vector3.Cross(Vector3.up, forward);
            float eyeY = head.position.y + 0.09f * k;
            float front = float.NegativeInfinity;
            for (int i = 0; i < vertices.Count; i++)
            {
                Vector3 d = vertices[i] - head.position;
                if (Mathf.Abs(vertices[i].y - eyeY) > 0.03f * k || Mathf.Abs(Vector3.Dot(d, right)) > 0.07f * k) continue;
                float along = Vector3.Dot(d, forward);
                if (along > 0.25f * k) continue; // a hat brim or a hand, not the face
                front = Mathf.Max(front, along);
            }
            if (float.IsNegativeInfinity(front))
            {
                front = 0.1f * k;
                Debug.LogWarning($"EnemyModelBuilder: no face vertices at eye height on {animator.name}; eyes placed at a default depth.");
            }

            for (int side = -1; side <= 1; side += 2)
            {
                GameObject eye = GameObject.CreatePrimitive(PrimitiveType.Cube);
                Object.DestroyImmediate(eye.GetComponent<Collider>());
                eye.name = side < 0 ? "Eye_L" : "Eye_R";
                eye.transform.SetParent(head, false);
                Vector3 position = new Vector3(head.position.x, eyeY, head.position.z)
                                   + right * (side * 0.035f * k) + forward * (front + 0.005f * k);
                eye.transform.SetPositionAndRotation(position, Quaternion.LookRotation(forward, Vector3.up));
                eye.transform.localScale = Divide(new Vector3(0.045f, 0.03f, 0.02f) * k, head.lossyScale);
                var meshRenderer = eye.GetComponent<MeshRenderer>();
                meshRenderer.sharedMaterial = material;
                meshRenderer.shadowCastingMode = ShadowCastingMode.Off;
                eye.AddComponent<ShatterProxy>().Configure(EyeColor, false);
            }
        }

        // ------------------------------------------------------------------ helpers

        private static Spec SpecFor(EnemyType type)
        {
            foreach (Spec spec in Specs)
                if (spec.Type == type) return spec;
            return Specs[0];
        }

        private static Vector3 Divide(Vector3 a, Vector3 b)
        {
            return new Vector3(a.x / NonZero(b.x), a.y / NonZero(b.y), a.z / NonZero(b.z));
        }

        private static float NonZero(float v) => Mathf.Abs(v) < 1e-6f ? 1e-6f : v;

        private static Color Hex(string hex) => ColorUtility.TryParseHtmlString(hex, out Color c) ? c : Color.magenta;

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = System.IO.Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, System.IO.Path.GetFileName(path));
        }
    }
}
