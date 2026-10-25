using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace DanceWithTheDevil.Tests
{
    /// <summary>Combo rules (production/design/combo-haste.md §2, §4, §8; user decision 2026-10-25: only death resets) on the scene-free <see cref="ComboCounter"/>.</summary>
    public sealed class ComboTrackerTests
    {
        private GameObject _player;
        private GameObject _playerGun;
        private GameObject _waves;
        private GameObject _enemy;
        private ComboCounter _combo;

        [SetUp]
        public void SetUp()
        {
            _player = new GameObject("Player");
            _playerGun = new GameObject("Gun");
            _playerGun.transform.SetParent(_player.transform, false);
            _waves = new GameObject("WaveDirector");
            _enemy = new GameObject("Shooter");
            _combo = new ComboCounter(3f) { IgnoredSource = _waves, CreditedRoot = _player.transform };
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_player);
            Object.DestroyImmediate(_waves);
            Object.DestroyImmediate(_enemy);
        }

        private static DamageInfo From(GameObject source, float amount = 10f) =>
            new DamageInfo(amount, Vector3.zero, Vector3.forward, source, DamageKind.Bullet);

        private void Hits(int n)
        {
            for (int i = 0; i < n; i++) _combo.RegisterHit(From(_player));
        }

        [Test]
        public void HitAndKill_EachAddOne()
        {
            // Act
            _combo.RegisterHit(From(_player));
            _combo.RegisterHit(From(_playerGun));
            _combo.RegisterKill(From(_player));

            // Assert
            Assert.AreEqual(3, _combo.Count);
            Assert.AreEqual(1f, _combo.TimeLeft01, 1e-5f);
        }

        [Test]
        public void ZeroDamageEnemySourceOrNullSource_DoNotCount()
        {
            // Act
            bool zero = _combo.RegisterHit(From(_player, 0f));
            bool enemy = _combo.RegisterHit(From(_enemy));
            bool none = _combo.RegisterHit(From(null));

            // Assert
            Assert.IsFalse(zero);
            Assert.IsFalse(enemy);
            Assert.IsFalse(none);
            Assert.AreEqual(0, _combo.Count);
        }

        [Test]
        public void ShockwaveKill_IsIgnored()
        {
            // Arrange
            Hits(4);

            // Act
            bool hit = _combo.RegisterHit(From(_waves, 31f));
            bool kill = _combo.RegisterKill(From(_waves, 31f));

            // Assert
            Assert.IsFalse(hit);
            Assert.IsFalse(kill);
            Assert.AreEqual(4, _combo.Count);
        }

        [Test]
        public void Window_TimesOutAfterThreeSecondsWithEnemiesAlive()
        {
            // Arrange
            var broken = new List<ComboBreak>();
            _combo.Broken += (reason, lost) => broken.Add(reason);
            Hits(3);

            // Act
            _combo.Tick(2.9f, enemiesAlive: 2, frozen: false);
            int before = _combo.Count;
            _combo.Tick(0.2f, enemiesAlive: 2, frozen: false);

            // Assert
            Assert.AreEqual(3, before);
            Assert.AreEqual(0, _combo.Count);
            CollectionAssert.AreEqual(new[] { ComboBreak.Timeout }, broken);
        }

        [Test]
        public void HitBeforeExpiry_RefillsWindow()
        {
            // Arrange
            Hits(1);
            _combo.Tick(2.9f, 1, false);

            // Act
            Hits(1);
            _combo.Tick(2.9f, 1, false);

            // Assert
            Assert.AreEqual(2, _combo.Count);
        }

        [Test]
        public void Window_PausesWhileNoEnemyAliveOrFrozen()
        {
            // Arrange
            Hits(2);

            // Act
            _combo.Tick(10f, enemiesAlive: 0, frozen: false);
            bool pausedEmpty = _combo.TimerPaused;
            _combo.Tick(10f, enemiesAlive: 3, frozen: true);
            bool pausedFrozen = _combo.TimerPaused;
            _combo.Tick(1f, enemiesAlive: 3, frozen: false);

            // Assert
            Assert.IsTrue(pausedEmpty);
            Assert.IsTrue(pausedFrozen);
            Assert.IsFalse(_combo.TimerPaused);
            Assert.AreEqual(2, _combo.Count);
            Assert.AreEqual(2f / 3f, _combo.TimeLeft01, 1e-4f);
        }

        [Test]
        public void PlayerDamaged_Survivable_KeepsComboAndWindow()
        {
            // Arrange (user decision 2026-10-25: damage no longer resets the combo)
            int breaks = 0;
            _combo.Broken += (r, l) => breaks++;
            Hits(12);
            _combo.Tick(1f, enemiesAlive: 2, frozen: false);
            float timeLeft = _combo.TimeLeft;

            // Act
            bool broke = _combo.PlayerDamaged(remainingHealth: 40f);
            _combo.PlayerDamaged(remainingHealth: 1f);

            // Assert
            Assert.IsFalse(broke);
            Assert.AreEqual(0, breaks);
            Assert.AreEqual(12, _combo.Count);
            Assert.AreEqual(timeLeft, _combo.TimeLeft, 1e-5f, "damage neither refills nor drains the window");
            Assert.AreEqual(1.2f, _combo.Multiplier, 1e-5f);
        }

        [Test]
        public void PlayerDamaged_Lethal_ResetsWithDamageReason()
        {
            // Arrange
            ComboBreak reason = ComboBreak.Timeout;
            int lost = -1;
            _combo.Broken += (r, l) => { reason = r; lost = l; };
            Hits(12);

            // Act
            bool broke = _combo.PlayerDamaged(remainingHealth: 0f);

            // Assert
            Assert.IsTrue(broke);
            Assert.AreEqual(0, _combo.Count);
            Assert.AreEqual(ComboBreak.Damage, reason);
            Assert.AreEqual(12, lost);
            Assert.AreEqual(1f, _combo.Multiplier, 1e-5f);
        }

        [Test]
        public void PlayerDied_AtZero_RaisesNothing()
        {
            // Arrange
            int breaks = 0;
            _combo.Broken += (r, l) => breaks++;

            // Act
            bool broke = _combo.PlayerDied();

            // Assert
            Assert.IsFalse(broke);
            Assert.AreEqual(0, breaks);
        }

        [TestCase(10, 25)]
        [TestCase(11, 28)]
        [TestCase(15, 38)]
        [TestCase(20, 50)]
        [TestCase(30, 75)]
        public void CritBonus_TwentyFiveTimesMultiplier_RoundedHalfUp(int tenths, int expected)
        {
            // Act
            int bonus = ScoreKeeper.CritBonus(25, tenths);

            // Assert
            Assert.AreEqual(expected, bonus);
        }

        [TestCase(0, 10)]
        [TestCase(4, 10)]
        [TestCase(5, 11)]
        [TestCase(10, 12)]
        [TestCase(25, 15)]
        [TestCase(49, 19)]
        [TestCase(51, 20)]
        [TestCase(100, 30)]
        [TestCase(250, 30)]
        public void MultiplierTenths_StepsEveryFiveAndCapsAtThree(int count, int expectedTenths)
        {
            // Act
            int tenths = ComboCounter.TenthsFor(count);

            // Assert
            Assert.AreEqual(expectedTenths, tenths);
        }

        [Test]
        public void KillAtFortyNine_PaysTwoHundred()
        {
            // Arrange
            Hits(49);

            // Act: the killing hit makes 50, the kill 51 (design §8)
            _combo.RegisterHit(From(_player));
            _combo.RegisterKill(From(_player));

            // Assert
            Assert.AreEqual(51, _combo.Count);
            Assert.AreEqual(200, 100 * _combo.MultiplierTenths / 10);
        }

        [Test]
        public void TierUp_FiresOnceAtEachMark()
        {
            // Arrange
            var tiers = new List<int>();
            _combo.TierUp += tiers.Add;

            // Act
            Hits(120);

            // Assert
            CollectionAssert.AreEqual(new[] { 1, 2, 3, 4 }, tiers);
            Assert.AreEqual(4, _combo.Tier);
            Assert.AreEqual(3f, _combo.Multiplier, 1e-5f);
        }

        [Test]
        public void TierUp_AfterBreak_FiresAgain()
        {
            // Arrange
            var tiers = new List<int>();
            _combo.TierUp += tiers.Add;
            Hits(10);
            _combo.PlayerDied();

            // Act
            Hits(10);

            // Assert
            CollectionAssert.AreEqual(new[] { 1, 1 }, tiers);
        }
    }
}
