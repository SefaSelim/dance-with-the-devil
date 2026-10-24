namespace DanceWithTheDevil
{
    /// <summary>
    /// The six death contracts of MAIN_GDD §8 in order, with the §11 ledger labels. Card texts follow the §8 table;
    /// where the implementation uses a provisional value for an open question, the card shows the implemented
    /// number so nothing is hidden (K-3): contract 1 radius 2,5 → 3,5 m (A-9, area ×2).
    /// </summary>
    public static class DealLibrary
    {
        public const int DealCount = 6;

        /// <summary>New in-memory instances every call (caller owns and destroys them).</summary>
        public static DealDefinition[] CreateDefaults()
        {
            return new[]
            {
                DealDefinition.Create(1,
                    "Sana iki kat güçlü roket, hem de daha sık. Ondan mı? Biraz renk. Fark etmez bile.",
                    // A-9 open: area ×2 (radius ×1,414) is the provisional choice, see DealEffects.
                    "Roket hasarı iki katı (40 → 80), patlama alanı iki katı (yarıçap 2,5 → 3,5 m), dolum %40 hızlı (6,0 → 4,3 sn)",
                    "Rengi",
                    "Dünya %30 solar, Defne'nin rengi çekilir.",
                    "Hiçbir şey.",
                    "Roket gücü", "Rengi", "",
                    0f, true),

                DealDefinition.Create(2,
                    "Sana pala dalgası, bir kaçış hakkı daha. Ondan da sesini alıyorum. Nasılsa hep susuyordu.",
                    "Pala savurunca öne bir dalga da çıkar; kaçış hakkı 1 → 2",
                    "Sesi",
                    "Defne'nin sesi ve müziğin bir kısmı susar (uyarı sesleri kalır); can çubuğu kısalır.",
                    "Can sınırı 100 → 80",
                    "Pala dalgası, +1 kaçış", "Sesi", "can sınırı −20",
                    -20f, true),

                DealDefinition.Create(3,
                    "Mermilerin delip geçsin, arada bir de çifte vursun. Ondan da arkadaşlarını alıyorum. Zaten onlara hiç vakti yoktu.",
                    "Tüfek mermileri 2 düşmanı daha delip geçer (kalkanı delmez); bütün silahlarda +%15 kritik şansı",
                    "Arkadaşları",
                    "Ekrandaki küçük fotoğraf kaybolur.",
                    "Düşmanlar %15 daha hızlı",
                    "Delen mermi, +%15 kritik", "Arkadaşları", "düşmanlar %15 daha hızlı",
                    0f, true),

                DealDefinition.Create(4,
                    "Biraz daha sert, biraz daha seri. Ondan yüzünü alıyorum. Aynaya bakmıyordu nasılsa.",
                    "Bütün hasar %10, saldırı hızı %25 artar (tüfek saniyede 6 → 7,5 atış, pala beklemesi 0,5 → 0,4 sn)",
                    "Yüzü",
                    "Defne'nin yüzünden parçalar kopar, geri gelmez (vurulma alanı küçülmez).",
                    "Can sınırı 80 → 60",
                    "+%10 hasar, +%25 saldırı hızı", "Yüzü", "can sınırı −20",
                    -20f, true),

                DealDefinition.Create(5,
                    "Bir roket daha. Ondan da zamanını alıyorum. Nasılsa kendine hiç ayırmıyordu.",
                    "Bir roket hakkı daha (2 → 3)",
                    "Zamanı",
                    "Ekranın kenarları kararır.",
                    "Dirilince koruma süresi yok; kaçış beklemesi iki katı (1,6 → 3,2 sn)",
                    "+1 roket", "Zamanı", "dirilince koruma yok, kaçış beklemesi ×2",
                    0f, true),

                // A-4 open: provisional "grey + delayed short play, the next death ends" (see DevilDirector).
                DealDefinition.Create(6,
                    "Son anlaşma: ondan geriye ne kaldıysa.\nBu sefer sormuyorum.",
                    "—",
                    "Kendisi",
                    "Defne griye döner.",
                    "Tuşlar 0,25 sn geç tepki verir",
                    "Hiçbir şey", "Kendisi", "",
                    0f, false),
            };
        }
    }
}
