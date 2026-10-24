namespace DanceWithTheDevil
{
    /// <summary>
    /// Combo and world popup strings (placeholders; the writer polishes them). Kept apart from GameTexts, which another
    /// lane owns.
    /// </summary>
    public static class ComboTexts
    {
        /// <summary>Small caption above the count (old ComboHud counter, off by default).</summary>
        public const string Title = "KOMBO";
        /// <summary>Multiplier: {0} whole part, {1} tenths (Turkish decimal comma: "×1,5").</summary>
        public const string MultiplierFormat = "×{0},{1}";
        /// <summary>Score line in the HUD with the live multiplier: {0} the score line, {1} whole, {2} tenths.</summary>
        public const string ScoreWithMultiplierFormat = "{0}  <color=#FFD24D>×{1},{2}</color>";
        /// <summary>Shown as the counter falls after Defne lost health (old ComboHud).</summary>
        public const string BrokenByDamage = "KIRILDI";
        /// <summary>Shown as the counter fades after the window ran out (old ComboHud).</summary>
        public const string BrokenByTimeout = "SÖNDÜ";

        /// <summary>World popup on a critical hit: {0} the damage dealt.</summary>
        public const string PopupCritFormat = "KRİTİK! {0}";
        /// <summary>World popup over Defne every 5 combo: {0} the count.</summary>
        public const string PopupComboFormat = "{0} KOMBO";
        /// <summary>World popup over Defne at a tier (10/25/50/100): {0} count, {1} whole, {2} tenths of the multiplier.</summary>
        public const string PopupTierFormat = "{0} KOMBO!\n×{1},{2}";
    }
}
