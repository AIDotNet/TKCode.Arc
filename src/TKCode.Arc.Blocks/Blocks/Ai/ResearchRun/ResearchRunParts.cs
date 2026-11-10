using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>How a stage of the stepper or a plan step reads.</summary>
internal enum ResearchRunStageState
{
    Upcoming,
    Current,
    Done,
    Stopped
}

/// <summary>One stage of the <see cref="ResearchRun"/> stepper: a connector, a dot and a word.</summary>
[PseudoClasses(CurrentPseudoClass, DonePseudoClass, StoppedPseudoClass, FirstPseudoClass)]
internal sealed class ResearchRunStage : Avalonia.Controls.Primitives.TemplatedControl
{
    public const string CurrentPseudoClass = ":current";
    public const string DonePseudoClass = ":done";
    public const string StoppedPseudoClass = ":stopped";
    public const string FirstPseudoClass = ":first";

    public static readonly StyledProperty<string?> TextProperty =
        AvaloniaProperty.Register<ResearchRunStage, string?>(nameof(Text));

    public static readonly StyledProperty<ResearchRunStageState> StateProperty =
        AvaloniaProperty.Register<ResearchRunStage, ResearchRunStageState>(nameof(State));

    public static readonly StyledProperty<bool> IsFirstProperty =
        AvaloniaProperty.Register<ResearchRunStage, bool>(nameof(IsFirst));

    public string? Text { get => GetValue(TextProperty); set => SetValue(TextProperty, value); }

    public ResearchRunStageState State { get => GetValue(StateProperty); set => SetValue(StateProperty, value); }

    public bool IsFirst { get => GetValue(IsFirstProperty); set => SetValue(IsFirstProperty, value); }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == StateProperty || change.Property == TextProperty)
        {
            var state = State;
            PseudoClasses.Set(CurrentPseudoClass, state == ResearchRunStageState.Current);
            PseudoClasses.Set(DonePseudoClass, state == ResearchRunStageState.Done);
            PseudoClasses.Set(StoppedPseudoClass, state == ResearchRunStageState.Stopped);
            AutomationProperties.SetName(this, Text);
        }
        else if (change.Property == IsFirstProperty)
        {
            PseudoClasses.Set(FirstPseudoClass, change.GetNewValue<bool>());
        }
    }
}

/// <summary>
/// A source of a <see cref="ResearchRun"/>: its mark, title, site and status, with a reading line along the bottom that
/// fills as the source is read. Pressing it opens the source.
/// </summary>
[PseudoClasses(ReadingPseudoClass, ReadPseudoClass, SkippedPseudoClass)]
internal sealed class ResearchRunSourceRow : AiAgentButton
{
    public const string ReadingPseudoClass = ":reading";
    public const string ReadPseudoClass = ":read";
    public const string SkippedPseudoClass = ":skipped";

    public static readonly StyledProperty<ResearchRunSource?> SourceProperty =
        AvaloniaProperty.Register<ResearchRunSourceRow, ResearchRunSource?>(nameof(Source));

    public static readonly StyledProperty<string?> StatusTextProperty =
        AvaloniaProperty.Register<ResearchRunSourceRow, string?>(nameof(StatusText));

    public static readonly StyledProperty<double> ProgressProperty =
        AvaloniaProperty.Register<ResearchRunSourceRow, double>(nameof(Progress));

    public ResearchRunSource? Source { get => GetValue(SourceProperty); set => SetValue(SourceProperty, value); }

    public string? StatusText { get => GetValue(StatusTextProperty); set => SetValue(StatusTextProperty, value); }

    public double Progress { get => GetValue(ProgressProperty); set => SetValue(ProgressProperty, value); }

    public static readonly StyledProperty<string?> TitleProperty =
        AvaloniaProperty.Register<ResearchRunSourceRow, string?>(nameof(Title));

    public static readonly StyledProperty<string?> DomainProperty =
        AvaloniaProperty.Register<ResearchRunSourceRow, string?>(nameof(Domain));

    public string? Title { get => GetValue(TitleProperty); set => SetValue(TitleProperty, value); }

    public string? Domain { get => GetValue(DomainProperty); set => SetValue(DomainProperty, value); }

    public ResearchRun? Owner { get; set; }

    public void Refresh()
    {
        if (Source is not { } source)
        {
            return;
        }
        var labels = Owner?.Labels ?? new ResearchRunLabels();
        var status = source.Status;
        PseudoClasses.Set(ReadingPseudoClass, status == ResearchRunSourceStatus.Reading);
        PseudoClasses.Set(ReadPseudoClass, status == ResearchRunSourceStatus.Read);
        PseudoClasses.Set(SkippedPseudoClass, status == ResearchRunSourceStatus.Skipped);
        StatusText = labels.SourceStatus(status);
        Title = source.Title;
        Domain = source.Domain;
        Progress = status switch
        {
            ResearchRunSourceStatus.Read => 1,
            ResearchRunSourceStatus.Reading => Math.Clamp(source.Progress, 0, 1),
            _ => 0
        };
        AutomationProperties.SetName(this, labels.SourceName(source.Index, source.Title, source.Domain, StatusText));
        AutomationProperties.SetHelpText(this, source.Snippet);
    }

    protected override void OnClick()
    {
        base.OnClick();
        if (Source is { } source)
        {
            Owner?.OpenSource(source);
        }
    }
}

/// <summary>A numbered citation in the report or a note: a small pill that opens its source.</summary>
internal sealed class ResearchRunCitation : AiAgentButton
{
    public ResearchRunSource? Source { get; set; }

    public ResearchRun? Owner { get; set; }

    protected override void OnClick()
    {
        base.OnClick();
        if (Source is { } source)
        {
            Owner?.OpenSource(source);
        }
    }
}
