// MuzzleFlashBuilderExample.cs  (Editor only — place under an Editor folder)
// Reference pattern for building a multi-layer effect entirely from code, with EVERY used module
// set explicitly and every number justified. Copy this structure for other effects.
// Menu: Tools/VFX/Build Example Rifle Muzzle Flash  -> Assets/VFX/Generated/
//
// Effect brief: rifle, ~10 shots/s, weight 0.35, first-person or third-person close camera.
// Read: "I fired, from the muzzle, that way." Lean because it repeats constantly.
// Timing @60fps: core+star frames 0-2 (33-50 ms), sparks 5-11 frames, smoke 25-60 frames,
// light 2-4 frames. Barrel forward = +Z of the prefab root.
//
// Requires: VFX_ParticleUnlit.shader, VFXTextureBaker.cs, MuzzleFlash.cs, LightFlash.cs.

using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

public static class MuzzleFlashBuilderExample
{
    const string Root = "Assets/VFX/Generated";

    [MenuItem("Tools/VFX/Build Example Rifle Muzzle Flash")]
    public static void Build()
    {
        Directory.CreateDirectory(Root + "/Materials");
        Directory.CreateDirectory(Root + "/Meshes");
        if (!File.Exists(VFXTextureBaker.Folder + "/T_HotCore.png")) VFXTextureBaker.BakeAll();

        var texCore  = Tex("T_HotCore");
        var texStar  = Tex("T_MuzzleStar");
        var texSpark = Tex("T_SparkStreak");
        var texSmoke = Tex("T_SmokeFlipbook4x4");
        var texNoise = Tex("T_Noise");

        // HDR intensities: core is the only element far above the bloom threshold (~1).
        var matCore  = Mat("M_Muzzle_Core",  texCore,  new Color(1f, 0.92f, 0.75f), 12f, additive: 1f);
        var matStar  = Mat("M_Muzzle_Star",  texStar,  new Color(1f, 0.70f, 0.35f), 7f,  additive: 1f);
        var matSpark = Mat("M_Muzzle_Spark", texSpark, new Color(1f, 0.75f, 0.40f), 5f,  additive: 1f);
        var matSmoke = Mat("M_Muzzle_Smoke", texSmoke, new Color(0.8f, 0.78f, 0.75f), 1f, additive: 0f,
                           noise: texNoise, erosionSoftness: 0.3f, soft: true);
        var crossMesh = CrossMesh();

        var root = new GameObject("VFX_MuzzleFlash_Rifle");

        // ---------- Layer 1: core flash (front billboard) ----------
        var core = Layer(root, "Core", local: true);
        {
            var m = core.main;
            m.startLifetime = new ParticleSystem.MinMaxCurve(0.033f, 0.05f); // 2-3 frames: impact frame only
            m.startSpeed = 0f;                                                  // a flash doesn't travel
            m.startSize = new ParticleSystem.MinMaxCurve(0.16f, 0.26f);        // ~4-7x a 3-4 cm bore
            m.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f); // no two shots alike
            m.maxParticles = 4;
            NoShape(core);
            SizeCurve(core, Key(0f, 0.7f), Key(0.25f, 1f), Key(1f, 0.85f));   // pops to full in 1 frame
            Fade(core, Color.white, Color.white, fadeIn: 0f, holdUntil: 0.4f); // starts at full: it IS the impact
            SetupRenderer(core, matCore, ParticleSystemRenderMode.Billboard);
        }

        // ---------- Layer 2: side star (cross of quads along the barrel, readable from the side) ----------
        var star = Layer(root, "SideStar", local: true);
        {
            var m = star.main;
            m.startLifetime = new ParticleSystem.MinMaxCurve(0.033f, 0.05f);
            m.startSpeed = 0f;
            m.startSize3D = true;
            m.startSizeX = new ParticleSystem.MinMaxCurve(0.12f, 0.2f);  // width
            m.startSizeY = new ParticleSystem.MinMaxCurve(0.12f, 0.2f);
            m.startSizeZ = new ParticleSystem.MinMaxCurve(0.25f, 0.42f); // length forward
            m.startRotation3D = true;
            m.startRotationX = 0f; m.startRotationY = 0f;
            m.startRotationZ = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f); // roll around barrel
            m.maxParticles = 4;
            NoShape(star);
            SizeCurve(star, Key(0f, 0.6f), Key(0.3f, 1f), Key(1f, 1.1f));
            Fade(star, new Color(1f, 0.95f, 0.85f), new Color(1f, 0.55f, 0.25f), 0f, 0.3f); // cools as it dies
            var r = SetupRenderer(star, matStar, ParticleSystemRenderMode.Mesh);
            r.mesh = crossMesh;
            r.alignment = ParticleSystemRenderSpace.Local;
        }

        // ---------- Layer 3: sparks (ballistic, decelerating, stretched by velocity) ----------
        var sparks = Layer(root, "Sparks", local: false);
        {
            var m = sparks.main;
            m.startLifetime = new ParticleSystem.MinMaxCurve(0.08f, 0.18f);
            m.startSpeed = new ParticleSystem.MinMaxCurve(10f, 24f);  // fast out, drag stops them in ~1 m
            m.startSize = new ParticleSystem.MinMaxCurve(0.012f, 0.028f);
            m.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.95f, 0.8f), new Color(1f, 0.7f, 0.35f));
            m.gravityModifier = 0.6f;
            m.maxParticles = 48;
            var sh = sparks.shape;
            sh.enabled = true;
            sh.shapeType = ParticleSystemShapeType.Cone;
            sh.angle = 9f;                 // narrow: muzzle gas is directional
            sh.radius = 0.01f;             // emit from the bore, not from a 1 m disc
            sh.randomDirectionAmount = 0.08f;
            var lv = sparks.limitVelocityOverLifetime;
            lv.enabled = true;
            lv.limit = 100f;               // no hard cap; we only want drag
            lv.drag = 3f;                  // exponential slowdown -> "burst then die"
            lv.multiplyDragByParticleSize = false;
            lv.multiplyDragByParticleVelocity = true;
            SizeCurve(sparks, Key(0f, 1f), Key(0.7f, 0.7f), Key(1f, 0f));    // stays readable, then dies
            Fade(sparks, new Color(1f, 0.95f, 0.8f), new Color(1f, 0.35f, 0.1f), 0f, 0.5f);
            var r = SetupRenderer(sparks, matSpark, ParticleSystemRenderMode.Stretch);
            r.velocityScale = 0.035f;      // length from speed: a 20 m/s spark is ~0.7 units long
            r.lengthScale = 1.2f;
            r.cameraVelocityScale = 0f;
        }

        // ---------- Layer 4: smoke wisp (residue, alpha, erodes away) ----------
        var smoke = Layer(root, "Smoke", local: false);
        {
            var m = smoke.main;
            m.startLifetime = new ParticleSystem.MinMaxCurve(0.45f, 0.9f);
            m.startSpeed = new ParticleSystem.MinMaxCurve(0.6f, 1.6f);
            m.startSize = new ParticleSystem.MinMaxCurve(0.08f, 0.14f);
            m.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            m.gravityModifier = -0.05f;    // slight buoyancy
            m.maxParticles = 40;
            var sh = smoke.shape;
            sh.enabled = true;
            sh.shapeType = ParticleSystemShapeType.Cone;
            sh.angle = 14f;
            sh.radius = 0.015f;
            var lv = smoke.limitVelocityOverLifetime;
            lv.enabled = true;
            lv.limit = 100f;
            lv.drag = 2.5f;                // puff forward, then hang
            var noise = smoke.noise;
            noise.enabled = true;
            noise.strength = 0.35f;
            noise.frequency = 0.8f;
            noise.scrollSpeed = 0.4f;
            noise.damping = true;
            noise.quality = ParticleSystemNoiseQuality.Medium;
            var iv = smoke.inheritVelocity;
            iv.enabled = true;
            iv.mode = ParticleSystemInheritVelocityMode.Initial;
            iv.curveMultiplier = 0.6f;     // smoke shouldn't shoot backwards when the shooter moves
            var rot = smoke.rotationOverLifetime;
            rot.enabled = true;
            rot.z = new ParticleSystem.MinMaxCurve(-0.6f, 0.6f); // rad/s, slow churn
            SizeCurve(smoke, Key(0f, 0.5f, 0f, 2.5f), Key(0.4f, 1f), Key(1f, 1.6f)); // keeps expanding
            Fade(smoke, new Color(0.95f, 0.88f, 0.8f), new Color(0.7f, 0.72f, 0.75f), 0.06f, 0.2f, peakAlpha: 0.32f);
            var tsa = smoke.textureSheetAnimation;
            tsa.enabled = true;
            tsa.numTilesX = 4; tsa.numTilesY = 4;
            tsa.animation = ParticleSystemAnimationType.WholeSheet;
            // Random between two curves (normalised 0..1 of the sheet): each puff starts on a different
            // early frame and never wraps past the last frame.
            tsa.frameOverTime = new ParticleSystem.MinMaxCurve(1f,
                AnimationCurve.Linear(0f, 0f, 1f, 0.75f), AnimationCurve.Linear(0f, 0.2f, 1f, 0.97f));
            var cd = smoke.customData;           // erosion 0 -> 1 over life (read by the shader)
            cd.enabled = true;
            cd.SetMode(ParticleSystemCustomData.Custom1, ParticleSystemCustomDataMode.Vector);
            cd.SetVectorComponentCount(ParticleSystemCustomData.Custom1, 1);
            cd.SetVector(ParticleSystemCustomData.Custom1, 0, new ParticleSystem.MinMaxCurve(1f,
                new AnimationCurve(new Keyframe(0f, 0f), new Keyframe(0.35f, 0.05f), new Keyframe(1f, 0.95f))));
            var r = SetupRenderer(smoke, matSmoke, ParticleSystemRenderMode.Billboard);
            r.sortMode = ParticleSystemSortMode.YoungestInFront;
            r.sortingFudge = 10f;               // behind the additive layers
            r.SetActiveVertexStreams(new List<ParticleSystemVertexStream> {
                ParticleSystemVertexStream.Position, ParticleSystemVertexStream.Color,
                ParticleSystemVertexStream.UV, ParticleSystemVertexStream.Custom1X,
                ParticleSystemVertexStream.StableRandomX });
        }

        // ---------- Layer 5: light ----------
        var lightGO = new GameObject("Light");
        lightGO.transform.SetParent(root.transform, false);
        lightGO.transform.localPosition = new Vector3(0f, 0f, 0.15f); // just ahead of the muzzle
        var light = lightGO.AddComponent<Light>();
        light.type = LightType.Point;
        light.color = new Color(1f, 0.75f, 0.45f);
        light.range = 6f;
        light.intensity = 0f;
        light.shadows = LightShadows.None;
        var flash = lightGO.AddComponent<LightFlash>();
        flash.peakIntensity = 4f;
        flash.duration = 0.05f;

        // ---------- Wiring ----------
        var mf = root.AddComponent<MuzzleFlash>();
        mf.layers = new[]
        {
            new MuzzleFlash.Layer { system = core,   countRange = new Vector2Int(1, 1), chance = 1f,    scaleWithBigFlash = true },
            new MuzzleFlash.Layer { system = star,   countRange = new Vector2Int(1, 1), chance = 0.85f, scaleWithBigFlash = true },
            new MuzzleFlash.Layer { system = sparks, countRange = new Vector2Int(2, 6), chance = 0.9f,  scaleWithBigFlash = false },
            new MuzzleFlash.Layer { system = smoke,  countRange = new Vector2Int(0, 2), chance = 0.7f,  scaleWithBigFlash = false },
        };
        mf.lightFlash = flash;
        mf.bigFlashChance = 0.15f;
        mf.bigFlashScale = 1.35f;

        string path = Root + "/VFX_MuzzleFlash_Rifle.prefab";
        PrefabUtility.SaveAsPrefabAsset(root, path);
        Object.DestroyImmediate(root);
        AssetDatabase.SaveAssets();
        Debug.Log("Built " + path + " — parent it at the barrel tip (+Z forward) and call MuzzleFlash.Fire() per shot.");
    }

    // ---------- helpers ----------

    // Attached, rapid-fire layers: loop on + emission off. MuzzleFlash.Fire() emits exact counts.
    // (Spawned one-shot effects should instead use loop off, bursts, stopAction.)
    static ParticleSystem Layer(GameObject parent, string name, bool local)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent.transform, false);
        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var m = ps.main;
        m.duration = 1f;
        m.loop = true;                       // stays "playing" so Emit() always simulates
        m.playOnAwake = true;
        m.startDelay = 0f;
        m.startColor = Color.white;          // colour comes from Color over Lifetime + material tint
        m.gravityModifier = 0f;
        m.simulationSpace = local ? ParticleSystemSimulationSpace.Local : ParticleSystemSimulationSpace.World;
        m.scalingMode = ParticleSystemScalingMode.Hierarchy;
        m.cullingMode = ParticleSystemCullingMode.AlwaysSimulate;
        m.stopAction = ParticleSystemStopAction.None;
        var em = ps.emission;
        em.enabled = false;                  // never self-emit
        em.rateOverTime = 0f;
        return ps;
    }

    static void NoShape(ParticleSystem ps) { var sh = ps.shape; sh.enabled = false; }

    static Keyframe Key(float t, float v) => new Keyframe(t, v);
    static Keyframe Key(float t, float v, float inT, float outT) => new Keyframe(t, v, inT, outT);

    static void SizeCurve(ParticleSystem ps, params Keyframe[] keys)
    {
        var sol = ps.sizeOverLifetime;
        sol.enabled = true;
        sol.separateAxes = false;            // uniform multiplier (also over a 3D start size)
        sol.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(keys));
    }

    // Colour ramp + alpha envelope: fade in over [0, fadeIn], hold to holdUntil, fade out to 0 at 1.
    static void Fade(ParticleSystem ps, Color from, Color to, float fadeIn, float holdUntil, float peakAlpha = 1f)
    {
        var col = ps.colorOverLifetime;
        col.enabled = true;
        var g = new Gradient();
        var alphas = fadeIn > 0f
            ? new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(peakAlpha, fadeIn), new GradientAlphaKey(peakAlpha * 0.9f, holdUntil), new GradientAlphaKey(0f, 1f) }
            : new[] { new GradientAlphaKey(peakAlpha, 0f), new GradientAlphaKey(peakAlpha * 0.9f, holdUntil), new GradientAlphaKey(0f, 1f) };
        g.SetKeys(new[] { new GradientColorKey(from, 0f), new GradientColorKey(to, 1f) }, alphas);
        col.color = new ParticleSystem.MinMaxGradient(g);
    }

    static ParticleSystemRenderer SetupRenderer(ParticleSystem ps, Material mat, ParticleSystemRenderMode mode)
    {
        var r = ps.GetComponent<ParticleSystemRenderer>();
        r.renderMode = mode;
        r.sharedMaterial = mat;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        r.receiveShadows = false;
        r.minParticleSize = 0.001f;
        r.maxParticleSize = 0.5f;
        r.alignment = ParticleSystemRenderSpace.View;
        return r;
    }

    static Texture2D Tex(string name) =>
        AssetDatabase.LoadAssetAtPath<Texture2D>(VFXTextureBaker.Folder + "/" + name + ".png");

    static Material Mat(string name, Texture2D tex, Color tint, float intensity, float additive,
                        Texture2D noise = null, float erosionSoftness = 0.25f, bool soft = false)
    {
        string path = Root + "/Materials/" + name + ".mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null)
        {
            var shader = Shader.Find("VFX/ParticleUnlit");
            if (shader == null) throw new System.Exception("VFX/ParticleUnlit shader not found — import VFX_ParticleUnlit.shader first.");
            mat = new Material(shader);
            AssetDatabase.CreateAsset(mat, path);   // persist BEFORE assigning to the prefab
        }
        mat.SetTexture("_MainTex", tex);
        mat.SetColor("_TintColor", tint);
        mat.SetFloat("_Intensity", intensity);
        mat.SetFloat("_Additive", additive);
        mat.SetFloat("_ErosionSoftness", erosionSoftness);
        if (noise != null)
        {
            mat.SetTexture("_NoiseTex", noise);
            mat.SetFloat("_NoiseStrength", 0.04f);
            mat.SetVector("_NoiseScroll", new Vector4(0.05f, 0.12f, 0f, 0f));
        }
        mat.SetFloat("_SoftParticles", soft ? 1f : 0f);
        if (soft) mat.EnableKeyword("_SOFTPARTICLES_ON"); else mat.DisableKeyword("_SOFTPARTICLES_ON");
        EditorUtility.SetDirty(mat);
        return mat;
    }

    // Two perpendicular quads along +Z (0..1), centred on the Z axis. Used as a mesh particle so the
    // flash has a silhouette from the side, not just a camera-facing card.
    static Mesh CrossMesh()
    {
        string path = Root + "/Meshes/MuzzleCross.asset";
        var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (existing != null) return existing;
        var m = new Mesh { name = "MuzzleCross" };
        m.vertices = new[]
        {
            new Vector3(-0.5f, 0f, 0f), new Vector3(0.5f, 0f, 0f), new Vector3(-0.5f, 0f, 1f), new Vector3(0.5f, 0f, 1f),
            new Vector3(0f, -0.5f, 0f), new Vector3(0f, 0.5f, 0f), new Vector3(0f, -0.5f, 1f), new Vector3(0f, 0.5f, 1f),
        };
        // U across, V along the barrel: the star texture's centre sits at V = 0.5; the core flash covers the base.
        m.uv = new[]
        {
            new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 1f), new Vector2(1f, 1f),
            new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 1f), new Vector2(1f, 1f),
        };
        m.colors = new[] { Color.white, Color.white, Color.white, Color.white, Color.white, Color.white, Color.white, Color.white };
        m.triangles = new[] { 0, 2, 1, 1, 2, 3, 4, 6, 5, 5, 6, 7 };
        m.RecalculateNormals();
        m.RecalculateBounds();
        AssetDatabase.CreateAsset(m, path);
        return m;
    }
}
