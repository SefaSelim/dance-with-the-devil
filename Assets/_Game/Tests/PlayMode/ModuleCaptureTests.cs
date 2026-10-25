using System.Collections;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace DanceWithTheDevil.Tests
{
    /// <summary>
    /// Visual check, not a gate: plays the Arena with the waves off, equips Yoğunluk, Yankı and Delici, holds Q to open
    /// the selection ring, then throws two modules; saves frames to Logs/ModuleCaptures. Explicit; run it with
    /// -testFilter ModuleCaptureTests.
    /// </summary>
    [Explicit("Writes screenshots; run on demand.")]
    public sealed class ModuleCaptureTests
    {
        private const string ArenaPath = "Assets/_Game/Scenes/Arena.unity";
        private InputSettings.BackgroundBehavior _background;
        private InputSettings.EditorInputBehaviorInPlayMode _editorBehavior;

        [UnityTearDown]
        public IEnumerator UnloadArena()
        {
            if (Keyboard.current != null) InputSystem.QueueStateEvent(Keyboard.current, new KeyboardState());
            InputSystem.settings.backgroundBehavior = _background;
            InputSystem.settings.editorInputBehaviorInPlayMode = _editorBehavior;
            yield return TestScenes.LeaveLoadedScenes();
        }

        [UnityTest]
        public IEnumerator RingAndThrownModules_AreCaptured()
        {
#if UNITY_EDITOR
            yield return UnityEditor.SceneManagement.EditorSceneManager.LoadSceneAsyncInPlayMode(
                ArenaPath, new LoadSceneParameters(LoadSceneMode.Single));
#else
            Assert.Ignore("Editor only.");
            yield break;
#endif
            string folder = Path.Combine(Directory.GetCurrentDirectory(), "Logs", "ModuleCaptures");
            Directory.CreateDirectory(folder);
            var waves = Object.FindAnyObjectByType<WaveDirector>();
            if (waves != null) waves.enabled = false;
            yield return Wait(3.5f);

            var modules = Object.FindAnyObjectByType<PlayerModules>();
            Assert.IsNotNull(modules);
            modules.EquipImmediate(ModuleType.Density);
            modules.EquipImmediate(ModuleType.Echo);
            modules.EquipImmediate(ModuleType.Pierce);
            yield return Wait(0.5f);

            Assert.IsNotNull(Keyboard.current, "keyboard");
            // The Game view has no focus during a test run: let the queued key reach the game anyway.
            _background = InputSystem.settings.backgroundBehavior;
            _editorBehavior = InputSystem.settings.editorInputBehaviorInPlayMode;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSystem.settings.editorInputBehaviorInPlayMode =
                InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            InputSystem.QueueStateEvent(Keyboard.current, new KeyboardState(Key.Q));
            yield return Wait(0.8f);
            yield return Capture(folder, "01_ring");
            InputSystem.QueueStateEvent(Keyboard.current, new KeyboardState());
            yield return Wait(0.3f);

            modules.TryEject(ModuleType.Density);
            yield return Wait(0.08f);
            yield return Capture(folder, "02_thrown_density_a");
            yield return Wait(0.12f);
            yield return Capture(folder, "03_thrown_density_b");
            yield return Wait(0.8f);
            modules.TryEject(ModuleType.Echo);
            yield return Wait(0.12f);
            yield return Capture(folder, "04_thrown_echo");
        }

        private static IEnumerator Capture(string folder, string name)
        {
            yield return new WaitForEndOfFrame();
            Texture2D tex = ScreenCapture.CaptureScreenshotAsTexture();
            File.WriteAllBytes(Path.Combine(folder, name + ".png"), tex.EncodeToPNG());
            Object.Destroy(tex);
        }

        private static IEnumerator Wait(float seconds)
        {
            float end = Time.realtimeSinceStartup + seconds;
            while (Time.realtimeSinceStartup < end) yield return null;
        }
    }
}
