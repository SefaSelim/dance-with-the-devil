using System.Collections;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace DanceWithTheDevil.Tests
{
    /// <summary>
    /// Visual check, not a gate: puts Defne right behind the biggest crypt as the camera sees it, with the waves off,
    /// and saves the game view with the occlusion fade off and on to Logs/OcclusionCaptures. Explicit; run it with
    /// -testFilter OcclusionCaptureTests.
    /// </summary>
    [Explicit("Writes screenshots; run on demand.")]
    public sealed class OcclusionCaptureTests
    {
        private const string ArenaPath = "Assets/_Game/Scenes/Arena.unity";

        [UnityTearDown]
        public IEnumerator UnloadArena() => TestScenes.LeaveLoadedScenes();

        [UnityTest]
        public IEnumerator BehindCrypt_IsCaptured()
        {
#if UNITY_EDITOR
            yield return UnityEditor.SceneManagement.EditorSceneManager.LoadSceneAsyncInPlayMode(
                ArenaPath, new LoadSceneParameters(LoadSceneMode.Single));
#else
            Assert.Ignore("Editor only.");
            yield break;
#endif
            string folder = Path.Combine(Directory.GetCurrentDirectory(), "Logs", "OcclusionCaptures");
            Directory.CreateDirectory(folder);
            var waves = Object.FindAnyObjectByType<WaveDirector>();
            if (waves != null) waves.enabled = false;
            yield return Wait(1f);

            Renderer crypt = null;
            foreach (Renderer r in Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None))
                if (r.name.StartsWith("SM_Prop_Crypt_0") && !r.name.Contains("Glass") && !r.name.Contains("Door")
                    && (crypt == null || r.bounds.size.y > crypt.bounds.size.y)) crypt = r;
            Assert.IsNotNull(crypt, "a crypt");
            var rig = Object.FindAnyObjectByType<PixelCameraRig>();
            Camera cam = Camera.allCameras.Length > 0 ? null : null;
            foreach (Camera c in Camera.allCameras) if (c.targetTexture != null) cam = c;
            Vector3 forward = Vector3.ProjectOnPlane(cam != null ? cam.transform.forward : new Vector3(1f, 0f, 1f), Vector3.up).normalized;
            Bounds b = crypt.bounds;
            Vector3 spot = new Vector3(b.center.x, 0f, b.center.z) + forward * (Mathf.Max(b.extents.x, b.extents.z) + 1.2f);

            var player = Object.FindAnyObjectByType<PlayerController>();
            var controller = player.GetComponent<CharacterController>();
            if (controller != null) controller.enabled = false;
            player.transform.position = spot;
            if (controller != null) controller.enabled = true;
            rig.SetTarget(player.transform, true);

            var fader = rig.GetComponent<OcclusionFader>();
            Assert.IsNotNull(fader, "OcclusionFader on the camera rig");
            fader.enabled = false;
            yield return Wait(0.6f);
            yield return Capture(folder, "01_fade_off");
            fader.enabled = true;
            yield return Wait(0.6f);
            yield return Capture(folder, "02_fade_on");
            GameObject cryptRoot = UnityEditor.PrefabUtility.GetOutermostPrefabInstanceRoot(crypt.gameObject) ?? crypt.gameObject;
            cryptRoot.SetActive(false);
            yield return Wait(0.2f);
            yield return Capture(folder, "03_crypt_hidden");
            cryptRoot.SetActive(true);
            File.WriteAllText(Path.Combine(folder, "pos.txt"), "spot " + spot + " player " + player.transform.position
                + " crypt " + b.center + " ext " + b.extents + " forward " + forward
                + " screen01 " + rig.WorldToDisplay01(player.transform.position + Vector3.up)
                + " faded " + CountFaded());
        }

        private static int CountFaded()
        {
            int n = 0;
            foreach (Renderer r in Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None))
                foreach (Material m in r.sharedMaterials)
                    if (m != null && m.name.EndsWith("(faded)")) { n++; break; }
            return n;
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
