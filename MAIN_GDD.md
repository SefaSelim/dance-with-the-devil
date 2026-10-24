# MAIN GDD — Dance with the Devil (Bir Şey Olmaz)

**Son güncelleme: 2026-10-24 04:58 (UTC+3)**

> **Bu dosya projenin tek ve yaşayan Game Design Document'ıdır.** Sürüm numarası yoktur (GDD2, GDD3…
> diye gitmez). Yeni bir mekanik eklendiğinde, var olan bir mekanik değiştiğinde ya da bir tasarım kararı
> alındığında **bu dosya aynı görevde güncellenir** ve yukarıdaki **Son güncelleme** tarih-saati yenilenir;
> değişiklik en alttaki *Değişiklik geçmişi* bölümüne bir satır olarak eklenir. Kural `AGENTS.md`'de kayıtlıdır.

### Durum etiketleri

| Etiket | Anlamı |
|---|---|
| ✅ **Uygulandı** | Kodda var, oyunda çalışıyor. |
| 🟡 **Kısmi** | Bir kısmı ya da bir prototipi var; döngü henüz tamam değil. |
| ⬜ **Yok** | Tasarımda var, kodda yok. |
| ❓ **AÇIK** | Tasarım kararı verilmedi. Tahminle kapatılmaz. |
| ⛔ **İptal / Ertelendi** | Rafa kaldırıldı ya da jam sonrasına bırakıldı. Uygulanmaz. |
| 🧪 **Geçici** | Test/geliştirme aracı; teslimden önce kapatılacak. |

**Kaynaklar:** ekibin ilk GDD'si (*Bir Şey Olmaz*, lazer/modül arenası) ve kullanıcının onayladığı birleşik
tasarım (izometrik voksel nişancı + Şeytan sözleşmeleri). İlk GDD'nin bölüm numaraları metinde "ilk GDD §N"
diye anılır. **"Başlangıç önerisi"** yazan sayılar kaynakta yoktu; ilk oyun testine kadar geçerli öneridir.

**Kesin kullanıcı kararları:**
- İki tasarım birleşir: ilk GDD'nin savaş içi kabul / ret / sökme mekaniği kalır ve bu teklifler artık Şeytan'dan gelir.
- Ölümde "Hayır" demek mümkündür ve gerçek sona açılır.
- Bir koşu yaklaşık 5 dakikadır.

---

## 1. Oyun tanımı ve vizyon — ⬜

**Etkinlik:** Women Game Jam · **Tema:** "Hayır demeyi öğrenmelisin." · **Ekip:** 4 kişi

**Tek cümle:** *Bir Şey Olmaz*, her şeye "evet" demeye alışmış Defne'nin yorgunluğunun içindeki voksel arenada,
vurulan her şeyin piksellerine dağıldığı izometrik bir nişancı oyunudur; her ölümde ekranın kenarından eğilen
Şeytan "sana bunu veririm, ondan şunu alırım" der ve oyuncu sonunda doğru seçimin "hayır" olduğunu fark eder.

| # | İlke | Oyuncu için anlamı |
|---|---|---|
| 1 | **Güç gerçekten baştan çıkarmalı** | Şeytan'ın her teklifi sahiden iyi hissettirir: daha büyük patlama, daha çok dağılan piksel, daha uzun koşu. Evet demek kolay ve keyiflidir; hayır ancak bu yüzden anlam taşır. |
| 2 | **Her evetin bedeli görünür ve bedende kalır** | Kabul edilen her sözleşmenin karşılığı Defne'nin renginde, sesinde, yüzünde ve hareketinde görülür. Gizli ceza yoktur. |
| 3 | **Küçük hayırlar geri alınır, büyük evetler alınmaz** | Savaş içindeki fısıltı modüllerini reddetmek ve sökmek rahatça öğrenilir; ölüm sözleşmeleri ise geri dönmez. |

**İlke testleri:** bir yükseltme hissettirmiyorsa güçlendirilir, zayıf bırakılıp "hayır" kolaylaştırılmaz. Bir bedel
yalnızca bir sayıda görünüyorsa yetersizdir; ekranda, seste veya karakterde fark edilmelidir. Ölüm sözleşmelerini geri
alınabilir ya da küçük modülleri kalıcı yapan öneriler reddedilir.

**Temanın mekanik dersi:** "Küçük hayırları öğrendin, ama büyük evetleri hiç sorgulamadın."

## 2. Hikâye çerçevesi — ⬜ (karar K-1: Şeytan oyuncuya konuşur)

Defne her gece her şeye evet diyor: bir iş daha, bir iyilik daha, bir mesaj daha. Kendine hep aynı şeyi söylüyor:
"Bir şey olmaz." Arena onun yorgunluğunun içi; üstüne gelen yaratıklar, yıllardır biriktirdiği talepler. Her düştüğünde
Şeytan monitörün kenarından eğilir ve Defne'ye değil, **oyuncuya** konuşur. Evetleri oyuncu verir; bedelini Defne öder.

**Defne'nin görünüşü (ilk GDD §13):** büyük köşeli tutamlarla kıvırcık saç, yuvarlak gözlük, düz petrol rengi tulum,
sade bot, küçük turkuaz detaylar.

**Giriş metni (koşu başında, tek satır):** Defne bu gece de her şeye evet dedi. "Bir şey olmaz."

## 3. Temel döngü (~5 dk) — ⬜

**Saniyelik döngü (ilk GDD §4):** yer aç → nişan al ve ateş et → düşman ön işaretini oku → fısıltıyı değerlendir →
kabul et / reddet / sök → yeni hareket alanına göre konumlan.

**Koşu döngüsü:**
1. Arena ilk GDD §9 düzenindedir (bkz. §9).
2. Dalgalar hızla zorlaşır; oyuncu kaçınılmaz olarak ölür.
3. Ölümde zaman donar, ekran solar ve **Şeytan ekranın sağ kenarından, monitör çerçevesinin dışından eğilir**.
4. **Sözleşme:** "Sana X veriyorum, ondan Y alıyorum."
5. **EVET:** oyuncu dirilir, Şeytan'dan çıkan pikselleştiren bir şok dalgası yakındaki düşmanları parçalar; oyuncu
   yükseltmeyi ve kalıcı bedeli alır.
6. **HAYIR:** koşu biter ve gerçek sona gider.

**Ölüm takvimi (hedef, orta beceri):** ~0:50, 1:40, 2:30, 3:15, 4:00 → 5 sözleşme. **6. ölümde** Şeytan **yalnızca alır**
("Son anlaşma: neyin kaldıysa"). Ölümleri zorluk getirir: düşman canları sabittir; sayı ve tür karışımı büyür. **Ölüm garantidir; ilk GDD'deki "5. dakikada dalga temizlenince kazanılır" koşulu kalkar** (karar K-4). Zorluk adil ve okunur kalmalıdır.

**Diriliş (karar K-12):** can yeni azami değere dolar; 2,0 sn dokunulmazlık; şok dalgası 6 m içindeki
bütün düşmanları parçalar; dalga saati kaldığı yerden sürer. Sözleşme ekranı açıkken dalga saati de durur.

## 4. Kontroller — 🟡 (hareket, nişan, kaçınma, tüfek, roket, pala çalışıyor; ret, Q ve R henüz yok)

| Girdi | İşlev |
|---|---|
| WASD | Kamera düzlemine göre hareket (6 m/sn) |
| Fare | Zemindeki imleç noktasına nişan |
| Sol tık / basılı tut | Tüfek |
| E / orta tık | Roket (şarjlı) |
| F | Pala (yakın vuruş, her an kullanılabilir) |
| Space | Kaçınma |
| Sağ tık | İmleç yönüne reddetme darbesi |
| Q basılı | Takılı modülleri karakter çevresinde göster |
| Q + sağ tık | Vurgulanan modülü sök ve imleç yönüne fırlat |
| R | Yeniden başlat (❓ A-15: yalnız sonuç ekranında mı, oyun içinde de mi?) |

**Kaçınma (ilk GDD §5):** ~3 m / 0,18 sn, bekleme 1,6 sn. Hareket yönüne, girdi yoksa imleç yönüne gider. Sırasında
hasar alınmaz, kabul halkası ilerlemez, ateş ve pala kesilir; ret/sökme girdisi tamponlanmaz.

**Oyuncu:** 100 can; hasardan sonra 0,6 sn dokunulmazlık (aynı anda gelen atışlar tek karede yığılmaz).
Pala vuruşu bittikten sonra 0,5 sn bekleme başlar (yani en fazla 0,7 sn'de bir vuruş). Kaçınma sırasında üç silah da susar;
basılı tutulan ateş kaçınma bitince sürer, kaçınma sırasında basılan tuşlar saklanmaz.

## 5. Silahlar — 🟡 (tüfek, roket ve pala çalışıyor; pala dalgası, çevre kırma ve kalkan kuralı henüz yok)

Fısıltı modülleri (§7) ilk GDD'deki temel lazerin yerini alan **tüfeği** etkiler; roket ve palayı etkilemez
(karar K-7).

| Silah | Değerler | Kaynak |
|---|---|---|
| **Tüfek** | 10 hasar, saniyede 6 atış, sınırsız cephane; mermi 40 m/sn, menzil 25 m; delme yok | Hasar/hız ilk GDD; gerisi başlangıç önerisi |
| **Roket** | 2 şarj, şarj başına 6,0 sn dolum; mermi 18 m/sn; ilk temasta ya da 20 m sonra patlar; 2,5 m yarıçapta düz 40 hasar ve ~2 m savurma; kalkanı yok sayar; oyuncuya hasar vermez; iç duvar ve dekorları kırar, dış sınırı kırmaz | Başlangıç önerisi (kalkan ve çevre kuralı birleşik tasarım) |
| **Pala** | 35 hasar, önde 120° yay, 2 m; vuruş 0,2 sn (bu sürede tüfek susar), bekleme 0,5 sn; kalkanı yok sayar; vurduğu yüzeyde düzlemsel kesik açar | Başlangıç önerisi |
| **Pala dalgası** (Sözleşme 2 ile açılır, bkz. §8) | Her pala vuruşu öne bir dalga da çıkarır: 20 hasar, delip geçer, 8 m / 2,5 m genişlik / 20 m/sn; kalkanlı irinin önünden geçemez | Başlangıç önerisi; kalkan kuralı karar K-13 |

**Vuruş hissi (yer tutucu):** her atış ve isabet aynı karede namlu/isabet parlaması, küp sıçraması ve ekran
sarsıntısı üretir. Tüfek hafif (sarsıntı yok denecek kadar az, duraklama yok), pala orta (0,06 sn vuruş duraklaması),
roket ağır (büyük parlama, ışık, güçlü sarsıntı, 0,1 sn duraklama). Ekran sarsıntısı görüntüyü kaydırır, kamerayı
çevirmez; erişilebilirlik için genel bir sarsıntı çarpanı vardır.

## 6. Düşmanlar — 🟡 (Takipçi, Koşucu ve Nişancı oyunda; patlayan imp ve kalkanlı iri Minimum kapsam dışında)

Canlar dalgalarla artmaz. Ön işaret süreleri hiçbir sözleşmeden etkilenmez (okunabilirlik). Görsel: voksel şeytanlar ve yaratıklar.

| Tür | Can | Hız | Davranış ve ön işaret | Hasar | Tehdit puanı |
|---|---|---|---|---|---|
| **Takipçi** (ilk GDD) | 30 | 3,5 m/sn* | Yakınlaşır, üst üste binmeden yer değiştirmeye zorlar. 0,45 sn durur, kollarını açar, zeminde kısa kama; 1,6 m'den hamle | 10 | 1 |
| **Nişancı** (ilk GDD) | 40 | 3,0 m/sn* | 7–11 m* mesafe korur, açık görüş arar, duvar içinden ateş etmez. 0,8 sn çizgili turuncu hat, son 0,25 sn yön kilitli | 12 (mermi 12 m/sn*) | 2 |
| **Koşucu** | 15* | 7,5 m/sn* | Düz çizgide hızla yaklaşır. 0,35 sn çömelme + kısa zemin çizgisi* | 6* | 1 |
| **Patlayan imp** | 20* | 4,5 m/sn* | Yaklaşır, 1,5 m'de durup fitili yakar. 0,7 sn yanıp sönme + zeminde 2,5 m daire*. Fitilden önce ölürse patlamaz, sonra ölürse patlar*; çevreyi de kırar | 25 alan, 2,5 m* | 2 |
| **Kalkanlı iri** | 120* | 2,5 m/sn*, dönüş 90°/sn* | Önden zırhlı, yavaşça üstüne yürür. 0,7 sn kol kaldırma + 2,5 m daire*. Ön 120° yaydan gelen tüfek mermisi ve pala dalgası hasar vermez; roket, yakın pala, modül/imp patlaması ve yan/arka ateş tam hasar verir* | 20 alan* | 4 |

\* başlangıç önerisi. Yakından (3 m) irinin arkasına dolanılabilir, uzaktan (5 m) dolanılamaz: "yaklaş ya da roket harca" kararı.

**Doğuş güvenliği (ilk GDD §9):** dört kenardaki işaretli girişlerden; oyuncuya en az 9 m; 0,8 sn ön işaret; uygun giriş
yoksa doğuş ertelenir. Nişancılar aynı anda her yönü kapatacak biçimde doğmaz.

## 7. Fısıltı modülleri: kabul, ret, sökme — ⬜

Savaş sırasında Şeytan'ın "fısıltıları" yüzen modüller olarak gelir. Kurallar ilk GDD §6–7'deki gibidir. Bunlar
**geri alınabilir** evetlerdir; ölüm sözleşmeleri (§8) **geri alınamaz**.

| Konu | Kural |
|---|---|
| Yaklaşma | Oyuncudan ≥4 m uzakta boş zeminde belirir, yavaşça yaklaşıp ~2 m'de durur; kendisi kabulü tetiklemez |
| Niyetli kabul | Çekirdeğe 0,45 sn temas; dolan halka gösterir; ayrılınca sıfırlanır; geçip gitmek veya kaçınarak üstünden geçmek sayılmaz |
| Kapasite | Her türden en fazla 1, toplam 3 tür; kopya teklif "TAKILI" gösterir |
| Teklif ömrü | Aynı anda en fazla 1 teklif; 14 sn sonra söner; tekrar kovalamaz |
| Ret | Sağ tık: imleç yönünde 60° koni ve 3 m içindeki en yakın teklif fırlatılır; darbe kendisi hasar vermez; boşa da 0,65 sn bekleme |
| Fırlatılan modül | ~16 m/sn; ilk düşman/duvar temasında ya da 0,7 sn sonra patlar; 1,8 m içinde 25 hasar ve kısa savurma; duvar arkasına işlemez, oyuncuya vurmaz; bir kez patlar |
| Sökme | Q + sağ tık; takılı modül aynı fırlatma davranışıyla tüketilir; yerden geri alınamaz; retle aynı beklemeyi paylaşır. Seçim çemberi ve sabit yuva kuralları ilk GDD §6'daki gibidir |
| Sıklık | İlk teklif ~8. sn, sonra ~25. ve ~42. sn; öğretimden sonra 14–20 sn aralık; üç tür torbadan karışık gelir |

| Modül | Avantaj | Bedel | Fısıltı |
|---|---|---|---|
| **Üçlü Atış** | 3 mermi, yan açılar ±12°, her biri tam hasar | Her salvo oyuncuyu nişanın tersine iter (hız katkısı ≤2,5 m/sn, duvarda birikmez) | Bir tetik, üç atış. Biraz geri iter, o kadar. |
| **Yankı** | Mermi görünür duvardan bir kez seker | Sekmiş mermi oyuncuya da vurur (düşman hasarının yarısı); ikinci duvarda söner | Duvardan seker. Bazen sana döner. Bir şey olmaz. |
| **Yoğunluk** | Hasar ×1,6 | Gövde ve isabet yarıçapı ×1,5; hız değişmez | Daha sert vur. Biraz büyürsün, kimse fark etmez. |

Sökülünce etkiler tek noktadan kalkar (ilk GDD §6): Üçlü tekleşir ve geri tepme durur; Yoğunluk gövdeyi ve hasarı
tabana döndürür; Yankı'nın havadaki mermileri oyuncuya zararsız olur. Ölümde takılı modüllerin ne olacağı ❓ A-11b.

## 8. Şeytan'ın ölüm sözleşmeleri — ⬜

**Eğri:** vaat küçülür, bedel büyür. Eğri sabittir, hikâye için yazılmıştır, rastgele değildir. Her bedel görünür ve
mekaniktir. Ölümde zaman donar, ekran solar, Şeytan sağ kenardan eğilir ve sözleşme kartı açılır: EVET / HAYIR.
Kartta bedelin hem şiirsel adı hem tam mekanik etkisi yazar (K-3). HAYIR her zaman görünür ve seçilebilir; ama
Şeytan'ın sunumu EVET'i öne çıkarır (büyük, parlak EVET; küçük, sönük HAYIR; Şeytan HAYIR'ı küçümser) (K-2).
Şeytan oyuncuya "sen" der, Defne'den hep "o" diye söz eder: "Sana X, ondan Y" (K-10).

| # | Vaat (yükseltme) | Bedel: görünür | Bedel: mekanik | Şeytan'ın teklifi |
|---|---|---|---|---|
| 1 | Roket hasarı/yarıçapı ×2 (40 → 80, 2,5 → 5 m*, ❓ A-9), atış hızı +%40 (dolum 6,0 → 4,3 sn) | **Rengin:** dünya %30 solar, saçındaki renk çekilir | Yok | Sana iki kat roket, daha hızlı ateş. Ondan? Biraz renk. Fark etmez bile. |
| 2 | Pala dalgası açılır; kaçınma şarjı 1 → 2 | **Sesin:** Defne'nin sesleri ve bir müzik katmanı susar; oyun işaretleri (hasar, kaçınma hazır, kabul) kalır (K-8); can çubuğu kısalır | Azami can 100 → 80 | Sana pala dalgası, bir kaçış hakkı daha. Ondan sesini alıyorum. Zaten susuyordu. |
| 3 | Tüfek mermileri delip geçer (2 ek düşman*, kalkanı delmez*) | **Arkadaşların:** HUD'daki küçük fotoğraf kaybolur | Düşmanlar %15 hızlı | Mermilerin delip geçsin. Ondan arkadaşlarını alıyorum. Onlara zaten vakti yoktu. |
| 4 | Tüm hasar +%10 (❓ A-10) | **Yüzün:** modelden vokseller kalıcı olarak düşer (isabet alanı küçülmez, K-13) | Azami can 80 → 60 | Yüzde on hasar. Ondan yüzünü alıyorum. Aynaya bakmıyordu nasılsa. |
| 5 | +1 roket şarjı (2 → 3) | **Zamanın:** ekran kenarları kararır (doğuş işaretleri kararmanın üstünde çizilir*) | Dirilişte dokunulmazlık yok; kaçınma beklemesi ×2 (1,6 → 3,2 sn) | Bir roket daha. Ondan zamanını alıyorum. |
| 6 | — | **Kendin:** karakter griye döner | Kontroller gecikir (0,25 sn*), kaçınılmaz sona gider (❓ A-4) | Son anlaşma: neyin kaldıysa. / Bu sefer sormuyorum. |

\* başlangıç önerisi.

**Birikme kuralları (başlangıç önerisi):** hasar çarpanları çarpımla birikir (Yoğunluk ×1,6 × Sözleşme 4 ×1,1).
Azami can kayıpları toplanır ve kalıcıdır (100 → 80 → 60); diriliş yeni azami değere doldurur. Bekleme çarpanı tabana
uygulanır, şarj sayısını değiştirmez.

**İlk Şeytan görünüşü (1. ölümde, teklif satırından önce):** "Psst. Sen. Evet, ekrandaki sen."

**Oyuncu HAYIR dediğinde:** 1. ölümde "Hayır mı? Hem de ilk seferde. Pek eğlenceli değilsin." · 2–5. ölümde
"Şimdi mi hayır? Aldıklarım bende kalıyor."

## 9. Arena — 🟡 (yer tutucu blok arena kuruldu)

İlk GDD §9 düzeni: ~32 × 28 m dikdörtgen platform, kesintisiz dış sekme duvarı (kırılmaz), ~12 × 10 m açık merkez,
kuzeybatı ve güneydoğuda birbirini karşılayan iki L duvar, en dar geçit 4 m, dört kenarda işaretli giriş. Kamera
yakınındaki duvarlar karakteri gizlediğinde saydamlaşır. İç L duvarlar ve dekorlar kırılabilir olarak planlandı;
ne kadarının kırılacağı Faz 4'te dengelenir (❓ A-12: Yankı'nın sekme yüzeyleriyle çakışma).

**Kurulan yerleşim (yer tutucu):** oynanabilir alan 32 × 28 m; kuzey ve doğu duvarları 2 m, kameraya yakın güney ve batı
duvarları görüşü kapatmasın diye 1 m yüksekliktedir ama 2 m gibi seker. L bloklar 1,5 m yüksek, 3 m kalındır. Çevre
halkası 5–6 m, iki L arası en az 11,7 m; merkezde kuzey–güney ve doğu–batı yönünde 4 m'lik iki açık görüş koridoru
vardır. Açık merkez ~14 × 12 m çıktı (ilk GDD ~12 × 10 m), halkayı ≥5 m ve 6 m diriliş şokuna yer bırakmak için.
Dört giriş kenarların ortasında, içe bakan şeritli zemin plakalarıdır; doğuş noktaları oyuncu başlangıcından 12–14 m uzaktadır.

## 10. Dalgalar ve zorluk — 🟡 (dalga yöneticisi çalışıyor; ölüm takvimi henüz dengelenmedi)

İlk GDD §10 bütçe mantığı korunur: 30 sn'lik pencereler, pencere başında kısa alt gruplar, aynı anda aktif düşman sınırı,
sınır doluysa doğuş bekler, geç kalanlar topluca boşaltılmaz. **Değişen:** bütçe adet yerine **tehdit puanıyla** tutulur
(§6 tablosu), çünkü türlerin tehdidi eşit değildir.

**Önerilen pencere bütçesi (başlangıç önerisi):**

| Pencere | Takipçi | Koşucu | Nişancı | İmp | İri | Tehdit | Aktif sınır | Hedef ölüm |
|---|---|---|---|---|---|---|---|---|
| 0:00–0:30 | 8 | – | – | – | – | 8 | 3 | |
| 0:30–1:00 | 10 | 4 | – | – | – | 14 | 5 | 1 (~0:50) |
| 1:00–1:30 | 8 | 4 | 3 | – | – | 18 | 6 | |
| 1:30–2:00 | 7 | 4 | 3 | 2 | – | 21 | 7 | 2 (~1:40) |
| 2:00–2:30 | 6 | 4 | 3 | 3 | 1 | 25 | 8 | 3 (~2:30) |
| 2:30–3:00 | 6 | 5 | 4 | 3 | 1 | 29 | 8 | |
| 3:00–3:30 | 6 | 5 | 4 | 3 | 1 | 29 | 9 | 4 (~3:15) |
| 3:30–4:00 | 4 | 3 | 3 | 2 | 1 | 20 | 9 | 5 (~4:00) |
| 4:00–4:30 | 4 | 3 | 3 | 3 | 1 | 22 | 9 | |
| 4:30–5:00 | 4 | 4 | 3 | 3 | 1 | 23 | 9 | 6 (~4:40) |

**Kaba hesap bulguları (Faz 4'te oyun testiyle doğrulanacak):**
- İlk GDD'nin 3 / 6 / 10… bütçesi 0:50'de ölüm üretmez; ilk iki pencere ~2,5–4 kat daha kalabalık olmalı.
- Azami can düştüğü için gereken hasar hızı aşağı yukarı sabittir (1,3–2,2 can/sn). Sözleşme 4–5 bedelleri işi kendisi
  yapar; bütçe büyümeye devam ederse 5. ve 6. ölüm ~3:40 ve ~4:05'e kayar. Bu yüzden 3:30'dan sonra adet düşer, karışım
  ağırlaşır (karar K-5).
- Takvim yalnızca orta beceri için tutar; usta oyuncu daha geç ölür; zorluk saate bağlı kalır (karar K-6). Faz 4'teki "yerinde durup ateş eden bot"
  insandan çok daha erken ölür; sonuçları alt sınır olarak okunur.
- Sözleşme 2 (+1 kaçınma) ve 5 (bekleme ×2) sonrası sürekli kaçınma hızı tabanla aynıdır; yalnız arka arkaya 2 kaçınma kalır.

**Uygulanan hâli (Minimum kapsam, K-11):** imp ve iri olmadığı için tablodaki her imp yerine 1 Takipçi + 1 Koşucu,
her iri yerine 2 Nişancı doğar; pencerelerin tehdit puanı aynı kalır. Her pencerenin bütçesi 0., 4. ve 8. saniyelerde
üç alt gruba bölünür. Nişancılar 3 sn içinde en fazla 2 farklı girişten gelir. Uyarı süresindeki doğuş da aktif sınıra
sayılır; oyuncu uyarı sırasında girişe 9 m'den fazla yaklaşırsa doğuş başka girişe kayar. Yeni doğuşlar 5:00'te durur.

**İlk bot ölçümü (2026-10-24, 🧪 tahmini veri):** yerinde durup ±12° ıskalayan, 0,35 sn tepki süreli, kaçınmayan bir
bot; her ölümde tam canla dirilip 2 sn dokunulmazlık ve 6 m şok dalgası alıyor, sözleşme etkileri yok. Ölümler:
1:07, 1:21, 1:43, 2:16, 2:38, 2:48, 3:14, 3:26, 3:39, 4:09, 4:22, 4:34 (12 ölüm). Okuma: ilk ölüm hedeften (0:50) geç,
sonrakiler hedeften çok sık. Bot hareket etmediği ve kaçınmadığı için bu bir alt sınırdır; takvim insan oyun testiyle
ayarlanacak.

## 11. Sonlar ve sonuç ekranı — ⬜

| Durum | Metin |
|---|---|
| **1. ölümde HAYIR** | **Kaybettin ama kendin kaldın.** Defne yere oturdu, nefes nefese. Saçı hâlâ kıvırcık, hâlâ renkli. *Yarın bir şeye hayır diyecek.* (Tam renkli Defne.) |
| **2–5. ölümde HAYIR** | **Eksildin ama ayaktasın.** {evet sayısı} kez evet dedin. Defne'den gidenler: (verilenler alt alta: Rengi · Sesi · Arkadaşları · Yüzü · Zamanı) *Hayır demek geç oldu. Ama oldu.* |
| **Hep EVET (6. ölüm)** | Şeytan: **"Bir şey olmadı, değil mi?"** Gri kabuk dağılır. Son kartı: **Hayır demek de bir seçenekti.** Düğme: *Tekrar dene* (yeni koşu). |

**Küçük hayırlar:** defterde puansız tek satır: "Küçük hayırların: N" (savaşta reddedilen ve sökülen modüller, karar K-9).

**Sonuç ekranı:** skor yerine **"Aldıkların / Verdiklerin"** defteri gösterilir ("Verdiklerin" üstünde küçük not: *Defne'den*).
İlk GDD §10 skoru en altta, küçük kalır. 1. ölümde hayır denirse defter: "Aldıkların: Hiçbir şey · Verdiklerin: Hiçbir şey.
Kendin kaldın."

| # | Aldıkların | Verdiklerin |
|---|---|---|
| 1 | Roket gücü | Rengi |
| 2 | Pala dalgası, +1 kaçınma | Sesi · azami can −20 |
| 3 | Delici mermi | Arkadaşları · düşmanlar %15 hızlı |
| 4 | +%10 hasar | Yüzü · azami can −20 |
| 5 | +1 roket | Zamanı · dirilişte dokunulmazlık yok, kaçınma beklemesi ×2 |
| 6 | Hiçbir şey | Kendisi |

**Skor (ilk GDD §10):** düşman +100, isabetli modül fırlatma +25, hızlı temizleme bonusu, hasardan kaçınma (❓ A-16).

## 12. Arayüz ve öğretim — ⬜

İlk GDD §11 korunur: üstte can, süre ve skor; altta üç sabit modül yuvası ile ret/kaçınma hazır göstergeleri; teklif
üzerinde tek satır avantaj, tek satır bedel. Ek olarak HUD'da Defne'nin küçük fotoğrafı (Sözleşme 3 bedeli) ve roket
şarjları. Bağlamsal ipuçları ilk dakikada sırayla gelir, oyunu durdurmaz. Renk tek ayırt edici değildir.

## 13. Görsel ve ses yönü — 🟡 (sabit izometrik kamera ve pikselli görüntü var; dış çizgi ve toon ışık yok)

- **Görünüm:** sabit ortografik izometrik kamera (35° eğim, 45° dönüş; bütün arena görünür, metre başına 12 piksel), düşük çözünürlükte (640×360) çizilip nokta filtresiyle büyütülen, hafif
  pikselli voksel görünüm; düz gölgeli toon ışık; 1 px dış çizgi; kamera kayarken titreme olmaz.
- **Parçalanma:** vurulan her şey, düşmanlar ve çevre, piksellerine (küplere) dağılır. Roket geniş, pala düzlemsel kesik,
  mermi küçük delik açar.
  Şu an 🟡: ölen hedef kendi parça renkleriyle küplere ayrılıp sıçrar, yere düşer ve küçülerek kaybolur; aynı karede çok
  ölüm olursa sonrakiler daha iri küplerle dağılır. Çevre kırılması ve delikler henüz yok (İsteğe bağlı katman).
- **Karakterler:** parçalı voksel (baş, gövde, kollar, bacaklar), yordamsal animasyon (yürüme sallanması, nişan, geri tepme).
- **Şeytan:** ekranın sağ kenarından, monitör çerçevesinin dışından eğilen 2B çizim; ekibin kendi çizimiyle değişecek.
- **Ses:** kısa okunur SFX, karanlık tek müzik döngüsü; Sözleşme 2 bir müzik katmanını susturur.
- **Yer tutucular 🧪:** son modeller gelene kadar küplerden Defne (16 parça) ve Takipçi (8 parça) kullanılır. Yer tutucu
  renkler: koyu soğuk zemin ve duvarlar, krem sekme kenarı, erik rengi saç, petrol tulum, turkuaz detay, kırmızı Takipçi.

## 14. Kapsam (jam teslimine kadar) — ⬜

**Teslim:** 2026-10-26 15:00 (UTC+3), kullanıcı kararı. 24 Ekim gecesinden itibaren ~59 saat kalır. Öneri (producer): hedef **Minimum** katman olsun, İsteğe bağlı ekler ancak Minimum oynanır olunca başlasın.

**Kapsam ilkesi:** temanın taşıyıcısı ölüm anındaki Şeytan sözleşmesi ve "Hayır" seçeneğidir. Kesmek gerekirse önce
görsel gösteriş, sonra içerik çeşitliliği gider. Sözleşme → bedel → son zinciri en son kesilir.

| Katman | İçerik |
|---|---|
| **Minimum** | Tek arena (duvarlar sağlam olabilir); izometrik kamera ve düşük çözünürlüklü pikselli görünüm; parçalı voksel Defne; hareket, nişan, kaçınma, can; tüfek, roket, pala; Takipçi ve Nişancı; bütçeli dalgalar ve ölüm takvimi; üç fısıltı modülü ile kabul/ret/sökme; ölüm dondurması → Şeytan → sözleşme kartı → EVET/HAYIR; altı görünür bedel (basit uygulama yeterli); bütün sonlar ve defter; giriş metni ve replikler; temel ses; ilk dakika ipuçları; vuruşta sarsıntı, kısa duraklama, namlu parlaması ve basit küp patlaması. |
| **İsteğe bağlı** (bu sırayla) | Koşucu → dış çizgi ve titreşimsiz kamera → küçük dekorların küplere dağılması → tam voksel enkaz sistemi → imp ve kalkanlı iri → Defne'den tek tek voksel düşmesi (minimumda parça gizleme yeterli) → iç duvarların kırılması → ek kozmetik efektler → tuş atama. |
| **Kapsam dışı** | Yeni arena, boss, ek modül ya da silah; koşular arası kalıcı güç, mağaza, envanter, yetenek ağacı; uzun eğitim, ara sahne, seslendirme; çok oyunculu. |

**Hiç kesilmez:** ölüm → Şeytan → EVET/HAYIR zinciri, her evetin görünür bedeli, HAYIR ve hep-EVET sonları, defter,
savaş içi fısıltı modülleri.

## 15. Kararlar ve açık sorular

### Kararlar (Burak, 2026-10-24: "önerilerini uygula")

| # | Karar |
|---|---|
| K-1 | Hikâye çerçevesi önerildiği gibidir: Şeytan **oyuncuya** konuşur; evetleri oyuncu verir, bedelini Defne öder. |
| K-2 | HAYIR her sözleşmede görünür ve seçilebilir; Şeytan'ın sunumu EVET'i öne çıkarır. |
| K-3 | Sözleşme kartı bedelin şiirsel adını ve tam mekanik etkisini birlikte gösterir (gizli ceza yok). |
| K-4 | Ölüm garantidir; ilk GDD'nin 5. dakika kazanma koşulu kalkar. |
| K-5 | Sözleşme 4'ten sonra (3:30 penceresinden itibaren) dalga adedi düşer, tür karışımı ağırlaşır. |
| K-6 | Dalga zorluğu saate bağlıdır (ilk GDD mantığı). |
| K-7 | Fısıltı modülleri yalnız tüfeği etkiler. |
| K-8 | Sözleşme 2'nin "ses" bedeli yalnız Defne'nin seslerini ve bir müzik katmanını susturur; oyun işaretleri kalır. |
| K-9 | Küçük hayırlar defterde puansız tek satırla sayılır. |
| K-10 | Şeytan hep "ondan" der ("Sana X, ondan Y"). |
| K-11 | Kapsam hedefi §14'teki **Minimum** katmandır; İsteğe bağlı ekler Minimum oynanır olunca başlar. |
| K-12 | Diriliş: can yeni azami değere dolar, 2,0 sn dokunulmazlık (Sözleşme 5'ten sonra 0), şok dalgası 6 m içindeki bütün düşmanları (iri dahil) parçalar. |
| K-13 | Pala dalgası kalkanı yok saymaz; Defne'den düşen vokseller isabet alanını küçültmez. |

### Açık sorular (❓ AÇIK — kullanıcı kararı bekler)

| # | Soru | Seçenekler |
|---|---|---|
| A-4 | 6. ölümde ne olur? | Seçimsiz, doğrudan son / gri ve gecikmeli kısa oynanış, sonraki ölümde son / kart çıkar ama HAYIR grileşmiş |
| A-9 | "Roket ×2" yarıçap ×2 mi (alan ×4), alan ×2 mi (yarıçap 3,5 m)? | Alan ×2 dengeli / yarıçap ×2 harfiyen |
| A-10 | Sözleşme 4'ün +%10'u bilerek boş vaat mi? Hiçbir öldürme eşiğini değiştirmiyor. | Bilerek boş / eşiği aşan değer (≥ +%34) |
| A-11b | Ölümde takılı fısıltı modüllerine ne olur? | Dirilişte kalır / düşer |
| A-12 | İç L duvarlar kırılabilir mi? Yankı'nın sekme yüzeyleri ve geçit ölçüleri değişir. Minimum'da sağlamdır. | Sağlam kalır / kısmen kırılır (Faz 4 testi) |
| A-15 | R oyun içinde de mi çalışır? | Yalnız sonuç ekranı (ilk GDD) / her an (plan tablosu) |
| A-16 | "Hasardan kaçınma" skoru zorunlu ölümlerle her koşuda 0 olur. Kalsın mı? | Kalsın / değişsin / çıkarılsın |
| A-18 | İmp patlaması diğer düşmanlara vurur mu? | Evet / hayır |
| A-19 | Kişi başı iş dağılımı nedir? (Teslim: 2026-10-26 15:00, bkz. §14.) | — |

---

## Değişiklik geçmişi

- 2026-10-23 — SefaSelim (Claude Code) — boş GDD iskeleti oluşturuldu.
- 2026-10-24 03:40 — Burak (Claude Code) — ilk GDD ile birleşik tasarım (Şeytan sözleşmeleri, üç silah, beş düşman, ölüm takvimi, sonlar, defter, hikâye, replikler, kapsam) yazıldı; 19 açık soru listelendi.
- 2026-10-24 03:57 — Burak (Claude Code) — jam teslim tarihi kaydedildi (2026-10-26 15:00).
- 2026-10-24 03:59 — Burak (Claude Code) — Burak "önerilerini uygula" dedi: 13 açık soru öneriler doğrultusunda karara bağlandı (K-1…K-13, ör. ölüm garanti, modüller yalnız tüfekte, Minimum kapsam); 9 soru açık kaldı.
- 2026-10-24 04:14 — Burak (Claude Code) — Faz 1 başlangıcı: yer tutucu blok arena (§9 yerleşimi) ve sabit pikselli izometrik kamera (§13) kuruldu.
- 2026-10-24 04:30 — Burak (Claude Code) — Faz 2: hareket, nişan, kaçınma, tüfek, roket, pala ve vuruş hissi oynanır hâle geldi (§4, §5 🟡); pala beklemesi vuruş bitince başlar.
- 2026-10-24 04:40 — Burak (Claude Code) — Faz 3 (Minimum): ölen hedeflerin küplere dağılması eklendi (§13).
- 2026-10-24 04:58 — Burak (Claude Code) — Faz 4 (Minimum): Takipçi, Koşucu, Nişancı, dalga yöneticisi ve doğuş güvenliği eklendi; imp/iri yerine konan dağılım ve ilk bot ölçümü yazıldı (§6, §10).
