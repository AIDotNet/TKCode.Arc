using Avalonia.Interactivity;

namespace TKCode.Arc;

/// <summary>Kind of data a <see cref="DataGridColumn"/> holds; decides parsing, alignment, formatting and totals.</summary>
public enum DataGridColumnType
{
    /// <summary>Free text.</summary>
    Text,

    /// <summary>A number (stored as <see cref="decimal"/>), right-aligned, totalled.</summary>
    Number,

    /// <summary>A number shown with the column's currency symbol.</summary>
    Currency,

    /// <summary>A number of percent (32.5 shows as 32.5%); totals average by default.</summary>
    Percent,

    /// <summary>One of the column's options.</summary>
    Select,

    /// <summary>A calendar day (stored as <see cref="DateOnly"/>), edited with a calendar.</summary>
    Date
}

/// <summary>Row height of a <see cref="DataGrid"/>.</summary>
public enum DataGridDensity
{
    /// <summary>32px rows and 13px text.</summary>
    Compact,

    /// <summary>40px rows.</summary>
    Standard,

    /// <summary>48px rows.</summary>
    Comfortable
}

/// <summary>What the totals row and group subtotals show for a column.</summary>
public enum DataGridAggregate
{
    None,
    Sum,
    Average,
    Min,
    Max,

    /// <summary>Number of non-empty cells.</summary>
    Count
}

/// <summary>Direction of one sort key.</summary>
public enum DataGridSortDirection
{
    Ascending,
    Descending
}

/// <summary>A file format the grid exports.</summary>
public enum DataGridExportFormat
{
    /// <summary>RFC 4180 CSV, UTF-8 with a byte order mark.</summary>
    Csv,

    /// <summary>An Excel workbook (.xlsx).</summary>
    Xlsx
}

/// <summary>The formats Export offers.</summary>
[Flags]
public enum DataGridExportFormats
{
    None = 0,
    Csv = 1,
    Xlsx = 2,
    All = Csv | Xlsx
}

/// <summary>What wrote a set of cells.</summary>
public enum DataGridChangeReason
{
    Edit,
    Paste,
    Cut,
    Clear,
    Fill,
    FillDown,
    Undo,
    Redo
}

/// <summary>One sort key: a column key and a direction.</summary>
public readonly record struct DataGridSort(string Key, DataGridSortDirection Direction = DataGridSortDirection.Ascending)
{
    /// <summary>Ascending sort on <paramref name="key"/>.</summary>
    public static DataGridSort Ascending(string key) => new(key, DataGridSortDirection.Ascending);

    /// <summary>Descending sort on <paramref name="key"/>.</summary>
    public static DataGridSort Descending(string key) => new(key, DataGridSortDirection.Descending);
}

/// <summary>
/// A column filter: an expression for text, number and date columns (<c>abc</c>, <c>=abc</c>, <c>!abc</c>,
/// <c>&gt;100</c>, <c>10..50</c>, <c>2026-03</c>), or the list of values to keep for select columns.
/// </summary>
public sealed class DataGridFilter : IEquatable<DataGridFilter>
{
    private DataGridFilter(string? expression, IReadOnlyList<string>? values)
    {
        Expression = expression;
        Values = values;
    }

    /// <summary>The typed expression, or null for a value list.</summary>
    public string? Expression { get; }

    /// <summary>The values a select filter keeps (an empty string keeps empty cells), or null for an expression.</summary>
    public IReadOnlyList<string>? Values { get; }

    /// <summary>An expression filter.</summary>
    public static DataGridFilter Text(string expression) => new(expression ?? string.Empty, null);

    /// <summary>A select filter that keeps <paramref name="values"/>.</summary>
    public static DataGridFilter OneOf(IReadOnlyList<string> values) => new(null, values.ToArray());

    /// <summary>True when both filters have the same expression or the same kept values in the same order.</summary>
    public bool Equals(DataGridFilter? other)
    {
        if (other is null)
        {
            return false;
        }
        if (Expression is not null || other.Expression is not null)
        {
            return string.Equals(Expression, other.Expression, StringComparison.Ordinal);
        }
        return Values!.SequenceEqual(other.Values!, StringComparer.Ordinal);
    }

    public override bool Equals(object? obj) => Equals(obj as DataGridFilter);

    public override int GetHashCode()
    {
        if (Expression is not null)
        {
            return Expression.GetHashCode(StringComparison.Ordinal);
        }
        var hash = new HashCode();
        foreach (var value in Values!)
        {
            hash.Add(value, StringComparer.Ordinal);
        }
        return hash.ToHashCode();
    }

    public override string ToString() => Expression ?? string.Join(", ", Values!);
}

/// <summary>A cell position: the display row index and the visible column index.</summary>
public readonly record struct DataGridCellAddress(int Row, int Column);

/// <summary>A rectangle of cells (inclusive), in display rows and visible columns.</summary>
public readonly record struct DataGridCellRange(int Row0, int Column0, int Row1, int Column1)
{
    /// <summary>Normalizes two corners into a range.</summary>
    public static DataGridCellRange FromCorners(DataGridCellAddress a, DataGridCellAddress b) =>
        new(Math.Min(a.Row, b.Row), Math.Min(a.Column, b.Column), Math.Max(a.Row, b.Row), Math.Max(a.Column, b.Column));

    /// <summary>Number of rows in the range.</summary>
    public int RowCount => Row1 - Row0 + 1;

    /// <summary>Number of columns in the range.</summary>
    public int ColumnCount => Column1 - Column0 + 1;

    /// <summary>Number of cells in the range.</summary>
    public int CellCount => RowCount * ColumnCount;

    /// <summary>True when the range is one cell.</summary>
    public bool IsSingleCell => Row0 == Row1 && Column0 == Column1;

    /// <summary>True when the cell at <paramref name="row"/>, <paramref name="column"/> lies inside the range.</summary>
    public bool Contains(int row, int column) => row >= Row0 && row <= Row1 && column >= Column0 && column <= Column1;
}

/// <summary>One cell written by an edit, paste, fill, clear, undo or redo.</summary>
/// <param name="Row">The row item.</param>
/// <param name="RowKey">The row's key.</param>
/// <param name="ColumnKey">The column's key.</param>
/// <param name="OldValue">The value before (a string, a decimal, a DateOnly or null).</param>
/// <param name="NewValue">The value after.</param>
public sealed record DataGridCellChange(object Row, object RowKey, string ColumnKey, object? OldValue, object? NewValue);

/// <summary>Arguments of <see cref="DataGrid.CellEditEnding"/>: set <see cref="Cancel"/> to keep the editor open.</summary>
public sealed class DataGridCellEditEndingEventArgs : RoutedEventArgs
{
    public DataGridCellEditEndingEventArgs(RoutedEvent routedEvent, object row, object rowKey, DataGridColumn column,
                                           object? oldValue, object? newValue)
        : base(routedEvent)
    {
        Row = row;
        RowKey = rowKey;
        Column = column;
        OldValue = oldValue;
        NewValue = newValue;
    }

    /// <summary>The row item.</summary>
    public object Row { get; }

    /// <summary>The row's key.</summary>
    public object RowKey { get; }

    /// <summary>The edited column.</summary>
    public DataGridColumn Column { get; }

    /// <summary>The value before the edit.</summary>
    public object? OldValue { get; }

    /// <summary>The parsed value about to be written.</summary>
    public object? NewValue { get; }

    /// <summary>True keeps the editor open and marks the value invalid.</summary>
    public bool Cancel { get; set; }

    /// <summary>Why the value was refused; announced and shown in the status bar.</summary>
    public string? ErrorMessage { get; set; }
}

/// <summary>Arguments of <see cref="DataGrid.CellsChanged"/>.</summary>
public sealed class DataGridCellsChangedEventArgs : RoutedEventArgs
{
    public DataGridCellsChangedEventArgs(RoutedEvent routedEvent, IReadOnlyList<DataGridCellChange> changes, DataGridChangeReason reason)
        : base(routedEvent)
    {
        Changes = changes;
        Reason = reason;
    }

    /// <summary>The written cells.</summary>
    public IReadOnlyList<DataGridCellChange> Changes { get; }

    /// <summary>What wrote them.</summary>
    public DataGridChangeReason Reason { get; }
}

/// <summary>Arguments of <see cref="DataGrid.RowsDeleted"/> and <see cref="DataGrid.RowsRestored"/>.</summary>
public sealed class DataGridRowsEventArgs : RoutedEventArgs
{
    public DataGridRowsEventArgs(RoutedEvent routedEvent, IReadOnlyList<object> rows, IReadOnlyList<int> indices)
        : base(routedEvent)
    {
        Rows = rows;
        Indices = indices;
    }

    /// <summary>The row items, in source order.</summary>
    public IReadOnlyList<object> Rows { get; }

    /// <summary>Each row's index in the source before it was deleted (and where an undo puts it back).</summary>
    public IReadOnlyList<int> Indices { get; }
}

/// <summary>Arguments of <see cref="DataGrid.BulkActionInvoked"/>.</summary>
public sealed class DataGridBulkActionEventArgs : RoutedEventArgs
{
    public DataGridBulkActionEventArgs(RoutedEvent routedEvent, DataGridBulkAction action, IReadOnlyList<object> rows)
        : base(routedEvent)
    {
        Action = action;
        Rows = rows;
    }

    /// <summary>The clicked action.</summary>
    public DataGridBulkAction Action { get; }

    /// <summary>The checked rows, in data order.</summary>
    public IReadOnlyList<object> Rows { get; }
}

/// <summary>Arguments of <see cref="DataGrid.Exporting"/>: change the file name or cancel before the save dialog opens.</summary>
public sealed class DataGridExportingEventArgs : RoutedEventArgs
{
    public DataGridExportingEventArgs(RoutedEvent routedEvent, DataGridExportFormat format, IReadOnlyList<object> rows, string fileName)
        : base(routedEvent)
    {
        Format = format;
        Rows = rows;
        FileName = fileName;
    }

    /// <summary>The chosen format.</summary>
    public DataGridExportFormat Format { get; }

    /// <summary>The rows about to be written, in view order.</summary>
    public IReadOnlyList<object> Rows { get; }

    /// <summary>The suggested file name without the extension.</summary>
    public string FileName { get; set; }

    /// <summary>True stops the export.</summary>
    public bool Cancel { get; set; }
}

/// <summary>What a <see cref="IDataGridDataSource"/> is asked for.</summary>
/// <param name="Start">First row (inclusive).</param>
/// <param name="End">Row after the last (exclusive).</param>
/// <param name="Sort">Sort keys in priority order.</param>
/// <param name="Filters">Column filters by column key.</param>
/// <param name="Search">The quick search text.</param>
public sealed record DataGridRowsRequest(int Start, int End, IReadOnlyList<DataGridSort> Sort,
    IReadOnlyDictionary<string, DataGridFilter> Filters, string Search);

/// <summary>One block of rows from a <see cref="IDataGridDataSource"/>.</summary>
/// <param name="Rows">The rows of the requested block.</param>
/// <param name="RowCount">Number of rows in the whole filtered set.</param>
/// <param name="Totals">Totals by column key for the totals row; null keeps the previous totals.</param>
public sealed record DataGridRowsResult(IReadOnlyList<object> Rows, int RowCount, IReadOnlyDictionary<string, decimal?>? Totals = null);

/// <summary>
/// Server rows for a <see cref="DataGrid"/>: the grid asks for the blocks that cover the rows in view, keeps up to 40
/// blocks, and starts over (cancelling requests in flight) when the sort, filters or search change.
/// </summary>
public interface IDataGridDataSource
{
    /// <summary>Rows per request (default 100).</summary>
    int BlockSize => 100;

    /// <summary>Returns one block; sort, filter and search the whole data set on the server.</summary>
    Task<DataGridRowsResult> GetRowsAsync(DataGridRowsRequest request, CancellationToken cancellation);
}
