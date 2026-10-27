using Avalonia;
using Avalonia.Interactivity;

namespace TKCode.Arc;

/// <summary>
/// A sample for a <see cref="LineReplay"/>: when it was taken (<paramref name="Time"/>, minutes on the block's clock),
/// what it measured (<paramref name="Value"/>), and optionally what a normal day measured then
/// (<paramref name="Baseline"/>), which the chart shows as a dashed line.
/// </summary>
public readonly record struct LineReplayPoint(double Time, double Value, double? Baseline = null);

/// <summary>The sort of event a <see cref="LineReplayMoment"/> marks, which decides its icon.</summary>
public enum LineReplayMomentKind
{
    /// <summary>Something went live (a rocket).</summary>
    Launch,

    /// <summary>Press or social coverage (a newspaper).</summary>
    Press,

    /// <summary>An email or newsletter went out (an envelope).</summary>
    Email,

    /// <summary>A problem (a warning triangle in the warning color); its row reports the drop in the 20 minutes after it.</summary>
    Incident,

    /// <summary>A goal was reached (a trophy).</summary>
    Milestone,
}

/// <summary>How a <see cref="LineReplay"/> looks before anyone has played it.</summary>
public enum LineReplayPosition
{
    /// <summary>Empty: nothing is drawn until the first play.</summary>
    Start,

    /// <summary>Complete: the full line and all moments are visible, and the button offers to play it again.</summary>
    End,
}

/// <summary>
/// An event to point out in a <see cref="LineReplay"/>, timed on the same clock as the data. It gets a row in the list of
/// moments, a tick on the slider, and a dot and a card on the chart once the replay gets there.
/// </summary>
public class LineReplayMoment : AvaloniaObject
{
    /// <summary>A stable id, reported back by <see cref="LineReplay.MomentReached"/>.</summary>
    public static readonly StyledProperty<string?> IdProperty =
        AvaloniaProperty.Register<LineReplayMoment, string?>(nameof(Id));

    /// <summary>The clock time of the event in minutes; it may fall between two samples.</summary>
    public static readonly StyledProperty<double> TimeProperty =
        AvaloniaProperty.Register<LineReplayMoment, double>(nameof(Time));

    /// <summary>A short name for the event ("Release notes published").</summary>
    public static readonly StyledProperty<string?> TitleProperty =
        AvaloniaProperty.Register<LineReplayMoment, string?>(nameof(Title));

    /// <summary>One optional line of detail under the title.</summary>
    public static readonly StyledProperty<string?> DetailProperty =
        AvaloniaProperty.Register<LineReplayMoment, string?>(nameof(Detail));

    /// <summary>The sort of event, which decides the icon (problems also use the warning color).</summary>
    public static readonly StyledProperty<LineReplayMomentKind> KindProperty =
        AvaloniaProperty.Register<LineReplayMoment, LineReplayMomentKind>(nameof(Kind));

    public LineReplayMoment()
    {
    }

    public LineReplayMoment(string id, double time, string title, string? detail = null,
        LineReplayMomentKind kind = LineReplayMomentKind.Launch)
    {
        Id = id;
        Time = time;
        Title = title;
        Detail = detail;
        Kind = kind;
    }

    /// <inheritdoc cref="IdProperty"/>
    public string? Id { get => GetValue(IdProperty); set => SetValue(IdProperty, value); }

    /// <inheritdoc cref="TimeProperty"/>
    public double Time { get => GetValue(TimeProperty); set => SetValue(TimeProperty, value); }

    /// <inheritdoc cref="TitleProperty"/>
    public string? Title { get => GetValue(TitleProperty); set => SetValue(TitleProperty, value); }

    /// <inheritdoc cref="DetailProperty"/>
    public string? Detail { get => GetValue(DetailProperty); set => SetValue(DetailProperty, value); }

    /// <inheritdoc cref="KindProperty"/>
    public LineReplayMomentKind Kind { get => GetValue(KindProperty); set => SetValue(KindProperty, value); }
}

/// <summary>Arguments of <see cref="LineReplay.MomentReached"/>.</summary>
public sealed class LineReplayMomentEventArgs : RoutedEventArgs
{
    public LineReplayMomentEventArgs(RoutedEvent routedEvent, LineReplayMoment moment) : base(routedEvent)
    {
        Moment = moment;
    }

    /// <summary>The moment playback just passed.</summary>
    public LineReplayMoment Moment { get; }
}
