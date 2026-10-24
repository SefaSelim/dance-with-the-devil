# Critical hits, Keskin Göz, combo popups, bullet-hell difficulty, Echo range — design and balance check

Date: 2026-10-25 · Author: game-designer (systems-designer + balance-check) · Status: design proposal, not implemented
User decisions taken as given (SefaSelim, 2026-10-25), not reopened here:
1. Critical hits exist; crit chance comes as a 5th whisper module **and** inside a Devil contract (contract 3).
2. Combo/crit feedback is text popping above enemies ("KOMBO!", "KRİTİK!") **instead of** the top-right combo counter.
3. All module slots full → no more offers (closes A-20).
4. 2× enemies, much more frequent spawns, enemy HP unchanged (goal: kill the player, bullet hell).
5. Echo bullets travel half as far after a bounce.

This file is a working note. `MAIN_GDD.md` stays the only GDD: section 8 holds the Turkish text to paste into it.

---

## 1. Balance check frame

### Data sources analysed
- `MAIN_GDD.md` §3, §5, §6, §7, §8, §10, §11b, §12, §13, §14, §15 (FOUND)
- `production/design/combo-haste.md` (combo counting, stacking tables, Hız) (FOUND)
- `Assets/_Game/Scripts/Core/WaveDirector.cs` (`_windowSpread` 18, `_packSizePerCap` 0.4, pack 2–4, `_lateGrowth` 1/1/1/cap 1, `_maxActiveCap` 16, `_pullAhead` 6, `_maxEmptyTime` 2.5; schedule = the 10 windows in the brief) (FOUND)
- `Assets/_Game/Scripts/Weapons/Projectile.cs` (bounces count toward the one max range; `_range` 25 m on the rifle) (FOUND)
- `Assets/_Game/Scenes/Arena.unity` (enemy pools `_prewarmCount` 10 / `_maxSize` 32 each; two projectile pools on one object, `_prewarm` 4 and 32) (FOUND)
- `assets/data/`, `design/gdd/`, `design/registry/entities.yaml`: ABSENT (project keeps numbers in code and `MAIN_GDD.md`; expected)

### Health summary: CRITICAL ISSUES → resolved in this note
One approved default was a **trap**: Keskin Göz with a −15% damage cost loses against 2 of the 3 enemies because
it breaks the hits-to-kill breakpoints (section 3). The note replaces the cost; with that change no dominant option
remains. Two remaining **concerns** (run length vs. the "~5 min" decision, popup spam with high crit) are listed with
knobs.

### Targets used
Enemy HP 15 / 30 / 40 (unchanged by decision). Rifle 10 dmg, 6 shots/s. Hits-to-kill is the real damage metric in
this game (small integer HP), so every damage change is judged by **expected hits-to-kill (E[HTK])**, not by EV of
damage per bullet.

---

## 2. Critical hits — final rules

| Topic | Rule |
|---|---|
| Base chance | **5%** on every weapon hit against an enemy |
| Crit damage | **×2** the final hit damage (after Yoğunluk, contract 4, etc.); crit is the last multiplier |
| Rifle | One roll **per bullet, at launch**. A crit bullet crits every enemy it pierces (contract 3) and still crits after an Echo bounce. Triple Shot: each of the 3 bullets rolls on its own |
| Machete | One roll **per enemy hit** per swing. Machete wave (contract 2): its own roll per enemy |
| Rocket | One roll **per enemy** inside the explosion |
| Not crits | Thrown/ejected module explosion, revive shockwave, any damage to Defne (her own bounced Echo bullet is never a crit; self-damage stays half of the non-crit bullet damage) |
| Chance sources (additive) | Base 5% · Keskin Göz +20% (rifle only) · Contract 3 +15% (all weapons) |
| Totals | Rifle 5 / 25 / 20 / **40%** (base / KG / C3 / both). Machete & rocket 5 / **20%** (base / C3) |
| Combo | A crit hit counts like any hit (+1 per enemy, +1 per kill). No bonus combo, no bonus score |
| Shielded front (if the brute ever ships) | 0 damage → no crit, no popup |

Why per-bullet roll on the rifle: one roll per projectile is what the user approved ("rifle per bullet"), and it lets
a crit bullet look different in flight (optional visual), which is cheap readability.

### Hits-to-kill with crits (rifle, single target)

| Loadout | Dmg / crit | Crit % | Koşucu 15 | Takipçi 30 | Nişancı 40 |
|---|---|---|---|---|---|
| Base | 10 / 20 | 5 | 1.95 | 2.90 | 3.86 |
| Keskin Göz (final, sec. 3) | 10 / 20 | 25 | 1.75 | 2.56 | 3.36 |
| KG + C3 | 10 / 20 | 40 | 1.60 | 2.36 | 3.02 |
| Yoğunluk + KG + C3 | 16 / 32 | 40 | 1.00 | 1.60 (crit one-shots) | 2.36 |

Machete 35 / 70: a crit one-shots the Nişancı (2 swings → 1). Wave 20 / 40: a crit one-shots Takipçi and Nişancı.
**Rocket 40 / 80: the crit changes nothing today** — base 40 already kills every enemy in the roster (max HP 40). See
concerns.

---

## 3. Keskin Göz (5th whisper module) — the trap and the fix

### The approved cost fails
Approved: rifle-only +20% crit, cost −15% rifle base damage. Damage-EV check (0.85 × 1.25 = 1.0625 vs 1.05) says
"+1%", but damage in this game is spent in whole hits:

| Enemy | Base E[HTK] (10 dmg, 5%) | KG at −15% (8.5 dmg, 25%) | Verdict |
|---|---|---|---|
| Koşucu 15 | 1.95 | 1.75 | better |
| Takipçi 30 | 2.90 | **3.36** | worse: 8.5 × 3 = 25.5 < 30, non-crit needs 4 hits |
| Nişancı 40 | 3.86 | **4.16** | worse: 8.5 × 4 = 34 < 40, non-crit needs 5 hits |

Takipçi is the most common enemy in every window. **Any** rifle damage cut below 10 breaks the 3-hit Takipçi
breakpoint, and it also breaks it on top of Yoğunluk (16 → 13.6: 2 hits → 3) and contract 4 (11 → 9.35: 3 → 4). So no
percentage of damage cost fixes it — the cost type has to change. It is a trap, not a choice (SDT autonomy: a
false choice).

### Final: cost moved from damage to movement
- **Advantage:** rifle crit chance **+20%** (5% → 25%; 40% with contract 3).
- **Cost:** **while the fire button is held, Defne walks ×0.85 (6 → 5.1 m/s).** Dodge distance/speed unaffected.
  Releasing fire restores full speed at once.
- Why this cost: it is visible and in the body (pillar 2: she slows down to aim), it never touches a breakpoint, and
  in a doubled-density bullet hell mobility is the resource the player feels most. The trade is ~−12% hits-to-kill
  for −15% walking while shooting — a real choice: strong when you can stand and shoot, risky when packs close in.
- Not dominant: Hız gives more DPS up close (×1.4 rate), Yoğunluk gives breakpoints (Takipçi 2 hits) at a body-size
  cost; KG is the "steady aim" option. Stacks with Üçlü Atış recoil (recoil push is independent, still ≤ 2.5 m/s).
- Removal: crit chance and speed return to base the same frame (single point of removal, §7).

Alternative if the user insists on a damage-type cost: no damage cut works (above). The nearest non-trap is
"fire rate ×0.9 (6 → 5.4/s)": TTK ≈ base −2–3%, i.e. a pure sidegrade that does not feel like power (fails pillar 1).
Not recommended.

### Capacity with 5 types
5 types, **5 fixed slots** (one per type). While all 5 are equipped **no offer spawns**; when a slot frees (eject),
offers resume on the normal 14–20 s rhythm (first new offer no earlier than 14 s after the slot freed — the spawner
just waits for a free slot). Duplicate offers of an equipped type still appear and show "TAKILI" while slots are free.
Closes A-20.

---

## 4. Contract 3 — adds crit
Promise: pierce 2 more enemies (unchanged) **+ crit chance +15% on all weapons** (rifle, machete, wave, rocket).
Cost unchanged (Arkadaşları; enemies +15% speed). Contract 3 arrives after the 2nd death, when the doubled waves start
to bite; +15% crit adds ~−8% E[HTK] on the rifle and gives the machete a 20% Nişancı one-shot. Not a power spike
beyond what contract 4 already delivers.

Ledger (§11) row 3 "Aldıkların": `Delen mermi, +%15 kritik`.

---

## 5. Difficulty — final numbers

All user-approved multipliers kept. HP unchanged.

| Knob | Old | New |
|---|---|---|
| Every window count (C/R/S) | as below | ×2 |
| Every window ActiveCap | as below | ×2 |
| `maxActiveCap` | 16 | **32** |
| `windowSpread` | 18 s | **9 s** |
| `maxPackSize` | 4 | **6** (min 2, `packSizePerCap` 0.4 unchanged) |
| `lateGrowth` (C/R/S/cap per window past 5:00) | 1/1/1/1 | **2/2/2/2** |
| Enemy pool prewarm (each of 3 pools) | 10 | **24** (`maxSize` 32 stays) |

| Window | Takipçi | Koşucu | Nişancı | Total | Active cap | Pack size | Packs (every) |
|---|---|---|---|---|---|---|---|
| 0:00 | 36 | – | – | 36 | 12 | 5 | 8 (1.1 s) |
| 0:30 | 42 | 24 | – | 66 | 16 | 6 | 11 (0.8 s) |
| 1:00 | 34 | 16 | 8 | 58 | 18 | 6 | 10 (0.9 s) |
| 1:30 | 34 | 16 | 8 | 58 | 20 | 6 | 10 |
| 2:00 | 30 | 16 | 10 | 56 | 20 | 6 | 10 |
| 2:30 | 30 | 20 | 10 | 60 | 22 | 6 | 10 |
| 3:00 | 34 | 20 | 10 | 64 | 22 | 6 | 11 |
| 3:30 | 34 | 24 | 10 | 68 | 24 | 6 | 12 |
| 4:00 | 36 | 24 | 16 | 76 | 24 | 6 | 13 |
| 4:30 | 36 | 28 | 16 | 80 | 26 | 6 | 14 |
| 5:00+ | +2 | +2 | +2 per 30 s | | +2 per 30 s, max 32 (reached at 6:00) | 6 | |

### What this actually does (systems read)
- Required kill rate to keep up: 0:00 window 1.2 kills/s (36 DPS of Takipçi HP); 0:30 window 2.2 kills/s (~54 DPS).
  Base rifle DPS is 60 at 100% accuracy. From ~0:30 on **the queue outruns any player**, so the active cap is
  permanently full and becomes the real difficulty knob; window counts above that only feed a backlog (spawns never
  stop, K-15). This is the intended "you will die" pressure.
- Pack rhythm: a pack every ~1 s at 6 per pack means a new entrance warning almost every second — readable only
  because warnings stay 0.8 s and spawn distance stays ≥ 9 m (unchanged).
- Shooters: up to 16 per window at 4:00+ with cap 24 → many simultaneous orange lines. The rule "shooters use at most
  2 entrances per 3 s" stays and is now important.
- Death schedule estimate (from the ×1.35/×1.5/×1.75 bot runs: doubling simultaneous enemies roughly doubles incoming
  damage): first death ~0:20–0:35, later deaths every ~20–35 s → hep-EVET run ends ~2:30–3:15 instead of ~5:00.
  This conflicts with the §3 decision "Bir koşu yaklaşık 5 dakikadır" — **needs the user** (see concerns). Tuning
  lever if they want to keep ~5 min: lower only the early caps (0:00 cap 12 → 8, 0:30 cap 16 → 12) and leave counts ×2.
- Echo + bullet hell: more enemies means more bullets in flight and more Echo self-hits — the halved post-bounce range
  (section 6) is a needed mitigation, not just flavour.

---

## 6. Echo — range after a bounce
- At the bounce, the bullet's **remaining range is halved**. Example: rifle range 25 m, bounce at 10 m → 15 m left →
  goes on **7.5 m**. Bounce at 4 m → 21 → 10.5 m.
- Everything else unchanged: one bounce, the 2nd wall ends it, the bounced bullet still hurts Defne at half damage,
  ejected Echo makes in-flight bullets harmless.
- Effect: shooting at a far wall makes the return leg short (safer); point-blank wall shots still come back to Defne
  (risk kept — the cost stays readable). Advantage barely changes, because bounced hits mostly land near the wall.

---

## 7. Combo and crit popups — final rules

The top-right combo block (count, multiplier, 3 s bar, KIRILDI/SÖNDÜ) is **removed**. The score stays in the HUD.
All combo and crit feedback is world-space text above enemies (or above Defne for resets).

### Popup types (priority high → low)

| Prio | Popup | When | Where | Size / life |
|---|---|---|---|---|
| 1 | **Tier-up**: "×1,2 KOMBO!" / "×1,5" / "×2,0" / "×3,0 KOMBO!" | combo crosses 10 / 25 / 50 / 100 | above the enemy whose hit/kill crossed it | large, 1.0 s; §13 post-process pulse stays |
| 2 | **Reset**: "KIRILDI" (damage) / "SÖNDÜ" (timer) | combo ≥ 5 is lost | above Defne | medium, 0.8 s; not shown for combos < 5 |
| 3 | **Milestone**: "15 KOMBO" | combo crosses any multiple of 5 that is not a tier | above the enemy whose hit/kill crossed it | medium, 0.7 s |
| 4 | **Crit**: "KRİTİK!" | a crit hit damages an enemy | above that enemy | small, 0.5 s |

### Limits (readability in a bullet hell)
- **Combo slot: 1 on screen.** Tier-up, milestone and reset share one slot; a new one replaces the old one at once.
  A lower-priority popup never replaces a higher one still in its first 0.4 s (e.g. a milestone right after a tier-up
  waits or is dropped).
- **One jump, one popup:** if a single attack crosses several steps (rocket +10, 23 → 33), show only the highest
  (tier if any, else the last multiple of 5: "30 KOMBO").
- **Crit slot: max 3 on screen, max 1 new per 0.2 s.** Extra crits in that time are not shown (damage and hit flash
  still apply). One attack that crits several enemies in the same frame (rocket, pierce, wave) shows **one** popup on
  the first crit target: "KRİTİK ×3!".
- **One popup per enemy at a time**; a newer popup on the same enemy replaces the older.
- **Total on screen: max 4** (1 combo + 3 crit).
- Popups rise ~0.6 m and fade; they stay at the world point where they spawned (do not follow a running enemy; a
  killed enemy's popup stays where it died). They never block input, aiming or telegraph visibility: drawn semi-
  transparent over the world, under the HUD, and never on top of Defne (reset popups sit above her head only).
- Frozen with pause, hidden while the Devil card is open, cleared on revive.
- Multiplier visibility: the player now reads the multiplier only from popups; the current multiplier is part of
  every milestone ("15 KOMBO ×1,3"). Timer bar is gone (see concerns).

Estimated rate at the new density: ~1 combo popup/s in a fight, crits 0.3/s at base, up to ~12 crits/s fired with
Hız + Üçlü + KG + C3 → the 5/s throttle and the per-attack merge are what keep this readable.

---

## 8. Edge cases

| Case | Rule |
|---|---|
| Crit bullet pierces 3 enemies | All 3 take ×2; one popup "KRİTİK ×3!"; combo +3 (+kills) |
| Crit bullet bounces (Echo) then hits Defne | Defne takes half of the **non-crit** damage; combo reset as before |
| Crit overkill (rocket 80 on 15 HP) | Applies, no extra effect; popup still shows (throttled) |
| Crit kill crosses a combo tier in the same frame | Hit +1, kill +1, multiplier, score (as §11b), then popups: tier (combo slot) + crit (crit slot) |
| All 5 modules equipped, one ejected | Offers resume once a slot is free; next offer ≥ 14 s later |
| Ejecting Keskin Göz while firing | Crit chance 25 → 5% for bullets launched after; bullets in flight keep their roll; speed back to 6 m/s at once |
| Keskin Göz + Hız + Üçlü | Spread/recoil apply as now; crit roll per bullet; walking ×0.85 while firing |
| Echo bounce with < 1 m range left | Bounces, travels the halved remainder (≤ 0.5 m), ends |
| Active cap 32, pool `maxSize` 32 per type | Never more than 32 alive in total, so per-type pools suffice; prewarm 24 avoids mid-fight instantiation for the common case |
| Revive shockwave with a full cap of 32 | Kills all (K-16); no combo, no crit, no popups (shockwave is not Defne's hit) |

---

## 9. Tuning knobs

| Knob | Default | Range | Category |
|---|---|---|---|
| Base crit chance | 5% | 3–10% | curve |
| Crit multiplier | ×2 | ×1.5–×2.5 | curve |
| Keskin Göz crit bonus | +20% | +15–30% | curve |
| Keskin Göz move factor while firing | ×0.85 | ×0.75–×0.9 | feel |
| Contract 3 crit bonus | +15% | +10–20% | curve |
| Echo post-bounce range factor | ×0.5 of remaining | ×0.3–×0.7 | feel |
| Crit popup throttle / max | 0.2 s / 3 | 0.1–0.4 s / 2–5 | feel |
| Combo popup min hold before replace | 0.4 s | 0.2–0.6 | feel |
| Reset popup threshold | combo ≥ 5 | 0–10 | feel |
| Early active caps (0:00 / 0:30) | 12 / 16 | 8–12 / 12–16 | gate (run-length lever) |
| windowSpread / maxPack / maxActiveCap / lateGrowth | 9 s / 6 / 32 / 2-2-2-2 | — | gate |

---

## 10. Turkish GDD-ready text (paste into MAIN_GDD.md)

### 10.1 §5 — new note after the "Saldırı hızı" paragraph

> **Kritik vuruş (kullanıcı kararı 2026-10-25) — ⬜:** Defne'nin düşmana verdiği her silah isabeti **%5** ihtimalle
> kritik olur ve **iki kat** hasar verir (bütün çarpanlardan sonra uygulanır). Tüfekte ihtimal mermi çıkarken bir kez
> atılır: kritik mermi deldiği her düşmana ve Yankı'yla sektikten sonra da kritik vurur; Üçlü Atış'ta her mermi ayrı
> atılır. Palada, pala dalgasında ve rokette her düşman için ayrı atılır. Fırlatılan modül patlaması, diriliş şok
> dalgası ve Defne'ye gelen hasar (kendi seken Yankı mermisi dahil) kritik olmaz. Kritik ihtimali iki kaynaktan artar
> ve toplanır: **Keskin Göz** modülü (§7, yalnız tüfek +%20) ve **Sözleşme 3** (§8, bütün silahlar +%15). Tüfekte en
> çok %40, pala ve rokette en çok %20. Kritik isabet komboda normal isabet gibi sayılır; ek kombo ya da puan vermez.
> Kritik vuran düşmanın üstünde "KRİTİK!" yazısı çıkar (§11b).

### 10.2 §7 — capacity row (replaces "Kapasite")

> | Kapasite | Her türden en fazla 1; beş tür, beş sabit yuva (her türe bir yuva). Zaten takılı bir türün teklifi yine gelir ve "TAKILI" gösterir. **Beş modül de takılıyken yeni teklif gelmez**; bir modül sökülüp yuva boşalınca teklifler normal aralıkla (en erken 14 sn sonra) yeniden başlar (kullanıcı kararı 2026-10-25, A-20 kapandı) |

Sıklık satırının sonu: "... beş tür torbadan karışık gelir".

### 10.3 §7 — Yankı row (replaces it)

> | **Yankı** | Mermiler duvardan bir kez seker (görünür duvarlardan) | Seken mermi sana da çarpar (yarım hasar), 2. duvarda söner; **sektikten sonra kalan menzilinin yarısı kadar gider** (ör. 10 m'de seken mermi 15 m yerine 7,5 m daha gider) | Duvardan seker. Arada bir sana döner. Bir şey olmaz. |

### 10.4 §7 — new Keskin Göz row

> | **Keskin Göz** (kullanıcı kararı 2026-10-25; ⬜) | Tüfek mermileri %20 daha sık kritik vurur (%5 → %25; Sözleşme 3 ile %40) | Ateş ederken yavaşlarsın: sol tık basılıyken yürüme hızı 6 → 5,1 m/sn (×0,85); kaçınma etkilenmez | *(yer tutucu, yazar ekibine)* Gözünü kıs, tam ortadan vur. Biraz yavaşlarsın, kimse fark etmez. |

Sökme paragrafına eklenecek cümle:

> Keskin Göz sökülünce kritik ihtimali ve yürüme hızı hemen tabana döner; havadaki mermiler kendi atışlarını korur.

### 10.5 §8 — contract 3 row (replaces it)

> | 3 | Tüfek mermileri 2 düşmanı daha delip geçer (kalkanı delmez*); **bütün silahlarda kritik ihtimali +%15** (tüfek %5 → %20, Keskin Göz'le %40; pala ve roket %5 → %20) | **Arkadaşları:** ekrandaki küçük fotoğraf kaybolur | Düşmanlar %15 daha hızlı | Mermilerin delip geçsin, daha sık da tam yerine otursun. Ondan da arkadaşlarını alıyorum. Zaten onlara hiç vakti yoktu. |

(Şeytan repliği yer tutucudur; son söz yazar ekibinin.) §11 defter satırı 3, Aldıkların: "Delen mermi, +%15 kritik".

### 10.6 §10 — difficulty (new paragraph after "Güncel uygulama", and the window table)

> **Mermi cehennemi (kullanıcı kararı 2026-10-25) — ⬜:** amaç oyuncuyu öldürmek. Düşman canları değişmez; her
> pencerenin düşman sayısı ve aktif sınırı **iki katına** çıkar. Paketler pencerenin ilk **9 sn**'sine yayılır (18 yerine),
> bir paket en çok **6** düşmandır (4 yerine). 5:00'ten sonra her 30 sn'de **+2 Takipçi, +2 Koşucu, +2 Nişancı, sınır +2**,
> aktif sınır en çok **32** (16 yerine). Doğuş uyarısı (0,8 sn), 9 m doğuş mesafesi ve "Nişancılar 3 sn içinde en çok 2
> girişten" kuralı aynen kalır; okunabilirlik bunlara dayanır.
>
> | Pencere | Takipçi | Koşucu | Nişancı | Aktif sınır |
> |---|---|---|---|---|
> | 0:00–0:30 | 36 | – | – | 12 |
> | 0:30–1:00 | 42 | 24 | – | 16 |
> | 1:00–1:30 | 34 | 16 | 8 | 18 |
> | 1:30–2:00 | 34 | 16 | 8 | 20 |
> | 2:00–2:30 | 30 | 16 | 10 | 20 |
> | 2:30–3:00 | 30 | 20 | 10 | 22 |
> | 3:00–3:30 | 34 | 20 | 10 | 22 |
> | 3:30–4:00 | 34 | 24 | 10 | 24 |
> | 4:00–4:30 | 36 | 24 | 16 | 24 |
> | 4:30–5:00 | 36 | 28 | 16 | 26 |
> | 5:00 sonrası | +2 | +2 | +2 | +2 (en çok 32, 6:00'da) |
>
> Kaba hesap: 0:30'dan sonra hiçbir oyuncu doğuş hızına yetişemez; arena hep aktif sınır kadar doludur, asıl zorluğu
> aktif sınır belirler. İlk ölüm tahminen 0:20–0:35, sonrakiler 20–35 sn arayla; hep-EVET koşusu ~2:30–3:15 sürer. Bu,
> §3'teki "~5 dakika" kararıyla çelişir (❓ kullanıcı kararı bekler). Koşuyu uzatmak gerekirse yalnız ilk iki pencerenin
> aktif sınırı düşürülür (12 → 8, 16 → 12).

### 10.7 §11b — "Geri bildirim" row (replaces it)

> | Geri bildirim | Sağ üstteki kombo sayacı kalkar; puan HUD'da kalır. Kombo ve kritik **düşmanların üstünde beliren yazılarla**
> gösterilir. **Eşik** (10, 25, 50, 100): büyük "×1,2 KOMBO!" … "×3,0 KOMBO!", 1 sn, görüntü efekti nabzı (§13) kalır.
> **Her 5 kombo:** "15 KOMBO ×1,3", 0,7 sn. **Kritik:** küçük "KRİTİK!", 0,5 sn. **Kırılma:** kombo en az 5 iken
> hasarla kaybedilirse Defne'nin üstünde "KIRILDI", süre dolarsa "SÖNDÜ", 0,8 sn. Yazı, eşiği geçiren ya da kritik
> alan düşmanın üstünde çıkar, ~0,6 m yükselip söner, düşmanı takip etmez. **Ekran sınırları:** kombo yazısından
> aynı anda 1 tane (yenisi eskisinin yerini alır; eşik yazısı ilk 0,4 sn'sinde daha düşük bir yazıyla değişmez; tek
> saldırı birden çok basamak geçerse yalnız en yükseği çıkar); kritik yazısından en çok 3 tane ve 0,2 sn'de en çok 1
> yeni (fazlası gösterilmez, hasar yine uygulanır); tek saldırının aynı karedeki kritikleri tek yazıda birleşir
> ("KRİTİK ×3!"). Yazılar yarı saydamdır, Defne'nin üstünü kapatmaz, duraklatmada donar, Şeytan kartında gizlenir,
> dirilişte silinir. |

### 10.8 Knock-on lines elsewhere in MAIN_GDD.md
- §7 status line: "beş modül".
- §12 HUD: "puanın altında kombo" removed (popups, §11b); bottom module slots **5**.
- §14 Kapsam: "Kullanıcı kararıyla eklendi (2026-10-25)": kritik vuruş, Keskin Göz, kombo yazıları, mermi cehennemi
  zorluğu, Yankı menzili. "Kapsam dışı" line: "Hız ve Keskin Göz dışında yeni modül".
- §15: new K-20 (kritik vuruş + Keskin Göz + Sözleşme 3 kritik), K-21 (zorluk ×2, mermi cehennemi), K-22 (kombo
  yazıları), K-23 (Yankı menzili); A-20 removed from the open list and its 🧪 line; new open question on run length
  (see concerns).
- Değişiklik geçmişi + Son güncelleme per AGENTS.md.

---

## 11. Acceptance criteria

Functional
- Over 1,000 base rifle bullets, crits ≈ 5% (±1.5%); with KG ≈ 25%; KG + C3 ≈ 40%.
- A crit pierce bullet deals ×2 to every enemy it passes; Triple Shot bullets roll independently.
- Machete crit one-shots a Nişancı (70 ≥ 40); revive shockwave and module explosions never show "KRİTİK!".
- KG equipped: walking 5.1 m/s with fire held, 6.0 released; dodge unchanged; eject restores both the same frame.
- 5 modules equipped: no offer spawns for any length of time; eject one → an offer appears 14–20 s later.
- Echo bounce at 10 m from the muzzle: bullet ends ≤ 7.5 m after the wall.
- Never more than 1 combo popup and 3 crit popups on screen; rocket killing 5 with 2 crits shows one "KRİTİK ×2!".
- Wave director at 6:00 holds 32 alive at most; no enemy instantiation spike during play (prewarm 24).

Experiential (playtest)
- Players can say what "KRİTİK!" means after one minute and notice KG as "stand and shoot harder".
- In a full-cap fight, telegraphs (Takipçi wedge, Nişancı orange line, entrance warnings) stay readable through
  the popups.
- Players still feel the combo multiplier exists without the HUD counter (ask: "what was your multiplier?").
- Deaths feel unfair-free: every death traceable to a visible telegraph, not to spawn on top / off-screen bullets.
