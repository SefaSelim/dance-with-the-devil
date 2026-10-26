using System.Collections;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace DanceWithTheDevil.Tests
{
    /// <summary>
    /// Visual check, not a gate: Defne fires the rifle with the waves off; a side camera close to her renders the
    /// first frames of the shot (muzzle flash and bullet tracer leaving the barrel) to Logs/FireCaptures. Explicit;
    /// run it with -testFilter FireCaptureTests.
    /// </summary>
    [Explicit("Writes screenshots; run on demand.")]
    public sealed class FireCaptureTests
    {
        private const string ArenaPath = "Assets/_Game/Scenes/Arena.unity";

        [UnityTearDown]
        public IEnumerator UnloadArena() => TestScenes.LeaveLoadedScenes();

        [UnityTest]
        public IEnumerator Shot_IsCaptured()
        {
#if UNITY_EDITOR
            yield return UnityEditor.SceneManagement.EditorSceneManager.LoadSceneAsyncInPlayMode(
                ArenaPath, new LoadSceneParameters(LoadSceneMode.Single));
#else
            Assert.Ignore("Editor only.");
            yield break;
#endif
            string folder = Path.Combine(Directory.GetCurrentDirectory(), "Logs", "FireCaptures");
            if (Directory.Exists(folder)) Directory.Delete(folder, true);
            Directory.CreateDirectory(folder);
            var waves = Object.FindAnyObjectByType<WaveDirector>();
            if (waves != null) waves.enabled = false;
            float end = Time.realtimeSinceStartup + 3.5f;
            while (Time.realtimeSinceStartup < end) yield return null;

            var player = Object.FindAnyObjectByType<PlayerController>();
            var rifle = player.GetComponentInChildren<Rifle>();
            Transform body = player.transform;
            Vector3 side = body.right;
            for (int shot = 0; shot < 2; shot++)
            {
                Time.timeScale = 0.15f; // slow, so the first frames of the tracer are visible
                Vector3 target = body.position + body.forward * 10f + Vector3.up;
                Assert.IsTrue(rifle.TryFire(rifle.MuzzlePosition, body.forward), "shot fired");
                for (int f = 0; f < 4; f++)
                {
                    yield return null;
                    Vector3 centre = body.position + Vector3.up * 1.2f + body.forward * 1.2f;
                    yield return Render(folder, $"shot{shot}_f{f}", centre, centre + side * 3.2f + Vector3.up * 0.4f);
                }
                Time.timeScale = 1f;
                end = Time.realtimeSinceStartup + 0.6f;
                while (Time.realtimeSinceStartup < end) yield return null;
            }
        }

        private static IEnumerator Render(string folder, string name, Vector3 target, Vector3 from)
        {
            yield return new WaitForEndOfFrame();
            var go = new GameObject("FireCam");
            var cam = go.AddComponent<Camera>();
            cam.enabled = false;
            cam.fieldOfView = 40f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.55f, 0.6f, 0.65f);
            go.transform.position = from;
            go.transform.LookAt(target);
            var rt = new RenderTexture(800, 450, 24);
            cam.targetTexture = rt;
            cam.Render();
            RenderTexture.active = rt;
            var tex = new Texture2D(800, 450, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, 800, 450), 0, 0);
            tex.Apply();
            RenderTexture.active = null;
            File.WriteAllBytes(Path.Combine(folder, name + ".png"), tex.EncodeToPNG());
            Object.Destroy(tex);
            cam.targetTexture = null;
            Object.Destroy(rt);
            Object.Destroy(go);
        }
    }
}
