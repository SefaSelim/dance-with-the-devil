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
        public const string IntroGoal = "Dayanabildiğin kadar dayan. Arkası kesilmez.";

        // §8 Devil lines
        public const string DevilFirstAppearance = "Pışt! Sana diyorum. Evet, ekrandaki sen.";
        public const string DevilNoAtFirstDeath = "Daha ilk seferden hayır mı? Amma sıkıcısın.";
        public const string DevilNoLater = "Şimdi mi hayır diyorsun? Aldıklarım bende kalıyor ama.";
        public const string DevilAlwaysYes = "Bir şey olmadı, değil mi?";

        // §8 contract card (K-2, K-3; "Bedel: görünür / mekanik" columns)
        public const string CardGive = "Sana:";
        public const string CardTake = "Ondan:";
        public const string CardCostVisible = "Görürsün:";
        public const string CardCostMechanical = "Sayılarla:";
        public const string Yes = "EVET";
        public const string No = "HAYIR";
        // EVET needs a deliberate hold (never E, which is the rocket); HAYIR is H or a click.
        public const string YesKeys = "[Enter] basılı tut";
        public const string NoKeys = "[H] basılı tut";
        // The stakes, stated plainly under each choice.
        public const string YesStakes = "dirilirsin · bedeli kalıcı";
        public const string NoStakes = "oyun biter";
        // Devil speech bubble: how to go on.
        public const string LineContinue = "[Enter] devam";

        // §11 endings
        public const string KeptTitle = "Kaybettin ama kendin kaldın.";
        public const string KeptBody = "Defne olduğu yere çöktü, nefes nefese. Saçları hâlâ kıvırcık, hâlâ rengârenk.";
        public const string KeptClosing = "Yarın bir şeye hayır diyecek. Belki ikisine.";
        public const string DiminishedTitle = "Eksildin ama hâlâ ayaktasın.";
        public const string DiminishedYesCountFormat = "{0} kere evet dedin.";
        public const string DiminishedGone = "Defne'den gidenler:";
        public const string DiminishedClosing = "Hayır biraz geç geldi. Ama geldi.";
        public const string AlwaysYesTitle = "Oysa hayır da diyebilirdin.";
        public const string Retry = "Bir daha dene";
        public const string RetryKeys = "[R]";

        // §11 ledger
        public const string LedgerTaken = "Aldıkların";
        public const string LedgerGiven = "Verdiklerin";
        public const string LedgerGivenNote = "Defne'den";
        public const string LedgerEmpty = "Aldıkların: hiçbir şey · Verdiklerin: hiçbir şey. Kendin kaldın.";
        public const string SmallNosFormat = "Küçük hayırların: {0}";
        public const string ScoreFormat = "Puan: {0}";
        public const string Separator = " · ";

        // §12 HUD (labels use the GDD §4 names of the actions)
        public const string HudScoreFormat = "Puan {0}";
        public const string HudRocket = "Roket [E]";
        public const string HudMachete = "Pala [F]";
        public const string HudDodge = "Kaçış [Space]";
        // TODO(Burak): call the reject "hayır" during play (F15)? Writer pass 2026-10-24 uses "Hayır"; confirm.
        public const string HudReject = "Hayır [Sağ tık]";
        // Costs taken so far (same word as the results ledger, GDD §11).
        public const string HudGiven = "Verdiklerin";
        public const string HudSmallNosFormat = "Küçük hayırlar: {0}";
        // Empty module slot (the module has never been offered yet).
        public const string HudEmptySlot = "—";
        // GDD §7: a duplicate offer shows "TAKILI".
        public const string HudEquippedDuplicate = "TAKILI";
        // Q held: the highlighted slot.
        public const string HudEjectHint = "Sağ tık: söküp at";

        // Pause (Esc).
        public const string PauseTitle = "Mola";
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
        public const string SettingsMaster = "Genel ses";
        public const string SettingsMusic = "Müzik";
        public const string SettingsSfx = "Efektler";
        public const string SettingsShake = "Ekran sarsıntısı";
        public const string SettingsFullscreen = "Tam ekran";
        public const string SettingsOn = "Açık";
        public const string SettingsOff = "Kapalı";
        public const string CreditsTitle = "Emeği Geçenler";
        public const string CreditsBody =
            "Dance with the Devil ekibi — Women Game Jam 2026\n\n" +
            "Mekân: Synty POLYGON Dark Fantasy\n" +
            "Arayüz: Synty Interface Dark Fantasy HUD\n" +
            "Yazı tipleri: Pirata One, Markazi Text (SIL OFL)\n" +
            "Animasyonlar: Adobe Mixamo · Silahlar: Low Poly Weapons VOL.1\n" +
            "Efekt dokuları: Kenney (CC0)\n" +
            "Şeytan çizimi (geçici): Gustave Doré gravürü (kamu malı)\n" +
            "Sesler ve müzik: oyunun içinde üretildi";

        // §12 first-minute hints (first GDD §11 "İlk dakika önerisi"), one at a time.
        public const string HintMoveFire = "WASD: yürü · Fare: nişan al · Sol tık: ateş et";
        public const string HintOffer = "Üstüne git, bekle: kabul · Sağ tık: hayır de";
        public const string HintEject = "Q'yu basılı tut + Sağ tık: modülü söküp fırlat";
        public const string HintRocket = "E: roket at · F: pala savur · Space: kaç";

        // §7 whisper modules: name, advantage, cost, whisper line.
        public static string ModuleName(ModuleType type) => type switch
        {
            ModuleType.TripleShot => "Üçlü Atış",
            ModuleType.Echo => "Yankı",
            ModuleType.Haste => "Hız",
            ModuleType.CritChance => "Keskin Göz",
            ModuleType.Pierce => "Delici",
            _ => "Yoğunluk",
        };

        public static string ModuleAdvantage(ModuleType type) => type switch
        {
            ModuleType.TripleShot => "Tek atışta 3 mermi (±12°), hepsi tam hasar",
            ModuleType.Echo => "Mermiler duvardan bir kez seker",
            ModuleType.Haste => "Tüfek atış hızı ×1,4 (6 → 8,4 atış/sn)",
            ModuleType.CritChance => "Tüfekte +%20 kritik şansı",
            ModuleType.Pierce => "Mermi 2 düşmanı daha deler (tüfek)",
            _ => "Mermi hasarı ×1,6",
        };

        public static string ModuleCost(ModuleType type) => type switch
        {
            ModuleType.TripleShot => "Her atış seni nişanın tersine iter",
            ModuleType.Echo => "Seken mermi sana da çarpar (yarım hasar), 2. duvarda söner",
            ModuleType.Haste => "Mermiler nişandan rastgele ±6° sapar",
            ModuleType.CritChance => "Ateş ederken yavaşlarsın (yürüme 6 → 5,1 m/sn)",
            ModuleType.Pierce => "Her delişte mermi %30 zayıflar",
            _ => "Gövden ×1,5 büyür, daha kolay vurulursun",
        };

        public static string ModuleWhisper(ModuleType type) => type switch
        {
            ModuleType.TripleShot => "Bir tetik, üç mermi. Azıcık teper, o kadar.",
            ModuleType.Echo => "Duvardan seker. Arada bir sana döner. Bir şey olmaz.",
            ModuleType.Haste => "Daha hızlı sık. Nereye mi gider? Aşağı yukarı oraya.",
            ModuleType.CritChance => "Gözünü kıs, tam ortadan vur. Biraz ağır yürürsün, bir şey olmaz.",
            ModuleType.Pierce => "Bir mermi, üç düşman. Sondaki biraz gıdıklanır, o kadar.",
            _ => "Daha sert vur. Biraz irileşirsin, kimse fark etmez.",
        };
    }
}
