using System.Collections.Generic;
using System.Text;

namespace TKCode.Arc;

public sealed partial class ExtractionDocument
{
    /// <summary>
    /// The text to send to the model: the blocks in order with a blank line between them, one line per paragraph, address
    /// line, label and value (<c>Label: value</c>), totals row and table row (cells separated by <c> | </c>, a row's detail in
    /// brackets after its first cell); spans are plain text. The masthead gives its name and title, an email header its
    /// subject, sender, recipient and date, a footer its lines; a rule adds nothing.
    /// </summary>
    public string ToPlainText()
    {
        var lines = new List<string>();
        var text = new StringBuilder();
        foreach (var block in Blocks)
        {
            AppendBlock(block, lines, text);
        }
        return text.ToString();
    }

    private static void AppendBlock(DocumentBlock block, List<string> lines, StringBuilder text)
    {
        if (block is DocumentColumns columns)
        {
            // Columns read one after the other, left first.
            foreach (var column in columns.Columns)
            {
                AppendBlock(column, lines, text);
            }
            return;
        }
        lines.Clear();
        LinesOf(block, lines);
        if (lines.Count == 0)
        {
            return;
        }
        if (text.Length > 0)
        {
            text.Append("\n\n");
        }
        text.AppendJoin('\n', lines);
    }

    private static void LinesOf(DocumentBlock block, List<string> lines)
    {
        switch (block)
        {
            case DocumentMasthead masthead:
                Add(lines, string.IsNullOrEmpty(masthead.Detail) ? masthead.Name : masthead.Name + " " + masthead.Detail);
                Add(lines, masthead.Title);
                break;
            case DocumentEmailHeader header:
                Add(lines, Plain(header.Subject));
                var from = Plain(header.FromName);
                var address = Plain(header.FromAddress);
                Add(lines, from.Length > 0 && address.Length > 0 ? from + " " + address : from + address);
                Add(lines, Plain(header.To));
                Add(lines, Plain(header.Date));
                break;
            case DocumentHeading heading:
                Add(lines, Plain(heading.Text));
                break;
            case DocumentParagraph paragraph:
                Add(lines, Plain(paragraph.Text));
                break;
            case DocumentLines group:
                Add(lines, group.Label);
                foreach (var line in group.Lines)
                {
                    Add(lines, Plain(line));
                }
                break;
            case DocumentPairs pairs:
                foreach (var pair in pairs.Pairs)
                {
                    Add(lines, Pair(pair.Label, Plain(pair.Value)));
                }
                break;
            case DocumentMetaStrip strip:
                foreach (var cell in strip.Cells)
                {
                    Add(lines, Pair(cell.Label, Plain(cell.Value)));
                }
                break;
            case DocumentTable table:
                if (table.Columns.Any(c => c.Header.Length > 0))
                {
                    Add(lines, string.Join(" | ", table.Columns.Select(c => c.Header.Trim())));
                }
                foreach (var row in table.Rows)
                {
                    var cells = row.Cells.Select(Plain).ToArray();
                    if (row.Detail is { } detail && Plain(detail) is { Length: > 0 } extra)
                    {
                        if (cells.Length == 0)
                        {
                            cells = [extra];
                        }
                        else
                        {
                            cells[0] = cells[0].Length > 0 ? cells[0] + " (" + extra + ")" : extra;
                        }
                    }
                    Add(lines, string.Join(" | ", cells));
                }
                break;
            case DocumentTotals totals:
                foreach (var row in totals.Rows)
                {
                    Add(lines, Pair(row.Label, Plain(row.Value)));
                }
                break;
            case DocumentFooter footer:
                foreach (var line in footer.Left)
                {
                    Add(lines, line);
                }
                Add(lines, footer.Right);
                break;
        }
    }

    /// <summary>"Label: value", or whichever of the two there is.</summary>
    private static string Pair(string label, string value)
    {
        label = label.Trim();
        return label.Length == 0 ? value : value.Length == 0 ? label : label + ": " + value;
    }

    private static string Plain(DocumentText? text) => text?.PlainText.Trim() ?? string.Empty;

    private static void Add(List<string> lines, string? line)
    {
        line = line?.Trim();
        if (!string.IsNullOrEmpty(line))
        {
            lines.Add(line);
        }
    }
}
