using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using Unity.AI.Navigation;
using UnityEngine.AI;
using UnityEngine;
using UnityEngine.TestTools;

namespace DanceWithTheDevil.Tests
{
    /// <summary>
    /// Combo × score in a code-built arena (same setup as <see cref="EnemyWaveTests"/>): Defne's kills pay
    /// 100 × multiplier, revive-shockwave kills a flat 100 and leave the combo alone (production/design/combo-haste.md §8);
    /// a crit hit adds 25 × multiplier; only Defne's death resets the combo (user decision 2026-10-25).
    /// </summary>
    public sealed class ComboScoreTests
    {
        private readonly List<GameObject> _spawned = new List<GameObject>();

        [TearDown]
        public void TearDown()
        {
            for (int i = 0; i < _spawned.Count; i++)
                if (_spawned[i] != null) Object.Destroy(_spawned[i]);
            _spawned.Clear();
            RunRestart.ResetStatics();
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
            plane.transform.localScale = new Vector3(5f, 1f, 5f);
            var surface = root.AddComponent<NavMeshSurface>();
            surface.collectObjects = CollectObjects.Children;
            surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
            surface.BuildNavMesh();
        }

        private Enemy BuildEnemyTemplate()
        {
            GameObject go = Spawn("TestChaser", new Vector3(0f, -50f, 0f));
            go.SetActive(false);
            var model = new GameObject("Model");
            model.transform.SetParent(go.transform, false);
            var capsule = go.AddComponent<CapsuleCollider>();
            capsule.center = new Vector3(0f, 0.7f, 0f);
            capsule.height = 1.4f;
            capsule.radius = 0.35f;
            Enemy enemy = go.AddComponent<Enemy>();
            go.AddComponent<ChaserBrain>();
            return enemy;
        }

        [UnityTest]
        public IEnumerator DefneKill_PaysMultiplier_ShockwaveKill_PaysFlat()
        {
            // Arrange: arena with two chasers, Defne's Health far from the doors, score wired to the combo.
            BuildFloor();
            GameObject player = Spawn("Player", Vector3.zero);
            player.AddComponent<Health>();
            Transform[] doors =
            {
                Spawn("North", new Vector3(0f, 0f, 12f)).transform,
                Spawn("South", new Vector3(0f, 0f, -12f)).transform,
            };
            EnemyPool pool = Spawn("Pool", Vector3.zero).AddComponent<EnemyPool>();
            pool.Configure(BuildEnemyTemplate());
            WaveDirector waves = Spawn("WaveDirector", Vector3.zero).AddComponent<WaveDirector>();
            var windows = new List<WaveDirector.WaveWindow> { new WaveDirector.WaveWindow(0f, 2, 0, 0, 2) };
            waves.Configure(player.transform, doors, null, pool, pool, pool, windows, beginOnStart: false);

            GameObject scoreGo = Spawn("Score", Vector3.zero);
            ComboTracker combo = scoreGo.AddComponent<ComboTracker>();
            combo.Configure(waves, player.GetComponent<Health>());
            ScoreKeeper score = scoreGo.AddComponent<ScoreKeeper>();
            score.Configure(waves);
            score.ConfigureCombo(combo);

            waves.Begin();
            float waited = 0f;
            while (waves.ActiveCount < 2 && waited < 6f)
            {
                waited += Time.deltaTime;
                yield return null;
            }
            Assert.AreEqual(2, waves.ActiveCount, "both chasers spawned");

            var fromDefne = new DamageInfo(10f, Vector3.zero, Vector3.forward, player, DamageKind.Bullet);
            for (int i = 0; i < 8; i++) combo.Counter.RegisterHit(fromDefne); // combo 8 (×1.1)
            Enemy first = waves.ActiveEnemies[0];

            // Act 1: Defne's lethal crit hit → hit 9 (×1.1: crit bonus 25 × 1.1 = 27.5 → 28), kill 10 → ×1.2 → 120.
            first.Health.TakeDamage(new DamageInfo(1000f, Vector3.zero, Vector3.forward, player, DamageKind.Bullet, true));

            // Assert 1
            Assert.AreEqual(10, combo.Count);
            Assert.AreEqual(148, score.Score, "crit bonus 28 at ×1.1, then the kill crossing the ×1.2 step pays 120");

            // Act 2: revive shockwave clears the rest.
            waves.KillAll();

            // Assert 2
            Assert.AreEqual(248, score.Score, "shockwave kill pays a flat 100");
            Assert.AreEqual(10, combo.Count, "shockwave kills do not count");

            // Act 3: Defne loses health but lives (user decision 2026-10-25: the combo survives damage).
            Health defne = player.GetComponent<Health>();
            defne.InvulnerabilityAfterHit = 0f;
            defne.TakeDamage(new DamageInfo(5f, Vector3.zero, Vector3.forward, null, DamageKind.Contact));

            // Assert 3
            Assert.AreEqual(10, combo.Count, "survivable damage keeps the combo");

            // Act 4: Defne dies.
            defne.TakeDamage(new DamageInfo(1000f, Vector3.zero, Vector3.forward, null, DamageKind.Contact));

            // Assert 4
            Assert.AreEqual(0, combo.Count, "death resets the combo");
            yield return null;
        }
    }
}
