# Bir Şey Olmaz: teslime kadar düzeltme planı (~52 sa, son: 26 Ekim 15:00)

K-14, K-15, K-16 ve K-17 (Burak, 24 Ekim 11:22) karar sayılır ve aşağıda uygulanır. GDD'nin §9, §10 ve §13 bölümleri hâlâ eski kuralı yazıyor; bunlar ilgili partide güncellenir.

## 1. Neden kötü hissettiriyor
1. **Ana eylem bozuk.** İmleç sağdaki düşmanın tam üstündeyken mermi önünden geçip gidiyor. Nişangah yok, Defne'nin elinde silah yok (F1, F20, F36, F22).
2. **Her şey minik ve cansız.** Sabit kamera bütün arenayı gösteriyor ve karakterler ekran yüksekliğinin %4-5'i kadar kalıyor. Animasyon, karakter gölgesi ve zemin dokusu yok (F5, F22, F29, F12, F30).
3. **Vuruş hissi piksel altında kalıyor.** Namlu, isabet, sarsıntı ve ölüm parçaları 1-3 texel. Vurulmak da kaçınmak da görünmüyor. Patlama opak sarı bir küp (F4, F9, F13, F21).
4. **Tempo kırık ve kimse elle oynamadı.** İlk dakikanın yarısında arena boş. Yürüyen oyuncuya neredeyse hiç vurulamıyor. 5:00'ten sonra oyun kilitleniyor ve Esc yok. Bütün ölçümler yerinde duran bottan geliyor (F2, F3, F10, F19).
5. **Tema oyuncuya ulaşmıyor.** Kart, HAYIR'ın koşuyu bitirdiğini söylemiyor. Bedeller oyun ölçeğinde görünmüyor. E tuşu kazara EVET imzalatıyor. Sonlar düz metin tablosu (F6, F7, F8, F17).

## 2. Düzeltme partileri
Her partide geçerli kurallar:
- Değerler `Assets/_Game/Editor/PlaceholderBuilder.cs` içinde değiştirilir, çünkü Arena.unity her yeniden kurulumda sıfırlanır.
- Sahne ve ProjectSettings değişiklikleri WORKLOG'a yazılır.
- Görsel kontrol 1920×1080 tam ekran build'de yapılır. Editörün 1,28× görüntüsü yanıltır (F18).
- Her partiden sonra 10 dk klavye-fare testi yapılır.

### Parti 1: Görünür ve isabetli ateş (F1, F5/K-14, F20, F36, F22(1), F29) · ~5 sa
Neden ilk sırada: oyunun her saniyesini değiştirir ve çoğu tek satırlık bir değer ya da hazır altyapı (kamera düzeneği takibi zaten destekliyor).
- K-14: `BuildCameraRig` içinde `_target` = Player, `_orthographicSize` 15 → 9. `PixelCameraRig` odağı nişana doğru en çok 2 m kayar ve arena sınırında durur.
- F1: `PlayerController._aimHeight` 1,0 → 0,7. `WeaponHub` tüfeği ve roketi `Flatten(AimPoint − MuzzlePosition)` yönünde atar; imleç namluya 1 m'den yakınsa `AimDirection` kullanılır.
- F1/F20: Muzzle yerel x 0,37 → 0. `Projectile`, başlangıçta duvarla çakışınca normali `ClosestPoint` ile bulur.
- F36: HUD'da canlı fare konumunda 7×7 texel bir artı. Oynarken `Cursor.visible=false` ve `Confined`; diğer bütün durumlarda ve odak kaybında eski hâline döner.
- F22(1): Defne'ye 0,1×0,1×0,5 m koyu bir "Gun" küpü eklenir, ucu namluda biter. Arm_R, Nişancı'daki gibi öne uzanır.
- F29: `PC_RPAsset` gölge mesafesi 50 → 75, kademe 4 → 1. Takip kamerasında Defne hep ~50 m uzakta kalır ve bu ayar olmadan gölgesi neredeyse tamamen söner.
- GDD: §4, §9 ve §13 metni güncellenir.
- Doğrulama:
  - Build görüntüsünde Defne ekran yüksekliğinin ~%8'i ve gölgesi var.
  - 8 yönde, özellikle sağdaki Takipçi ve Koşucu'ya 10'ar tık, 10'u da isabet.
  - Köşedeyken arena dışı görünmüyor.

### Parti 2: Koşu bitsin, boşluk olmasın (F3/K-15, F19/K-16, K-17, F10, F23, F32, F2) · ~5 sa
- K-15: `WaveDirector` 300 sn'den sonra son pencereyi her 30 sn'de bir, büyüterek yeniden kuyruğa alır (TP +2, sınır +1, en çok ~12). `GameHud.UpdateTime` geçen süreyi gösterir.
- K-17: 3:30 sonrasındaki pencerelerde düşman sayısı düşmez, artar. §10 tablosu ve "5:00'te durur" cümlesi düzeltilir.
- K-16: `DevilDirector`, `KillWithin(pos, 6)` yerine bütün canlı düşmanları parçalar.
- F10 (yalnız sayılar): W0 8 → 12 Takipçi; W1 14 → 17 (W2'nin 18 TP'sinin altında kalır). Sınırlar 3 ve 5 olarak kalır, böylece akış kesilmez.
- F23: Takipçi ve Koşucu atılması temas mesafesinde durur (ajan yarıçapı + Defne'nin güncel CC yarıçapı).
- F32: QualitySettings PC seviyesinde `vSyncCount` 0 → 1, yedek olarak `targetFrameRate`.
- Esc güvenlik ağı: 1. karar gelince eklenir (~0,5 sa).
- F2, önce ölçüm: 2 kişi 10'ar dk oynar ve ölüm saatlerini yazar. İlk ölüm 1:15'ten geç gelirse şu sırayla ayarlanır: atılma 1,5 → 2,5 m, Takipçi hızı 3,5 → 4,5, Koşucu hasarı 6 → 10. §6 ve §10 güncellenir.
- Doğrulama:
  - 6:30'a kadar bot koşusu: 5:00 sonrası düşman artıyor, ölüm geliyor, kilitlenme yok.
  - 0:15 görüntüsünde arenada en az 3 düşman var.
  - İnsan testinde ilk ölüm 0:45 ile 1:15 arasında.

### Parti 3: Vuruş hissi (F4, F21, F13, F9) · ~7,5 sa · `unity-vfx-game-feel` skill
- F4, sayı değişiklikleri:
  - Tüfek yoğunlukları (namlu/isabet/duvar) 0,15/0,25/0,1 → 0,6/0,8/0,4.
  - Namlu parlaması FlashSize 0,6, 4 küp, küp boyu 0,12-0,2 m. Mermi isabeti FlashSize 0,5, 6 küp.
  - Pala `_hitIntensity` 0,5 → 1,0; bu GDD'deki 0,06 sn duraklamayı verir. Yakın dövüş Trauma 0,3 → 0,6.
  - Sarsıntı eğrisi trauma² → trauma^1,5.
  - Işığa süre parametresi (namlu 0,06 sn, patlama 0,2 sn); LightFlash'in menzili ezmesi düzeltilir.
  - Tüfeğin sarsıntısı yine ~0 kalır (§5).
- F21: `_shatterCell` 0,12 → ~0,17 (gözle ayarlanır), hız (2,6) → (3,8), `_bulletForce` 0,8 → 1,0. Ölümde 1 m'lik beyaz, 0,06 sn'lik bir parlama.
- F13:
  - `mat_vfx_flash` saydam ve additive olur.
  - Patlama çekirdeği 0,8-1,5 m'ye iner.
  - Gerçek `ExplosionRadius` büyüklüğünde bir yer halkası çizilir. Yarıçap olayla birlikte gelir, modül patlaması (1,8 m) dahil; A-9'a dokunmaz.
  - Dirilişte kırmızı bir halka çıkar.
- F9: Defne hasar alınca 0,1 sn parlar ve dokunulmazlık boyunca 15 Hz yanıp söner (ölçekli zamanla, ölümde durur). Trauma 0,5. Can çubuğu 2-3 kare kırmızı yanar.
- Doğrulama:
  - Build'de 0,1 zaman ölçeğinde kare kare görüntü: namlu ve isabet en az 5 texel.
  - Sözleşme 1 öncesi ve sonrası patlama halkası farklı büyüklükte.
  - Testçiye sorulur: "Vurduğunu ve vurulduğunu gördün mü?"

### Parti 4: Sözleşme anı okunur ve adil (F8, F6(1), F7, F17, F16) · ~8 sa (+3 sa F16)
- F8:
  - EVET'ten E tuşu kalkar; EVET Enter, tık ya da pad South ile seçilir.
  - Kart kilidi 0,45 → 1,0 sn; düğmeler görünür biçimde belirir. EVET'e tıklama da bu kilide bağlanır.
  - Repliklerin ilk 1 sn'sinde Space, E ve sol tık sayılmaz.
- F6(1): her düğmenin altında sonucunu söyleyen tek satır (metin 7. karara bağlı).
- F7:
  - Sözleşme 1'de renk önce 1,0'a döner, oyun sürerken 1,5 sn'de 0,7'ye iner.
  - HUD portresinde saç grileşir.
  - EVET'ten sonra zaman 1,5 sn durur ve bedel 120 px'lik UI blok portrede uygulanır.
  - Sözleşme 4'te giden parçalar `Shatter` ile kopar.
- F17: aynı portre sonuç ekranlarında, koşunun bıraktığı hâliyle görünür. HUD gizlenir.
- F16 (zaman kalırsa): yalnız transform hareketi (±2° salınım, nefes, seçimde geri tepme, kayarak çıkış). Konuşma baloncuğu başın yanında; atlanabilir daktilo efekti.
- Doğrulama:
  - E basılıyken ölme testi kazara EVET vermiyor.
  - 3 kişiye "HAYIR ne yapar?" diye sorulur; üçü de doğru cevaplar.

### Parti 5: Canlılık ve okunurluk (F22(2), F33, F24, F12(1), F30) · ~7 sa
- F22(2), ortak yordamsal animasyon:
  - Hareket yönüne 8-12° eğilme ve ~1 texel'lik yürüme sallanması.
  - Oyuncu ateş edince Arm_R ve silahta 0,06-0,08 sn geri tepme.
  - Değerler `TexelWorldSize`'a göre yuvarlanır. ChaserBrain'in tell pozuna yazmaz.
- F33: düşman vurulunca ayrı bir alt nesnede ezilme (x/z 1,15, y 0,85, 0,12 sn'lik yay). Havuzdan geri gelince sıfırlanır.
- F24: pala vuruşu anında 120°×2 m additive bir yelpaze, 0,12-0,15 sn, ölçeksiz zamanla.
- F12(1), renk geçişi:
  - Kenar şeridi #F2E6B8 → ~#B5A784; zemin biraz açık ve soğuk.
  - Nişancı açık leylak olur (Defne'nin erik rengi saçından uzak).
  - Koşucu yalnız açılır; Defne'nin bot ve bacakları açılır.
- F30: zemine ayrı, nokta filtreli bir malzeme (1 m karo, 2-3 yakın ton). Dekor yalnız `level-designer` planıyla, çarpışmasız olarak `PlaceholderBuilder` tarafından yerleştirilir.
- Doğrulama: aynı 3 anın önce/sonra build görüntüsü; dekorda aşağı doğru ışın testi.

### Parti 6: Arayüz ve öğretim (F14, F11, F25, F31, F38, F28, F15) · ~6 sa
- F14(7): Ret göstergesi `PlayerModules.RejectReady`'yi okur (0,1 sa; istenirse hemen yapılır).
- F11:
  - HUD'a "Pala [F]" eklenir.
  - E/F/Space ipucu, yürü/ateş ipucundan sonra her zaman gösterilir.
  - Giriş satırı koyu bir bant üstünde ~4 sn kalır. §12 güncellenir.
- F14(1-4): teklif çekirdeği 2 kat büyür, halka kenarı parlar, kopya teklifte "TAKILI" yazar, kalan süre çubuğu eklenir, retle 60°/3 m'lik bir kama görünür.
- F25 + F31:
  - Q basılıyken seçili HUD yuvası parlar ve "Sağ tık: sök" yazar.
  - Dünyadaki yuva küpleri 0,35 → 0,6 m, Defne'ye uzaklık 1,2 → 1,5 m.
  - Dolu yuva modülün kendi renginde görünür.
- F38: HUD içeriği oyun görüntüsünün dikdörtgenine ve ölçeğine bağlanır.
- F28: italik kalkar. HAYIR yazısı en az 4,5:1 kontrastlı olur ama EVET'ten küçük kalır (K-2).
- F15 (b, c): ret edilince 0,6 sn "hayır" sözcüğü çıkar; fısıltı afişinde küçük bir Şeytan başı.
- Doğrulama:
  - 1366×768, 1600×900 ve 1920×1080 görüntüleri.
  - Yeni bir oyuncu ilk 2 dakikada palayı ve modül sökmeyi buluyor mu?

### Parti 7: Ses ve teslim cilası (F35, F39, F40, F12(2), F18) · ~5 sa
- F35: kulaklıkla 30 dk dinleme turu. Ardından rifle_shot −14 → −18 dB ve 3 varyant, patlama −4 → −7 dB, enemy_die 5 → 8 bit; Bake All.
- F39: companyName (ekip adı sorulur), `resizableWindow` 1, pencere 1280×720, 256 px simge.
- F40: ekran kamerasında renk ve derinlik kopyası kapatılır.
- F12(2): yalnız oyun kamerasında 1 px dış çizgi.
- F18: QA listesine "görsel yalnız tam ekran build'de değerlendirilir" satırı.
- Doğrulama: build'de kare süresi önce/sonra; `Docs/QA_SMOKE_CHECKLIST.md` baştan sona.

**Kesme çizgisi:** Parti 1-4 zorunlu (~25 sa). 5 ve 6 sırayla, 7 kalan süreye göre. Ekip paralelde Şeytan çizimini, insan testlerini, dinleme turunu ve simgeyi yürütür.

## 3. Burak'ın karar vermesi gerekenler
Bu kararlar gelmeden ilgili kısım uygulanmaz.

| # | Soru (bulgu) | Seçenekler | Öneri |
|---|---|---|---|
| 1 | Esc ne yapsın? Yeni bir girdi, §4'te yok (F3) | Oyunu durdurup "Devam / Çık" açar · doğrudan çıkar · hiçbir şey | Devam / Çık |
| 2 | A-15: R oyun içinde de çalışsın mı? (F3) | Yalnız sonuç ekranında · Esc penceresinde "Baştan" · her an R | Esc penceresinde |
| 3 | Takip kamerasında ekran dışındaki doğuş nasıl gösterilsin? (F5) | Ekran kenarında ok · yalnız ses · hiçbiri | Kenar oku |
| 4 | Nişancı yürüyen oyuncunun gideceği yere nişan alsın mı? (F2) | Hayır, yalnız sayı ayarı · kilitlenince hız tahmini (en çok 3 m) · insan testinden sonra karar | Testten sonra |
| 5 | Dalga içindeki alt grup aralığı (F10) | 0/4/8 · 0/8/16 · 4 sn'de bir 6 grup | 0/8/16 |
| 6 | Dalga saati giriş satırını beklesin mi? (F11) | Hemen başlar · satır sönene kadar (~4 sn) bekler · "bir tuşa bas" | ~4 sn bekler |
| 7 | Kartta sonucu söyleyen satır (F6) | Yok · "EVET: dirilirsin, bedeli kalıcı · HAYIR: koşu biter" · ilk ölümde Şeytan'ın "Seni geri getiririm. Bir şartla." repliği | İkincisi (+ üçüncüsü) |
| 8 | HAYIR sağ tıkla da seçilsin mi? (F8) | Evet · hayır, yalnız H | Evet, Ret ile aynı tuş |
| 9 | HUD fotoğrafında kim var, kart etiketi ne olsun? (F37) | Defne yalnız · Defne ve 2-3 arkadaşı; etiket "Ondan: Rengin" · "Bedel: Rengin" | Grup fotoğrafı + "Bedel:" |
| 10 | "Ret" yerine "hayır" sözcüğü ve sade teklif metni (F15, F14 5-6) | Olduğu gibi kalır · "Hayır [Sağ tık]", sonuç ekranında "M büyük evet · N küçük hayır", sade +/− satırları | İkincisi; §7 ve §12 metni değişir |
| 11 | Kaçınma düşmanın içinden geçsin mi? (F23) | Hayır · yalnız kaçınma süresince evet | Evet |
| 12 | Şeytan çizimini kim, ne zaman yapar? (F16, A-19) | Kutu Şeytan kalır · 170×300 tek poz, 25 Ekim akşamına · 2-3 poz | Tek poz |
| 13 | Yazı tipi (F28) | Yerleşik font kalır · CC0 piksel font (Türkçe harfler kontrol edilir, CREDITS.md'ye yazılır) | CC0, Türkçe harfler eksiksizse |
| 14 | İmp yapılsın mı? A-18 hâlâ açık (F27) | Hayır, K-15 ve K-17 yeter · evet, A-18'e de karar gerekir | Hayır |
| 15 | Renk bedelindeki %30 kayıp yetiyor mu? (F7, §8) | Kalsın, görünür biçimde inmesi yeter · %50 | Parti 4'ten sonra bakılsın |

## 4. Bilerek yapmıyoruz
- **İmp ve kalkanlı iri (F27):** İsteğe bağlı listede 5. sırada ve A-18 açık. Oyunun sonundaki baskıyı K-15 ve K-17 sağlıyor.
- **Nişanı düşmana yapıştırmak (F1, adım 1):** nişan yardımı sayılır. 0,7 m'lik nişan düzlemi ve namludan atış yeterli.
- **Mermiyle sendeletme, yavaşlatma, oyuncuyu geri itme (F33, F9):** kural değişikliği olur. Vuruş tepkisi yalnız görsel kalır.
- **Kartta bedeli başlık, mekaniği küçük yazı yapmak (F6, kısım 2):** K-2 ve K-3 ile çatışır.
- **Kutu Şeytan'a göz kırpma ve sırıtma (F16):** çizim gelince boşa gider.
- **Yakın plan kamera ve yeni "Defne" katmanı (F7, F17):** ortak TagManager dosyasında birleştirme riski var. UI blok portresi yeter.
- **Monitör çerçevesi (F16 c) ve ekrana göre RT boyutu (F18, seçenek 3):** biri GDD'nin bir yorumu, öteki §13'teki sabit 640×360'ı değiştirir.
- **Enkazın 6-10 sn kalması, kırılan dekor ve duvarlar (F30 d, A-12):** 8000 küp sınırı dolunca sonraki ölümler parçalanmaz. Zaten İsteğe bağlı katmanda.
- **Roket izi, kaçınma hayaletleri ve tozu (F13 f, F9):** "ek kozmetik efektler" listede en sonda.
- **Dirilişte mermileri ve uyarıları iptal etmek, dirilişten sonra doğuşa ara vermek (F19):** yeni kural olur. K-16 zaten bütün düşmanları siliyor.
- **AudioMixer ve kompresör (F35):** ses seviyelerini kısmak aynı işi risksiz görür.
- **Tüfekle öldürmede duraklama (F21):** §5'e göre tüfekte duraklama yok.
