# MAIN GDD — Dance with the Devil (Bir Şey Olmaz)

**Son güncelleme: 2026-10-27 03:41 (UTC+3)**

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
- Bir koşu yaklaşık 5 dakikadır. *(2026-10-25: kullanıcı zorluğu bilerek iki katına çıkardı, "amaç oyuncuyu öldürmek";
  koşular şimdilik tahminen ~2:30–3:15 sürüyor, bkz. §3 ve K-21. Süre hedefi oyun testine kadar 🧪 geçicidir.)*

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
sade bot, küçük turkuaz detaylar. **Oyundaki model (2026-10-24, kullanıcının eklediği):** kıvırcık koyu saçlı, yuvarlak
gözlüklü, haki/zeytin rengi asker kıyafetli kadın; botlar, kemerde kesecikler. Tulum ve turkuaz detay bu modelde yok.

**Giriş metni (koşu başında, tek satır):** Defne bu gece de her şeye evet dedi. "Bir şey olmaz." Altında hedef satırı:
"Dayanabildiğin kadar dayan. Arkası kesilmez."

**Oyun metinleri (2026-10-24):** oyundaki bütün Türkçe metinler gündelik, sıcak, samimi bir Türkçeyle yeniden yazıldı;
sayılar ve kurallar değişmedi. Güncel metinler ilgili bölümlerde (§7, §8, §11, §12) yazılıdır.

## 3. Temel döngü (~5 dk → şimdilik ~2:30–3:15 🧪) — 🟡 (ölüm → Şeytan → EVET/HAYIR döngüsü oynanıyor; takvim henüz dengelenmedi)

> **Not (kullanıcı kararı 2026-10-25, K-21):** zorluk bilerek iki katına çıkarıldı ("amaç oyuncuyu öldürmek"; düşman
> canları aynı). Tahmine göre ilk ölüm ~0:20–0:35'te, sonrakiler 20–35 sn arayla gelir; hep-EVET koşusu ~2:30–3:15 sürer.
> Aşağıdaki ~5 dk döngü ve ölüm takvimi bu yüzden **🧪 geçicidir**; insan oyun testinden sonra yeniden yazılır. Koşuyu
> uzatmak gerekirse yalnız ilk iki pencerenin aktif sınırı düşürülür (12 → 8, 16 → 12; bkz. §10).

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

**Ölüm takvimi (eski hedef, orta beceri; K-21'den sonra 🧪, yukarıdaki nota bakın):** ~0:50, 1:40, 2:30, 3:15, 4:00 → 5 sözleşme. **6. ölümde** Şeytan **yalnızca alır**
("Son anlaşma: neyin kaldıysa"). Ölümleri zorluk getirir: düşman canları sabittir; sayı ve tür karışımı büyür. **Ölüm garantidir; ilk GDD'deki "5. dakikada dalga temizlenince kazanılır" koşulu kalkar** (karar K-4). Zorluk adil ve okunur kalmalıdır.

**Diriliş (karar K-12, şok dalgası K-16):** can yeni azami değere dolar; 2,0 sn dokunulmazlık; şok dalgası 6 m içindeki
bütün düşmanları parçalar (K-16: artık bütün arena); dalga saati kaldığı yerden sürer. Sözleşme ekranı açıkken dalga saati de durur.

## 4. Kontroller — 🟡 (hareket, nişan, kaçınma, tüfek, roket, pala çalışıyor; ret, Q ve R henüz yok)

| Girdi | İşlev |
|---|---|
| WASD | Kamera düzlemine göre hareket (6 m/sn; Keskin Göz takılıyken ateş ederken 5,1 m/sn, §7) |
| Fare | Nişan: imlecin altındaki düşmana, yoksa imlecin gövde yüksekliğindeki noktasına; mermiler silahın ucundan o noktaya gider. Ekranda piksel artı nişangah |
| Sol tık / basılı tut | Tüfek |
| E / orta tık | Roket (şarjlı) |
| F | Pala (yakın vuruş, her an kullanılabilir) |
| Space | Kaçınma |
| Sağ tık | İmleç yönüne reddetme darbesi |
| Q basılı | Takılı modülleri karakter çevresinde göster |
| Q + sağ tık | Vurgulanan modülü sök ve imleç yönüne fırlat |
| R | Yeniden başlat (yalnız sonuç ekranında; ❓ A-15) |
| Esc | Duraklat: "Devam" / "Çık" |

**Kaçınma (ilk GDD §5):** ~3 m / 0,18 sn, bekleme 1,6 sn. Hareket yönüne, girdi yoksa imleç yönüne gider. Sırasında
hasar alınmaz, kabul halkası ilerlemez, ateş ve pala kesilir; ret/sökme girdisi tamponlanmaz.

**Hareket:** tam hıza ~0,06 sn'de çıkar, ~0,05 sn'de durur; gövde nişana hızlıca döner.

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

**Saldırı hızı (kullanıcı kararı 2026-10-24, K-19) — 🟡 (uygulandı, oyun testi bekliyor):** iki kaynaktan gelir:
**Hız** modülü (§7, yalnız tüfek ×1,4) ve **Sözleşme 4** (§8, tüfek ve pala ×1,25). Kaynaklar çarpımla birikir. Tüfekte
saniyedeki atış sayısı artar (6 → Sözleşme 4 ile 7,5 · Hız ile 8,4 · ikisiyle 10,5). Palada yalnız vuruştan sonraki
bekleme kısalır (0,5 → 0,4 sn); vuruşun kendisi 0,2 sn kalır. Hız palayı etkilemez (K-7). Roketin dolum süresini yalnız
Sözleşme 1 değiştirir.

**Kritik vuruş (kullanıcı kararı 2026-10-25, K-20; taban şans ve puan K-22) — 🟡 (uygulandı, oyun testi bekliyor):**
Defne'nin düşmana verdiği her silah isabeti **%12** ihtimalle kritik olur (tüfek, pala, pala dalgası, roket; önceden %5)
ve **iki kat** hasar verir (bütün çarpanlardan sonra uygulanır). Tüfekte ihtimal
mermi çıkarken bir kez atılır: kritik mermi deldiği her düşmana ve Yankı'yla sektikten sonra da kritik vurur; Üçlü Atış'ta
her mermi ayrı atılır. Palada, pala dalgasında ve rokette her düşman için ayrı atılır. Fırlatılan ya da sökülen modülün
patlaması, diriliş şok dalgası ve Defne'ye gelen hasar (kendi seken Yankı mermisi dahil) kritik olmaz. Kritik ihtimali iki
kaynaktan artar ve toplanır: **Keskin Göz** modülü (§7, yalnız tüfek +%20) ve **Sözleşme 3** (§8, bütün silahlar +%15).
Tüfekte en çok **%47** (12 + 20 + 15), pala, pala dalgası ve rokette en çok **%27** (12 + 15). Kritik isabet komboda normal
isabet gibi sayılır, ek kombo vermez; ama **her kritik isabet +25 × kombo çarpanı ek puan** verir (×1,5'te +37,5; §11).
**Geri bildirim:** kritik vuruşta yüksek, metalik bir kritik sesi çalar ve vurulan noktadan altın-beyaz kıvılcımlar
saçılır; düşmanın üstünde daha büyük bir "KRİTİK! N" yazısı (N: verilen hasar) çıkar (§11b) ve ekran kısa bir renk kayması
ile bükülme halkası atar (§13). Not: roketin 40 hasarı bugünkü bütün düşmanları zaten öldürdüğü için roket kritiği hasar
olarak fark yaratmaz; yalnız görünür ve ek puanını verir.

## 6. Düşmanlar — 🟡 (Takipçi, Koşucu ve Nişancı oyunda, Synty modelleriyle ve ekibin yaratık animasyonlarıyla; patlayan imp ve kalkanlı iri Minimum kapsam dışında)

Canlar dalgalarla artmaz. Ön işaret süreleri hiçbir sözleşmeden etkilenmez (okunabilirlik).

**Görünüş (🟡, 2026-10-24):** düşmanlar dokulu Synty POLYGON Dark Fantasy karakterleridir: **Takipçi** boynuzlu bir
şeytan, **Koşucu** bir iskelet, **Nişancı** bir cadı. Her biri kendi kimlik rengine boyanır (Takipçi kırmızı, Koşucu
turuncu-kırmızı, Nişancı şarap-macenta). Nişancının mermisi küp değil, parlayan turuncu bir büyü küresidir: arkasında
~4 m'lik sönen bir iz ve korlar bırakır, böylece yaklaşan mermi uzaktan okunur (2026-10-25). **2026-10-25:** düşmanlar artık parlamaz (kenar ışığı kapalı), Defne'yle aynı
parlaklıktadır. Ölünce kendi dokularının renklerinde küçük piksellere dağılırlar (§13). Takipçi'nin ön işaretinde kollarını açması korunur.
**Animasyonlar (🟡, 2026-10-25):** ekibin eklediği yaratık paketiyle hareket ederler. Takipçi ve Koşucu kambur bir
yaratık gibi bekler, yürür ve koşar; atıldıkları anda pençe savururlar. Takipçi'nin ön işareti pençeyi kaldırmasıdır
(kolları yukarı açılır), Koşucu'nunki çömelmedir (ikisi bilerek farklı okunur). Nişancı soluk alarak bekler, geri çekilip
yana kayarken nişancı paketinin adımlarını kullanır, küreyi fırlatırken pençe savurur. Vurulunca üst gövde sarsılır.
Ölüm klibi hazırdır ama oynatılmaz: düşmanlar ölünce piksellere dağılır.

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

## 7. Fısıltı modülleri: kabul, ret, sökme — 🟡 (altı modül, temasla kabul, ret, Q ile sökme kodda; elle oyun testi bekliyor)

Savaş sırasında Şeytan'ın "fısıltıları" yüzen modüller olarak gelir. Kurallar ilk GDD §6–7'deki gibidir. Bunlar
**geri alınabilir** evetlerdir; ölüm sözleşmeleri (§8) **geri alınamaz**.

| Konu | Kural |
|---|---|
| Yaklaşma | Oyuncudan ≥4 m uzakta boş zeminde belirir, yavaşça yaklaşıp ~2 m'de durur; kendisi kabulü tetiklemez |
| Niyetli kabul | Çekirdeğe 0,45 sn temas; dolan halka gösterir; ayrılınca sıfırlanır; geçip gitmek veya kaçınarak üstünden geçmek sayılmaz |
| Kapasite | (kullanıcı kararı 2026-10-25, K-23) **Üç yuva:** altı modülün herhangi üçü aynı anda takılı olabilir; her türden en fazla 1. Kabul edilen modül ilk boş yuvaya girer; sökülen modülün yuvası boşalır, diğerleri yerinde kalır. **Takılı bir tür bir daha teklif edilmez.** **Üç yuva da doluyken teklifler gelmeye devam eder:** teklif "DOLU" gösterir (yerde çarpı, şeritte "DOLU"), üstünde durmak kabul etmez; oyuncu Q + sağ tıkla eski bir modülü söküp yuvayı boşaltınca aynı teklif alınabilir. Altta "Yuvalar dolu · Q basılı + Sağ tık: birini sök" ipucu çıkar (K-21'in "dolu yuvada teklif yok" kuralı kalktı) |
| Teklif ömrü | Aynı anda en fazla 1 teklif; 14 sn sonra söner; tekrar kovalamaz |
| Ret | Sağ tık: imleç yönünde 60° koni ve 3 m içindeki en yakın teklif fırlatılır; darbe kendisi hasar vermez; boşa da 0,65 sn bekleme |
| Fırlatılan modül | ~16 m/sn; ilk düşman/duvar temasında ya da 0,7 sn sonra patlar; 1,8 m içinde 25 hasar ve kısa savurma; duvar arkasına işlemez, oyuncuya vurmaz; bir kez patlar |
| Sökme | Q + sağ tık; takılı modül aynı fırlatma davranışıyla tüketilir; yerden geri alınamaz; retle aynı beklemeyi paylaşır. **Seçim çemberi (K-23):** Q basılıyken Defne'nin çevresinde üç yuva yeri (sol, üst, sağ) belirir; her dolu yuvada o modülün simgesi (teklifte görünen şeklin aynısı) yavaşça döner, imlece en yakın olan büyür ve sökülecek olandır |
| Sıklık | İlk teklif ~8. sn, sonra ~25. ve ~42. sn; öğretimden sonra 14–20 sn aralık; takılı olmayan türler torbadan karışık gelir |

Tablodaki Avantaj, Bedel ve Fısıltı yazıları oyunda teklifin üstünde görünen metinlerdir; parantez içindekiler kuraldır.

| Modül | Avantaj | Bedel | Fısıltı |
|---|---|---|---|
| **Üçlü Atış** (simgesi: yelpaze gibi açılan üç mermi, 2026-10-25) | Tek atışta 3 mermi (±12°), hepsi tam hasar | Her atış seni nişanın tersine iter (hız katkısı ≤2,5 m/sn, duvarda birikmez) | Bir tetik, üç mermi. Azıcık teper, o kadar. |
| **Yankı** | Mermiler duvardan bir kez seker (görünür duvarlardan) | Seken mermi sana da çarpar (yarım hasar), 2. duvarda söner (sektikten sonra kalan menzilinin en çok yarısı kadar gider: 10 m'de seken mermi 15 m yerine 7,5 m daha gider; 2026-10-25) | Duvardan seker. Arada bir sana döner. Bir şey olmaz. |
| **Yoğunluk** | Mermi hasarı ×1,6 | Gövden ×1,5 büyür, daha kolay vurulursun (gövde ve isabet yarıçapı ×1,5; hız değişmez) | Daha sert vur. Biraz irileşirsin, kimse fark etmez. |
| **Hız** (kullanıcı kararı 2026-10-24, K-19; 🟡 oyun testi bekliyor) | Tüfek saniyede 6 → 8,4 atış (×1,4; palayı etkilemez, K-7) | Mermiler nişandan rastgele ±6° sapar (Üçlü Atış'ta her mermi ayrı ayrı sapar; yakında güçlü, ~8 m'den uzakta ıskalar) | Daha hızlı sık. Nereye mi gider? Aşağı yukarı oraya. |
| **Keskin Göz** (kullanıcı kararı 2026-10-25, K-20; 🟡 oyun testi bekliyor) | Tüfekte +%20 kritik şansı (%12 → %32; Sözleşme 3 ile %47; §5) | Ateş ederken yavaşlarsın (yürüme 6 → 5,1 m/sn) (yalnız sol tık basılıyken ×0,85; bırakınca hemen tam hız; kaçınma etkilenmez) | Gözünü kıs, tam ortadan vur. Biraz ağır yürürsün, bir şey olmaz. |
| **Delici** (kullanıcı kararı 2026-10-25, K-22; 🟡 oyun testi bekliyor) | Mermi 2 düşmanı daha deler (tüfek) (yalnız tüfek, K-7; Sözleşme 3'ün +2 delmesiyle toplanır, en çok 4 düşman daha) | Her delişte mermi %30 zayıflar (deldiği her düşmandan sonra hasar ×0,7, katlanarak: 10 → 7 → 4,9 → …; Delici takılıyken Sözleşme 3'ün delmesi de böyle zayıflar) | Bir mermi, üç düşman. Sondaki biraz gıdıklanır, o kadar. |

Sökülünce etkiler tek noktadan kalkar (ilk GDD §6): Üçlü tekleşir ve geri tepme durur; Yoğunluk gövdeyi ve hasarı
tabana döndürür; Yankı'nın havadaki mermileri oyuncuya zararsız olur; Hız sökülünce atış hızı ve sapma hemen tabana
döner; Keskin Göz sökülünce kritik ihtimali ve yürüme hızı hemen tabana döner, havadaki mermiler kendi atışlarını korur;
Delici sökülünce delme Sözleşme 3'ün değerine (yoksa sıfıra) döner ve zayıflama kalkar.
Ölümde takılı modüllerin ne olacağı ❓ A-11b.

Keskin Göz'ün bedeli bilerek hasar değil harekettir: tüfek hasarındaki her kesinti Takipçi'nin 3 vuruşluk öldürme eşiğini
bozar ve modülü tuzağa çevirir (tasarım notu `production/design/crit-difficulty.md` §3).

## 8. Şeytan'ın ölüm sözleşmeleri — 🟡 (altı sözleşme, kart ve bütün görünür bedeller oynanıyor; A-4, A-9, A-10 için geçici seçimler var)

**Eğri:** vaat küçülür, bedel büyür. Eğri sabittir, hikâye için yazılmıştır, rastgele değildir. Her bedel görünür ve
mekaniktir. Ölümde zaman donar, ekran solar, Şeytan sağ kenardan eğilir ve sözleşme kartı açılır: EVET / HAYIR.
Kartta bedelin hem şiirsel adı hem tam mekanik etkisi yazar (K-3). Kartın bedel satırları: **Ondan:** bedelin adı ·
**Görürsün:** görünen bedel · **Sayılarla:** mekanik bedel. Bedel adları Defne'den üçüncü kişiyle söz eder (K-10 ile
uyumlu): **Rengi, Sesi, Arkadaşları, Yüzü, Zamanı, Kendisi** (kart "Ondan: Rengi" der; defterdeki adlar da aynıdır).
EVET'in altında "dirilirsin · bedeli kalıcı", HAYIR'ın altında "oyun biter" yazar.
**Kart düzeni (2026-10-25, K-2/K-22):** kart ekranın sağ altında durur; elle çizilmiş Şeytan kartın sağ yanının önündedir.
EVET sağda, büyük ve parlaktır ve yerinde durur (Şeytan'la salınmaz); HAYIR solda, küçük ve sönüktür.
**Seçim:** iki düğme de **0,6 sn basılı tutularak** seçilir (kazara seçimi önlemek için): EVET Enter ya da fareyle; HAYIR
[H], gamepad B ya da fareyle. Basılı tutarken bütün düğme soldan sağa dolar (EVET altın, HAYIR soluk renkte); erken
bırakılırsa dolgu geri boşalır. Savaş tuşları (E, Space, sol tık) kartta hiçbir şey seçmez. HAYIR her zaman görünür ve
seçilebilir; ama Şeytan'ın sunumu EVET'i öne çıkarır (büyük, parlak EVET; küçük, sönük HAYIR; Şeytan HAYIR'ı küçümser) (K-2).
Şeytan oyuncuya "sen" der, Defne'den hep "o" diye söz eder: "Sana X, ondan Y" (K-10).

**Şeytan'ın görünüşü (🟡, 2026-10-25):** yer tutucu gravürün yerini ekibin elle çizdiği Şeytan aldı. Ekranın sağ altında,
sözleşme kartının sağ yanının önünde durur; EVET artık onun salınımını izlemez, yerinde durur. Kart, düğmeler ve Şeytan
kısa bir "açılma" hareketiyle belirir (§12).

| # | Vaat (yükseltme) | Bedel: görünür | Bedel: mekanik | Şeytan'ın teklifi |
|---|---|---|---|---|
| 1 | Roket hasarı iki katı (40 → 80), patlama alanı iki katı (yarıçap 2,5 → 3,5 m 🧪, ❓ A-9; yarıçap ×2 olsaydı 5 m), dolum %40 hızlı (6,0 → 4,3 sn) | **Rengi:** dünya %30 solar, Defne'nin rengi çekilir | Hiçbir şey | Sana iki kat güçlü roket, hem de daha sık. Ondan mı? Biraz renk. Fark etmez bile. |
| 2 | Pala savurunca öne bir dalga da çıkar; kaçış hakkı 1 → 2 | **Sesi:** Defne'nin sesi susar, müzik boğuklaşır (uyarı sesleri kalır, K-8); can çubuğu kısalır | Can sınırı 100 → 80 | Sana pala dalgası, bir kaçış hakkı daha. Ondan da sesini alıyorum. Nasılsa hep susuyordu. |
| 3 | Tüfek mermileri 2 düşmanı daha delip geçer (kalkanı delmez*); bütün silahlarda +%15 kritik şansı (tüfek %12 → %27, Keskin Göz'le %47; pala, pala dalgası ve roket %12 → %27; K-20, K-22, §5). Delici modülü takılıyken bu delme de her düşmanda %30 zayıflar ve +2 daha eklenir (en çok 4; §7). Şeytan'ın delme yükseltmesi yalnız bu sözleşmedir | **Arkadaşları:** ekrandaki küçük fotoğraf kaybolur | Düşmanlar %15 daha hızlı | Mermilerin delip geçsin, arada bir de çifte vursun. Ondan da arkadaşlarını alıyorum. Zaten onlara hiç vakti yoktu. |
| 4 | Bütün hasar %10, saldırı hızı %25 artar: tüfek saniyede 6 → 7,5 atış, pala beklemesi 0,5 → 0,4 sn (roket değişmez) (❓ A-10; saldırı hızı K-19, 🟡 oyun testi bekliyor) | **Yüzü:** Defne'nin yüzünden parçalar kopar, geri gelmez (vurulma alanı küçülmez, K-13) | Can sınırı 80 → 60 | Biraz daha sert, biraz daha seri. Ondan yüzünü alıyorum. Aynaya bakmıyordu nasılsa. |
| 5 | Bir roket hakkı daha (2 → 3) | **Zamanı:** ekranın kenarları kararır (doğuş işaretleri kararmanın üstünde çizilir*) | Dirilince koruma süresi yok; kaçış beklemesi iki katı (1,6 → 3,2 sn) | Bir roket daha. Ondan da zamanını alıyorum. Nasılsa kendine hiç ayırmıyordu. |
| 6 | — | **Kendisi:** Defne griye döner | Tuşlar 0,25 sn geç tepki verir*, kaçınılmaz sona gider (❓ A-4) | Son anlaşma: ondan geriye ne kaldıysa. / Bu sefer sormuyorum. |

\* başlangıç önerisi.

**Birikme kuralları (başlangıç önerisi):** hasar çarpanları çarpımla birikir (Yoğunluk ×1,6 × Sözleşme 4 ×1,1).
Saldırı hızı çarpanları da çarpımla birikir (Hız ×1,4 × Sözleşme 4 ×1,25 = ×1,75; tüfekte en çok 10,5 atış/sn).
Azami can kayıpları toplanır ve kalıcıdır (100 → 80 → 60); diriliş yeni azami değere doldurur. Bekleme çarpanı tabana
uygulanır, şarj sayısını değiştirmez.

**İlk Şeytan görünüşü (1. ölümde, teklif satırından önce):** "Pışt! Sana diyorum. Evet, ekrandaki sen."

**Oyuncu HAYIR dediğinde:** 1. ölümde "Daha ilk seferden hayır mı? Amma sıkıcısın." · 2–5. ölümde
"Şimdi mi hayır diyorsun? Aldıklarım bende kalıyor ama."

## 9. Arena — 🟡 (uçurumdaki mezarlık haritası, 8 giriş; oyun testi bekliyor)

İlk GDD §9 düzeni: ~32 × 28 m dikdörtgen platform, kesintisiz dış sekme duvarı (kırılmaz), ~12 × 10 m açık merkez,
kuzeybatı ve güneydoğuda birbirini karşılayan iki L duvar, en dar geçit 4 m, dört kenarda işaretli giriş. Kamera
yakınındaki duvarlar karakteri gizlediğinde saydamlaşır. İç L duvarlar ve dekorlar kırılabilir olarak planlandı;
ne kadarının kırılacağı Faz 4'te dengelenir (❓ A-12: Yankı'nın sekme yüzeyleriyle çakışma).

**Kamera (K-14):** Defne'yi takip eder, nişana doğru en fazla 2 m öne bakar (oyun başında bu pay ilk saniyede yavaşça gelir); metre başına ~20 piksel. Mezarlık haritasında arena dışındaki deniz ve uçurumlar görünür (2026-10-27).

**Harita: uçurumdaki mezarlık (kullanıcı kararı 2026-10-27):** arena, ekibin seçtiği hazır Synty haritasıdır: deniz
üstünde yükselen bir kayalık adanın tepesinde, alçak taş çitle çevrili bir mezarlık. Eski blok arena ve gotik avlu kalktı.
- **Ölçek ve zemin (2026-10-27):** harita karakterlere göre 1,5 kat büyütüldü. Düzlüğün engebeli toprak-çimen zemini,
  eski arenanın düz, çatlak taş karolarıyla değiştirildi (tamamen düz, yükseklik 0); mezarlar, türbeler ve bitkiler bu
  zemine oturur. Haritanın kendi sisi neredeyse tamamen kaldırıldı (yalnız çok hafif bir iz kalır).
- **Saydamlaşma (2026-10-27):** kameradan bakınca Defne'nin önünde kalan yüksek her şey (türbeler, çit, ağaçlar, büyük
  mezarlar) 0,15 sn içinde neredeyse saydam olur (%12); Defne önünden çekilince geri gelir. Alçak dekor (yassı taşlar,
  bitkiler) hiç saydamlaşmaz.
- **Oynanabilir alan:** adanın düzlüğü, ~45 × 38 m (x −24…+22,5, z −20…+18). Kenarında görünmez duvarlar vardır, Defne
  uçurumdan düşemez. Dışarıda deniz, uçurumlar ve uzak kaya adaları görünür (bu harita bilerek manzara gösterir).
- **Çit ve siperler:** ~35 × 27 m'lik mezarlık çiti (2,9 m, sütunlar 3,6 m) iç halka olarak durur ve mermiyi keser; kuzeyde,
  güneyde, güneydoğuda, doğuda (eski kapı kaldırıldı) ve batıda geniş bir yıkıkta açıklıkları vardır. Çitin dışındaki
  çimenli halka da oynanabilir. İki büyük türbe (kuzeybatı, kuzeydoğu), kuyu, ölü ağaçlar ve kenardaki büyük mezarlar
  siper ve yön işaretidir.
- **Açık merkez:** Defne'nin başlangıcı (orta) çevresinde ~16 × 13 m'lik boş bir alan açıldı; ortadaki türbe ve aradaki
  mezar taşları, cesetler, mumlar kaldırıldı. Girişlerden merkeze ~5 m'lik yollar açıktır.
- **Girişler:** sekiz giriş, adanın kenarına dağılmış (K, KD, D, GD, G, iki B, KB), hepsi başlangıçtan 16–23 m uzakta;
  her birinde içe bakan şeritli zemin plakası ve doğuştan önce yanan uyarı vardır. Düşmanlar her birinden merkeze yürüyerek
  ulaşır (yol bulma yalnız adanın düzlüğünde; deniz, uçurum ve merdivenler dışarıda).
- Küçük dekorun (cesetler, mumlar, mangallar, yassı mezar taşları, kütükler, bitkiler) çarpışması yoktur; büyük mezarlar,
  türbeler, çit, ağaç gövdeleri ve kayalar engeldir. Kamera, Defne'yi takip ederken odağını adanın ortasında tutar
  (merkezden en çok ±12 m doğu–batı, ±9 m kuzey–güney).

## 10. Dalgalar ve zorluk — 🟡 (dalga yöneticisi çalışıyor; mermi cehennemi zorluğu K-21 uygulandı; ölüm takvimi henüz dengelenmedi)

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

**2026-10-24 uygulaması (aşağıdaki mermi cehennemi bunun sayılarını ikiye katlar):** her pencerenin bütçesi pencere boyunca 2–4 kişilik paketlere
bölünür (16 sn'ye yayılır), arena ~3 sn'den fazla boş kalmaz, paketler tek girişten gelir. Takipçi ve Koşucu oyuncunun
gideceği yere yönelir ve atılma mesafeleri uzar (Takipçi 2,6 m'ye, Koşucu 2,2 m'ye kadar); Nişancı hedefin hareketine göre
önden nişan alır ve atışlar arasında yer değiştirir. Başlangıç değerleri: Takipçi hızı 3,5 → 4,2 m/sn, Koşucu hasarı 6 → 8,
Nişancı mermisi 12 → 14 m/sn (başlangıç önerisi). Pencere sayıları ilk tablonun yaklaşık 1,75 katı, aktif sınır +3
(K-17: 3:00'ten sonra artmaya devam eder; K-15: 5:00'ten sonra her 30 sn'de +1 Takipçi, +1 Koşucu, +1 Nişancı, sınır +1, en çok 16).
Bot ölçümü (kaçınan, dolaşan, ±12° ıskalayan bot): sayılar ×1,35 iken ilk ölüm 1:47; ×1,75 iken ölümler 0:33 ve 1:14; şu anki değerler (×1,5) ikisinin ortası. Takvim insan oyun testiyle son hâlini alacak.

**Güncel uygulama — mermi cehennemi (kullanıcı kararı 2026-10-25, K-21) — 🟡 (uygulandı, oyun testi bekliyor):** amaç
oyuncuyu öldürmek. Düşman canları değişmez; her pencerenin düşman sayısı ve aktif sınırı **iki katına** çıkar. Paketler
pencerenin ilk **9 sn**'sine yayılır (18 yerine), bir paket en çok **6** düşmandır (4 yerine). 5:00'ten sonra her 30 sn'de
**+2 Takipçi, +2 Koşucu, +2 Nişancı, sınır +2**; aktif sınır en çok **32** (16 yerine). Doğuş uyarısı (0,8 sn), 9 m doğuş
mesafesi ve "Nişancılar 3 sn içinde en çok 2 girişten" kuralı aynen kalır; okunabilirlik bunlara dayanır.

| Pencere | Takipçi | Koşucu | Nişancı | Aktif sınır |
|---|---|---|---|---|
| 0:00–0:30 | 36 | – | – | 12 |
| 0:30–1:00 | 42 | 24 | – | 16 |
| 1:00–1:30 | 34 | 16 | 8 | 18 |
| 1:30–2:00 | 34 | 16 | 8 | 20 |
| 2:00–2:30 | 30 | 16 | 10 | 20 |
| 2:30–3:00 | 30 | 20 | 10 | 22 |
| 3:00–3:30 | 34 | 20 | 10 | 22 |
| 3:30–4:00 | 34 | 24 | 10 | 24 |
| 4:00–4:30 | 36 | 24 | 16 | 24 |
| 4:30–5:00 | 36 | 28 | 16 | 26 |
| 5:00 sonrası | +2 | +2 | +2 | +2 (en çok 32, 6:00'da) |

Kaba hesap: 0:30'dan sonra hiçbir oyuncu doğuş hızına yetişemez; arena hep aktif sınır kadar doludur, asıl zorluğu aktif
sınır belirler. İlk ölüm tahminen 0:20–0:35, sonrakiler 20–35 sn arayla; hep-EVET koşusu ~2:30–3:15 sürer. Bu, "~5 dakika"
hedefinden kısadır; kullanıcı bunu bilerek istedi, §3'teki süre 🧪 geçicidir. Koşuyu uzatmak gerekirse yalnız ilk iki
pencerenin aktif sınırı düşürülür (12 → 8, 16 → 12). Yankı'nın sekme sonrası kısalan menzili (§7) kalabalıkta kendi
mermisine yakalanmayı azaltır.

**Önceki uygulama (Minimum kapsam, K-11):** imp ve iri olmadığı için tablodaki her imp yerine 1 Takipçi + 1 Koşucu,
her iri yerine 2 Nişancı doğar; pencerelerin tehdit puanı aynı kalır. Her pencerenin bütçesi 0., 4. ve 8. saniyelerde
üç alt gruba bölünür. Nişancılar 3 sn içinde en fazla 2 farklı girişten gelir. Uyarı süresindeki doğuş da aktif sınıra
sayılır; oyuncu uyarı sırasında girişe 9 m'den fazla yaklaşırsa doğuş başka girişe kayar. Yeni doğuşlar 5:00'te durur.

**İlk bot ölçümü (2026-10-24, 🧪 tahmini veri):** yerinde durup ±12° ıskalayan, 0,35 sn tepki süreli, kaçınmayan bir
bot; her ölümde tam canla dirilip 2 sn dokunulmazlık ve 6 m şok dalgası alıyor, sözleşme etkileri yok. Ölümler:
1:07, 1:21, 1:43, 2:16, 2:38, 2:48, 3:14, 3:26, 3:39, 4:09, 4:22, 4:34 (12 ölüm). Okuma: ilk ölüm hedeften (0:50) geç,
sonrakiler hedeften çok sık. Bot hareket etmediği ve kaçınmadığı için bu bir alt sınırdır; takvim insan oyun testiyle
ayarlanacak.

## 11. Sonlar ve sonuç ekranı — 🟡 (üç son, defter ve küçük hayır sayısı oynanıyor; puanın düşman ve kritik kalemleri kombo çarpanıyla sayılıyor, diğer kalemler yok)

| Durum | Metin |
|---|---|
| **1. ölümde HAYIR** | **Kaybettin ama kendin kaldın.** Defne olduğu yere çöktü, nefes nefese. Saçları hâlâ kıvırcık, hâlâ rengârenk. *Yarın bir şeye hayır diyecek. Belki ikisine.* (Tam renkli Defne.) |
| **2–5. ölümde HAYIR** | **Eksildin ama hâlâ ayaktasın.** {evet sayısı} kere evet dedin. Defne'den gidenler: (verilenler alt alta: Rengi · Sesi · Arkadaşları · Yüzü · Zamanı) *Hayır biraz geç geldi. Ama geldi.* |
| **Hep EVET (6. ölüm)** | Şeytan: **"Bir şey olmadı, değil mi?"** Gri kabuk dağılır. Son kartı: **Oysa hayır da diyebilirdin.** Düğme: *Bir daha dene* (yeni koşu). |

**Küçük hayırlar:** defterde puansız tek satır: "Küçük hayırların: N" (savaşta reddedilen ve sökülen modüller, karar K-9).

**Sonlarda Defne:** sonuç ekranında Defne'nin çizilmiş portresi (ekibin çiziminden, 2026-10-25) koşunun bıraktığı
hâliyle görünür (Sözleşme 1: yarı solgun, 4: yüzünde karanlık bir peçe, 5: daha karanlık, 6: tamamen gri); defter
satırları kartın içine sığar, uzun satırlar alt satıra geçer (1. ölümde HAYIR: tam renkli; sonraki HAYIR: alınan bedeller; hep EVET: gri kabuk dağılır).

**Sonuç ekranı:** skor yerine **"Aldıkların / Verdiklerin"** defteri gösterilir ("Verdiklerin" üstünde küçük not: *Defne'den*).
İlk GDD §10 skoru en altta, küçük kalır; ekranda **"Puan"** diye yazar. 1. ölümde hayır denirse defter: "Aldıkların:
hiçbir şey · Verdiklerin: hiçbir şey. Kendin kaldın."

| # | Aldıkların | Verdiklerin |
|---|---|---|
| 1 | Roket gücü | Rengi |
| 2 | Pala dalgası, +1 kaçış | Sesi · can sınırı −20 |
| 3 | Delen mermi, +%15 kritik | Arkadaşları · düşmanlar %15 daha hızlı |
| 4 | +%10 hasar, +%25 saldırı hızı | Yüzü · can sınırı −20 |
| 5 | +1 roket | Zamanı · dirilince koruma yok, kaçış beklemesi ×2 |
| 6 | Hiçbir şey | Kendisi |

**Skor (ilk GDD §10; oyunda "Puan"):** düşman +100 (aşağıdaki kombo çarpanıyla), **her kritik isabet +25 × kombo
çarpanı** (K-22, uygulandı; §5), isabetli modül fırlatma +25, hızlı temizleme bonusu, hasardan kaçınma (❓ A-16).

### 11b. Kombo ve skor çarpanı — 🟡 (uygulandı, oyun testi bekliyor; kullanıcı kararı 2026-10-24, K-18)

Defne'nin düşmana verdiği her isabet komboyu büyütür; kombo büyüdükçe öldürme puanı katlanır. Komboyu yalnız oyalanmak
(3 sn isabetsiz) ya da Defne'nin ölümü sıfırlar; hasar almak artık sıfırlamaz (K-22).

| Konu | Kural |
|---|---|
| Artış | Hasar veren her isabet, hasar alan **her düşman için +1**; düşmanı öldürmek **ayrıca +1**. Üçlü Atış, delen mermi, roket ya da pala aynı anda birden çok düşmana vurursa her biri sayılır (roket 5 düşmanı öldürürse +10). |
| Sayılanlar | Defne'nin verdiği hasar: tüfek (sekmiş Yankı mermisi dahil), roket, pala ve pala dalgası, fırlatılan ya da sökülen modülün patlaması. Hasar vermeyen isabet ve diriliş şok dalgasının vurdukları/öldürdükleri sayılmaz. |
| Süre | Son sayılan isabetten sonra **3,0 sn** içinde yeni isabet gelmezse kombo sıfırlanır. Arenada canlı düşman yokken (doğuş uyarısı sırasında da) süre işlemez. |
| Hasar | (2026-10-25, K-22) Defne'nin can kaybetmesi komboyu **sıfırlamaz** (kendi seken Yankı mermisi dahil). Yalnız ölüm sıfırlar; diriliş 0'dan başlar. |
| Donma | Şeytan ekranı, duraklatma ve vuruş duraklaması sırasında süre işlemez. |
| Çarpan | Her tam 5 kombo için +0,1; en çok **×3,0**. 10 kombo ×1,2 · 25 kombo ×1,5 · 50 kombo ×2,0 · 100 kombo ×3,0. Sayaç 100'ü geçebilir, çarpan ×3,0'da kalır. |
| Puan | Defne'nin öldürdüğü düşman **100 × çarpan** puan verir (×1,5'te 150). Öldüren isabet ve öldürme önce sayılır, puan yeni çarpanla verilir. Şok dalgasının öldürdükleri 100 puan, çarpansız. Çarpan düşman puanına ve kritik ek puanına (her kritik isabet **+25 × çarpan**, K-22, §5) uygulanır. |
| Yüksek kombo öldürmeleri | (2026-10-25, K-22) Çarpan **×2,0'a (50 kombo)** ulaştıktan sonra Defne'nin her öldürmesi eşik efektini (ekran sarsıntısı, yakınlaşma, şok dalgası halkası, ekran nabzı) **0,75 güçle** yeniden oynatır; en sık **0,12 sn'de bir** (arada gelen öldürmeler efektsiz kalır). Şok dalgasının öldürdükleri sayılmaz. Ekran sarsıntısı ayarıyla ölçeklenir. |
| Geri bildirim (2026-10-25, K-20) | Sağ üstteki kombo sayacı (sayı, çarpan, 3 sn çubuğu) **kalktı**. Çarpan puanın yanında görünür: "Puan 1200 ×1,5". Kombo ve kritik **dünyada beliren yazılarla** gösterilir; yazılar yükselip söner. **Kritik:** büyük "KRİTİK! N" (N: verilen hasar; 2026-10-25'te büyütüldü, K-22) vurulan düşmanın üstünde; ekranda en çok 3 tane, 0,2 sn'de en çok 1 yeni (fazlası gösterilmez, hasar yine uygulanır). **Her 5 kombo:** Defne'nin üstünde "N KOMBO". **Eşik** (10, 25, 50, 100): Defne'nin üstünde daha büyük, eşiğe göre renklenen "N KOMBO! ×1,5"; ekran sarsılır, görüntü efekti nabzı ve bükülme halkası atar (§13); vuruş duraklaması yoktur. Defne'nin üstünde aynı anda tek yazı durur (yenisi eskisinin yerini alır; taze eşik yazısı korunur). Yazılar duraklatmada ve Şeytan ekranında gizlenir. Kombo kırılınca ("KIRILDI" / "SÖNDÜ") yazı şu an gösterilmez (⬜). |

Hasar artışları düşman başına isabet sayısını azalttığı için komboyu yavaşlatır; saldırı hızı yalnız hızlandırır,
düşman başına kombo sayısını büyütmez. Kombo, düşman canıyla sınırlıdır.

## 12. Arayüz ve öğretim — 🟡 (HUD, üç modül yuvası, teklif şeridi, giriş metni ve ilk dakika ipuçları var; elle test bekliyor)

**İlk dakika ipuçları (uygulanan):** ekranın altında aynı anda tek satır. Başta "WASD: yürü · Fare: nişan al · Sol tık:
ateş et" (oyuncu hem yürüyüp hem ateş edince söner); ilk teklif gelince "Üstüne git, bekle: kabul · Sağ tık: hayır de"
(teklif kabul, ret ya da sönmeyle biter); ilk kabulden sonra 6 sn "Q'yu basılı tut + Sağ tık: modülü söküp fırlat";
20 sn roket kullanılmazsa 5 sn "E: roket at · F: pala savur · Space: kaç". Öncelik: teklif > sökme > yürü/ateş > roket. İpuçları oyunu durdurmaz;
Şeytan ekranı ve sonlarda gizlenir.

**Ana menü ve ayarlar (kullanıcı isteği 2026-10-24):** oyun ana menüyle açılır. Solda başlık "Bir Şey Olmaz", altında
"Dance with the Devil" ve dört düğme: **Başla** (ekran kararır, arena başlar), **Ayarlar**, **Emeği Geçenler**, **Çıkış**.
Menü 2B'dir (2026-10-25): sağda ekibin çizimi durur, Defne silahıyla önde, Şeytan arkasında, kolu omzunda; çizim
hafifçe nefes alır ve sallanır. Arkada kırmızıya boyanmış, loş bir Şeytan gravürü (Gustave Doré), yumuşak kırmızı bir
parıltı, havada süzülen korlar, kıvılcımlar, kül ve yavaş sis vardır (azaltılmış hareketle hepsi durur). Menü arka planı
her ekran oranında ekranı doldurur, kenarları yumuşakça kararır (2026-10-25). Altta "Hayır demeyi
öğrenmelisin." yazar. Menü fare, klavye (ok tuşları/W-S, Enter) ve gamepad ile gezilir; Esc ya da gamepad B açık
paneli kapatır.
- **Ayarlar** (sade): Genel ses, Müzik, Efektler, Ekran sarsıntısı (0 = kapalı) kaydırıcıları ve Tam ekran açık/kapalı.
  Değerler hemen uygulanır, kaydedilir ve oyun açılışında geri yüklenir.
- **Emeği Geçenler:** ekip satırı ve kullanılan varlıklar ile lisansları (CREDITS.md özeti).
- Oyundaki duraklatma menüsünde "Çık" yerine **Ana menü** vardır ve menüye döner.

**Arayüz görünüşü:** HUD, Şeytan kartı, son ekranı ve duraklatma paneli karanlık fantastik süslü metal çerçeveler
(Synty Interface Dark Fantasy HUD) kullanır; başlıklar, düğmeler ve Şeytan'ın sözleri gotik Pirata One, diğer yazılar
Markazi yazı tipiyledir. Yerleşim ve bilgiler değişmedi. **Düğmeler (🟡, 2026-10-25):** ana menü, duraklatma, son
ekranı ve Şeytan kartındaki düğmeler sadeleşti: ince çerçeveli koyu bir şerit (Synty Dark Fantasy HUD "WorldSpace_Item_01"),
seçili düğmede altın vurgu. Kartta EVET yine büyük ve parlak, HAYIR küçük ve sönüktür (K-2). **Hareket (DOTween):**
paneller, düğmeler, Şeytan kartı ve son ekranındaki defter satırları kısa bir "açılma" hareketiyle belirir.
**Teklif şeridi:** savaş içi teklifin avantaj/bedel yazısı, ekranın sağ üst köşesinde puanın altında küçük bir şerittir.

İlk GDD §11 korunur: üstte can, süre ve puan ("Puan", kombo çarpanı yanında: "Puan 1200 ×1,5"; sağ üstteki kombo sayacı
kalktı, kombo yazıları dünyada görünür, §11b); altta üç modül yuvası (K-23; boş yuva "—", dolu
yuva modülün renginde ve adıyla) ile ret/kaçış hazır göstergeleri ("Hayır [Sağ tık]", "Kaçış [Space]"); teklif şeridinde (sağ üst) tek satır
avantaj, tek satır bedel; takılı modülün sökme ipucu "Sağ tık: söküp at". Duraklatma panelinin başlığı "Mola"dır. Ek olarak HUD'da Defne'nin küçük fotoğrafı (ekibin çiziminden portre; bedelleri sonuç ekranındaki gibi taşır: 1 yarı solgun, 4 yüz peçesi, 5 daha karanlık, 6 gri; Sözleşme 3 bedeliyle kaybolur) ve roket
şarjları. Bağlamsal ipuçları ilk dakikada sırayla gelir, oyunu durdurmaz. Renk tek ayırt edici değildir.

**Yazılar tek Excel dosyasında (✅, 2026-10-25):** oyuncunun gördüğü bütün yazılar (menü, HUD, ipuçları, Şeytan kartı
ve sözleşmeler, son ekranı, kombo yazıları) `Assets/_Game/Texts/Yazilar.xlsx` dosyasındadır. Tasarımcı yalnız **Metin**
sütununu değiştirir (`{0}` gibi yer tutucular korunur); dosya kaydedilip Unity'ye dönülünce oyun yeni yazıyı gösterir.
Boş bırakılan hücrede koddaki varsayılan yazı görünür. Koda sonradan eklenen yazılar "Dance/Texts/Excel'i Güncelle"
ile tabloya girer.

## 13. Görsel ve ses yönü — 🟡 (takip kamerası, pikselli görüntü, dış çizgi, Synty düşmanlar, piksel dağılması, şok dalgası halkaları, kombo görüntü efektleri, elle çizilmiş Şeytan ve kayıtlı savaş sesleri var; yaratık animasyonlu düşmanlar var; toon ışık yok)

- **Görünüm:** Defne'yi takip eden ortografik izometrik kamera (35° eğim, 45° dönüş, metre başına ~20 piksel, K-14); karakterlerde 1 piksellik koyu dış çizgi; karakterler sahnenin en parlak ve en doygun şeyleri, zemin ve duvarlar koyu ve sakin; düşük çözünürlükte (640×360) çizilip nokta filtresiyle büyütülen, hafif
  pikselli voksel görünüm; düz gölgeli toon ışık; 1 px dış çizgi; kamera kayarken titreme olmaz.
- **Parçalanma:** vurulan her şey, düşmanlar ve çevre, piksellerine (küplere) dağılır. Roket geniş, pala düzlemsel kesik,
  mermi küçük delik açar.
  Şu an 🟡 (2026-10-25): ölen düşman, kendi dokusunun gerçek renklerinde **çok sayıda küçük piksele** (1–2,5 doku pikseli
  boyunda) dağılır: ölümde ~320 parça, roketle öldürülünce 400. Öldürmeyen her isabette 20–40 küçük piksel vuruş yönünün
  tersine yontulup saçılır. Parçalar sıçrar, yere düşer ve küçülerek kaybolur. Çevre kırılması ve delikler henüz yok
  (İsteğe bağlı katman).
- **Karakterler:** 🟡 Düşmanlar dokulu Synty POLYGON Dark Fantasy karakterleridir (Takipçi: boynuzlu şeytan, Koşucu:
  iskelet, Nişancı: cadı), kimlik renklerine boyanmış (kırmızı / turuncu-kırmızı / şarap-macenta); 2026-10-25'ten beri
  parlamazlar (kenar ışığı kapalı, Defne'yle aynı parlaklık); ölünce kendi renklerinde küçük piksellere dağılırlar. 🟡 Animasyonları ekibin yaratık paketinden gelir: kambur
  bekleme, yürüme, koşu ve atılırken pençe savurma; Takipçi pençesini kaldırarak, Koşucu çömelerek uyarır (bkz. §6).
  **Defne** artık kullanıcının eklediği dokulu 3B model ve hazır animasyonlarla oynar (nişanlı bekleme, yürüme, koşu, geri
  geri koşma, yana koşma; vurulunca üst gövde sarsılır). Hareket yönü nişana göre okunur: nişan alırken geri geri ya da
  yana koşar. Elinde gerçek bir AK tüfek var; roket atınca bir an omzuna RPG alır (atılan roket namludan çıkar, tüp boş
  görünür), sonra RPG sırtına döner. Koyu zeminde okunsun diye modelde sıcak bir kenar ışığı vardır.
- **Mermiler:** tüfek mermisi pirinç renkli küçük bir mermi ve arkasında kısa sarı-turuncu iz (tracer); roket, RPG'nin kendi
  savaş başlığı ve arkasında turuncudan griye dönen duman izi. Defne tüfeği omzunda tutar; namlu ateşi ve mermi izi
  tüfeğin ucundan dümdüz çıkar. Mermi yine yerden 1 m yükseklikte uçar ve vurur (isabet kuralları değişmez); yalnız
  görüntüsü namlu hizasında çizilir (2026-10-27).
- **Kamera ve ölçek (kullanıcı kararı 2026-10-24):** kamera yaklaştı (görünen yükseklik 18 m → 13 m) ve görüntü daha ince
  çizilir (640×360 → 960×540; hafif piksel his kalır ama karakterler bloklaşmaz). Defne 2,0 m boyunda; düşmanlar %15 büyük
  (çarpışma alanları da). Defne'nin modelinde dış parlama (kenar ışığı) yok.
- **Silahı tutuş:** tüfek sağ elde, sol el silahın ön tutamağına uzanır; RPG sırtta çaprazdır. Tutuş, ekibin oyun içinde
  ince ayar yaptığı bir profilden gelir (animasyon başına düzeltme yapılabilir).
- **Efektler (gerçekçi, dokulu):** tüfek ateşinde namlu parlaması, namlu boyunca kısa alev, kıvılcım ve ince duman; roket
  atışında ön alev ve arkadan geri ateş (RPG'ye özgü) ile duman bulutu; roket patlamasında parlama, sarıdan turuncuya ve koyu
  kırmızıya soğuyan ateş topu, yükselen duman, savrulan toprak, korlar, yerde toz halkası ve 6 sn kalan yanık izi; mermi
  isabetinde kıvılcım ve toz. Işık ve vuruş duraklaması eskisi gibi; küp parlama yerine bu efektler oynar.
- **Vuruş hissi (🟡, 2026-10-25):** ekran sarsıntısı çok daha güçlü (daha geniş kenar payı, hafif yuvarlanma ve kısa
  yakınlaşma tepmesi). Roket patlamalarında, öldürmelerde, kritik vuruşlarda, kombo eşiklerinde ve Şeytan'ın diriliş
  dalgasında görüntüyü büken **şok dalgası halkaları** yayılır; kritik vuruşta kısa bir renk kayması (chromatic) parlar.
  Hepsi Ekran sarsıntısı ayarıyla ölçeklenir, azaltılmış hareket seçeneğinde kapanır.
- **Şeytan:** ekranın sağ kenarından, monitör çerçevesinin dışından eğilen 2B çizim. 🟡 (2026-10-25) Yer tutucunun yerini
  ekibin elle çizdiği Şeytan aldı; sağ altta sözleşme kartının sağ yanının önünde durur, EVET yerinde sabittir (§8).
- **Müzik (kullanıcı kararı 2026-10-25, K-8 güncellendi):** oyun içinde "Tense Conversational Pulse" parçası döngüyle çalar
  (koddan üretilen iki katmanlı müziğin yerine). Sözleşme 2 alınınca müzik susmaz, 1,5 sn içinde boğuklaşır, uzaktan
  geliyormuş gibi olur.
- **Ses:** 🟡 kısa okunur SFX, karanlık tek müzik döngüsü; Sözleşme 2 bir müzik katmanını susturur. Müzik ve arayüz/uyarı
  sesleri koddan üretilir. Müzik Re minör, 90 BPM, 8 ölçülük (~21 sn) kesintisiz döngü; iki katman: taban (drone,
  bas nabız, kalp atışı) ve melodi (müzik kutusu, yankı). **2026-10-25:** savaş ve düşman sesleri artık kayıtlı ses
  dosyalarıdır (main dalından alındı); koddan üretilen sesler bunların olmadığı yerlerde kalır. Sözleşme 2: Defne'nin sesleri susar, melodi katmanı 1,5 sn'de
  kısılır; oyun işaretleri (vuruş, uyarı, kaçınma hazır, kabul/ret) kalır (K-8). Ses tasarımında geçici seçimler: Koşucu
  uyarısı Takipçi uyarısının tiz hâli; fırlatılan modül patlaması roket patlamasının tiz ve kısık hâli; kaçınma sesi oyun
  sesi sayılır.
- **Yer tutucular 🧪:** küp düşmanların yerini Synty düşman modelleri aldı (yukarıda); küp Defne yalnızca yeni model
  kurulmamışsa yedek olarak kalır. Yer tutucu renkler: koyu soğuk zemin ve duvarlar, krem sekme kenarı.
- **Görüntü efektleri (post-processing, 🟡 2026-10-24):** oyun kamerası artık görüntü efektleri kullanır; temel görünüş
  sakin bir parlamadır. Kombo eşik atlayınca (§11b) kısa bir nabız atar: parlama (bloom), renk kayması (chromatic
  aberration), hafif mercek bükülmesi, pozlama ve doygunluk artışı; sonraki eşikler daha güçlü vurur. Yüksek komboda
  (50 ve 100 eşiklerinde) yumuşak bir parlama ve doygunluk kalıcı olarak üstte durur, kombo düşünce söner. Ekran
  sarsıntısı ve mercek bükülmesi Ekran sarsıntısı ayarıyla ölçeklenir (0'da yok); mercek bükülmesi azaltılmış hareket
  seçeneğinde de kapanır. Şu an parlama/renk nabzını kısan bir ayar yoktur ve azaltılmış hareket seçeneği Ayarlar
  menüsünde görünmez (⬜).
- **Yeni Defne modelinde bedeller (🟡):** Sözleşme 1'de saçla birlikte bütün modelin rengi yarı yarıya çekilir (dokulu modelde
  saç ayrı parça değil); Sözleşme 6'da tamamen griye döner. Sözleşme 4'te yüzünden sırayla dört küp patlaması çıkar ama
  yüz dokusu yerinde kalır (yüzden gerçek parça eksilmesi ⬜). Kaçınmadaki iz görüntüleri (afterimage) bu modelde yoktur.

## 14. Kapsam (jam teslimine kadar) — ⬜

**Teslim:** 2026-10-26 15:00 (UTC+3), kullanıcı kararı. 24 Ekim gecesinden itibaren ~59 saat kalır. Öneri (producer): hedef **Minimum** katman olsun, İsteğe bağlı ekler ancak Minimum oynanır olunca başlasın.

**Kapsam ilkesi:** temanın taşıyıcısı ölüm anındaki Şeytan sözleşmesi ve "Hayır" seçeneğidir. Kesmek gerekirse önce
görsel gösteriş, sonra içerik çeşitliliği gider. Sözleşme → bedel → son zinciri en son kesilir.

| Katman | İçerik |
|---|---|
| **Minimum** | Tek arena (duvarlar sağlam olabilir); izometrik kamera ve düşük çözünürlüklü pikselli görünüm; parçalı voksel Defne; hareket, nişan, kaçınma, can; tüfek, roket, pala; Takipçi ve Nişancı; bütçeli dalgalar ve ölüm takvimi; üç fısıltı modülü ile kabul/ret/sökme; ölüm dondurması → Şeytan → sözleşme kartı → EVET/HAYIR; altı görünür bedel (basit uygulama yeterli); bütün sonlar ve defter; giriş metni ve replikler; temel ses; ilk dakika ipuçları; vuruşta sarsıntı, kısa duraklama, namlu parlaması ve basit küp patlaması. **Kullanıcı kararıyla eklendi (2026-10-24, uygulandı):** kombo ve skor çarpanı (K-18), dördüncü fısıltı modülü Hız ve Sözleşme 4'ün saldırı hızı (K-19). **Kullanıcı kararıyla eklendi (2026-10-25, uygulandı):** kritik vuruş, beşinci modül Keskin Göz ve Sözleşme 3'ün kritik şansı (K-20); dünyada beliren kombo/kritik yazıları; mermi cehennemi zorluğu (K-21); üç modül yuvası, doluyken de teklif (K-23); Yankı'nın sekme sonrası kısa menzili; hasarla sıfırlanmayan kombo, ×2 sonrası öldürme efekti, %12 kritik + kritik puanı + kritik sesi/kıvılcımı, altıncı modül Delici, basılı tutulan HAYIR (K-22). |
| **İsteğe bağlı** (bu sırayla) | Koşucu → dış çizgi ve titreşimsiz kamera → küçük dekorların küplere dağılması → tam voksel enkaz sistemi → imp ve kalkanlı iri → Defne'den tek tek voksel düşmesi (minimumda parça gizleme yeterli) → iç duvarların kırılması → ek kozmetik efektler → tuş atama. |
| **Kapsam dışı** | Yeni arena, boss, ek silah (Hız, Keskin Göz ve Delici dışında yeni modül de); koşular arası kalıcı güç, mağaza, envanter, yetenek ağacı; uzun eğitim, ara sahne, seslendirme; çok oyunculu. |

**Hiç kesilmez:** ölüm → Şeytan → EVET/HAYIR zinciri, her evetin görünür bedeli, HAYIR ve hep-EVET sonları, defter,
savaş içi fısıltı modülleri.

## 15. Kararlar ve açık sorular

### Kararlar (Burak, 2026-10-24: "önerilerini uygula")

| # | Karar |
|---|---|
| K-1 | Hikâye çerçevesi önerildiği gibidir: Şeytan **oyuncuya** konuşur; evetleri oyuncu verir, bedelini Defne öder. |
| K-2 | HAYIR her sözleşmede görünür ve seçilebilir; Şeytan'ın sunumu EVET'i öne çıkarır. (SefaSelim, 2026-10-25, K-22) HAYIR da EVET gibi **0,6 sn basılı tutularak** seçilir ([H] / gamepad B / fare); kart sağ altta, Şeytan sağ yanının önünde; EVET sağda büyük, parlak ve sabit, HAYIR solda küçük ve sönük; basılı tutunca düğme soldan sağa dolar (EVET altın, HAYIR soluk), erken bırakınca boşalır (§8). |
| K-3 | Sözleşme kartı bedelin şiirsel adını ve tam mekanik etkisini birlikte gösterir (gizli ceza yok). |
| K-4 | Ölüm garantidir; ilk GDD'nin 5. dakika kazanma koşulu kalkar. |
| K-5 | Sözleşme 4'ten sonra (3:30 penceresinden itibaren) dalga adedi düşer, tür karışımı ağırlaşır. |
| K-6 | Dalga zorluğu saate bağlıdır (ilk GDD mantığı). |
| K-7 | Fısıltı modülleri yalnız tüfeği etkiler (Hız dahil; SefaSelim 2026-10-24 onayladı). |
| K-8 | Sözleşme 2'nin "ses" bedeli yalnız Defne'nin seslerini susturur ve müziği boğuklaştırır (2026-10-25: tek parça müzikte katman yok, müzik boğuk ve uzak duyulur); oyun işaretleri kalır. |
| K-9 | Küçük hayırlar defterde puansız tek satırla sayılır. |
| K-10 | Şeytan hep "ondan" der ("Sana X, ondan Y"). |
| K-11 | Kapsam hedefi §14'teki **Minimum** katmandır; İsteğe bağlı ekler Minimum oynanır olunca başlar. |
| K-12 | Diriliş: can yeni azami değere dolar, 2,0 sn dokunulmazlık (Sözleşme 5'ten sonra 0), şok dalgası 6 m içindeki bütün düşmanları (iri dahil) parçalar. |
| K-13 | Pala dalgası kalkanı yok saymaz; Defne'den düşen vokseller isabet alanını küçültmez. |
| K-14 | (Burak, 2026-10-24 11:22: "hepsi için önerini yap") **Kamera Defne'yi takip eder**; karakterler ekranda ~1,7 kat büyük (ortografik boyut 15 → 9), nişana doğru hafif öne bakar, arena dışını göstermez. §9'daki "bütün alanı gösteren sabit kamera" kuralının yerini alır. |
| K-15 | **5:00'ten sonra düşmanlar gelmeye ve artmaya devam eder**, oyuncu ölene kadar (K-4 ölüm garantisi). HUD geri sayım yerine geçen süreyi gösterir. |
| K-16 | **Diriliş şok dalgası canlı bütün düşmanları siler** (6 m yerine). K-12'nin yarıçap kısmının yerini alır. |
| K-17 | **3:00'ten sonra oyun kolaylaşmaz**: düşman sayısı koşu süresiyle artmaya devam eder. K-5'teki "adet düşer" kuralının yerini alır. |
| K-18 | (SefaSelim, 2026-10-24) **Kombo ve skor çarpanı:** Defne'nin isabetleri ve öldürmeleri komboyu büyütür, kombo öldürme puanını en çok ×3,0'a kadar katlar; hasar almak ya da 3 sn oyalanmak sıfırlar (kurallar §11b). |
| K-19 | (SefaSelim, 2026-10-24) **Saldırı hızı** iki kaynaktan gelir: dördüncü fısıltı modülü **Hız** (yalnız tüfek ×1,4, bedeli ±6° sapma; K-7 değişmez) ve **Sözleşme 4** (+%25: tüfek ve pala beklemesi). Dört tür modül için **dört sabit yuva** vardır (her türe bir yuva). Kurallar §5, §7, §8. |
| K-20 | (SefaSelim, 2026-10-25) **Kritik vuruş:** bütün silahlarda %5 ihtimalle ×2 hasar (tüfekte mermi başına, atışta; delme ve Yankı korur; pala, dalga ve rokette düşman başına); modül patlaması, diriliş şok dalgası ve Defne'ye gelen hasar kritik olmaz. Beşinci fısıltı modülü **Keskin Göz** (yalnız tüfek +%20; bedeli ateş ederken yürüme 6 → 5,1 m/sn, kaçınma etkilenmez). **Sözleşme 3**'e "+%15 kritik şansı (bütün silahlar)" eklendi. Tüfekte en çok %40, diğerlerinde %20. Kombo ve kritik geri bildirimi sağ üstteki sayaç yerine dünyada beliren yazılardır; çarpan puanın yanında görünür. Yankı mermisi sektikten sonra kalan menzilinin en çok yarısı kadar gider. Kurallar §5, §7, §8, §11b. |
| K-21 | (SefaSelim, 2026-10-25: "amaç oyuncuyu öldürmek") **Zorluk iki kat, canlar aynı:** her pencerenin düşman sayısı ve aktif sınırı ×2, paketler 9 sn'ye yayılır, paket başına en çok 6 düşman, 5:00'ten sonra her pencerede +2 / +2 / +2 / sınır +2, aktif sınır en çok 32. Koşular tahminen ~2:30–3:15'e kısalır; kullanıcı bunu bilerek istedi, ~5 dk hedefi 🧪 geçicidir (§3, §10). **Beş yuva doluyken teklif gelmez, takılı tür bir daha teklif edilmez** (A-20 kapandı, §7). |
| K-22 | (SefaSelim, 2026-10-25) **Kombo hasarla sıfırlanmaz:** yalnız 3 sn isabetsizlik ya da ölüm sıfırlar; ×2'den (50 kombo) sonra her öldürme eşik efektini 0,75 güçle, en sık 0,12 sn'de bir yeniden oynatır (§11b; K-18'in "hasar almak sıfırlar" kısmının yerini alır). **Kritik:** taban şans %5 → %12 (tüfek en çok %47, diğerleri %27), her kritik isabet +25 × kombo çarpanı puan, yüksek metalik kritik sesi, altın-beyaz kıvılcım, daha büyük "KRİTİK!" yazısı (§5, §11). **Altıncı fısıltı modülü Delici** (yalnız tüfek, K-7): +2 delme, her delişte mermi %30 zayıflar (Delici takılıyken Sözleşme 3'ün delmesi de); Sözleşme 3 ile en çok 4; altı tür, altı yuva, dolunca teklif gelmez; Şeytan'ın delme yükseltmesi yalnız Sözleşme 3 kalır (§7, §8). **HAYIR basılı tutulur** (K-2 güncellendi, §8). |
| K-23 | (SefaSelim, 2026-10-25) **Üç modül yuvası:** altı modülün herhangi üçü aynı anda takılı olabilir (her türden 1). Yuvalar doluyken teklifler gelmeye devam eder ama "DOLU" gösterir ve alınamaz; oyuncu Q + sağ tıkla birini söküp yenisini alır. Q basılıyken çemberde modül simgeleri görünür; Üçlü Atış'ın simgesi yelpaze gibi üç mermi oldu. K-21'in "dolu yuvada teklif yok" kuralının yerine geçer. Kurallar §7, §12. |

### Açık sorular (❓ AÇIK — kullanıcı kararı bekler)

Oyunun oynanabilmesi için bazılarında **geçici bir seçim 🧪** uygulandı. Bunlar karar değildir; kullanıcı başka bir
seçenek seçerse değiştirilir:
- **A-4 (6. ölüm):** kart seçimsiz çıkar ("Son anlaşma: ondan geriye ne kaldıysa." / "Bu sefer sormuyorum."), Defne
  griye döner, kontroller 0,25 sn gecikir, oyun sürer; bir sonraki ölümde hep-EVET sonu gelir.
- **A-9 (roket ×2):** alan ×2 uygulandı (yarıçap 2,5 → 3,5 m); kart bu gerçek değeri yazar.
- **A-10 (Sözleşme 4):** yazıldığı gibi +%10, üç silaha birden. K-19'un +%25 saldırı hızı da bu sözleşmeye eklendi; bunun
  A-10'u yanıtlayıp yanıtlamadığına kullanıcı karar vermedi, soru açık kalır.
- **A-11b (ölümde modüller):** takılı modüller dirilişte kalır.
- **A-15 (R):** yalnız sonuç ekranında çalışır.
- **A-16 (skor):** şimdilik yalnız düşman başına +100 ve kritik isabet başına +25 sayılır (ikisi de kombo çarpanıyla; K-18, K-22); diğer kalemler 0.

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
- 2026-10-24 05:23 — Burak (Claude Code) — Faz 5: Şeytan'ın ölüm sözleşmeleri, görünür bedeller, üç son, defter, HUD ve fısıltı modülleri oynanır hâle geldi (§3, §7, §8, §11, §12 🟡); açık sorularda geçici seçimler 🧪 listelendi (§15).
- 2026-10-24 05:40 — Burak (Claude Code) — Faz 6: koddan üretilen sesler ve iki katmanlı müzik eklendi; Sözleşme 2 ses bedeli çalışıyor (§13).
- 2026-10-24 05:56 — Burak (Claude Code) — Faz 7: ilk dakika ipuçları eklendi (§12); Sözleşme 1 satırı geçici 3,5 m yarıçapla uyumlu yazıldı (§8); Windows build alındı.
- 2026-10-24 11:23 — Burak (Claude Code) — Oyun incelemesinden sonra Burak dört öneriyi onayladı: K-14 takip eden kamera, K-15 5:00 sonrası tırmanan dalgalar, K-16 bütün arenayı silen şok dalgası, K-17 3:00 sonrası artan düşman sayısı.
- 2026-10-24 12:45 — Burak (Claude Code) — İnceleme sonrası düzeltmeler: takip kamerası (K-14), animasyon ve silah, nişan, vuruş efektleri, paketli dalgalar ve tırmanan zorluk (K-15, K-17), şok dalgası (K-16), kart ve HUD açıklığı, Esc menüsü (§4, §5, §6, §8, §9, §10, §11, §13).
- 2026-10-24 15:15 — SefaSelim (Claude Code) — Defne, kullanıcının eklediği animasyonlu 3B modelle değişti; AK tüfek ve RPG elinde/sırtında; mermiler gerçekçi (pirinç mermi + iz, RPG roketi + duman); yeni modelde bedellerin görünüşü yazıldı (§2, §13).
- 2026-10-24 16:08 — SefaSelim (Claude Code) — Kamera yaklaştı ve görüntü inceldi, karakterler büyüdü, Defne'nin dış parlaması kalktı; silah tutuşu ayarlanabilir profil + sol el IK oldu; namlu, roket ve patlama efektleri gerçekçi dokulu efektlerle değişti (§13).
- 2026-10-24 17:18 — SefaSelim (Claude Code) — Arena karanlık gotik harabe olarak giydirildi, ışıkları yeniden kuruldu (yalnız görsel, §9); ana menü, sade ayarlar ve emeği geçenler eklendi, duraklatmadaki "Çık" ana menüye döner, arayüz Synty çerçeveleri ve gotik yazı tipleriyle yenilendi (§12).
- 2026-10-24 18:55 — SefaSelim (Claude Code) — Gotik arena düzeltmeleri: girişler kapalı demir kapı/parmaklık oldu (görünmez duvar kalmadı), güney ve batı duvarları 1 m'ye indi, oynanabilir alana taşan sütunlar ve içinden geçilen uzun mezar taşları kaldırıldı (§9).
- 2026-10-24 21:30 — SefaSelim (Claude Code) — Kombo ve skor çarpanı eklendi (§11b, K-18); dördüncü fısıltı modülü Hız (yalnız tüfek, K-7 korunur) ve dört sabit yuva, Sözleşme 4'e +%25 saldırı hızı (§5, §7, §8, K-19); dört modül takılıyken teklif sorusu açıldı (A-20); düşmanlar Synty POLYGON Dark Fantasy modelleri oldu (geçici animasyonlarla, §6, §13); menü, duraklatma, son ve Şeytan kartı düğmeleri Synty düğmeleri oldu (§12); kombo eşiklerinde post-processing efektleri (§13); bütün oyun metinleri samimi Türkçeyle yeniden yazıldı, bedel adları üçüncü kişi oldu (Rengi, Sesi, …; §2, §7, §8, §11, §12).
- 2026-10-25 03:30 — SefaSelim (Claude Code) — Kritik vuruş (%5 ×2, bütün silahlar), beşinci modül Keskin Göz ve Sözleşme 3'e +%15 kritik (K-20; §5, §7, §8, §11); Yankı sekme sonrası yarım menzil (§7); kullanıcı kararıyla zorluk ×2, mermi cehennemi, ~5 dk koşu hedefi 🧪 geçici (K-21; §3, §10); dolu yuvada ve takılı türde teklif yok, A-20 kapandı (§7, §15); kombo sayacı kalktı, dünyada kombo/kritik yazıları ve puan yanında çarpan (§11b, §12); küçük piksel dağılması, güçlü sarsıntı, şok dalgası halkaları, düşmanların parlaması kapandı (§6, §13); sade düğmeler, DOTween açılışları, sağ üstte teklif şeridi, beş yuva, tam ekran menü arka planı (§12); elle çizilmiş Şeytan (§8, §13); kayıtlı savaş/düşman sesleri (§13).
- 2026-10-25 05:00 — SefaSelim (Claude Code) — K-22: kombo hasarla sıfırlanmaz, yalnız 3 sn isabetsizlik ya da ölüm; ×2 sonrası her öldürmede 0,75 güçlü eşik efekti (§11b); kritik taban %5 → %12 (tüfek en çok %47), kritik isabet başına +25 × çarpan puan, metalik kritik sesi, altın-beyaz kıvılcım, büyük "KRİTİK!" yazısı (§5, §11); altıncı modül Delici (+2 delme, delişte %30 zayıflama), altı yuva (§7, §8, §12, §14); Şeytan kartı sağ altta, EVET sağda sabit, HAYIR solda ve artık basılı tutulur, düğme dolgu göstergesi (§8, §13, K-2).
- 2026-10-25 12:06 — SefaSelim (Claude Code) — Nişancı mermisi izli, parlayan bir küre oldu; sonuç ekranı taşmıyor ve Defne'nin çizilmiş portresini gösteriyor; ana menü ekibin çizimiyle 2B'ye döndü (§6, §11, §12).
- 2026-10-25 12:12 — SefaSelim (Claude Code) — HUD'daki küçük portre de ekibin çizdiği Defne portresi oldu, bedelleri üstünde gösterir (§12).
- 2026-10-25 13:20 — SefaSelim (Claude Code) — Düşmanlar ekibin eklediği yaratık animasyon paketini kullanıyor: kambur bekleme/yürüme/koşu, atılırken pençe savurma, Takipçi'nin ön işareti pençeyi kaldırmak; Koşucu çömelmeyi korur, Nişancı küreyi pençeyle fırlatır (§6, §13).
- 2026-10-25 14:58 — SefaSelim (Claude Code) — Oyuncunun gördüğü bütün yazılar tek Excel dosyasından (Yazilar.xlsx, Metin sütunu) okunuyor (§12).
- 2026-10-25 15:44 — SefaSelim (Claude Code) — K-23: üç modül yuvası, yuvalar doluyken de teklif gelir ("DOLU", birini sökünce alınır), Q çemberinde modül simgeleri, Üçlü Atış'a yeni simge (§7, §12, §15); oyun müziği "Tense Conversational Pulse", Sözleşme 2 müziği boğuklaştırır (K-8; §8, §13, §15).
- 2026-10-27 02:15 — SefaSelim (Claude Code) — Tüfek omuzda; mermi ve namlu ateşi tüfeğin ucundan çıkar, mermi ilk 2,5 m'de 1 m vuruş yüksekliğine iner (§13).
- 2026-10-27 02:26 — SefaSelim (Claude Code) — Mermi izi eğimli inmek yerine namlu hizasında dümdüz gider; vuruş yüksekliği 1 m kalır (§13).
- 2026-10-27 02:54 — SefaSelim (Claude Code) — Arena, uçurumdaki mezarlık haritasına taşındı: ~30 × 25 m düzlük, çit iç halka ve açıklıklar, açılmış merkez, sekiz kenar girişi, deniz ve uçurum görünür (§9, K-14).
- 2026-10-27 03:08 — SefaSelim (Claude Code) — Mezarlık haritası 1,5 kat büyüdü, düzlük eski düz taş zeminle döşendi, haritanın sisi neredeyse kalktı (§9).
- 2026-10-27 03:41 — SefaSelim (Claude Code) — Defne'nin önündeki yüksek dekor saydamlaşır (§9).
