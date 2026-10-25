namespace DanceWithTheDevil
{
    /// <summary>
    /// Combo and world popup strings (placeholders; the writer polishes them). Kept apart from GameTexts, which another
    /// lane owns. Each is a <see cref="TextEntry"/> the designer can override in the Excel table (see <see cref="Texts"/>).
    /// </summary>
    public static class ComboTexts
    {
        private static readonly TextEntry _title = new TextEntry("combo.title", "KOMBO",
            "Eski kombo sayacının başlığı (şu an oyunda kapalı).");
        private static readonly TextEntry _multiplierFormat = new TextEntry("combo.multiplier", "×{0},{1}",
            "Eski kombo sayacında çarpan (şu an kapalı). {0} = tam kısmı, {1} = ondalık hanesi (\"×1,5\").");
        private static readonly TextEntry _scoreWithMultiplierFormat = new TextEntry("combo.score_with_multiplier",
            "{0}  <color=#FFD24D>×{1},{2}</color>",
            "Oyun ekranı sağ üst: kombo çarpanı varken puan satırı. {0} = puan yazısı (\"Puan 1200\"), " +
            "{1} = çarpanın tam kısmı, {2} = ondalık hanesi. <color> etiketi rengi verir, silme.");
        private static readonly TextEntry _brokenByDamage = new TextEntry("combo.broken_damage", "KIRILDI",
            "Eski kombo sayacı: Defne vurulunca (şu an kapalı).");
        private static readonly TextEntry _brokenByTimeout = new TextEntry("combo.broken_timeout", "SÖNDÜ",
            "Eski kombo sayacı: süre dolunca (şu an kapalı).");
        private static readonly TextEntry _popupCritFormat = new TextEntry("combo.popup_crit", "KRİTİK! {0}",
            "Kritik vuruşta düşmanın üstünde beliren yazı. {0} = verilen hasar.");
        private static readonly TextEntry _popupComboFormat = new TextEntry("combo.popup_combo", "{0} KOMBO",
            "Her 5 komboda Defne'nin üstünde beliren yazı. {0} = kombo sayısı.");
        private static readonly TextEntry _popupTierFormat = new TextEntry("combo.popup_tier", "{0} KOMBO!\n×{1},{2}",
            "Kombo eşiğinde (10, 25, 50, 100) Defne'nin üstünde büyük yazı, iki satır. {0} = kombo sayısı, " +
            "{1} = çarpanın tam kısmı, {2} = ondalık hanesi.");

        /// <summary>Small caption above the count (old ComboHud counter, off by default).</summary>
        public static string Title => _title.Text;
        /// <summary>Multiplier: {0} whole part, {1} tenths (Turkish decimal comma: "×1,5").</summary>
        public static string MultiplierFormat => _multiplierFormat.Text;
        /// <summary>Score line in the HUD with the live multiplier: {0} the score line, {1} whole, {2} tenths.</summary>
        public static string ScoreWithMultiplierFormat => _scoreWithMultiplierFormat.Text;
        /// <summary>Shown as the counter falls after Defne lost health (old ComboHud).</summary>
        public static string BrokenByDamage => _brokenByDamage.Text;
        /// <summary>Shown as the counter fades after the window ran out (old ComboHud).</summary>
        public static string BrokenByTimeout => _brokenByTimeout.Text;

        /// <summary>World popup on a critical hit: {0} the damage dealt.</summary>
        public static string PopupCritFormat => _popupCritFormat.Text;
        /// <summary>World popup over Defne every 5 combo: {0} the count.</summary>
        public static string PopupComboFormat => _popupComboFormat.Text;
        /// <summary>World popup over Defne at a tier (10/25/50/100): {0} count, {1} whole, {2} tenths of the multiplier.</summary>
        public static string PopupTierFormat => _popupTierFormat.Text;
    }
}
