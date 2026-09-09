using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Xml.Linq;
using LexPercent.Domain;
using PdfSharp.Drawing;
using PdfSharp.Fonts;
using PdfSharp.Pdf;
namespace LexPercent.Documents;

/// <summary>Offline OOXML and PDF exporters. Office is not required.</summary>
public static class Exporter
{
    static readonly XNamespace W = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";
    static readonly XNamespace S = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
    static readonly XNamespace R = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
    static readonly XNamespace P = "http://schemas.openxmlformats.org/package/2006/relationships";
    static string N(double v) => v.ToString(CultureInfo.InvariantCulture);
    static XElement V(string name, object value) => new(W + name, new XAttribute(W + "val", value));
    static void Entry(ZipArchive zip, string path, XElement xml) { using var st = zip.CreateEntry(path).Open(); new XDocument(new XDeclaration("1.0", "utf-8", "yes"), xml).Save(st); }
    static void Types(ZipArchive z, params (string Path, string Type)[] items)
    {
        XNamespace c = "http://schemas.openxmlformats.org/package/2006/content-types";
        Entry(z, "[Content_Types].xml", new(c + "Types", new XElement(c + "Default", new XAttribute("Extension", "rels"), new XAttribute("ContentType", "application/vnd.openxmlformats-package.relationships+xml")), new XElement(c + "Default", new XAttribute("Extension", "xml"), new XAttribute("ContentType", "application/xml")), items.Select(i => new XElement(c + "Override", new XAttribute("PartName", "/" + i.Path), new XAttribute("ContentType", i.Type)))));
    }
    static XElement Rel(string id, string type, string target) => new(P + "Relationship", new XAttribute("Id", id), new XAttribute("Type", R.NamespaceName + "/" + type), new XAttribute("Target", target));
    static XElement Para(string text, BlockStyle style, bool header = false, string? rightText = null, int tabWidth = 9800)
    {
        var props = new XElement(W + "pPr", new XElement(W + "spacing", new XAttribute(W + "before", (int)(style.Before * 20)), new XAttribute(W + "after", (int)(style.After * 20)), new XAttribute(W + "line", (int)(style.LineSpacing * 240)), new XAttribute(W + "lineRule", "auto")), new XElement(W + "ind", new XAttribute(W + "left", (int)(style.Indent * 20))));
        props.Add(V("jc", style.Align)); if (header) props.AddFirst(new XElement(W + "keepNext"));
        var rpr = new XElement(W + "rPr", new XElement(W + "rFonts", new XAttribute(W + "ascii", style.Font), new XAttribute(W + "hAnsi", style.Font)));
        if (style.Bold) rpr.Add(new XElement(W + "b")); if (style.Italic) rpr.Add(new XElement(W + "i"));
        rpr.Add(V("color", style.Color.TrimStart('#'))); rpr.Add(V("sz", (int)(style.Size * 2))); var run = new XElement(W + "r", rpr); var lines = text.Split('\n'); for (int i = 0; i < lines.Length; i++) { if (i > 0) run.Add(new XElement(W + "br")); run.Add(new XElement(W + "t", new XAttribute(XNamespace.Xml + "space", "preserve"), lines[i])); }
        if (rightText is not null)
        {
            props.AddFirst(new XElement(W + "tabs", new XElement(W + "tab", new XAttribute(W + "val", "right"), new XAttribute(W + "pos", tabWidth))));
            props.AddFirst(new XElement(W + "keepLines"));
            run.Add(new XElement(W + "tab"), new XElement(W + "t", rightText));
        }
        return new(W + "p", props, run);
    }
    public static void Docx(string path, Report report)
    {
        using var z = ZipFile.Open(path, ZipArchiveMode.Create);
        Types(z, ("word/document.xml", "application/vnd.openxmlformats-officedocument.wordprocessingml.document.main+xml"), ("word/footer.xml", "application/vnd.openxmlformats-officedocument.wordprocessingml.footer+xml"));
        Entry(z, "_rels/.rels", new(P + "Relationships", Rel("rId1", "officeDocument", "word/document.xml")));
        Entry(z, "word/_rels/document.xml.rels", new(P + "Relationships", Rel("rIdFooter", "footer", "footer.xml")));
        var body = new XElement(W + "body", report.Before.Select(b => Para(b.Text, b.Style)));
        var table = new XElement(W + "tbl", new XElement(W + "tblPr", new XElement(W + "tblW", new XAttribute(W + "w", 5000), new XAttribute(W + "type", "pct")), new XElement(W + "tblBorders", new[] { "top", "left", "bottom", "right", "insideH", "insideV" }.Select(n => new XElement(W + n, new XAttribute(W + "val", "single"), new XAttribute(W + "sz", 4)))), new XElement(W + "tblLayout", new XAttribute(W + "type", "fixed"))));
        int pageWidth = report.Options.Landscape ? 16838 : 11906; int margin = (int)(report.Options.MarginMm * 56.6929); int width = pageWidth - 2 * margin; double[] ratios = [.31, .23, .15, .09, .22];
        table.Add(new XElement(W + "tblGrid", ratios.Select(v => new XElement(W + "gridCol", new XAttribute(W + "w", (int)(width * v))))));
        foreach (var row in report.Rows)
        {
            var tr = new XElement(W + "tr", new XElement(W + "trPr", new XElement(W + "cantSplit"), row.Header ? new XElement(W + "tblHeader") : null));
            for (int i = 0; i < 5; i++) tr.Add(new XElement(W + "tc", new XElement(W + "tcPr", new XElement(W + "tcW", new XAttribute(W + "w", (int)(width * ratios[i])), new XAttribute(W + "type", "dxa"))), Para(row.Cells[i], report.Options.Table with { Bold = row.Header || row.Cells[0] == "ИТОГО", Align = i == 0 ? "left" : "right", Before = 3, After = 3 })));
            table.Add(tr);
        }
        body.Add(table); body.Add(report.After.Select(b => Para(b.Text, b.Style, rightText: b.RightText, tabWidth: width - (int)(b.Style.Indent * 20))));
        body.Add(new XElement(W + "sectPr", new XElement(W + "footerReference", new XAttribute(W + "type", "default"), new XAttribute(R + "id", "rIdFooter")), new XElement(W + "pgSz", new XAttribute(W + "w", pageWidth), new XAttribute(W + "h", report.Options.Landscape ? 11906 : 16838), new XAttribute(W + "orient", report.Options.Landscape ? "landscape" : "portrait")), new XElement(W + "pgMar", new[] { "top", "bottom", "left", "right" }.Select(n => new XAttribute(W + n, margin)), new XAttribute(W + "header", 360), new XAttribute(W + "footer", 360))));
        Entry(z, "word/document.xml", new(W + "document", new XAttribute(XNamespace.Xmlns + "w", W), new XAttribute(XNamespace.Xmlns + "r", R), body));
        Entry(z, "word/footer.xml", new(W + "ftr", new XElement(W + "p", new XElement(W + "pPr", V("jc", "center")), new XElement(W + "fldSimple", new XAttribute(W + "instr", "PAGE")))));
    }
    public static void Xlsx(string path, Report report)
    {
        using var z = ZipFile.Open(path, ZipArchiveMode.Create);
        Types(z, ("xl/workbook.xml", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml"), ("xl/worksheets/sheet1.xml", "application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml"), ("xl/styles.xml", "application/vnd.openxmlformats-officedocument.spreadsheetml.styles+xml"));
        Entry(z, "_rels/.rels", new(P + "Relationships", Rel("rId1", "officeDocument", "xl/workbook.xml")));
        Entry(z, "xl/_rels/workbook.xml.rels", new(P + "Relationships", Rel("rId1", "worksheet", "worksheets/sheet1.xml"), Rel("rId2", "styles", "styles.xml")));
        var data = new XElement(S + "sheetData"); var merges = new List<string>(); int n = 0;
        var styles = new List<(BlockStyle Style, int Number, bool Border)>();
        int StyleId(BlockStyle style, int number = 0, bool border = false) { var key = (style, number, border); int found = styles.IndexOf(key); if (found >= 0) return found; styles.Add(key); return styles.Count - 1; }
        StyleId(report.Options.Body);
        void Row(object[] vals, BlockStyle style, bool merge = false)
        {
            n++; var r = new XElement(S + "row", new XAttribute("r", n));
            if (merge) { r.Add(new XAttribute("ht", Math.Max(22, vals[0].ToString()!.Split('\n').Length * 16 + Math.Ceiling(vals[0].ToString()!.Length / 95.0) * 14)), new XAttribute("customHeight", 1)); merges.Add($"A{n}:E{n}"); }
            for (int i = 0; i < vals.Length; i++)
            {
                bool numeric = vals[i] is decimal or int; var c = new XElement(S + "c", new XAttribute("r", $"{(char)('A' + i)}{n}"), new XAttribute("s", StyleId(style, numeric ? (vals[i] is int ? 1 : 4) : 0, !merge)));
                if (numeric) c.Add(new XElement(S + "v", Convert.ToString(vals[i], CultureInfo.InvariantCulture)));
                else c.Add(new XAttribute("t", "inlineStr"), new XElement(S + "is", new XElement(S + "t", new XAttribute(XNamespace.Xml + "space", "preserve"), vals[i].ToString())));
                r.Add(c);
            }
            data.Add(r);
        }
        foreach (var b in report.Before) Row([b.Text], b.Style, true);
        int header = n + 1;
        foreach (var r in report.Rows) Row(r.Values ?? r.Cells.Cast<object>().ToArray(), report.Options.Table with { Bold = r.Header || r.Cells[0] == "ИТОГО" });
        foreach (var b in report.After)
        {
            if (b.RightText is null) { Row([b.Text], b.Style, true); continue; }
            var lines = b.Text.Split('\n');
            foreach (var line in lines[..^1]) Row([line], b.Style, true);
            Row([lines[^1], "", "", b.RightText, ""], b.Style);
            merges.Add($"A{n}:C{n}"); merges.Add($"D{n}:E{n}");
            var cells = data.Elements().Last().Elements(S + "c").ToArray();
            foreach (var cell in cells) cell.SetAttributeValue("s", StyleId(b.Style, 0, false));
            cells[3].SetAttributeValue("s", StyleId(b.Style with { Align = "right" }, 0, false));
        }
        var ws = new XElement(S + "worksheet", new XElement(S + "sheetPr", new XElement(S + "pageSetUpPr", new XAttribute("fitToPage", 1))), new XElement(S + "cols", new[] { 31, 24, 16, 9, 22 }.Select((w, i) => new XElement(S + "col", new XAttribute("min", i + 1), new XAttribute("max", i + 1), new XAttribute("width", w), new XAttribute("customWidth", 1)))), data, new XElement(S + "mergeCells", merges.Select(m => new XElement(S + "mergeCell", new XAttribute("ref", m)))), new XElement(S + "pageMargins", new XAttribute("left", N(report.Options.MarginMm / 25.4)), new XAttribute("right", N(report.Options.MarginMm / 25.4)), new XAttribute("top", N(report.Options.MarginMm / 25.4)), new XAttribute("bottom", N(report.Options.MarginMm / 25.4)), new XAttribute("header", .2), new XAttribute("footer", .2)), new XElement(S + "pageSetup", new XAttribute("paperSize", 9), new XAttribute("orientation", report.Options.Landscape ? "landscape" : "portrait"), new XAttribute("fitToWidth", 1), new XAttribute("fitToHeight", 0)), new XElement(S + "headerFooter", new XElement(S + "oddFooter", "&C&P / &N")));
        Entry(z, "xl/worksheets/sheet1.xml", ws);
        Entry(z, "xl/workbook.xml", new(S + "workbook", new XAttribute(XNamespace.Xmlns + "r", R), new XElement(S + "sheets", new XElement(S + "sheet", new XAttribute("name", "Расчет"), new XAttribute("sheetId", 1), new XAttribute(R + "id", "rId1"))), new XElement(S + "definedNames", new XElement(S + "definedName", new XAttribute("name", "_xlnm.Print_Area"), new XAttribute("localSheetId", 0), $"'Расчет'!$A$1:$E${n}"), new XElement(S + "definedName", new XAttribute("name", "_xlnm.Print_Titles"), new XAttribute("localSheetId", 0), $"'Расчет'!${header}:${header}"))));
        XElement Font(BlockStyle st) => new(S + "font", st.Bold ? new XElement(S + "b") : null, st.Italic ? new XElement(S + "i") : null, new XElement(S + "sz", new XAttribute("val", st.Size)), new XElement(S + "color", new XAttribute("rgb", "FF" + st.Color.TrimStart('#'))), new XElement(S + "name", new XAttribute("val", st.Font)));
        XElement Xf(int i, BlockStyle st, int num, bool border) => new(S + "xf", new XAttribute("numFmtId", num), new XAttribute("fontId", i), new XAttribute("fillId", 0), new XAttribute("borderId", border ? 1 : 0), new XAttribute("xfId", 0), new XAttribute("applyAlignment", 1), new XElement(S + "alignment", new XAttribute("horizontal", num != 0 ? "right" : st.Align), new XAttribute("vertical", "center"), new XAttribute("wrapText", 1)));
        Entry(z, "xl/styles.xml", new(S + "styleSheet",
            new XElement(S + "fonts", new XAttribute("count", styles.Count), styles.Select(x => Font(x.Style))),
            new XElement(S + "fills", new XAttribute("count", 2), new XElement(S + "fill", new XElement(S + "patternFill", new XAttribute("patternType", "none"))), new XElement(S + "fill", new XElement(S + "patternFill", new XAttribute("patternType", "gray125")))),
            new XElement(S + "borders", new XAttribute("count", 2), new XElement(S + "border"), new XElement(S + "border", new[] { "left", "right", "top", "bottom" }.Select(x => new XElement(S + x, new XAttribute("style", "thin"))))),
            new XElement(S + "cellStyleXfs", new XAttribute("count", 1), Xf(0, report.Options.Body, 0, false)),
            new XElement(S + "cellXfs", new XAttribute("count", styles.Count), styles.Select((x, i) => Xf(i, x.Style, x.Number, x.Border))),
            new XElement(S + "cellStyles", new XAttribute("count", 1), new XElement(S + "cellStyle", new XAttribute("name", "Normal"), new XAttribute("xfId", 0), new XAttribute("builtinId", 0)))));
    }

    public static void Pdf(string path, Report report)
    {
        GlobalFontSettings.FontResolver ??= new WindowsFontResolver();
        using var pdf = new PdfDocument(); pdf.Info.Title = "LexPercent Pro — расчет";
        XGraphics? g = null; double y = 0, width = 0, height = 0, margin = report.Options.MarginMm * 72 / 25.4;
        void Page() { g?.Dispose(); var p = pdf.AddPage(); p.Size = PdfSharp.PageSize.A4; p.Orientation = report.Options.Landscape ? PdfSharp.PageOrientation.Landscape : PdfSharp.PageOrientation.Portrait; g = XGraphics.FromPdfPage(p); width = p.Width.Point - 2 * margin; height = p.Height.Point; y = margin; }
        Page();
        XFont Font(BlockStyle st) => new(st.Font, st.Size, (st.Bold ? XFontStyleEx.Bold : XFontStyleEx.Regular) | (st.Italic ? XFontStyleEx.Italic : XFontStyleEx.Regular));
        string[] Wrap(string text, XFont font, double w)
        {
            var lines = new List<string>(); foreach (var paragraph in text.Split('\n')) { string line = ""; foreach (var word in paragraph.Split(' ')) { var next = line.Length == 0 ? word : line + " " + word; if (g!.MeasureString(next, font).Width > w && line.Length > 0) { lines.Add(line); line = word; } else line = next; } lines.Add(line); }
            return lines.ToArray();
        }
        void Text(ReportBlock block)
        {
            var st = block.Style; var f = Font(st); var line = st.Size * st.LineSpacing + 2;
            if (block.RightText is not null)
            {
                var paragraphs = block.Text.Split('\n');
                var lines = paragraphs[..^1].SelectMany(t => Wrap(t, f, width - st.Indent)).ToList();
                double rightWidth = g!.MeasureString(block.RightText, f).Width;
                lines.AddRange(Wrap(paragraphs[^1], f, Math.Max(40, width - st.Indent - rightWidth - 24)));
                if (y + st.Before + lines.Count * line + st.After > height - margin - 20) Page();
                y += st.Before;
                var brush = new XSolidBrush(XColor.FromArgb(unchecked((int)(0xff000000u | Convert.ToUInt32(st.Color.TrimStart('#'), 16)))));
                for (int i = 0; i < lines.Count; i++)
                {
                    if (y + line > height - margin - 20) Page();
                    g!.DrawString(lines[i], f, brush, new XPoint(margin + st.Indent, y + st.Size));
                    if (i == lines.Count - 1) g.DrawString(block.RightText, f, brush, new XPoint(margin + width - rightWidth, y + st.Size));
                    y += line;
                }
                y += st.After; return;
            }
            y += st.Before;
            foreach (var t in Wrap(block.Text, f, width - st.Indent)) { if (y + line > height - margin - 20) Page(); double tw = g!.MeasureString(t, f).Width; double x = margin + st.Indent + (st.Align == "center" ? (width - st.Indent - tw) / 2 : st.Align == "right" ? width - st.Indent - tw : 0); g.DrawString(t, f, new XSolidBrush(XColor.FromArgb(unchecked((int)(0xff000000u | Convert.ToUInt32(st.Color.TrimStart('#'), 16))))), new XPoint(x, y + st.Size)); y += line; }
            y += st.After;
        }
        foreach (var b in report.Before) Text(b);
        double[] ratios = [.31, .23, .15, .09, .22];
        void TableRow(ReportRow row)
        {
            var st = report.Options.Table with { Bold = row.Header || row.Cells[0] == "ИТОГО" }; var font = Font(st); var lines = row.Cells.Select((t, i) => Wrap(t, font, width * ratios[i] - 8)).ToArray(); double lineHeight = st.Size * 1.2; double rh = lines.Max(x => x.Length) * lineHeight + 8;
            if (y + rh > height - margin - 20) { Page(); if (!row.Header) TableRow(report.Rows[0]); }
            double x = margin;
            for (int i = 0; i < 5; i++) { double w = width * ratios[i]; g!.DrawRectangle(new XPen(XColors.Gray, .4), x, y, w, rh); for (int j = 0; j < lines[i].Length; j++) { var t = lines[i][j]; double tx = i == 0 ? x + 4 : x + w - 4 - g.MeasureString(t, font).Width; g.DrawString(t, font, new XSolidBrush(XColor.FromArgb(unchecked((int)(0xff000000u | Convert.ToUInt32(st.Color.TrimStart('#'), 16))))), new XPoint(tx, y + 4 + st.Size + j * lineHeight)); } x += w; }
            y += rh;
        }
        foreach (var row in report.Rows) TableRow(row);
        y += 8; foreach (var b in report.After) Text(b); g?.Dispose();
        for (int i = 0; i < pdf.Pages.Count; i++) { using var footer = XGraphics.FromPdfPage(pdf.Pages[i], XGraphicsPdfPageOptions.Append); footer.DrawString($"{i + 1} / {pdf.Pages.Count}", new XFont("Arial", 9), XBrushes.Gray, new XRect(0, pdf.Pages[i].Height.Point - 25, pdf.Pages[i].Width.Point, 15), XStringFormats.Center); }
        pdf.Save(path);
    }
    public static void Export(string path, Report report)
    {
        var ext = Path.GetExtension(path).ToLowerInvariant(); var temp = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try { switch (ext) { case ".pdf": Pdf(temp, report); break; case ".docx": Docx(temp, report); break; case ".xlsx": Xlsx(temp, report); break; default: throw new ArgumentException("Выберите PDF, DOCX или XLSX."); } File.Move(temp, path, true); } finally { if (File.Exists(temp)) File.Delete(temp); }
    }
}
internal sealed class WindowsFontResolver : IFontResolver
{
    public FontResolverInfo ResolveTypeface(string familyName, bool bold, bool italic)
    {
        string family = familyName.ToLowerInvariant(); string file = family.Contains("times") ? "times" : family.Contains("courier") ? "cour" : family.Contains("segoe") ? "segoeui" : "arial";
        string suffix = file == "segoeui" ? (bold ? (italic ? "z" : "b") : (italic ? "i" : "")) : (bold ? (italic ? "bi" : "bd") : (italic ? "i" : ""));
        return new(file + suffix + ".ttf");
    }
    public byte[] GetFont(string faceName) => File.ReadAllBytes(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Fonts), faceName));
}
