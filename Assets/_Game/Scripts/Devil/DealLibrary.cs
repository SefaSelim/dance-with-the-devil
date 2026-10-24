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
                    "Sana iki kat roket, daha hızlı ateş. Ondan? Biraz renk. Fark etmez bile.",
                    // A-9 open: area ×2 (radius ×1,414) is the provisional choice, see DealEffects.
                    "Roket hasarı ×2 (40 → 80), patlama alanı ×2 (yarıçap 2,5 → 3,5 m), atış hızı +%40 (dolum 6,0 → 4,3 sn)",
                    "Rengin",
                    "Dünya %30 solar, saçındaki renk çekilir.",
                    "Yok.",
                    "Roket gücü", "Rengi", "",
                    0f, true),

                DealDefinition.Create(2,
                    "Sana pala dalgası, bir kaçış hakkı daha. Ondan sesini alıyorum. Zaten susuyordu.",
                    "Pala dalgası açılır; kaçınma şarjı 1 → 2",
                    "Sesin",
                    "Defne'nin sesleri ve bir müzik katmanı susar (oyun işaretleri kalır); can çubuğu kısalır.",
                    "Azami can 100 → 80",
                    "Pala dalgası, +1 kaçınma", "Sesi", "azami can −20",
                    -20f, true),

                DealDefinition.Create(3,
                    "Mermilerin delip geçsin. Ondan arkadaşlarını alıyorum. Onlara zaten vakti yoktu.",
                    "Tüfek mermileri delip geçer (2 ek düşman, kalkanı delmez)",
                    "Arkadaşların",
                    "HUD'daki küçük fotoğraf kaybolur.",
                    "Düşmanlar %15 hızlı",
                    "Delici mermi", "Arkadaşları", "düşmanlar %15 hızlı",
                    0f, true),

                DealDefinition.Create(4,
                    "Yüzde on hasar. Ondan yüzünü alıyorum. Aynaya bakmıyordu nasılsa.",
                    "Tüm hasar +%10",
                    "Yüzün",
                    "Modelden vokseller kalıcı olarak düşer (isabet alanı küçülmez).",
                    "Azami can 80 → 60",
                    "+%10 hasar", "Yüzü", "azami can −20",
                    -20f, true),

                DealDefinition.Create(5,
                    "Bir roket daha. Ondan zamanını alıyorum.",
                    "+1 roket şarjı (2 → 3)",
                    "Zamanın",
                    "Ekran kenarları kararır.",
                    "Dirilişte dokunulmazlık yok; kaçınma beklemesi ×2 (1,6 → 3,2 sn)",
                    "+1 roket", "Zamanı", "dirilişte dokunulmazlık yok, kaçınma beklemesi ×2",
                    0f, true),

                // A-4 open: provisional "grey + delayed short play, the next death ends" (see DevilDirector).
                DealDefinition.Create(6,
                    "Son anlaşma: neyin kaldıysa.\nBu sefer sormuyorum.",
                    "—",
                    "Kendin",
                    "Karakter griye döner.",
                    "Kontroller gecikir (0,25 sn)",
                    "Hiçbir şey", "Kendisi", "",
                    0f, false),
            };
        }
    }
}
