using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using Unity.AI.Navigation;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.TestTools;

namespace DanceWithTheDevil.Tests
{
    /// <summary>
    /// Wave director and enemy pooling rules from MAIN_GDD §6 (Doğuş güvenliği) and §10 (bütçe, aktif sınır).
    /// Each test builds a flat NavMesh floor, four entrances 12 m from the centre and Chaser-like enemies in code.
    /// </summary>
    public sealed class EnemyWaveTests
    {
        private readonly List<GameObject> _spawned = new List<GameObject>();

        [TearDown]
        public void TearDown()
        {
            for (int i = 0; i < _spawned.Count; i++)
                if (_spawned[i] != null) Object.Destroy(_spawned[i]);
            _spawned.Clear();
        }

        private GameObject Spawn(string name, Vector3 position)
        {
            var go = new GameObject(name);
            go.transform.position = position;
            _spawned.Add(go);
            return go;
        }

        private void BuildFloor()
        {
            GameObject root = Spawn("NavRoot", Vector3.zero);
            GameObject plane = GameObject.CreatePrimitive(PrimitiveType.Plane);
            plane.transform.SetParent(root.transform, false);
            plane.transform.localScale = new Vector3(5f, 1f, 5f); // 50 × 50 m
            var surface = root.AddComponent<NavMeshSurface>();
            surface.collectObjects = CollectObjects.Children;
            surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
            surface.BuildNavMesh();
        }

        /// <summary>An inactive Chaser template: Model of cubes, a capsule collider, Enemy + ChaserBrain.</summary>
        private Enemy BuildEnemyTemplate()
        {
            GameObject go = Spawn("TestChaser", new Vector3(0f, -50f, 0f));
            go.SetActive(false);
            var model = new GameObject("Model");
            model.transform.SetParent(go.transform, false);
            GameObject body = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Object.DestroyImmediate(body.GetComponent<Collider>());
            body.transform.SetParent(model.transform, false);
            body.transform.localPosition = new Vector3(0f, 0.7f, 0f);
            var capsule = go.AddComponent<CapsuleCollider>();
            capsule.center = new Vector3(0f, 0.7f, 0f);
            capsule.height = 1.4f;
            capsule.radius = 0.35f;
            Enemy enemy = go.AddComponent<Enemy>();
            go.AddComponent<ChaserBrain>();
            return enemy;
        }

        private EnemyPool BuildPool(Enemy template)
        {
            EnemyPool pool = Spawn("ChaserPool", Vector3.zero).AddComponent<EnemyPool>();
            pool.Configure(template);
            return pool;
        }

        private Transform[] BuildEntrances(float radius)
        {
            return new[]
            {
                Spawn("North", new Vector3(0f, 0f, radius)).transform,
                Spawn("South", new Vector3(0f, 0f, -radius)).transform,
                Spawn("East", new Vector3(radius, 0f, 0f)).transform,
                Spawn("West", new Vector3(-radius, 0f, 0f)).transform,
            };
        }

        private WaveDirector BuildDirector(Transform player, Transform[] entrances, EnemyPool pool, int chasers, int cap)
        {
            WaveDirector director = Spawn("WaveDirector", Vector3.zero).AddComponent<WaveDirector>();
            var windows = new List<WaveDirector.WaveWindow> { new WaveDirector.WaveWindow(0f, chasers, 0, 0, cap) };
            director.Configure(player, entrances, null, pool, pool, pool, windows, beginOnStart: false);
            return director;
        }

        private static float FlatDistance(Vector3 a, Vector3 b)
        {
            a.y = 0f;
            b.y = 0f;
            return Vector3.Distance(a, b);
        }

        [UnityTest]
        public IEnumerator Director_NeverSpawnsCloserThanMinDistance()
        {
            BuildFloor();
            Transform player = Spawn("Player", new Vector3(10f, 0f, 0f)).transform; // 2 m from East
            Transform[] entrances = BuildEntrances(12f);
            EnemyPool pool = BuildPool(BuildEnemyTemplate());
            WaveDirector director = BuildDirector(player, entrances, pool, 9, 9);

            for (int i = 0; i < 50; i++)
            {
                int pick = director.PickEntrance(player.position, EnemyType.Chaser);
                Assert.AreNotEqual(2, pick, "East entrance is 2 m from the player");
                Assert.GreaterOrEqual(FlatDistance(entrances[pick].position, player.position), director.MinSpawnDistance);
            }

            var spawnDistances = new List<float>();
            director.EnemySpawned += e => spawnDistances.Add(FlatDistance(e.transform.position, player.position));
            director.Begin();

            yield return new WaitForSeconds(1.5f);

            Assert.GreaterOrEqual(spawnDistances.Count, 3, "first sub-group (3 of 9) should have spawned");
            for (int i = 0; i < spawnDistances.Count; i++)
                Assert.GreaterOrEqual(spawnDistances[i], director.MinSpawnDistance - 0.8f, // spawn jitter ≤ 0.75 m sideways
                    $"spawn {i} at {spawnDistances[i]:0.00} m");
        }

        [UnityTest]
        public IEnumerator Director_PostponesWhenNoEntranceQualifies()
        {
            BuildFloor();
            Transform player = Spawn("Player", Vector3.zero).transform;
            Transform[] entrances = BuildEntrances(5f); // every entrance closer than 9 m
            EnemyPool pool = BuildPool(BuildEnemyTemplate());
            WaveDirector director = BuildDirector(player, entrances, pool, 6, 6);

            Assert.AreEqual(-1, director.PickEntrance(player.position, EnemyType.Chaser));
            director.Begin();

            yield return new WaitForSeconds(1.5f);

            Assert.AreEqual(0, director.ActiveCount, "nothing may spawn");
            Assert.AreEqual(0, director.WarningCount, "no warning may start");
            Assert.Greater(director.PendingCount, 0, "spawns are postponed, not dropped");
        }

        [UnityTest]
        public IEnumerator Director_RespectsActiveCap()
        {
            BuildFloor();
            Transform player = Spawn("Player", Vector3.zero).transform;
            Transform[] entrances = BuildEntrances(12f);
            EnemyPool pool = BuildPool(BuildEnemyTemplate());
            WaveDirector director = BuildDirector(player, entrances, pool, 9, 2);
            director.Begin();

            float end = Time.time + 2f;
            while (Time.time < end)
            {
                Assert.LessOrEqual(director.ActiveCount + director.WarningCount, 2, "cap counts warnings too");
                yield return null;
            }
            Assert.AreEqual(2, director.ActiveCount);
            Assert.Greater(director.PendingCount, 0, "the rest wait for room");

            director.ActiveEnemies[0].Kill(new DamageInfo(0f, Vector3.zero, Vector3.forward, null, DamageKind.Bullet));
            Assert.AreEqual(1, director.ActiveCount);

            end = Time.time + 1.5f;
            while (Time.time < end)
            {
                Assert.LessOrEqual(director.ActiveCount + director.WarningCount, 2);
                yield return null;
            }
            Assert.AreEqual(2, director.ActiveCount, "a waiting spawn fills the freed slot");
        }

        [UnityTest]
        public IEnumerator Enemy_DiesOnce_AndReturnsToPool()
        {
            BuildFloor();
            Transform target = Spawn("Player", new Vector3(0f, 0f, 10f)).transform;
            EnemyPool pool = BuildPool(BuildEnemyTemplate());

            Enemy enemy = pool.Get(Vector3.zero, Quaternion.identity, target);
            Assert.IsNotNull(enemy);
            Assert.IsTrue(enemy.IsAlive);
            Assert.AreEqual(30f, enemy.Health.Current, 1e-4f, "Chaser health (GDD §6)");
            Assert.AreEqual(1, pool.CountActive);

            int died = 0;
            enemy.Died += _ => died++;
            yield return null;

            var hit = new DamageInfo(100f, Vector3.zero, Vector3.forward, null, DamageKind.Bullet);
            Assert.IsTrue(enemy.Health.TakeDamage(hit));
            Assert.IsFalse(enemy.Health.TakeDamage(hit), "dead enemies ignore damage");
            Assert.AreEqual(1, died);
            Assert.IsFalse(enemy.IsAlive);

            yield return null;
            yield return null;

            Assert.IsFalse(enemy.gameObject.activeSelf, "returned to the pool (inactive)");
            Assert.AreEqual(0, pool.CountActive);
            Assert.AreEqual(1, pool.CountInactive);
            Assert.AreEqual(1, died, "Died fires once");

            Enemy again = pool.Get(new Vector3(2f, 0f, 0f), Quaternion.identity, target);
            Assert.AreSame(enemy, again, "pooled instance is reused");
            Assert.IsTrue(again.IsAlive);
            Assert.AreEqual(30f, again.Health.Current, 1e-4f);
            Assert.IsTrue(again.HasNavMesh, "agent is placed on the NavMesh");
        }
    }
}
