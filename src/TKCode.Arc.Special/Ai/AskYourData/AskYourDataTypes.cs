using Avalonia.Interactivity;
using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>How an <see cref="AskYourData"/> answer is drawn.</summary>
public enum AskYourDataChartKind
{
    /// <summary>Bars: stacked when the result's series add up, side by side when they do not.</summary>
    Bar,

    /// <summary>One line per series.</summary>
    Line,

    /// <summary>A table with a row per category and a column per series.</summary>
    Table
}

/// <summary>What follows each label in an <see cref="AskYourData"/> legend.</summary>
public enum AskYourDataLegendValue
{
    /// <summary>The series total, for flows such as signups.</summary>
    Sum,

    /// <summary>The latest value, for levels such as MRR.</summary>
    Last,

    /// <summary>Nothing.</summary>
    None
}

/// <summary>Where an <see cref="AskYourData"/> run is.</summary>
public enum AskYourDataStage
{
    /// <summary>Nothing asked yet: the empty state with the source and the suggestions.</summary>
    Idle,

    /// <summary>The model is writing the SQL, which streams into the Generated SQL panel.</summary>
    Writing,

    /// <summary>The query runs; the rows scanned roll in.</summary>
    Running,

    /// <summary>The answer is charted.</summary>
    Done,

    /// <summary>The query matched no rows: a message and the suggestions.</summary>
    Empty,

    /// <summary>Writing or running failed: the error verbatim and Try again.</summary>
    Error,

    /// <summary>The person stopped the run.</summary>
    Stopped
}

/// <summary>Which step of an <see cref="AskYourData"/> run failed.</summary>
public enum AskYourDataErrorStage
{
    /// <summary>Writing the SQL.</summary>
    Sql,

    /// <summary>Running the query.</summary>
    Query
}

/// <summary>The database an <see cref="AskYourData"/> question runs against.</summary>
public sealed class AskYourDataSource
{
    /// <summary>The name shown in the field and the empty state, such as "analytics". Empty shows no name.</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>The SQL dialect, which leads the SQL meta ("PostgreSQL · 48 rows · 214ms"). Null leaves it out.</summary>
    public string? Dialect { get; init; }

    /// <summary>A 24 × 24 stroked glyph for the source; a database cylinder when null.</summary>
    public Geometry? Mark { get; init; }
}

/// <summary>One series of an <see cref="AskYourDataResult"/>: a plan, a region or a metric with one value per category.</summary>
public sealed class AskYourDataSeries
{
    /// <summary>A stable key for the series.</summary>
    public string Id { get; init; } = string.Empty;

    /// <summary>The name in the legend, the readout and the table header.</summary>
    public string Label { get; init; } = string.Empty;

    /// <summary>One value per category, in the order of <see cref="AskYourDataResult.Categories"/>.</summary>
    public IReadOnlyList<double> Values { get; init; } = [];

    /// <summary>The series color; the Series1 to Series4 tokens in order when null.</summary>
    public Color? Color { get; init; }
}

/// <summary>What <see cref="AskYourData.RunQuery"/> returns, shaped for the chart.</summary>
public sealed class AskYourDataResult
{
    /// <summary>The chart title, such as "Weekly signups by plan".</summary>
    public string Title { get; init; } = string.Empty;

    /// <summary>A quiet line under the title, such as "Aug 3 to Oct 25 · 1,284 signups".</summary>
    public string? Subtitle { get; init; }

    /// <summary>The X axis labels in order, such as week starts. Labels thin out on narrow cards.</summary>
    public IReadOnlyList<string> Categories { get; init; } = [];

    /// <summary>One entry per plan, region or metric, each with one value per category.</summary>
    public IReadOnlyList<AskYourDataSeries> Series { get; init; } = [];

    /// <summary>
    /// Whether the series add up (default true): bars stack, and the legend, readout and table show totals. Set false for
    /// rates and averages; bars then group side by side.
    /// </summary>
    public bool Stacked { get; init; } = true;

    /// <summary>What follows each legend label; <see cref="AskYourDataLegendValue.Sum"/> when stacked, else none, when null.</summary>
    public AskYourDataLegendValue? LegendValue { get; init; }

    /// <summary>The view this result opens on; the card's <see cref="AskYourData.DefaultChart"/> when null.</summary>
    public AskYourDataChartKind? Chart { get; init; }

    /// <summary>The header of the table's first column, such as "Week"; "Category" when null.</summary>
    public string? CategoryLabel { get; init; }

    /// <summary>Formats axis ticks, legend values, the readout and table cells (such as $258k or 3.9%); en-US grouping when null.</summary>
    public Func<double, string>? FormatValue { get; init; }

    /// <summary>One or two sentences that read the chart. It streams in under it with its final height reserved.</summary>
    public string? Insight { get; init; }

    /// <summary>The rows the query returned, shown in the SQL meta as "48 rows".</summary>
    public long? RowCount { get; init; }

    /// <summary>The query time in milliseconds, shown as 214ms or 1.2s.</summary>
    public double? DurationMs { get; init; }

    /// <summary>Whether the result has anything to chart.</summary>
    public bool HasData => Categories.Count > 0 && Series.Count > 0;
}

/// <summary>What <see cref="AskYourData.GenerateSql"/> and <see cref="AskYourData.StreamSql"/> receive.</summary>
public sealed class AskYourDataSqlRequest
{
    /// <summary>Creates the request.</summary>
    public AskYourDataSqlRequest(string question, AskYourDataSource source, CancellationToken cancellationToken)
    {
        Question = question;
        Source = source;
        CancellationToken = cancellationToken;
    }

    /// <summary>The question as asked.</summary>
    public string Question { get; }

    /// <summary>The database it runs against.</summary>
    public AskYourDataSource Source { get; }

    /// <summary>Cancelled when the person presses Stop or asks again. Honour it.</summary>
    public CancellationToken CancellationToken { get; }
}

/// <summary>What <see cref="AskYourData.RunQuery"/> receives.</summary>
public sealed class AskYourDataQueryRequest
{
    private readonly Action<long, long?> _progress;

    /// <summary>Creates the request; <paramref name="progress"/> receives the rows scanned and the total, if known.</summary>
    public AskYourDataQueryRequest(string sql, string question, CancellationToken cancellationToken, Action<long, long?> progress)
    {
        Sql = sql;
        Question = question;
        CancellationToken = cancellationToken;
        _progress = progress;
    }

    /// <summary>The SQL to run (read only).</summary>
    public string Sql { get; }

    /// <summary>The question it answers.</summary>
    public string Question { get; }

    /// <summary>Cancelled when the person presses Stop or asks again. Honour it.</summary>
    public CancellationToken CancellationToken { get; }

    /// <summary>
    /// Rolls the count in the running state: the rows scanned so far and, when known, the total (which reserves its
    /// width). Safe from any thread; batch very fast updates to a few per second.
    /// </summary>
    public void ReportProgress(long rowsScanned, long? totalRows = null) => _progress(rowsScanned, totalRows);
}

/// <summary>Arguments of <see cref="AskYourData.ResultReady"/>.</summary>
public sealed class AskYourDataResultEventArgs : RoutedEventArgs
{
    /// <summary>Creates the arguments.</summary>
    public AskYourDataResultEventArgs(RoutedEvent routedEvent, AskYourDataResult result, string question, string sql)
        : base(routedEvent)
    {
        Result = result;
        Question = question;
        Sql = sql;
    }

    /// <summary>The result that was charted.</summary>
    public AskYourDataResult Result { get; }

    /// <summary>The question it answers.</summary>
    public string Question { get; }

    /// <summary>The SQL that produced it.</summary>
    public string Sql { get; }
}

/// <summary>Arguments of <see cref="AskYourData.Failed"/>.</summary>
public sealed class AskYourDataErrorEventArgs : RoutedEventArgs
{
    /// <summary>Creates the arguments.</summary>
    public AskYourDataErrorEventArgs(RoutedEvent routedEvent, Exception error, string question, AskYourDataErrorStage stage)
        : base(routedEvent)
    {
        Error = error;
        Question = question;
        Stage = stage;
    }

    /// <summary>What was thrown.</summary>
    public Exception Error { get; }

    /// <summary>The question that failed.</summary>
    public string Question { get; }

    /// <summary>Whether writing the SQL or running it failed.</summary>
    public AskYourDataErrorStage Stage { get; }
}
