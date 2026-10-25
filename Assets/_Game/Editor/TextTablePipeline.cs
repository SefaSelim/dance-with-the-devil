using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace DanceWithTheDevil.Editor
{
    /// <summary>
    /// The designer's text table pipeline. Code declares every player-facing text as a static <see cref="TextEntry"/>
    /// (key, default, note); <b>Export</b> writes or merges them into <c>Assets/_Game/Texts/Yazilar.xlsx</c> (the
    /// designer's 'Metin' edits are kept); <b>Import</b> reads the Excel file into
    /// <c>Assets/_Game/Resources/GameTextTable.asset</c>, which <see cref="Texts"/> uses at runtime. Import runs by
    /// itself whenever Unity sees the Excel file change (<see cref="TextTablePostprocessor"/>).
    /// </summary>
    public static class TextTablePipeline
    {
        public const string XlsxPath = "Assets/_Game/Texts/Yazilar.xlsx";
        public const string TablePath = "Assets/_Game/Resources/GameTextTable.asset";
        /// <summary>'Nerede' prefix of rows whose key no longer exists in code (kept at the bottom, never deleted).</summary>
        public const string UnusedMark = "(kullanılmıyor)";

        private static readonly Regex Placeholder = new Regex(@"(?<!\{)\{(\d+)(?:[,:][^}]*)?\}(?!\})");

        // ------------------------------------------------------------ registry

        /// <summary>
        /// Every <see cref="TextEntry"/> held in a static field (or an array of them) of the runtime assembly, in a
        /// stable order. Duplicate keys are logged as errors and only the first is kept.
        /// </summary>
        public static List<TextEntry> CollectEntries()
        {
            var result = new List<TextEntry>();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            Type[] types;
            try { types = typeof(Texts).Assembly.GetTypes(); }
            catch (ReflectionTypeLoadException e) { types = Array.FindAll(e.Types, t => t != null); }

            Array.Sort(types, (a, b) => string.CompareOrdinal(a.FullName, b.FullName));
            const BindingFlags flags = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
            foreach (Type type in types)
            {
                if (type.ContainsGenericParameters) continue;
                foreach (FieldInfo field in type.GetFields(flags))
                {
                    if (!HoldsEntries(field.FieldType)) continue;
                    object value;
                    try { value = field.GetValue(null); }
                    catch (Exception) { continue; }
                    Add(value, result, seen, type.Name + "." + field.Name);
                }
            }
            return result;
        }

        /// <summary>TextEntry, arrays of it (any depth) or IEnumerable&lt;TextEntry&gt;; other statics are never read.</summary>
        private static bool HoldsEntries(Type type)
        {
            if (type == typeof(TextEntry)) return true;
            if (type.IsArray) return HoldsEntries(type.GetElementType());
            return typeof(IEnumerable<TextEntry>).IsAssignableFrom(type);
        }

        private static void Add(object value, List<TextEntry> result, HashSet<string> seen, string where)
        {
            if (value is TextEntry entry)
            {
                if (string.IsNullOrEmpty(entry.Key)) Debug.LogError("[Texts] Empty key in " + where);
                else if (!seen.Add(entry.Key)) Debug.LogError("[Texts] Duplicate key '" + entry.Key + "' in " + where);
                else result.Add(entry);
            }
            else if (value is IEnumerable list && !(value is string))
            {
                foreach (object item in list)
                    if (item is TextEntry || item is IEnumerable) Add(item, result, seen, where);
            }
        }

        // ------------------------------------------------------------ export

        [MenuItem("Dance/Texts/Excel'i Güncelle (Export)")]
        public static void ExportMenu()
        {
            try
            {
                string summary = Export(XlsxPath);
                Debug.Log("[Texts] " + summary);
                AssetDatabase.ImportAsset(XlsxPath, ImportAssetOptions.ForceUpdate); // the postprocessor imports it back
            }
            catch (IOException e)
            {
                Debug.LogError("[Texts] Could not write " + XlsxPath + " (is it open in Excel? close it and retry): " + e.Message);
            }
        }

        /// <summary>
        /// Writes or merges the sheet at <paramref name="path"/>: every code key sorted by key, existing 'Metin' kept,
        /// new keys get Metin = default, 'Varsayılan' and 'Nerede' refreshed from code; keys no longer in code stay
        /// at the bottom marked <see cref="UnusedMark"/>. Returns a one-line summary.
        /// </summary>
        public static string Export(string path)
        {
            List<TextEntry> entries = CollectEntries();
            var existing = new Dictionary<string, TextRow>(StringComparer.Ordinal);
            if (File.Exists(path))
                foreach (TextRow row in TextTableXlsx.Read(path))
                    if (!existing.ContainsKey(row.Key)) existing.Add(row.Key, row);

            entries.Sort((a, b) => string.CompareOrdinal(a.Key, b.Key));
            var rows = new List<TextRow>(entries.Count + existing.Count);
            var inCode = new HashSet<string>(StringComparer.Ordinal);
            int added = 0;
            foreach (TextEntry entry in entries)
            {
                inCode.Add(entry.Key);
                string text;
                if (existing.TryGetValue(entry.Key, out TextRow old)) text = old.Text;
                else
                {
                    text = entry.Default;
                    added++;
                }
                rows.Add(new TextRow(entry.Key, text, entry.Default, entry.Note));
            }

            var unused = new List<TextRow>();
            foreach (TextRow row in existing.Values)
            {
                if (inCode.Contains(row.Key)) continue;
                string note = row.Note ?? "";
                if (!note.StartsWith(UnusedMark, StringComparison.Ordinal)) note = (UnusedMark + " " + note).TrimEnd();
                unused.Add(new TextRow(row.Key, row.Text, row.Default, note));
            }
            unused.Sort((a, b) => string.CompareOrdinal(a.Key, b.Key));
            rows.AddRange(unused);

            string dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            TextTableXlsx.Write(path, rows);
            return $"Exported {path}: {entries.Count} keys ({added} new), {unused.Count} unused kept at the bottom.";
        }

        // ------------------------------------------------------------ import

        [MenuItem("Dance/Texts/Excel'den Oku (Import)")]
        public static void ImportMenu()
        {
            if (!File.Exists(XlsxPath))
            {
                Debug.LogWarning("[Texts] " + XlsxPath + " not found; run Dance/Texts/Excel'i Güncelle (Export) first.");
                return;
            }
            Import();
        }

        /// <summary>Reads <see cref="XlsxPath"/> into <see cref="TablePath"/> and logs a short summary.</summary>
        public static void Import()
        {
            List<TextRow> rows;
            try { rows = TextTableXlsx.Read(XlsxPath); }
            catch (Exception e)
            {
                Debug.LogError("[Texts] Could not read " + XlsxPath + ": " + e.Message);
                return;
            }

            var defaults = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (TextEntry entry in CollectEntries()) defaults[entry.Key] = entry.Default;

            var keys = new List<string>(rows.Count);
            var values = new List<string>(rows.Count);
            var seen = new HashSet<string>(StringComparer.Ordinal);
            var warnings = new StringBuilder();
            int empty = 0, unknown = 0, rejected = 0;
            foreach (TextRow row in rows)
            {
                if (!seen.Add(row.Key))
                {
                    warnings.Append("\n  ").Append(row.Key).Append(": appears twice, the first row is used");
                    continue;
                }
                string text = row.Text ?? "";
                if (!defaults.TryGetValue(row.Key, out string def))
                {
                    unknown++;
                    def = row.Default ?? "";
                }
                if (text.Length == 0) empty++;
                else if (!CheckPlaceholders(row.Key, text, def, warnings))
                {
                    rejected++;
                    text = ""; // the code default is shown instead
                }
                keys.Add(row.Key);
                values.Add(text);
            }

            WriteTable(keys.ToArray(), values.ToArray());
            Texts.Reload();

            string summary = $"[Texts] Imported {XlsxPath}: {rows.Count} rows, {empty} empty (default used), " +
                             $"{unknown} not used by code, {rejected} rejected (bad placeholders).";
            if (warnings.Length > 0) Debug.LogWarning(summary + warnings);
            else Debug.Log(summary);
        }

        /// <summary>
        /// For texts whose default has {0}-style placeholders: the text must format with the default's argument
        /// count (else it is rejected); a placeholder left out only warns.
        /// </summary>
        public static bool CheckPlaceholders(string key, string text, string defaultText, StringBuilder warnings)
        {
            if (string.IsNullOrEmpty(defaultText)) return true;
            int argCount = 0;
            var used = new HashSet<int>();
            foreach (Match m in Placeholder.Matches(defaultText))
                argCount = Math.Max(argCount, int.Parse(m.Groups[1].Value) + 1);
            if (argCount == 0) return true;

            var args = new object[argCount];
            for (int i = 0; i < argCount; i++) args[i] = "x";
            try
            {
                string.Format(text, args);
            }
            catch (FormatException)
            {
                warnings?.Append("\n  ").Append(key).Append(": bad {n} placeholder (allowed {0}..{")
                    .Append(argCount - 1).Append("}), the default is used");
                return false;
            }

            foreach (Match m in Placeholder.Matches(text)) used.Add(int.Parse(m.Groups[1].Value));
            for (int i = 0; i < argCount; i++)
                if (!used.Contains(i))
                    warnings?.Append("\n  ").Append(key).Append(": {").Append(i).Append("} is missing (allowed, but the value will not show)");
            return true;
        }

        private static void WriteTable(string[] keys, string[] values)
        {
            var table = AssetDatabase.LoadAssetAtPath<GameTextTable>(TablePath);
            if (table == null)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(TablePath));
                table = ScriptableObject.CreateInstance<GameTextTable>();
                table.SetRows(keys, values);
                AssetDatabase.CreateAsset(table, TablePath);
                AssetDatabase.SaveAssets();
                return;
            }
            if (Same(table.Keys, keys) && Same(table.Values, values)) return; // no needless asset churn in git
            table.SetRows(keys, values);
            EditorUtility.SetDirty(table);
            AssetDatabase.SaveAssetIfDirty(table);
        }

        private static bool Same(string[] a, string[] b)
        {
            if (a == null || b == null || a.Length != b.Length) return false;
            for (int i = 0; i < a.Length; i++)
                if (!string.Equals(a[i], b[i], StringComparison.Ordinal)) return false;
            return true;
        }
    }

    /// <summary>Re-imports the text table whenever Unity imports a changed <c>Yazilar.xlsx</c> (saved from Excel).</summary>
    public sealed class TextTablePostprocessor : AssetPostprocessor
    {
        private static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] moved, string[] movedFrom)
        {
            foreach (string path in imported)
            {
                if (!string.Equals(path, TextTablePipeline.XlsxPath, StringComparison.OrdinalIgnoreCase)) continue;
                // Right here, not on EditorApplication.delayCall: that queue is not run while the editor sits in the
                // background, and the table asset is a different path, so writing it cannot loop back to this file.
                TextTablePipeline.Import();
                return;
            }
        }
    }
}
