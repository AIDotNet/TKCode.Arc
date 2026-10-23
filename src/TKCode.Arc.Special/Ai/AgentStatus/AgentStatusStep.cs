using Avalonia;
using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>
/// One step of an agent's run in an <see cref="AgentStatus"/>. List every step, pending ones included, so the progress
/// bar knows the total; the panel lists the latest finished and running ones. Write a running step's label in the present
/// tense and a finished one's in the past tense ("Running tests", then "Passed 12 of 12 tests"), so the words carry the
/// state. Changes reach the screen at once (each property is observed).
/// </summary>
public sealed class AgentStatusStep : AvaloniaObject
{
    /// <summary>A stable id for the step (keeps its row when the label changes).</summary>
    public static readonly StyledProperty<string?> IdProperty =
        AvaloniaProperty.Register<AgentStatusStep, string?>(nameof(Id));

    /// <summary>What the step does or did.</summary>
    public static readonly StyledProperty<string> LabelProperty =
        AvaloniaProperty.Register<AgentStatusStep, string>(nameof(Label), string.Empty);

    /// <summary>A 24 × 24 stroked glyph drawn at 16px before the label (a Lucide icon), or null.</summary>
    public static readonly StyledProperty<Geometry?> IconProperty =
        AvaloniaProperty.Register<AgentStatusStep, Geometry?>(nameof(Icon));

    /// <summary>Where the step is; pending steps only count in the progress bar.</summary>
    public static readonly StyledProperty<AgentStatusStepState> StateProperty =
        AvaloniaProperty.Register<AgentStatusStep, AgentStatusStepState>(nameof(State));

    /// <summary>When an active or waiting step started; its time counts in whole seconds from here, leaving out paused time.</summary>
    public static readonly StyledProperty<DateTimeOffset?> StartedAtProperty =
        AvaloniaProperty.Register<AgentStatusStep, DateTimeOffset?>(nameof(StartedAt));

    /// <summary>How long a finished step took; shown with one decimal ("1.9s").</summary>
    public static readonly StyledProperty<TimeSpan?> DurationProperty =
        AvaloniaProperty.Register<AgentStatusStep, TimeSpan?>(nameof(Duration));

    /// <summary>Creates an empty step.</summary>
    public AgentStatusStep()
    {
    }

    /// <summary>Creates a step.</summary>
    public AgentStatusStep(string id, string label, AgentStatusStepState state, Geometry? icon = null)
    {
        Id = id;
        Label = label;
        State = state;
        Icon = icon;
    }

    /// <inheritdoc cref="IdProperty"/>
    public string? Id { get => GetValue(IdProperty); set => SetValue(IdProperty, value); }

    /// <inheritdoc cref="LabelProperty"/>
    public string Label { get => GetValue(LabelProperty); set => SetValue(LabelProperty, value); }

    /// <inheritdoc cref="IconProperty"/>
    public Geometry? Icon { get => GetValue(IconProperty); set => SetValue(IconProperty, value); }

    /// <inheritdoc cref="StateProperty"/>
    public AgentStatusStepState State { get => GetValue(StateProperty); set => SetValue(StateProperty, value); }

    /// <inheritdoc cref="StartedAtProperty"/>
    public DateTimeOffset? StartedAt { get => GetValue(StartedAtProperty); set => SetValue(StartedAtProperty, value); }

    /// <inheritdoc cref="DurationProperty"/>
    public TimeSpan? Duration { get => GetValue(DurationProperty); set => SetValue(DurationProperty, value); }

    /// <inheritdoc />
    public override string ToString() => $"{State}: {Label}";
}
