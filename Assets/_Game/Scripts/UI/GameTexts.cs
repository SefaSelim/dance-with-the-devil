namespace DanceWithTheDevil
{
    /// <summary>
    /// Every player-facing Turkish string outside the deal assets, in one place (there is no localization system
    /// yet; when one arrives, these become its keys). Sources: MAIN_GDD §2 (intro), §7 (whispers), §8 (Devil lines),
    /// §11 (endings, ledger), §12 (HUD). Short HUD labels reuse the GDD's own words.
    /// </summary>
    public static class GameTexts
    {
        // §2
        public const string Intro = "Defne bu gece de her şeye evet dedi. \"Bir şey olmaz.\"";

        // §8 Devil lines
        public const string DevilFirstAppearance = "Psst. Sen. Evet, ekrandaki sen.";
        public const string DevilNoAtFirstDeath = "Hayır mı? Hem de ilk seferde. Pek eğlenceli değilsin.";
        public const string DevilNoLater = "Şimdi mi hayır? Aldıklarım bende kalıyor.";
        public const string DevilAlwaysYes = "Bir şey olmadı, değil mi?";

        // §8 contract card (K-2, K-3; "Bedel: görünür / mekanik" columns)
        public const string CardGive = "Sana:";
        public const string CardTake = "Ondan:";
        public const string CardCostVisible = "Görünür:";
        public const string CardCostMechanical = "Mekanik:";
        public const string Yes = "EVET";
        public const string No = "HAYIR";
        public const string YesKeys = "[E]";
        public const string NoKeys = "[H]";

        // §11 endings
        public const string KeptTitle = "Kaybettin ama kendin kaldın.";
        public const string KeptBody = "Defne yere oturdu, nefes nefese. Saçı hâlâ kıvırcık, hâlâ renkli.";
        public const string KeptClosing = "Yarın bir şeye hayır diyecek.";
        public const string DiminishedTitle = "Eksildin ama ayaktasın.";
        public const string DiminishedYesCountFormat = "{0} kez evet dedin.";
        public const string DiminishedGone = "Defne'den gidenler:";
        public const string DiminishedClosing = "Hayır demek geç oldu. Ama oldu.";
        public const string AlwaysYesTitle = "Hayır demek de bir seçenekti.";
        public const string Retry = "Tekrar dene";
        public const string RetryKeys = "[R]";

        // §11 ledger
        public const string LedgerTaken = "Aldıkların";
        public const string LedgerGiven = "Verdiklerin";
        public const string LedgerGivenNote = "Defne'den";
        public const string LedgerEmpty = "Aldıkların: Hiçbir şey · Verdiklerin: Hiçbir şey. Kendin kaldın.";
        public const string SmallNosFormat = "Küçük hayırların: {0}";
        public const string ScoreFormat = "Skor: {0}";
        public const string Separator = " · ";

        // §12 HUD (labels use the GDD §4 names of the actions)
        public const string HudScoreFormat = "Skor {0}";
        public const string HudRocket = "Roket [E]";
        public const string HudDodge = "Kaçınma [Space]";
        public const string HudReject = "Ret [Sağ tık]";

        // §7 whisper modules: name, advantage, cost, whisper line.
        public static string ModuleName(ModuleType type) => type switch
        {
            ModuleType.TripleShot => "Üçlü Atış",
            ModuleType.Echo => "Yankı",
            _ => "Yoğunluk",
        };

        public static string ModuleAdvantage(ModuleType type) => type switch
        {
            ModuleType.TripleShot => "3 mermi, yan açılar ±12°, her biri tam hasar",
            ModuleType.Echo => "Mermi görünür duvardan bir kez seker",
            _ => "Hasar ×1,6",
        };

        public static string ModuleCost(ModuleType type) => type switch
        {
            ModuleType.TripleShot => "Her salvo oyuncuyu nişanın tersine iter",
            ModuleType.Echo => "Sekmiş mermi oyuncuya da vurur (düşman hasarının yarısı); ikinci duvarda söner",
            _ => "Gövde ve isabet yarıçapı ×1,5; hız değişmez",
        };

        public static string ModuleWhisper(ModuleType type) => type switch
        {
            ModuleType.TripleShot => "Bir tetik, üç atış. Biraz geri iter, o kadar.",
            ModuleType.Echo => "Duvardan seker. Bazen sana döner. Bir şey olmaz.",
            _ => "Daha sert vur. Biraz büyürsün, kimse fark etmez.",
        };
    }
}
