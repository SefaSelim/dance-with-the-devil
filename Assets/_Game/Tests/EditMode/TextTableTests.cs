using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Text.RegularExpressions;
using DanceWithTheDevil.Editor;
using NUnit.Framework;
using UnityEngine;

namespace DanceWithTheDevil.Tests
{
    /// <summary>The designer's Excel text table: xlsx write/read, Excel-shaped files, lookup fallback, key registry.</summary>
    public sealed class TextTableTests
    {
        private string _dir;
        private GameTextTable _table;

        [SetUp]
        public void SetUp()
        {
            _dir = Path.Combine(Path.GetTempPath(), "dwtd_texts_" + System.Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_dir);
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_dir)) Directory.Delete(_dir, true);
            if (_table != null) Object.DestroyImmediate(_table);
            Texts.Reload(); // back to the project's table
        }

        [Test]
        public void Xlsx_RoundTrip_KeepsTurkishCharactersNewlinesAndSpaces()
        {
            var rows = new List<TextRow>
            {
                new TextRow("a.turkish", "Şeytan ğüşıöç İĞÜŞÖÇ — ×1,5", "Varsayılan", "Nerede: {0} = sayı"),
                new TextRow("b.newline", "satır bir\nsatır iki", "x\ny", ""),
                new TextRow("c.spaces", " · ", "", "<color=#FFD24D>etiket</color> & \"tırnak\""),
            };
            string path = Path.Combine(_dir, "t.xlsx");

            TextTableXlsx.Write(path, rows);
            List<TextRow> back = TextTableXlsx.Read(path);

            Assert.AreEqual(rows.Count, back.Count);
            for (int i = 0; i < rows.Count; i++)
            {
                Assert.AreEqual(rows[i].Key, back[i].Key);
                Assert.AreEqual(rows[i].Text, back[i].Text, rows[i].Key);
                Assert.AreEqual(rows[i].Default, back[i].Default, rows[i].Key);
                Assert.AreEqual(rows[i].Note, back[i].Note, rows[i].Key);
            }
        }

        [Test]
        public void Xlsx_Read_HandlesWhatExcelSaves()
        {
            // Shared strings with rich-text runs and a phonetic run, a CR escape, sparse cells (no column C), columns
            // found by header name, a numeric cell and a sheet path given by the workbook relations.
            string path = Path.Combine(_dir, "excel.xlsx");
            using (var zip = new ZipArchive(new FileStream(path, FileMode.Create), ZipArchiveMode.Create))
            {
                Put(zip, "xl/workbook.xml",
                    "<workbook xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\" " +
                    "xmlns:r=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships\">" +
                    "<sheets><sheet name=\"Yazilar\" sheetId=\"1\" r:id=\"rId7\"/></sheets></workbook>");
                Put(zip, "xl/_rels/workbook.xml.rels",
                    "<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\">" +
                    "<Relationship Id=\"rId7\" Type=\"x\" Target=\"worksheets/sheet3.xml\"/></Relationships>");
                Put(zip, "xl/sharedStrings.xml",
                    "<sst xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\">" +
                    "<si><t>Anahtar</t></si><si><t>Metin</t></si><si><t>Nerede</t></si>" +
                    "<si><t>hud.score</t></si>" +
                    "<si><r><rPr><b/></rPr><t>Puan</t></r><r><t xml:space=\"preserve\"> {0}</t></r>" +
                    "<rPh sb=\"0\" eb=\"1\"><t>IGNORED</t></rPh></si>" +
                    "<si><t>satır_x000D_\niki</t></si>" +
                    "</sst>");
                Put(zip, "xl/worksheets/sheet3.xml",
                    "<worksheet xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\"><sheetData>" +
                    "<row r=\"1\"><c r=\"A1\" t=\"s\"><v>0</v></c><c r=\"B1\" t=\"s\"><v>1</v></c>" +
                    "<c r=\"D1\" t=\"s\"><v>2</v></c></row>" +
                    "<row r=\"3\"><c r=\"A3\" t=\"s\"><v>3</v></c><c r=\"B3\" t=\"s\"><v>4</v></c>" +
                    "<c r=\"D3\" t=\"s\"><v>5</v></c></row>" +
                    "<row r=\"4\"><c r=\"A4\" t=\"inlineStr\"><is><t>n.number</t></is></c><c r=\"B4\"><v>100</v></c></row>" +
                    "<row r=\"5\"><c r=\"B5\" t=\"inlineStr\"><is><t>anahtarsız satır atlanır</t></is></c></row>" +
                    "</sheetData></worksheet>");
            }

            List<TextRow> rows = TextTableXlsx.Read(path);

            Assert.AreEqual(2, rows.Count);
            Assert.AreEqual("hud.score", rows[0].Key);
            Assert.AreEqual("Puan {0}", rows[0].Text);
            Assert.AreEqual("", rows[0].Default);
            Assert.AreEqual("satır\niki", rows[0].Note);
            Assert.AreEqual("n.number", rows[1].Key);
            Assert.AreEqual("100", rows[1].Text);
        }

        [Test]
        public void Get_FallsBackOnMissingKeyAndEmptyValue()
        {
            _table = ScriptableObject.CreateInstance<GameTextTable>();
            _table.SetRows(new[] { "t.set", "t.empty" }, new[] { "Tablodan", "" });
            Texts.Load(_table);

            Assert.AreEqual("Tablodan", Texts.Get("t.set", "kod"));
            Assert.AreEqual("kod", Texts.Get("t.empty", "kod"));
            Assert.AreEqual("yedek", Texts.Get("t.__missing__", "yedek"));
            Assert.AreEqual("yedek", Texts.Get(null, "yedek"));
        }

        [Test]
        public void Format_BrokenTableFormat_UsesDefault()
        {
            _table = ScriptableObject.CreateInstance<GameTextTable>();
            _table.SetRows(new[] { "t.fmt" }, new[] { "Puan {3}" });
            Texts.Load(_table);

            Assert.AreEqual("Puan 7", Texts.Format("t.fmt", "Puan {0}", 7));
        }

        [Test]
        public void Placeholders_RejectUnknownIndexAndWarnOnMissing()
        {
            var warnings = new StringBuilder();
            Assert.IsFalse(TextTablePipeline.CheckPlaceholders("k", "{2} kombo", "{0} ×{1}", warnings));
            Assert.IsFalse(TextTablePipeline.CheckPlaceholders("k", "Puan {0", "Puan {0}", warnings));
            Assert.IsTrue(TextTablePipeline.CheckPlaceholders("k", "Skor: {0}", "Puan {0}", warnings));
            warnings.Clear();
            Assert.IsTrue(TextTablePipeline.CheckPlaceholders("k", "KOMBO", "{0} KOMBO", warnings));
            StringAssert.Contains("{0} is missing", warnings.ToString());
            Assert.IsTrue(TextTablePipeline.CheckPlaceholders("k", "{x}", "Ses yok", warnings), "no placeholders in default");
        }

        [Test]
        public void Registry_HasEveryAreaWithUniqueReadableKeys()
        {
            List<TextEntry> entries = TextTablePipeline.CollectEntries(); // duplicates would log errors and fail
            var keyPattern = new Regex(@"^[a-z0-9_]+(\.[a-z0-9_]+)+$");
            var keys = new HashSet<string>();
            foreach (TextEntry e in entries)
            {
                Assert.IsTrue(keyPattern.IsMatch(e.Key), "key " + e.Key);
                Assert.IsTrue(keys.Add(e.Key), "duplicate " + e.Key);
                Assert.IsNotNull(e.Default, e.Key);
                Assert.IsFalse(string.IsNullOrEmpty(e.Note), "note of " + e.Key);
            }
            foreach (string key in new[] { "intro.line", "hud.score", "menu.start", "combo.popup_crit",
                         "module.pierce.name", "deal.6.offer", "ending.kept_yourself.title" })
                Assert.IsTrue(keys.Contains(key), key);
            Assert.GreaterOrEqual(entries.Count, 140);
        }

        private static void Put(ZipArchive zip, string name, string xml)
        {
            using (var w = new StreamWriter(zip.CreateEntry(name).Open(), new UTF8Encoding(false))) w.Write(xml);
        }
    }
}
