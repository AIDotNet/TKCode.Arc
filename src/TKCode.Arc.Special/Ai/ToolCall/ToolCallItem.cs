using Avalonia;
using Avalonia.Collections;
using Avalonia.Media;
using Avalonia.Metadata;

namespace TKCode.Arc;

/// <summary>Where a tool call stands.</summary>
public enum ToolCallStatus
{
    /// <summary>The call is running: the title shimmers and a spinner turns.</summary>
    Running,

    /// <summary>The call finished: a check, the summary and the duration.</summary>
    Success,

    /// <summary>The call failed: the reason under the title and a Retry button.</summary>
    Error,

    /// <summary>The call waits for a person: an accent outline, the reason line and Deny / Allow.</summary>
    Approval,

    /// <summary>A person denied the call, so it never ran.</summary>
    Denied
}

/// <summary>One argument of a tool call, shown as a key and a value in the call's mono Input list.</summary>
public sealed class ToolCallArgument
{
    /// <summary>Creates an empty argument; set <see cref="Key"/> and <see cref="Value"/>.</summary>
    public ToolCallArgument()
    {
    }

    /// <summary>Creates an argument.</summary>
    /// <param name="key">The argument's name, such as "query".</param>
    /// <param name="value">The value as the model passed it; long values wrap.</param>
    public ToolCallArgument(string key, string value)
    {
        Key = key;
        Value = value;
    }

    /// <summary>The argument's name, such as "query".</summary>
    public string Key { get; set; } = string.Empty;

    /// <summary>The value as the model passed it. Long values wrap in the list; nothing is cut.</summary>
    public string Value { get; set; } = string.Empty;

    /// <inheritdoc />
    public override string ToString() => Key + ": " + Value;
}

/// <summary>
/// One tool call of an agent turn: which tool ran, what it did in plain words, how it went, its arguments and what came
/// back. Change <see cref="Status"/> (and the other properties) in place as the call moves on: a row keeps its place and
/// its open state, and every change reaches the screen. Keep <see cref="Id"/> stable.
/// </summary>
public sealed class ToolCallItem : AvaloniaObject
{
    /// <summary>A stable id. A group keys its rows by it, so a call whose status changes keeps its row.</summary>
    public static readonly StyledProperty<string> IdProperty =
        AvaloniaProperty.Register<ToolCallItem, string>(nameof(Id), string.Empty);

    /// <summary>The tool's name, read before the title by screen readers ("Search: Searched the web …").</summary>
    public static readonly StyledProperty<string> ToolNameProperty =
        AvaloniaProperty.Register<ToolCallItem, string>(nameof(ToolName), string.Empty);

    /// <summary>The tool's mark: a 24 × 24 stroked glyph such as a Lucide icon, drawn at 16px in the row and 12px in a
    /// group's mark stack. Null draws a wrench.</summary>
    public static readonly StyledProperty<Geometry?> ToolMarkProperty =
        AvaloniaProperty.Register<ToolCallItem, Geometry?>(nameof(ToolMark));

    /// <summary>The mark's color; null uses the text color, which follows the theme.</summary>
    public static readonly StyledProperty<IBrush?> ToolMarkBrushProperty =
        AvaloniaProperty.Register<ToolCallItem, IBrush?>(nameof(ToolMarkBrush));

    /// <summary>The settled verb phrase in the past tense, such as "Searched tickets in finance/close". One line; it
    /// truncates.</summary>
    public static readonly StyledProperty<string> TitleProperty =
        AvaloniaProperty.Register<ToolCallItem, string>(nameof(Title), string.Empty);

    /// <summary>The phrase while running, in the present tense ("Searching tickets in finance/close"); null uses
    /// <see cref="Title"/>.</summary>
    public static readonly StyledProperty<string?> RunningTitleProperty =
        AvaloniaProperty.Register<ToolCallItem, string?>(nameof(RunningTitle));

    /// <summary>The phrase while waiting for approval, as a request ("Post a status note to #finance-ops"); null uses
    /// <see cref="Title"/>.</summary>
    public static readonly StyledProperty<string?> ApprovalTitleProperty =
        AvaloniaProperty.Register<ToolCallItem, string?>(nameof(ApprovalTitle));

    /// <summary>The phrase after a failure ("Could not read the export job log"); null uses <see cref="Title"/>.</summary>
    public static readonly StyledProperty<string?> ErrorTitleProperty =
        AvaloniaProperty.Register<ToolCallItem, string?>(nameof(ErrorTitle));

    /// <summary>Where the call stands. Change it in place as the call runs, fails, waits or finishes.</summary>
    public static readonly StyledProperty<ToolCallStatus> StatusProperty =
        AvaloniaProperty.Register<ToolCallItem, ToolCallStatus>(nameof(Status), ToolCallStatus.Success);

    /// <summary>What came back: a list, a table, JSON or text. Shown under Output once the call succeeded.</summary>
    public static readonly StyledProperty<ToolCallOutput?> OutputProperty =
        AvaloniaProperty.Register<ToolCallItem, ToolCallOutput?>(nameof(Output));

    /// <summary>Quiet meta before the duration, such as "4 results". Null derives it from <see cref="Output"/> (results,
    /// rows, fields, items).</summary>
    public static readonly StyledProperty<string?> SummaryProperty =
        AvaloniaProperty.Register<ToolCallItem, string?>(nameof(Summary));

    /// <summary>One line under the title while the call failed (the reason) or waits for approval (what allowing will
    /// do). Not shown in the other statuses.</summary>
    public static readonly StyledProperty<string?> MessageProperty =
        AvaloniaProperty.Register<ToolCallItem, string?>(nameof(Message));

    /// <summary>How long the call took, shown as "760ms" or "1.2s"; a group adds up the successful ones.</summary>
    public static readonly StyledProperty<TimeSpan?> DurationProperty =
        AvaloniaProperty.Register<ToolCallItem, TimeSpan?>(nameof(Duration));

    /// <summary>Creates an empty call; set at least <see cref="Id"/>, <see cref="ToolName"/>, <see cref="Title"/> and
    /// <see cref="Status"/>.</summary>
    public ToolCallItem()
    {
    }

    /// <summary>Creates a call.</summary>
    /// <param name="id">A stable id.</param>
    /// <param name="toolName">The tool's name.</param>
    /// <param name="title">The settled verb phrase.</param>
    /// <param name="status">Where the call stands.</param>
    /// <param name="toolMark">The tool's 24 × 24 stroked glyph, or null for a wrench.</param>
    public ToolCallItem(string id, string toolName, string title, ToolCallStatus status, Geometry? toolMark = null)
    {
        Id = id;
        ToolName = toolName;
        Title = title;
        Status = status;
        ToolMark = toolMark;
    }

    /// <inheritdoc cref="IdProperty"/>
    public string Id { get => GetValue(IdProperty); set => SetValue(IdProperty, value); }

    /// <inheritdoc cref="ToolNameProperty"/>
    public string ToolName { get => GetValue(ToolNameProperty); set => SetValue(ToolNameProperty, value); }

    /// <inheritdoc cref="ToolMarkProperty"/>
    public Geometry? ToolMark { get => GetValue(ToolMarkProperty); set => SetValue(ToolMarkProperty, value); }

    /// <inheritdoc cref="ToolMarkBrushProperty"/>
    public IBrush? ToolMarkBrush { get => GetValue(ToolMarkBrushProperty); set => SetValue(ToolMarkBrushProperty, value); }

    /// <inheritdoc cref="TitleProperty"/>
    public string Title { get => GetValue(TitleProperty); set => SetValue(TitleProperty, value); }

    /// <inheritdoc cref="RunningTitleProperty"/>
    public string? RunningTitle { get => GetValue(RunningTitleProperty); set => SetValue(RunningTitleProperty, value); }

    /// <inheritdoc cref="ApprovalTitleProperty"/>
    public string? ApprovalTitle { get => GetValue(ApprovalTitleProperty); set => SetValue(ApprovalTitleProperty, value); }

    /// <inheritdoc cref="ErrorTitleProperty"/>
    public string? ErrorTitle { get => GetValue(ErrorTitleProperty); set => SetValue(ErrorTitleProperty, value); }

    /// <inheritdoc cref="StatusProperty"/>
    public ToolCallStatus Status { get => GetValue(StatusProperty); set => SetValue(StatusProperty, value); }

    /// <inheritdoc cref="OutputProperty"/>
    public ToolCallOutput? Output { get => GetValue(OutputProperty); set => SetValue(OutputProperty, value); }

    /// <inheritdoc cref="SummaryProperty"/>
    public string? Summary { get => GetValue(SummaryProperty); set => SetValue(SummaryProperty, value); }

    /// <inheritdoc cref="MessageProperty"/>
    public string? Message { get => GetValue(MessageProperty); set => SetValue(MessageProperty, value); }

    /// <inheritdoc cref="DurationProperty"/>
    public TimeSpan? Duration { get => GetValue(DurationProperty); set => SetValue(DurationProperty, value); }

    /// <summary>The arguments the model passed, in order; shown as a mono key and value list under Input.</summary>
    [Content]
    public AvaloniaList<ToolCallArgument> Input { get; } = new();

    /// <summary>The phrase for <paramref name="status"/>: the running, approval or error phrase when set, otherwise
    /// <see cref="Title"/>.</summary>
    public string TitleFor(ToolCallStatus status)
    {
        var phrase = status switch
        {
            ToolCallStatus.Running => RunningTitle,
            ToolCallStatus.Approval => ApprovalTitle,
            ToolCallStatus.Error => ErrorTitle,
            _ => null
        };
        return string.IsNullOrEmpty(phrase) ? Title : phrase;
    }

    /// <summary>The summary shown before the duration: <see cref="Summary"/>, or one derived from the output.</summary>
    public string? SummaryText(ToolCallLabels? labels = null) =>
        !string.IsNullOrEmpty(Summary) ? Summary : Output?.DeriveSummary(labels ?? ToolCallLabels.Default);

    /// <summary>Built-in sample data (<see cref="ToolCallSamples"/>): Allow and Retry with no handler are simulated.</summary>
    internal bool IsSample { get; set; }

    /// <summary>The simulation that drives this call, if any; actions nobody handled go to it.</summary>
    internal ToolCallSimulation? Simulation { get; set; }

    /// <summary>A fresh copy with the same content (a simulation replays its script from copies).</summary>
    internal ToolCallItem Copy()
    {
        var copy = new ToolCallItem
        {
            Id = Id,
            ToolName = ToolName,
            ToolMark = ToolMark,
            ToolMarkBrush = ToolMarkBrush,
            Title = Title,
            RunningTitle = RunningTitle,
            ApprovalTitle = ApprovalTitle,
            ErrorTitle = ErrorTitle,
            Status = Status,
            Output = Output,
            Summary = Summary,
            Message = Message,
            Duration = Duration,
            IsSample = IsSample
        };
        copy.Input.AddRange(Input);
        return copy;
    }

    /// <inheritdoc />
    public override string ToString() => ToolName + ": " + TitleFor(Status);
}
