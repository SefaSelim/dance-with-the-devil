namespace DanceWithTheDevil
{
    /// <summary>
    /// The six death contracts of MAIN_GDD §8 in order, with the §11 ledger labels. Card texts follow the §8 table;
    /// where the implementation uses a provisional value for an open question, the card shows the implemented
    /// number so nothing is hidden (K-3): contract 1 radius 2,5 → 3,5 m (A-9, area ×2). The texts are
    /// <see cref="TextEntry"/> rows (keys <c>deal.N.*</c>) the designer can override in the Excel table; they are read
    /// when the deals are built (once per run).
    /// </summary>
    public static class DealLibrary
    {
        public const int DealCount = 6;

        // Per deal, in DealDefinition.Create order: offer, upgrade, cost name, visible cost, mechanical cost,
        // ledger taken, ledger given name, ledger given detail.
        private const int Offer = 0, Upgrade = 1, CostName = 2, CostVisible = 3, CostMechanical = 4, LedgerTaken = 5,
            LedgerGivenName = 6, LedgerGivenDetail = 7;

        private const string OfferNote = "Şeytan'ın konuşma balonu: bu sözleşmenin kartı açılırken söylediği teklif.";
        private const string UpgradeNote = "Şeytan kartı: \"Sana:\" satırı (vaat, tam sayılarıyla).";
        private const string CostNameNote = "Şeytan kartı: \"Ondan:\" başlığından sonra bedelin kısa adı.";
        private const string CostVisibleNote = "Şeytan kartı: \"Görürsün:\" satırı (bedelin görünen etkisi).";
        private const string CostMechanicalNote = "Şeytan kartı: \"Sayılarla:\" satırı (bedelin sayısal etkisi).";
        private const string LedgerTakenNote = "Son ekranı defteri: \"Aldıkların\" sütununda bu sözleşmenin satırı.";
        private const string LedgerGivenNameNote =
            "Son ekranı defteri: \"Verdiklerin\" sütunu; eksildin sonunda \"Defne'den gidenler\" listesinde de.";
        private const string LedgerGivenDetailNote =
            "Son ekranı defteri: \"Verdiklerin\" satırında addan sonra \" · \" ile eklenen not (boş olabilir).";

        private static readonly TextEntry[] _deal1 =
        {
            new TextEntry("deal.1.offer",
                "Sana iki kat güçlü roket, hem de daha sık. Ondan mı? Biraz renk. Fark etmez bile.", OfferNote),
            // A-9 open: area ×2 (radius ×1,414) is the provisional choice, see DealEffects.
            new TextEntry("deal.1.upgrade",
                "Roket hasarı iki katı (40 → 80), patlama alanı iki katı (yarıçap 2,5 → 3,5 m), dolum %40 hızlı (6,0 → 4,3 sn)",
                UpgradeNote),
            new TextEntry("deal.1.cost_name", "Rengi", CostNameNote),
            new TextEntry("deal.1.cost_visible", "Dünya %30 solar, Defne'nin rengi çekilir.", CostVisibleNote),
            new TextEntry("deal.1.cost_mechanical", "Hiçbir şey.", CostMechanicalNote),
            new TextEntry("deal.1.ledger_taken", "Roket gücü", LedgerTakenNote),
            new TextEntry("deal.1.ledger_given", "Rengi", LedgerGivenNameNote),
            new TextEntry("deal.1.ledger_given_detail", "", LedgerGivenDetailNote),
        };

        private static readonly TextEntry[] _deal2 =
        {
            new TextEntry("deal.2.offer",
                "Sana pala dalgası, bir kaçış hakkı daha. Ondan da sesini alıyorum. Nasılsa hep susuyordu.", OfferNote),
            new TextEntry("deal.2.upgrade", "Pala savurunca öne bir dalga da çıkar; kaçış hakkı 1 → 2", UpgradeNote),
            new TextEntry("deal.2.cost_name", "Sesi", CostNameNote),
            new TextEntry("deal.2.cost_visible",
                "Defne'nin sesi ve müziğin bir kısmı susar (uyarı sesleri kalır); can çubuğu kısalır.", CostVisibleNote),
            new TextEntry("deal.2.cost_mechanical", "Can sınırı 100 → 80", CostMechanicalNote),
            new TextEntry("deal.2.ledger_taken", "Pala dalgası, +1 kaçış", LedgerTakenNote),
            new TextEntry("deal.2.ledger_given", "Sesi", LedgerGivenNameNote),
            new TextEntry("deal.2.ledger_given_detail", "can sınırı −20", LedgerGivenDetailNote),
        };

        private static readonly TextEntry[] _deal3 =
        {
            new TextEntry("deal.3.offer",
                "Mermilerin delip geçsin, arada bir de çifte vursun. Ondan da arkadaşlarını alıyorum. Zaten onlara hiç vakti yoktu.",
                OfferNote),
            new TextEntry("deal.3.upgrade",
                "Tüfek mermileri 2 düşmanı daha delip geçer (kalkanı delmez); bütün silahlarda +%15 kritik şansı",
                UpgradeNote),
            new TextEntry("deal.3.cost_name", "Arkadaşları", CostNameNote),
            new TextEntry("deal.3.cost_visible", "Ekrandaki küçük fotoğraf kaybolur.", CostVisibleNote),
            new TextEntry("deal.3.cost_mechanical", "Düşmanlar %15 daha hızlı", CostMechanicalNote),
            new TextEntry("deal.3.ledger_taken", "Delen mermi, +%15 kritik", LedgerTakenNote),
            new TextEntry("deal.3.ledger_given", "Arkadaşları", LedgerGivenNameNote),
            new TextEntry("deal.3.ledger_given_detail", "düşmanlar %15 daha hızlı", LedgerGivenDetailNote),
        };

        private static readonly TextEntry[] _deal4 =
        {
            new TextEntry("deal.4.offer",
                "Biraz daha sert, biraz daha seri. Ondan yüzünü alıyorum. Aynaya bakmıyordu nasılsa.", OfferNote),
            new TextEntry("deal.4.upgrade",
                "Bütün hasar %10, saldırı hızı %25 artar (tüfek saniyede 6 → 7,5 atış, pala beklemesi 0,5 → 0,4 sn)",
                UpgradeNote),
            new TextEntry("deal.4.cost_name", "Yüzü", CostNameNote),
            new TextEntry("deal.4.cost_visible",
                "Defne'nin yüzünden parçalar kopar, geri gelmez (vurulma alanı küçülmez).", CostVisibleNote),
            new TextEntry("deal.4.cost_mechanical", "Can sınırı 80 → 60", CostMechanicalNote),
            new TextEntry("deal.4.ledger_taken", "+%10 hasar, +%25 saldırı hızı", LedgerTakenNote),
            new TextEntry("deal.4.ledger_given", "Yüzü", LedgerGivenNameNote),
            new TextEntry("deal.4.ledger_given_detail", "can sınırı −20", LedgerGivenDetailNote),
        };

        private static readonly TextEntry[] _deal5 =
        {
            new TextEntry("deal.5.offer",
                "Bir roket daha. Ondan da zamanını alıyorum. Nasılsa kendine hiç ayırmıyordu.", OfferNote),
            new TextEntry("deal.5.upgrade", "Bir roket hakkı daha (2 → 3)", UpgradeNote),
            new TextEntry("deal.5.cost_name", "Zamanı", CostNameNote),
            new TextEntry("deal.5.cost_visible", "Ekranın kenarları kararır.", CostVisibleNote),
            new TextEntry("deal.5.cost_mechanical",
                "Dirilince koruma süresi yok; kaçış beklemesi iki katı (1,6 → 3,2 sn)", CostMechanicalNote),
            new TextEntry("deal.5.ledger_taken", "+1 roket", LedgerTakenNote),
            new TextEntry("deal.5.ledger_given", "Zamanı", LedgerGivenNameNote),
            new TextEntry("deal.5.ledger_given_detail", "dirilince koruma yok, kaçış beklemesi ×2", LedgerGivenDetailNote),
        };

        // A-4 open: provisional "grey + delayed short play, the next death ends" (see DevilDirector).
        private static readonly TextEntry[] _deal6 =
        {
            new TextEntry("deal.6.offer", "Son anlaşma: ondan geriye ne kaldıysa.\nBu sefer sormuyorum.",
                "Şeytan'ın konuşma balonu: son sözleşme (soru sorulmaz, EVET/HAYIR yok). İki satır."),
            new TextEntry("deal.6.upgrade", "—", UpgradeNote + " Son sözleşmede vaat yok."),
            new TextEntry("deal.6.cost_name", "Kendisi", CostNameNote),
            new TextEntry("deal.6.cost_visible", "Defne griye döner.", CostVisibleNote),
            new TextEntry("deal.6.cost_mechanical", "Tuşlar 0,25 sn geç tepki verir", CostMechanicalNote),
            new TextEntry("deal.6.ledger_taken", "Hiçbir şey", LedgerTakenNote),
            new TextEntry("deal.6.ledger_given", "Kendisi", LedgerGivenNameNote),
            new TextEntry("deal.6.ledger_given_detail", "", LedgerGivenDetailNote),
        };

        /// <summary>New in-memory instances every call (caller owns and destroys them).</summary>
        public static DealDefinition[] CreateDefaults()
        {
            return new[]
            {
                Create(1, _deal1, 0f, true),
                Create(2, _deal2, -20f, true),
                Create(3, _deal3, 0f, true),
                Create(4, _deal4, -20f, true),
                Create(5, _deal5, 0f, true),
                Create(6, _deal6, 0f, false),
            };
        }

        private static DealDefinition Create(int number, TextEntry[] t, float maxHealthDelta, bool offersChoice)
        {
            return DealDefinition.Create(number, t[Offer].Text, t[Upgrade].Text, t[CostName].Text,
                t[CostVisible].Text, t[CostMechanical].Text, t[LedgerTaken].Text, t[LedgerGivenName].Text,
                t[LedgerGivenDetail].Text, maxHealthDelta, offersChoice);
        }
    }
}
