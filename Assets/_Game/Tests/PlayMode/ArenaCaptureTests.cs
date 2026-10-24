using System.Collections;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace DanceWithTheDevil.Tests
{
    /// <summary>
    /// Visual check, not a gate: plays the real Arena scene and saves what the game camera draws (its low-res
    /// render texture) to Logs/ArenaCaptures: Defne standing, mid-run, firing the rifle, and right after a rocket.
    /// Explicit, so the normal test run skips it; run it with -testFilter ArenaCaptureTests.
    /// </summary>
    [Explicit("Writes screenshots; run on demand.")]
    public sealed class ArenaCaptureTests
    {
        private const string ArenaPath = "Assets/_Game/Scenes/Arena.unity";

        [UnityTest]
        public IEnumerator Arena_PlayerShots_AreCaptured()
        {
#if UNITY_EDITOR
            yield return UnityEditor.SceneManagement.EditorSceneManager.LoadSceneAsyncInPlayMode(
                ArenaPath, new LoadSceneParameters(LoadSceneMode.Single));
#else
            Assert.Ignore("Editor only.");
            yield break;
#endif
            yield return null;
            var rig = Object.FindAnyObjectByType<PixelCameraRig>();
            var player = Object.FindAnyObjectByType<PlayerController>();
            Assert.IsNotNull(rig, "camera rig");
            Assert.IsNotNull(player, "player");
            var waves = Object.FindAnyObjectByType<WaveDirector>();
            if (waves != null) waves.enabled = false; // a clean frame of the player

            string folder = Path.Combine(Directory.GetCurrentDirectory(), "Logs", "ArenaCaptures");
            Directory.CreateDirectory(folder);

            yield return Wait(0.6f);
            Save(rig, folder, "01_idle");
            yield return CloseUps(player.transform, folder, "00_closeup");

            // Mid-run: move her through the controller toward +x for half a second.
            var controller = player.GetComponent<CharacterController>();
            for (float t = 0f; t < 0.5f; t += Time.deltaTime)
            {
                controller.Move(Vector3.right * 6f * Time.deltaTime);
                yield return null;
            }
            Save(rig, folder, "02_run");

            Vector3 aim = player.transform.position + player.transform.forward * 6f + Vector3.up;
            var rifle = player.GetComponent<Rifle>();
            for (int i = 0; i < 4; i++)
            {
                rifle.TryFireAt(aim, player.transform.forward);
                yield return Wait(0.09f);
            }
            Save(rig, folder, "03_rifle");

            var rocket = player.GetComponent<RocketLauncher>();
            bool exploded = false;
            System.Action<Vector3> onExploded = _ => exploded = true;
            RocketLauncher.Exploded += onExploded;
            rocket.TryFireAt(aim + player.transform.forward * 4f, player.transform.forward);
            yield return Wait(0.05f);
            Save(rig, folder, "04a_rocket_launch");
            yield return CloseUps(player.transform, folder, "04b_launch_closeup");
            for (float t = 0f; t < 2f && !exploded; t += Time.unscaledDeltaTime) yield return null;
            RocketLauncher.Exploded -= onExploded;
            yield return Wait(0.03f);
            Save(rig, folder, "05a_explosion_flash");
            yield return Wait(0.15f);
            Save(rig, folder, "05b_explosion_fire");
            yield return Wait(0.4f);
            Save(rig, folder, "05c_explosion_smoke");
            yield return Wait(1.2f);
            Save(rig, folder, "06_after_explosion");

            // Running close-up: hands and weapon during the run clip.
            for (float t = 0f; t < 0.4f; t += Time.deltaTime)
            {
                controller.Move(-Vector3.right * 6f * Time.deltaTime);
                yield return null;
            }
            yield return CloseUps(player.transform, folder, "07_run_closeup");
        }

        /// <summary>Close views of Defne (front-left and right side) from a temporary camera, 512 px.</summary>
        private static IEnumerator CloseUps(Transform player, string folder, string name)
        {
            var go = new GameObject("CaptureCloseUpCamera");
            var cam = go.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = 1.2f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.1f, 0.09f, 0.12f);
            cam.enabled = false;
            var rt = new RenderTexture(512, 512, 24);
            cam.targetTexture = rt;
            Vector3 focus = player.position + Vector3.up * 1.1f;
            var views = new[] { player.rotation * Quaternion.Euler(10f, 150f, 0f), player.rotation * Quaternion.Euler(10f, 270f, 0f) };
            for (int i = 0; i < views.Length; i++)
            {
                go.transform.rotation = views[i];
                go.transform.position = focus - views[i] * Vector3.forward * 6f;
                cam.Render();
                SaveTexture(rt, folder, $"{name}_{i}");
            }
            cam.targetTexture = null;
            Object.Destroy(rt);
            Object.Destroy(go);
            yield return null;
        }

        private static IEnumerator Wait(float seconds)
        {
            float end = Time.realtimeSinceStartup + seconds;
            while (Time.realtimeSinceStartup < end) yield return null;
        }

        private static void Save(PixelCameraRig rig, string folder, string name) => SaveTexture(rig.RenderTexture, folder, name);

        private static void SaveTexture(RenderTexture rt, string folder, string name)
        {
            RenderTexture previous = RenderTexture.active;
            RenderTexture.active = rt;
            var texture = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
            texture.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
            texture.Apply();
            RenderTexture.active = previous;
            File.WriteAllBytes(Path.Combine(folder, name + ".png"), texture.EncodeToPNG());
            Object.Destroy(texture);
        }
    }
}
