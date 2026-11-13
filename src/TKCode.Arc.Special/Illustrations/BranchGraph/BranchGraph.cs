using System.Collections.Specialized;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Metadata;
using Avalonia.Threading;
using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;

namespace TKCode.Arc;

/// <summary>
/// A git commit graph that previews how a pull request lands. Each commit is a row (newest on top) with its branch's
/// colored lane on the left, the message and branch pills, the author, the short hash and the date; merge commits get a
/// ring and an icon. The strategy control in the header switches between the history as it is (Before) and three
/// previews computed from the data: Merge (a merge commit on top joins both lines), Rebase (the pull request's commits
/// lift like cards and are replayed on the target's tip with new hashes) and Squash (they fold into one commit). The
/// graph draws in on first sight, from the oldest commit up; while nobody interacts it cycles Before → Rebase → Merge →
/// Squash, 2.6 s after each change settles. Hovering or arrowing to a commit shows its message, author and diff stats
/// and dims the other branches; a click or a tap pins that card.
/// <para>
/// The <see cref="Explainer"/> clock drives the cycle: <see cref="Explainer.AutoPlay"/> starts it on first sight,
/// <see cref="Explainer.IsPlaying"/> is true while it cycles, <see cref="Explainer.Pause"/> holds it (picks by the reader
/// still animate), <see cref="Explainer.StepForward"/> moves to the next strategy, <see cref="Explainer.Restart"/> draws the
/// graph in again and cycles from Before, and <see cref="Explainer.Speed"/> scales every animation (0 shows each state at
/// once). The cycle always repeats, so <see cref="Explainer.IsLooping"/> has no effect; <see cref="Explainer.Finished"/>
/// is raised each time it returns to Before. The first pick, tap or key press stops it for good, and a pointer resting
/// on the commits holds it. <see cref="Explainer.AreControlsVisible"/> shows the strategy control. Under reduced motion
/// every state shows at once and nothing cycles.
/// </para>
/// </summary>
[TemplatePart(StagePart, typeof(BranchGraphStage))]
[TemplatePart(StrategyPart, typeof(SegmentedControl))]
[TemplatePart(SummaryPart, typeof(TextBlock))]
[TemplatePart(HeaderPart, typeof(Grid))]
[TemplatePart(HeadingPart, typeof(Control))]
[TemplatePart(TitlePart, typeof(TextBlock))]
[TemplatePart(NumberPart, typeof(TextBlock))]
public class BranchGraph : Explainer
{
    /// <summary>The commit list and its graph.</summary>
    public const string StagePart = "PART_Stage";

    /// <summary>The strategy control (a <see cref="SegmentedControl"/>).</summary>
    public const string StrategyPart = "PART_Strategy";

    /// <summary>The summary line under the commits.</summary>
    public const string SummaryPart = "PART_Summary";

    /// <summary>The header row (title and strategy control).</summary>
    public const string HeaderPart = "PART_Header";

    /// <summary>The heading: icon, title and pull request number.</summary>
    public const string HeadingPart = "PART_Heading";

    /// <summary>The title.</summary>
    public const string TitlePart = "PART_Title";

    /// <summary>The pull request number.</summary>
    public const string NumberPart = "PART_Number";

    /// <summary>The history from a view model, oldest first; when set it replaces <see cref="Commits"/>.</summary>
    public static readonly StyledProperty<IEnumerable<BranchGraphCommit>?> ItemsSourceProperty =
        AvaloniaProperty.Register<BranchGraph, IEnumerable<BranchGraphCommit>?>(nameof(ItemsSource));

    /// <summary>No longer has an effect: the newest commit is always on top.</summary>
    [Obsolete("No longer has an effect: the newest commit is always on top (deprecated in 1.2.0).")]
    public static readonly StyledProperty<bool> IsNewestFirstProperty =
        AvaloniaProperty.Register<BranchGraph, bool>(nameof(IsNewestFirst));

    /// <summary>Opens with every commit drawn (no first-load sweep) and without the autoplay cycle. Use it for a still graph.</summary>
    public static readonly StyledProperty<bool> StartsFinishedProperty =
        AvaloniaProperty.Register<BranchGraph, bool>(nameof(StartsFinished));

    /// <summary>The commit whose detail card is pinned; null for none. A click, a tap or the arrow keys on the commits set it, Escape clears it.</summary>
    public static readonly StyledProperty<BranchGraphCommit?> SelectedCommitProperty =
        AvaloniaProperty.Register<BranchGraph, BranchGraphCommit?>(nameof(SelectedCommit), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>Every word the card shows or announces.</summary>
    public static readonly StyledProperty<BranchGraphLabels> LabelsProperty =
        AvaloniaProperty.Register<BranchGraph, BranchGraphLabels>(nameof(Labels), new BranchGraphLabels());

    /// <summary>How many commits have been drawn (the first-load sweep draws them one by one).</summary>
    public static readonly DirectProperty<BranchGraph, int> RevealedCountProperty =
        AvaloniaProperty.RegisterDirect<BranchGraph, int>(nameof(RevealedCount), o => o.RevealedCount);

    /// <summary>Raised as each commit is drawn, with the commit, its index and what it did.</summary>
    public static readonly RoutedEvent<BranchGraphCommitEventArgs> CommitDrawnEvent =
        RoutedEvent.Register<BranchGraph, BranchGraphCommitEventArgs>(nameof(CommitDrawn), RoutingStrategies.Bubble);

    /// <summary>The preview shown: the history as it is, or the pull request merged, rebased or squashed (default Before).</summary>
    public static readonly StyledProperty<BranchGraphStrategy> SelectedStrategyProperty =
        AvaloniaProperty.Register<BranchGraph, BranchGraphStrategy>(nameof(SelectedStrategy), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>Raised when <see cref="SelectedStrategy"/> changes, by the reader, by code or by the autoplay cycle.</summary>
    public static readonly RoutedEvent<BranchGraphSelectionChangedEventArgs> SelectionChangedEvent =
        RoutedEvent.Register<BranchGraph, BranchGraphSelectionChangedEventArgs>(nameof(SelectionChanged), RoutingStrategies.Bubble);

    /// <summary>The pull request's title: the heading (unless <see cref="Explainer.Title"/> is set) and the squashed commit's message.</summary>
    public static readonly StyledProperty<string?> PullRequestTitleProperty =
        AvaloniaProperty.Register<BranchGraph, string?>(nameof(PullRequestTitle));

    /// <summary>The pull request's number, shown as "#N" after the heading (0 shows none).</summary>
    public static readonly StyledProperty<int> PullRequestNumberProperty =
        AvaloniaProperty.Register<BranchGraph, int>(nameof(PullRequestNumber));

    /// <summary>The pull request's branch, whose commits merge, rebase or squash; null picks the newest branch not merged yet.</summary>
    public static readonly StyledProperty<string?> SourceBranchProperty =
        AvaloniaProperty.Register<BranchGraph, string?>(nameof(SourceBranch));

    /// <summary>The branch the pull request lands on; null takes the first branch of the history (usually main).</summary>
    public static readonly StyledProperty<string?> TargetBranchProperty =
        AvaloniaProperty.Register<BranchGraph, string?>(nameof(TargetBranch));

    /// <summary>The pause after a change settles before the autoplay cycle picks the next strategy (seconds at speed 1).</summary>
    internal const double Dwell = 2.6;

    /// <summary>When the pointer leaves, the cycle goes on after at most this long.</summary>
    internal const double ResumeAfterHover = 1.2;

    private static readonly BranchGraphStrategy[] s_cycle =
        [BranchGraphStrategy.Rebase, BranchGraphStrategy.Merge, BranchGraphStrategy.Squash, BranchGraphStrategy.Before];

    private static readonly IReadOnlyList<BranchGraphCommit> s_sample = BranchGraphSamples.CreateCommits();
    private INotifyCollectionChanged? _observedSource;
    private BranchGraphStage? _stage;
    private SegmentedControl? _strategy;
    private TextBlock? _summary;
    private Grid? _header;
    private Control? _heading;
    private TextBlock? _title;
    private TextBlock? _number;
    private IReadOnlyList<BranchGraphCommit> _history = Array.Empty<BranchGraphCommit>();
    private BranchGraphModel? _model;
    private ViewportVisibility? _sight;
    private SegmentedOption[] _options = [];
    private int _revealed;
    private bool _drawn;
    private bool _usesSample;
    private bool _rebuildPending;
    private bool _autoPicking;
    private bool _userPicking;
    private bool _syncingStrategy;
    private bool _syncingCommit;
    private bool _cycle;
    private bool _hovered;
    private bool _loopRestart;
    private bool _loopPending;

    static BranchGraph()
    {
        TitleProperty.OverrideDefaultValue<BranchGraph>("Branch history");
        AutomationProperties.ControlTypeOverrideProperty.OverrideDefaultValue<BranchGraph>(AutomationControlType.Group);
    }

    /// <summary>Creates a graph that previews the sample pull request until it is given commits.</summary>
    public BranchGraph()
    {
        Commits.CollectionChanged += (_, _) =>
        {
            if (ItemsSource is null)
            {
                ScheduleRebuild();
            }
        };
        UpdateAccessibleName();
    }

    /// <summary>The history, oldest first (the XAML content). While it is empty and <see cref="ItemsSource"/> is null, an invented sample pull request is shown.</summary>
    [Content]
    public AvaloniaList<BranchGraphCommit> Commits { get; } = new();

    /// <inheritdoc cref="ItemsSourceProperty"/>
    public IEnumerable<BranchGraphCommit>? ItemsSource { get => GetValue(ItemsSourceProperty); set => SetValue(ItemsSourceProperty, value); }

    /// <summary>No longer has an effect: the newest commit is always on top.</summary>
    [Obsolete("No longer has an effect: the newest commit is always on top (deprecated in 1.2.0).")]
    public bool IsNewestFirst { get => GetValue(IsNewestFirstProperty); set => SetValue(IsNewestFirstProperty, value); }

    /// <inheritdoc cref="StartsFinishedProperty"/>
    public bool StartsFinished { get => GetValue(StartsFinishedProperty); set => SetValue(StartsFinishedProperty, value); }

    /// <inheritdoc cref="SelectedCommitProperty"/>
    public BranchGraphCommit? SelectedCommit { get => GetValue(SelectedCommitProperty); set => SetValue(SelectedCommitProperty, value); }

    /// <inheritdoc cref="LabelsProperty"/>
    public BranchGraphLabels Labels { get => GetValue(LabelsProperty); set => SetValue(LabelsProperty, value); }

    /// <inheritdoc cref="RevealedCountProperty"/>
    public int RevealedCount { get => _revealed; private set => SetAndRaise(RevealedCountProperty, ref _revealed, value); }

    /// <inheritdoc cref="SelectedStrategyProperty"/>
    public BranchGraphStrategy SelectedStrategy { get => GetValue(SelectedStrategyProperty); set => SetValue(SelectedStrategyProperty, value); }

    /// <inheritdoc cref="PullRequestTitleProperty"/>
    public string? PullRequestTitle { get => GetValue(PullRequestTitleProperty); set => SetValue(PullRequestTitleProperty, value); }

    /// <inheritdoc cref="PullRequestNumberProperty"/>
    public int PullRequestNumber { get => GetValue(PullRequestNumberProperty); set => SetValue(PullRequestNumberProperty, value); }

    /// <inheritdoc cref="SourceBranchProperty"/>
    public string? SourceBranch { get => GetValue(SourceBranchProperty); set => SetValue(SourceBranchProperty, value); }

    /// <inheritdoc cref="TargetBranchProperty"/>
    public string? TargetBranch { get => GetValue(TargetBranchProperty); set => SetValue(TargetBranchProperty, value); }

    /// <summary>Raised as each commit is drawn, with the commit, its index and what it did.</summary>
    public event EventHandler<BranchGraphCommitEventArgs>? CommitDrawn
    {
        add => AddHandler(CommitDrawnEvent, value);
        remove => RemoveHandler(CommitDrawnEvent, value);
    }

    /// <summary>Raised when <see cref="SelectedStrategy"/> changes, by the reader, by code or by the autoplay cycle.</summary>
    public event EventHandler<BranchGraphSelectionChangedEventArgs>? SelectionChanged
    {
        add => AddHandler(SelectionChangedEvent, value);
        remove => RemoveHandler(SelectionChangedEvent, value);
    }

    internal BranchGraphModel? GraphModel => _model;

    internal bool IsDrawn => _drawn;

    internal BranchGraphStage? Stage => _stage;

    internal SegmentedControl? StrategyControl => _strategy;

    /// <summary>The summary line now.</summary>
    internal string SummaryText => _model?[SelectedStrategy].Summary ?? string.Empty;

    /// <summary>The heading shown: <see cref="Explainer.Title"/> when set, otherwise the pull request's title.</summary>
    internal string HeadingText => IsSet(TitleProperty) ? Title ?? string.Empty : EffectiveTitle ?? Title ?? string.Empty;

    /// <summary>True while the autoplay cycle may still pick (no reader has interacted).</summary>
    internal bool IsCycling => _cycle;

    private string? EffectiveTitle => !string.IsNullOrWhiteSpace(PullRequestTitle) ? PullRequestTitle : _usesSample ? BranchGraphSamples.PullRequestTitle : null;

    private int EffectiveNumber => PullRequestNumber > 0 ? PullRequestNumber : _usesSample ? BranchGraphSamples.PullRequestNumber : 0;

    private protected override bool IsClockHeld => _hovered && IsPlaying;

    /// <summary>A step moves to the next strategy at once; the pick animates from there.</summary>
    private protected override bool StepsPlayThrough => false;

    private protected override bool OpensFinishedWhenReduced => false;

    /// <summary>A commit's accessible name in a preview.</summary>
    internal string Describe(BranchGraphItem item, BranchGraphLayout layout)
    {
        var labels = Labels;
        var text = labels.Describe(item.Message, layout.Hash[item.Index], item.Branch, item.Author);
        if (item.Date.Length > 0)
        {
            text += ", " + item.Date;
        }
        text += ".";
        if (item.Additions is not null || item.Deletions is not null || item.Files is not null)
        {
            text += " " + labels.Stats(item.Additions ?? 0, item.Deletions ?? 0, item.Files ?? 0) + ".";
        }
        var heads = layout.Pills[item.Index];
        if (heads.Length > 0)
        {
            text += " " + labels.Heads(string.Join(" and ", heads.Select(p => p.Text))) + ".";
        }
        return text;
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        if (_strategy is not null)
        {
            _strategy.SelectionChanged -= OnStrategyPicked;
        }
        _stage = e.NameScope.Find<BranchGraphStage>(StagePart);
        _strategy = e.NameScope.Find<SegmentedControl>(StrategyPart);
        _summary = e.NameScope.Find<TextBlock>(SummaryPart);
        _header = e.NameScope.Find<Grid>(HeaderPart);
        _heading = e.NameScope.Find<Control>(HeadingPart);
        _title = e.NameScope.Find<TextBlock>(TitlePart);
        _number = e.NameScope.Find<TextBlock>(NumberPart);
        if (_strategy is not null)
        {
            _strategy.SelectionChanged += OnStrategyPicked;
        }
        UpdateOptions();
        _stage?.Attach(this);
        UpdateChrome();
        UpdateSizing(Bounds.Width);
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _sight?.Dispose();
        _sight = null;
        if (!_drawn)
        {
            if (StartsFinished)
            {
                DrawAtOnce();
            }
            else
            {
                _sight = new ViewportVisibility(this, 0.6, once: true);
                _sight.VisibilityChanged += OnFirstSight;
            }
        }
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _sight?.Dispose();
        _sight = null;
    }

    private void OnFirstSight(object? sender, bool visible)
    {
        if (!visible || _drawn)
        {
            return;
        }
        _drawn = true;
        if (StartsFinished || !ArcMotion.IsFull(this))
        {
            _stage?.ShowAll();
        }
        else
        {
            _stage?.StartSweep();
        }
    }

    private void DrawAtOnce()
    {
        _drawn = true;
        SetCurrentValue(AutoPlayProperty, false);
        _stage?.ShowAll();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ItemsSourceProperty)
        {
            ObserveSource(IsAttached ? ItemsSource : null);
            ScheduleRebuild();
        }
        else if (change.Property == PullRequestTitleProperty || change.Property == PullRequestNumberProperty ||
                 change.Property == SourceBranchProperty || change.Property == TargetBranchProperty)
        {
            ScheduleRebuild();
            UpdateChrome();
        }
        else if (change.Property == SelectedStrategyProperty)
        {
            OnStrategyChanged(change.GetOldValue<BranchGraphStrategy>(), change.GetNewValue<BranchGraphStrategy>());
        }
        else if (change.Property == SelectedCommitProperty)
        {
            if (!_syncingCommit)
            {
                _stage?.SetActiveCommit(SelectedCommit);
            }
        }
        else if (change.Property == StartsFinishedProperty && StartsFinished && IsAttached && !_drawn)
        {
            DrawAtOnce();
        }
        else if (change.Property == LabelsProperty)
        {
            ScheduleRebuild();
            UpdateOptions();
            UpdateChrome();
        }
        else if (change.Property == TitleProperty)
        {
            UpdateChrome();
        }
        else if (change.Property == AreControlsVisibleProperty)
        {
            UpdateChrome();
        }
        else if (change.Property == IsPlayingProperty)
        {
            if (IsPlaying)
            {
                _cycle = true;
            }
            else if (!IsFinished)
            {
                // Paused or stepped: the reader holds the preview.
                _cycle = false;
            }
        }
        else if (change.Property == BoundsProperty)
        {
            UpdateSizing(change.GetNewValue<Rect>().Width);
        }
    }

    private void OnStrategyChanged(BranchGraphStrategy oldValue, BranchGraphStrategy newValue)
    {
        var user = _userPicking;
        if (!_autoPicking)
        {
            StopCycle();
        }
        _stage?.GoTo(newValue);
        SyncStrategyControl();
        UpdateChrome();
        RaiseEvent(new BranchGraphSelectionChangedEventArgs(SelectionChangedEvent, this, oldValue, newValue, user));
        if (user)
        {
            Announce(SummaryText);
        }
        if (_autoPicking)
        {
            AnnounceBeat(SummaryText);
        }
    }

    private void OnStrategyPicked(object? sender, SelectionChangedEventArgs e)
    {
        if (_syncingStrategy || _strategy?.SelectedItem is not SegmentedOption { Value: BranchGraphStrategy value } || value == SelectedStrategy)
        {
            return;
        }
        _userPicking = true;
        try
        {
            SetCurrentValue(SelectedStrategyProperty, value);
        }
        finally
        {
            _userPicking = false;
        }
    }

    /// <summary>The reader took over (a pick, a tap, a key): the autoplay cycle stops for good.</summary>
    internal void OnUserInteraction() => StopCycle();

    private void StopCycle()
    {
        _cycle = false;
        if (IsPlaying)
        {
            Pause();
        }
        else if (!_drawn && AutoPlay)
        {
            SetCurrentValue(AutoPlayProperty, false);
        }
    }

    /// <summary>The pointer rests on the commits (holds the cycle) or left them (the cycle goes on within a second or so).</summary>
    internal void OnHoverChanged(bool hovered)
    {
        if (hovered == _hovered)
        {
            return;
        }
        _hovered = hovered;
        ClockHoldChanged();
        if (!hovered && IsPlaying && Script.NextBeatTime() is { } next && next - Now > ResumeAfterHover)
        {
            Advance(next - Now - ResumeAfterHover);
        }
    }

    internal void OnActiveChanged(BranchGraphCommit? commit)
    {
        _syncingCommit = true;
        try
        {
            SetCurrentValue(SelectedCommitProperty, commit);
        }
        finally
        {
            _syncingCommit = false;
        }
    }

    internal void OnCommitDrawn(BranchGraphItem item)
    {
        if (item.Commit is null)
        {
            return;
        }
        RevealedCount = Math.Min(_history.Count, RevealedCount + 1);
        RaiseEvent(new BranchGraphCommitEventArgs(CommitDrawnEvent, this, item.Commit, item.CommitIndex, item.Kind));
    }

    private protected override void WatchData(bool watch) => ObserveSource(watch ? ItemsSource : null);

    private void ObserveSource(IEnumerable<BranchGraphCommit>? source)
    {
        if (_observedSource is not null)
        {
            _observedSource.CollectionChanged -= OnSourceChanged;
        }
        _observedSource = source as INotifyCollectionChanged;
        if (_observedSource is not null)
        {
            _observedSource.CollectionChanged += OnSourceChanged;
        }
    }

    private void OnSourceChanged(object? sender, NotifyCollectionChangedEventArgs e) => ScheduleRebuild();

    private void ScheduleRebuild()
    {
        if (_rebuildPending)
        {
            return;
        }
        // Coalesce: XAML and view models add commits one at a time.
        _rebuildPending = true;
        Dispatcher.UIThread.Post(() =>
        {
            _rebuildPending = false;
            Rebuild();
        }, DispatcherPriority.Background);
    }

    private protected override void BuildStory(ExplainerScript script)
    {
        if (!_loopRestart || _model is null)
        {
            if (ItemsSource is { } source)
            {
                _history = source.Where(c => c is not null).ToArray();
                _usesSample = false;
            }
            else if (Commits.Count > 0)
            {
                _history = Commits.ToArray();
                _usesSample = false;
            }
            else
            {
                _history = s_sample;
                _usesSample = true;
            }
            _model = BranchGraphModel.Build(_history, Labels, EffectiveTitle, EffectiveNumber, SourceBranch, TargetBranch);
        }
        var model = _model;
        if (model.MergeItem < 0)
        {
            script.Length = 0.1;
            return;
        }
        // The cycle: after the first-load sweep (or right away on a repeat), each pick waits for the last to settle plus the dwell.
        var t = _loopRestart ? 0 : SweepSeconds(model);
        var current = BranchGraphStrategy.Before;
        foreach (var strategy in s_cycle)
        {
            t += Dwell;
            var pick = strategy;
            script.At(t, () => AutoPick(pick), isStop: true);
            t += BranchGraphStage.DurationOf(model, current, strategy);
            current = strategy;
        }
        script.Length = t;
    }

    /// <summary>How long the first-load sweep of <paramref name="model"/> takes at speed 1.</summary>
    private static double SweepSeconds(BranchGraphModel model)
    {
        var rows = model[BranchGraphStrategy.Before].RowCount;
        return BranchGraphMotion.SweepStart + Math.Max(0, rows - 1) * 44 / BranchGraphMotion.SweepSpeed + BranchGraphMotion.LandSettle;
    }

    private void AutoPick(BranchGraphStrategy strategy)
    {
        _autoPicking = true;
        try
        {
            SetCurrentValue(SelectedStrategyProperty, strategy);
        }
        finally
        {
            _autoPicking = false;
        }
    }

    private protected override void ResetScene()
    {
        if (_loopRestart)
        {
            return;
        }
        RevealedCount = 0;
        if (SelectedCommit is { } selected && !_history.Contains(selected))
        {
            OnActiveChanged(null);
        }
        if (_model is not null && _stage is not null)
        {
            _stage.SetModel(_model, SelectedStrategy, drawn: false);
            if (_drawn)
            {
                if (StartsFinished || !ArcMotion.IsFull(this))
                {
                    _stage.ShowAll();
                }
                else
                {
                    _stage.StartSweep();
                }
            }
        }
        UpdateOptions();
        UpdateChrome();
    }

    private protected override void OnFinished()
    {
        if (!_cycle || _model is null || _model.MergeItem < 0 || _loopPending)
        {
            return;
        }
        // The cycle goes round again from Before, without drawing the graph in again.
        _loopPending = true;
        Dispatcher.UIThread.Post(() =>
        {
            _loopPending = false;
            if (!_cycle || !IsFinished || !IsAttached)
            {
                return;
            }
            _loopRestart = true;
            try
            {
                Restart();
            }
            finally
            {
                _loopRestart = false;
            }
        }, DispatcherPriority.Background);
    }

    private void UpdateOptions()
    {
        var labels = Labels;
        _options =
        [
            new SegmentedOption(BranchGraphStrategy.Before, labels.Before),
            new SegmentedOption(BranchGraphStrategy.Merge, labels.Merge),
            new SegmentedOption(BranchGraphStrategy.Rebase, labels.Rebase),
            new SegmentedOption(BranchGraphStrategy.Squash, labels.Squash)
        ];
        if (_strategy is not null)
        {
            _syncingStrategy = true;
            try
            {
                _strategy.ItemsSource = _options;
            }
            finally
            {
                _syncingStrategy = false;
            }
            AutomationProperties.SetName(_strategy, labels.StrategyGroup);
        }
        SyncStrategyControl();
    }

    private void SyncStrategyControl()
    {
        if (_strategy is null)
        {
            return;
        }
        _syncingStrategy = true;
        try
        {
            _strategy.SelectedIndex = (int)SelectedStrategy;
        }
        finally
        {
            _syncingStrategy = false;
        }
    }

    private void UpdateChrome()
    {
        var heading = HeadingText;
        if (_title is not null)
        {
            _title.Text = heading;
        }
        var number = EffectiveNumber;
        if (_number is not null)
        {
            _number.Text = number > 0 ? "#" + number : string.Empty;
            _number.IsVisible = number > 0;
        }
        if (_heading is not null)
        {
            _heading.IsVisible = heading.Length > 0;
        }
        if (_strategy is not null)
        {
            _strategy.IsVisible = AreControlsVisible && _model is { MergeItem: >= 0 };
        }
        if (_summary is not null)
        {
            _summary.Text = SummaryText;
        }
        _stage?.UpdateSummary();
        UpdateAccessibleName();
    }

    private void UpdateAccessibleName()
    {
        var heading = HeadingText;
        var name = _model is { Source: { } source } && heading.Length > 0 ? Labels.GraphName(heading, source, _model.Target) : heading;
        AutomationProperties.SetName(this, name.Length > 0 ? name : Title);
    }

    /// <summary>Under 600 px the strategy control takes the header's second row, stretched.</summary>
    internal const double NarrowWidth = 600;

    private void UpdateSizing(double width)
    {
        if (width <= 0 || _header is null || _strategy is null)
        {
            return;
        }
        var narrow = width < NarrowWidth;
        Grid.SetRow(_strategy, narrow ? 1 : 0);
        Grid.SetColumn(_strategy, narrow ? 0 : 1);
        Grid.SetColumnSpan(_strategy, narrow ? 2 : 1);
        _strategy.HorizontalAlignment = narrow ? HorizontalAlignment.Stretch : HorizontalAlignment.Right;
        _strategy.Margin = narrow ? new Thickness(0, 8, 0, 0) : default;
        PseudoClasses.Set(":narrow", narrow);
    }
}

/// <summary>The invented sample pull request a <see cref="BranchGraph"/> shows when it is given no commits.</summary>
public static class BranchGraphSamples
{
    /// <summary>The sample pull request's title.</summary>
    internal const string PullRequestTitle = "Search filters";

    /// <summary>The sample pull request's number.</summary>
    internal const int PullRequestNumber = 317;

    /// <summary>
    /// A fresh copy of the sample history, oldest first: a small shop's catalog on main, a feature branch adding search
    /// filters (the pull request) and a short fix branch that was merged back. The names and messages are invented.
    /// </summary>
    public static IReadOnlyList<BranchGraphCommit> CreateCommits() =>
    [
        Commit("3c1e8a2", "main", "Set up the catalog page", "Priya Shah", "5 days ago", 388, 0, 14,
            "Lists every product with its photo, price and stock."),
        Commit("7a0f4d1", "main", "Add product cards", "Priya Shah", "4 days ago", 146, 12, 6, null),
        Commit("d94b2c7", "feat/filters", "Add a filter sidebar", "Leo Martin", "3 days ago", 212, 35, 8,
            "Splits the catalog into a sidebar and a results grid."),
        Commit("51e6ab9", "fix/currency", "Format prices per locale", "Noor Haddad", "3 days ago", 22, 9, 3,
            "Prices follow the shopper's locale instead of always showing dollars.", parents: "7a0f4d1"),
        Commit("8f2d330", "main", "Merge branch fix/currency", "Noor Haddad", "2 days ago", 22, 9, 3, null, parents: "7a0f4d1, 51e6ab9"),
        Commit("2be97f0", "feat/filters", "Filter by price range", "Leo Martin", "Yesterday", 97, 14, 5,
            "A two-handle slider narrows the results as it moves."),
        Commit("e6c1a84", "main", "Upgrade the image loader", "Sam Ortega", "6 hours ago", 18, 18, 2,
            "Moves to the new loader API and drops a deprecated flag."),
        Commit("0d7f5b3", "feat/filters", "Remember filters in the link", "Leo Martin", "1 hour ago", 131, 27, 4,
            "Shared links open with the same filters applied.")
    ];

    private static BranchGraphCommit Commit(string id, string branch, string message, string author, string date, int added, int removed,
        int files, string? body, string? parents = null) => new(id, branch, message, parents, author)
    {
        Date = date,
        Additions = added,
        Deletions = removed,
        Files = files,
        Body = body
    };
}
