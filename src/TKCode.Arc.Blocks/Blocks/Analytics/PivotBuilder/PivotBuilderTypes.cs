using Avalonia.Interactivity;

namespace TKCode.Arc;

/// <summary>How the values of a field are combined in a pivot cell.</summary>
public enum PivotBuilderAggregation
{
    /// <summary>Adds the values up.</summary>
    Sum,

    /// <summary>Counts the records.</summary>
    Count,

    /// <summary>The mean of the values.</summary>
    Average,

    /// <summary>The smallest value.</summary>
    Min,

    /// <summary>The largest value.</summary>
    Max,
}

/// <summary>Where a field sits in the pivot layout.</summary>
public enum PivotBuilderZone
{
    /// <summary>Not used: it waits in the field list.</summary>
    None,

    /// <summary>Each distinct value makes a row.</summary>
    Rows,

    /// <summary>Each distinct value makes a column.</summary>
    Columns,

    /// <summary>Aggregated into the cells.</summary>
    Values,
}

/// <summary>A field of the records: a key, a title and how to read it from a record.</summary>
public sealed class PivotBuilderField
{
    /// <summary>Creates an empty field; set <see cref="Key"/>, <see cref="Title"/> and <see cref="Selector"/>.</summary>
    public PivotBuilderField()
    {
    }

    /// <summary>Creates a field from a key, a title and a selector.</summary>
    public PivotBuilderField(string key, string title, Func<object, object?> selector, bool isNumeric = false, string? format = null)
    {
        Key = key;
        Title = title;
        Selector = selector;
        IsNumeric = isNumeric;
        Format = format;
    }

    /// <summary>Creates a field from a typed selector.</summary>
    public static PivotBuilderField Create<T>(string key, string title, Func<T, object?> selector, bool isNumeric = false, string? format = null)
        => new(key, title, o => o is T t ? selector(t) : null, isNumeric, format);

    /// <summary>The field's stable key (the layout collections hold keys).</summary>
    public string Key { get; set; } = string.Empty;

    /// <summary>The name on the chip and in the table's headers.</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>Reads the field from a record.</summary>
    public Func<object, object?> Selector { get; set; } = _ => null;

    /// <summary>True for a number that can be summed; only numeric fields offer Sum, Average, Min and Max.</summary>
    public bool IsNumeric { get; set; }

    /// <summary>The number format of aggregated values ("C0", "N1"); null uses "N0" (Average: "N1").</summary>
    public string? Format { get; set; }

    /// <inheritdoc />
    public override string ToString() => Title;
}

/// <summary>A field placed in the values zone with its aggregation.</summary>
public sealed class PivotBuilderValue
{
    /// <summary>Creates an empty value.</summary>
    public PivotBuilderValue()
    {
    }

    /// <summary>Creates a value from a field key and an aggregation.</summary>
    public PivotBuilderValue(string fieldKey, PivotBuilderAggregation aggregation = PivotBuilderAggregation.Sum)
    {
        FieldKey = fieldKey;
        Aggregation = aggregation;
    }

    /// <summary>The <see cref="PivotBuilderField.Key"/> aggregated.</summary>
    public string FieldKey { get; set; } = string.Empty;

    /// <summary>How the values are combined.</summary>
    public PivotBuilderAggregation Aggregation { get; set; }
}

/// <summary>A computed pivot: row and column keys, the cells, and the totals.</summary>
public sealed class PivotBuilderResult
{
    internal PivotBuilderResult(IReadOnlyList<string[]> rowKeys, IReadOnlyList<string[]> columnKeys, IReadOnlyList<PivotBuilderValue> values,
        double?[,,] cells, double?[,] rowTotals, double?[,] columnTotals, double?[] grandTotals)
    {
        RowKeys = rowKeys;
        ColumnKeys = columnKeys;
        Values = values;
        _cells = cells;
        _rowTotals = rowTotals;
        _columnTotals = columnTotals;
        _grandTotals = grandTotals;
    }

    private readonly double?[,,] _cells;
    private readonly double?[,] _rowTotals;
    private readonly double?[,] _columnTotals;
    private readonly double?[] _grandTotals;

    /// <summary>The rows: one entry per distinct combination of the row fields' values, sorted.</summary>
    public IReadOnlyList<string[]> RowKeys { get; }

    /// <summary>The columns: one entry per distinct combination of the column fields' values, sorted (one empty key without column fields).</summary>
    public IReadOnlyList<string[]> ColumnKeys { get; }

    /// <summary>The values aggregated in each cell.</summary>
    public IReadOnlyList<PivotBuilderValue> Values { get; }

    /// <summary>The aggregate of value <paramref name="value"/> at a row and a column; null when no record falls there.</summary>
    public double? Cell(int row, int column, int value) => _cells[row, column, value];

    /// <summary>The aggregate of a row across every column.</summary>
    public double? RowTotal(int row, int value) => _rowTotals[row, value];

    /// <summary>The aggregate of a column across every row.</summary>
    public double? ColumnTotal(int column, int value) => _columnTotals[column, value];

    /// <summary>The aggregate of every record.</summary>
    public double? GrandTotal(int value) => _grandTotals[value];
}

/// <summary>Arguments of <see cref="PivotBuilder.LayoutChanged"/>.</summary>
public sealed class PivotBuilderLayoutChangedEventArgs : RoutedEventArgs
{
    /// <summary>Creates the arguments.</summary>
    public PivotBuilderLayoutChangedEventArgs(RoutedEvent routedEvent, string fieldKey, PivotBuilderZone zone)
        : base(routedEvent)
    {
        FieldKey = fieldKey;
        Zone = zone;
    }

    /// <summary>The field that moved or changed.</summary>
    public string FieldKey { get; }

    /// <summary>The zone it is in now.</summary>
    public PivotBuilderZone Zone { get; }
}

/// <summary>The words <see cref="PivotBuilder"/> shows and reads out; every one has an English default.</summary>
public sealed class PivotBuilderLabels
{
    /// <summary>The field list's heading.</summary>
    public string Fields { get; init; } = "Fields";

    /// <summary>The zones' names, in the order of <see cref="PivotBuilderZone"/> (the first is the field list).</summary>
    public IReadOnlyList<string> Zones { get; init; } = ["Fields", "Rows", "Columns", "Values"];

    /// <summary>The line in an empty zone.</summary>
    public string DropHere { get; init; } = "Drag a field here";

    /// <summary>The aggregations' names, in the order of <see cref="PivotBuilderAggregation"/>.</summary>
    public IReadOnlyList<string> Aggregations { get; init; } = ["Sum", "Count", "Average", "Min", "Max"];

    /// <summary>The menu entry that puts a field in a zone; {0} is the zone.</summary>
    public string MoveToFormat { get; init; } = "Move to {0}";

    /// <summary>The menu entry that moves a chip earlier in its zone.</summary>
    public string MoveEarlier { get; init; } = "Move left";

    /// <summary>The menu entry that moves a chip later in its zone.</summary>
    public string MoveLater { get; init; } = "Move right";

    /// <summary>The menu entry that takes a field out of the layout.</summary>
    public string Remove { get; init; } = "Remove";

    /// <summary>The totals row and column.</summary>
    public string Total { get; init; } = "Total";

    /// <summary>The table's caption.</summary>
    public string TableCaption { get; init; } = "Pivot table";

    /// <summary>The line shown while no value is placed.</summary>
    public string NoValues { get; init; } = "Drag a number into Values to fill the table.";

    /// <summary>A chip's screen reader name; {0} is the field, {1} its zone.</summary>
    public string ChipFormat { get; init; } = "{0}, in {1}. Press Enter for options.";

    /// <summary>The announcement after a field moved; {0} is the field, {1} the zone.</summary>
    public string MovedFormat { get; init; } = "{0} moved to {1}";

    /// <summary>The value header; {0} is the aggregation, {1} the field.</summary>
    public string ValueHeaderFormat { get; init; } = "{0} of {1}";

    /// <summary>The line with the table's size; {0} rows, {1} columns, {2} records.</summary>
    public string SizeFormat { get; init; } = "{0} rows × {1} columns from {2} records";
}
