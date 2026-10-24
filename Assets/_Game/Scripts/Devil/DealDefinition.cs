using UnityEngine;

namespace DanceWithTheDevil
{
    /// <summary>
    /// One of the Devil's death contracts (MAIN_GDD §8) with everything the card and the results ledger show (§11).
    /// The mechanics are applied by <see cref="DealEffects"/> by <see cref="Number"/>; this asset holds the data
    /// that can be edited without code (texts, max-health change, whether the player is asked at all).
    /// <see cref="DealLibrary.CreateDefaults"/> builds the six GDD deals in memory so the scene works without assets.
    /// </summary>
    [CreateAssetMenu(menuName = "Dance with the Devil/Deal", fileName = "deal_")]
    public sealed class DealDefinition : ScriptableObject
    {
        [Tooltip("Contract number 1-6 (GDD §8 table). Selects the mechanical effect in DealEffects.")]
        [SerializeField, Range(1, 6)] private int _number = 1;
        [Tooltip("The Devil's offer line (GDD §8 'Şeytan'ın teklifi'). Line breaks allowed.")]
        [SerializeField, TextArea] private string _offerLine = "";

        [Header("Card: Sana (upgrade)")]
        [Tooltip("Full mechanical upgrade shown after 'Sana:' (GDD §8 'Vaat').")]
        [SerializeField, TextArea] private string _upgradeText = "";

        [Header("Card: Ondan (cost, K-3: poetic name + full effect)")]
        [Tooltip("Poetic cost name, second person (e.g. 'Rengin').")]
        [SerializeField] private string _costName = "";
        [Tooltip("Visible cost (GDD §8 'Bedel: görünür').")]
        [SerializeField, TextArea] private string _costVisibleText = "";
        [Tooltip("Mechanical cost (GDD §8 'Bedel: mekanik').")]
        [SerializeField, TextArea] private string _costMechanicalText = "";

        [Header("Ledger (GDD §11)")]
        [Tooltip("'Aldıkların' column entry.")]
        [SerializeField] private string _ledgerTaken = "";
        [Tooltip("What Defne gave, third person (e.g. 'Rengi'). Also used in the 'Defne'den gidenler' list.")]
        [SerializeField] private string _ledgerGivenName = "";
        [Tooltip("Optional mechanical note after the given name (e.g. 'azami can −20').")]
        [SerializeField] private string _ledgerGivenDetail = "";

        [Header("Rules")]
        [Tooltip("Added to max health when accepted (GDD §8: −20 on contracts 2 and 4). Losses stack and are permanent.")]
        [SerializeField] private float _maxHealthDelta;
        [Tooltip("False = the Devil does not ask (contract 6: no EVET/HAYIR).")]
        [SerializeField] private bool _offersChoice = true;

        public int Number => _number;
        public string OfferLine => _offerLine;
        public string UpgradeText => _upgradeText;
        public string CostName => _costName;
        public string CostVisibleText => _costVisibleText;
        public string CostMechanicalText => _costMechanicalText;
        public string LedgerTaken => _ledgerTaken;
        public string LedgerGivenName => _ledgerGivenName;
        public string LedgerGivenDetail => _ledgerGivenDetail;
        public float MaxHealthDelta => _maxHealthDelta;
        public bool OffersChoice => _offersChoice;

        /// <summary>The 'Verdiklerin' line: name, plus " · detail" when there is one.</summary>
        public string LedgerGiven => string.IsNullOrEmpty(_ledgerGivenDetail)
            ? _ledgerGivenName
            : _ledgerGivenName + GameTexts.Separator + _ledgerGivenDetail;

        /// <summary>Builds an in-memory deal (defaults, tests). Destroy it when no longer needed.</summary>
        public static DealDefinition Create(int number, string offerLine, string upgradeText, string costName,
            string costVisibleText, string costMechanicalText, string ledgerTaken, string ledgerGivenName,
            string ledgerGivenDetail, float maxHealthDelta, bool offersChoice)
        {
            var deal = CreateInstance<DealDefinition>();
            deal.name = "deal_" + number;
            deal._number = number;
            deal._offerLine = offerLine;
            deal._upgradeText = upgradeText;
            deal._costName = costName;
            deal._costVisibleText = costVisibleText;
            deal._costMechanicalText = costMechanicalText;
            deal._ledgerTaken = ledgerTaken;
            deal._ledgerGivenName = ledgerGivenName;
            deal._ledgerGivenDetail = ledgerGivenDetail;
            deal._maxHealthDelta = maxHealthDelta;
            deal._offersChoice = offersChoice;
            return deal;
        }
    }
}
