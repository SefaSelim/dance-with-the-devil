using System.IO;
using UnityEditor;
using UnityEngine;

namespace DanceWithTheDevil.Editor
{
    /// <summary>
    /// Builds the textured gunfire and rocket effects (unity-vfx-game-feel recipes "small-arms muzzle flash",
    /// "explosion", "bullet impact"; Kenney Particle Pack CC0 textures in Assets/_Game/ThirdParty/KenneyParticlePack)
    /// into prefabs under Assets/_Game/Prefabs/VFX, used by <see cref="RealisticVfx"/>. Menu <c>Dance/VFX/Build Effects</c>.
    /// Every module a layer uses is set explicitly; unused modules stay off. Values are starting points tuned for the
    /// orthographic follow camera (size 6.5, ~42 px per metre): check them at Time.timeScale 0.1 and from gameplay view.
    /// Material blending: the VFX_ParticleUnlit shader is premultiplied; _Additive 1 = fire, sparks, flashes;
    /// 0 = smoke, dirt, scorch (they darken and cover).
    /// </summary>
    public static class VfxBuilder
    {
        private const string TextureFolder = "Assets/_Game/ThirdParty/KenneyParticlePack";
        private const string MaterialFolder = "Assets/_Game/Art/Materials/VFX";
        public const string PrefabFolder = "Assets/_Game/Prefabs/VFX";
        public const string MuzzlePath = PrefabFolder + "/vfx_muzzle_rifle.prefab";
        public const string LaunchPath = PrefabFolder + "/vfx_rocket_launch.prefab";
        public const string ExplosionPath = PrefabFolder + "/vfx_rocket_explosion.prefab";
        public const string ImpactPath = PrefabFolder + "/vfx_bullet_impact.prefab";
        private const string ShaderPath = "Assets/_Game/Art/Shaders/VFX_ParticleUnlit.shader";

        // Temperature ramp of fire (white-hot → yellow → orange → deep red), linear colours.
        private static readonly Color WhiteHot = new Color(1f, 0.96f, 0.85f);
        private static readonly Color Yellow = new Color(1f, 0.78f, 0.3f);
        private static readonly Color Orange = new Color(1f, 0.45f, 0.1f);
        private static readonly Color DeepRed = new Color(0.55f, 0.12f, 0.04f);
        private static readonly Color SmokeDark = new Color(0.32f, 0.3f, 0.29f); // dark enough to read as smoke, light enough for the dark floor
        private static readonly Color SmokeLight = new Color(0.55f, 0.53f, 0.5f);
        private static readonly Color Dirt = new Color(0.42f, 0.33f, 0.24f);

        [MenuItem("Dance/VFX/Build Effects")]
        public static void BuildAll()
        {
            EnsureFolder(MaterialFolder);
            EnsureFolder(PrefabFolder);
            ConfigureTextures();
            BuildMuzzle();
            BuildLaunch();
            BuildExplosion();
            BuildImpact();
            AssetDatabase.SaveAssets();
            Debug.Log("VfxBuilder: muzzle, launch, explosion and impact effects built.");
        }

        // ------------------------------------------------------------------ effects

        /// <summary>Rifle: 6 shots/s, weight 0.25. Read: "a shot left the barrel, that way". Lean, 2–3 frame core.</summary>
        private static void BuildMuzzle()
        {
            var root = Layer(null, "vfx_muzzle_rifle", local: true);
            {   // Core flash: impact frame only (2–3 frames), front view of the starburst.
                var p = root;
                var m = p.main;
                m.startLifetime = new ParticleSystem.MinMaxCurve(0.035f, 0.055f);
                m.startSpeed = 0f;
                m.startSize = new ParticleSystem.MinMaxCurve(0.32f, 0.46f);
                m.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
                m.startColor = Color.white; // colour and alpha come from Fade (colour over life)
                m.maxParticles = 4;
                Burst(p, 1);
                NoShape(p);
                Size(p, Key(0f, 0.75f), Key(0.2f, 1f), Key(1f, 0.6f));
                Fade(p, WhiteHot, Yellow, 0f, 0.3f);
                Render(p, Mat("muzzle_core", "muzzle_02", 1.3f, additive: true), ParticleSystemRenderMode.Billboard);
            }
            {   // Side flame: the long shape along the barrel that reads from the isometric side view.
                var p = Layer(root.gameObject, "Flame", local: true);
                var m = p.main;
                m.startLifetime = new ParticleSystem.MinMaxCurve(0.045f, 0.07f);
                m.startSpeed = new ParticleSystem.MinMaxCurve(5f, 9f);
                m.startSize = new ParticleSystem.MinMaxCurve(0.16f, 0.26f);
                m.startColor = Color.white; // colour and alpha come from Fade (colour over life)
                m.maxParticles = 6;
                Burst(p, 2, 3);
                Cone(p, 4f, 0.01f);
                Size(p, Key(0f, 0.6f), Key(0.3f, 1f), Key(1f, 0.4f));
                Fade(p, WhiteHot, Orange, 0f, 0.35f);
                Render(p, Mat("muzzle_flame", "flame_02", 1.2f, additive: true), ParticleSystemRenderMode.Stretch, 0.04f, 1.6f);
            }
            {   // Sparks: few, fast, drag then gravity.
                var p = Layer(root.gameObject, "Sparks", local: false);
                var m = p.main;
                m.startLifetime = new ParticleSystem.MinMaxCurve(0.08f, 0.18f);
                m.startSpeed = new ParticleSystem.MinMaxCurve(9f, 18f);
                m.startSize = new ParticleSystem.MinMaxCurve(0.025f, 0.045f);
                m.startColor = Color.white; // colour and alpha come from Fade (colour over life)
                m.gravityModifier = 0.5f;
                m.maxParticles = 24;
                Burst(p, 2, 5);
                Cone(p, 10f, 0.02f);
                Drag(p, 3f);
                Fade(p, WhiteHot, Orange, 0f, 0.5f);
                Render(p, Mat("spark_hot", "spark_02", 1.3f, additive: true), ParticleSystemRenderMode.Stretch, 0.035f, 1f);
            }
            {   // Smoke wisp: soft, slow, rises a little; few so 6 shots/s don't fog the screen.
                var p = Layer(root.gameObject, "Smoke", local: false);
                var m = p.main;
                m.startLifetime = new ParticleSystem.MinMaxCurve(0.45f, 0.8f);
                m.startSpeed = new ParticleSystem.MinMaxCurve(0.6f, 1.4f);
                m.startSize = new ParticleSystem.MinMaxCurve(0.14f, 0.22f);
                m.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
                m.startColor = Color.white; // colour and alpha come from Fade (colour over life)
                m.gravityModifier = -0.05f;
                m.maxParticles = 30;
                Burst(p, 1, 2);
                Cone(p, 18f, 0.03f);
                Drag(p, 2f);
                Size(p, Key(0f, 0.6f), Key(1f, 2.4f));
                Fade(p, SmokeLight, SmokeLight, 0.15f, 0.2f, 0.3f);
                Render(p, Mat("smoke_soft", "smoke_04", 1f, additive: false), ParticleSystemRenderMode.Billboard);
            }
            Save(root.gameObject, MuzzlePath);
        }

        /// <summary>Rocket launch: rare, weight 0.6. Front flame, a hot backblast behind her and a grey smoke cloud.</summary>
        private static void BuildLaunch()
        {
            var root = Layer(null, "vfx_rocket_launch", local: true);
            {   // Front flash.
                var p = root;
                var m = p.main;
                m.startLifetime = new ParticleSystem.MinMaxCurve(0.05f, 0.08f);
                m.startSpeed = 0f;
                m.startSize = new ParticleSystem.MinMaxCurve(0.6f, 0.8f);
                m.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
                m.startColor = Color.white; // colour and alpha come from Fade (colour over life)
                m.maxParticles = 4;
                Burst(p, 1);
                NoShape(p);
                Size(p, Key(0f, 0.7f), Key(0.25f, 1f), Key(1f, 0.5f));
                Fade(p, WhiteHot, Orange, 0f, 0.3f);
                Render(p, Mat("muzzle_core", "muzzle_02", 1.3f, additive: true), ParticleSystemRenderMode.Billboard);
            }
            {   // Backblast: a cone of fire out of the back of the tube (the RPG's real signature).
                var p = Layer(root.gameObject, "Backblast", local: false);
                p.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
                p.transform.localPosition = new Vector3(0f, 0f, -0.9f);
                var m = p.main;
                m.startLifetime = new ParticleSystem.MinMaxCurve(0.12f, 0.22f);
                m.startSpeed = new ParticleSystem.MinMaxCurve(5f, 9f);
                m.startSize = new ParticleSystem.MinMaxCurve(0.35f, 0.6f);
                m.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
                m.startColor = Color.white; // colour and alpha come from Fade (colour over life)
                m.maxParticles = 16;
                Burst(p, 6, 8);
                Cone(p, 16f, 0.08f);
                Drag(p, 6f);
                Size(p, Key(0f, 0.5f), Key(0.3f, 1f), Key(1f, 1.3f));
                FireFade(p, 0.85f);
                Render(p, Mat("fire_ball", "fire_01", 1f, additive: true), ParticleSystemRenderMode.Billboard);
            }
            {   // Launch smoke behind and around the tube.
                var p = Layer(root.gameObject, "Smoke", local: false);
                p.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
                p.transform.localPosition = new Vector3(0f, 0f, -0.7f);
                var m = p.main;
                m.startLifetime = new ParticleSystem.MinMaxCurve(0.9f, 1.6f);
                m.startSpeed = new ParticleSystem.MinMaxCurve(1.5f, 3.5f);
                m.startSize = new ParticleSystem.MinMaxCurve(0.4f, 0.7f);
                m.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
                m.startColor = Color.white; // colour and alpha come from Fade (colour over life)
                m.gravityModifier = -0.06f;
                m.maxParticles = 20;
                Burst(p, 6, 9);
                Cone(p, 30f, 0.1f);
                Drag(p, 3f);
                Size(p, Key(0f, 0.6f), Key(1f, 2.2f));
                Fade(p, SmokeLight, SmokeDark, 0.08f, 0.25f, 0.45f);
                Render(p, Mat("smoke_soft", "smoke_04", 1f, additive: false), ParticleSystemRenderMode.Billboard);
            }
            Save(root.gameObject, LaunchPath);
        }

        /// <summary>
        /// Rocket explosion, radius 2.5 m, weight 1. Read: "it blew up there, and the ground took it". Fast in (flash and
        /// fireball within 3 frames), slow out (smoke 2–3 s, scorch 6 s).
        /// </summary>
        private static void BuildExplosion()
        {
            var root = Layer(null, "vfx_rocket_explosion", local: false);
            {   // Flash: the biggest, brightest thing, 4 frames.
                var p = root;
                var m = p.main;
                m.startLifetime = new ParticleSystem.MinMaxCurve(0.06f, 0.08f);
                m.startSpeed = 0f;
                m.startSize = new ParticleSystem.MinMaxCurve(4.4f, 5.2f);
                m.startColor = Color.white; // colour and alpha come from Fade (colour over life)
                m.maxParticles = 2;
                Burst(p, 1);
                NoShape(p);
                Size(p, Key(0f, 0.6f), Key(0.3f, 1f), Key(1f, 0.8f));
                Fade(p, WhiteHot, Yellow, 0f, 0.2f);
                Render(p, Mat("flash_light", "light_01", 1.2f, additive: true), ParticleSystemRenderMode.Billboard);
            }
            {   // Fireball: rolling flame puffs pushed out and braked hard, cooling white → orange → red → gone.
                var p = Layer(root.gameObject, "Fireball", local: false);
                var m = p.main;
                m.startLifetime = new ParticleSystem.MinMaxCurve(0.35f, 0.7f);
                m.startSpeed = new ParticleSystem.MinMaxCurve(3f, 7f);
                m.startSize = new ParticleSystem.MinMaxCurve(1.4f, 2.2f);
                m.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
                m.startColor = Color.white; // colour and alpha come from Fade (colour over life)
                m.gravityModifier = -0.15f;
                m.maxParticles = 24;
                Burst(p, 14, 18);
                Sphere(p, 0.55f);
                Drag(p, 8f);
                Size(p, Key(0f, 0.55f), Key(0.25f, 1f), Key(1f, 1.35f));
                Spin(p, -90f, 90f);
                FireFade(p, 0.8f);
                Render(p, Mat("fire_ball", "fire_01", 1f, additive: true), ParticleSystemRenderMode.Billboard);
            }
            {   // Smoke column: starts a little late, under and after the fire, dark, rises and spreads.
                var p = Layer(root.gameObject, "Smoke", local: false);
                var m = p.main;
                m.startDelay = 0.06f;
                m.startLifetime = new ParticleSystem.MinMaxCurve(1.8f, 3f);
                m.startSpeed = new ParticleSystem.MinMaxCurve(0.8f, 2.4f);
                m.startSize = new ParticleSystem.MinMaxCurve(1.1f, 1.9f);
                m.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
                m.startColor = Color.white; // colour and alpha come from Fade (colour over life)
                m.gravityModifier = -0.12f;
                m.maxParticles = 30;
                Burst(p, 10, 14);
                Sphere(p, 0.6f);
                Drag(p, 1.5f);
                Size(p, Key(0f, 0.5f), Key(1f, 2f));
                Spin(p, -25f, 25f);
                Fade(p, SmokeDark, SmokeLight, 0.06f, 0.3f, 0.85f);
                Render(p, Mat("smoke_dense", "smoke_07", 1f, additive: false), ParticleSystemRenderMode.Billboard);
            }
            {   // Dirt: chunks thrown up and out, falling back.
                var p = Layer(root.gameObject, "Dirt", local: false);
                var m = p.main;
                m.startLifetime = new ParticleSystem.MinMaxCurve(0.7f, 1.3f);
                m.startSpeed = new ParticleSystem.MinMaxCurve(5f, 10f);
                m.startSize = new ParticleSystem.MinMaxCurve(0.12f, 0.28f);
                m.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
                m.startColor = Color.white; // colour and alpha come from Fade (colour over life)
                m.gravityModifier = 2f;
                m.maxParticles = 30;
                Burst(p, 14, 20);
                Hemisphere(p, 0.3f);
                Drag(p, 0.8f);
                Spin(p, -360f, 360f);
                Fade(p, Dirt, Dirt, 0f, 0.75f);
                Render(p, Mat("dirt_chunk", "dirt_02", 1f, additive: false), ParticleSystemRenderMode.Billboard);
            }
            {   // Embers and sparks: fast streaks, a few hang in the air.
                var p = Layer(root.gameObject, "Embers", local: false);
                var m = p.main;
                m.startLifetime = new ParticleSystem.MinMaxCurve(0.4f, 1.2f);
                m.startSpeed = new ParticleSystem.MinMaxCurve(6f, 15f);
                m.startSize = new ParticleSystem.MinMaxCurve(0.04f, 0.08f);
                m.startColor = Color.white; // colour and alpha come from Fade (colour over life)
                m.gravityModifier = 0.8f;
                m.maxParticles = 60;
                Burst(p, 28, 40);
                Sphere(p, 0.3f);
                Drag(p, 2.5f);
                Fade(p, WhiteHot, DeepRed, 0f, 0.5f);
                Render(p, Mat("spark_hot", "spark_02", 1.3f, additive: true), ParticleSystemRenderMode.Stretch, 0.03f, 1f);
            }
            {   // Ground dust ring: a flat shove of dust along the floor.
                var p = Layer(root.gameObject, "DustRing", local: false);
                p.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f); // circle shape in the XZ plane
                var m = p.main;
                m.startLifetime = new ParticleSystem.MinMaxCurve(0.6f, 1f);
                m.startSpeed = new ParticleSystem.MinMaxCurve(4f, 6.5f);
                m.startSize = new ParticleSystem.MinMaxCurve(0.6f, 0.9f);
                m.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
                m.startColor = Color.white; // colour and alpha come from Fade (colour over life)
                m.maxParticles = 24;
                Burst(p, 14, 18);
                var shape = p.shape;
                shape.enabled = true;
                shape.shapeType = ParticleSystemShapeType.Circle;
                shape.radius = 0.4f;
                shape.radiusThickness = 0f;
                shape.arc = 360f;
                Drag(p, 5f);
                Size(p, Key(0f, 0.6f), Key(1f, 1.9f));
                Fade(p, Dirt, SmokeLight, 0f, 0.2f, 0.55f);
                Render(p, Mat("dust_soft", "smoke_02", 1f, additive: false), ParticleSystemRenderMode.Billboard);
            }
            {   // Scorch mark: flat on the floor, lasts, fades out late.
                var p = Layer(root.gameObject, "Scorch", local: false);
                var m = p.main;
                m.startLifetime = 6f;
                m.startSpeed = 0f;
                m.startSize = new ParticleSystem.MinMaxCurve(2.6f, 3.2f);
                m.startRotation3D = true;
                m.startRotationX = new ParticleSystem.MinMaxCurve(Mathf.PI * 0.5f);
                m.startRotationY = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
                m.startRotationZ = new ParticleSystem.MinMaxCurve(0f);
                m.startColor = Color.white; // colour and alpha come from Fade (colour over life)
                m.maxParticles = 2;
                Burst(p, 1);
                NoShape(p);
                p.transform.localPosition = new Vector3(0f, -0.95f, 0f); // rockets burst about 1 m up: mark the floor
                Fade(p, Color.black, Color.black, 0f, 0.7f, 0.8f);
                Render(p, Mat("scorch", "scorch_02", 1f, additive: false), ParticleSystemRenderMode.Billboard);
                var renderer = p.GetComponent<ParticleSystemRenderer>();
                renderer.alignment = ParticleSystemRenderSpace.World;
                renderer.sortingFudge = 50f; // drawn under the smoke and fire
            }
            Save(root.gameObject, ExplosionPath);
        }

        /// <summary>Bullet impact: frequent, weight 0.2. A short dust puff and a few sparks off the surface.</summary>
        private static void BuildImpact()
        {
            var root = Layer(null, "vfx_bullet_impact", local: false);
            {   // Sparks off the surface.
                var p = root;
                var m = p.main;
                m.startLifetime = new ParticleSystem.MinMaxCurve(0.08f, 0.2f);
                m.startSpeed = new ParticleSystem.MinMaxCurve(4f, 10f);
                m.startSize = new ParticleSystem.MinMaxCurve(0.02f, 0.04f);
                m.startColor = Color.white; // colour and alpha come from Fade (colour over life)
                m.gravityModifier = 1f;
                m.maxParticles = 12;
                Burst(p, 3, 5);
                Cone(p, 40f, 0.02f);
                Drag(p, 2f);
                Fade(p, WhiteHot, Orange, 0f, 0.5f);
                Render(p, Mat("spark_hot", "spark_02", 1.3f, additive: true), ParticleSystemRenderMode.Stretch, 0.03f, 1f);
            }
            {   // Dust puff.
                var p = Layer(root.gameObject, "Dust", local: false);
                var m = p.main;
                m.startLifetime = new ParticleSystem.MinMaxCurve(0.3f, 0.55f);
                m.startSpeed = new ParticleSystem.MinMaxCurve(0.8f, 2f);
                m.startSize = new ParticleSystem.MinMaxCurve(0.14f, 0.24f);
                m.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
                m.startColor = Color.white; // colour and alpha come from Fade (colour over life)
                m.maxParticles = 12;
                Burst(p, 2, 3);
                Cone(p, 35f, 0.02f);
                Drag(p, 4f);
                Size(p, Key(0f, 0.6f), Key(1f, 2f));
                Fade(p, SmokeLight, SmokeLight, 0f, 0.2f, 0.4f);
                Render(p, Mat("smoke_soft", "smoke_04", 1f, additive: false), ParticleSystemRenderMode.Billboard);
            }
            Save(root.gameObject, ImpactPath);
        }

        // ------------------------------------------------------------------ module helpers

        /// <summary>A one-shot layer: no loop, rate 0 (bursts only), world or local, stop action none (pooled).</summary>
        private static ParticleSystem Layer(GameObject parent, string name, bool local)
        {
            var go = new GameObject(name);
            if (parent != null) go.transform.SetParent(parent.transform, false);
            var p = go.AddComponent<ParticleSystem>();
            p.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var m = p.main;
            m.duration = 0.5f;
            m.loop = false;
            m.playOnAwake = false;
            m.simulationSpace = local ? ParticleSystemSimulationSpace.Local : ParticleSystemSimulationSpace.World;
            m.scalingMode = ParticleSystemScalingMode.Hierarchy;
            m.stopAction = ParticleSystemStopAction.None;
            m.startSpeed = 0f;
            m.gravityModifier = 0f;
            var emission = p.emission;
            emission.enabled = true;
            emission.rateOverTime = 0f;
            var shape = p.shape;
            shape.enabled = false;
            return p;
        }

        private static void Burst(ParticleSystem p, int min, int max = -1)
        {
            var emission = p.emission;
            emission.SetBursts(new[]
            {
                new ParticleSystem.Burst(0f, (short)min, (short)(max < 0 ? min : max)),
            });
        }

        private static void NoShape(ParticleSystem p)
        {
            var shape = p.shape;
            shape.enabled = false;
        }

        private static void Cone(ParticleSystem p, float angle, float radius)
        {
            var shape = p.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = angle;
            shape.radius = radius;
            shape.radiusThickness = 1f;
        }

        private static void Sphere(ParticleSystem p, float radius)
        {
            var shape = p.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = radius;
            shape.radiusThickness = 1f;
        }

        private static void Hemisphere(ParticleSystem p, float radius)
        {
            var shape = p.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Hemisphere;
            shape.radius = radius;
            shape.radiusThickness = 1f;
            shape.rotation = new Vector3(-90f, 0f, 0f); // dome opens up (+Y)
        }

        private static void Drag(ParticleSystem p, float drag)
        {
            var limit = p.limitVelocityOverLifetime;
            limit.enabled = true;
            limit.drag = drag;
            limit.multiplyDragByParticleSize = false;
            limit.multiplyDragByParticleVelocity = false;
        }

        private static void Spin(ParticleSystem p, float minDegrees, float maxDegrees)
        {
            var rotation = p.rotationOverLifetime;
            rotation.enabled = true;
            rotation.z = new ParticleSystem.MinMaxCurve(minDegrees * Mathf.Deg2Rad, maxDegrees * Mathf.Deg2Rad);
        }

        private static void Size(ParticleSystem p, params Keyframe[] keys)
        {
            var size = p.sizeOverLifetime;
            size.enabled = true;
            float max = 0f;
            foreach (Keyframe k in keys) max = Mathf.Max(max, k.value);
            for (int i = 0; i < keys.Length; i++) keys[i].value /= Mathf.Max(0.0001f, max);
            size.size = new ParticleSystem.MinMaxCurve(max, new AnimationCurve(keys));
        }

        /// <summary>Colour over life from <paramref name="start"/> to <paramref name="end"/>; alpha fades in for
        /// <paramref name="fadeIn"/>, holds until <paramref name="holdUntil"/>, then fades out (the "designed death").</summary>
        private static void Fade(ParticleSystem p, Color start, Color end, float fadeIn, float holdUntil, float peakAlpha = 1f)
        {
            var colour = p.colorOverLifetime;
            colour.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(start, 0f), new GradientColorKey(end, 1f) },
                fadeIn > 0f
                    ? new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(peakAlpha, fadeIn),
                              new GradientAlphaKey(peakAlpha, Mathf.Max(fadeIn, holdUntil)), new GradientAlphaKey(0f, 1f) }
                    : new[] { new GradientAlphaKey(peakAlpha, 0f), new GradientAlphaKey(peakAlpha, holdUntil),
                              new GradientAlphaKey(0f, 1f) });
            colour.color = new ParticleSystem.MinMaxGradient(gradient);
        }

        /// <summary>
        /// Fire cooling over life: white-hot only on the birth frames, then yellow, orange and deep red, fading out;
        /// peak alpha below 1 so overlapping additive puffs keep their colour instead of stacking to white.
        /// </summary>
        private static void FireFade(ParticleSystem p, float peakAlpha)
        {
            var colour = p.colorOverLifetime;
            colour.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(
                new[]
                {
                    new GradientColorKey(WhiteHot, 0f), new GradientColorKey(Yellow, 0.08f),
                    new GradientColorKey(Orange, 0.35f), new GradientColorKey(DeepRed, 1f),
                },
                new[]
                {
                    new GradientAlphaKey(peakAlpha, 0f), new GradientAlphaKey(peakAlpha * 0.85f, 0.4f),
                    new GradientAlphaKey(0f, 1f),
                });
            colour.color = new ParticleSystem.MinMaxGradient(gradient);
        }

        private static void Render(ParticleSystem p, Material material, ParticleSystemRenderMode mode,
            float velocityScale = 0f, float lengthScale = 1f)
        {
            var renderer = p.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = mode;
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.sortMode = ParticleSystemSortMode.Distance;
            renderer.minParticleSize = 0f;
            renderer.maxParticleSize = 4f;
            if (mode == ParticleSystemRenderMode.Stretch)
            {
                renderer.velocityScale = velocityScale;
                renderer.lengthScale = lengthScale;
            }
        }

        private static Keyframe Key(float time, float value) => new Keyframe(time, value);

        // ------------------------------------------------------------------ assets

        private static Material Mat(string name, string texture, float intensity, bool additive)
        {
            string path = MaterialFolder + "/mat_vfx_" + name + ".mat";
            Shader shader = AssetDatabase.LoadAssetAtPath<Shader>(ShaderPath);
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, path);
            }
            material.shader = shader;
            material.SetTexture("_MainTex", AssetDatabase.LoadAssetAtPath<Texture2D>(TextureFolder + "/" + texture + ".png"));
            material.SetColor("_TintColor", Color.white);
            material.SetFloat("_Intensity", intensity);
            material.SetFloat("_Additive", additive ? 1f : 0f);
            material.renderQueue = 3000;
            EditorUtility.SetDirty(material);
            return material;
        }

        private static void ConfigureTextures()
        {
            foreach (string guid in AssetDatabase.FindAssets("t:Texture2D", new[] { TextureFolder }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var importer = (TextureImporter)AssetImporter.GetAtPath(path);
                if (importer == null) continue;
                bool changed = importer.maxTextureSize != 256 || importer.wrapMode != TextureWrapMode.Clamp
                               || !importer.alphaIsTransparency;
                if (!changed) continue;
                importer.textureType = TextureImporterType.Default;
                importer.alphaIsTransparency = true;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.mipmapEnabled = true;
                importer.maxTextureSize = 256; // ~40 px on screen at most
                importer.SaveAndReimport();
            }
        }

        private static void Save(GameObject root, string path)
        {
            PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
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
