using Avalonia.Interactivity;
using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>Where an agent's run is, as an <see cref="AgentStatus"/> shows it.</summary>
public enum AgentStatusRunState
{
    /// <summary>Nothing runs: the pill reads "Ready" and shows only the agent's mark.</summary>
    Idle,

    /// <summary>The agent works: the ring spins, the activity shimmers and the timer counts.</summary>
    Working,

    /// <summary>The agent waits for a person: an accent dot, the request as the label, and Approve and Deny.</summary>
    NeedsInput,

    /// <summary>The run is paused: the ring and the timer freeze and the label reads "Paused".</summary>
    Paused,

    /// <summary>The run finished: the ring closes around a check and the label reads "Done in …".</summary>
    Done,

    /// <summary>The run failed: the ring closes around a cross, the label is the failure, and Retry shows.</summary>
    Failed,

    /// <summary>The run was stopped: a small square and "Stopped after …".</summary>
    Stopped
}

/// <summary>Where one step of a run is.</summary>
public enum AgentStatusStepState
{
    /// <summary>Not started; counts in the progress bar but is not listed.</summary>
    Pending,

    /// <summary>Running now; its label shimmers and its time counts in whole seconds.</summary>
    Active,

    /// <summary>Waiting for a person (an approval); its icon takes the accent color and its time counts.</summary>
    Waiting,

    /// <summary>Finished; listed with its duration ("1.9s").</summary>
    Done,

    /// <summary>Failed; its icon and segment take the danger color.</summary>
    Failed,

    /// <summary>Skipped or cancelled; listed in the muted color without a duration.</summary>
    Skipped
}

/// <summary>
/// The actions an <see cref="AgentStatus"/> may offer. A button appears only while its action is allowed here and fits the
/// run's state (Pause while working, Resume while paused, Stop while working or paused, Approve and Deny while input is
/// needed, Retry after a failure, Open run always); this is the counterpart of showing a control only when its handler
/// exists.
/// </summary>
[Flags]
public enum AgentStatusActions
{
    /// <summary>No action buttons.</summary>
    None = 0,

    /// <summary>Pause in the panel while working.</summary>
    Pause = 1,

    /// <summary>Resume in the panel while paused (the same button as Pause, so focus stays put).</summary>
    Resume = 2,

    /// <summary>Stop in the panel while working or paused.</summary>
    Stop = 4,

    /// <summary>Approve, inline and in the panel, while input is needed.</summary>
    Approve = 8,

    /// <summary>Deny, inline and in the panel, while input is needed.</summary>
    Deny = 16,

    /// <summary>Retry, inline and in the panel, after a failure.</summary>
    Retry = 32,

    /// <summary>Open run in the panel, for a full log or trace view.</summary>
    OpenRun = 64,

    /// <summary>Every action.</summary>
    All = Pause | Resume | Stop | Approve | Deny | Retry | OpenRun
}

/// <summary>Which edge of the pill the panel grows from.</summary>
public enum AgentStatusPanelAlignment
{
    /// <summary>The panel's left edge stays on the pill's left edge (a pill against the start of a header).</summary>
    Start,

    /// <summary>The panel grows evenly to both sides of the pill's center.</summary>
    Center,

    /// <summary>The panel's right edge stays on the pill's right edge (a pill against the end of a header).</summary>
    End
}

/// <summary>
/// What an agent is doing now: a short present-tense label ("Reading 12 files"), an optional icon and an optional count
/// that ticks in place ("8 of 12"). While input is needed the label is the request, when done it is the outcome that
/// follows "Done in …", and after a failure it is the failure. Immutable: set a new instance to change it; a new label
/// or icon rolls in, a changed count only changes its digits.
/// </summary>
public sealed class AgentStatusActivity
{
    /// <summary>Creates an empty activity; set <see cref="Label"/>.</summary>
    public AgentStatusActivity()
    {
    }

    /// <summary>Creates an activity.</summary>
    /// <param name="label">What the agent is doing, in the present tense.</param>
    /// <param name="icon">A 24 × 24 stroked glyph (a Lucide icon), or null.</param>
    /// <param name="progressValue">The count done so far, shown as "value of total" with <paramref name="progressTotal"/>.</param>
    /// <param name="progressTotal">The count's total.</param>
    public AgentStatusActivity(string label, Geometry? icon = null, int? progressValue = null, int? progressTotal = null)
    {
        Label = label;
        Icon = icon;
        ProgressValue = progressValue;
        ProgressTotal = progressTotal;
    }

    /// <summary>What the agent is doing, in the present tense; the request, the outcome or the failure in those states.</summary>
    public string Label { get; init; } = string.Empty;

    /// <summary>A 24 × 24 stroked glyph drawn at 16px before the label, or null for none.</summary>
    public Geometry? Icon { get; init; }

    /// <summary>The count done so far; shown only together with <see cref="ProgressTotal"/>.</summary>
    public int? ProgressValue { get; init; }

    /// <summary>The count's total; shown only together with <see cref="ProgressValue"/>.</summary>
    public int? ProgressTotal { get; init; }

    /// <summary>True when both parts of the count are set.</summary>
    public bool HasProgress => ProgressValue is not null && ProgressTotal is not null;

    /// <summary>A copy with another count value (the label and icon stay, so the pill only changes digits).</summary>
    public AgentStatusActivity WithProgress(int value) => new(Label, Icon, value, ProgressTotal);

    /// <summary>True when <paramref name="other"/> shows the same label and icon, whatever its count.</summary>
    public bool IsSameActivity(AgentStatusActivity? other) =>
        other is not null && other.Label == Label && ReferenceEquals(other.Icon, Icon);

    /// <inheritdoc />
    public override string ToString() => Label;
}

/// <summary>Arguments of the action events of an <see cref="AgentStatus"/> (Pause, Resume, Stop, Approve, Deny, Retry, Open run).</summary>
public sealed class AgentStatusActionEventArgs : RoutedEventArgs
{
    /// <summary>Creates the arguments.</summary>
    public AgentStatusActionEventArgs(RoutedEvent routedEvent, AgentStatusActions action, AgentStatusRunState state)
        : base(routedEvent)
    {
        Action = action;
        State = state;
    }

    /// <summary>The action that was pressed (one flag).</summary>
    public AgentStatusActions Action { get; }

    /// <summary>The run's state when it was pressed.</summary>
    public AgentStatusRunState State { get; }
}

/// <summary>Arguments of <see cref="AgentStatus.ExpandedChangedEvent"/>.</summary>
public sealed class AgentStatusExpandedChangedEventArgs : RoutedEventArgs
{
    /// <summary>Creates the arguments.</summary>
    public AgentStatusExpandedChangedEventArgs(RoutedEvent routedEvent, bool isExpanded)
        : base(routedEvent)
    {
        IsExpanded = isExpanded;
    }

    /// <summary>True when the panel opened, false when it closed.</summary>
    public bool IsExpanded { get; }
}
