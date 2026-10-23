using Avalonia.Collections;
using Avalonia.Metadata;

namespace TKCode.Arc;

/// <summary>What kind of value a field holds; it decides how the text is checked and how it appears in the typed output.</summary>
public enum ExtractionFieldType
{
    /// <summary>Free text.</summary>
    Text,

    /// <summary>A date; the output is an ISO date (YYYY-MM-DD) and anything else is invalid.</summary>
    Date,

    /// <summary>An amount of money; the output is a number with the thousands separators removed.</summary>
    Money,

    /// <summary>A three-letter currency code.</summary>
    Currency,

    /// <summary>An IBAN; spaces are removed and the check digits are verified (mod 97).</summary>
    Iban,

    /// <summary>A whole number.</summary>
    Integer,

    /// <summary>A number.</summary>
    Number
}

/// <summary>A group of fields; its color (one of the four series colors in turn) tints the dot, the rows, the cited text and the links.</summary>
public sealed class ExtractionGroup
{
    /// <summary>The key fields refer to.</summary>
    public string Key { get; set; } = string.Empty;

    /// <summary>The title.</summary>
    public string Title { get; set; } = string.Empty;
}

/// <summary>A column of a table field.</summary>
public sealed class ExtractionColumn
{
    /// <summary>The key.</summary>
    public string Key { get; set; } = string.Empty;

    /// <summary>The header.</summary>
    public string Label { get; set; } = string.Empty;

    /// <summary>The type of the cells.</summary>
    public ExtractionFieldType Type { get; set; }
}

/// <summary>A field to extract.</summary>
public class ExtractionField
{
    /// <summary>The key; value paths start with it.</summary>
    public string Key { get; set; } = string.Empty;

    /// <summary>The label.</summary>
    public string Label { get; set; } = string.Empty;

    /// <summary>The type.</summary>
    public ExtractionFieldType Type { get; set; }

    /// <summary>What to look for; sent to the model in the JSON schema.</summary>
    public string? Description { get; set; }

    /// <summary>The key of the group, or null for a group of its own.</summary>
    public string? Group { get; set; }
}

/// <summary>A field that is a table of rows; its value paths are <c>key.row.column</c>.</summary>
public sealed class ExtractionTableField : ExtractionField
{
    /// <summary>The columns.</summary>
    public AvaloniaList<ExtractionColumn> Columns { get; } = new();
}

/// <summary>What to extract from a document.</summary>
public sealed partial class ExtractionSchema
{
    /// <summary>The name (used as the JSON schema's title key).</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>A title.</summary>
    public string? Title { get; set; }

    /// <summary>A description.</summary>
    public string? Description { get; set; }

    /// <summary>The groups, in order; none makes every field its own group.</summary>
    public AvaloniaList<ExtractionGroup> Groups { get; } = new();

    /// <summary>The fields in order.</summary>
    [Content]
    public AvaloniaList<ExtractionField> Fields { get; } = new();
}

/// <summary>A document with the schema to read it by (a choice of the file menu).</summary>
public sealed class ExtractionSample
{
    /// <summary>The document.</summary>
    public ExtractionDocument Document { get; set; } = new();

    /// <summary>The schema.</summary>
    public ExtractionSchema Schema { get; set; } = new();
}
