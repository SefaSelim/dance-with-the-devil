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
    /// Visual check, not a gate (K-23): plays the Arena with the waves off, fills the three module slots, brings a
    /// Triple Shot offer next to Defne (blocked: "DOLU") and then holds Q so the selection ring shows the module
    /// icons. Saves Game-view shots and close-ups to Logs/ModuleSlotCaptures.
    /// Explicit; run it with -testFilter ModuleSlotCaptureTests.
    /// </summary>
    [Explicit("Writes screenshots; run on demand.")]
    public sealed class ModuleSlotCaptureTests
    {
        private const string ArenaPath = "Assets/_Game/Scenes/Arena.unity";

        [UnityTearDown]
        public IEnumerator UnloadArena()
        {
            if (Keyboard.current != null) InputSystem.QueueStateEvent(Keyboard.current, new KeyboardState());
            return TestScenes.LeaveLoadedScenes();
        }

        [UnityTest]
        public IEnumerator SlotsFullOffer_AndRingIcons_AreCaptured()
        {
#if UNITY_EDITOR
            yield return UnityEditor.SceneManagement.EditorSceneManager.LoadSceneAsyncInPlayMode(
                ArenaPath, new LoadSceneParameters(LoadSceneMode.Single));
#else
            Assert.Ignore("Editor only.");
            yield break;
#endif
            string folder = Path.Combine(Directory.GetCurrentDirectory(), "Logs", "ModuleSlotCaptures");
            Directory.CreateDirectory(folder);
            var waves = Object.FindAnyObjectByType<WaveDirector>();
            if (waves != null) waves.enabled = false;
            var spawner = Object.FindAnyObjectByType<ModuleSpawner>();
            if (spawner != null) spawner.enabled = false;
            yield return Wait(3.5f); // past the intro line

            var modules = Object.FindAnyObjectByType<PlayerModules>();
            Assert.IsNotNull(modules);
            Assert.IsTrue(modules.EquipImmediate(ModuleType.Echo));
            Assert.IsTrue(modules.EquipImmediate(ModuleType.Pierce));
            Assert.IsTrue(modules.EquipImmediate(ModuleType.Density));
            Transform player = modules.transform;
            Vector3 offerPoint = player.position + new Vector3(2.6f, 0f, 1.2f);
            Assert.IsTrue(modules.PresentOffer(ModuleType.TripleShot, offerPoint), "an offer comes with full slots");
            yield return Wait(1f);
            Assert.IsTrue(modules.Current != null && modules.Current.IsBlocked, "full slots block it");
            yield return Capture(folder, "01_full_offer_game");
            ModuleOffer offer = modules.Current;
            yield return Render(folder, "02_triple_icon", offer.CorePosition,
                offer.CorePosition + new Vector3(0f, 0.6f, -2.2f));

            Keyboard keyboard = Keyboard.current ?? InputSystem.AddDevice<Keyboard>();
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Q));
            yield return Wait(0.6f);
            Assert.IsTrue(modules.SelectionOpen, "Q opens the ring");
            yield return Capture(folder, "03_ring_game");
            Vector3 centre = player.position + Vector3.up * 1.2f;
            yield return Render(folder, "04_ring_close", centre, centre + new Vector3(0f, 2.4f, -3.4f));
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            yield return null;
        }

        private static IEnumerator Capture(string folder, string name)
        {
            yield return new WaitForEndOfFrame();
            Texture2D tex = ScreenCapture.CaptureScreenshotAsTexture();
            File.WriteAllBytes(Path.Combine(folder, name + ".png"), tex.EncodeToPNG());
            Object.Destroy(tex);
        }

        private static IEnumerator Render(string folder, string name, Vector3 target, Vector3 from)
        {
            yield return new WaitForEndOfFrame();
            var go = new GameObject("SlotCam");
            var cam = go.AddComponent<Camera>();
            cam.enabled = false;
            cam.fieldOfView = 40f;
            cam.nearClipPlane = 0.05f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.55f, 0.6f, 0.65f);
            go.transform.position = from;
            go.transform.LookAt(target);
            var rt = new RenderTexture(800, 600, 24);
            cam.targetTexture = rt;
            cam.Render();
            RenderTexture.active = rt;
            var tex = new Texture2D(800, 600, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, 800, 600), 0, 0);
            tex.Apply();
            RenderTexture.active = null;
            File.WriteAllBytes(Path.Combine(folder, name + ".png"), tex.EncodeToPNG());
            Object.Destroy(tex);
            cam.targetTexture = null;
            Object.Destroy(rt);
            Object.Destroy(go);
        }

        private static IEnumerator Wait(float seconds)
        {
            float end = Time.realtimeSinceStartup + seconds;
            while (Time.realtimeSinceStartup < end) yield return null;
        }
    }
}
