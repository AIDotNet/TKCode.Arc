using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;

namespace TKCode.Arc;

/// <summary>Where one cell of an <see cref="EnrichColumn"/> is.</summary>
public enum EnrichColumnCellStatus
{
    /// <summary>Not run yet: a dash.</summary>
    Idle,

    /// <summary>Waiting for a free worker: a clock and "Queued".</summary>
    Queued,

    /// <summary>Working: a spinner and a shimmering activity label.</summary>
    Running,

    /// <summary>Settled into a value, with its source when there is one.</summary>
    Done,

    /// <summary>Failed: an alert, "Failed" and the reason, with a retry control.</summary>
    Failed
}

/// <summary>One row of an <see cref="EnrichColumn"/>: the record the prompt runs on (a company, a lead, a ticket).</summary>
public sealed class EnrichColumnRow
{
    /// <summary>Creates an empty row; set <see cref="Id"/> and <see cref="Name"/> before showing it.</summary>
    public EnrichColumnRow()
    {
    }

    /// <summary>Creates a row.</summary>
    /// <param name="id">The row's stable key; cells are stored under it.</param>
    /// <param name="name">The row header, such as the company's name.</param>
    /// <param name="domain">A quiet detail after the name, such as the company's domain.</param>
    public EnrichColumnRow(string id, string name, string? domain = null)
    {
        Id = id;
        Name = name;
        Domain = domain;
    }

    /// <summary>The row's stable key; cells, <see cref="EnrichColumn.InitialCells"/> and events use it.</summary>
    public string Id { get; init; } = string.Empty;

    /// <summary>The row header, such as the company's name.</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>A quiet detail after the name, such as the company's domain; it folds away below 560px.</summary>
    public string? Domain { get; init; }

    /// <summary>A 24 × 24 stroked glyph drawn at 16px before the name; null draws a letter monogram in a tinted square.</summary>
    public Geometry? Logo { get; init; }

    /// <summary>The logo's color; null uses the text color.</summary>
    public IBrush? LogoBrush { get; init; }

    /// <inheritdoc />
    public override string ToString() => Name;
}

/// <summary>The AI column: its name in the header and the prompt that runs down every row.</summary>
public sealed class EnrichColumnDefinition
{
    /// <summary>Creates an empty column.</summary>
    public EnrichColumnDefinition()
    {
    }

    /// <summary>Creates a column with a name and a prompt.</summary>
    public EnrichColumnDefinition(string name, string prompt)
    {
        Name = name;
        Prompt = prompt;
    }

    /// <summary>The column's name, shown in its header.</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>The prompt that runs once per row, shown under the name.</summary>
    public string Prompt { get; init; } = string.Empty;

    /// <inheritdoc />
    public override string ToString() => Name;
}

/// <summary>Where a value came from: a short label and the address it opens.</summary>
public sealed class EnrichColumnSource
{
    /// <summary>Creates an empty source.</summary>
    public EnrichColumnSource()
    {
    }

    /// <summary>Creates a source.</summary>
    public EnrichColumnSource(string label, string href)
    {
        Label = label;
        Href = href;
    }

    /// <summary>The short label after the value, such as the page's host.</summary>
    public string Label { get; init; } = string.Empty;

    /// <summary>The address the link opens.</summary>
    public string Href { get; init; } = string.Empty;
}

/// <summary>What the enrich delegate returns for one row: the value and, optionally, its source.</summary>
public sealed class EnrichColumnResult
{
    /// <summary>Creates an empty result.</summary>
    public EnrichColumnResult()
    {
    }

    /// <summary>Creates a result.</summary>
    public EnrichColumnResult(string value, EnrichColumnSource? source = null)
    {
        Value = value;
        Source = source;
    }

    /// <summary>The cell's value; keep it to one short line.</summary>
    public string Value { get; init; } = string.Empty;

    /// <summary>Where the value came from, or null.</summary>
    public EnrichColumnSource? Source { get; init; }
}

/// <summary>
/// One cell of the AI column: its status and, once settled, its value and source or the reason it failed. Immutable;
/// the column hands out a new cell on every change.
/// </summary>
public sealed class EnrichColumnCell
{
    /// <summary>A cell that has not run.</summary>
    public static EnrichColumnCell Idle { get; } = new();

    /// <summary>Where the cell is.</summary>
    public EnrichColumnCellStatus Status { get; init; }

    /// <summary>The value, once done.</summary>
    public string? Value { get; init; }

    /// <summary>Where the value came from, once done.</summary>
    public EnrichColumnSource? Source { get; init; }

    /// <summary>Why it failed, once failed.</summary>
    public string? Error { get; init; }

    /// <summary>What the cell is doing while it runs ("Searching for the company"), set through <see cref="EnrichColumnContext.SetActivity"/>.</summary>
    public string? Activity { get; init; }

    /// <summary>How many times the cell has been run (1 on the first run).</summary>
    public int Attempt { get; init; }

    /// <summary>A done cell with a value.</summary>
    public static EnrichColumnCell Done(string value, EnrichColumnSource? source = null, int attempt = 1) =>
        new() { Status = EnrichColumnCellStatus.Done, Value = value, Source = source, Attempt = attempt };

    /// <summary>A failed cell with its reason.</summary>
    public static EnrichColumnCell Failed(string error, int attempt = 1) =>
        new() { Status = EnrichColumnCellStatus.Failed, Error = error, Attempt = attempt };

    /// <summary>Whether the cell has settled into a value.</summary>
    public bool HasValue => Status == EnrichColumnCellStatus.Done;
}

/// <summary>The model shown in the footer of an <see cref="EnrichColumn"/>.</summary>
public sealed class EnrichColumnModel
{
    /// <summary>Creates an empty model.</summary>
    public EnrichColumnModel()
    {
    }

    /// <summary>Creates a model.</summary>
    public EnrichColumnModel(string name, string? detail = null, Geometry? mark = null)
    {
        Name = name;
        Detail = detail;
        Mark = mark;
    }

    /// <summary>The model's display name.</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>A quiet detail after the name, such as the tools it uses ("web search"); it folds away below 560px.</summary>
    public string? Detail { get; init; }

    /// <summary>A 24 × 24 stroked glyph before the name; null draws the sparkle.</summary>
    public Geometry? Mark { get; init; }

    /// <summary>The mark's color; null uses the accent.</summary>
    public IBrush? MarkBrush { get; init; }
}

/// <summary>
/// What the enrich delegate gets with each row: the column and its prompt, a cancellation token that fires when the
/// person stops the run or the column is re-run, the attempt number and <see cref="SetActivity"/> for the shimmering label.
/// </summary>
public sealed class EnrichColumnContext
{
    private readonly Action<string> _setActivity;

    internal EnrichColumnContext(EnrichColumnRow row, EnrichColumnDefinition column, int attempt, CancellationToken token,
        Action<string> setActivity)
    {
        Row = row;
        Column = column;
        Attempt = attempt;
        CancellationToken = token;
        _setActivity = setActivity;
    }

    /// <summary>The row being enriched.</summary>
    public EnrichColumnRow Row { get; }

    /// <summary>The column as it was when this cell started.</summary>
    public EnrichColumnDefinition Column { get; }

    /// <summary>The column's prompt.</summary>
    public string Prompt => Column.Prompt;

    /// <summary>How many times this cell has been run, this run included (1 on the first run).</summary>
    public int Attempt { get; }

    /// <summary>Fires when the run is stopped or replaced; pass it to your request.</summary>
    public CancellationToken CancellationToken { get; }

    /// <summary>Sets the shimmering label of the running cell ("Reading the company's site"). Safe from any thread.</summary>
    public void SetActivity(string text)
    {
        if (Dispatcher.UIThread.CheckAccess())
        {
            _setActivity(text);
        }
        else
        {
            Dispatcher.UIThread.Post(() => _setActivity(text));
        }
    }
}

/// <summary>Arguments of <see cref="EnrichColumn.CellChanged"/>: the row and its new cell.</summary>
public sealed class EnrichColumnCellChangedEventArgs : RoutedEventArgs
{
    internal EnrichColumnCellChangedEventArgs(RoutedEvent routedEvent, EnrichColumnRow row, EnrichColumnCell cell)
        : base(routedEvent)
    {
        Row = row;
        Cell = cell;
    }

    /// <summary>The row whose cell changed.</summary>
    public EnrichColumnRow Row { get; }

    /// <summary>The new cell.</summary>
    public EnrichColumnCell Cell { get; }
}

/// <summary>Arguments of <see cref="EnrichColumn.RunCompleted"/>: every cell once a run settles.</summary>
public sealed class EnrichColumnRunCompletedEventArgs : RoutedEventArgs
{
    internal EnrichColumnRunCompletedEventArgs(RoutedEvent routedEvent, IReadOnlyDictionary<string, EnrichColumnCell> cells,
        bool stopped) : base(routedEvent)
    {
        Cells = cells;
        WasStopped = stopped;
    }

    /// <summary>Every row's cell, keyed by row id.</summary>
    public IReadOnlyDictionary<string, EnrichColumnCell> Cells { get; }

    /// <summary>True when the person stopped the run before it finished.</summary>
    public bool WasStopped { get; }
}

/// <summary>Arguments of <see cref="EnrichColumn.ColumnChanged"/>: the column the person saved.</summary>
public sealed class EnrichColumnColumnChangedEventArgs : RoutedEventArgs
{
    internal EnrichColumnColumnChangedEventArgs(RoutedEvent routedEvent, EnrichColumnDefinition column) : base(routedEvent)
    {
        Column = column;
    }

    /// <summary>The saved column.</summary>
    public EnrichColumnDefinition Column { get; }
}

/// <summary>Arguments of <see cref="EnrichColumn.SourceRequested"/>: the source link the person opened.</summary>
public sealed class EnrichColumnSourceRequestedEventArgs : RoutedEventArgs
{
    internal EnrichColumnSourceRequestedEventArgs(RoutedEvent routedEvent, EnrichColumnRow row, EnrichColumnSource source)
        : base(routedEvent)
    {
        Row = row;
        ValueSource = source;
    }

    /// <summary>The row whose source was opened.</summary>
    public EnrichColumnRow Row { get; }

    /// <summary>The value's source (named so it does not hide <see cref="RoutedEventArgs.Source"/>); set <see cref="RoutedEventArgs.Handled"/> to open it yourself.</summary>
    public EnrichColumnSource ValueSource { get; }
}
