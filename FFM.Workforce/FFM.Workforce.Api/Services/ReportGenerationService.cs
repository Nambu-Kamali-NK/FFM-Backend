using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace FFM.Workforce.Api.Services;

public static class ReportGenerationService
{
    public static byte[] GenerateCsv(IEnumerable<string> headers, IEnumerable<IEnumerable<string>> rows)
    {
        var sb = new StringBuilder();
        // UTF-8 BOM so Microsoft Excel automatically opens with UTF-8 encoding
        sb.Append('\uFEFF');

        // Headers
        sb.AppendLine(string.Join(",", headers.Select(EscapeCsv)));

        // Rows
        foreach (var row in rows)
        {
            sb.AppendLine(string.Join(",", row.Select(EscapeCsv)));
        }

        return Encoding.UTF8.GetBytes(sb.ToString());
    }

    private static string EscapeCsv(string? val)
    {
        if (val == null) return "\"\"";
        var escaped = val.Replace("\"", "\"\"");
        return $"\"{escaped}\"";
    }

    public static byte[] GenerateExcelXml(string sheetName, IEnumerable<string> headers, IEnumerable<IEnumerable<string>> rows)
    {
        var sb = new StringBuilder();
        sb.AppendLine("<?xml version=\"1.0\" encoding=\"utf-8\"?>");
        sb.AppendLine("<?mso-application progid=\"Excel.Sheet\"?>");
        sb.AppendLine("<Workbook xmlns=\"urn:schemas-microsoft-com:office:spreadsheet\"");
        sb.AppendLine(" xmlns:o=\"urn:schemas-microsoft-com:office:office\"");
        sb.AppendLine(" xmlns:x=\"urn:schemas-microsoft-com:office:excel\"");
        sb.AppendLine(" xmlns:ss=\"urn:schemas-microsoft-com:office:spreadsheet\"");
        sb.AppendLine(" xmlns:html=\"http://www.w3.org/TR/REC-html40\">");

        sb.AppendLine(" <Styles>");
        sb.AppendLine("  <Style ss:ID=\"Default\" ss:Name=\"Normal\"><Font ss:FontName=\"Segoe UI\" ss:Size=\"10\"/></Style>");
        sb.AppendLine("  <Style ss:ID=\"HeaderStyle\"><Font ss:FontName=\"Segoe UI\" ss:Size=\"11\" ss:Bold=\"1\" ss:Color=\"#FFFFFF\"/><Interior ss:Color=\"#1C585C\" ss:Pattern=\"Solid\"/><Alignment ss:Horizontal=\"Center\"/></Style>");
        sb.AppendLine("  <Style ss:ID=\"ZebraStyle\"><Interior ss:Color=\"#F8FAFB\" ss:Pattern=\"Solid\"/></Style>");
        sb.AppendLine(" </Styles>");

        sb.AppendLine($" <Worksheet ss:Name=\"{System.Security.SecurityElement.Escape(sheetName)}\">");
        sb.AppendLine("  <Table>");

        // Header Row
        sb.AppendLine("   <Row ss:Height=\"24\">");
        foreach (var h in headers)
        {
            sb.AppendLine($"    <Cell ss:StyleID=\"HeaderStyle\"><Data ss:Type=\"String\">{System.Security.SecurityElement.Escape(h)}</Data></Cell>");
        }
        sb.AppendLine("   </Row>");

        // Data Rows
        int rowIndex = 0;
        foreach (var r in rows)
        {
            var style = rowIndex % 2 == 1 ? " ss:StyleID=\"ZebraStyle\"" : "";
            sb.AppendLine($"   <Row{style}>");
            foreach (var cell in r)
            {
                sb.AppendLine($"    <Cell><Data ss:Type=\"String\">{System.Security.SecurityElement.Escape(cell ?? string.Empty)}</Data></Cell>");
            }
            sb.AppendLine("   </Row>");
            rowIndex++;
        }

        sb.AppendLine("  </Table>");
        sb.AppendLine(" </Worksheet>");
        sb.AppendLine("</Workbook>");

        return Encoding.UTF8.GetBytes(sb.ToString());
    }

    public static byte[] GeneratePdf(string title, string subtitle, IEnumerable<string> headers, IEnumerable<IEnumerable<string>> rows)
    {
        // Standard, pure C# PDF-1.4 generation with 0 external C++ or native runtime dependencies
        using var ms = new MemoryStream();
        using var writer = new StreamWriter(ms, Encoding.ASCII);

        var objects = new List<long>();

        // 1. PDF Header
        writer.Write("%PDF-1.4\n");
        writer.Flush();

        // Object 1: Catalog
        objects.Add(ms.Position);
        writer.Write("1 0 obj\n<< /Type /Catalog /Pages 2 0 R >>\nendobj\n");
        writer.Flush();

        // Object 2: Pages
        objects.Add(ms.Position);
        writer.Write("2 0 obj\n<< /Type /Pages /Kids [3 0 R] /Count 1 >>\nendobj\n");
        writer.Flush();

        // Object 4: Font Regular
        objects.Add(ms.Position);
        writer.Write("4 0 obj\n<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>\nendobj\n");
        writer.Flush();

        // Object 5: Font Bold
        objects.Add(ms.Position);
        writer.Write("5 0 obj\n<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica-Bold >>\nendobj\n");
        writer.Flush();

        // Stream Content (Content of Page)
        var streamContent = new StringBuilder();
        // Title banner
        streamContent.Append("BT /F2 18 Tf 50 740 Td (").Append(EscapePdf(title)).Append(") Tj ET\n");
        streamContent.Append("BT /F1 10 Tf 50 722 Td (").Append(EscapePdf(subtitle)).Append(" | Generated: ").Append(DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm UTC")).Append(") Tj ET\n");

        // Horizontal line
        streamContent.Append("50 712 m 562 712 l S\n");

        // Table Headers
        int y = 690;
        var headerList = headers.Take(5).ToList();
        streamContent.Append("BT /F2 9 Tf 50 ").Append(y).Append(" Td (").Append(EscapePdf(string.Join("   |   ", headerList))).Append(") Tj ET\n");
        y -= 8;
        streamContent.Append("50 ").Append(y).Append(" m 562 ").Append(y).Append(" l S\n");

        // Rows (up to 35 rows per page)
        foreach (var row in rows.Take(35))
        {
            y -= 16;
            if (y < 60) break;
            var rowSummary = string.Join("   |   ", row.Take(5).Select(c => c?.Length > 22 ? c.Substring(0, 19) + "..." : c));
            streamContent.Append("BT /F1 8 Tf 50 ").Append(y).Append(" Td (").Append(EscapePdf(rowSummary)).Append(") Tj ET\n");
        }

        // Footer
        streamContent.Append("50 45 m 562 45 l S\n");
        streamContent.Append("BT /F1 8 Tf 50 35 Td (Field Force Management \\(FFM\\) Platform — Official Confidential Export) Tj ET\n");
        streamContent.Append("BT /F1 8 Tf 500 35 Td (Page 1 of 1) Tj ET\n");

        var streamBytes = Encoding.ASCII.GetBytes(streamContent.ToString());

        // Object 6: Stream
        objects.Add(ms.Position);
        writer.Write($"6 0 obj\n<< /Length {streamBytes.Length} >>\nstream\n");
        writer.Flush();
        ms.Write(streamBytes, 0, streamBytes.Length);
        writer.Write("\nendstream\nendobj\n");
        writer.Flush();

        // Object 3: Page
        objects.Add(ms.Position);
        writer.Write("3 0 obj\n<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] /Resources << /Font << /F1 4 0 R /F2 5 0 R >> >> /Contents 6 0 R >>\nendobj\n");
        writer.Flush();

        // XRef table
        long xrefPos = ms.Position;
        writer.Write($"xref\n0 {objects.Count + 1}\n0000000000 65535 f \n");
        foreach (var pos in objects)
        {
            writer.Write($"{pos:D10} 00000 n \n");
        }
        writer.Write($"trailer\n<< /Size {objects.Count + 1} /Root 1 0 R >>\nstartxref\n{xrefPos}\n%%EOF\n");
        writer.Flush();

        return ms.ToArray();
    }

    private static string EscapePdf(string input)
    {
        if (string.IsNullOrEmpty(input)) return string.Empty;
        return input.Replace("\\", "\\\\").Replace("(", "\\(").Replace(")", "\\)");
    }
}
