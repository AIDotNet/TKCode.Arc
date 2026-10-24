using System.Collections;
using System.Collections.Specialized;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Metadata;
using Avalonia.Threading;

namespace TKCode.Arc;

/// <summary>
/// An animated explainer of tree search over thoughts (tree of thoughts with beam search). Each depth proposes candidate
/// thoughts as edges draw out of their parents, scores them with bars that fill while the digits count, keeps the best
/// <see cref="BeamWidth"/> and prunes the rest (they fade and their edges retract), and the survivors expand one depth
/// further, up to three. The last depth picks its best thought as the answer, and the winning path draws from the question
/// to the answer with a sheen at its head while each node on it lights up. Play, pause, step and restart drive the story;
/// the beam width control starts it over. Wide cards read left to right in four columns; narrow ones become an indented
/// outline. The scores are illustrative values, not the output of a model.
/// </summary>
[TemplatePart(StagePart, typeof(ReasoningTreeStage))]
[TemplatePart(StatusPart, typeof(ReasoningTreeStatus))]
[TemplatePart(BeamPart, typeof(SegmentedControl))]
[TemplatePart(BeamLabelPart, typeof(TextBlock))]
[TemplatePart(TransportPart, typeof(Panel))]
[PseudoClasses(CompactPseudoClass, TinyPseudoClass)]
public class ReasoningTree : Explainer
{
    /// <summary>The drawn tree part.</summary>
    public const string StagePart = "PART_Stage";

    /// <summary>The header status part.</summary>
    public const string StatusPart = "PART_Status";

    /// <summary>The beam width control part.</summary>
    public const string BeamPart = "PART_Beam";

    /// <summary>The beam width label part.</summary>
    public const string BeamLabelPart = "PART_BeamLabel";

    /// <summary>The group of playback buttons.</summary>
    public const string TransportPart = "PART_Transport";

    /// <summary>Set when the card is narrower than 520px (tighter padding, no title, a short beam label).</summary>
    public const string CompactPseudoClass = ":compact";

    /// <summary>Set when the card is narrower than 306px (the footer stacks its two groups).</summary>
    public const string TinyPseudoClass = ":tiny";

    /// <summary>Card widths under this are compact.</summary>
    public const double CompactWidth = 520;

    /// <summary>Card widths under this stack the footer.</summary>
    public const double TinyWidth = 306;

    // The story's beats at speed 1 (Arc Reasoning tree, design reference): read the question, then per depth propose,
    // score and keep; the last depth goes from scoring straight to the answer.
    internal const double ReadBeat = 0.56;
    internal const double ProposeBeat = 0.62;
    internal const double ScoreBeat = 0.88;
    internal const double KeepBeat = 0.76;

    /// <summary>Defines the <see cref="Question"/> property.</summary>
    public static readonly StyledProperty<string> QuestionProperty =
        AvaloniaProperty.Register<ReasoningTree, string>(nameof(Question), ReasoningTreeSamples.Question);

    /// <summary>Defines the <see cref="QuestionDetail"/> property.</summary>
    public static readonly StyledProperty<string?> QuestionDetailProperty =
        AvaloniaProperty.Register<ReasoningTree, string?>(nameof(QuestionDetail));

    /// <summary>Defines the <see cref="ThoughtsSource"/> property.</summary>
    public static readonly StyledProperty<IEnumerable<ReasoningThought>?> ThoughtsSourceProperty =
        AvaloniaProperty.Register<ReasoningTree, IEnumerable<ReasoningThought>?>(nameof(ThoughtsSource));

    /// <summary>Defines the <see cref="BeamWidth"/> property.</summary>
    public static readonly StyledProperty<int> BeamWidthProperty =
        AvaloniaProperty.Register<ReasoningTree, int>(nameof(BeamWidth), 2, coerce: CoerceBeamWidth,
            defaultBindingMode: BindingMode.TwoWay);

    /// <summary>Defines the <see cref="Seed"/> property.</summary>
    public static readonly StyledProperty<int> SeedProperty =
        AvaloniaProperty.Register<ReasoningTree, int>(nameof(Seed), 7);

    /// <summary>Defines the <see cref="AccentBrush"/> property.</summary>
    public static readonly StyledProperty<IBrush?> AccentBrushProperty =
        AvaloniaProperty.Register<ReasoningTree, IBrush?>(nameof(AccentBrush));

    /// <summary>Defines the <see cref="Labels"/> property.</summary>
    public static readonly StyledProperty<ReasoningTreeLabels> LabelsProperty =
        AvaloniaProperty.Register<ReasoningTree, ReasoningTreeLabels>(nameof(Labels), new ReasoningTreeLabels());

    /// <summary>Defines the <see cref="Answer"/> property.</summary>
    public static readonly DirectProperty<ReasoningTree, ReasoningThought?> AnswerProperty =
        AvaloniaProperty.RegisterDirect<ReasoningTree, ReasoningThought?>(nameof(Answer), o => o.Answer);

    /// <summary>Defines the <see cref="Answered"/> routed event.</summary>
    public static readonly RoutedEvent<ReasoningTreeAnsweredEventArgs> AnsweredEvent =
        RoutedEvent.Register<ReasoningTree, ReasoningTreeAnsweredEventArgs>(nameof(Answered), RoutingStrategies.Bubble);

    private readonly ReasoningTreeScene _scene = new();
    private readonly List<ReasoningThought> _watched = new();
    private INotifyCollectionChanged? _watchedSource;
    private object?[]? _dataSeen;
    private ReasoningTreeStage? _stage;
    private ReasoningTreeStatus? _status;
    private SegmentedControl? _beam;
    private TextBlock? _beamLabel;
    private Panel? _transport;
    private ReasoningTreeModel? _model;
    private ReasoningTreeResult[] _results = [];
    private ReasoningTreeResult? _result;
    private ReasoningThought? _answer;
    private bool _modelDirty = true;
    private bool _rebuildPosted;
    private bool _storyStarted;
    private bool _pauseAnnouncePending;
    private bool _syncingBeam;

    static ReasoningTree()
    {
        TitleProperty.OverrideDefaultValue<ReasoningTree>(ReasoningTreeSamples.Title);
        ModelProperty.OverrideDefaultValue<ReasoningTree>(new ExplainerModel { Name = ReasoningTreeSamples.ModelName });
    }

    /// <summary>Creates the card with the built-in sample story.</summary>
    public ReasoningTree()
    {
        Thoughts = new AvaloniaList<ReasoningThought>();
        Thoughts.CollectionChanged += OnThoughtsChanged;
        AutomationProperties.SetName(this, Title);
    }

    /// <summary>
    /// The short question on the root node (about three short lines at most; cut with an ellipsis). The default belongs to
    /// the sample story: set it whenever you give your own thoughts.
    /// </summary>
    public string Question { get => GetValue(QuestionProperty); set => SetValue(QuestionProperty, value); }

    /// <summary>The full question, shown in the root's tag and read to screen readers; null uses <see cref="Question"/>.</summary>
    public string? QuestionDetail { get => GetValue(QuestionDetailProperty); set => SetValue(QuestionDetailProperty, value); }

    /// <summary>
    /// The thoughts at depth 1 with their children (read to depth 3). Empty (and no <see cref="ThoughtsSource"/>) plays
    /// the built-in sample story. Changing a thought starts the search over.
    /// </summary>
    [Content]
    public AvaloniaList<ReasoningThought> Thoughts { get; }

    /// <summary>Thoughts at depth 1 from a view model; when set, used instead of <see cref="Thoughts"/>.</summary>
    public IEnumerable<ReasoningThought>? ThoughtsSource { get => GetValue(ThoughtsSourceProperty); set => SetValue(ThoughtsSourceProperty, value); }

    /// <summary>How many thoughts survive each depth, 1 to 3 (default 2; clamped). A new value starts the search over and plays it.</summary>
    public int BeamWidth { get => GetValue(BeamWidthProperty); set => SetValue(BeamWidthProperty, value); }

    /// <summary>Seeds the scores of thoughts without one, so the same tree and seed always search the same way (default 7).</summary>
    public int Seed { get => GetValue(SeedProperty); set => SetValue(SeedProperty, value); }

    /// <summary>The color of kept thoughts and the winning path (default: chart series 1). The answer always uses the success color.</summary>
    public IBrush? AccentBrush { get => GetValue(AccentBrushProperty); set => SetValue(AccentBrushProperty, value); }

    /// <summary>Every word the card shows or announces.</summary>
    public ReasoningTreeLabels Labels { get => GetValue(LabelsProperty); set => SetValue(LabelsProperty, value); }

    /// <summary>The answer of the current search once it is reached; null before.</summary>
    public ReasoningThought? Answer { get => _answer; private set => SetAndRaise(AnswerProperty, ref _answer, value); }

    /// <summary>Raised once each time the search reaches its answer, with the answer and the path from depth 1.</summary>
    public event EventHandler<ReasoningTreeAnsweredEventArgs>? Answered
    {
        add => AddHandler(AnsweredEvent, value);
        remove => RemoveHandler(AnsweredEvent, value);
    }

    /// <summary>The stage (tests).</summary>
    internal ReasoningTreeStage? Stage => _stage;

    /// <summary>The scene (tests).</summary>
    internal ReasoningTreeScene Scene => _scene;

    /// <summary>The snapshot the story was built from (tests).</summary>
    internal ReasoningTreeModel? SearchModel => _model;

    /// <summary>The current search (tests).</summary>
    internal ReasoningTreeResult? Result => _result;

    /// <summary>The header status (tests).</summary>
    internal ReasoningTreeStatus? Status => _status;

    /// <summary>Keeps the clock's frames coming for a transition the stage started (hover, tag).</summary>
    internal void KeepFrames(double seconds) => RequestFrames(seconds);

    /// <summary>True when changes animate (full motion).</summary>
    internal bool Animates => IsTravelEnabled;

    private protected override bool StepsPlayThrough => false;

    private protected override void BuildStory(ExplainerScript script)
    {
        EnsureModel();
        _result = _results[BeamWidth - 1];
        _stage?.SetModel(_model!, _results, _result, Labels);
        var t = ReadBeat;
        foreach (var depth in _result.Depths)
        {
            var d = depth;
            script.At(t, () => OnPropose(d), isStop: true);
            t += ProposeBeat;
            script.At(t, () => OnScore(d), isStop: true);
            t += ScoreBeat;
            if (d.IsFinal)
            {
                script.At(t, OnAnswer, isStop: true);
            }
            else
            {
                script.At(t, () => OnKeep(d), isStop: true);
                t += KeepBeat;
            }
        }
    }

    private protected override void ResetScene()
    {
        EnsureModel();
        _scene.Reset(_model!.Nodes.Count, WallTime, IsTravelEnabled);
        Answer = null;
        UpdateStatus();
        SyncStage();
    }

    private protected override void OnTimeChanged()
    {
        if (_stage is null)
        {
            return;
        }
        var remaining = _stage.OnFrame(WallTime);
        if (remaining > 0)
        {
            RequestFrames(remaining);
        }
    }

    private protected override void OnFinished() => UpdateStatus();

    private protected override void OnPlaybackChanged()
    {
        UpdateStatus();
        if (IsPlaying)
        {
            _storyStarted = true;
            _pauseAnnouncePending = false;
            // Resuming: the live region goes quiet again.
            Announcer?.Clear();
            return;
        }
        if (IsReaderDriven && !IsFinished && !_pauseAnnouncePending)
        {
            // A pause by the reader says where the search stands, unless a step announces its own beat right after.
            _pauseAnnouncePending = true;
            Dispatcher.UIThread.Post(() =>
            {
                if (_pauseAnnouncePending && !IsPlaying)
                {
                    Announce(BeatAnnouncement());
                }
                _pauseAnnouncePending = false;
            });
        }
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        if (_beam is not null)
        {
            _beam.SelectionChanged -= OnBeamSelectionChanged;
        }
        _stage = e.NameScope.Find<ReasoningTreeStage>(StagePart);
        _status = e.NameScope.Find<ReasoningTreeStatus>(StatusPart);
        _beam = e.NameScope.Find<SegmentedControl>(BeamPart);
        _beamLabel = e.NameScope.Find<TextBlock>(BeamLabelPart);
        _transport = e.NameScope.Find<Panel>(TransportPart);
        if (_beam is not null)
        {
            if (_beam.ItemCount == 0)
            {
                _beam.ItemsSource = new[]
                {
                    new SegmentedOption(1, "1"),
                    new SegmentedOption(2, "2"),
                    new SegmentedOption(3, "3")
                };
            }
            SyncBeam();
            _beam.SelectionChanged += OnBeamSelectionChanged;
        }
        _stage?.Attach(this, _scene);
        if (_stage is not null && _model is not null && _result is not null)
        {
            _stage.SetModel(_model, _results, _result, Labels);
            SyncStage();
        }
        UpdateStatus();
        UpdateTexts();
    }

    protected override void OnSizeChanged(SizeChangedEventArgs e)
    {
        base.OnSizeChanged(e);
        var width = e.NewSize.Width;
        PseudoClasses.Set(CompactPseudoClass, width < CompactWidth);
        PseudoClasses.Set(TinyPseudoClass, width < TinyWidth);
        UpdateTexts();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == BeamWidthProperty)
        {
            SyncBeam();
            OnBeamWidthChanged();
        }
        else if (change.Property == QuestionProperty || change.Property == QuestionDetailProperty || change.Property == SeedProperty)
        {
            DataChanged();
        }
        else if (change.Property == ThoughtsSourceProperty)
        {
            WatchSource();
            DataChanged();
        }
        else if (change.Property == LabelsProperty)
        {
            if (_model is not null && _result is not null)
            {
                _stage?.SetModel(_model, _results, _result, Labels);
            }
            UpdateTexts();
            UpdateStatus();
            SyncStage();
        }
        else if (change.Property == TitleProperty)
        {
            AutomationProperties.SetName(this, Title);
        }
    }

    private static int CoerceBeamWidth(AvaloniaObject owner, int value) =>
        Math.Clamp(value, ReasoningTreeSearch.MinBeamWidth, ReasoningTreeSearch.MaxBeamWidth);

    private IEnumerable<ReasoningThought> EffectiveThoughts()
    {
        if (ThoughtsSource is { } source)
        {
            return source;
        }
        return Thoughts.Count > 0 ? Thoughts : ReasoningTreeSamples.CreateThoughts();
    }

    private string EffectiveDetail()
    {
        if (!string.IsNullOrWhiteSpace(QuestionDetail))
        {
            return QuestionDetail!;
        }
        return Question == ReasoningTreeSamples.Question ? ReasoningTreeSamples.QuestionDetail : Question;
    }

    private void EnsureModel()
    {
        if (!_modelDirty && _model is not null)
        {
            return;
        }
        _modelDirty = false;
        _dataSeen = DataState();
        _model = ReasoningTreeModel.Build(Question ?? string.Empty, EffectiveDetail(), EffectiveThoughts(), Seed);
        _results =
        [
            ReasoningTreeSearch.Run(_model, 1),
            ReasoningTreeSearch.Run(_model, 2),
            ReasoningTreeSearch.Run(_model, 3)
        ];
    }

    private void OnPropose(ReasoningTreeDepth depth)
    {
        _storyStarted = true;
        _scene.Propose(_model!, depth, WallTime, IsTravelEnabled, _stage?.IsWide ?? true);
        AfterBeat(Labels.ProposedAnnouncement(depth.Depth, depth.Candidates.Length));
    }

    private void OnScore(ReasoningTreeDepth depth)
    {
        _storyStarted = true;
        _scene.Score(_model!, depth, WallTime, IsTravelEnabled);
        var best = _model!.Nodes[ReasoningTreeSearch.Best(_model, depth.Candidates)];
        AfterBeat(Labels.ScoredAnnouncement(depth.Depth, depth.Candidates.Length, best.Label, Labels.FormatScore(best.Score)));
    }

    private void OnKeep(ReasoningTreeDepth depth)
    {
        _storyStarted = true;
        _scene.Keep(depth, Labels.KeptCount(depth.Kept.Length, depth.Candidates.Length), WallTime, IsTravelEnabled);
        AfterBeat(Labels.KeptAnnouncement(depth.Depth, depth.Kept.Length, depth.Candidates.Length));
    }

    private void OnAnswer()
    {
        _storyStarted = true;
        var result = _result!;
        var model = _model!;
        var last = result.Depths[^1];
        _scene.Answer(result, Labels.BestOf(last.Candidates.Length), WallTime, IsTravelEnabled);
        _pauseAnnouncePending = false;
        UpdateStatus();
        SyncStage();
        var answer = model.Nodes[result.Answer];
        Answer = answer.Thought;
        Announce(Labels.AnswerAnnouncement(Labels.FormatScore(answer.Score), answer.Text));
        if (answer.Thought is { } thought)
        {
            var path = result.Path.Select(i => model.Nodes[i].Thought).OfType<ReasoningThought>().ToArray();
            RaiseEvent(new ReasoningTreeAnsweredEventArgs(AnsweredEvent, thought, path, result.BeamWidth, answer.Score));
        }
    }

    private void AfterBeat(string announcement)
    {
        _pauseAnnouncePending = false;
        UpdateStatus();
        SyncStage();
        AnnounceBeat(announcement);
    }

    private void SyncStage()
    {
        if (_stage is null)
        {
            return;
        }
        _stage.Sync(WallTime, IsTravelEnabled);
        var remaining = _scene.BusyUntil - WallTime;
        if (remaining > 0)
        {
            RequestFrames(remaining + 0.05);
        }
    }

    private void UpdateStatus()
    {
        if (_status is null || _model is null)
        {
            return;
        }
        var labels = Labels;
        if (_model.ThoughtCount == 0)
        {
            _status.Set(labels.NothingToSearch, null, ExplainerTone.Muted, 14, false);
            return;
        }
        var depth = _result?.DepthOf(_scene.BeatDepth);
        var candidates = depth?.Candidates.Length ?? 0;
        var kept = depth?.Kept.Length ?? 0;
        if (_scene.Beat == ReasoningTreeBeat.Answer)
        {
            _status.Set(labels.AnswerFound, ArcIcons.CircleCheck, ExplainerTone.Success, 14, false);
        }
        else if (IsPlaying)
        {
            var text = _scene.Beat switch
            {
                ReasoningTreeBeat.Propose => labels.Proposing(candidates),
                ReasoningTreeBeat.Score => labels.Scoring(candidates),
                ReasoningTreeBeat.Keep => labels.Keeping(kept),
                _ => labels.Reading
            };
            _status.Set(text, null, ExplainerTone.Neutral, 14, true);
        }
        else if (_scene.Beat == ReasoningTreeBeat.Ready)
        {
            _status.Set(labels.Ready, null, ExplainerTone.Neutral, 14, false);
        }
        else
        {
            var text = _scene.Beat switch
            {
                ReasoningTreeBeat.Propose => labels.Proposed(candidates),
                ReasoningTreeBeat.Score => labels.Scored(candidates),
                _ => labels.KeptBest(kept)
            };
            _status.Set(text, ArcIcons.Pause, ExplainerTone.Muted, 12, false);
        }
    }

    /// <summary>What the live region says about the current beat (when the reader pauses).</summary>
    private string BeatAnnouncement()
    {
        var labels = Labels;
        var depth = _result?.DepthOf(_scene.BeatDepth);
        if (_model is null || depth is null)
        {
            return labels.ReadyAnnouncement;
        }
        switch (_scene.Beat)
        {
            case ReasoningTreeBeat.Propose:
                return labels.ProposedAnnouncement(depth.Depth, depth.Candidates.Length);
            case ReasoningTreeBeat.Score:
                var best = _model.Nodes[ReasoningTreeSearch.Best(_model, depth.Candidates)];
                return labels.ScoredAnnouncement(depth.Depth, depth.Candidates.Length, best.Label, labels.FormatScore(best.Score));
            case ReasoningTreeBeat.Keep:
                return labels.KeptAnnouncement(depth.Depth, depth.Kept.Length, depth.Candidates.Length);
            default:
                return labels.ReadyAnnouncement;
        }
    }

    private void UpdateTexts()
    {
        if (_beamLabel is not null)
        {
            _beamLabel.Text = Bounds.Width > 0 && Bounds.Width < CompactWidth ? Labels.BeamWidthShort : Labels.BeamWidth;
        }
        if (_beam is not null)
        {
            AutomationProperties.SetName(_beam, Labels.BeamWidth);
        }
        if (_transport is not null)
        {
            AutomationProperties.SetName(_transport, Labels.PlaybackGroup);
        }
    }

    private void SyncBeam()
    {
        if (_beam is null)
        {
            return;
        }
        _syncingBeam = true;
        try
        {
            _beam.SelectedIndex = BeamWidth - 1;
        }
        finally
        {
            _syncingBeam = false;
        }
    }

    private void OnBeamSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_syncingBeam || _beam is null || _beam.SelectedIndex < 0)
        {
            return;
        }
        SetCurrentValue(BeamWidthProperty, _beam.SelectedIndex + 1);
    }

    private void OnBeamWidthChanged()
    {
        if (_model is null)
        {
            return;
        }
        if (IsTravelEnabled)
        {
            // Starts over and plays (before the card is first seen it only rebuilds; first sight plays it).
            Rebuild(play: true);
            if (IsPlaying)
            {
                // After the restart (which quiets the region as play resumes), say why the search started over.
                Announce(Labels.BeamWidthAnnouncement(BeamWidth));
            }
        }
        else if (_storyStarted)
        {
            // Reduced motion shows finished searches: the new width's answer appears at once.
            Rebuild(play: false);
            JumpToEnd();
        }
        else
        {
            Rebuild();
        }
    }

    private void OnThoughtsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        WatchThoughts();
        DataChanged();
    }

    private protected override void WatchData(bool watch)
    {
        WatchSource();
        if (watch && _dataSeen is not null && !_dataSeen.SequenceEqual(DataState()))
        {
            // Changed while nothing listened: search again as a change seen live would.
            DataChanged();
        }
    }

    /// <summary>A view model's list and its thoughts can outlive the card: they are observed only while it is attached.</summary>
    private void WatchSource()
    {
        if (_watchedSource is not null)
        {
            _watchedSource.CollectionChanged -= OnThoughtsChanged;
        }
        _watchedSource = IsAttached ? ThoughtsSource as INotifyCollectionChanged : null;
        if (_watchedSource is not null)
        {
            _watchedSource.CollectionChanged += OnThoughtsChanged;
        }
        WatchThoughts();
    }

    private void WatchThoughts()
    {
        foreach (var thought in _watched)
        {
            thought.Changed -= OnThoughtChanged;
        }
        _watched.Clear();
        if (!IsAttached)
        {
            return;
        }
        IEnumerable list = ThoughtsSource ?? (IEnumerable)Thoughts;
        foreach (var item in list)
        {
            if (item is ReasoningThought thought)
            {
                thought.Changed += OnThoughtChanged;
                _watched.Add(thought);
            }
        }
    }

    private void OnThoughtChanged(object? sender, EventArgs e) => DataChanged();

    /// <summary>The given thoughts the search reads (to depth 3) and their values, to tell whether they changed unobserved.</summary>
    private object?[] DataState()
    {
        var state = new List<object?>();
        // An empty Thoughts (the sample story) reads as no thoughts.
        foreach (var thought in ThoughtsSource ?? Thoughts)
        {
            AddState(state, thought, 1);
        }
        return state.ToArray();
    }

    private static void AddState(List<object?> state, ReasoningThought? thought, int depth)
    {
        state.Add(thought);
        if (thought is null)
        {
            return;
        }
        state.Add(thought.Key);
        state.Add(thought.Label);
        state.Add(thought.Text);
        state.Add(thought.Score);
        if (depth >= ReasoningTreeModel.MaxDepth)
        {
            return;
        }
        // The count marks where this thought's children end.
        state.Add(thought.Children.Count);
        foreach (var child in thought.Children)
        {
            if (!ReferenceEquals(child, thought))
            {
                AddState(state, child, depth + 1);
            }
        }
    }

    /// <summary>The data changed: search again once the current batch of changes is done.</summary>
    private void DataChanged()
    {
        _modelDirty = true;
        if (_rebuildPosted || _model is null)
        {
            return;
        }
        _rebuildPosted = true;
        Dispatcher.UIThread.Post(RebuildFromData, DispatcherPriority.Background);
    }

    private void RebuildFromData()
    {
        _rebuildPosted = false;
        if (!_modelDirty)
        {
            return;
        }
        var wasFinished = IsFinished;
        var started = _storyStarted;
        Rebuild();
        if (started && wasFinished && !IsTravelEnabled)
        {
            // Reduced motion keeps showing a finished search.
            JumpToEnd();
        }
    }
}
