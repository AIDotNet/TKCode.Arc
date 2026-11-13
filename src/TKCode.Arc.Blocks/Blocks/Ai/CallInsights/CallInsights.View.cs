using TKCode.Arc.Primitives;
using TKCode.Arc.DesignTokens;
using System.Collections.Specialized;
using System.Globalization;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using TKCode.Arc.Motion;

namespace TKCode.Arc;

[TemplatePart(BodyPart, typeof(Decorator))]
[TemplatePart(AnnouncerPart, typeof(AnalyticsAnnouncer))]
[PseudoClasses(FramedPseudoClass, CompactPseudoClass, PlayingPseudoClass)]
public partial class CallInsights
{
    public const string BodyPart = "PART_Body";
    public const string AnnouncerPart = "PART_Announcer";

    /// <summary>Set while <see cref="IsFramed"/> is true.</summary>
    public const string FramedPseudoClass = ":framed";

    /// <summary>Set while the block is at most <c>CompactWidth</c> (760) wide: one column, tighter padding.</summary>
    public const string CompactPseudoClass = ":compact";

    /// <summary>Set while <see cref="IsPlaying"/> is true.</summary>
    public const string PlayingPseudoClass = ":playing";

    internal static readonly StyledProperty<double> CompactWidthProperty =
        AvaloniaProperty.Register<CallInsights, double>(nameof(CompactWidth), 760);

    internal double CompactWidth { get => GetValue(CompactWidthProperty); set => SetValue(CompactWidthProperty, value); }

    private static readonly double[] s_rates = [1, 1.5, 2];

    private CallInsightsModel _model;
    private Decorator? _body;
    private AnalyticsAnnouncer? _announcer;
    private MotionTimeline? _playback;
    private bool _driving;
    private bool _compact;
    private bool _syncingSearch;
    private CallInsightsUtterance? _shownCurrent;

    // Built parts (null until the template is applied).
    private TextBlock? _title;
    private TextBlock? _subtitle;
    private Badge? _sentimentBadge;
    private Button? _play;
    private ArcGlyph? _playGlyph;
    private TextBlock? _clock;
    private Button? _speed;
    private TextBlock? _readout;
    private CallInsightsTimeline? _timeline;
    private Grid? _columns;
    private Control? _side;
    private Control? _transcriptPane;
    private TextBlock? _matchCount;
    private SearchField? _search;
    private ItemsControl? _transcript;
    private TextBlock? _noMatches;
    private CallInsightsTalkBar? _talkBar;
    private StackPanel? _talkRows;
    private WrapPanel? _topicChips;
    private StackPanel? _momentRows;
    private StackPanel? _stepRows;
    private readonly Dictionary<string, CallInsightsPressable> _chips = new(StringComparer.CurrentCultureIgnoreCase);

    static CallInsights()
    {
        FocusableProperty.OverrideDefaultValue<CallInsights>(false);
    }

    public CallInsights()
    {
        _model = BuildModel();
        Duration = _model.Duration;
        OverallSentiment = _model.OverallSentiment;
        PseudoClasses.Set(FramedPseudoClass, true);
        Speakers.CollectionChanged += OnDataChanged;
        Utterances.CollectionChanged += OnDataChanged;
        Moments.CollectionChanged += OnDataChanged;
        Topics.CollectionChanged += OnDataChanged;
        NextSteps.CollectionChanged += OnStepsChanged;
    }

    /// <summary>True while the block shows its built-in invented call (no speakers and no utterances were given).</summary>
    public bool IsShowingSamples => Speakers.Count == 0 && Utterances.Count == 0;

    /// <summary>The talk-time split worked out from the utterances, one entry per speaker in order.</summary>
    public IReadOnlyList<CallInsightsTalkShare> TalkShares => _model.TalkShares;

    /// <summary>The next steps shown (the samples' while <see cref="IsShowingSamples"/>).</summary>
    public IReadOnlyList<CallInsightsNextStep> EffectiveNextSteps => _effectiveSteps;

    private IReadOnlyList<CallInsightsNextStep> _effectiveSteps = Array.Empty<CallInsightsNextStep>();

    internal CallInsightsModel Model => _model;

    internal CallInsightsTimeline? Timeline => _timeline;

    internal ItemsControl? Transcript => _transcript;

    internal AnalyticsAnnouncer? Announcer => _announcer;

    internal IReadOnlyDictionary<string, CallInsightsPressable> TopicChips => _chips;

    internal StackPanel? MomentRows => _momentRows;

    internal StackPanel? StepRows => _stepRows;

    internal Button? PlayButton => _play;

    internal Button? SpeedButton => _speed;

    internal bool IsCompact => _compact;

    #region Public methods

    /// <summary>Moves the playhead to <paramref name="position"/> (clamped to the call) as if the person did, raising <see cref="Seeked"/>.</summary>
    public void Seek(TimeSpan position) => SeekCore(position, announce: true);

    /// <summary>Starts moving the playhead in real time (from the start when it is at the end).</summary>
    public void Play() => SetCurrentValue(IsPlayingProperty, true);

    /// <summary>Stops the playhead where it is.</summary>
    public void Pause() => SetCurrentValue(IsPlayingProperty, false);

    /// <summary>Re-reads the data: call it after changing an item's properties in place (the collections are followed on their own).</summary>
    public void Refresh() => RebuildModel();

    #endregion

    #region Model

    private CallInsightsModel BuildModel()
    {
        if (IsShowingSamples)
        {
            _effectiveSteps = _sampleSteps ??= CallInsightsSamples.NextSteps();
            return new CallInsightsModel(CallInsightsSamples.Speakers(), CallInsightsSamples.Utterances(),
                CallInsightsSamples.Moments(), CallInsightsSamples.Topics());
        }
        _effectiveSteps = NextSteps;
        return new CallInsightsModel(Speakers, Utterances, Moments, Topics);
    }

    private CallInsightsNextStep[]? _sampleSteps;

    private void OnDataChanged(object? sender, NotifyCollectionChangedEventArgs e) => RebuildModel();

    private void OnStepsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        _effectiveSteps = IsShowingSamples ? _effectiveSteps : NextSteps;
        BuildSteps();
    }

    private void RebuildModel()
    {
        _model = BuildModel();
        Duration = _model.Duration;
        OverallSentiment = _model.OverallSentiment;
        if (Position > Duration)
        {
            SetCurrentValue(PositionProperty, Duration);
        }
        RefreshAll();
    }

    #endregion

    #region Template

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        if (_body is not null)
        {
            _body.Child = null;
        }
        _body = e.NameScope.Find<Decorator>(BodyPart);
        _announcer = e.NameScope.Find<AnalyticsAnnouncer>(AnnouncerPart);
        if (_body is not null)
        {
            _body.Child = BuildBody();
            RefreshAll();
        }
    }

    private Control BuildBody()
    {
        var labels = Labels;
        var root = new StackPanel { Spacing = 20 };

        // Header: the title and subtitle, the overall sentiment on the right.
        _title = CallInsightsUi.Text(null, ArcDesignTokenKind.Foreground, ArcDesignTokenKind.TypeLG, FontWeight.Medium, 24);
        _title.TextWrapping = TextWrapping.NoWrap;
        _title.TextTrimming = TextTrimming.CharacterEllipsis;
        ArcTokens.Bind(_title, TextBlock.FontFamilyProperty, ArcDesignTokenKind.DisplayFontFamily);
        AutomationProperties.SetHeadingLevel(_title, 2);
        _subtitle = CallInsightsUi.Text(null, ArcDesignTokenKind.TextMuted, ArcDesignTokenKind.TypeXS, lineHeight: 16.8);
        var titles = new StackPanel { Spacing = 2, VerticalAlignment = VerticalAlignment.Center };
        titles.Children.Add(_title);
        titles.Children.Add(_subtitle);
        _sentimentBadge = new Badge { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(12, 0, 0, 0) };
        var header = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
        header.Children.Add(titles);
        Grid.SetColumn(_sentimentBadge, 1);
        header.Children.Add(_sentimentBadge);
        root.Children.Add(header);

        // Player: play / pause, the clock, the speed and the hover readout.
        _playGlyph = CallInsightsUi.Glyph(ArcIcons.Play, 16);
        _play = new Button { Variant = ButtonVariant.Secondary, Size = ControlSize.Small, Content = _playGlyph, Width = 36, Padding = default };
        _play.Click += (_, _) => SetCurrentValue(IsPlayingProperty, !IsPlaying);
        _clock = CallInsightsUi.Tabular(CallInsightsUi.Text(null, ArcDesignTokenKind.Foreground, ArcDesignTokenKind.TypeSM, FontWeight.Medium));
        _clock.VerticalAlignment = VerticalAlignment.Center;
        _clock.TextWrapping = TextWrapping.NoWrap;
        _speed = new Button { Variant = ButtonVariant.Ghost, Size = ControlSize.Small, MinWidth = 48 };
        AutomationProperties.SetName(_speed, labels.Speed);
        _speed.Click += (_, _) => CycleRate();
        _readout = CallInsightsUi.Tabular(CallInsightsUi.Text(null, ArcDesignTokenKind.TextMuted, ArcDesignTokenKind.TypeXS, lineHeight: 16.8));
        _readout.TextWrapping = TextWrapping.NoWrap;
        _readout.TextTrimming = TextTrimming.CharacterEllipsis;
        _readout.VerticalAlignment = VerticalAlignment.Center;
        _readout.HorizontalAlignment = HorizontalAlignment.Right;
        var player = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,Auto,Auto,*"), ColumnSpacing = 10 };
        player.Children.Add(_play);
        Grid.SetColumn(_clock, 1);
        player.Children.Add(_clock);
        Grid.SetColumn(_speed, 2);
        player.Children.Add(_speed);
        Grid.SetColumn(_readout, 3);
        player.Children.Add(_readout);

        _timeline = new CallInsightsTimeline
        {
            Labels = labels,
            SeekRequested = t => SeekCore(t, announce: true),
            PlayToggleRequested = () => SetCurrentValue(IsPlayingProperty, !IsPlaying),
            HoverChanged = OnTimelineHover,
        };
        var timelineGroup = new StackPanel { Spacing = 12 };
        timelineGroup.Children.Add(player);
        timelineGroup.Children.Add(_timeline);
        root.Children.Add(timelineGroup);

        _transcriptPane = BuildTranscript(labels);
        _side = BuildSide(labels);
        _columns = new Grid();
        _columns.Children.Add(_transcriptPane);
        _columns.Children.Add(_side);
        root.Children.Add(_columns);
        ApplyColumns();
        return root;
    }

    private Control BuildTranscript(CallInsightsLabels labels)
    {
        var pane = new StackPanel { Spacing = 10 };
        var head = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
        head.Children.Add(CallInsightsUi.Heading(labels.Transcript));
        _matchCount = CallInsightsUi.Tabular(CallInsightsUi.Text(null, ArcDesignTokenKind.TextMuted, ArcDesignTokenKind.TypeXS, lineHeight: 16.8));
        Grid.SetColumn(_matchCount, 1);
        head.Children.Add(_matchCount);
        pane.Children.Add(head);

        _search = new SearchField { Placeholder = labels.Search, Size = ControlSize.Small };
        AutomationProperties.SetName(_search, labels.Search);
        _search.PropertyChanged += (_, e) =>
        {
            if (e.Property == TextBox.TextProperty && !_syncingSearch)
            {
                SetCurrentValue(SearchTextProperty, string.IsNullOrEmpty(_search.Text) ? null : _search.Text);
            }
        };
        pane.Children.Add(_search);

        _transcript = new ItemsControl
        {
            ItemsPanel = new FuncTemplate<Panel?>(() => new VirtualizingStackPanel()),
            ItemTemplate = new FuncDataTemplate<CallInsightsUtterance>((u, _) => BuildLine(u), supportsRecycling: false),
        };
        AutomationProperties.SetName(_transcript, labels.Transcript);
        _transcript.ContainerPrepared += (_, e) => MarkContainer(e.Container);
        var scroller = new ScrollViewer
        {
            Content = _transcript,
            MaxHeight = 380,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
        };
        scroller.Bind(MaxHeightProperty, scroller.GetResourceObservable(CallInsightsTokens.Key(CallInsightsTokenKind.TranscriptHeight)));
        _noMatches = CallInsightsUi.Text(labels.NoMatches, ArcDesignTokenKind.TextMuted, ArcDesignTokenKind.TypeSM);
        _noMatches.Margin = new Thickness(10, 12);
        _noMatches.IsVisible = false;
        var list = new Panel();
        list.Children.Add(scroller);
        list.Children.Add(_noMatches);
        pane.Children.Add(list);
        return pane;
    }

    private Control BuildLine(CallInsightsUtterance? utterance)
    {
        if (utterance is null)
        {
            return new Panel();
        }
        var speaker = _model.SpeakerOf(utterance);
        var index = _model.IndexOfSpeaker(utterance.SpeakerKey);
        var name = CallInsightsUi.Text(speaker?.Name ?? utterance.SpeakerKey, ArcDesignTokenKind.Foreground, ArcDesignTokenKind.TypeXS,
            FontWeight.Medium, 16.8);
        name.TextWrapping = TextWrapping.NoWrap;
        var time = CallInsightsUi.Tabular(CallInsightsUi.Text(CallInsightsModel.FormatTime(utterance.Start), ArcDesignTokenKind.TextMuted,
            ArcDesignTokenKind.TypeXS, lineHeight: 16.8));
        time.TextWrapping = TextWrapping.NoWrap;
        var top = new ArcShrinkRow { Spacing = 8 };
        top.Children.Add(new CallInsightsDot { SeriesIndex = index });
        ArcShrinkRow.SetIsShrinkable(name, true);
        name.TextTrimming = TextTrimming.CharacterEllipsis;
        top.Children.Add(name);
        top.Children.Add(time);
        var text = CallInsightsUi.Text(utterance.Text, ArcDesignTokenKind.TextSecondary, ArcDesignTokenKind.TypeSM);
        text.Margin = new Thickness(16, 0, 0, 0);
        var stack = new StackPanel { Spacing = 2 };
        stack.Children.Add(top);
        stack.Children.Add(text);
        var row = new CallInsightsPressable { Content = stack, Tag = utterance };
        AutomationProperties.SetName(row, string.Format(CultureInfo.CurrentCulture, Labels.SeekFormat,
            CallInsightsModel.FormatTime(utterance.Start), speaker?.Name ?? utterance.SpeakerKey, utterance.Text));
        row.Click += (_, _) => SeekCore(utterance.Start, announce: false);
        return row;
    }

    private Control BuildSide(CallInsightsLabels labels)
    {
        var side = new StackPanel { Spacing = 22 };

        var talk = new StackPanel { Spacing = 10 };
        talk.Children.Add(CallInsightsUi.Heading(labels.TalkTime));
        _talkBar = new CallInsightsTalkBar();
        talk.Children.Add(_talkBar);
        _talkRows = new StackPanel { Spacing = 8 };
        talk.Children.Add(_talkRows);
        side.Children.Add(talk);

        var topics = new StackPanel { Spacing = 10 };
        topics.Children.Add(CallInsightsUi.Heading(labels.Topics));
        _topicChips = new WrapPanel { ItemSpacing = 6, LineSpacing = 6 };
        topics.Children.Add(_topicChips);
        side.Children.Add(topics);

        var moments = new StackPanel { Spacing = 6 };
        moments.Children.Add(CallInsightsUi.Heading(labels.Moments));
        _momentRows = new StackPanel { Spacing = 0, Margin = new Thickness(-10, 0) };
        moments.Children.Add(_momentRows);
        side.Children.Add(moments);

        var steps = new StackPanel { Spacing = 6 };
        steps.Children.Add(CallInsightsUi.Heading(labels.NextSteps));
        _stepRows = new StackPanel { Spacing = 2 };
        steps.Children.Add(_stepRows);
        side.Children.Add(steps);
        return side;
    }

    /// <summary>Two columns (transcript, side) when wide; one under the other when compact.</summary>
    private void ApplyColumns()
    {
        if (_columns is null || _transcriptPane is null || _side is null)
        {
            return;
        }
        _columns.ColumnDefinitions.Clear();
        _columns.RowDefinitions.Clear();
        if (_compact)
        {
            _columns.RowDefinitions = new RowDefinitions("Auto,Auto");
            _columns.RowSpacing = 24;
            _columns.ColumnSpacing = 0;
            Grid.SetColumn(_side, 0);
            Grid.SetRow(_side, 1);
        }
        else
        {
            _columns.ColumnDefinitions = new ColumnDefinitions("*,300");
            _columns.ColumnSpacing = 28;
            _columns.RowSpacing = 0;
            Grid.SetColumn(_side, 1);
            Grid.SetRow(_side, 0);
        }
    }

    #endregion

    #region Refresh

    private void RefreshAll()
    {
        if (_body?.Child is null)
        {
            return;
        }
        var labels = Labels;
        _title!.Text = Title ?? (IsShowingSamples ? CallInsightsSamples.Title : null);
        _title.IsVisible = !string.IsNullOrEmpty(_title.Text);
        _subtitle!.Text = Subtitle ?? (IsShowingSamples && Title is null ? CallInsightsSamples.Subtitle : null);
        _subtitle.IsVisible = !string.IsNullOrEmpty(_subtitle.Text);
        AutomationProperties.SetName(this, _title.Text);

        var sentiment = _model.OverallSentiment;
        (_sentimentBadge!.Content, _sentimentBadge.Tone) = sentiment > 0.2
            ? (labels.Positive, BadgeTone.Success)
            : sentiment < -0.2 ? (labels.Negative, BadgeTone.Danger) : (labels.Neutral, BadgeTone.Neutral);
        AutomationProperties.SetName(_sentimentBadge, labels.Sentiment + ": " + _sentimentBadge.Content);

        _timeline!.Labels = labels;
        _timeline.SetModel(_model);
        _timeline.SetTopic(SelectedTopicSummary());
        BuildTalk();
        BuildTopics();
        BuildMoments();
        BuildSteps();
        ApplySearch();
        UpdateRate();
        UpdatePosition(follow: false);
        UpdatePlayButton();
    }

    private void BuildTalk()
    {
        if (_talkRows is null || _talkBar is null)
        {
            return;
        }
        _talkBar.Shares = _model.TalkShares;
        _talkRows.Children.Clear();
        var labels = Labels;
        for (var i = 0; i < _model.TalkShares.Count; i++)
        {
            var share = _model.TalkShares[i];
            var name = CallInsightsUi.Text(share.Speaker.Name, ArcDesignTokenKind.Foreground, ArcDesignTokenKind.TypeSM, FontWeight.Medium);
            name.TextWrapping = TextWrapping.NoWrap;
            name.TextTrimming = TextTrimming.CharacterEllipsis;
            var detailText = string.Format(CultureInfo.CurrentCulture, labels.LongestFormat, CallInsightsModel.FormatTime(share.LongestMonologue));
            if (share.Speaker.IsHost)
            {
                detailText += " · " + string.Format(CultureInfo.CurrentCulture, labels.QuestionsFormat, share.Questions);
            }
            var detail = CallInsightsUi.Text((share.Speaker.Role is { Length: > 0 } role ? role + " · " : string.Empty) + detailText,
                ArcDesignTokenKind.TextMuted, ArcDesignTokenKind.TypeXS, lineHeight: 16.8);
            var copy = new StackPanel();
            copy.Children.Add(name);
            copy.Children.Add(detail);
            var percent = CallInsightsUi.Tabular(CallInsightsUi.Text(CallInsightsModel.FormatShare(share.Share), ArcDesignTokenKind.Foreground,
                ArcDesignTokenKind.TypeSM, FontWeight.Medium));
            percent.TextWrapping = TextWrapping.NoWrap;
            var row = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto"), ColumnSpacing = 10 };
            var dot = new CallInsightsDot { SeriesIndex = i, VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(0, 6, 0, 0) };
            row.Children.Add(dot);
            Grid.SetColumn(copy, 1);
            row.Children.Add(copy);
            Grid.SetColumn(percent, 2);
            row.Children.Add(percent);
            AutomationProperties.SetName(row, share.Speaker.Name + ", " + CallInsightsModel.FormatShare(share.Share) + ", " + detailText);
            _talkRows.Children.Add(row);
        }
    }

    private void BuildTopics()
    {
        if (_topicChips is null)
        {
            return;
        }
        _topicChips.Children.Clear();
        _chips.Clear();
        foreach (var topic in _model.Topics)
        {
            var label = CallInsightsUi.Text(topic.Name, ArcDesignTokenKind.Foreground, ArcDesignTokenKind.TypeXS, FontWeight.Medium, 16.8);
            label.TextWrapping = TextWrapping.NoWrap;
            var share = CallInsightsUi.Tabular(CallInsightsUi.Text(CallInsightsModel.FormatShare(topic.Share), ArcDesignTokenKind.TextMuted,
                ArcDesignTokenKind.TypeXS, lineHeight: 16.8));
            share.TextWrapping = TextWrapping.NoWrap;
            var content = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
            content.Children.Add(label);
            content.Children.Add(share);
            var chip = new CallInsightsPressable { Content = content, Classes = { CallInsightsPressable.ChipClass } };
            var name = topic.Name;
            chip.Click += (_, _) => SetCurrentValue(SelectedTopicProperty, string.Equals(SelectedTopic, name, StringComparison.CurrentCultureIgnoreCase) ? null : name);
            AutomationProperties.SetName(chip, string.Format(CultureInfo.CurrentCulture, Labels.TopicFormat, topic.Name, CallInsightsModel.FormatShare(topic.Share)));
            _chips[topic.Name] = chip;
            _topicChips.Children.Add(chip);
        }
        UpdateTopicChips();
    }

    private void UpdateTopicChips()
    {
        var selected = SelectedTopicSummary();
        foreach (var (name, chip) in _chips)
        {
            var isSelected = selected is not null && string.Equals(selected.Name, name, StringComparison.CurrentCultureIgnoreCase);
            chip.IsCurrent = isSelected;
            AutomationProperties.SetItemStatus(chip, isSelected ? "selected" : null);
        }
    }

    private CallInsightsTopicSummary? SelectedTopicSummary()
        => SelectedTopic is { Length: > 0 } name
            ? _model.Topics.FirstOrDefault(t => string.Equals(t.Name, name, StringComparison.CurrentCultureIgnoreCase))
            : null;

    private void BuildMoments()
    {
        if (_momentRows is null)
        {
            return;
        }
        _momentRows.Children.Clear();
        var kinds = Labels.MomentKinds;
        foreach (var moment in _model.Moments)
        {
            var kindName = (int)moment.Kind < kinds.Count ? kinds[(int)moment.Kind] : moment.Kind.ToString();
            var time = CallInsightsUi.Tabular(CallInsightsUi.Text(CallInsightsModel.FormatTime(moment.Time), ArcDesignTokenKind.TextMuted,
                ArcDesignTokenKind.TypeXS, lineHeight: 16.8));
            time.TextWrapping = TextWrapping.NoWrap;
            time.MinWidth = 36;
            var title = CallInsightsUi.Text(moment.Title, ArcDesignTokenKind.Foreground, ArcDesignTokenKind.TypeSM);
            var kind = CallInsightsUi.Text(kindName, ArcDesignTokenKind.TextMuted, ArcDesignTokenKind.TypeXS, lineHeight: 16.8);
            var copy = new StackPanel();
            copy.Children.Add(title);
            copy.Children.Add(kind);
            var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,Auto,*"), ColumnSpacing = 10 };
            var dot = new CallInsightsDot { Moment = moment.Kind, IsRing = true, Width = 10, Height = 10, VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(0, 5, 0, 0) };
            grid.Children.Add(dot);
            Grid.SetColumn(time, 1);
            time.VerticalAlignment = VerticalAlignment.Top;
            time.Margin = new Thickness(0, 1, 0, 0);
            grid.Children.Add(time);
            Grid.SetColumn(copy, 2);
            grid.Children.Add(copy);
            var row = new CallInsightsPressable { Content = grid, Tag = moment };
            AutomationProperties.SetName(row, CallInsightsModel.FormatTime(moment.Time) + ", " + kindName + ": " + moment.Title);
            row.Click += (_, _) => SeekCore(moment.Time, announce: true);
            _momentRows.Children.Add(row);
        }
    }

    private void BuildSteps()
    {
        if (_stepRows is null)
        {
            return;
        }
        _stepRows.Children.Clear();
        foreach (var step in _effectiveSteps)
        {
            var meta = string.Join(" · ", new[] { step.Owner, step.Due }.Where(s => !string.IsNullOrEmpty(s)));
            var box = new Checkbox
            {
                Content = step.Text,
                Description = meta.Length > 0 ? meta : null,
                IsChecked = step.IsDone,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                Tag = step,
            };
            box.IsCheckedChanged += (_, _) =>
            {
                var done = box.IsChecked == true;
                if (step.IsDone == done)
                {
                    return;
                }
                step.IsDone = done;
                RaiseEvent(new CallInsightsNextStepToggledEventArgs(NextStepToggledEvent, step, done));
            };
            _stepRows.Children.Add(box);
        }
    }

    private void ApplySearch()
    {
        if (_transcript is null)
        {
            return;
        }
        var query = SearchText;
        var matches = _model.Matches(query);
        var filtered = !string.IsNullOrWhiteSpace(query);
        _transcript.ItemsSource = matches;
        _noMatches!.IsVisible = filtered && matches.Count == 0;
        _matchCount!.Text = filtered ? string.Format(CultureInfo.CurrentCulture, Labels.MatchesFormat, matches.Count) : null;
        _timeline?.SetMatches(matches, filtered);
        if (_search is not null && _search.Text != (query ?? string.Empty))
        {
            _syncingSearch = true;
            _search.Text = query ?? string.Empty;
            _syncingSearch = false;
        }
        if (filtered)
        {
            _announcer?.Announce(_matchCount.Text);
        }
    }

    private void MarkContainer(Control container)
    {
        if (container is ContentPresenter { Child: CallInsightsPressable row } && row.Tag is CallInsightsUtterance u)
        {
            row.IsCurrent = ReferenceEquals(u, _shownCurrent);
        }
        else if (container is ContentPresenter presenter)
        {
            // The child is created by the template after the container is prepared.
            presenter.PropertyChanged += OnPresenterChildChanged;
        }
    }

    private void OnPresenterChildChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == ContentPresenter.ChildProperty && sender is ContentPresenter { Child: CallInsightsPressable row } presenter)
        {
            presenter.PropertyChanged -= OnPresenterChildChanged;
            row.IsCurrent = row.Tag is CallInsightsUtterance u && ReferenceEquals(u, _shownCurrent);
        }
    }

    private void UpdatePosition(bool follow)
    {
        var position = Position;
        _timeline?.SetPosition(position);
        if (_clock is not null)
        {
            _clock.Text = CallInsightsModel.FormatTime(position) + " / " + CallInsightsModel.FormatTime(_model.Duration);
        }
        var index = _model.UtteranceAt(position);
        if (index < 0)
        {
            // Between utterances the transcript keeps the last line that started.
            index = _model.UtteranceStartedBy(position);
        }
        var current = index >= 0 ? _model.Utterances[index] : null;
        CurrentUtterance = _model.UtteranceAt(position) is var at and >= 0 ? _model.Utterances[at] : null;
        if (ReferenceEquals(current, _shownCurrent) || _transcript is null)
        {
            return;
        }
        var previous = _shownCurrent;
        _shownCurrent = current;
        SetRowCurrent(previous, false);
        SetRowCurrent(current, true);
        if (follow && FollowsPlayhead && current is not null && _transcript.ItemsSource is IReadOnlyList<CallInsightsUtterance> items)
        {
            var at2 = IndexOf(items, current);
            if (at2 >= 0)
            {
                _transcript.ScrollIntoView(at2);
            }
        }
    }

    private static int IndexOf(IReadOnlyList<CallInsightsUtterance> items, CallInsightsUtterance item)
    {
        for (var i = 0; i < items.Count; i++)
        {
            if (ReferenceEquals(items[i], item))
            {
                return i;
            }
        }
        return -1;
    }

    private void SetRowCurrent(CallInsightsUtterance? utterance, bool value)
    {
        if (utterance is null || _transcript?.ContainerFromItem(utterance) is not ContentPresenter { Child: CallInsightsPressable row })
        {
            return;
        }
        row.IsCurrent = value;
    }

    private void UpdatePlayButton()
    {
        if (_play is null || _playGlyph is null)
        {
            return;
        }
        _playGlyph.Data = IsPlaying ? ArcIcons.Pause : ArcIcons.Play;
        AutomationProperties.SetName(_play, IsPlaying ? Labels.Pause : Labels.Play);
    }

    private void UpdateRate()
    {
        if (_speed is not null)
        {
            _speed.Content = PlaybackRate.ToString("0.##", CultureInfo.CurrentCulture) + "×";
        }
    }

    private void CycleRate()
    {
        var index = Array.FindIndex(s_rates, r => Math.Abs(r - PlaybackRate) < 0.01);
        SetCurrentValue(PlaybackRateProperty, s_rates[(index + 1) % s_rates.Length]);
    }

    private void OnTimelineHover(TimeSpan? time)
    {
        if (_readout is null)
        {
            return;
        }
        if (time is not { } t)
        {
            _readout.Text = null;
            return;
        }
        var index = _model.UtteranceAt(t);
        var sample = _model.Sentiment.Length == 0 || _model.Duration <= TimeSpan.Zero
            ? 0
            : _model.Sentiment[(int)Math.Round(t.TotalSeconds / _model.Duration.TotalSeconds * (_model.Sentiment.Length - 1))];
        var text = CallInsightsModel.FormatTime(t);
        if (index >= 0 && _model.SpeakerOf(_model.Utterances[index]) is { } speaker)
        {
            text += " · " + speaker.Name;
        }
        text += " · " + Labels.Sentiment + " " + sample.ToString("+0.0;−0.0;0.0", CultureInfo.CurrentCulture);
        _readout.Text = text;
    }

    #endregion

    #region Seeking and playback

    private void SeekCore(TimeSpan position, bool announce)
    {
        var clamped = position < TimeSpan.Zero ? TimeSpan.Zero : position > Duration ? Duration : position;
        SetCurrentValue(PositionProperty, clamped);
        var index = _model.UtteranceAt(clamped);
        if (index < 0)
        {
            index = _model.UtteranceStartedBy(clamped);
        }
        var utterance = index >= 0 ? _model.Utterances[index] : null;
        if (announce && utterance is not null)
        {
            _announcer?.Announce(string.Format(CultureInfo.CurrentCulture, Labels.SeekFormat, CallInsightsModel.FormatTime(clamped),
                _model.SpeakerOf(utterance)?.Name ?? utterance.SpeakerKey, utterance.Text));
        }
        RaiseEvent(new CallInsightsSeekedEventArgs(SeekedEvent, clamped, utterance));
    }

    private void StartPlayback()
    {
        var duration = Duration;
        if (duration <= TimeSpan.Zero)
        {
            SetCurrentValue(IsPlayingProperty, false);
            return;
        }
        if (Position >= duration)
        {
            _driving = true;
            SetCurrentValue(PositionProperty, TimeSpan.Zero);
            _driving = false;
            UpdatePosition(follow: true);
        }
        if (_playback is null)
        {
            _playback = new MotionTimeline(this);
            _playback.Changed += OnPlaybackChanged;
            _playback.Completed += () => SetCurrentValue(IsPlayingProperty, false);
        }
        var rate = PlaybackRate > 0 ? PlaybackRate : 1;
        _playback.Start(TimeSpan.FromTicks((long)(duration.Ticks / rate)), Position.TotalSeconds / duration.TotalSeconds);
    }

    private void OnPlaybackChanged(double value)
    {
        if (!IsPlaying)
        {
            return;
        }
        _driving = true;
        SetCurrentValue(PositionProperty, TimeSpan.FromSeconds(Math.Round(Duration.TotalSeconds * value, 1)));
        _driving = false;
    }

    #endregion

    #region Property changes and lifetime

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == PositionProperty)
        {
            if (Position < TimeSpan.Zero || Position > Duration)
            {
                SetCurrentValue(PositionProperty, Position < TimeSpan.Zero ? TimeSpan.Zero : Duration);
                return;
            }
            UpdatePosition(follow: true);
            if (IsPlaying && !_driving)
            {
                StartPlayback();
            }
        }
        else if (change.Property == IsPlayingProperty)
        {
            PseudoClasses.Set(PlayingPseudoClass, IsPlaying);
            if (IsPlaying)
            {
                StartPlayback();
            }
            else
            {
                _playback?.Stop();
            }
            UpdatePlayButton();
        }
        else if (change.Property == PlaybackRateProperty)
        {
            UpdateRate();
            if (IsPlaying)
            {
                StartPlayback();
            }
        }
        else if (change.Property == SelectedTopicProperty)
        {
            _timeline?.SetTopic(SelectedTopicSummary());
            UpdateTopicChips();
            if (SelectedTopicSummary() is { } topic)
            {
                _announcer?.Announce(string.Format(CultureInfo.CurrentCulture, Labels.TopicFormat, topic.Name, CallInsightsModel.FormatShare(topic.Share)));
            }
        }
        else if (change.Property == SearchTextProperty)
        {
            ApplySearch();
        }
        else if (change.Property == TitleProperty || change.Property == SubtitleProperty)
        {
            RefreshAll();
        }
        else if (change.Property == LabelsProperty)
        {
            if (_body is not null)
            {
                _body.Child = BuildBody();
                RefreshAll();
            }
        }
        else if (change.Property == IsFramedProperty)
        {
            PseudoClasses.Set(FramedPseudoClass, IsFramed);
        }
        else if (change.Property == CompactWidthProperty)
        {
            UpdateCompact(Bounds.Width);
        }
    }

    protected override void OnSizeChanged(SizeChangedEventArgs e)
    {
        base.OnSizeChanged(e);
        UpdateCompact(e.NewSize.Width);
    }

    private void UpdateCompact(double width)
    {
        var compact = width > 0 && width <= CompactWidth;
        if (compact == _compact)
        {
            return;
        }
        _compact = compact;
        PseudoClasses.Set(CompactPseudoClass, compact);
        ApplyColumns();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        if (IsPlaying)
        {
            SetCurrentValue(IsPlayingProperty, false);
        }
    }

    #endregion
}
