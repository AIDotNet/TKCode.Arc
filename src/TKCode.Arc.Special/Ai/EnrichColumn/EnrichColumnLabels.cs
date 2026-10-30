using System.Globalization;

namespace TKCode.Arc;

/// <summary>
/// Every word <see cref="EnrichColumn"/> shows or says, in English by default. Replace the whole object to localize or
/// reword; counts and names are format functions.
/// </summary>
public sealed class EnrichColumnLabels
{
    private static readonly CultureInfo s_invariant = CultureInfo.InvariantCulture;

    /// <summary>The English defaults.</summary>
    public static EnrichColumnLabels Default { get; } = new();

    /// <summary>The header of the row column.</summary>
    public string RowHeader { get; init; } = "Company";

    /// <summary>The row count in the top bar: "7 rows".</summary>
    public Func<int, string> Rows { get; init; } = n => string.Create(s_invariant, $"{n} {(n == 1 ? "row" : "rows")}");

    /// <summary>The note after the row count when the column runs its built-in simulation.</summary>
    public string SampleNote { get; init; } = "sample data";

    /// <summary>The column button before anything has run.</summary>
    public string RunColumn { get; init; } = "Run column";

    /// <summary>The column button while it runs.</summary>
    public string Stop { get; init; } = "Stop";

    /// <summary>The column button once cells have values.</summary>
    public string Rerun { get; init; } = "Re-run";

    /// <summary>The name of the edit control on the column header.</summary>
    public string EditColumn { get; init; } = "Edit column";

    /// <summary>The editor's name field.</summary>
    public string ColumnName { get; init; } = "Column name";

    /// <summary>The editor's prompt field.</summary>
    public string Prompt { get; init; } = "Prompt";

    /// <summary>The editor's hint under the fields ("⌘ + Enter" on macOS).</summary>
    public string SaveHint { get; init; } =
        OperatingSystem.IsMacOS() ? "⌘ + Enter to save and run" : "Ctrl + Enter to save and run";

    /// <summary>The editor's save button.</summary>
    public string SaveAndRun { get; init; } = "Save and run";

    /// <summary>The editor's cancel button.</summary>
    public string Cancel { get; init; } = "Cancel";

    /// <summary>A cell that has not run, for screen readers (the cell shows a dash).</summary>
    public string NotRun { get; init; } = "Not run";

    /// <summary>A cell waiting for a worker.</summary>
    public string Queued { get; init; } = "Queued";

    /// <summary>The running label before the enrich delegate sets one: "Researching Lumen Freight".</summary>
    public Func<EnrichColumnRow, string> Working { get; init; } = row => "Researching " + row.Name;

    /// <summary>A failed cell's label.</summary>
    public string Failed { get; init; } = "Failed";

    /// <summary>The reason of a failure that came without a message.</summary>
    public string UnknownError { get; init; } = "Something went wrong";

    /// <summary>The name of a row's run control: "Run Lumen Freight".</summary>
    public Func<string, string> RunRow { get; init; } = name => "Run " + name;

    /// <summary>The name of a failed row's retry control: "Retry Lumen Freight".</summary>
    public Func<string, string> RetryRow { get; init; } = name => "Retry " + name;

    /// <summary>The name of a source link: "lumenfreight.example, opens in a new tab".</summary>
    public Func<string, string> OpenSource { get; init; } = label => label + ", opens in a new tab";

    /// <summary>The footer's progress: "5 of 7 enriched".</summary>
    public Func<int, int, string> Progress { get; init; } =
        (done, total) => string.Create(s_invariant, $"{done} of {total} enriched");

    /// <summary>The footer's failure count: "1 failed".</summary>
    public Func<int, string> FailedCount { get; init; } = n => string.Create(s_invariant, $"{n} failed");

    /// <summary>Announced when a run starts: "Enriching 7 rows."</summary>
    public Func<int, string> AnnounceRun { get; init; } =
        n => string.Create(s_invariant, $"Enriching {n} {(n == 1 ? "row" : "rows")}.");

    /// <summary>Announced when the person stops a run: "Stopped. 4 of 7 rows enriched."</summary>
    public Func<int, int, string> AnnounceStopped { get; init; } =
        (done, total) => string.Create(s_invariant, $"Stopped. {done} of {total} rows enriched.");

    /// <summary>Announced when a run finishes: "Done. 6 of 7 rows enriched, 1 failed."</summary>
    public Func<int, int, int, string> AnnounceFinished { get; init; } = (done, failed, total) => failed > 0
        ? string.Create(s_invariant, $"Done. {done} of {total} rows enriched, {failed} failed.")
        : string.Create(s_invariant, $"Done. {done} of {total} rows enriched.");

    /// <summary>Added to the run announcement when the values come from the built-in simulation.</summary>
    public string AnnounceSimulated { get; init; } = "Values are simulated; nothing is sent.";
}
