# Text pass: natural Turkish (2026-10-24, writer)

Goal: all player-facing Turkish reads as everyday, warm, informal Turkish rather than a translation. Only string
values changed. Constant names, `{0}` placeholders, `\n` and signatures are unchanged.

Files: `Assets/_Game/Scripts/UI/GameTexts.cs`, `Assets/_Game/Scripts/Devil/DealLibrary.cs`,
`Assets/_Game/Tests/EditMode/DealTests.cs` ("Rengin" → "Rengi"). `ComboTexts.cs` was checked and left as it is.

## 1. GameTexts.cs

| Constant | Old | New |
|---|---|---|
| Intro | Defne bu gece de her şeye evet dedi. "Bir şey olmaz." | *(unchanged)* |
| IntroGoal | Dayanabildiğin kadar dayan. Dalgalar durmaz. | Dayanabildiğin kadar dayan. Arkası kesilmez. |
| DevilFirstAppearance | Psst. Sen. Evet, ekrandaki sen. | Pışt! Sana diyorum. Evet, ekrandaki sen. |
| DevilNoAtFirstDeath | Hayır mı? Hem de ilk seferde. Pek eğlenceli değilsin. | Daha ilk seferden hayır mı? Amma sıkıcısın. |
| DevilNoLater | Şimdi mi hayır? Aldıklarım bende kalıyor. | Şimdi mi hayır diyorsun? Aldıklarım bende kalıyor ama. |
| DevilAlwaysYes | Bir şey olmadı, değil mi? | *(unchanged)* |
| CardGive / CardTake | Sana: / Ondan: | *(unchanged)* |
| CardCostVisible | Görürsün: | *(unchanged)* |
| CardCostMechanical | Oyunda: | Sayılarla: |
| Yes / No | EVET / HAYIR | *(unchanged)* |
| YesKeys / NoKeys | [Enter] basılı tut / [H] | *(unchanged)* |
| YesStakes | dirilirsin · bedeli kalıcı | *(unchanged)* |
| NoStakes | koşu biter | oyun biter |
| LineContinue | [Enter] devam | *(unchanged)* |
| KeptTitle | Kaybettin ama kendin kaldın. | *(unchanged)* |
| KeptBody | Defne yere oturdu, nefes nefese. Saçı hâlâ kıvırcık, hâlâ renkli. | Defne olduğu yere çöktü, nefes nefese. Saçları hâlâ kıvırcık, hâlâ rengârenk. |
| KeptClosing | Yarın bir şeye hayır diyecek. | Yarın bir şeye hayır diyecek. Belki ikisine. |
| DiminishedTitle | Eksildin ama ayaktasın. | Eksildin ama hâlâ ayaktasın. |
| DiminishedYesCountFormat | {0} kez evet dedin. | {0} kere evet dedin. |
| DiminishedGone | Defne'den gidenler: | *(unchanged)* |
| DiminishedClosing | Hayır demek geç oldu. Ama oldu. | Hayır biraz geç geldi. Ama geldi. |
| AlwaysYesTitle | Hayır demek de bir seçenekti. | Oysa hayır da diyebilirdin. |
| Retry | Tekrar dene | Bir daha dene |
| RetryKeys | [R] | *(unchanged)* |
| LedgerTaken / LedgerGiven / LedgerGivenNote | Aldıkların / Verdiklerin / Defne'den | *(unchanged)* |
| LedgerEmpty | Aldıkların: Hiçbir şey · Verdiklerin: Hiçbir şey. Kendin kaldın. | Aldıkların: hiçbir şey · Verdiklerin: hiçbir şey. Kendin kaldın. |
| SmallNosFormat | Küçük hayırların: {0} | *(unchanged)* |
| ScoreFormat | Skor: {0} | Puan: {0} |
| HudScoreFormat | Skor {0} | Puan {0} |
| HudRocket / HudMachete | Roket [E] / Pala [F] | *(unchanged)* |
| HudDodge | Kaçınma [Space] | Kaçış [Space] |
| HudReject | Ret [Sağ tık] | Hayır [Sağ tık] |
| HudGiven | Verdiklerin | *(unchanged)* |
| HudSmallNosFormat | Küçük hayırlar: {0} | *(unchanged)* |
| HudEmptySlot | — | *(unchanged)* |
| HudEquippedDuplicate | TAKILI | *(unchanged)* |
| HudEjectHint | Sağ tık: sök | Sağ tık: söküp at |
| PauseTitle | Duraklatıldı | Mola |
| PauseResume / PauseQuit | Devam [Esc] / Ana menü | *(unchanged)* |
| Menu* (Title, Subtitle, Start, Settings, Credits, Quit, Footer, Back, Hint) | — | *(unchanged)* |
| SettingsMaster | Ana ses | Genel ses |
| Settings* (rest) | — | *(unchanged)* |
| CreditsBody line 2 | Çevre: Synty POLYGON Dark Fantasy | Mekân: Synty POLYGON Dark Fantasy |
| CreditsBody line 6 | Parçacık dokuları: Kenney (CC0) | Efekt dokuları: Kenney (CC0) |
| CreditsBody line 7 | Şeytan görseli (geçici): … | Şeytan çizimi (geçici): … |
| CreditsBody line 8 | Sesler ve müzik: oyunun içinde sentezlendi | Sesler ve müzik: oyunun içinde üretildi |
| HintMoveFire | WASD: yürü · Fare: nişan · Sol tık: ateş | WASD: yürü · Fare: nişan al · Sol tık: ateş et |
| HintOffer | Temasta kal: kabul · Sağ tık: reddet | Üstüne git, bekle: kabul · Sağ tık: hayır de |
| HintEject | Q basılı + sağ tık: modülü sök ve fırlat | Q'yu basılı tut + Sağ tık: modülü söküp fırlat |
| HintRocket | E: roket · F: pala · Space: kaçın | E: roket at · F: pala savur · Space: kaç |

### Modules (ModuleName / Advantage / Cost / Whisper)

| Module | Field | Old | New |
|---|---|---|---|
| Üçlü Atış | Name | Üçlü Atış | *(unchanged)* |
| | Advantage | 3 mermi, yan açılar ±12°, her biri tam hasar | Tek atışta 3 mermi (±12°), hepsi tam hasar |
| | Cost | Her salvo oyuncuyu nişanın tersine iter | Her atış seni nişanın tersine iter |
| | Whisper | Bir tetik, üç atış. Biraz geri iter, o kadar. | Bir tetik, üç mermi. Azıcık teper, o kadar. |
| Yankı | Name | Yankı | *(unchanged)* |
| | Advantage | Mermi görünür duvardan bir kez seker | Mermiler duvardan bir kez seker |
| | Cost | Sekmiş mermi oyuncuya da vurur (düşman hasarının yarısı); ikinci duvarda söner | Seken mermi sana da çarpar (yarım hasar), 2. duvarda söner |
| | Whisper | Duvardan seker. Bazen sana döner. Bir şey olmaz. | Duvardan seker. Arada bir sana döner. Bir şey olmaz. |
| Hız | Name | Hız | *(unchanged)* |
| | Advantage | Atış hızı ×1,4 (saniyede 6 → 8,4) | Tüfek saniyede 6 → 8,4 atış (×1,4) |
| | Cost | Her mermi nişandan rastgele ±6° sapar | Mermiler nişandan rastgele ±6° sapar |
| | Whisper | Daha hızlı. Nişan mı? Aşağı yukarı. Bir şey olmaz. | Daha hızlı sık. Nereye mi gider? Aşağı yukarı oraya. |
| Yoğunluk | Name | Yoğunluk | *(unchanged, see open questions)* |
| | Advantage | Hasar ×1,6 | Mermi hasarı ×1,6 |
| | Cost | Gövde ve isabet yarıçapı ×1,5; hız değişmez | Gövden ×1,5 büyür, daha kolay vurulursun |
| | Whisper | Daha sert vur. Biraz büyürsün, kimse fark etmez. | Daha sert vur. Biraz irileşirsin, kimse fark etmez. |

## 2. DealLibrary.cs (numbers and effects unchanged)

| # | Field | Old | New |
|---|---|---|---|
| 1 | Offer | Sana iki kat roket, daha hızlı ateş. Ondan? Biraz renk. Fark etmez bile. | Sana iki kat güçlü roket, hem de daha sık. Ondan mı? Biraz renk. Fark etmez bile. |
| 1 | Upgrade | Roket hasarı ×2 (40 → 80), patlama alanı ×2 (yarıçap 2,5 → 3,5 m), atış hızı +%40 (dolum 6,0 → 4,3 sn) | Roket hasarı iki katı (40 → 80), patlama alanı iki katı (yarıçap 2,5 → 3,5 m), dolum %40 hızlı (6,0 → 4,3 sn) |
| 1 | CostName | Rengin | Rengi |
| 1 | Visible | Dünya %30 solar, saçındaki renk çekilir. | Dünya %30 solar, Defne'nin rengi çekilir. |
| 1 | Mechanical | Yok. | Hiçbir şey. |
| 2 | Offer | Sana pala dalgası, bir kaçış hakkı daha. Ondan sesini alıyorum. Zaten susuyordu. | Sana pala dalgası, bir kaçış hakkı daha. Ondan da sesini alıyorum. Nasılsa hep susuyordu. |
| 2 | Upgrade | Pala dalgası açılır; kaçınma şarjı 1 → 2 | Pala savurunca öne bir dalga da çıkar; kaçış hakkı 1 → 2 |
| 2 | CostName | Sesin | Sesi |
| 2 | Visible | Defne'nin sesleri ve bir müzik katmanı susar (oyun işaretleri kalır); can çubuğu kısalır. | Defne'nin sesi ve müziğin bir kısmı susar (uyarı sesleri kalır); can çubuğu kısalır. |
| 2 | Mechanical | Azami can 100 → 80 | Can sınırı 100 → 80 |
| 2 | Ledger | Pala dalgası, +1 kaçınma · azami can −20 | Pala dalgası, +1 kaçış · can sınırı −20 |
| 3 | Offer | Mermilerin delip geçsin. Ondan arkadaşlarını alıyorum. Onlara zaten vakti yoktu. | Mermilerin delip geçsin. Ondan da arkadaşlarını alıyorum. Zaten onlara hiç vakti yoktu. |
| 3 | Upgrade | Tüfek mermileri delip geçer (2 ek düşman, kalkanı delmez) | Tüfek mermileri 2 düşmanı daha delip geçer (kalkanı delmez) |
| 3 | CostName | Arkadaşların | Arkadaşları |
| 3 | Visible | HUD'daki küçük fotoğraf kaybolur. | Ekrandaki küçük fotoğraf kaybolur. |
| 3 | Mechanical | Düşmanlar %15 hızlı | Düşmanlar %15 daha hızlı |
| 3 | Ledger | Delici mermi · düşmanlar %15 hızlı | Delen mermi · düşmanlar %15 daha hızlı |
| 4 | Offer | Yüzde on hasar. Ondan yüzünü alıyorum. Aynaya bakmıyordu nasılsa. | Biraz daha sert, biraz daha seri. Ondan yüzünü alıyorum. Aynaya bakmıyordu nasılsa. |
| 4 | Upgrade | Tüm hasar +%10, saldırı hızı +%25 (tüfek 6 → 7,5 atış/sn, pala beklemesi 0,5 → 0,4 sn) | Bütün hasar %10, saldırı hızı %25 artar (tüfek saniyede 6 → 7,5 atış, pala beklemesi 0,5 → 0,4 sn) |
| 4 | CostName | Yüzün | Yüzü |
| 4 | Visible | Modelden vokseller kalıcı olarak düşer (isabet alanı küçülmez). | Defne'nin yüzünden parçalar kopar, geri gelmez (vurulma alanı küçülmez). |
| 4 | Mechanical | Azami can 80 → 60 | Can sınırı 80 → 60 |
| 4 | Ledger | +%10 hasar, +%25 saldırı hızı · azami can −20 | +%10 hasar, +%25 saldırı hızı · can sınırı −20 |
| 5 | Offer | Bir roket daha. Ondan zamanını alıyorum. | Bir roket daha. Ondan da zamanını alıyorum. Nasılsa kendine hiç ayırmıyordu. |
| 5 | Upgrade | +1 roket şarjı (2 → 3) | Bir roket hakkı daha (2 → 3) |
| 5 | CostName | Zamanın | Zamanı |
| 5 | Visible | Ekran kenarları kararır. | Ekranın kenarları kararır. |
| 5 | Mechanical | Dirilişte dokunulmazlık yok; kaçınma beklemesi ×2 (1,6 → 3,2 sn) | Dirilince koruma süresi yok; kaçış beklemesi iki katı (1,6 → 3,2 sn) |
| 5 | Ledger | +1 roket · dirilişte dokunulmazlık yok, kaçınma beklemesi ×2 | +1 roket · dirilince koruma yok, kaçış beklemesi ×2 |
| 6 | Offer | Son anlaşma: neyin kaldıysa.\nBu sefer sormuyorum. | Son anlaşma: ondan geriye ne kaldıysa.\nBu sefer sormuyorum. |
| 6 | CostName | Kendin | Kendisi |
| 6 | Visible | Karakter griye döner. | Defne griye döner. |
| 6 | Mechanical | Kontroller gecikir (0,25 sn) | Tuşlar 0,25 sn geç tepki verir |
| 6 | Ledger | Hiçbir şey · Kendisi | *(unchanged)* |

Why CostName moved from "Rengin" to "Rengi": the card prints `Ondan: <CostName>`, so "Ondan: Rengin" mixed "her" and
"your". K-10 says the Devil always speaks of Defne as "o". The card now reads "Ondan: Rengi", which matches the ledger
("Verdiklerin: Rengi"). The tooltip in `DealDefinition.cs` still says "Poetic cost name, second person (e.g. 'Rengin')".
That file is outside this pass, so the owner should update the comment.

## 3. Combo (ComboTexts.cs, checked and unchanged)

Current values work: `KOMBO`, `×{0},{1}`, `KIRILDI` (damage), `SÖNDÜ` (timeout). No tier shout-out constants exist yet.
If tier marks get text (combo-haste.md §2, Feedback), here are suggestions in the Devil's voice, each ≤ 16 characters:

| Tier | Suggested |
|---|---|
| 10 (×1,2) | Isındın! |
| 25 (×1,5) | Açıldın bakıyorum! |
| 50 (×2,0) | Coştun! |
| 100 (×3,0) | Şeytan bile şaştı! |

The 25 and 100 lines are 18 characters, so check the width. Shorter fallbacks: "Açıldın!" and "Durdurulamazsın!" (16).

## 4. Suggested MAIN_GDD wording (the main session pastes this in)

### §8: card description paragraph
- "EVET'in altında "dirilirsin · bedeli kalıcı", HAYIR'ın altında **"oyun biter"** yazar."
- Add: "Kartın bedel satırları: **Ondan: <Rengi/Sesi/…>** · **Görürsün:** görünen bedel · **Sayılarla:** mekanik bedel."

### §8: table (Vaat | Bedel: görünür | Bedel: mekanik | Şeytan'ın teklifi)

| # | Vaat (yükseltme) | Bedel: görünür | Bedel: mekanik | Şeytan'ın teklifi |
|---|---|---|---|---|
| 1 | Roket hasarı iki katı (40 → 80), patlama alanı iki katı (yarıçap 2,5 → 3,5 m 🧪, ❓ A-9), dolum %40 hızlı (6,0 → 4,3 sn) | **Rengi:** dünya %30 solar, Defne'nin rengi çekilir | Hiçbir şey | Sana iki kat güçlü roket, hem de daha sık. Ondan mı? Biraz renk. Fark etmez bile. |
| 2 | Pala savurunca öne bir dalga da çıkar; kaçış hakkı 1 → 2 | **Sesi:** Defne'nin sesi ve müziğin bir kısmı susar (uyarı sesleri kalır, K-8); can çubuğu kısalır | Can sınırı 100 → 80 | Sana pala dalgası, bir kaçış hakkı daha. Ondan da sesini alıyorum. Nasılsa hep susuyordu. |
| 3 | Tüfek mermileri 2 düşmanı daha delip geçer (kalkanı delmez*) | **Arkadaşları:** ekrandaki küçük fotoğraf kaybolur | Düşmanlar %15 daha hızlı | Mermilerin delip geçsin. Ondan da arkadaşlarını alıyorum. Zaten onlara hiç vakti yoktu. |
| 4 | Bütün hasar %10, saldırı hızı %25 artar: tüfek saniyede 6 → 7,5 atış, pala beklemesi 0,5 → 0,4 sn (roket değişmez) (❓ A-10) | **Yüzü:** Defne'nin yüzünden parçalar kopar, geri gelmez (vurulma alanı küçülmez, K-13) | Can sınırı 80 → 60 | Biraz daha sert, biraz daha seri. Ondan yüzünü alıyorum. Aynaya bakmıyordu nasılsa. |
| 5 | Bir roket hakkı daha (2 → 3) | **Zamanı:** ekranın kenarları kararır (doğuş işaretleri kararmanın üstünde çizilir*) | Dirilince koruma süresi yok; kaçış beklemesi iki katı (1,6 → 3,2 sn) | Bir roket daha. Ondan da zamanını alıyorum. Nasılsa kendine hiç ayırmıyordu. |
| 6 | — | **Kendisi:** Defne griye döner | Tuşlar 0,25 sn geç tepki verir*, kaçınılmaz sona gider (❓ A-4) | Son anlaşma: ondan geriye ne kaldıysa. / Bu sefer sormuyorum. |

### §8: Devil lines
- **İlk Şeytan görünüşü:** "Pışt! Sana diyorum. Evet, ekrandaki sen."
- **Oyuncu HAYIR dediğinde:** 1. ölümde "Daha ilk seferden hayır mı? Amma sıkıcısın." · 2–5. ölümde "Şimdi mi hayır diyorsun? Aldıklarım bende kalıyor ama."
- §15 A-4: "Son anlaşma: ondan geriye ne kaldıysa." / "Bu sefer sormuyorum."

### §11: endings table

| Durum | Metin |
|---|---|
| **1. ölümde HAYIR** | **Kaybettin ama kendin kaldın.** Defne olduğu yere çöktü, nefes nefese. Saçları hâlâ kıvırcık, hâlâ rengârenk. *Yarın bir şeye hayır diyecek. Belki ikisine.* (Tam renkli Defne.) |
| **2–5. ölümde HAYIR** | **Eksildin ama hâlâ ayaktasın.** {evet sayısı} kere evet dedin. Defne'den gidenler: (Rengi · Sesi · Arkadaşları · Yüzü · Zamanı) *Hayır biraz geç geldi. Ama geldi.* |
| **Hep EVET (6. ölüm)** | Şeytan: **"Bir şey olmadı, değil mi?"** Gri kabuk dağılır. Son kartı: **Oysa hayır da diyebilirdin.** Düğme: *Bir daha dene* (yeni koşu). |

- Ledger table: row 2 "Sesi · can sınırı −20", row 3 Aldıkların "Delen mermi", Verdiklerin "Arkadaşları · düşmanlar %15 daha
  hızlı", row 4 "Yüzü · can sınırı −20", row 5 "Zamanı · dirilince koruma yok, kaçış beklemesi ×2".
- Score line: the screen shows "Puan", not "Skor".

### §7: module table (Avantaj / Bedel / Fısıltı columns as the game shows them)
- Üçlü Atış: "Bir tetik, üç mermi. Azıcık teper, o kadar."
- Yankı: "Duvardan seker. Arada bir sana döner. Bir şey olmaz."
- Hız: "Daha hızlı sık. Nereye mi gider? Aşağı yukarı oraya."
- Yoğunluk: "Daha sert vur. Biraz irileşirsin, kimse fark etmez."

### §12: hints and HUD
- "Başta "WASD: yürü · Fare: nişan al · Sol tık: ateş et" … ilk teklif gelince "Üstüne git, bekle: kabul · Sağ tık: hayır
  de" … "Q'yu basılı tut + Sağ tık: modülü söküp fırlat" … "E: roket at · F: pala savur · Space: kaç"."
- HUD: the reject indicator reads "Hayır [Sağ tık]", the dodge indicator reads "Kaçış [Space]", the score reads "Puan",
  and the pause title is "Mola".

## 5. Open questions for the owner / narrative-director
- **Cost names in the third person** ("Rengi", not "Rengin"): this changes the GDD's poetic names. If the second person
  was deliberate, the alternative is to change the card label `Ondan:` instead.
- **"Yoğunluk"** is a literal "Density". Suggested replacements: "İrilik" or "Ağır Mermi". Not changed, because the
  name is a GDD term.
- **HudReject "Hayır"** answers TODO F15. Burak should confirm.
- **"Belki ikisine."** (KeptClosing) adds a small beat. Drop it if the original line should stay bare.
- **Contract 1 visible cost** now says "Defne'nin rengi çekilir", following §13 (the whole model loses colour on the new
  model), instead of "saçındaki renk".
- **"TAKILI"** was kept for width (60 px box). A warmer option is "SENDE VAR", if it fits.
