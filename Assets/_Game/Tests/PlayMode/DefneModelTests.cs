using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace DanceWithTheDevil.Tests
{
    /// <summary>
    /// The imported, animated Defne (char_defne.prefab, built by Dance/Character/Build Defne): she stands on her root
    /// while idle and running (the Mixamo clips travel; the Animator must drop that travel), and she holds a rifle
    /// and a launcher whose muzzles the weapons fire from. Editor-only: the prefab is loaded through AssetDatabase.
    /// </summary>
    public sealed class DefneModelTests
    {
        private const string PrefabPath = "Assets/_Game/Prefabs/char_defne.prefab";
        private GameObject _defne;

        [TearDown]
        public void TearDown()
        {
            if (_defne != null) Object.Destroy(_defne);
        }

        private bool Spawn(Vector3 position)
        {
#if UNITY_EDITOR
            var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (prefab == null) return false;
            _defne = Object.Instantiate(prefab, position, Quaternion.identity);
            return true;
#else
            return false;
#endif
        }

        [UnityTest]
        public IEnumerator Defne_StaysOnHerRoot_IdleAndRunning([Values(0f, 1f, -1f)] float moveZ)
        {
            if (!Spawn(new Vector3(200f, 0f, 0f))) Assert.Ignore("char_defne.prefab is not built.");
            var animator = _defne.GetComponentInChildren<Animator>();
            Assert.IsNotNull(animator, "no Animator on the model");
            Assert.IsTrue(animator.isHuman, "the model must be humanoid");

            for (int i = 0; i < 40; i++)
            {
                animator.SetFloat("MoveZ", moveZ);
                animator.SetFloat("Speed", Mathf.Abs(moveZ));
                yield return null;
            }

            Transform hips = animator.GetBoneTransform(HumanBodyBones.Hips);
            Vector3 local = _defne.transform.InverseTransformPoint(hips.position);
            Debug.Log($"DefneModelTests moveZ {moveZ}: hips at {local} (root space)");
            Assert.That(local.y, Is.InRange(0.6f, 1.3f), "hips height");
            Assert.Less(new Vector2(local.x, local.z).magnitude, 0.35f, "hips drift away from the root");
        }

        [UnityTest]
        public IEnumerator Defne_HoldsRifleAndLauncher_WithMuzzles()
        {
            if (!Spawn(new Vector3(210f, 0f, 0f))) Assert.Ignore("char_defne.prefab is not built.");
            var rig = _defne.AddComponent<CharacterAnimator>();
            yield return null;

            Assert.IsTrue(rig.IsHumanoid, "CharacterAnimator should run in humanoid mode");
            Assert.IsNotNull(rig.Muzzle, "rifle muzzle");
            Assert.IsNotNull(rig.LauncherMuzzle, "launcher muzzle");

            Transform hand = _defne.GetComponentInChildren<Animator>().GetBoneTransform(HumanBodyBones.RightHand);
            float reach = Vector3.Distance(hand.position, rig.Muzzle.position);
            Debug.Log($"DefneModelTests rifle muzzle {reach:F2} m from the right hand");
            Assert.That(reach, Is.InRange(0.2f, 0.9f), "rifle muzzle distance from the hand");
        }
    }
}
