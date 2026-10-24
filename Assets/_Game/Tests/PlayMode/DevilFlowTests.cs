using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace DanceWithTheDevil.Tests
{
    /// <summary>
    /// Death → Devil → EVET/HAYIR flow (MAIN_GDD §3, §8, §11) without UI: lines are skipped with Advance and the
    /// card is answered through ChooseYes/ChooseNo.
    /// </summary>
    public sealed class DevilFlowTests
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

        private (Health, DevilDirector) Build(Vector3 position)
        {
            var player = new GameObject("Player");
            player.transform.position = position;
            _spawned.Add(player);
            Health health = player.AddComponent<Health>();

            var devil = new GameObject("Devil");
            _spawned.Add(devil);
            DevilDirector director = devil.AddComponent<DevilDirector>();
            director.Configure(health);
            return (health, director);
        }

        private static void Kill(Health health)
        {
            Assert.IsTrue(health.TakeDamage(new DamageInfo(1000f, Vector3.zero, Vector3.forward, null, DamageKind.Bullet)));
            Assert.IsTrue(health.IsDead);
        }

        private static IEnumerator WaitForState(DevilDirector director, DevilState state, float timeout = 10f)
        {
            float t = 0f;
            while (director.State != state && t < timeout)
            {
                director.Advance();
                t += Time.unscaledDeltaTime;
                yield return null;
            }
            Assert.AreEqual(state, director.State);
        }

        [UnityTest]
        public IEnumerator DeathOne_Yes_RevivesWithSameMax_ThenDeathTwo_Yes_Max80()
        {
            (Health health, DevilDirector director) = Build(new Vector3(0f, 0f, 200f));

            Kill(health);
            Assert.AreEqual(0f, Time.timeScale, "time freezes at death");
            yield return WaitForState(director, DevilState.Offer);
            Assert.AreEqual(1, director.CurrentDeal.Number);

            director.ChooseYes();
            yield return null;
            Assert.AreEqual(DevilState.Playing, director.State);
            Assert.IsFalse(health.IsDead);
            Assert.AreEqual(1f, Time.timeScale);
            Assert.AreEqual(100f, health.MaxHealth, 1e-4f);
            Assert.AreEqual(100f, health.Current, 1e-4f);
            Assert.IsTrue(health.IsInvulnerable, "2 s revive invulnerability (K-12)");

            yield return new WaitForSeconds(2.1f);
            Kill(health);
            yield return WaitForState(director, DevilState.Offer);
            Assert.AreEqual(2, director.CurrentDeal.Number);

            director.ChooseYes();
            yield return null;
            Assert.IsFalse(health.IsDead);
            Assert.AreEqual(80f, health.MaxHealth, 1e-4f);
            Assert.AreEqual(80f, health.Current, 1e-4f);
            Assert.AreEqual(2, director.DealsTaken);
        }

        [UnityTest]
        public IEnumerator DeathOne_No_EndsKeptYourself()
        {
            (Health health, DevilDirector director) = Build(new Vector3(0f, 0f, 260f));

            Kill(health);
            yield return WaitForState(director, DevilState.Offer);
            director.ChooseNo();
            yield return WaitForState(director, DevilState.Ended);

            Assert.AreEqual(EndingKind.KeptYourself, director.Ending);
            Assert.AreEqual(0, director.DealsTaken);
            Assert.IsTrue(health.IsDead);
        }
    }
}
