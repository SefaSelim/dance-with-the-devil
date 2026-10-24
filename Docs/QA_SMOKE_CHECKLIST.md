# Elle Smoke Test Listesi — Arena (jam teslimi öncesi)

**Tür:** Entegrasyon + UI + ses (elle oyun testi) · **Kapı:** BLOCKING (teslimden önce 1–7 geçmeli; 8 tasarım girdisidir)
**Sahne:** `Assets/_Game/Scenes/Arena.unity` → Play · **Kaynak:** `MAIN_GDD.md` §3–§15, `WORKLOG.md` Faz 2–6
**Neden elle:** Klavye/fare oyunu, modüller, Şeytan akışı ve ses yalnızca script/MCP ile sürüldü; hiç kimse elle oynamadı ve dinlemedi.

**Hata bulunca:** `WORKLOG.md` sonuna şablonla bir giriş ekle (`Status: uncommitted`, ne yapıldı → ne beklendi → ne oldu, kaç denemede kaç kez).
Giriş başlığına bulan kişiyi yaz; aşağıdaki **Sorumlu** sütununa düzeltecek kişiyi yaz. Ağır hatayı (çökme, akış kilitlenmesi, ilerlenemiyor) hemen ekibe söyle.
🧪 işaretli davranışlar geçici seçimdir (GDD §15): farklıysa hata değil, not olarak yaz.

Test eden: ______ · Tarih/saat: ______ · Commit: ______

## 1. Kontroller ve silahlar
| ☐ | Yap | Beklenen | Sorumlu |
|---|---|---|---|
| ☐ | WASD ile dört yöne yürü | Kamera düzlemine göre hareket (W ekranda yukarı), ~6 m/sn, duvardan geçmez | |
| ☐ | Fareyi zeminde gezdir | Defne imleç noktasına döner; ekranın her köşesinde doğru | |
| ☐ | Sol tık / basılı tut | Tüfek ~6 atış/sn, namlu parlaması, sesi var | |
| ☐ | E (ve orta tık) iki kez, sonra tekrar | 2 roket çıkar, sonra ~6 sn'de bir şarj dolar; patlama düşmanları savurur, Defne'ye hasar yok | |
| ☐ | F yakın düşmana | Önde 120° yay vurur; ~0,7 sn'de birden sık vurmaz; vuruş sırasında tüfek susar | |
| ☐ | Space (hareketli / duruyorken) | ~3 m hareket yönüne, girdi yoksa imleç yönüne; sırada hasar yok; 1,6 sn bekleme; "hazır" sesi | |
| ☐ | Sağ tık (teklif yokken) | Boşa darbe; 0,65 sn bekleme göstergesi | |
| ☐ | Q basılı tut, Q + sağ tık | Takılı modüller Defne çevresinde görünür; vurgulanan sökülüp imleç yönüne fırlar | |

## 2. Fısıltı modülleri
| ☐ | Yap | Beklenen | Sorumlu |
|---|---|---|---|
| ☐ | ~8. sn'deki teklife dur, çekirdeğe değ | Halka dolar, 0,45 sn sonra kabul; ayrılınca halka sıfırlanır | |
| ☐ | Teklifin üstünden yürüyerek geç / Space ile geç | Kabul **olmaz** | |
| ☐ | Teklife yakınken (≤3 m, 60° koni) sağ tık | Modül fırlar, düşmanda/duvarda patlar (1,8 m, 25 hasar); Defne'ye vurmaz | |
| ☐ | Her türü takıp Q+sağ tıkla sök | Üçlü → tek mermi, itme durur · Yoğunluk → gövde ve hasar tabana · Yankı → havadaki mermiler artık zararsız | |
| ☐ | Üçlü Atış: duvara sırtın dönük ateş et | Geri iter ama duvara yapışmaz/titremez/içine girmez | |
| ☐ | Yankı: duvara yakın, kendine doğru sekecek açıyla ateş et | Sekmiş mermi Defne'ye yarım hasar verir; ikinci duvarda söner | |
| ☐ | Yoğunluk takılıyken | Defne gözle görülür büyük; hasar artmış; dar geçitten (4 m) hâlâ geçilir | |
| ☐ | Aynı türü ikinci kez gör | "TAKILI" yazar; teklif 14 sn sonra söner | |

## 3. Düşmanlar ve dalgalar
| ☐ | Yap | Beklenen | Sorumlu |
|---|---|---|---|
| ☐ | Her türün saldırısını izle | Takipçi 0,45 sn kol + kama · Koşucu 0,35 sn çömelme + çizgi · Nişancı 0,8 sn turuncu hat; ilk görüşte anlaşılır | |
| ☐ | Girişleri izle | Her doğuştan önce 0,8 sn uyarı plakası; uyarısız doğuş yok | |
| ☐ | Uyarı yanan girişe koş | Doğuş başka girişe kayar; 9 m içinde hiç doğuş yok | |
| ☐ | 1:00 sonrası Nişancıları izle | Aynı anda her yönden gelmezler (3 sn'de en çok 2 giriş); duvar arkasından ateş etmezler | |

## 4. Ölüm takvimi (normal oyuncu, sözleşmelere hep EVET)
Bot ilk ölümü 1:07'de, sonra çok sık öldü (12 ölüm). İnsan süresini yaz (HUD saati):

| Ölüm | 1 | 2 | 3 | 4 | 5 | 6 |
|---|---|---|---|---|---|---|
| Hedef | 0:50 | 1:40 | 2:30 | 3:15 | 4:00 | ~4:40 |
| Gerçek (oyuncu: ____) | | | | | | |

☐ İki farklı oyuncu ölçtü · ☐ Ölümler adil hissettirdi (nedenini gördüm: evet/hayır yaz)

## 5. Şeytan akışı
| ☐ | Yap | Beklenen | Sorumlu |
|---|---|---|---|
| ☐ | Öl | Zaman donar, ekran solar, Şeytan sağdan eğilir; 1. ölümde "Psst. Sen. Evet, ekrandaki sen." | |
| ☐ | Ölürken E'ye bas | Kart açılır açılmaz kazara EVET seçilmez (E hem roket hem EVET) | |
| ☐ | Kartta EVET: Enter / E; HAYIR: H | EVET büyük, HAYIR küçük ama görünür; tuşlar çalışır | |
| ☐ | 1–6 her kart: bedeli ve sonucu kontrol et | 1 roket 40→80, yarıçap 2,5→3,5 m 🧪, dolum 4,3 sn; dünya %30 solar · 2 pala dalgası, 2 kaçınma; ses + melodi susar; can 80 · 3 delici mermi; fotoğraf kaybolur; düşmanlar %15 hızlı · 4 +%10 hasar; yüz vokselleri düşer; can 60 · 5 3 roket; kenar kararır; dirilişte dokunulmazlık yok, kaçınma 3,2 sn · 6 seçimsiz; gri; kontrol 0,25 sn gecikir 🧪 | |
| ☐ | EVET sonrası | Can yeni azamiye dolar, 6 m şok dalgası, 2 sn dokunulmazlık (5'ten sonra yok) | |
| ☐ | 1. ölümde HAYIR | "Kaybettin ama kendin kaldın." Defter: Aldıkların/Verdiklerin "Hiçbir şey" | |
| ☐ | 2–5. ölümde HAYIR | "Eksildin ama ayaktasın.", evet sayısı doğru, verilenler alt alta; "Küçük hayırların: N" doğru | |
| ☐ | Hep EVET (6. sözleşmeden sonra öl) | "Bir şey olmadı, değil mi?" → "Hayır demek de bir seçenekti."; defter 6 satır | |
| ☐ | Sonuç ekranında R, sonra "Tekrar dene" | Temiz yeni koşu: renk, ses, can 100, modüller, saat sıfırlanmış; oyun içinde R çalışmaz 🧪 | |

## 6. Ses dinleme (kulaklıkla + hoparlörle)
| ☐ | Yap | Beklenen | Sorumlu |
|---|---|---|---|
| ☐ | 2 dk oyna | Müzik/SFX dengesi iyi; uyarı sesleri duyulur; hiçbir ses patlamıyor | |
| ☐ | 30 sn tüfeği basılı tut | Yorucu/kulak tırmalayıcı değil | |
| ☐ | Müziği ~21 sn döngü noktasında dinle | Döngüde tık, boşluk veya atlama yok | |
| ☐ | Sözleşme 2'ye EVET | Defne sesleri ve melodi 1,5 sn'de susar; vuruş, uyarı, kaçınma hazır, kabul/ret sesleri **kalır** | |

## 7. Okunabilirlik
| ☐ | Yap | Beklenen | Sorumlu |
|---|---|---|---|
| ☐ | Sözleşme 1 sonrası (%30 soluk) | Düşman türleri, mermiler, turuncu hat, modüller hâlâ ayırt edilir | |
| ☐ | Sözleşme 5 sonrası (kenar kararması) | Kenardaki giriş uyarıları kararmanın üstünde görünür | |
| ☐ | Sözleşme 6 (gri Defne) | Defne düşmanlardan ve zeminden ayırt edilir | |
| ☐ | Teklif satırı | Şeytan repliğinin arkasında soluk kalıyor mu (bilinen sorun) — not et | |

## 8. Baskın strateji soruları (ilk GDD §10/§15) — tasarım girdisi, hata değil
☐ **Hep ret:** her modülü sağ tıkla fırlat. Hepsini kabul etmekten daha mı güçlü? Ölüm saatleri: ______
☐ **Hep kabul:** her modülü al, hiç sökme. Bedeller (itme, sekme, büyüme) hissedildi mi? Ölüm saatleri: ______
☐ Sonuç: biri açıkça baskınsa → `WORKLOG.md` notu + Burak'a. Bu bir tasarım kararıdır: denge değişikliğine kullanıcı karar verir, tahminle kapatılmaz.
