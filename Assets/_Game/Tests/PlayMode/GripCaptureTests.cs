using System.Collections;
using System.IO;
using System.Text;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace DanceWithTheDevil.Tests
{
    /// <summary>
    /// Visual check, not a gate: plays the Arena with the waves off, lets Defne stand in her aiming idle and renders
    /// close-ups of the rifle in her hands from the side, the front and above (a temporary camera, full resolution) to
    /// Logs/GripCaptures, plus grip.txt with both hands' positions in the rifle's frame (+Z barrel, origin = grip)
    /// and how far each hand bone ended from its target. Then the same while she runs forward and strafes, and with
    /// the rocket launcher held.
    /// Explicit; run it with -testFilter GripCaptureTests.
    /// </summary>
    [Explicit("Writes screenshots; run on demand.")]
    public sealed class GripCaptureTests
    {
        private const string ArenaPath = "Assets/_Game/Scenes/Arena.unity";

        [UnityTearDown]
        public IEnumerator UnloadArena() => TestScenes.LeaveLoadedScenes();

        [UnityTest]
        public IEnumerator Grip_IsCaptured()
        {
#if UNITY_EDITOR
            yield return UnityEditor.SceneManagement.EditorSceneManager.LoadSceneAsyncInPlayMode(
                ArenaPath, new LoadSceneParameters(LoadSceneMode.Single));
#else
            Assert.Ignore("Editor only.");
            yield break;
#endif
            string folder = Path.Combine(Directory.GetCurrentDirectory(), "Logs", "GripCaptures");
            Directory.CreateDirectory(folder);
            var waves = Object.FindAnyObjectByType<WaveDirector>();
            if (waves != null) waves.enabled = false;
            yield return Wait(3.5f);

            var hold = Object.FindAnyObjectByType<WeaponHold>();
            Assert.IsNotNull(hold, "WeaponHold on Defne");
            var player = Object.FindAnyObjectByType<PlayerController>();
            var input = player.GetComponent<PlayerInputReader>();
            if (input != null) input.enabled = false;
            var body = player.GetComponent<CharacterController>();
            Transform root = player.transform;

            var log = new StringBuilder();
            for (int k = 0; k < 4; k++)
            {
                log.AppendLine("goal " + k + " " + hold.transform.InverseTransformPoint(hold.GoalPosition).ToString("F3")
                               + " socket " + hold.transform.InverseTransformPoint(hold.ActiveWeapon.position).ToString("F3"));
                yield return Wait(0.25f);
            }
            yield return new WaitForEndOfFrame();
            LogHands(log, hold, "idle");
            yield return Views(folder, hold, "");
            Texture2D game = ScreenCapture.CaptureScreenshotAsTexture(); // as the player sees it
            File.WriteAllBytes(Path.Combine(folder, "game.png"), game.EncodeToPNG());
            Object.Destroy(game);

            // Running forward and strafing right, aim kept forward (as MoveCaptureTests).
            Vector3[] directions = { Vector3.forward, Vector3.right };
            string[] names = { "run", "strafe" };
            for (int d = 0; d < directions.Length; d++)
            {
                float end = Time.time + 1.2f;
                bool shot = false;
                while (Time.time < end)
                {
                    root.rotation = Quaternion.identity;
                    Vector3 step = directions[d] * (6f * Time.deltaTime);
                    if (body != null && body.enabled) body.Move(step);
                    else root.position += step;
                    if (!shot && Time.time > end - 0.3f)
                    {
                        shot = true;
                        yield return new WaitForEndOfFrame();
                        LogHands(log, hold, names[d]);
                        yield return Side(folder, hold, names[d] + "_side_left", -1f);
                        yield return Side(folder, hold, names[d] + "_side_right", 1f);
                    }
                    else yield return null;
                }
            }

            // Rocket launcher in the hands (kept shown).
            var character = Object.FindAnyObjectByType<CharacterAnimator>();
            Assert.IsNotNull(character, "CharacterAnimator on Defne");
            for (int f = 0; f < 30; f++)
            {
                root.rotation = Quaternion.identity;
                character.ShowLauncher();
                yield return null;
            }
            character.ShowLauncher();
            yield return new WaitForEndOfFrame();
            LogHands(log, hold, "launcher");
            File.WriteAllText(Path.Combine(folder, "grip.txt"), log.ToString());
            yield return Views(folder, hold, "launcher_");
        }

        private static void LogHands(StringBuilder log, WeaponHold hold, string label)
        {
            Animator animator = hold.GetComponent<Animator>();
            Transform weapon = hold.ActiveWeapon;
            WeaponHoldProfile profile = hold.ActiveProfile;
            Transform socket = hold.Socket;
            log.AppendLine("== " + label + " weapon " + weapon.name + " clip " + hold.DominantClip);
            foreach (bool r in new[] { true, false })
            {
                Transform h = animator.GetBoneTransform(r ? HumanBodyBones.RightHand : HumanBodyBones.LeftHand);
                Transform mid = animator.GetBoneTransform(r ? HumanBodyBones.RightMiddleProximal : HumanBodyBones.LeftMiddleProximal);
                Transform idx = animator.GetBoneTransform(r ? HumanBodyBones.RightIndexProximal : HumanBodyBones.LeftIndexProximal);
                Transform lit = animator.GetBoneTransform(r ? HumanBodyBones.RightLittleProximal : HumanBodyBones.LeftLittleProximal);
                Transform dist = animator.GetBoneTransform(r ? HumanBodyBones.RightMiddleDistal : HumanBodyBones.LeftMiddleDistal);
                Vector3 dir = weapon.InverseTransformDirection(mid.position - h.position).normalized;
                Vector3 across = weapon.InverseTransformDirection(idx.position - lit.position);
                Vector3 palm = (r ? Vector3.Cross(dir, across) : Vector3.Cross(across, dir)).normalized;
                Vector3 curl = Vector3.ProjectOnPlane(weapon.InverseTransformDirection(dist.position - mid.position), dir).normalized;
                Vector3 target = socket.position + socket.rotation * (r ? profile.RightHandPosition : profile.LeftHandPosition);
                log.AppendLine((r ? "R" : "L") + " hand " + weapon.InverseTransformPoint(h.position).ToString("F3")
                               + " index " + weapon.InverseTransformPoint(idx.position).ToString("F3")
                               + " miss " + (h.position - target).magnitude.ToString("F3")
                               + " dir " + dir.ToString("F2") + " palm " + palm.ToString("F2") + " curl " + curl.ToString("F2"));
            }
            Vector3 flat = Vector3.ProjectOnPlane(weapon.forward, Vector3.up);
            log.AppendLine("barrel yaw vs body " + Vector3.SignedAngle(hold.transform.forward, flat, Vector3.up).ToString("F1")
                           + "  pitch " + (90f - Vector3.Angle(Vector3.up, weapon.forward)).ToString("F1")
                           + "  roll(up·world up) " + Vector3.Dot(weapon.up, Vector3.up).ToString("F2"));
        }

        private static IEnumerator Views(string folder, WeaponHold hold, string prefix)
        {
            Transform weapon = hold.ActiveWeapon;
            Vector3 centre = weapon.position + weapon.forward * 0.1f;
            Vector3 side = Vector3.Cross(Vector3.up, weapon.forward).normalized;
            yield return Render(folder, prefix + "side_right", centre, centre + side * 0.9f + Vector3.up * 0.1f);
            yield return Render(folder, prefix + "side_left", centre, centre - side * 0.9f + Vector3.up * 0.1f);
            yield return Render(folder, prefix + "front", centre, centre + weapon.forward * 1.0f + side * 0.3f + Vector3.up * 0.2f);
            yield return Render(folder, prefix + "top", centre, centre + Vector3.up * 1.0f - weapon.forward * 0.2f);
        }

        private static IEnumerator Side(string folder, WeaponHold hold, string name, float sign)
        {
            Transform weapon = hold.ActiveWeapon;
            Vector3 centre = weapon.position + weapon.forward * 0.1f;
            Vector3 side = Vector3.Cross(Vector3.up, weapon.forward).normalized;
            yield return Render(folder, name, centre, centre + side * (0.9f * sign) + Vector3.up * 0.1f);
        }

        private static IEnumerator Render(string folder, string name, Vector3 target, Vector3 from)
        {
            yield return new WaitForEndOfFrame();
            var go = new GameObject("GripCam");
            var cam = go.AddComponent<Camera>();
            cam.enabled = false;
            cam.fieldOfView = 40f;
            cam.nearClipPlane = 0.02f;
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
