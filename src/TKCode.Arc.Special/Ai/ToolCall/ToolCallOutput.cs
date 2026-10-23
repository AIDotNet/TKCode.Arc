using System.Text;
using System.Text.Json.Nodes;
using Avalonia.Collections;
using Avalonia.Metadata;

namespace TKCode.Arc;

/// <summary>
/// What a tool call returned, shown under Output once the call succeeded: a <see cref="ToolCallListOutput"/>, a
/// <see cref="ToolCallTableOutput"/>, a <see cref="ToolCallJsonOutput"/> or a <see cref="ToolCallTextOutput"/>. Assign a new
/// output to change what a row shows; an output is read when the row opens.
/// </summary>
public abstract class ToolCallOutput
{
    private protected ToolCallOutput()
    {
    }

    /// <summary>The text the Output copy button puts on the clipboard: JSON for JSON, one line per entry for a list,
    /// tab separated rows with a header for a table, the text itself for text.</summary>
    public abstract string ToClipboardText();

    /// <summary>The quiet summary used when the call has none ("4 results", "2 rows", "12 fields"); null for none.</summary>
    internal abstract string? DeriveSummary(ToolCallLabels labels);
}

/// <summary>One entry of a <see cref="ToolCallListOutput"/>: a short mono lead (an id such as "#482"), a title and a tag.</summary>
public sealed class ToolCallListEntry
{
    /// <summary>Creates an empty entry.</summary>
    public ToolCallListEntry()
    {
    }

    /// <summary>Creates an entry.</summary>
    /// <param name="lead">A short id in mono digits, such as "#482"; null for none.</param>
    /// <param name="title">The entry's title; one line, it truncates.</param>
    /// <param name="tag">A quiet tag on the right, such as a label name; null for none.</param>
    public ToolCallListEntry(string? lead, string title, string? tag = null)
    {
        Lead = lead;
        Title = title;
        Tag = tag;
    }

    /// <summary>A short id in mono digits, such as "#482"; null for none.</summary>
    public string? Lead { get; set; }

    /// <summary>The entry's title; one line, it truncates.</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>A quiet tag on the right, such as a label name; null for none.</summary>
    public string? Tag { get; set; }
}

/// <summary>A list result (search hits, tickets): a lead, a title and a tag per row, 32px rows, folding after five.</summary>
public sealed class ToolCallListOutput : ToolCallOutput
{
    /// <summary>Creates an empty list; add entries to <see cref="Items"/>.</summary>
    public ToolCallListOutput()
    {
    }

    /// <summary>Creates a list of <paramref name="items"/>.</summary>
    public ToolCallListOutput(IEnumerable<ToolCallListEntry> items)
    {
        Items.AddRange(items);
    }

    /// <summary>The entries in order.</summary>
    [Content]
    public AvaloniaList<ToolCallListEntry> Items { get; } = new();

    /// <inheritdoc />
    public override string ToClipboardText()
    {
        var text = new StringBuilder();
        foreach (var item in Items)
        {
            if (text.Length > 0)
            {
                text.Append('\n');
            }
            var first = true;
            foreach (var part in new[] { item.Lead, item.Title, item.Tag })
            {
                if (string.IsNullOrEmpty(part))
                {
                    continue;
                }
                if (!first)
                {
                    text.Append(' ');
                }
                text.Append(part);
                first = false;
            }
        }
        return text.ToString();
    }

    internal override string? DeriveSummary(ToolCallLabels labels) => labels.Results(Items.Count);
}

/// <summary>A table result (database rows): column headers and mono cells, 30px rows, folding after five rows. A null
/// cell reads "null". A wide table scrolls inside its well.</summary>
public sealed class ToolCallTableOutput : ToolCallOutput
{
    /// <summary>Creates an empty table; fill <see cref="Columns"/> and <see cref="Rows"/>.</summary>
    public ToolCallTableOutput()
    {
    }

    /// <summary>Creates a table.</summary>
    /// <param name="columns">The column headers.</param>
    /// <param name="rows">The rows, each with one cell per column (null reads "null").</param>
    public ToolCallTableOutput(IEnumerable<string> columns, IEnumerable<IReadOnlyList<string?>> rows)
    {
        Columns.AddRange(columns);
        Rows.AddRange(rows);
    }

    /// <summary>The column headers.</summary>
    public AvaloniaList<string> Columns { get; } = new();

    /// <summary>The rows, each with one cell per column; a null cell reads "null".</summary>
    public AvaloniaList<IReadOnlyList<string?>> Rows { get; } = new();

    /// <summary>The text of a cell (null reads "null"; a missing cell is empty).</summary>
    internal static string CellText(IReadOnlyList<string?> row, int column) =>
        column < row.Count ? row[column] ?? "null" : string.Empty;

    /// <inheritdoc />
    public override string ToClipboardText()
    {
        var text = new StringBuilder();
        text.Append(string.Join('\t', Columns));
        foreach (var row in Rows)
        {
            text.Append('\n');
            var count = Math.Max(Columns.Count, row.Count);
            for (var i = 0; i < count; i++)
            {
                if (i > 0)
                {
                    text.Append('\t');
                }
                text.Append(CellText(row, i));
            }
        }
        return text.ToString();
    }

    internal override string? DeriveSummary(ToolCallLabels labels) => labels.Rows(Rows.Count);
}

/// <summary>
/// A JSON result (an API response): indented two spaces, tinted (keys, strings, numbers, literals and punctuation in
/// their own colors), 20px lines, folding after ten lines behind "Show N more lines". Wide lines scroll inside the well.
/// Built on <see cref="System.Text.Json.Nodes"/>, so no reflection is involved.
/// </summary>
public sealed class ToolCallJsonOutput : ToolCallOutput
{
    private JsonNode? _value;
    private string? _text;

    /// <summary>Creates an empty JSON output; set <see cref="Value"/> or <see cref="Json"/>.</summary>
    public ToolCallJsonOutput()
    {
    }

    /// <summary>Creates a JSON output from a node.</summary>
    public ToolCallJsonOutput(JsonNode? value)
    {
        Value = value;
    }

    /// <summary>Parses <paramref name="json"/> into an output; throws <see cref="System.Text.Json.JsonException"/> when it
    /// is not valid JSON.</summary>
    public static ToolCallJsonOutput Parse(string json) => new(JsonNode.Parse(json));

    /// <summary>The returned value.</summary>
    public JsonNode? Value
    {
        get => _value;
        set
        {
            _value = value;
            _text = null;
        }
    }

    /// <summary>The value as JSON text (indented two spaces). Setting it parses the text (handy in XAML).</summary>
    public string Json
    {
        get => _text ??= ToolCallJson.Format(_value);
        set => Value = JsonNode.Parse(value);
    }

    /// <inheritdoc />
    public override string ToClipboardText() => Json;

    internal override string? DeriveSummary(ToolCallLabels labels) => _value switch
    {
        JsonObject obj => labels.Fields(obj.Count),
        JsonArray array => labels.Items(array.Count),
        _ => null
    };
}

/// <summary>A text result (a sent message, a short log): one paragraph that keeps its line breaks.</summary>
public sealed class ToolCallTextOutput : ToolCallOutput
{
    /// <summary>Creates an empty text output.</summary>
    public ToolCallTextOutput()
    {
    }

    /// <summary>Creates a text output.</summary>
    public ToolCallTextOutput(string text)
    {
        Text = text;
    }

    /// <summary>The text; line breaks are kept.</summary>
    [Content]
    public string Text { get; set; } = string.Empty;

    /// <inheritdoc />
    public override string ToClipboardText() => Text;

    internal override string? DeriveSummary(ToolCallLabels labels) => null;
}
