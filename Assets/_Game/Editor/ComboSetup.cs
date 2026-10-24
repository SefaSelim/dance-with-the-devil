using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace DanceWithTheDevil.Editor
{
    /// <summary>
    /// Installs the combo (design: production/design/combo-haste.md §2) into an arena scene without hand edits:
    /// the base post-processing profile <see cref="ProfilePath"/> on a global Volume, and a "Combo" object with
    /// <see cref="ComboTracker"/>, <see cref="ComboFeel"/> and <see cref="ComboHud"/>, wired by SerializedObject; the
    /// scene's <see cref="ScoreKeeper"/> gets the tracker. Idempotent: an earlier "Combo" / "PostVolume" root is replaced.
    /// Usage: <c>ComboSetup.Install(scene);</c> after PlaceholderBuilder built the Devil and UI objects (the caller
    /// saves the scene), or menu Dance/Combo/Install In Active Scene (saves it).
    /// </summary>
    public static class ComboSetup
    {
        public const string ProfilePath = "Assets/_Game/Settings/ArenaVolumeProfile.asset";
        private const string ComboRootName = "Combo";
        private const string VolumeRootName = "PostVolume";

        [MenuItem("Dance/Combo/Install In Active Scene")]
        private static void InstallInActiveScene()
        {
            Scene scene = SceneManager.GetActiveScene();
            Install(scene);
            EditorSceneManager.SaveScene(scene);
        }

        /// <summary>Creates/refreshes the profile asset and adds the Volume and combo objects to <paramref name="scene"/>.</summary>
        public static void Install(Scene scene)
        {
            if (!scene.IsValid() || !scene.isLoaded)
            {
                Debug.LogError("ComboSetup: scene is not loaded.");
                return;
            }

            VolumeProfile profile = CreateOrUpdateProfile();

            var waves = Find<WaveDirector>(scene);
            var score = Find<ScoreKeeper>(scene);
            var devil = Find<DevilDirector>(scene);
            var rig = Find<PixelCameraRig>(scene);
            Health playerHealth = null;
            if (waves != null && waves.Player != null) playerHealth = waves.Player.GetComponentInParent<Health>();
            if (playerHealth == null)
            {
                var player = Find<PlayerController>(scene);
                if (player != null) playerHealth = player.GetComponent<Health>();
            }
            if (waves == null) Debug.LogWarning("ComboSetup: no WaveDirector in the scene; the combo will never count.");
            if (score == null) Debug.LogWarning("ComboSetup: no ScoreKeeper in the scene; kills will not use the multiplier.");

            foreach (GameObject root in scene.GetRootGameObjects())
                if (root.name == ComboRootName || root.name == VolumeRootName) Object.DestroyImmediate(root);

            Scene previous = SceneManager.GetActiveScene();
            if (previous != scene) SceneManager.SetActiveScene(scene);

            var volumeGo = new GameObject(VolumeRootName);
            Volume volume = volumeGo.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.priority = 0f;
            volume.weight = 1f;
            volume.sharedProfile = profile;

            var comboGo = new GameObject(ComboRootName);
            var tracker = comboGo.AddComponent<ComboTracker>();
            var tso = new SerializedObject(tracker);
            tso.FindProperty("_waves").objectReferenceValue = waves;
            tso.FindProperty("_devil").objectReferenceValue = devil;
            tso.FindProperty("_playerHealth").objectReferenceValue = playerHealth;
            tso.ApplyModifiedPropertiesWithoutUndo();

            var feel = comboGo.AddComponent<ComboFeel>();
            var fso = new SerializedObject(feel);
            fso.FindProperty("_combo").objectReferenceValue = tracker;
            fso.FindProperty("_rig").objectReferenceValue = rig;
            fso.ApplyModifiedPropertiesWithoutUndo();

            var hud = comboGo.AddComponent<ComboHud>();
            var hso = new SerializedObject(hud);
            hso.FindProperty("_combo").objectReferenceValue = tracker;
            hso.FindProperty("_devil").objectReferenceValue = devil;
            hso.ApplyModifiedPropertiesWithoutUndo();

            if (score != null)
            {
                var sso = new SerializedObject(score);
                sso.FindProperty("_combo").objectReferenceValue = tracker;
                sso.ApplyModifiedPropertiesWithoutUndo();
            }

            if (previous != scene && previous.IsValid() && previous.isLoaded) SceneManager.SetActiveScene(previous);
            EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log($"ComboSetup: installed in {scene.path} (waves {(waves != null)}, score {(score != null)}, " +
                      $"devil {(devil != null)}, rig {(rig != null)}, player health {(playerHealth != null)}).");
        }

        /// <summary>
        /// Calm base look for the game camera once post-processing is on: slight bloom on HDR-bright pixels only; no
        /// vignette (DisplayGrade owns it), no tonemapping (keeps today's colours), and the effects the URP default
        /// profile (SampleSceneProfile on PC_RPAsset: Neutral tonemapping, vignette 0.2) would otherwise add switched off.
        /// </summary>
        public static VolumeProfile CreateOrUpdateProfile()
        {
            EnsureFolder("Assets/_Game/Settings");
            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(ProfilePath);
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<VolumeProfile>();
                AssetDatabase.CreateAsset(profile, ProfilePath);
            }

            Bloom bloom = Get<Bloom>(profile);
            bloom.threshold.Override(1f);
            bloom.intensity.Override(0.35f);
            bloom.scatter.Override(0.55f);
            bloom.highQualityFiltering.Override(false);
            bloom.maxIterations.Override(5); // halo ≤ ~32 texels at 962×548

            Get<Tonemapping>(profile).mode.Override(TonemappingMode.None);
            Get<Vignette>(profile).intensity.Override(0f);
            Get<ChromaticAberration>(profile).intensity.Override(0f);
            Get<LensDistortion>(profile).intensity.Override(0f);
            Get<MotionBlur>(profile).intensity.Override(0f);
            Get<FilmGrain>(profile).intensity.Override(0f);
            Get<DepthOfField>(profile).mode.Override(DepthOfFieldMode.Off);
            Get<PaniniProjection>(profile).distance.Override(0f);
            ColorAdjustments color = Get<ColorAdjustments>(profile);
            color.postExposure.Override(0f);
            color.saturation.Override(0f);
            color.contrast.Override(0f);

            EditorUtility.SetDirty(profile);
            AssetDatabase.SaveAssets();
            return profile;
        }

        private static T Get<T>(VolumeProfile profile) where T : VolumeComponent
        {
            if (profile.TryGet(out T component)) return component;
            component = profile.Add<T>(false);
            component.name = typeof(T).Name;
            component.hideFlags = HideFlags.HideInInspector | HideFlags.HideInHierarchy;
            AssetDatabase.AddObjectToAsset(component, profile);
            return component;
        }

        private static T Find<T>(Scene scene) where T : Component
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                T found = root.GetComponentInChildren<T>(true);
                if (found != null) return found;
            }
            return null;
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            var parts = new List<string>(path.Split('/'));
            string current = parts[0];
            for (int i = 1; i < parts.Count; i++)
            {
                string next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(current, parts[i]);
                current = next;
            }
        }
    }
}
