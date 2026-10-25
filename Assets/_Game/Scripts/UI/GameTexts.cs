namespace DanceWithTheDevil
{
    /// <summary>
    /// Every player-facing Turkish string outside the deals and the combo popups, in one place. Each text is a
    /// <see cref="TextEntry"/> (key, default, Excel note); the designer overrides it in
    /// <c>Assets/_Game/Texts/Yazilar.xlsx</c> (see <see cref="Texts"/>), the defaults below are what the game shows
    /// until then. Sources: MAIN_GDD §2 (intro), §7 (whispers), §8 (Devil lines), §11 (endings, ledger), §12 (HUD).
    /// Short HUD labels reuse the GDD's own words.
    /// </summary>
    public static class GameTexts
    {
        // §2
        private static readonly TextEntry _intro = new TextEntry("intro.line",
            "Defne bu gece de her şeye evet dedi. \"Bir şey olmaz.\"",
            "Oyunun başında ekranın ortasındaki şeritte çıkan ilk cümle.");
        // One-line goal under the intro (provisional wording, D2/K-4: waves never stop, death is certain).
        // TODO(Burak): confirm the goal line wording.
        private static readonly TextEntry _introGoal = new TextEntry("intro.goal",
            "Dayanabildiğin kadar dayan. Arkası kesilmez.",
            "Giriş cümlesinin altındaki küçük hedef satırı.");

        public static string Intro => _intro.Text;
        public static string IntroGoal => _introGoal.Text;

        // §8 Devil lines
        private static readonly TextEntry _devilFirstAppearance = new TextEntry("devil.first_appearance",
            "Pışt! Sana diyorum. Evet, ekrandaki sen.",
            "Şeytan'ın konuşma balonu: ilk ölümde, ilk kartından önce bir kez.");
        private static readonly TextEntry _devilNoAtFirstDeath = new TextEntry("devil.no_first_death",
            "Daha ilk seferden hayır mı? Amma sıkıcısın.",
            "Şeytan'ın konuşma balonu: ilk kartta HAYIR denince (son ekranından önce).");
        private static readonly TextEntry _devilNoLater = new TextEntry("devil.no_later",
            "Şimdi mi hayır diyorsun? Aldıklarım bende kalıyor ama.",
            "Şeytan'ın konuşma balonu: sonraki bir kartta HAYIR denince.");
        private static readonly TextEntry _devilAlwaysYes = new TextEntry("devil.always_yes",
            "Bir şey olmadı, değil mi?",
            "Şeytan'ın konuşma balonu: bütün sözleşmeler alındıktan sonraki ölümde (hep evet sonu).");

        public static string DevilFirstAppearance => _devilFirstAppearance.Text;
        public static string DevilNoAtFirstDeath => _devilNoAtFirstDeath.Text;
        public static string DevilNoLater => _devilNoLater.Text;
        public static string DevilAlwaysYes => _devilAlwaysYes.Text;

        // §8 contract card (K-2, K-3; "Bedel: görünür / mekanik" columns)
        private static readonly TextEntry _cardGive = new TextEntry("card.give", "Sana:",
            "Şeytan kartı: vaat satırının başındaki altın renkli etiket.");
        private static readonly TextEntry _cardTake = new TextEntry("card.take", "Ondan:",
            "Şeytan kartı: bedel başlığının başı (arkasından bedelin adı gelir, ör. \"Ondan: Rengi\").");
        private static readonly TextEntry _cardCostVisible = new TextEntry("card.cost_visible", "Görürsün:",
            "Şeytan kartı: görünür bedel satırının başı.");
        private static readonly TextEntry _cardCostMechanical = new TextEntry("card.cost_mechanical", "Sayılarla:",
            "Şeytan kartı: sayısal bedel satırının başı.");
        private static readonly TextEntry _yes = new TextEntry("card.yes", "EVET",
            "Şeytan kartı: büyük kabul düğmesi. Kısa tut.");
        private static readonly TextEntry _no = new TextEntry("card.no", "HAYIR",
            "Şeytan kartı: ret düğmesi. Kısa tut.");
        // EVET needs a deliberate hold (never E, which is the rocket); HAYIR is H or a click.
        private static readonly TextEntry _yesKeys = new TextEntry("card.yes_keys", "[Enter] basılı tut",
            "EVET düğmesinin içinde, küçük tuş ipucu.");
        private static readonly TextEntry _noKeys = new TextEntry("card.no_keys", "[H] basılı tut",
            "HAYIR düğmesinin içinde, küçük tuş ipucu.");
        // The stakes, stated plainly under each choice.
        private static readonly TextEntry _yesStakes = new TextEntry("card.yes_stakes", "dirilirsin · bedeli kalıcı",
            "EVET düğmesinin altında: kabul edince ne olur.");
        private static readonly TextEntry _noStakes = new TextEntry("card.no_stakes", "oyun biter",
            "HAYIR düğmesinin altında: reddedince ne olur.");
        // Devil speech bubble: how to go on.
        private static readonly TextEntry _lineContinue = new TextEntry("card.continue", "[Enter] devam",
            "Şeytan'ın konuşma balonunun sağ alt köşesi: devam etme ipucu.");

        public static string CardGive => _cardGive.Text;
        public static string CardTake => _cardTake.Text;
        public static string CardCostVisible => _cardCostVisible.Text;
        public static string CardCostMechanical => _cardCostMechanical.Text;
        public static string Yes => _yes.Text;
        public static string No => _no.Text;
        public static string YesKeys => _yesKeys.Text;
        public static string NoKeys => _noKeys.Text;
        public static string YesStakes => _yesStakes.Text;
        public static string NoStakes => _noStakes.Text;
        public static string LineContinue => _lineContinue.Text;

        // §11 endings
        private static readonly TextEntry _keptTitle = new TextEntry("ending.kept_yourself.title",
            "Kaybettin ama kendin kaldın.",
            "Son ekranı başlığı: ilk kartta HAYIR denince (kendin kaldın sonu).");
        private static readonly TextEntry _keptBody = new TextEntry("ending.kept_yourself.body",
            "Defne olduğu yere çöktü, nefes nefese. Saçları hâlâ kıvırcık, hâlâ rengârenk.",
            "Son ekranı: kendin kaldın sonunda başlığın altındaki paragraf.");
        private static readonly TextEntry _keptClosing = new TextEntry("ending.kept_yourself.closing",
            "Yarın bir şeye hayır diyecek. Belki ikisine.",
            "Son ekranı: kendin kaldın sonunda defterin altındaki kapanış cümlesi.");
        private static readonly TextEntry _diminishedTitle = new TextEntry("ending.diminished.title",
            "Eksildin ama hâlâ ayaktasın.",
            "Son ekranı başlığı: birkaç evetten sonra HAYIR denince (eksildin sonu).");
        private static readonly TextEntry _diminishedYesCountFormat = new TextEntry("ending.diminished.yes_count",
            "{0} kere evet dedin.",
            "Eksildin sonu, paragrafın başı. {0} = kabul edilen sözleşme sayısı.");
        private static readonly TextEntry _diminishedGone = new TextEntry("ending.diminished.gone",
            "Defne'den gidenler:",
            "Eksildin sonu, paragrafta; arkasından giden şeylerin listesi gelir (\"Rengi · Sesi\").");
        private static readonly TextEntry _diminishedClosing = new TextEntry("ending.diminished.closing",
            "Hayır biraz geç geldi. Ama geldi.",
            "Son ekranı: eksildin sonunda defterin altındaki kapanış cümlesi.");
        private static readonly TextEntry _alwaysYesTitle = new TextEntry("ending.always_yes.title",
            "Oysa hayır da diyebilirdin.",
            "Son ekranı başlığı: bütün sözleşmelere evet denince (hep evet sonu).");
        private static readonly TextEntry _retry = new TextEntry("ending.retry", "Bir daha dene",
            "Son ekranı: yeniden başlatma düğmesi (arkasından tuş ipucu gelir).");
        private static readonly TextEntry _retryKeys = new TextEntry("ending.retry_keys", "[R]",
            "Son ekranı: yeniden başlatma düğmesindeki tuş ipucu.");

        public static string KeptTitle => _keptTitle.Text;
        public static string KeptBody => _keptBody.Text;
        public static string KeptClosing => _keptClosing.Text;
        public static string DiminishedTitle => _diminishedTitle.Text;
        public static string DiminishedYesCountFormat => _diminishedYesCountFormat.Text;
        public static string DiminishedGone => _diminishedGone.Text;
        public static string DiminishedClosing => _diminishedClosing.Text;
        public static string AlwaysYesTitle => _alwaysYesTitle.Text;
        public static string Retry => _retry.Text;
        public static string RetryKeys => _retryKeys.Text;

        // §11 ledger
        private static readonly TextEntry _ledgerTaken = new TextEntry("ledger.taken", "Aldıkların",
            "Son ekranı defteri: sol sütun başlığı (Şeytan'dan alınanlar).");
        private static readonly TextEntry _ledgerGiven = new TextEntry("ledger.given", "Verdiklerin",
            "Son ekranı defteri: sağ sütun başlığı (Defne'nin verdikleri).");
        private static readonly TextEntry _ledgerGivenNote = new TextEntry("ledger.given_note", "Defne'den",
            "Son ekranı defteri: sağ sütun başlığının yanındaki küçük not.");
        private static readonly TextEntry _ledgerEmpty = new TextEntry("ledger.empty",
            "Aldıkların: hiçbir şey · Verdiklerin: hiçbir şey. Kendin kaldın.",
            "Son ekranı defteri: hiç evet denmediyse sütunların yerine çıkan tek satır.");
        private static readonly TextEntry _smallNosFormat = new TextEntry("ledger.small_nos", "Küçük hayırların: {0}",
            "Son ekranı: reddedilen ve sökülen modül sayısı. {0} = sayı.");
        private static readonly TextEntry _scoreFormat = new TextEntry("ledger.score", "Puan: {0}",
            "Son ekranının en altında puan. {0} = puan.");
        private static readonly TextEntry _separator = new TextEntry("common.separator", " · ",
            "Liste ayırıcı (boşluklar dahil): defterde, eksildin sonundaki listede, söküm ipucunda.");

        public static string LedgerTaken => _ledgerTaken.Text;
        public static string LedgerGiven => _ledgerGiven.Text;
        public static string LedgerGivenNote => _ledgerGivenNote.Text;
        public static string LedgerEmpty => _ledgerEmpty.Text;
        public static string SmallNosFormat => _smallNosFormat.Text;
        public static string ScoreFormat => _scoreFormat.Text;
        public static string Separator => _separator.Text;

        // §12 HUD (labels use the GDD §4 names of the actions)
        private static readonly TextEntry _hudScoreFormat = new TextEntry("hud.score", "Puan {0}",
            "Oyun ekranı sağ üst: puan. {0} = puan (yanına kombo çarpanı eklenebilir).");
        private static readonly TextEntry _hudRocket = new TextEntry("hud.rocket", "Roket [E]",
            "Oyun ekranı sol alt: roket hakları satırının etiketi. Kısa tut.");
        private static readonly TextEntry _hudMachete = new TextEntry("hud.machete", "Pala [F]",
            "Oyun ekranı sol alt: pala satırının etiketi. Kısa tut.");
        private static readonly TextEntry _hudDodge = new TextEntry("hud.dodge", "Kaçış [Space]",
            "Oyun ekranı sol alt: kaçış hakları satırının etiketi. Kısa tut.");
        // TODO(Burak): call the reject "hayır" during play (F15)? Writer pass 2026-10-24 uses "Hayır"; confirm.
        private static readonly TextEntry _hudReject = new TextEntry("hud.reject", "Hayır [Sağ tık]",
            "Oyun ekranı sol alt: modül reddetme satırının etiketi. Kısa tut.");
        // Costs taken so far (same word as the results ledger, GDD §11).
        private static readonly TextEntry _hudGiven = new TextEntry("hud.given", "Verdiklerin",
            "Oyun ekranı sol üst, can çubuğunun altı: alınan bedellerin simgelerinin etiketi.");
        private static readonly TextEntry _hudSmallNosFormat = new TextEntry("hud.small_nos", "Küçük hayırlar: {0}",
            "Oyun ekranı alt: modül yuvalarının sağında. {0} = reddedilen/sökülen modül sayısı.");
        // Empty module slot (the module has never been offered yet).
        private static readonly TextEntry _hudEmptySlot = new TextEntry("hud.empty_slot", "—",
            "Oyun ekranı alt: boş modül yuvası (üç yuva var).");
        // GDD §7: a duplicate offer shows "TAKILI".
        private static readonly TextEntry _hudEquippedDuplicate = new TextEntry("hud.equipped", "TAKILI",
            "Modül teklif şeridi: aynı modül zaten takılıysa sağda çıkan işaret. Kısa tut.");
        // K-23: an offer that cannot be taken because all three slots are full.
        private static readonly TextEntry _hudSlotsFull = new TextEntry("hud.slots_full", "DOLU",
            "Modül teklif şeridi: üç yuva da doluyken sağda çıkan işaret (önce Q + Sağ tık ile birini sök). Kısa tut.");
        // Q held: the highlighted slot.
        private static readonly TextEntry _hudEjectHint = new TextEntry("hud.eject_hint", "Sağ tık: söküp at",
            "Q basılıyken yuvaların üstünde: \"Modül adı · bu yazı\".");

        public static string HudScoreFormat => _hudScoreFormat.Text;
        public static string HudRocket => _hudRocket.Text;
        public static string HudMachete => _hudMachete.Text;
        public static string HudDodge => _hudDodge.Text;
        public static string HudReject => _hudReject.Text;
        public static string HudGiven => _hudGiven.Text;
        public static string HudSmallNosFormat => _hudSmallNosFormat.Text;
        public static string HudEmptySlot => _hudEmptySlot.Text;
        public static string HudEquippedDuplicate => _hudEquippedDuplicate.Text;
        public static string HudSlotsFull => _hudSlotsFull.Text;
        public static string HudEjectHint => _hudEjectHint.Text;

        // Pause (Esc).
        private static readonly TextEntry _pauseTitle = new TextEntry("pause.title", "Mola",
            "Duraklatma (Esc) panelinin başlığı.");
        private static readonly TextEntry _pauseResume = new TextEntry("pause.resume", "Devam [Esc]",
            "Duraklatma paneli: oyuna dönme düğmesi.");
        private static readonly TextEntry _pauseQuit = new TextEntry("pause.quit", "Ana menü",
            "Duraklatma paneli: ana menüye dönme düğmesi.");

        public static string PauseTitle => _pauseTitle.Text;
        public static string PauseResume => _pauseResume.Text;
        public static string PauseQuit => _pauseQuit.Text;

        // Main menu (design/ux/main-menu.md §2).
        private static readonly TextEntry _menuTitle = new TextEntry("menu.title", "Bir Şey Olmaz",
            "Ana menü: büyük oyun adı.");
        private static readonly TextEntry _menuSubtitle = new TextEntry("menu.subtitle", "Dance with the Devil",
            "Ana menü: oyun adının altındaki kırmızı alt başlık.");
        private static readonly TextEntry _menuStart = new TextEntry("menu.start", "Başla", "Ana menü düğmesi.");
        private static readonly TextEntry _menuSettings = new TextEntry("menu.settings", "Ayarlar", "Ana menü düğmesi.");
        private static readonly TextEntry _menuCredits = new TextEntry("menu.credits", "Emeği Geçenler",
            "Ana menü düğmesi.");
        private static readonly TextEntry _menuQuit = new TextEntry("menu.quit", "Çıkış", "Ana menü düğmesi.");
        private static readonly TextEntry _menuFooter = new TextEntry("menu.footer", "Hayır demeyi öğrenmelisin.",
            "Ana menü: sol alttaki slogan.");
        private static readonly TextEntry _menuBack = new TextEntry("menu.back", "Geri",
            "Ayarlar ve Emeği Geçenler panellerinde geri düğmesi.");
        private static readonly TextEntry _menuHint = new TextEntry("menu.hint", "Enter: seç · Esc: geri",
            "Ana menü: düğmelerin altındaki küçük tuş ipucu.");

        public static string MenuTitle => _menuTitle.Text;
        public static string MenuSubtitle => _menuSubtitle.Text;
        public static string MenuStart => _menuStart.Text;
        public static string MenuSettings => _menuSettings.Text;
        public static string MenuCredits => _menuCredits.Text;
        public static string MenuQuit => _menuQuit.Text;
        public static string MenuFooter => _menuFooter.Text;
        public static string MenuBack => _menuBack.Text;
        public static string MenuHint => _menuHint.Text;

        private static readonly TextEntry _settingsTitle = new TextEntry("settings.title", "Ayarlar",
            "Ayarlar panelinin başlığı.");
        private static readonly TextEntry _settingsMaster = new TextEntry("settings.master", "Genel ses",
            "Ayarlar paneli: kaydırıcı etiketi.");
        private static readonly TextEntry _settingsMusic = new TextEntry("settings.music", "Müzik",
            "Ayarlar paneli: kaydırıcı etiketi.");
        private static readonly TextEntry _settingsSfx = new TextEntry("settings.sfx", "Efektler",
            "Ayarlar paneli: kaydırıcı etiketi.");
        private static readonly TextEntry _settingsShake = new TextEntry("settings.shake", "Ekran sarsıntısı",
            "Ayarlar paneli: kaydırıcı etiketi.");
        private static readonly TextEntry _settingsFullscreen = new TextEntry("settings.fullscreen", "Tam ekran",
            "Ayarlar paneli: tam ekran satırının etiketi.");
        private static readonly TextEntry _settingsOn = new TextEntry("settings.on", "Açık",
            "Ayarlar paneli: tam ekran düğmesi, açıkken.");
        private static readonly TextEntry _settingsOff = new TextEntry("settings.off", "Kapalı",
            "Ayarlar paneli: tam ekran düğmesi, kapalıyken.");

        public static string SettingsTitle => _settingsTitle.Text;
        public static string SettingsMaster => _settingsMaster.Text;
        public static string SettingsMusic => _settingsMusic.Text;
        public static string SettingsSfx => _settingsSfx.Text;
        public static string SettingsShake => _settingsShake.Text;
        public static string SettingsFullscreen => _settingsFullscreen.Text;
        public static string SettingsOn => _settingsOn.Text;
        public static string SettingsOff => _settingsOff.Text;

        private static readonly TextEntry _creditsTitle = new TextEntry("credits.title", "Emeği Geçenler",
            "Emeği Geçenler panelinin başlığı.");
        private static readonly TextEntry _creditsBody = new TextEntry("credits.body",
            "Dance with the Devil ekibi — Women Game Jam 2026\n\n" +
            "Mekân: Synty POLYGON Dark Fantasy\n" +
            "Arayüz: Synty Interface Dark Fantasy HUD\n" +
            "Yazı tipleri: Pirata One, Markazi Text (SIL OFL)\n" +
            "Animasyonlar: Adobe Mixamo · Silahlar: Low Poly Weapons VOL.1\n" +
            "Efekt dokuları: Kenney (CC0)\n" +
            "Şeytan çizimi (geçici): Gustave Doré gravürü (kamu malı)\n" +
            "Sesler ve müzik: oyunun içinde üretildi",
            "Emeği Geçenler paneli: bütün metin. Satır atlamak için Alt+Enter.");

        public static string CreditsTitle => _creditsTitle.Text;
        public static string CreditsBody => _creditsBody.Text;

        // §12 first-minute hints (first GDD §11 "İlk dakika önerisi"), one at a time.
        private static readonly TextEntry _hintMoveFire = new TextEntry("hint.move_fire",
            "WASD: yürü · Fare: nişan al · Sol tık: ateş et",
            "İlk dakika ipucu: oyunun başında, hareket edip ateş edene kadar.");
        private static readonly TextEntry _hintOffer = new TextEntry("hint.offer",
            "Üstüne git, bekle: kabul · Sağ tık: hayır de",
            "İlk dakika ipucu: ilk modül teklifi yerdeyken.");
        private static readonly TextEntry _hintEject = new TextEntry("hint.eject",
            "Q'yu basılı tut + Sağ tık: modülü söküp fırlat",
            "İlk dakika ipucu: ilk modül takıldıktan sonra kısa süre.");
        // K-23: an offer is up but all three slots are full.
        private static readonly TextEntry _hintSlotsFull = new TextEntry("hint.slots_full",
            "Yuvalar dolu · Q basılı + Sağ tık: birini sök",
            "Alt ipucu: teklif geldiğinde üç yuva da doluysa (her seferinde).");
        private static readonly TextEntry _hintRocket = new TextEntry("hint.rocket",
            "E: roket at · F: pala savur · Space: kaç",
            "İlk dakika ipucu: temel hareket öğrenildikten sonra kısa süre.");

        public static string HintMoveFire => _hintMoveFire.Text;
        public static string HintOffer => _hintOffer.Text;
        public static string HintEject => _hintEject.Text;
        public static string HintSlotsFull => _hintSlotsFull.Text;
        public static string HintRocket => _hintRocket.Text;

        // §7 whisper modules: name, advantage, cost, whisper line. Arrays are indexed by ModuleType
        // (TripleShot, Echo, Density, Haste, CritChance, Pierce); keep that order.
        private static readonly TextEntry[] _moduleNames =
        {
            new TextEntry("module.triple_shot.name", "Üçlü Atış", ModuleNameNote),
            new TextEntry("module.echo.name", "Yankı", ModuleNameNote),
            new TextEntry("module.density.name", "Yoğunluk", ModuleNameNote),
            new TextEntry("module.haste.name", "Hız", ModuleNameNote),
            new TextEntry("module.crit_chance.name", "Keskin Göz", ModuleNameNote),
            new TextEntry("module.pierce.name", "Delici", ModuleNameNote),
        };

        private static readonly TextEntry[] _moduleAdvantages =
        {
            new TextEntry("module.triple_shot.advantage", "Tek atışta 3 mermi (±12°), hepsi tam hasar", ModuleAdvantageNote),
            new TextEntry("module.echo.advantage", "Mermiler duvardan bir kez seker", ModuleAdvantageNote),
            new TextEntry("module.density.advantage", "Mermi hasarı ×1,6", ModuleAdvantageNote),
            new TextEntry("module.haste.advantage", "Tüfek atış hızı ×1,4 (6 → 8,4 atış/sn)", ModuleAdvantageNote),
            new TextEntry("module.crit_chance.advantage", "Tüfekte +%20 kritik şansı", ModuleAdvantageNote),
            new TextEntry("module.pierce.advantage", "Mermi 2 düşmanı daha deler (tüfek)", ModuleAdvantageNote),
        };

        private static readonly TextEntry[] _moduleCosts =
        {
            new TextEntry("module.triple_shot.cost", "Her atış seni nişanın tersine iter", ModuleCostNote),
            new TextEntry("module.echo.cost", "Seken mermi sana da çarpar (yarım hasar), 2. duvarda söner", ModuleCostNote),
            new TextEntry("module.density.cost", "Gövden ×1,5 büyür, daha kolay vurulursun", ModuleCostNote),
            new TextEntry("module.haste.cost", "Mermiler nişandan rastgele ±6° sapar", ModuleCostNote),
            new TextEntry("module.crit_chance.cost", "Ateş ederken yavaşlarsın (yürüme 6 → 5,1 m/sn)", ModuleCostNote),
            new TextEntry("module.pierce.cost", "Her delişte mermi %30 zayıflar", ModuleCostNote),
        };

        private static readonly TextEntry[] _moduleWhispers =
        {
            new TextEntry("module.triple_shot.whisper", "Bir tetik, üç mermi. Azıcık teper, o kadar.", ModuleWhisperNote),
            new TextEntry("module.echo.whisper", "Duvardan seker. Arada bir sana döner. Bir şey olmaz.", ModuleWhisperNote),
            new TextEntry("module.density.whisper", "Daha sert vur. Biraz irileşirsin, kimse fark etmez.", ModuleWhisperNote),
            new TextEntry("module.haste.whisper", "Daha hızlı sık. Nereye mi gider? Aşağı yukarı oraya.", ModuleWhisperNote),
            new TextEntry("module.crit_chance.whisper",
                "Gözünü kıs, tam ortadan vur. Biraz ağır yürürsün, bir şey olmaz.", ModuleWhisperNote),
            new TextEntry("module.pierce.whisper", "Bir mermi, üç düşman. Sondaki biraz gıdıklanır, o kadar.",
                ModuleWhisperNote),
        };

        private const string ModuleNameNote =
            "Modül adı: alttaki yuvada, teklif şeridinde ve söküm ipucunda. Kısa tut (yuva dar, ~10 harf).";
        private const string ModuleAdvantageNote = "Modül teklif şeridi: yeşil \"+\" satırı (avantaj).";
        private const string ModuleCostNote = "Modül teklif şeridi: kırmızı \"−\" satırı (bedel).";
        private const string ModuleWhisperNote = "Modül teklif şeridi: modül adının yanında Şeytan'ın fısıltısı.";

        public static string ModuleName(ModuleType type) => Pick(_moduleNames, type);
        public static string ModuleAdvantage(ModuleType type) => Pick(_moduleAdvantages, type);
        public static string ModuleCost(ModuleType type) => Pick(_moduleCosts, type);
        public static string ModuleWhisper(ModuleType type) => Pick(_moduleWhispers, type);

        // Unknown values read as Density, as the old switch defaults did.
        private static string Pick(TextEntry[] set, ModuleType type) =>
            set[(uint)type < (uint)set.Length ? (int)type : (int)ModuleType.Density].Text;
    }
}
