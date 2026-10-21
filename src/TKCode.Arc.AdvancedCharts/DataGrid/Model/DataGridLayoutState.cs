using System.Text.Json;
using System.Text.Json.Serialization;

namespace TKCode.Arc;

/// <summary>A column filter as stored in a <see cref="DataGridLayoutState"/>: an expression or the values a select column keeps.</summary>
public sealed class DataGridFilterState
{
    /// <summary>The typed expression, or null for a value list.</summary>
    public string? Expression { get; set; }

    /// <summary>The values a select or boolean column keeps (an empty string keeps empty cells), or null for an expression.</summary>
    public List<string>? Values { get; set; }
}

/// <summary>
/// The view of a <see cref="DataGrid"/> that people shape: column order, widths, pinned and hidden columns, sort, filters,
/// grouping and density. <see cref="DataGrid.GetLayoutState"/> reads it and <see cref="DataGrid.SetLayoutState"/> applies it,
/// so a view can be saved when a window closes and restored when it opens. <see cref="ToJson"/> and <see cref="FromJson"/> use a
/// source-generated serializer (trim and AOT safe); unknown fields are ignored and missing fields fall back to the defaults.
/// </summary>
public sealed class DataGridLayoutState
{
    /// <summary>The state format; readers ignore fields they do not know.</summary>
    public int Version { get; set; } = 1;

    /// <summary>Column keys in display order; null keeps the definition order.</summary>
    public List<string>? ColumnOrder { get; set; }

    /// <summary>Widths people set, by column key.</summary>
    public Dictionary<string, double>? ColumnWidths { get; set; }

    /// <summary>Pinned column keys; null uses the columns' <see cref="DataGridColumn.IsPinned"/>.</summary>
    public List<string>? PinnedColumns { get; set; }

    /// <summary>Hidden column keys; null uses the columns' <see cref="DataGridColumn.IsHidden"/>.</summary>
    public List<string>? HiddenColumns { get; set; }

    /// <summary>The sort keys, by priority.</summary>
    public List<DataGridSort> SortDescriptions { get; set; } = [];

    /// <summary>The column filters, by column key.</summary>
    public Dictionary<string, DataGridFilterState> Filters { get; set; } = [];

    /// <summary>The grouping column keys, outermost first.</summary>
    public List<string> GroupBy { get; set; } = [];

    /// <summary>The row height.</summary>
    public DataGridDensity Density { get; set; } = DataGridDensity.Standard;

    /// <summary>Writes the state as JSON.</summary>
    public string ToJson() => JsonSerializer.Serialize(this, DataGridJsonContext.Default.DataGridLayoutState);

    /// <summary>Reads a state written by <see cref="ToJson"/>; null when the text is not a state (nothing is thrown).</summary>
    public static DataGridLayoutState? FromJson(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }
        try
        {
            return JsonSerializer.Deserialize(json, DataGridJsonContext.Default.DataGridLayoutState);
        }
        catch (JsonException)
        {
            return null;
        }
    }
}

[JsonSourceGenerationOptions(UseStringEnumConverter = true, PropertyNameCaseInsensitive = true, WriteIndented = false)]
[JsonSerializable(typeof(DataGridLayoutState))]
internal sealed partial class DataGridJsonContext : JsonSerializerContext;
