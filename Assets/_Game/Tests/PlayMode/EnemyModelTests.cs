using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace DanceWithTheDevil.Tests
{
    /// <summary>
    /// The Synty enemy bodies (Dance/Enemies/Build Enemy Models) in the arena's pools: each type has a skinned,
    /// humanoid body under Model with ShatterProxy boxes, a hit shows the white flash on the textured body
    /// (_FlashAmount), and a death hides the body and bursts it into cubes. Ignored when the arena's enemies are
    /// still the cube fallback (Synty pack not imported on this machine).
    /// </summary>
    public sealed class EnemyModelTests
    {
        private const string ArenaPath = "Assets/_Game/Scenes/Arena.unity";

        [UnityTearDown]
        public IEnumerator UnloadArena() => TestScenes.LeaveLoadedScenes();
        private static readonly int FlashAmountId = Shader.PropertyToID("_FlashAmount");

        private static IEnumerator LoadArena()
        {
#if UNITY_EDITOR
            yield return UnityEditor.SceneManagement.EditorSceneManager.LoadSceneAsyncInPlayMode(
                ArenaPath, new LoadSceneParameters(LoadSceneMode.Single));
#else
            Assert.Ignore("Editor only.");
            yield break;
#endif
            var waves = Object.FindAnyObjectByType<WaveDirector>();
            if (waves != null) waves.enabled = false;
            yield return null;
        }

        /// <summary>Spawns one enemy of <paramref name="type"/> from the arena pool 6 m from the player, on the NavMesh.</summary>
        private static Enemy SpawnFromPool(EnemyType type, out Transform player)
        {
            player = Object.FindAnyObjectByType<PlayerController>().transform;
            foreach (EnemyPool pool in Object.FindObjectsByType<EnemyPool>(FindObjectsSortMode.None))
            {
                if (pool.Prefab == null || pool.Prefab.Type != type) continue;
                if (pool.Prefab.GetComponentInChildren<SkinnedMeshRenderer>(true) == null)
                    Assert.Ignore($"{type} prefab is the cube fallback (Synty pack not imported or enemies not rebuilt).");
                Vector3 position = player.position + new Vector3(6f, 0f, 0f);
                if (NavMesh.SamplePosition(position, out NavMeshHit hit, 4f, NavMesh.AllAreas)) position = hit.position;
                return pool.Get(position, Quaternion.identity, player);
            }
            Assert.Fail($"no pool for {type} in the arena");
            return null;
        }

        [UnityTest]
        public IEnumerator PooledEnemy_HasSkinnedBodyAndShatterProxies([Values(EnemyType.Chaser, EnemyType.Runner, EnemyType.Shooter)] EnemyType type)
        {
            // Arrange
            yield return LoadArena();
            Enemy enemy = SpawnFromPool(type, out _);
            yield return null;

            // Act
            var skinned = enemy.Model.GetComponentsInChildren<SkinnedMeshRenderer>();
            var proxies = enemy.Model.GetComponentsInChildren<ShatterProxy>();
            var animator = enemy.Model.GetComponentInChildren<Animator>();

            // Assert
            Assert.Greater(skinned.Length, 0, "skinned body under Model");
            Assert.GreaterOrEqual(proxies.Length, 10, "shatter proxies on the bones");
            Assert.IsNotNull(animator, "Animator on the body");
            Assert.IsTrue(animator.isHuman, "humanoid body");
            Assert.IsNotNull(animator.runtimeAnimatorController, "controller assigned");
            Assert.IsFalse(animator.applyRootMotion, "root motion must be off (the agent moves the enemy)");
            Assert.IsTrue(enemy.GetComponent<CapsuleCollider>().enabled, "gameplay collider kept");

            // Feet stay on the root and the body is about the old height (feet near 0, head 1.2–2.2 m).
            Transform head = animator.GetBoneTransform(HumanBodyBones.Head);
            Transform foot = animator.GetBoneTransform(HumanBodyBones.LeftFoot);
            float headY = head.position.y - enemy.transform.position.y;
            float footY = foot.position.y - enemy.transform.position.y;
            Debug.Log($"EnemyModelTests {type}: head {headY:F2} m, foot {footY:F2} m above the root");
            Assert.That(footY, Is.InRange(-0.15f, 0.3f), "foot height");
            Assert.That(headY, Is.InRange(0.8f, 2.2f), "head height");
        }

        [UnityTest]
        public IEnumerator Hit_SetsFlashOnSkinnedBody([Values(EnemyType.Chaser, EnemyType.Runner, EnemyType.Shooter)] EnemyType type)
        {
            // Arrange
            yield return LoadArena();
            Enemy enemy = SpawnFromPool(type, out Transform player);
            yield return null;
            SkinnedMeshRenderer body = enemy.Model.GetComponentInChildren<SkinnedMeshRenderer>();
            var block = new MaterialPropertyBlock();

            // Act
            enemy.Health.TakeDamage(new DamageInfo(1f, enemy.transform.position + Vector3.up, Vector3.forward,
                player.gameObject, DamageKind.Bullet));

            // Assert
            Assert.IsTrue(enemy.IsAlive, "1 damage must not kill");
            body.GetPropertyBlock(block);
            Assert.AreEqual(1f, block.GetFloat(FlashAmountId), 1e-4f, "hit flash on the textured body");
            yield return new WaitForSeconds(0.3f);
            body.GetPropertyBlock(block);
            Assert.AreEqual(0f, block.GetFloat(FlashAmountId), 1e-4f, "flash ends");
        }

        [UnityTest]
        public IEnumerator Death_HidesBodyAndBurstsCubes([Values(EnemyType.Chaser, EnemyType.Runner, EnemyType.Shooter)] EnemyType type)
        {
            // Arrange
            yield return LoadArena();
            Enemy enemy = SpawnFromPool(type, out Transform player);
            yield return null;
            Assert.IsNotNull(PixelImpactFeedback.Instance, "PixelImpactFeedback in the arena");
            int before = CubeCount();
            SkinnedMeshRenderer body = enemy.Model.GetComponentInChildren<SkinnedMeshRenderer>();

            // Act
            enemy.Kill(new DamageInfo(1f, enemy.transform.position + Vector3.up, Vector3.forward, player.gameObject,
                DamageKind.Bullet));

            // Assert: hidden at once, cubes in the air next frame.
            Assert.IsFalse(enemy.IsAlive);
            Assert.IsFalse(body.enabled, "body hidden on death");
            yield return null;
            int after = CubeCount();
            Debug.Log($"EnemyModelTests {type}: {after - before} cubes from the death burst");
            Assert.Greater(after - before, 10, "death burst from the shatter proxies");
        }

        private static int CubeCount()
        {
            int count = 0;
            foreach (ParticleSystem ps in PixelImpactFeedback.Instance.GetComponentsInChildren<ParticleSystem>())
                count += ps.particleCount;
            return count;
        }
    }
}
