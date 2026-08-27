using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security;
using System.Text;

namespace N8sIPScanner;

public static class ExcelExporter
{
    public static void Save(
        string filePath,
        IEnumerable<ScanResult> results,
        string scanDescription)
    {
        var ordered = results
            .OrderBy(r => IPv4SortKey(r.IPAddress))
            .ToList();

        if (File.Exists(filePath))
        {
            File.Delete(filePath);
        }

        using var archive = ZipFile.Open(filePath, ZipArchiveMode.Create);

        WriteEntry(archive, "[Content_Types].xml", ContentTypesXml());
        WriteEntry(archive, "_rels/.rels", RootRelationshipsXml());
        WriteEntry(archive, "xl/workbook.xml", WorkbookXml());
        WriteEntry(archive, "xl/_rels/workbook.xml.rels", WorkbookRelationshipsXml());
        WriteEntry(archive, "xl/styles.xml", StylesXml());
        WriteEntry(archive, "xl/worksheets/sheet1.xml", WorksheetXml(ordered, scanDescription));
    }

    private static string WorksheetXml(IReadOnlyList<ScanResult> results, string scanDescription)
    {
        var lastRow = results.Count + 4;
        var sb = new StringBuilder();

        sb.Append(@"<?xml version=""1.0"" encoding=""UTF-8"" standalone=""yes""?>");
        sb.Append(@"<worksheet xmlns=""http://schemas.openxmlformats.org/spreadsheetml/2006/main"">");
        sb.Append($@"<dimension ref=""A1:G{lastRow}""/>");
        sb.Append(@"<sheetViews><sheetView workbookViewId=""0""><pane ySplit=""4"" topLeftCell=""A5"" activePane=""bottomLeft"" state=""frozen""/></sheetView></sheetViews>");
        sb.Append(@"<cols>");
        sb.Append(@"<col min=""1"" max=""1"" width=""17"" customWidth=""1""/>");
        sb.Append(@"<col min=""2"" max=""2"" width=""30"" customWidth=""1""/>");
        sb.Append(@"<col min=""3"" max=""3"" width=""20"" customWidth=""1""/>");
        sb.Append(@"<col min=""4"" max=""4"" width=""28"" customWidth=""1""/>");
        sb.Append(@"<col min=""5"" max=""5"" width=""18"" customWidth=""1""/>");
        sb.Append(@"<col min=""6"" max=""6"" width=""12"" customWidth=""1""/>");
        sb.Append(@"<col min=""7"" max=""7"" width=""38"" customWidth=""1""/>");
        sb.Append(@"</cols>");
        sb.Append("<sheetData>");

        sb.Append(@"<row r=""1"" ht=""24"" customHeight=""1"">");
        sb.Append(Cell("A1", "N8s IP Scanner Results", 2));
        sb.Append("</row>");

        var metadata = $"Scan: {scanDescription} | Exported: {DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss zzz}";
        sb.Append(@"<row r=""2"">");
        sb.Append(Cell("A2", metadata, 0));
        sb.Append("</row>");

        sb.Append(@"<row r=""4"" ht=""20"" customHeight=""1"">");
        var headers = new[] { "IP Address", "Hostname", "MAC Address", "Manufacturer", "Status", "Web UI", "Web URL" };
        for (var i = 0; i < headers.Length; i++)
        {
            sb.Append(Cell(ColumnName(i + 1) + "4", headers[i], 1));
        }
        sb.Append("</row>");

        for (var i = 0; i < results.Count; i++)
        {
            var rowNumber = i + 5;
            var result = results[i];

            sb.Append($@"<row r=""{rowNumber}"">");
            sb.Append(Cell($"A{rowNumber}", Clean(result.IPAddress), 0));
            sb.Append(Cell($"B{rowNumber}", Clean(result.Hostname), 0));
            sb.Append(Cell($"C{rowNumber}", Clean(result.MacAddress), 0));
            sb.Append(Cell($"D{rowNumber}", Clean(result.Manufacturer), 0));
            sb.Append(Cell($"E{rowNumber}", Clean(result.Status), 0));
            sb.Append(Cell($"F{rowNumber}", result.HasWebUi ? "Yes" : "No", 0));
            sb.Append(Cell($"G{rowNumber}", result.HasWebUi ? result.PreferredUrl : "", 0));
            sb.Append("</row>");
        }

        sb.Append("</sheetData>");
        sb.Append(@"<mergeCells count=""2""><mergeCell ref=""A1:G1""/><mergeCell ref=""A2:G2""/></mergeCells>");
        sb.Append($@"<autoFilter ref=""A4:G{Math.Max(4, lastRow)}""/>");
        sb.Append("</worksheet>");

        return sb.ToString();
    }

    private static string Cell(string reference, string value, int style)
    {
        var escaped = SecurityElement.Escape(value) ?? "";
        return $@"<c r=""{reference}"" t=""inlineStr"" s=""{style}""><is><t xml:space=""preserve"">{escaped}</t></is></c>";
    }

    private static string Clean(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? "" : value.Trim();
    }

    private static string ColumnName(int column)
    {
        var name = "";
        while (column > 0)
        {
            column--;
            name = (char)('A' + (column % 26)) + name;
            column /= 26;
        }

        return name;
    }

    private static long IPv4SortKey(string ipAddress)
    {
        var parts = ipAddress.Split('.');
        if (parts.Length != 4)
        {
            return long.MaxValue;
        }

        long value = 0;
        foreach (var part in parts)
        {
            if (!int.TryParse(part, NumberStyles.Integer, CultureInfo.InvariantCulture, out var octet) ||
                octet < 0 ||
                octet > 255)
            {
                return long.MaxValue;
            }

            value = (value << 8) + octet;
        }

        return value;
    }

    private static void WriteEntry(ZipArchive archive, string path, string content)
    {
        var entry = archive.CreateEntry(path, CompressionLevel.Optimal);
        using var stream = entry.Open();
        using var writer = new StreamWriter(stream, new UTF8Encoding(false));
        writer.Write(content);
    }

    private static string ContentTypesXml() =>
        @"<?xml version=""1.0"" encoding=""UTF-8"" standalone=""yes""?>
<Types xmlns=""http://schemas.openxmlformats.org/package/2006/content-types"">
  <Default Extension=""rels"" ContentType=""application/vnd.openxmlformats-package.relationships+xml""/>
  <Default Extension=""xml"" ContentType=""application/xml""/>
  <Override PartName=""/xl/workbook.xml"" ContentType=""application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml""/>
  <Override PartName=""/xl/worksheets/sheet1.xml"" ContentType=""application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml""/>
  <Override PartName=""/xl/styles.xml"" ContentType=""application/vnd.openxmlformats-officedocument.spreadsheetml.styles+xml""/>
</Types>";

    private static string RootRelationshipsXml() =>
        @"<?xml version=""1.0"" encoding=""UTF-8"" standalone=""yes""?>
<Relationships xmlns=""http://schemas.openxmlformats.org/package/2006/relationships"">
  <Relationship Id=""rId1"" Type=""http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument"" Target=""xl/workbook.xml""/>
</Relationships>";

    private static string WorkbookXml() =>
        @"<?xml version=""1.0"" encoding=""UTF-8"" standalone=""yes""?>
<workbook xmlns=""http://schemas.openxmlformats.org/spreadsheetml/2006/main"" xmlns:r=""http://schemas.openxmlformats.org/officeDocument/2006/relationships"">
  <sheets>
    <sheet name=""Scan Results"" sheetId=""1"" r:id=""rId1""/>
  </sheets>
</workbook>";

    private static string WorkbookRelationshipsXml() =>
        @"<?xml version=""1.0"" encoding=""UTF-8"" standalone=""yes""?>
<Relationships xmlns=""http://schemas.openxmlformats.org/package/2006/relationships"">
  <Relationship Id=""rId1"" Type=""http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet"" Target=""worksheets/sheet1.xml""/>
  <Relationship Id=""rId2"" Type=""http://schemas.openxmlformats.org/officeDocument/2006/relationships/styles"" Target=""styles.xml""/>
</Relationships>";

    private static string StylesXml() =>
        @"<?xml version=""1.0"" encoding=""UTF-8"" standalone=""yes""?>
<styleSheet xmlns=""http://schemas.openxmlformats.org/spreadsheetml/2006/main"">
  <fonts count=""3"">
    <font><sz val=""11""/><name val=""Calibri""/><family val=""2""/></font>
    <font><b/><color rgb=""FFFFFFFF""/><sz val=""11""/><name val=""Calibri""/><family val=""2""/></font>
    <font><b/><sz val=""14""/><name val=""Calibri""/><family val=""2""/></font>
  </fonts>
  <fills count=""3"">
    <fill><patternFill patternType=""none""/></fill>
    <fill><patternFill patternType=""gray125""/></fill>
    <fill><patternFill patternType=""solid""><fgColor rgb=""FF1F497D""/><bgColor indexed=""64""/></patternFill></fill>
  </fills>
  <borders count=""1""><border><left/><right/><top/><bottom/><diagonal/></border></borders>
  <cellStyleXfs count=""1""><xf numFmtId=""0"" fontId=""0"" fillId=""0"" borderId=""0""/></cellStyleXfs>
  <cellXfs count=""3"">
    <xf numFmtId=""0"" fontId=""0"" fillId=""0"" borderId=""0"" xfId=""0""/>
    <xf numFmtId=""0"" fontId=""1"" fillId=""2"" borderId=""0"" xfId=""0"" applyFont=""1"" applyFill=""1""/>
    <xf numFmtId=""0"" fontId=""2"" fillId=""0"" borderId=""0"" xfId=""0"" applyFont=""1""/>
  </cellXfs>
  <cellStyles count=""1""><cellStyle name=""Normal"" xfId=""0"" builtinId=""0""/></cellStyles>
</styleSheet>";
}
