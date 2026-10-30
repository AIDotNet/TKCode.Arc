namespace TKCode.Arc;

/// <summary>
/// Every word <see cref="AskYourData"/> shows or says, in English by default. Replace the whole object to localize or
/// reword; counts and times are format functions.
/// </summary>
public sealed class AskYourDataLabels
{
    /// <summary>The English defaults.</summary>
    public static AskYourDataLabels Default { get; } = new();

    /// <summary>The name of the question field.</summary>
    public string Question { get; init; } = "Question";

    /// <summary>The Ask button.</summary>
    public string Ask { get; init; } = "Ask";

    /// <summary>The Ask button while a run is in progress (it stops it).</summary>
    public string Stop { get; init; } = "Stop";

    /// <summary>The empty state's line, from the source name: "Ask a question about analytics".</summary>
    public Func<string, string> EmptyTitle { get; init; } = source =>
        string.IsNullOrEmpty(source) ? "Ask a question about your data" : "Ask a question about " + source;

    /// <summary>The empty state's quiet line.</summary>
    public string EmptyHint { get; init; } = "Plain words in, a chart out. The SQL stays one click away.";

    /// <summary>The heading over the suggestions.</summary>
    public string Suggestions { get; init; } = "Try";

    /// <summary>The title of the SQL panel's row.</summary>
    public string GeneratedSql { get; init; } = "Generated SQL";

    /// <summary>The SQL meta while the model writes.</summary>
    public string WritingMeta { get; init; } = "writing";

    /// <summary>The SQL meta while the query runs.</summary>
    public string RunningMeta { get; init; } = "running";

    /// <summary>The name of the Copy SQL button.</summary>
    public string CopySql { get; init; } = "Copy SQL";

    /// <summary>The name of the scrollable code well.</summary>
    public string SqlCode { get; init; } = "SQL query";

    /// <summary>The stage header while the model writes.</summary>
    public string WritingSql { get; init; } = "Writing SQL";

    /// <summary>The stage header while the query runs.</summary>
    public string RunningQuery { get; init; } = "Running query";

    /// <summary>The rows scanned after the rolling count: "rows scanned".</summary>
    public string RowsScanned { get; init; } = "rows scanned";

    /// <summary>The stage header when the query matched no rows.</summary>
    public string NoData { get; init; } = "No rows matched";

    /// <summary>The quiet line under <see cref="NoData"/>.</summary>
    public string NoDataHint { get; init; } = "The query ran but returned nothing to chart. Try one of these:";

    /// <summary>The stage header when writing the SQL failed.</summary>
    public string SqlFailed { get; init; } = "Couldn't write the SQL";

    /// <summary>The stage header when running the query failed.</summary>
    public string QueryFailed { get; init; } = "The query failed";

    /// <summary>The stage header after the person stopped the run.</summary>
    public string Stopped { get; init; } = "Stopped";

    /// <summary>The quiet line under <see cref="Stopped"/>.</summary>
    public string StoppedHint { get; init; } = "Nothing was charted. Ask again when you are ready.";

    /// <summary>The Try again button of a failed run.</summary>
    public string TryAgain { get; init; } = "Try again";

    /// <summary>The chart switch's name and its options.</summary>
    public string ChartView { get; init; } = "Chart view";

    /// <summary>The bar view.</summary>
    public string Bars { get; init; } = "Bars";

    /// <summary>The line view.</summary>
    public string Lines { get; init; } = "Lines";

    /// <summary>The table view.</summary>
    public string Table { get; init; } = "Table";

    /// <summary>The total in the readout and the table's last column.</summary>
    public string Total { get; init; } = "Total";

    /// <summary>The table's first column when the result names none.</summary>
    public string Category { get; init; } = "Category";

    /// <summary>The rows the query returned: "48 rows", "1 row".</summary>
    public Func<long, string> Rows { get; init; } = rows => rows == 1 ? "1 row" : AskYourDataFormat.Grouped(rows) + " rows";

    /// <summary>The query time from milliseconds: "214ms", "1.2s".</summary>
    public Func<double, string> Duration { get; init; } = AskYourDataFormat.Duration;

    /// <summary>The live region after a simulated run.</summary>
    public string Simulated { get; init; } = "Simulated with sample data, nothing was queried.";

    /// <summary>The name of the chart, from the result title: "Weekly signups by plan, chart".</summary>
    public Func<string, string> ChartName { get; init; } = title => title + ", chart";

    /// <summary>The hint of the chart group.</summary>
    public string ChartHint { get; init; } = "Use the arrow keys to read each value.";
}
