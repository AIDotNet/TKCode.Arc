using System.Globalization;
using System.Net;
using System.Text;

namespace TKCode.Arc;

/// <summary>
/// Clipboard formats that Excel, Google Sheets and Numbers read and write: tab separated text with quoted cells, and an
/// HTML table. Pure functions, usable on any thread.
/// </summary>
public static class DataGridClipboardText
{
    /// <summary>Tab separated text; cells with tabs, line breaks or quotes are quoted with doubled quotes.</summary>
    public static string ToTsv(IReadOnlyList<IReadOnlyList<string>> matrix)
    {
        var builder = new StringBuilder();
        for (var r = 0; r < matrix.Count; r++)
        {
            if (r > 0)
            {
                builder.Append('\n');
            }
            var row = matrix[r];
            for (var c = 0; c < row.Count; c++)
            {
                if (c > 0)
                {
                    builder.Append('\t');
                }
                var cell = row[c] ?? string.Empty;
                if (cell.AsSpan().IndexOfAny('\t', '\n', '\r') >= 0 || cell.Contains('"'))
                {
                    builder.Append('"').Append(cell.Replace("\"", "\"\"")).Append('"');
                }
                else
                {
                    builder.Append(cell);
                }
            }
        }
        return builder.ToString();
    }

    /// <summary>Reads tab separated text (quoted cells may hold tabs and line breaks); a final line break is ignored.</summary>
    public static List<List<string>> ParseTsv(string? text)
    {
        var rows = new List<List<string>>();
        var s = (text ?? string.Empty).Replace("\r\n", "\n").Replace('\r', '\n');
        if (s.EndsWith('\n'))
        {
            s = s[..^1];
        }
        var row = new List<string>();
        var cell = new StringBuilder();
        var quoted = false;
        var i = 0;
        while (i < s.Length)
        {
            var ch = s[i];
            if (quoted)
            {
                if (ch == '"')
                {
                    if (i + 1 < s.Length && s[i + 1] == '"')
                    {
                        cell.Append('"');
                        i += 2;
                        continue;
                    }
                    quoted = false;
                    i++;
                    continue;
                }
                cell.Append(ch);
                i++;
                continue;
            }
            switch (ch)
            {
                case '"' when cell.Length == 0:
                    quoted = true;
                    break;
                case '\t':
                    row.Add(cell.ToString());
                    cell.Clear();
                    break;
                case '\n':
                    row.Add(cell.ToString());
                    rows.Add(row);
                    row = new List<string>();
                    cell.Clear();
                    break;
                default:
                    cell.Append(ch);
                    break;
            }
            i++;
        }
        row.Add(cell.ToString());
        rows.Add(row);
        return rows;
    }

    /// <summary>An HTML table of the matrix (cells escaped, line breaks as &lt;br&gt;), as spreadsheets put on the clipboard.</summary>
    public static string ToHtml(IReadOnlyList<IReadOnlyList<string>> matrix)
    {
        var builder = new StringBuilder("<meta charset=\"utf-8\"><table><tbody>");
        foreach (var row in matrix)
        {
            builder.Append("<tr>");
            foreach (var cell in row)
            {
                builder.Append("<td>").Append(Escape(cell ?? string.Empty).Replace("\n", "<br>")).Append("</td>");
            }
            builder.Append("</tr>");
        }
        builder.Append("</tbody></table>");
        return builder.ToString();
    }

    /// <summary>
    /// Reads the first table of an HTML fragment: one list per <c>tr</c>, one string per <c>td</c> / <c>th</c> (with
    /// <c>colspan</c> padding), whitespace collapsed as browsers do, <c>br</c> as a line break, entities decoded.
    /// Null when there is no table.
    /// </summary>
    public static List<List<string>>? ParseHtmlTable(string? html)
    {
        if (string.IsNullOrEmpty(html))
        {
            return null;
        }
        var start = html.IndexOf("<table", StringComparison.OrdinalIgnoreCase);
        if (start < 0)
        {
            return null;
        }
        var rows = new List<List<string>>();
        List<string>? row = null;
        StringBuilder? cell = null;
        var colspan = 1;
        var depth = 0;
        var i = start;
        while (i < html.Length)
        {
            if (html[i] == '<')
            {
                if (string.CompareOrdinal(html, i, "<!--", 0, 4) == 0)
                {
                    var end = html.IndexOf("-->", i + 4, StringComparison.Ordinal);
                    i = end < 0 ? html.Length : end + 3;
                    continue;
                }
                var close = html.IndexOf('>', i + 1);
                if (close < 0)
                {
                    break;
                }
                var tag = html.AsSpan(i + 1, close - i - 1).Trim();
                var closing = tag.Length > 0 && tag[0] == '/';
                if (closing)
                {
                    tag = tag[1..].TrimStart();
                }
                var nameEnd = 0;
                while (nameEnd < tag.Length && (char.IsLetterOrDigit(tag[nameEnd]) || tag[nameEnd] == ':'))
                {
                    nameEnd++;
                }
                var name = tag[..nameEnd].ToString().ToLowerInvariant();
                i = close + 1;
                if (name is "style" or "script" && !closing)
                {
                    var end = html.IndexOf("</" + name, i, StringComparison.OrdinalIgnoreCase);
                    i = end < 0 ? html.Length : html.IndexOf('>', end) + 1;
                    if (i <= 0)
                    {
                        i = html.Length;
                    }
                    continue;
                }
                switch (name)
                {
                    case "table":
                        depth += closing ? -1 : 1;
                        if (depth == 0)
                        {
                            FinishCell(row, ref cell, colspan);
                            if (row is not null)
                            {
                                rows.Add(row);
                            }
                            return rows.Count > 0 ? rows : null;
                        }
                        break;
                    case "tr" when depth == 1:
                        FinishCell(row, ref cell, colspan);
                        if (row is not null)
                        {
                            rows.Add(row);
                        }
                        row = closing ? null : new List<string>();
                        break;
                    case "td" or "th" when depth == 1:
                        FinishCell(row, ref cell, colspan);
                        if (!closing)
                        {
                            row ??= new List<string>();
                            cell = new StringBuilder();
                            colspan = ReadColspan(tag);
                        }
                        break;
                    case "br":
                        cell?.Append('\n');
                        break;
                    case "p" or "div" when closing && cell is { Length: > 0 }:
                        cell.Append('\n');
                        break;
                }
                continue;
            }
            var next = html.IndexOf('<', i);
            if (next < 0)
            {
                next = html.Length;
            }
            if (cell is not null)
            {
                AppendText(cell, html.AsSpan(i, next - i));
            }
            i = next;
        }
        FinishCell(row, ref cell, colspan);
        if (row is not null)
        {
            rows.Add(row);
        }
        return rows.Count > 0 ? rows : null;
    }

    /// <summary>Wraps an HTML fragment in the Windows clipboard's CF_HTML header (byte offsets in UTF-8).</summary>
    public static string WrapCfHtml(string fragment)
    {
        const string header = "Version:0.9\r\nStartHTML:{0:D10}\r\nEndHTML:{1:D10}\r\nStartFragment:{2:D10}\r\nEndFragment:{3:D10}\r\n";
        const string prefix = "<html><body>\r\n<!--StartFragment-->";
        const string suffix = "<!--EndFragment-->\r\n</body></html>";
        var headerLength = Encoding.UTF8.GetByteCount(string.Format(CultureInfo.InvariantCulture, header, 0, 0, 0, 0));
        var startHtml = headerLength;
        var startFragment = startHtml + Encoding.UTF8.GetByteCount(prefix);
        var endFragment = startFragment + Encoding.UTF8.GetByteCount(fragment);
        var endHtml = endFragment + Encoding.UTF8.GetByteCount(suffix);
        return string.Format(CultureInfo.InvariantCulture, header, startHtml, endHtml, startFragment, endFragment) + prefix + fragment + suffix;
    }

    /// <summary>The HTML inside a CF_HTML clipboard payload (the text is returned unchanged when it has no header).</summary>
    public static string UnwrapCfHtml(string payload)
    {
        if (!payload.StartsWith("Version:", StringComparison.Ordinal))
        {
            return payload;
        }
        var start = payload.IndexOf('<');
        return start < 0 ? string.Empty : payload[start..];
    }

    private static int ReadColspan(ReadOnlySpan<char> tag)
    {
        var text = tag.ToString();
        var at = text.IndexOf("colspan", StringComparison.OrdinalIgnoreCase);
        if (at < 0)
        {
            return 1;
        }
        var digits = new StringBuilder();
        for (var i = at + 7; i < text.Length; i++)
        {
            var ch = text[i];
            if (char.IsDigit(ch))
            {
                digits.Append(ch);
            }
            else if (digits.Length > 0)
            {
                break;
            }
            else if (ch is not ('=' or '"' or '\'' or ' '))
            {
                break;
            }
        }
        return int.TryParse(digits.ToString(), NumberStyles.None, CultureInfo.InvariantCulture, out var span) ? Math.Clamp(span, 1, 1000) : 1;
    }

    private static void AppendText(StringBuilder cell, ReadOnlySpan<char> raw)
    {
        var decoded = WebUtility.HtmlDecode(raw.ToString()).Replace(' ', ' ');
        foreach (var ch in decoded)
        {
            if (ch is ' ' or '\t' or '\r' or '\n')
            {
                if (cell.Length > 0 && cell[^1] is not (' ' or '\n'))
                {
                    cell.Append(' ');
                }
                continue;
            }
            cell.Append(ch);
        }
    }

    private static void FinishCell(List<string>? row, ref StringBuilder? cell, int colspan)
    {
        if (cell is null || row is null)
        {
            cell = null;
            return;
        }
        var lines = cell.ToString().Split('\n').Select(l => l.Trim());
        row.Add(string.Join("\n", lines).Trim('\n'));
        for (var i = 1; i < colspan; i++)
        {
            row.Add(string.Empty);
        }
        cell = null;
    }

    private static string Escape(string text) =>
        text.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;").Replace("\"", "&quot;");
}

/// <summary>RFC 4180 CSV with a byte order mark, so Excel reads UTF-8. Pure functions, usable on any thread.</summary>
public static class DataGridCsv
{
    /// <summary>
    /// CSV text: a header line, then one line per row, CRLF line breaks. Numbers are written with an invariant decimal
    /// point and no grouping, days as ISO (2026-01-31), and text as is (quoted when it holds commas, quotes or line breaks).
    /// </summary>
    public static string ToCsv(IReadOnlyList<string> headers, IEnumerable<IReadOnlyList<object?>> rows)
    {
        var builder = new StringBuilder();
        builder.AppendJoin(',', headers.Select(Quote));
        foreach (var row in rows)
        {
            builder.Append("\r\n");
            for (var c = 0; c < row.Count; c++)
            {
                if (c > 0)
                {
                    builder.Append(',');
                }
                builder.Append(Quote(Raw(row[c])));
            }
        }
        return builder.ToString();
    }

    /// <summary>Writes <see cref="ToCsv"/> as UTF-8 with a byte order mark.</summary>
    public static void Write(Stream stream, IReadOnlyList<string> headers, IEnumerable<IReadOnlyList<object?>> rows)
    {
        var bytes = new UTF8Encoding(encoderShouldEmitUTF8Identifier: true);
        stream.Write(bytes.GetPreamble());
        var text = bytes.GetBytes(ToCsv(headers, rows));
        stream.Write(text);
    }

    /// <summary>The raw text of a normalized value (invariant numbers, ISO days).</summary>
    public static string Raw(object? value) => value switch
    {
        null => string.Empty,
        string s => s,
        decimal d => d.ToString(CultureInfo.InvariantCulture),
        DateOnly day => day.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
        IFormattable f => f.ToString(null, CultureInfo.InvariantCulture),
        _ => value.ToString() ?? string.Empty
    };

    private static string Quote(string text) =>
        text.AsSpan().IndexOfAny(",\"\r\n") >= 0 ? "\"" + text.Replace("\"", "\"\"") + "\"" : text;
}
