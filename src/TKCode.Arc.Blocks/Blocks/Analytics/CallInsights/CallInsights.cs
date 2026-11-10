using Avalonia;
using Avalonia.Collections;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Interactivity;

namespace TKCode.Arc;

/// <summary>
/// A recorded sales call, taken apart: a timeline with the sentiment curve, a lane per speaker, the key moments and the
/// topics, a playhead you play, drag or step with the keys; the transcript, which follows the playhead and can be
/// searched; the talk-time split, the topics, the key moments and the next steps agreed on the call.
/// <para>
/// With no utterances and no speakers the block shows a built-in invented call, so it looks finished on a page as it is;
/// give it <see cref="Speakers"/>, <see cref="Utterances"/>, <see cref="Moments"/>, <see cref="Topics"/> and
/// <see cref="NextSteps"/> to show your own. The block has no audio of its own: bind <see cref="Position"/> to your player
/// (two-way), or let the play button move the playhead in real time.
/// </para>
/// </summary>
public partial class CallInsights : TemplatedControl
{
    /// <summary>The heading (the call's name); null shows the sample call's name while the samples are shown, otherwise nothing.</summary>
    public static readonly StyledProperty<string?> TitleProperty =
        AvaloniaProperty.Register<CallInsights, string?>(nameof(Title));

    /// <summary>The line under the heading (who, when); null leaves it out.</summary>
    public static readonly StyledProperty<string?> SubtitleProperty =
        AvaloniaProperty.Register<CallInsights, string?>(nameof(Subtitle));

    /// <summary>The playhead's place in the recording (two-way). Values past the end are clamped to <see cref="Duration"/>.</summary>
    public static readonly StyledProperty<TimeSpan> PositionProperty =
        AvaloniaProperty.Register<CallInsights, TimeSpan>(nameof(Position), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>Whether the playhead moves on its own in real time (two-way); it stops at the end.</summary>
    public static readonly StyledProperty<bool> IsPlayingProperty =
        AvaloniaProperty.Register<CallInsights, bool>(nameof(IsPlaying), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>How fast the playhead moves while playing (1, 1.5 or 2 from the speed button; any positive value from code).</summary>
    public static readonly StyledProperty<double> PlaybackRateProperty =
        AvaloniaProperty.Register<CallInsights, double>(nameof(PlaybackRate), 1);

    /// <summary>The name of the chosen topic (two-way): its stretches are tinted on the timeline; null chooses none.</summary>
    public static readonly StyledProperty<string?> SelectedTopicProperty =
        AvaloniaProperty.Register<CallInsights, string?>(nameof(SelectedTopic), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>The transcript search (two-way): lines that contain it are kept and marked on the timeline.</summary>
    public static readonly StyledProperty<string?> SearchTextProperty =
        AvaloniaProperty.Register<CallInsights, string?>(nameof(SearchText), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>Whether the transcript scrolls to the line under the playhead as it moves.</summary>
    public static readonly StyledProperty<bool> FollowsPlayheadProperty =
        AvaloniaProperty.Register<CallInsights, bool>(nameof(FollowsPlayhead), true);

    /// <summary>Draws a bordered card (true); false lays the block flat on the page.</summary>
    public static readonly StyledProperty<bool> IsFramedProperty =
        AvaloniaProperty.Register<CallInsights, bool>(nameof(IsFramed), true);

    /// <summary>The words the block shows and reads out.</summary>
    public static readonly StyledProperty<CallInsightsLabels> LabelsProperty =
        AvaloniaProperty.Register<CallInsights, CallInsightsLabels>(nameof(Labels), new CallInsightsLabels());

    /// <summary>The recording's length: the end of the last utterance, moment or topic.</summary>
    public static readonly DirectProperty<CallInsights, TimeSpan> DurationProperty =
        AvaloniaProperty.RegisterDirect<CallInsights, TimeSpan>(nameof(Duration), o => o.Duration);

    /// <summary>The talk-time-weighted average sentiment of the call, from −1 to 1.</summary>
    public static readonly DirectProperty<CallInsights, double> OverallSentimentProperty =
        AvaloniaProperty.RegisterDirect<CallInsights, double>(nameof(OverallSentiment), o => o.OverallSentiment);

    /// <summary>The utterance under the playhead, or null between utterances.</summary>
    public static readonly DirectProperty<CallInsights, CallInsightsUtterance?> CurrentUtteranceProperty =
        AvaloniaProperty.RegisterDirect<CallInsights, CallInsightsUtterance?>(nameof(CurrentUtterance), o => o.CurrentUtterance);

    /// <summary>Raised when a next step is ticked or cleared in the block.</summary>
    public static readonly RoutedEvent<CallInsightsNextStepToggledEventArgs> NextStepToggledEvent =
        RoutedEvent.Register<CallInsights, CallInsightsNextStepToggledEventArgs>(nameof(NextStepToggled), RoutingStrategies.Bubble);

    /// <summary>Raised when the person moves the playhead (a click or drag on the timeline, a key, a transcript line or a moment); not while playing.</summary>
    public static readonly RoutedEvent<CallInsightsSeekedEventArgs> SeekedEvent =
        RoutedEvent.Register<CallInsights, CallInsightsSeekedEventArgs>(nameof(Seeked), RoutingStrategies.Bubble);

    private TimeSpan _duration;
    private double _overallSentiment;
    private CallInsightsUtterance? _currentUtterance;

    /// <summary>The people on the call. Empty together with <see cref="Utterances"/>: the built-in sample call is shown.</summary>
    public AvaloniaList<CallInsightsSpeaker> Speakers { get; } = new();

    /// <summary>The transcript, in any order (the block sorts it by start).</summary>
    public AvaloniaList<CallInsightsUtterance> Utterances { get; } = new();

    /// <summary>The key moments pinned to the timeline.</summary>
    public AvaloniaList<CallInsightsMoment> Moments { get; } = new();

    /// <summary>The stretches of the call spent on each topic.</summary>
    public AvaloniaList<CallInsightsTopic> Topics { get; } = new();

    /// <summary>The follow-ups agreed on the call.</summary>
    public AvaloniaList<CallInsightsNextStep> NextSteps { get; } = new();

    /// <inheritdoc cref="TitleProperty"/>
    public string? Title { get => GetValue(TitleProperty); set => SetValue(TitleProperty, value); }

    /// <inheritdoc cref="SubtitleProperty"/>
    public string? Subtitle { get => GetValue(SubtitleProperty); set => SetValue(SubtitleProperty, value); }

    /// <inheritdoc cref="PositionProperty"/>
    public TimeSpan Position { get => GetValue(PositionProperty); set => SetValue(PositionProperty, value); }

    /// <inheritdoc cref="IsPlayingProperty"/>
    public bool IsPlaying { get => GetValue(IsPlayingProperty); set => SetValue(IsPlayingProperty, value); }

    /// <inheritdoc cref="PlaybackRateProperty"/>
    public double PlaybackRate { get => GetValue(PlaybackRateProperty); set => SetValue(PlaybackRateProperty, value); }

    /// <inheritdoc cref="SelectedTopicProperty"/>
    public string? SelectedTopic { get => GetValue(SelectedTopicProperty); set => SetValue(SelectedTopicProperty, value); }

    /// <inheritdoc cref="SearchTextProperty"/>
    public string? SearchText { get => GetValue(SearchTextProperty); set => SetValue(SearchTextProperty, value); }

    /// <inheritdoc cref="FollowsPlayheadProperty"/>
    public bool FollowsPlayhead { get => GetValue(FollowsPlayheadProperty); set => SetValue(FollowsPlayheadProperty, value); }

    /// <inheritdoc cref="IsFramedProperty"/>
    public bool IsFramed { get => GetValue(IsFramedProperty); set => SetValue(IsFramedProperty, value); }

    /// <inheritdoc cref="LabelsProperty"/>
    public CallInsightsLabels Labels { get => GetValue(LabelsProperty); set => SetValue(LabelsProperty, value); }

    /// <inheritdoc cref="DurationProperty"/>
    public TimeSpan Duration { get => _duration; private set => SetAndRaise(DurationProperty, ref _duration, value); }

    /// <inheritdoc cref="OverallSentimentProperty"/>
    public double OverallSentiment { get => _overallSentiment; private set => SetAndRaise(OverallSentimentProperty, ref _overallSentiment, value); }

    /// <inheritdoc cref="CurrentUtteranceProperty"/>
    public CallInsightsUtterance? CurrentUtterance
    {
        get => _currentUtterance;
        private set => SetAndRaise(CurrentUtteranceProperty, ref _currentUtterance, value);
    }

    /// <inheritdoc cref="NextStepToggledEvent"/>
    public event EventHandler<CallInsightsNextStepToggledEventArgs>? NextStepToggled
    {
        add => AddHandler(NextStepToggledEvent, value);
        remove => RemoveHandler(NextStepToggledEvent, value);
    }

    /// <inheritdoc cref="SeekedEvent"/>
    public event EventHandler<CallInsightsSeekedEventArgs>? Seeked
    {
        add => AddHandler(SeekedEvent, value);
        remove => RemoveHandler(SeekedEvent, value);
    }
}
