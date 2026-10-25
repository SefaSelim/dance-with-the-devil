using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Xml;
using System.Xml.Linq;

namespace DanceWithTheDevil.Editor
{
    /// <summary>One row of the text sheet: key, the text the game shows, the code default and the "where" note.</summary>
    public struct TextRow
    {
        public string Key;
        public string Text;
        public string Default;
        public string Note;

        public TextRow(string key, string text, string defaultText, string note)
        {
            Key = key;
            Text = text;
            Default = defaultText;
            Note = note;
        }
    }

    /// <summary>
    /// Minimal .xlsx reader / writer for the four-column text sheet (Anahtar, Metin, Varsayılan, Nerede), with no
    /// third-party package. The writer makes the smallest package Excel opens (inline strings, bold frozen header,
    /// wrapped cells). The reader accepts what Excel saves back: shared strings with rich-text runs, inline strings,
    /// sparse rows (cell positions from the <c>r</c> attribute), strict or transitional namespaces, <c>_xHHHH_</c>
    /// escapes; columns are found by their header names, so reordering them in Excel is fine.
    /// </summary>
    public static class TextTableXlsx
    {
        public const string SheetName = "Yazilar";
        public const string KeyHeader = "Anahtar";
        public const string TextHeader = "Metin";
        public const string DefaultHeader = "Varsayılan";
        public const string NoteHeader = "Nerede";

        private const string MainNs = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
        private const string RelNs = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
        private const string PackageRelNs = "http://schemas.openxmlformats.org/package/2006/relationships";

        // ------------------------------------------------------------ write

        /// <summary>
        /// Writes <paramref name="rows"/> under the header to <paramref name="path"/>. Goes through a temp file, so a
        /// failure (the file is open in Excel) leaves the old file intact and throws IOException.
        /// </summary>
        public static void Write(string path, IList<TextRow> rows)
        {
            string temp = path + ".tmp";
            using (var file = new FileStream(temp, FileMode.Create, FileAccess.Write))
            using (var zip = new ZipArchive(file, ZipArchiveMode.Create))
            {
                WriteEntry(zip, "[Content_Types].xml",
                    "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" +
                    "<Types xmlns=\"http://schemas.openxmlformats.org/package/2006/content-types\">" +
                    "<Default Extension=\"rels\" ContentType=\"application/vnd.openxmlformats-package.relationships+xml\"/>" +
                    "<Default Extension=\"xml\" ContentType=\"application/xml\"/>" +
                    "<Override PartName=\"/xl/workbook.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml\"/>" +
                    "<Override PartName=\"/xl/worksheets/sheet1.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml\"/>" +
                    "<Override PartName=\"/xl/styles.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.styles+xml\"/>" +
                    "</Types>");
                WriteEntry(zip, "_rels/.rels",
                    "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" +
                    "<Relationships xmlns=\"" + PackageRelNs + "\">" +
                    "<Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument\" Target=\"xl/workbook.xml\"/>" +
                    "</Relationships>");
                WriteEntry(zip, "xl/workbook.xml",
                    "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" +
                    "<workbook xmlns=\"" + MainNs + "\" xmlns:r=\"" + RelNs + "\">" +
                    "<sheets><sheet name=\"" + SheetName + "\" sheetId=\"1\" r:id=\"rId1\"/></sheets>" +
                    "</workbook>");
                WriteEntry(zip, "xl/_rels/workbook.xml.rels",
                    "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" +
                    "<Relationships xmlns=\"" + PackageRelNs + "\">" +
                    "<Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet\" Target=\"worksheets/sheet1.xml\"/>" +
                    "<Relationship Id=\"rId2\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/styles\" Target=\"styles.xml\"/>" +
                    "</Relationships>");
                // Fonts: 0 normal, 1 bold (header), 2 grey (read-only columns). Xfs: 0 default, 1 header, 2 wrapped
                // top-aligned (Metin), 3 wrapped grey (Anahtar, Varsayılan, Nerede).
                WriteEntry(zip, "xl/styles.xml",
                    "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" +
                    "<styleSheet xmlns=\"" + MainNs + "\">" +
                    "<fonts count=\"3\">" +
                    "<font><sz val=\"11\"/><name val=\"Calibri\"/><family val=\"2\"/></font>" +
                    "<font><b/><sz val=\"11\"/><name val=\"Calibri\"/><family val=\"2\"/></font>" +
                    "<font><sz val=\"11\"/><color rgb=\"FF707070\"/><name val=\"Calibri\"/><family val=\"2\"/></font>" +
                    "</fonts>" +
                    "<fills count=\"3\"><fill><patternFill patternType=\"none\"/></fill>" +
                    "<fill><patternFill patternType=\"gray125\"/></fill>" +
                    "<fill><patternFill patternType=\"solid\"><fgColor rgb=\"FFE8E2D0\"/><bgColor indexed=\"64\"/></patternFill></fill>" +
                    "</fills>" +
                    "<borders count=\"1\"><border><left/><right/><top/><bottom/><diagonal/></border></borders>" +
                    "<cellStyleXfs count=\"1\"><xf numFmtId=\"0\" fontId=\"0\" fillId=\"0\" borderId=\"0\"/></cellStyleXfs>" +
                    "<cellXfs count=\"4\">" +
                    "<xf numFmtId=\"0\" fontId=\"0\" fillId=\"0\" borderId=\"0\" xfId=\"0\"/>" +
                    "<xf numFmtId=\"0\" fontId=\"1\" fillId=\"2\" borderId=\"0\" xfId=\"0\" applyFont=\"1\" applyFill=\"1\"/>" +
                    "<xf numFmtId=\"49\" fontId=\"0\" fillId=\"0\" borderId=\"0\" xfId=\"0\" applyNumberFormat=\"1\" applyAlignment=\"1\"><alignment vertical=\"top\" wrapText=\"1\"/></xf>" +
                    "<xf numFmtId=\"49\" fontId=\"2\" fillId=\"0\" borderId=\"0\" xfId=\"0\" applyNumberFormat=\"1\" applyFont=\"1\" applyAlignment=\"1\"><alignment vertical=\"top\" wrapText=\"1\"/></xf>" +
                    "</cellXfs>" +
                    "<cellStyles count=\"1\"><cellStyle name=\"Normal\" xfId=\"0\" builtinId=\"0\"/></cellStyles>" +
                    "</styleSheet>");
                WriteSheet(zip, rows);
            }

            if (File.Exists(path)) File.Delete(path);
            File.Move(temp, path);
        }

        private static void WriteEntry(ZipArchive zip, string name, string content)
        {
            ZipArchiveEntry entry = zip.CreateEntry(name, CompressionLevel.Optimal);
            using (var writer = new StreamWriter(entry.Open(), new UTF8Encoding(false)))
                writer.Write(content);
        }

        private static void WriteSheet(ZipArchive zip, IList<TextRow> rows)
        {
            ZipArchiveEntry entry = zip.CreateEntry("xl/worksheets/sheet1.xml", CompressionLevel.Optimal);
            var settings = new XmlWriterSettings
            {
                Encoding = new UTF8Encoding(false),
                NewLineHandling = NewLineHandling.None, // keep "\n" inside cells exactly
                Indent = false,
            };
            using (Stream stream = entry.Open())
            using (XmlWriter w = XmlWriter.Create(stream, settings))
            {
                w.WriteStartDocument(true);
                w.WriteStartElement("worksheet", MainNs);
                w.WriteStartElement("sheetViews", MainNs);
                w.WriteStartElement("sheetView", MainNs);
                w.WriteAttributeString("workbookViewId", "0");
                w.WriteStartElement("pane", MainNs);
                w.WriteAttributeString("ySplit", "1");
                w.WriteAttributeString("topLeftCell", "A2");
                w.WriteAttributeString("activePane", "bottomLeft");
                w.WriteAttributeString("state", "frozen");
                w.WriteEndElement();
                w.WriteEndElement();
                w.WriteEndElement();

                w.WriteStartElement("cols", MainNs);
                WriteCol(w, 1, 30);
                WriteCol(w, 2, 60);
                WriteCol(w, 3, 45);
                WriteCol(w, 4, 55);
                w.WriteEndElement();

                w.WriteStartElement("sheetData", MainNs);
                WriteRow(w, 1, KeyHeader, TextHeader, DefaultHeader, NoteHeader, 1, 1);
                for (int i = 0; i < rows.Count; i++)
                {
                    TextRow row = rows[i];
                    WriteRow(w, i + 2, row.Key, row.Text, row.Default, row.Note, 3, 2);
                }
                w.WriteEndElement();
                w.WriteEndElement();
                w.WriteEndDocument();
            }
        }

        private static void WriteCol(XmlWriter w, int index, float width)
        {
            w.WriteStartElement("col", MainNs);
            w.WriteAttributeString("min", index.ToString());
            w.WriteAttributeString("max", index.ToString());
            w.WriteAttributeString("width", width.ToString(System.Globalization.CultureInfo.InvariantCulture));
            w.WriteAttributeString("customWidth", "1");
            w.WriteEndElement();
        }

        private static void WriteRow(XmlWriter w, int r, string a, string b, string c, string d, int style, int textStyle)
        {
            w.WriteStartElement("row", MainNs);
            w.WriteAttributeString("r", r.ToString());
            WriteCell(w, "A" + r, a, style);
            WriteCell(w, "B" + r, b, textStyle);
            WriteCell(w, "C" + r, c, style);
            WriteCell(w, "D" + r, d, style);
            w.WriteEndElement();
        }

        private static void WriteCell(XmlWriter w, string reference, string value, int style)
        {
            w.WriteStartElement("c", MainNs);
            w.WriteAttributeString("r", reference);
            w.WriteAttributeString("s", style.ToString());
            w.WriteAttributeString("t", "inlineStr");
            w.WriteStartElement("is", MainNs);
            w.WriteStartElement("t", MainNs);
            w.WriteAttributeString("xml", "space", null, "preserve");
            w.WriteString(CleanForXml(value));
            w.WriteEndElement();
            w.WriteEndElement();
            w.WriteEndElement();
        }

        /// <summary>Unix newlines, and no characters XML cannot hold.</summary>
        private static string CleanForXml(string value)
        {
            if (string.IsNullOrEmpty(value)) return "";
            value = value.Replace("\r\n", "\n").Replace('\r', '\n');
            var sb = new StringBuilder(value.Length);
            foreach (char ch in value)
                if (ch == '\n' || ch == '\t' || ch >= 0x20) sb.Append(ch);
            return sb.ToString();
        }

        // ------------------------------------------------------------ read

        /// <summary>
        /// Reads the rows under the header of the "Yazilar" sheet (or the first sheet). Rows without a key are
        /// skipped. Opens the file shared, so it also works while Excel has it open.
        /// </summary>
        public static List<TextRow> Read(string path)
        {
            using (var file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
            using (var zip = new ZipArchive(file, ZipArchiveMode.Read))
            {
                List<string> shared = ReadSharedStrings(zip);
                string sheetPath = FindSheetPath(zip);
                ZipArchiveEntry sheetEntry = FindEntry(zip, sheetPath);
                if (sheetEntry == null) throw new InvalidDataException("No worksheet in " + path);

                XDocument sheet = Load(sheetEntry);
                var rows = new List<string[]>();
                int nextRow = 1;
                foreach (XElement row in sheet.Descendants().Where(e => e.Name.LocalName == "row"))
                {
                    int rowIndex = ParseInt((string)row.Attribute("r"), nextRow);
                    nextRow = rowIndex + 1;
                    var cells = new List<string>();
                    int nextCol = 0;
                    foreach (XElement cell in row.Elements().Where(e => e.Name.LocalName == "c"))
                    {
                        int col = ColumnIndex((string)cell.Attribute("r"), nextCol);
                        nextCol = col + 1;
                        while (cells.Count <= col) cells.Add("");
                        cells[col] = CellValue(cell, shared);
                    }
                    while (rows.Count < rowIndex) rows.Add(null);
                    rows[rowIndex - 1] = cells.ToArray();
                }
                return ToTextRows(rows);
            }
        }

        private static List<TextRow> ToTextRows(List<string[]> rows)
        {
            var result = new List<TextRow>();
            int header = rows.FindIndex(r => r != null && r.Any(c => !string.IsNullOrWhiteSpace(c)));
            if (header < 0) return result;

            string[] names = rows[header];
            int keyCol = HeaderIndex(names, 0, KeyHeader);
            int textCol = HeaderIndex(names, 1, TextHeader);
            int defaultCol = HeaderIndex(names, 2, DefaultHeader, "Varsayilan");
            int noteCol = HeaderIndex(names, 3, NoteHeader);

            for (int i = header + 1; i < rows.Count; i++)
            {
                string[] r = rows[i];
                if (r == null) continue;
                string key = At(r, keyCol).Trim();
                if (key.Length == 0) continue;
                result.Add(new TextRow(key, At(r, textCol), At(r, defaultCol), At(r, noteCol)));
            }
            return result;
        }

        private static int HeaderIndex(string[] header, int fallback, params string[] names)
        {
            for (int i = 0; i < header.Length; i++)
            {
                string h = (header[i] ?? "").Trim();
                foreach (string name in names)
                    if (string.Equals(h, name, StringComparison.OrdinalIgnoreCase)) return i;
            }
            return fallback;
        }

        private static string At(string[] row, int i) => i >= 0 && i < row.Length && row[i] != null ? row[i] : "";

        private static List<string> ReadSharedStrings(ZipArchive zip)
        {
            var list = new List<string>();
            ZipArchiveEntry entry = FindEntry(zip, "xl/sharedStrings.xml");
            if (entry == null) return list;
            foreach (XElement si in Load(entry).Root.Elements().Where(e => e.Name.LocalName == "si"))
                list.Add(RichText(si));
            return list;
        }

        /// <summary>Text of an &lt;si&gt; or &lt;is&gt;: its own &lt;t&gt; plus every run's &lt;t&gt;, without phonetic runs.</summary>
        private static string RichText(XElement container)
        {
            var sb = new StringBuilder();
            foreach (XElement child in container.Elements())
            {
                string name = child.Name.LocalName;
                if (name == "t") sb.Append(child.Value);
                else if (name == "r")
                    foreach (XElement t in child.Elements().Where(e => e.Name.LocalName == "t")) sb.Append(t.Value);
            }
            return Normalize(sb.ToString());
        }

        private static string CellValue(XElement cell, List<string> shared)
        {
            string type = (string)cell.Attribute("t") ?? "n";
            if (type == "inlineStr")
            {
                XElement inline = cell.Elements().FirstOrDefault(e => e.Name.LocalName == "is");
                return inline != null ? RichText(inline) : "";
            }
            XElement v = cell.Elements().FirstOrDefault(e => e.Name.LocalName == "v");
            if (v == null) return "";
            if (type == "s")
            {
                int index = ParseInt(v.Value, -1);
                return index >= 0 && index < shared.Count ? shared[index] : "";
            }
            return Normalize(v.Value);
        }

        /// <summary>Unix newlines and decoded OOXML <c>_xHHHH_</c> escapes (Excel writes CR as <c>_x000D_</c>).</summary>
        private static string Normalize(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            if (s.IndexOf("_x", StringComparison.Ordinal) >= 0)
            {
                var sb = new StringBuilder(s.Length);
                for (int i = 0; i < s.Length; i++)
                {
                    if (s[i] == '_' && i + 6 < s.Length && s[i + 1] == 'x' && s[i + 6] == '_' &&
                        int.TryParse(s.Substring(i + 2, 4), System.Globalization.NumberStyles.HexNumber, null, out int code))
                    {
                        sb.Append((char)code);
                        i += 6;
                    }
                    else sb.Append(s[i]);
                }
                s = sb.ToString();
            }
            return s.Replace("\r\n", "\n").Replace('\r', '\n');
        }

        /// <summary>Path of the "Yazilar" sheet (or the first sheet) inside the package, via the workbook relations.</summary>
        private static string FindSheetPath(ZipArchive zip)
        {
            const string fallback = "xl/worksheets/sheet1.xml";
            ZipArchiveEntry workbook = FindEntry(zip, "xl/workbook.xml");
            ZipArchiveEntry rels = FindEntry(zip, "xl/_rels/workbook.xml.rels");
            if (workbook == null || rels == null) return fallback;

            List<XElement> sheets = Load(workbook).Descendants().Where(e => e.Name.LocalName == "sheet").ToList();
            if (sheets.Count == 0) return fallback;
            XElement chosen = sheets.FirstOrDefault(s =>
                string.Equals((string)s.Attribute("name"), SheetName, StringComparison.OrdinalIgnoreCase)) ?? sheets[0];
            string id = chosen.Attributes().FirstOrDefault(a => a.Name.LocalName == "id")?.Value;

            foreach (XElement rel in Load(rels).Root.Elements())
            {
                if ((string)rel.Attribute("Id") != id) continue;
                string target = ((string)rel.Attribute("Target") ?? "").Replace('\\', '/');
                return target.StartsWith("/") ? target.Substring(1) : "xl/" + target;
            }
            return fallback;
        }

        private static ZipArchiveEntry FindEntry(ZipArchive zip, string path) =>
            zip.Entries.FirstOrDefault(e => string.Equals(e.FullName.Replace('\\', '/'), path, StringComparison.OrdinalIgnoreCase));

        private static XDocument Load(ZipArchiveEntry entry)
        {
            using (Stream s = entry.Open()) return XDocument.Load(s, LoadOptions.PreserveWhitespace);
        }

        /// <summary>Zero-based column of a cell reference such as "B12", or <paramref name="fallback"/>.</summary>
        public static int ColumnIndex(string reference, int fallback)
        {
            if (string.IsNullOrEmpty(reference)) return fallback;
            int col = 0, letters = 0;
            foreach (char ch in reference)
            {
                char c = char.ToUpperInvariant(ch);
                if (c < 'A' || c > 'Z') break;
                col = col * 26 + (c - 'A' + 1);
                letters++;
            }
            return letters == 0 ? fallback : col - 1;
        }

        private static int ParseInt(string s, int fallback) =>
            int.TryParse(s, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture,
                out int v) ? v : fallback;
    }
}
