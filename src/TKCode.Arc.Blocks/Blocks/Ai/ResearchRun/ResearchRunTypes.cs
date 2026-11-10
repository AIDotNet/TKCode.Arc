using Avalonia;
using Avalonia.Interactivity;

namespace TKCode.Arc;

/// <summary>Where a <see cref="ResearchRun"/> stands.</summary>
public enum ResearchRunPhase
{
    /// <summary>Not started.</summary>
    Idle,

    /// <summary>Breaking the question into a plan.</summary>
    Planning,

    /// <summary>Reading sources and taking notes.</summary>
    Reading,

    /// <summary>Writing the report from the notes.</summary>
    Writing,

    /// <summary>The report is ready.</summary>
    Done,

    /// <summary>Stopped by the person before the report was ready.</summary>
    Stopped
}

/// <summary>Where a source of a <see cref="ResearchRun"/> stands.</summary>
public enum ResearchRunSourceStatus
{
    /// <summary>Found, not read yet.</summary>
    Queued,

    /// <summary>Being read; <see cref="ResearchRunSource.Progress"/> shows how far.</summary>
    Reading,

    /// <summary>Read.</summary>
    Read,

    /// <summary>Left out (unreachable, off topic or not trusted); <see cref="AiSource.Snippet"/> may say why.</summary>
    Skipped
}

/// <summary>
/// A source of a <see cref="ResearchRun"/>: an <see cref="AiSource"/> (title, domain, address, number) with how far it has
/// been read. Change <see cref="Status"/> and <see cref="Progress"/> while the run is on screen and its row follows.
/// </summary>
public class ResearchRunSource : AiSource
{
    /// <summary>Queued, reading, read or skipped.</summary>
    public static readonly StyledProperty<ResearchRunSourceStatus> StatusProperty =
        AvaloniaProperty.Register<ResearchRunSource, ResearchRunSourceStatus>(nameof(Status));

    /// <summary>How much of the source has been read, from 0 to 1.</summary>
    public static readonly StyledProperty<double> ProgressProperty =
        AvaloniaProperty.Register<ResearchRunSource, double>(nameof(Progress));

    /// <inheritdoc cref="StatusProperty"/>
    public ResearchRunSourceStatus Status { get => GetValue(StatusProperty); set => SetValue(StatusProperty, value); }

    /// <inheritdoc cref="ProgressProperty"/>
    public double Progress { get => GetValue(ProgressProperty); set => SetValue(ProgressProperty, value); }
}

/// <summary>A note taken while reading: a finding and the numbers of the sources it rests on.</summary>
/// <param name="Text">The finding, in a sentence or two.</param>
/// <param name="Sources">The <see cref="AiSource.Index"/> numbers it cites.</param>
public sealed record ResearchRunNote(string Text, IReadOnlyList<int> Sources);

/// <summary>Event data of <see cref="ResearchRun.SourceInvoked"/>: the source the person opened.</summary>
public sealed class ResearchRunSourceEventArgs(RoutedEvent routedEvent, ResearchRunSource source) : RoutedEventArgs(routedEvent)
{
    /// <summary>The source.</summary>
    public ResearchRunSource ResearchSource { get; } = source;
}
