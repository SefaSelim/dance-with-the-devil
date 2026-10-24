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
        // One-line goal under the intro (provisional wording, D2/K-4: waves never stop, death is certain).
        // TODO(Burak): confirm the goal line wording.
        public const string IntroGoal = "Dayanabildiğin kadar dayan. Dalgalar durmaz.";

        // §8 Devil lines
        public const string DevilFirstAppearance = "Psst. Sen. Evet, ekrandaki sen.";
        public const string DevilNoAtFirstDeath = "Hayır mı? Hem de ilk seferde. Pek eğlenceli değilsin.";
        public const string DevilNoLater = "Şimdi mi hayır? Aldıklarım bende kalıyor.";
        public const string DevilAlwaysYes = "Bir şey olmadı, değil mi?";

        // §8 contract card (K-2, K-3; "Bedel: görünür / mekanik" columns)
        public const string CardGive = "Sana:";
        public const string CardTake = "Ondan:";
        public const string CardCostVisible = "Görürsün:";
        public const string CardCostMechanical = "Oyunda:";
        public const string Yes = "EVET";
        public const string No = "HAYIR";
        // EVET needs a deliberate hold (never E, which is the rocket); HAYIR is H or a click.
        public const string YesKeys = "[Enter] basılı tut";
        public const string NoKeys = "[H]";
        // The stakes, stated plainly under each choice.
        public const string YesStakes = "dirilirsin · bedeli kalıcı";
        public const string NoStakes = "koşu biter";
        // Devil speech bubble: how to go on.
        public const string LineContinue = "[Enter] devam";

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
        public const string HudMachete = "Pala [F]";
        public const string HudDodge = "Kaçınma [Space]";
        // TODO(Burak): call the reject "hayır" during play (F15)? The GDD §12 wording is kept until decided.
        public const string HudReject = "Ret [Sağ tık]";
        // Costs taken so far (same word as the results ledger, GDD §11).
        public const string HudGiven = "Verdiklerin";
        public const string HudSmallNosFormat = "Küçük hayırlar: {0}";
        // Empty module slot (the module has never been offered yet).
        public const string HudEmptySlot = "—";
        // GDD §7: a duplicate offer shows "TAKILI".
        public const string HudEquippedDuplicate = "TAKILI";
        // Q held: the highlighted slot.
        public const string HudEjectHint = "Sağ tık: sök";

        // Pause (Esc).
        public const string PauseTitle = "Duraklatıldı";
        public const string PauseResume = "Devam [Esc]";
        public const string PauseQuit = "Ana menü";

        // Main menu (design/ux/main-menu.md §2).
        public const string MenuTitle = "Bir Şey Olmaz";
        public const string MenuSubtitle = "Dance with the Devil";
        public const string MenuStart = "Başla";
        public const string MenuSettings = "Ayarlar";
        public const string MenuCredits = "Emeği Geçenler";
        public const string MenuQuit = "Çıkış";
        public const string MenuFooter = "Hayır demeyi öğrenmelisin.";
        public const string MenuBack = "Geri";
        public const string MenuHint = "Enter: seç · Esc: geri";
        public const string SettingsTitle = "Ayarlar";
        public const string SettingsMaster = "Ana ses";
        public const string SettingsMusic = "Müzik";
        public const string SettingsSfx = "Efektler";
        public const string SettingsShake = "Ekran sarsıntısı";
        public const string SettingsFullscreen = "Tam ekran";
        public const string SettingsOn = "Açık";
        public const string SettingsOff = "Kapalı";
        public const string CreditsTitle = "Emeği Geçenler";
        public const string CreditsBody =
            "Dance with the Devil ekibi — Women Game Jam 2026\n\n" +
            "Çevre: Synty POLYGON Dark Fantasy\n" +
            "Arayüz: Synty Interface Dark Fantasy HUD\n" +
            "Yazı tipleri: Pirata One, Markazi Text (SIL OFL)\n" +
            "Animasyonlar: Adobe Mixamo · Silahlar: Low Poly Weapons VOL.1\n" +
            "Parçacık dokuları: Kenney (CC0)\n" +
            "Şeytan görseli (geçici): Gustave Doré gravürü (kamu malı)\n" +
            "Sesler ve müzik: oyunun içinde sentezlendi";

        // §12 first-minute hints (first GDD §11 "İlk dakika önerisi"), one at a time.
        public const string HintMoveFire = "WASD: yürü · Fare: nişan · Sol tık: ateş";
        public const string HintOffer = "Temasta kal: kabul · Sağ tık: reddet";
        public const string HintEject = "Q basılı + sağ tık: modülü sök ve fırlat";
        public const string HintRocket = "E: roket · F: pala · Space: kaçın";

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
