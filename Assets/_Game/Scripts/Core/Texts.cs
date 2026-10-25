using System;
using System.Collections.Generic;
using UnityEngine;

namespace DanceWithTheDevil
{
    /// <summary>
    /// Player-facing text lookup. Every text has a stable key (e.g. <c>hud.score</c>) and a default written in code;
    /// the designer's Excel table (<c>Assets/_Game/Texts/Yazilar.xlsx</c>, imported into
    /// <c>Resources/GameTextTable</c>) overrides it. A missing key or an empty cell shows the default, so the game
    /// never shows a raw key. The table is read once into a dictionary; lookups after that do not allocate.
    /// </summary>
    public static class Texts
    {
        /// <summary>Resources path of the imported table.</summary>
        public const string ResourcePath = "GameTextTable";

        private static Dictionary<string, string> _map;

        /// <summary>The table's text for <paramref name="key"/>, or <paramref name="fallback"/> when missing or empty.</summary>
        public static string Get(string key, string fallback)
        {
            if (_map == null) Load(Resources.Load<GameTextTable>(ResourcePath));
            return key != null && _map.TryGetValue(key, out string value) && !string.IsNullOrEmpty(value)
                ? value
                : fallback;
        }

        /// <summary>
        /// <see cref="Get"/> then string.Format. A broken placeholder in the table (the importer already rejects
        /// these) falls back to the default format instead of throwing.
        /// </summary>
        public static string Format(string key, string fallback, params object[] args)
        {
            string format = Get(key, fallback);
            try
            {
                return string.Format(format, args);
            }
            catch (FormatException)
            {
                return string.Format(fallback, args);
            }
        }

        /// <summary>Uses <paramref name="table"/> from now on (null = defaults only). Editor import and tests.</summary>
        public static void Load(GameTextTable table)
        {
            var map = new Dictionary<string, string>(StringComparer.Ordinal);
            if (table != null && table.Keys != null && table.Values != null)
            {
                int n = Mathf.Min(table.Keys.Length, table.Values.Length);
                for (int i = 0; i < n; i++)
                {
                    string key = table.Keys[i];
                    if (!string.IsNullOrEmpty(key)) map[key] = table.Values[i];
                }
            }
            _map = map;
        }

        /// <summary>Forgets the loaded table; the next lookup reads Resources again (after an Excel import).</summary>
        public static void Reload() => _map = null;

        // Without domain reload the cache would outlive an Excel edit between two play sessions.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnPlay() => _map = null;
    }

    /// <summary>
    /// One player-facing text: its table key, the default from code and a short Turkish note for the Excel 'Nerede'
    /// column (where it shows, what <c>{0}</c> means). Declared as static fields next to the code that uses them;
    /// the editor exporter finds every static <see cref="TextEntry"/> (and arrays of them) in this assembly.
    /// </summary>
    public sealed class TextEntry
    {
        public readonly string Key;
        public readonly string Default;
        public readonly string Note;

        public TextEntry(string key, string defaultText, string note)
        {
            Key = key;
            Default = defaultText;
            Note = note;
        }

        /// <summary>The text to show now (table value, or the default).</summary>
        public string Text => Texts.Get(Key, Default);

        public override string ToString() => Text;
    }
}
