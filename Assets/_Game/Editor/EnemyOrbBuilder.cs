using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace DanceWithTheDevil.Editor
{
    /// <summary>
    /// Builds the Shooter's (Nişancı) projectile prefab <c>proj_enemy_orb</c>: a hot glowing orb instead of the runtime
    /// cube. Layers (all additive, VFX_ParticleUnlit, HDR through _Intensity so the Arena Bloom (threshold 1) catches it):
    /// a white-hot pale-orange core (~0.33 m seen), an orange flickering halo (~0.75 m), a 0.3 s orange → deep red
    /// tapering trail (~4 m long at 14 m/s) and small embers left behind in world space. Only visuals: the hit radius,
    /// speed and range still come from <see cref="ShooterBrain"/>'s launch. Menu <c>Dance/VFX/Build Enemy Orb</c>;
    /// <c>Dance/Enemies/Build Enemy Models</c> builds it when missing and assigns it to the shooter prefab.
    /// Values are starting points for the 960×540 pixel camera (ortho 6.5, ~0.024 m per texel): tune by eye at
    /// Time.timeScale 0.1 from the gameplay view.
    /// </summary>
    public static class EnemyOrbBuilder
    {
        public const string PrefabPath = "Assets/_Game/Prefabs/proj_enemy_orb.prefab";
        private const string MaterialFolder = "Assets/_Game/Art/Materials/VFX";
        private const string TextureFolder = "Assets/_Game/ThirdParty/KenneyParticlePack";
        private const string ShaderPath = "Assets/_Game/Art/Shaders/VFX_ParticleUnlit.shader";

        // Enemy shot palette (GDD: red/orange belong to enemies; shot #FF7A1A), linear-ish colours.
        private static readonly Color CoreHot = new Color(1f, 0.85f, 0.6f);
        private static readonly Color ShotOrange = new Color(1f, 0.478f, 0.102f); // #FF7A1A
        private static readonly Color Yellow = new Color(1f, 0.72f, 0.3f);
        private static readonly Color DeepRed = new Color(0.55f, 0.07f, 0.04f);

        [MenuItem("Dance/VFX/Build Enemy Orb")]
        public static void BuildMenu()
        {
            Build();
            AssetDatabase.SaveAssets();
            Debug.Log("EnemyOrbBuilder: " + PrefabPath + " built.");
        }

        /// <summary>The orb prefab, built first if it does not exist yet.</summary>
        public static Projectile LoadOrBuild()
        {
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (existing != null && existing.TryGetComponent(out Projectile projectile)) return projectile;
            return Build();
        }

        public static Projectile Build()
        {
            EnsureFolder(MaterialFolder);
            EnsureFolder(Path.GetDirectoryName(PrefabPath).Replace('\\', '/'));

            var root = new GameObject("proj_enemy_orb");
            var projectile = root.AddComponent<Projectile>();
            root.AddComponent<ProjectileParticles>();

            var visual = new GameObject("Visual");
            visual.transform.SetParent(root.transform, false);

            {   // Core: overlapping soft blobs (≈4 alive), breathing size = the slight pulse. Pale white-hot orange ×2.4.
                var p = NewSystem(visual.transform, "Core", world: false, rate: 18f);
                var m = p.main;
                m.startLifetime = new ParticleSystem.MinMaxCurve(0.18f, 0.24f);
                m.startSize = new ParticleSystem.MinMaxCurve(0.66f, 0.78f); // circle_05 blob ≈ 45% of the quad → ~0.32 m
                m.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
                m.maxParticles = 8;
                Size(p, Key(0f, 0.8f), Key(0.35f, 1f), Key(1f, 0.85f));
                Fade(p, CoreHot, CoreHot, 0.25f, 0.6f, 0.85f);
                Render(p, Mat("enemy_orb_core", "circle_05", 2.4f), sortingFudge: -1f);
            }
            {   // Halo: larger, dimmer, orange, slowly spinning cloudy light; flickers by its random sizes.
                var p = NewSystem(visual.transform, "Halo", world: false, rate: 10f);
                var m = p.main;
                m.startLifetime = new ParticleSystem.MinMaxCurve(0.3f, 0.4f);
                m.startSize = new ParticleSystem.MinMaxCurve(0.9f, 1.1f); // light_01 fills ~75% → ~0.75 m glow
                m.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
                m.maxParticles = 8;
                Size(p, Key(0f, 0.85f), Key(0.4f, 1.05f), Key(1f, 0.9f));
                Fade(p, ShotOrange, ShotOrange, 0.3f, 0.55f, 0.45f);
                var rotation = p.rotationOverLifetime;
                rotation.enabled = true;
                rotation.z = new ParticleSystem.MinMaxCurve(-90f * Mathf.Deg2Rad, 90f * Mathf.Deg2Rad);
                Render(p, Mat("enemy_orb_halo", "light_01", 1.6f), sortingFudge: 0f);
            }
            {   // Embers: left behind in world space, small (2–3 texels), drift and cool to deep red.
                var p = NewSystem(root.transform, "Embers", world: true, rate: 40f);
                var m = p.main;
                m.startLifetime = new ParticleSystem.MinMaxCurve(0.25f, 0.45f);
                m.startSpeed = new ParticleSystem.MinMaxCurve(0.3f, 1.2f);
                m.startSize = new ParticleSystem.MinMaxCurve(0.1f, 0.18f);
                m.gravityModifier = -0.15f; // a little lift
                m.maxParticles = 32;
                var shape = p.shape;
                shape.enabled = true;
                shape.shapeType = ParticleSystemShapeType.Sphere;
                shape.radius = 0.08f;
                shape.radiusThickness = 1f;
                var limit = p.limitVelocityOverLifetime;
                limit.enabled = true;
                limit.drag = 2.5f;
                limit.multiplyDragByParticleSize = false;
                limit.multiplyDragByParticleVelocity = false;
                Size(p, Key(0f, 1f), Key(1f, 0.4f));
                var colour = p.colorOverLifetime;
                colour.enabled = true;
                var gradient = new Gradient();
                gradient.SetKeys(
                    new[] { new GradientColorKey(Yellow, 0f), new GradientColorKey(ShotOrange, 0.3f), new GradientColorKey(DeepRed, 1f) },
                    new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0.9f, 0.4f), new GradientAlphaKey(0f, 1f) });
                colour.color = new ParticleSystem.MinMaxGradient(gradient);
                var mainPlay = p.main;
                mainPlay.playOnAwake = false; // started by ProjectileParticles after the launch moved the orb
                Render(p, Mat("enemy_orb_ember", "circle_05", 2f), sortingFudge: 1f);
            }

            // Trail: 0.3 s × 14 m/s ≈ 4 m, 0.30 m wide at the orb tapering to nothing, orange → deep red → transparent.
            var trail = root.AddComponent<TrailRenderer>();
            trail.sharedMaterial = Mat("enemy_orb_trail", null, 1.8f);
            trail.time = 0.3f;
            trail.minVertexDistance = 0.08f;
            trail.widthMultiplier = 1f;
            trail.widthCurve = new AnimationCurve(new Keyframe(0f, 0.3f), new Keyframe(0.4f, 0.16f), new Keyframe(1f, 0f));
            var trailGradient = new Gradient();
            trailGradient.SetKeys(
                new[] { new GradientColorKey(Yellow, 0f), new GradientColorKey(ShotOrange, 0.3f), new GradientColorKey(DeepRed, 1f) },
                new[] { new GradientAlphaKey(0.9f, 0f), new GradientAlphaKey(0.5f, 0.5f), new GradientAlphaKey(0f, 1f) });
            trail.colorGradient = trailGradient;
            trail.textureMode = LineTextureMode.Stretch;
            trail.alignment = LineAlignment.View;
            trail.numCapVertices = 2;
            trail.autodestruct = false;
            trail.emitting = true;
            trail.shadowCastingMode = ShadowCastingMode.Off;
            trail.receiveShadows = false;

            var so = new SerializedObject(projectile);
            so.FindProperty("_visual").objectReferenceValue = visual.transform;
            so.FindProperty("_stretchVisual").boolValue = false; // authored size; trail cleared on every launch
            so.ApplyModifiedPropertiesWithoutUndo();

            GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            Object.DestroyImmediate(root);
            return saved != null ? saved.GetComponent<Projectile>() : null;
        }

        // ------------------------------------------------------------------ helpers

        /// <summary>A looping layer with continuous emission. Local layers prewarm and play on enable (full from frame 0).</summary>
        private static ParticleSystem NewSystem(Transform parent, string name, bool world, float rate)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var p = go.AddComponent<ParticleSystem>();
            p.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var m = p.main;
            m.duration = 1f;
            m.loop = true;
            m.prewarm = !world;
            m.playOnAwake = true;
            m.simulationSpace = world ? ParticleSystemSimulationSpace.World : ParticleSystemSimulationSpace.Local;
            m.scalingMode = ParticleSystemScalingMode.Hierarchy;
            m.stopAction = ParticleSystemStopAction.None;
            m.startSpeed = 0f;
            m.gravityModifier = 0f;
            m.startColor = Color.white; // colour and alpha come from colour over lifetime
            var emission = p.emission;
            emission.enabled = true;
            emission.rateOverTime = rate;
            emission.rateOverDistance = 0f;
            var shape = p.shape;
            shape.enabled = false;
            return p;
        }

        private static void Size(ParticleSystem p, params Keyframe[] keys)
        {
            var size = p.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(keys));
        }

        /// <summary>Alpha fades in for <paramref name="fadeIn"/>, holds until <paramref name="holdUntil"/>, then out.</summary>
        private static void Fade(ParticleSystem p, Color start, Color end, float fadeIn, float holdUntil, float peakAlpha)
        {
            var colour = p.colorOverLifetime;
            colour.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(start, 0f), new GradientColorKey(end, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(peakAlpha, fadeIn),
                        new GradientAlphaKey(peakAlpha, holdUntil), new GradientAlphaKey(0f, 1f) });
            colour.color = new ParticleSystem.MinMaxGradient(gradient);
        }

        private static void Render(ParticleSystem p, Material material, float sortingFudge)
        {
            var renderer = p.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.sortMode = ParticleSystemSortMode.None;
            renderer.sortingFudge = sortingFudge;
            renderer.minParticleSize = 0f;
            renderer.maxParticleSize = 4f;
        }

        private static Keyframe Key(float time, float value) => new Keyframe(time, value);

        /// <summary>Additive VFX_ParticleUnlit material; <paramref name="texture"/> null = plain white (trail ribbon).</summary>
        private static Material Mat(string name, string texture, float intensity)
        {
            string path = MaterialFolder + "/mat_vfx_" + name + ".mat";
            Shader shader = AssetDatabase.LoadAssetAtPath<Shader>(ShaderPath);
            if (shader == null) Debug.LogWarning("EnemyOrbBuilder: missing " + ShaderPath);
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, path);
            }
            if (shader != null) material.shader = shader;
            material.SetTexture("_MainTex", texture != null
                ? AssetDatabase.LoadAssetAtPath<Texture2D>(TextureFolder + "/" + texture + ".png")
                : null); // null = the shader's default "white"
            material.SetColor("_TintColor", Color.white);
            material.SetFloat("_Intensity", intensity);
            material.SetFloat("_Additive", 1f);
            material.renderQueue = 3000;
            EditorUtility.SetDirty(material);
            return material;
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
