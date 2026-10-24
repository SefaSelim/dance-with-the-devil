namespace DanceWithTheDevil
{
    /// <summary>
    /// Combo HUD strings (placeholders; the writer polishes them). Kept apart from GameTexts, which another lane owns.
    /// </summary>
    public static class ComboTexts
    {
        /// <summary>Small caption above the count.</summary>
        public const string Title = "KOMBO";
        /// <summary>Multiplier: {0} whole part, {1} tenths (Turkish decimal comma: "×1,5").</summary>
        public const string MultiplierFormat = "×{0},{1}";
        /// <summary>Shown as the counter falls after Defne lost health.</summary>
        public const string BrokenByDamage = "KIRILDI";
        /// <summary>Shown as the counter fades after the window ran out.</summary>
        public const string BrokenByTimeout = "SÖNDÜ";
    }
}
