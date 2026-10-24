# Combo + Attack Speed (Hız module, Contract 4) — design and balance check

Date: 2026-10-24 · Author: game-designer (systems-designer + balance-check) · Status: design proposal, not implemented
User decisions taken as given (SefaSelim, 2026-10-24): (1) a combo that gives a score multiplier; (2) an attack-speed
upgrade that comes both as a 4th whisper module and as a Devil contract reward. These are not reopened here.

This file is a working note. `MAIN_GDD.md` stays the only GDD: section 6 holds the Turkish text to paste into it.

---

## 1. Balance check frame

### Data sources analysed
- `MAIN_GDD.md` §3, §4, §5, §7, §8, §10, §11, §12, §14, §15 (FOUND)
- `Assets/_Game/Scripts/Weapons/Rifle.cs` (`_fireRate` 6, interval `1/_fireRate`, Triple Shot = 3 projectiles per salvo, `PierceCount`) (FOUND)
- `Assets/_Game/Scripts/Weapons/Machete.cs` (`_cooldown` 0.5 s starting at swing end) (FOUND)
- `Assets/_Game/Scripts/Devil/DealEffects.cs` (contract 4 = `AllDamageFactor` 1.1 on rifle, rocket, machete) (FOUND)
- `Assets/_Game/Scripts/Devil/ScoreKeeper.cs` (+100 per `WaveDirector.EnemyKilled`, any cause) (FOUND)
- `Assets/_Game/Scripts/Modules/PlayerModules.cs`, `ModuleType.cs` (fixed slot per module type; slot count = type count) (FOUND)
- `assets/data/`, `design/gdd/`, `design/registry/entities.yaml`: ABSENT (project keeps numbers in code and `MAIN_GDD.md`; expected)

### Health summary: CONCERNS
No degenerate loop found once the counting rule below is used (combo gain is bounded by enemy HP supply, not by fire
rate). Concerns are tuning and doc-consistency items, plus two small rule choices that need the user (section 7).

### Targets used
No stated target existed for combo or attack speed. Targets derived here from the GDD pacing: ~5 min run, first death
~0:50, packs of 2–4, arena never empty > ~3 s, enemies 15/30/40 HP. Combo target: typical play sits at x1.2–x1.6; x3
needs ~35–40 s of clean (undamaged) play. Attack speed target: noticeable (+25% / +40%) without changing any enemy's
hits-to-kill on its own.

---

## 2. Combo — final rules

### Counting rule (the key decision)
**+1 per damaging hit per enemy, +1 extra per kill.** Not per bullet fired, not per salvo.

Why this is safe: every counted hit removes HP from a finite enemy. Total combo an enemy can ever give =
hits-to-kill + 1. Fire rate, Triple Shot, pierce and Echo only make that happen *sooner*, they cannot make it *bigger*.
Multi-target attacks (rocket into 5, pierce through 3, Triple Shot fanning into 3) give more per attack, but only because
more enemies lose HP — that is the juicy moment we want, and it is still bounded by spawn supply.

| Source | Counts? | Example |
|---|---|---|
| Rifle bullet (incl. Triple Shot side bullets, pierce, Echo bounced bullet) | yes, per enemy damaged | pierce bullet through 3 enemies = +3 |
| Rocket explosion | yes, per enemy damaged | rocket damages 5, kills 5 = +5 hits +5 kills = **+10** |
| Machete blade and machete wave (Contract 2) | yes, per enemy per damage event | blade + wave both hit the same enemy = +2 |
| Thrown / ejected module explosion | yes, per enemy damaged | |
| Devil revive shockwave kill | **no** (no hit, no kill credit) | |
| Hit that deals 0 damage (shielded front of the big enemy, if it ever ships) | no | |
| Any kill not caused by Defne (shockwave; imp blast if A-18 says yes) | no | |

Expected combo per kill (base rifle 10 dmg): Koşucu 2+1 = 3, Takipçi 3+1 = 4, Nişancı 4+1 = 5. Higher damage
(Density, Contract 4) *lowers* combo per kill (Takipçi at 16 dmg = 2 hits + 1 = 3). This is a natural counterweight:
damage upgrades trade combo speed-per-enemy for safety.

### Timer
- Window **3.0 s** after the last counted hit or kill. No new count in 3.0 s → combo = 0.
  (Starting proposal was 2.5 s; raised because packs spawn 9–14 m away behind a 0.8 s warning and the arena can be
  empty up to ~3 s, so 2.5 s would drop combos between packs for reasons the player cannot control.)
- **The timer does not run while no enemy is alive** (including during spawn warnings). A clean wipe of a pack never
  costs the combo; dawdling with enemies alive does.
- The timer runs on game time: frozen during the Devil screen, pause menu, hitstop and results screen. Dodge does not
  freeze it (0.18 s, irrelevant).

### Reset on damage
- Combo = 0 the instant Defne **loses health** from any source, including her own bounced Echo bullet.
- Blocked hits do not reset: dodge i-frames, the 0.6 s post-hit invulnerability, revive invulnerability.
- Death resets (it is damage); after revive the combo starts at 0. Shockwave kills then score flat 100 (x1.0).

### Multiplier and score
- `multiplier = min(3.0, 1.0 + 0.1 × floor(combo / 5))`
- Tier marks: combo 10 → x1.2 · 25 → x1.5 · 50 → x2.0 · 100 → x3.0 (cap). Counter keeps climbing past 100 (bragging
  number), multiplier stays x3.0.
- `kill score = 100 × multiplier` (always an integer: 100–300). Order in the killing frame: count the hit (+1), count
  the kill (+1), recompute multiplier, then award score. So the kill that crosses a tier already pays the new tier.
- Multiplier applies only to kill score (decided). Future +25 module-throw score (A-16 area) is not multiplied unless
  the user says so.

### Feedback (rule-level only; look/sound belong to ux/art/audio)
- HUD near the score: combo count, current multiplier, a draining 3.0 s bar (bar visibly paused when the arena is
  empty).
- Every count: small number tick (0.5 s readability rule). Tier-up at 10/25/50/100: short text + sound accent,
  no hitstop, no gameplay pause.
- Reset: the counter breaks/falls so the player knows *why* (damage vs timeout should look different — the player must
  learn which mistake cost it).

### Projected values
Kill supply ≈ 40–45 enemies/min (window budget ×1.5). At ~4 combo per kill:

| Play | Combo reached | Multiplier | Kill score |
|---|---|---|---|
| Hit every ~5 s (typical mid-run) | 10–20 | x1.2–x1.4 | 120–140 |
| Clean ~15 s | ~40 | x1.8 | 180 |
| Clean ~35–40 s (skilled) | 100 | x3.0 cap | 300 |

Run-score ceiling: ~250 kills × 300 = 75,000 vs today's ~25,000. Bounded; cannot explode.

### Degenerate strategies checked
| Strategy | Result |
|---|---|
| Spam Haste + Triple to farm hits | Bounded by enemy HP; only faster, not bigger. OK |
| Keep one weak enemy alive to keep the timer | Every counted hit damages it; it dies in 2–4 hits. No regen exists. OK |
| Hide when hurt to "save" combo | Damage already reset it. OK |
| Let the arena empty to stall the timer | Timer pause on empty arena is intended (rewards wiping a pack). Waves never stop (K-15), so no infinite stall. OK |
| Die on purpose for shockwave kills | Shockwave gives no combo, flat 100, and costs a contract. OK |
| Echo + Haste spray | More bounced bullets → more self-hits → more combo resets. Intended cost synergy. OK |

---

## 3. Attack speed — final numbers

### Definition
- **Rifle:** fire rate multiplied (shots per second).
- **Machete:** only the post-swing cooldown is divided; the 0.2 s swing itself never changes (it is the readable
  commit window and silences the rifle). So the machete never "attacks faster" mid-swing.
- **Rocket:** not affected (its refill is Contract 1's territory).
- Sources stack multiplicatively with each other, and with damage multipliers.

### Hız module (4th whisper module)
- Advantage: rifle fire rate **x1.4** (6 → 8.4 shots/s).
- Cost: every bullet leaves at a random angle within **±6°** of the aim (uniform; with Triple Shot each of the 3
  bullets rolls its own deviation on top of its ±12°).
- Removal (eject or reject flow): fire rate and spread return to base immediately (single point of removal, §7 rule).
- Machete: **not affected** under the recommended option (keeps K-7). See section 7, decision D1, for the alternative.

Why ±6° is a real cost (rough, hit radius ≈ 0.55 m incl. bullet; verify in play):

| Distance | Hit chance per bullet | Hız DPS vs base 60 |
|---|---|---|
| 5 m | ~100% | 84 (better) |
| 8 m | ~66% | ~55 (≈ equal/worse) |
| 12 m | ~44% | ~37 (worse) |

Hız is a close-range module; against the Nişancı (holds 7–11 m) it is a downgrade. Real choice, not a flat buff.
Triple Shot recoil is per salvo, so Hız also pushes Triple Shot users back 1.4x as often (speed contribution is already
capped ≤ 2.5 m/s, does not stack into walls) — a second, intended cost synergy.

### Contract 4
- Promise: all damage **+10%** (unchanged) **and attack speed +25%**: rifle 6 → **7.5** shots/s; machete cooldown
  0.5 → **0.4 s** (cycle 0.7 → 0.6 s). Rocket unchanged.
- Cost unchanged: face voxels fall; max HP 80 → 60.
- Card must show the exact numbers (K-3).

### Stacking table (rifle, single bullet, point-blank)

| Loadout | Dmg/bullet | Shots/s | DPS | Takipçi (30) hits | Nişancı (40) hits |
|---|---|---|---|---|---|
| Base | 10 | 6.0 | 60 | 3 | 4 |
| Contract 4 | 11 | 7.5 | 82.5 | 3 | 4 |
| Hız | 10 | 8.4 | 84 | 3 | 4 |
| Hız + C4 | 11 | **10.5** | 115.5 | 3 | 4 |
| Hız + Density + C4 | 17.6 | 10.5 | 184.8 (x3 with Triple = 554) | 2 | 3 |

| Machete | Dmg | Cycle | DPS |
|---|---|---|---|
| Base | 35 | 0.7 s | 50 |
| Contract 4 | 38.5 | 0.6 s | 64 |
| (only if D1 = B) Hız + C4 | 38.5 | 0.2 + 0.5×0.7/1.25 = 0.48 s | 80 |

**10.5 shots/s: acceptable.** Contract 4 arrives ~3:15, after 3 deaths, when K-17 waves keep growing and max HP drops to
60; it is a late power spike the curve is built to absorb. Hits-to-kill only drop when Density is also equipped, and
Hız's spread eats much of it beyond 8 m. Risks are technical, not balance: up to 31.5 bullets/s with Triple Shot
(~20 in flight, more with Echo) — projectile pool, muzzle VFX and SFX voice count must handle ~40 live bullets.

---

## 4. Edge cases

| Case | Rule |
|---|---|
| Kill and tier-cross in the same frame | Hit + kill counted first, then score at the new multiplier |
| Several enemies die in one frame (rocket) | Each kill counts and scores in sequence at the rising multiplier |
| Defne takes damage and kills in the same frame | Damage resets first (safety first, simpler to read); the kill then counts +1 from 0 |
| Combo timer expires on the same frame as a hit | The hit wins (count before expiry check) |
| Arena empty, spawn warning showing | Timer paused |
| Echo bullet hits Defne during dodge/i-frames | No damage → no reset |
| Echo bullet already in the air when Echo is ejected | Harmless to Defne (§7) → cannot reset combo |
| Damage to Defne absorbed at 0 (no HP lost) | No reset |
| Contract 6 (A-4 provisional play continues) | Combo works normally; 0.25 s input delay just makes it harder |
| Pause / Devil card open for a long time | Timer frozen; combo kept (it was already 0 after a death) |
| Hız with Echo | Spread applies only at muzzle; bounce angle is the normal reflection |
| Hız fire rate x1.4 with Contract 6 input delay | Unaffected; delay is on input, cadence on the weapon |
| All 4 modules equipped (D2 = A) | No more offers spawn (spawner already waits for a free slot) |
| Machete hits the same enemy with blade and wave | +2 (two damage events), bounded by HP |

---

## 5. Tuning knobs

| Knob | Default | Range | Category | Rationale |
|---|---|---|---|---|
| Combo window | 3.0 s | 2.5–4.0 | feel | Bridges pack gaps; < 2.5 punishes spawn timing |
| Pause timer on empty arena | on | on/off | feel | Avoids losses the player cannot control |
| Combo per hit / per kill | 1 / 1 | 0–2 | curve | Kill bonus makes finishing matter |
| Combo per multiplier step | 5 | 4–10 | curve | Frequent visible steps |
| Multiplier step | +0.1 | 0.05–0.2 | curve | |
| Multiplier cap | x3.0 | x2–x4 | gate | Keeps score bounded |
| Tier marks | 10/25/50/100 | — | feel | Match x1.2/x1.5/x2.0/x3.0 |
| Hız fire-rate factor | 1.4 | 1.25–1.6 | curve | Clear but not hits-to-kill breaking |
| Hız spread | ±6° | ±4–±9 | curve | ~8 m break-even vs base |
| Contract 4 attack speed | +25% (x1.25 rifle, cooldown ÷1.25 machete) | +15–+40% | curve | Makes the contract worth its 20 HP |

Knobs live in the component inspectors / `DealEffects` constants, matching the existing pattern (no `assets/data/`
in this project).

---

## 6. Turkish GDD-ready wording (paste into MAIN_GDD.md)

### 6.1 New section — suggested under §11 after the **Skor** line (or as §11b)

> ### Kombo ve skor çarpanı — ⬜ (kullanıcı kararı 2026-10-24)
>
> Defne'nin düşmana verdiği her isabet komboyu büyütür; kombo büyüdükçe öldürme skoru katlanır. Hasar almak ya da
> oyalanmak komboyu sıfırlar.
>
> | Konu | Kural |
> |---|---|
> | Artış | Hasar veren her isabet, hasar alan **her düşman için +1**; düşmanı öldürmek **ayrıca +1**. Üçlü Atış, delen mermi, roket ya da pala aynı anda birden çok düşmana vurursa her biri sayılır (roket 5 düşmanı öldürürse +10). |
> | Sayılanlar | Tüfek (sekmiş Yankı mermisi dahil), roket, pala ve pala dalgası, fırlatılan ya da sökülen modülün patlaması. Hasar vermeyen isabet ve diriliş şok dalgasının öldürdükleri sayılmaz. |
> | Süre | Son sayılan isabetten sonra **3,0 sn** içinde yeni isabet gelmezse kombo sıfırlanır. Arenada canlı düşman yokken (doğuş uyarısı sırasında da) süre işlemez. |
> | Hasar | Defne can kaybettiği anda kombo sıfırlanır (kendi seken Yankı mermisi dahil). Kaçınma ve dokunulmazlık sırasında engellenen vuruşlar sıfırlamaz. Ölüm de sıfırlar; diriliş 0'dan başlar. |
> | Donma | Şeytan ekranı, duraklatma ve vuruş duraklaması sırasında süre işlemez. |
> | Çarpan | Her tam 5 kombo için +0,1; en çok **×3,0**. 10 kombo ×1,2 · 25 kombo ×1,5 · 50 kombo ×2,0 · 100 kombo ×3,0. Sayaç 100'ü geçebilir, çarpan ×3,0'da kalır. |
> | Skor | Defne'nin öldürdüğü düşman **100 × çarpan** puan verir (×1,5'te 150). Öldüren isabet ve öldürme önce sayılır, puan yeni çarpanla verilir. Şok dalgasının öldürdükleri 100 puan, çarpansız. Çarpan yalnız düşman puanına uygulanır. |
> | Geri bildirim | HUD'da skorun yanında kombo sayısı, çarpan ve azalan 3 sn çubuğu (arena boşken durduğu görünür). 10, 25, 50 ve 100'de kısa yazı ve ses vurgusu; oyun durmaz. Kombo sıfırlanınca sayaç kırılarak söner; hasarla ve süreyle sıfırlanma farklı görünür. |
>
> Hasar artışları düşman başına isabet sayısını azalttığı için komboyu yavaşlatır; saldırı hızı yalnız hızlandırır,
> düşman başına kombo sayısını büyütmez. Kombo, düşman canıyla sınırlıdır.

### 6.2 §5 attack-speed note (add after the weapons table)

> **Saldırı hızı (kullanıcı kararı 2026-10-24) — ⬜:** iki kaynaktan gelir: **Hız** modülü (§7, tüfek ×1,4) ve
> **Sözleşme 4** (§8, tüfek ve pala ×1,25). Kaynaklar çarpımla birikir. Tüfekte saniyedeki atış sayısı artar
> (6 → Sözleşme 4 ile 7,5 · Hız ile 8,4 · ikisiyle 10,5). Palada yalnız vuruştan sonraki bekleme kısalır
> (0,5 → 0,4 sn); vuruşun kendisi 0,2 sn kalır. Roketin dolum süresini yalnız Sözleşme 1 değiştirir.

### 6.3 §7 Hız module row (and the rules it touches)

Table row:

> | **Hız** | Tüfek saniyede 6 → 8,4 atış (×1,4) | Her mermi nişandan rastgele ±6° sapar (Üçlü Atış'ta her mermi ayrı ayrı); yakında güçlü, 8 m'den uzakta ıskalar | *(yer tutucu, yazar ekibine)* Daha hızlı. Nişan mı? Aşağı yukarı. Bir şey olmaz. |

Removal sentence to add to the paragraph under the table:

> Hız sökülünce atış hızı ve sapma hemen tabana döner.

Lines that must change in §7 with the 4th type (recommended option D2 = A):

> | Kapasite | Her türden en fazla 1; dört tür, dört sabit yuva; kopya teklif "TAKILI" gösterir; dört modül de takılıyken yeni teklif gelmez |
>
> Sıklık satırında: "... dört tür torbadan karışık gelir"

K-7 wording (recommended option D1 = A, rule stays as it is):

> K-7 | Fısıltı modülleri yalnız tüfeği etkiler (Hız dahil).

Alternative if the user picks D1 = B (Hız also speeds the machete):

> K-7 | Fısıltı modülleri yalnız tüfeği etkiler. Tek istisna Hız'dır: palanın vuruş sonrası beklemesini de ×0,7
> kısaltır (0,5 → 0,35 sn). Hız'ın bedeli (sapma) yalnız tüfektedir.

and the Hız advantage cell becomes: "Tüfek saniyede 6 → 8,4 atış (×1,4); pala beklemesi 0,5 → 0,35 sn".

### 6.4 §8 contract 4 row

> | 4 | Tüm hasar +%10 ve saldırı hızı +%25: tüfek saniyede 6 → 7,5 atış, pala beklemesi 0,5 → 0,4 sn (roket değişmez) (❓ A-10) | **Yüzün:** modelden vokseller kalıcı olarak düşer (isabet alanı küçülmez, K-13) | Azami can 80 → 60 | Yüzde on hasar. Ondan yüzünü alıyorum. Aynaya bakmıyordu nasılsa. |

(Devil line left unchanged; a new line is the writer's call.)

Add to **Birikme kuralları**:

> Saldırı hızı çarpanları da çarpımla birikir (Hız ×1,4 × Sözleşme 4 ×1,25 = ×1,75; tüfekte en çok 10,5 atış/sn).

### 6.5 Knock-on lines elsewhere in MAIN_GDD.md

- §11 ledger row 4, "Aldıkların": `+%10 hasar, +%25 saldırı hızı`.
- §12 HUD: "üstte can, süre, skor **ve kombo**"; bottom module slots **4** (if D2 = A).
- §14 Kapsam dışı: remove "ek modül" (the user's 2026-10-24 decision adds one); add combo and Hız to the Minimum or
  İsteğe bağlı tier — producer/user call.
- §15: record the two user decisions as K-18 (kombo ve skor çarpanı) and K-19 (saldırı hızı: Hız modülü + Sözleşme 4).
  A-10 stays open in wording until the user says whether adding attack speed answers it.
- Değişiklik geçmişi + Son güncelleme per AGENTS.md.

---

## 7. Decisions that still need the user (not settled here)

**D1 — Does Hız touch the machete?**
- **A. Rifle only (Recommended).** K-7 stays intact; advantage and cost sit on the same weapon (readable trade); the
  machete already gets speed from Contract 4.
- B. Rifle + machete cooldown x0.7. Matches the original proposal; needs the K-7 exception wording above; machete +
  wave spam reaches 0.48 s cycles with Contract 4.

**D2 — 4 types vs slots.**
- **A. 4 fixed slots, one per type (Recommended).** Matches the current fixed-slot pattern (slot count = type count);
  no new rule.
- B. Keep 3 slots; a 4th type cannot be accepted until one is ejected (offer shows "YUVA DOLU"). Thematic ("say no"
  pressure) but adds a new rule and HUD state.

**D3 — Combo window 3.0 s with empty-arena pause (Recommended) vs the starting 2.5 s without pause.** Only a tuning
choice; can be settled by playtest.

---

## 8. Acceptance criteria

Functional
- Rocket that kills 5 enemies raises combo by exactly 10.
- Pierce bullet through 3 enemies: +3; Triple Shot salvo where 2 bullets hit one enemy and 1 misses: +2.
- Shockwave kills: combo unchanged, +100 each.
- Combo 0 on any HP loss, including own Echo bullet; unchanged on dodge-blocked hits.
- Timer does not drain with zero live enemies, during the Devil card, pause or hitstop.
- Kill at combo 49 → hit makes 50, kill makes 51 → scores 200.
- Hız equipped: 8.4 shots/s held fire; ejecting restores 6.0 and zero spread the same frame.
- Contract 4: 7.5 shots/s, machete cooldown 0.4 s, damage x1.1; with Hız: 10.5 shots/s.

Experiential (playtest)
- Typical players see x1.2–x1.6 and understand why they lost the combo.
- A skilled player reaches x3.0 at least once in a run without it feeling automatic.
- Hız feels strong up close and noticeably worse vs Nişancı at range (players can say why).
- 10.5 shots/s with Triple Shot does not drop frame rate or clip audio.
