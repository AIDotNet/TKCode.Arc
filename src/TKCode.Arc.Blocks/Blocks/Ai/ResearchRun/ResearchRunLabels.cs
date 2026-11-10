namespace TKCode.Arc;

/// <summary>
/// Every word a <see cref="ResearchRun"/> shows or reads out, with English defaults. Words built from values are
/// functions, so an app can apply its own grammar. Assign a new instance to <see cref="ResearchRun.Labels"/> to change them.
/// </summary>
public sealed class ResearchRunLabels
{
    /// <summary>Accessible name of the run ("Research").</summary>
    public string Region { get; init; } = "Research";

    /// <summary>The names of the four stages in the stepper: "Plan", "Read", "Write", "Report".</summary>
    public IReadOnlyList<string> Stages { get; init; } = ["Plan", "Read", "Write", "Report"];

    /// <summary>The phase words for the status line and screen readers.</summary>
    public Func<ResearchRunPhase, string> Phase { get; init; } = static phase => phase switch
    {
        ResearchRunPhase.Planning => "Making a plan",
        ResearchRunPhase.Reading => "Reading sources",
        ResearchRunPhase.Writing => "Writing the report",
        ResearchRunPhase.Done => "Report ready",
        ResearchRunPhase.Stopped => "Stopped",
        _ => "Not started"
    };

    /// <summary>The section headings: "Plan", "Sources", "Notes", "Report".</summary>
    public string PlanHeading { get; init; } = "Plan";

    /// <inheritdoc cref="PlanHeading"/>
    public string SourcesHeading { get; init; } = "Sources";

    /// <inheritdoc cref="PlanHeading"/>
    public string NotesHeading { get; init; } = "Notes";

    /// <inheritdoc cref="PlanHeading"/>
    public string ReportHeading { get; init; } = "Report";

    /// <summary>The count next to the sources heading, from read and found ("4 of 7 read").</summary>
    public Func<int, int, string> SourcesCount { get; init; } = static (read, found) => $"{read} of {found} read";

    /// <summary>The status of a source ("Queued", "Reading", "Read", "Skipped").</summary>
    public Func<ResearchRunSourceStatus, string> SourceStatus { get; init; } = static status => status switch
    {
        ResearchRunSourceStatus.Reading => "Reading",
        ResearchRunSourceStatus.Read => "Read",
        ResearchRunSourceStatus.Skipped => "Skipped",
        _ => "Queued"
    };

    /// <summary>The accessible name of a source row, from number, title, domain and status ("Source 2: title, domain, Read").</summary>
    public Func<int, string, string?, string, string> SourceName { get; init; } =
        static (index, title, domain, status) => domain is null ? $"Source {index}: {title}, {status}" : $"Source {index}: {title}, {domain}, {status}";

    /// <summary>The accessible name of a citation in the report ("Source 3: title").</summary>
    public Func<int, string, string> CitationName { get; init; } = static (index, title) => $"Source {index}: {title}";

    /// <summary>The time the research has taken ("1m 05s").</summary>
    public Func<TimeSpan, string> Elapsed { get; init; } = AiAgentFormat.Elapsed;

    /// <summary>The line that stands for the process once the report is ready, from sources read, notes and time ("Read 6 sources, took 9 notes in 1m 05s").</summary>
    public Func<int, int, string, string> Summary { get; init; } =
        static (sources, notes, time) => $"Read {sources} sources, took {notes} notes in {time}";

    /// <summary>The toggles of the process under a finished report: "Show process" and "Hide process".</summary>
    public string ShowProcess { get; init; } = "Show process";

    /// <inheritdoc cref="ShowProcess"/>
    public string HideProcess { get; init; } = "Hide process";

    /// <summary>The report's place while it is written ("The report appears here once the sources are read.").</summary>
    public string ReportPending { get; init; } = "The report appears here once the sources are read.";

    /// <summary>The report's place after a stop ("Stopped before the report was written. The notes so far are kept.").</summary>
    public string ReportStopped { get; init; } = "Stopped before the report was written. The notes so far are kept.";

    /// <summary>The buttons: "Stop", "Research again".</summary>
    public string Stop { get; init; } = "Stop";

    /// <inheritdoc cref="Stop"/>
    public string Restart { get; init; } = "Research again";

    /// <summary>What screen readers hear when a phase starts or the report is ready (the phase word).</summary>
    public Func<ResearchRunPhase, string> PhaseAnnouncement { get; init; } = static phase => phase switch
    {
        ResearchRunPhase.Done => "Report ready",
        ResearchRunPhase.Stopped => "Research stopped",
        ResearchRunPhase.Planning => "Making a plan",
        ResearchRunPhase.Reading => "Reading sources",
        ResearchRunPhase.Writing => "Writing the report",
        _ => string.Empty
    };
}
