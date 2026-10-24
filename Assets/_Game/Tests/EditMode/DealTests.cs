using NUnit.Framework;
using UnityEngine;

namespace DanceWithTheDevil.Tests
{
    /// <summary>The six default contracts of MAIN_GDD §8 and the max-health stacking rule.</summary>
    public sealed class DealTests
    {
        private DealDefinition[] _deals;

        [SetUp]
        public void SetUp() => _deals = DealLibrary.CreateDefaults();

        [TearDown]
        public void TearDown()
        {
            for (int i = 0; i < _deals.Length; i++)
                if (_deals[i] != null) Object.DestroyImmediate(_deals[i]);
        }

        [Test]
        public void Defaults_AreSixDealsInOrder()
        {
            Assert.AreEqual(DealLibrary.DealCount, _deals.Length);
            for (int i = 0; i < _deals.Length; i++)
            {
                Assert.AreEqual(i + 1, _deals[i].Number);
                Assert.IsFalse(string.IsNullOrEmpty(_deals[i].OfferLine), $"deal {i + 1} offer line");
                Assert.IsFalse(string.IsNullOrEmpty(_deals[i].CostName), $"deal {i + 1} cost name");
                Assert.IsFalse(string.IsNullOrEmpty(_deals[i].LedgerTaken), $"deal {i + 1} ledger");
            }
            Assert.AreEqual("Rengin", _deals[0].CostName);
            Assert.AreEqual("Kendisi", _deals[5].LedgerGivenName);
        }

        [Test]
        public void MaxHealth_Stacks_100_100_80_80_60_60_60()
        {
            float[] expected = { 100f, 100f, 80f, 80f, 60f, 60f, 60f };
            for (int taken = 0; taken < expected.Length; taken++)
                Assert.AreEqual(expected[taken], DealEffects.MaxHealthAfter(_deals, taken), 1e-4f, $"after {taken} deals");
        }

        [Test]
        public void DealSix_OffersNoChoice_OthersDo()
        {
            for (int i = 0; i < 5; i++) Assert.IsTrue(_deals[i].OffersChoice, $"deal {i + 1}");
            Assert.IsFalse(_deals[5].OffersChoice);
        }
    }
}
