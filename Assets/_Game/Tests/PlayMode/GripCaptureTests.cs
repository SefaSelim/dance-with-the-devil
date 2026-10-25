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
    /// Logs/GripCaptures, plus grip.txt with both hands' positions in the rifle's frame (+Z barrel, origin = grip).
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
            Animator animator = hold.GetComponent<Animator>();
            Transform rifle = hold.ActiveWeapon;
            Transform right = animator.GetBoneTransform(HumanBodyBones.RightHand);
            Transform left = animator.GetBoneTransform(HumanBodyBones.LeftHand);
            Transform rightFinger = animator.GetBoneTransform(HumanBodyBones.RightIndexProximal);
            Transform leftFinger = animator.GetBoneTransform(HumanBodyBones.LeftIndexProximal);

            var log = new StringBuilder();
            for (int k = 0; k < 4; k++)
            {
                log.AppendLine("goal " + k + " " + hold.transform.InverseTransformPoint(hold.GoalPosition).ToString("F3")
                               + " socket " + hold.transform.InverseTransformPoint(rifle.position).ToString("F3"));
                yield return Wait(0.25f);
            }
            yield return new WaitForEndOfFrame();
            log.AppendLine("clip " + hold.DominantClip);
            log.AppendLine("rightHand " + rifle.InverseTransformPoint(right.position).ToString("F3"));
            log.AppendLine("rightIndex " + (rightFinger != null ? rifle.InverseTransformPoint(rightFinger.position).ToString("F3") : "-"));
            log.AppendLine("leftHand " + rifle.InverseTransformPoint(left.position).ToString("F3"));
            log.AppendLine("leftIndex " + (leftFinger != null ? rifle.InverseTransformPoint(leftFinger.position).ToString("F3") : "-"));
            foreach (bool r in new[] { true, false })
            {
                Transform h = animator.GetBoneTransform(r ? HumanBodyBones.RightHand : HumanBodyBones.LeftHand);
                Transform mid = animator.GetBoneTransform(r ? HumanBodyBones.RightMiddleProximal : HumanBodyBones.LeftMiddleProximal);
                Transform idx = animator.GetBoneTransform(r ? HumanBodyBones.RightIndexProximal : HumanBodyBones.LeftIndexProximal);
                Transform lit = animator.GetBoneTransform(r ? HumanBodyBones.RightLittleProximal : HumanBodyBones.LeftLittleProximal);
                Vector3 dir = rifle.InverseTransformDirection(mid.position - h.position).normalized;
                Vector3 across = rifle.InverseTransformDirection(idx.position - lit.position);
                Vector3 palm = (r ? Vector3.Cross(dir, across) : Vector3.Cross(across, dir)).normalized;
                Transform dist = animator.GetBoneTransform(r ? HumanBodyBones.RightMiddleDistal : HumanBodyBones.LeftMiddleDistal);
                Vector3 curl = Vector3.ProjectOnPlane(rifle.InverseTransformDirection(dist.position - mid.position), dir).normalized;
                log.AppendLine((r ? "R" : "L") + " curl " + curl.ToString("F2"));
                log.AppendLine((r ? "R" : "L") + " dir " + dir.ToString("F2") + " palm " + palm.ToString("F2"));
            }
            log.AppendLine("rifleScale " + rifle.lossyScale.ToString("F3"));
            Transform body = hold.transform;
            foreach (HumanBodyBones bone in new[] { HumanBodyBones.RightHand, HumanBodyBones.RightMiddleProximal,
                         HumanBodyBones.RightMiddleDistal, HumanBodyBones.LeftHand, HumanBodyBones.LeftMiddleProximal,
                         HumanBodyBones.LeftMiddleDistal })
            {
                Transform t = animator.GetBoneTransform(bone);
                log.AppendLine("body " + bone + " " + (t != null ? body.InverseTransformPoint(t.position).ToString("F3") : "missing"));
            }
            Vector3 flat = Vector3.ProjectOnPlane(rifle.forward, Vector3.up);
            log.AppendLine("barrel yaw vs body " + Vector3.SignedAngle(hold.transform.forward, flat, Vector3.up).ToString("F1")
                           + "  pitch " + (90f - Vector3.Angle(Vector3.up, rifle.forward)).ToString("F1")
                           + "  roll(up·world up) " + Vector3.Dot(rifle.up, Vector3.up).ToString("F2"));
            File.WriteAllText(Path.Combine(folder, "grip.txt"), log.ToString());

            Vector3 centre = rifle.position + rifle.forward * 0.1f;
            Vector3 side = Vector3.Cross(Vector3.up, rifle.forward).normalized;
            yield return Render(folder, "side_right", centre, centre + side * 0.9f + Vector3.up * 0.1f);
            yield return Render(folder, "side_left", centre, centre - side * 0.9f + Vector3.up * 0.1f);
            yield return Render(folder, "front", centre, centre + rifle.forward * 1.0f + side * 0.3f + Vector3.up * 0.2f);
            yield return Render(folder, "top", centre, centre + Vector3.up * 1.0f - rifle.forward * 0.2f);
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
