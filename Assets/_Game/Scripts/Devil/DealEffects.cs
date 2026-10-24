using System.Collections.Generic;
using UnityEngine;

namespace DanceWithTheDevil
{
    /// <summary>The player components a contract changes. Any field may be null (tests, partial scenes).</summary>
    public struct DealTargets
    {
        public Health Health;
        public PlayerController Controller;
        public Rifle Rifle;
        public RocketLauncher Rocket;
        public Machete Machete;
        public PlayerInputReader Input;

        /// <summary>Collects the targets from the player root (GetComponent / GetComponentInChildren only).</summary>
        public static DealTargets FromPlayer(Health health)
        {
            var targets = new DealTargets { Health = health };
            if (health == null) return targets;
            GameObject root = health.gameObject;
            targets.Controller = root.GetComponent<PlayerController>();
            targets.Rifle = root.GetComponentInChildren<Rifle>(true);
            targets.Rocket = root.GetComponentInChildren<RocketLauncher>(true);
            targets.Machete = root.GetComponentInChildren<Machete>(true);
            targets.Input = root.GetComponentInChildren<PlayerInputReader>(true);
            return targets;
        }
    }

    /// <summary>
    /// Mechanical upgrade + mechanical cost of each death contract (MAIN_GDD §8 table and stacking rules).
    /// Visible costs (colour, portrait, face parts, vignette) are presented by <see cref="DevilDirector"/>.
    /// Stacking: damage multipliers multiply; max-health losses add up and are permanent; cooldown multipliers
    /// multiply the base time and never change charge counts.
    /// </summary>
    public static class DealEffects
    {
        public const float BaseMaxHealth = 100f;

        // Contract 1. A-9 is open ("×2" = radius ×2 or area ×2). Provisional: area ×2 → radius ×√2 (2,5 → 3,54 m).
        public const float RocketDamageFactor = 2f;
        public const float RocketRadiusFactor = 1.41421356f;
        public const float RocketFireRateBonus = 1.4f; // refill time ÷ 1.4: 6,0 → 4,29 s
        // Contract 2.
        public const int ExtraDodgeCharges = 1;
        // Contract 3.
        public const int ExtraPierce = 2;
        public const float EnemySpeedFactor = 1.15f;
        // Contract 4. A-10 is open (is +10% a deliberately empty promise?). Implemented as written: all weapons ×1.1.
        public const float AllDamageFactor = 1.1f;
        // Contract 5.
        public const int ExtraRocketCharges = 1;
        public const float DodgeCooldownFactor = 2f;
        // Contract 6 (A-4 provisional, see DevilDirector).
        public const float ControlDelaySeconds = 0.25f;
        // K-12.
        public const float ReviveInvulnerabilitySeconds = 2f;

        /// <summary>Applies the contract's upgrade and mechanical cost. Max health changes without refilling.</summary>
        public static void Apply(DealDefinition deal, in DealTargets t)
        {
            if (deal == null) return;

            if (t.Health != null && !Mathf.Approximately(deal.MaxHealthDelta, 0f))
                t.Health.SetMaxHealth(t.Health.MaxHealth + deal.MaxHealthDelta, false);

            switch (deal.Number)
            {
                case 1:
                    if (t.Rocket != null)
                    {
                        t.Rocket.DamageMultiplier *= RocketDamageFactor;
                        t.Rocket.RadiusMultiplier *= RocketRadiusFactor;
                        t.Rocket.RefillTimeMultiplier /= RocketFireRateBonus;
                    }
                    break;

                case 2:
                    if (t.Machete != null) t.Machete.WaveEnabled = true;
                    if (t.Controller != null) t.Controller.SetDodgeCharges(t.Controller.MaxDodgeCharges + ExtraDodgeCharges);
                    // K-8: only Defne's voice and one music layer; game cues stay. Read by the audio layer (Phase 6).
                    AudioCosts.VoiceMuted = true;
                    AudioCosts.MusicLayerMuted = true;
                    break;

                case 3:
                    if (t.Rifle != null) t.Rifle.PierceCount += ExtraPierce;
                    Enemy.SpeedMultiplier *= EnemySpeedFactor;
                    break;

                case 4:
                    if (t.Rifle != null) t.Rifle.DamageMultiplier *= AllDamageFactor;
                    if (t.Rocket != null) t.Rocket.DamageMultiplier *= AllDamageFactor;
                    if (t.Machete != null) t.Machete.DamageMultiplier *= AllDamageFactor;
                    break;

                case 5:
                    if (t.Rocket != null) t.Rocket.SetMaxCharges(t.Rocket.MaxCharges + ExtraRocketCharges, false);
                    if (t.Controller != null) t.Controller.DodgeCooldownMultiplier *= DodgeCooldownFactor;
                    break;

                case 6:
                    if (t.Input != null) t.Input.InputDelay = ControlDelaySeconds;
                    break;
            }
        }

        /// <summary>Revive invulnerability after taking <paramref name="deal"/> (K-12: 2 s, 0 from contract 5 on).</summary>
        public static float ReviveInvulnerabilityAfter(DealDefinition deal, float current)
        {
            return deal != null && deal.Number == 5 ? 0f : current;
        }

        /// <summary>Max health after the first <paramref name="taken"/> deals of <paramref name="deals"/>.</summary>
        public static float MaxHealthAfter(IReadOnlyList<DealDefinition> deals, int taken, float baseMax = BaseMaxHealth)
        {
            float max = baseMax;
            int count = Mathf.Min(taken, deals != null ? deals.Count : 0);
            for (int i = 0; i < count; i++)
                if (deals[i] != null) max += deals[i].MaxHealthDelta;
            return Mathf.Max(1f, max);
        }
    }
}
