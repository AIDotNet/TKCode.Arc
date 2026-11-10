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
using Avalonia.Metadata;
using Avalonia.Threading;
using TKCode.Arc.Primitives;

namespace TKCode.Arc;

/// <summary>
/// A git-style history: one row per commit, each branch in its own colored lane, curves where a branch splits off and
/// where it merges back, merge commits drawn as rings, and pills for branch heads and tags. Its story draws the history
/// commit by commit (the edges draw in, the dot pops, the branch label moves up) with a caption that says what each
/// commit did. Play, pause, step, restart and a 0.5× / 1× / 2× speed control drive it; <see cref="StartsFinished"/> or
/// reduced motion opens it on the whole history. Selecting a commit (click, or the arrow keys on the focused graph)
/// highlights everything it descends from and dims the rest.
/// </summary>
[TemplatePart(StagePart, typeof(BranchGraphStage))]
[TemplatePart(ExplainerChapterKit.StatusPart, typeof(ExplainerStatus))]
[TemplatePart(ExplainerChapterKit.CaptionPart, typeof(ArcContentSwap))]
[TemplatePart(ExplainerChapterKit.SpeedPart, typeof(SegmentedControl))]
public class BranchGraph : Explainer
{
    /// <summary>The drawn graph.</summary>
    public const string StagePart = "PART_Stage";

    /// <summary>The history from a view model, oldest first; when set it replaces <see cref="Commits"/>.</summary>
    public static readonly StyledProperty<IEnumerable<BranchGraphCommit>?> ItemsSourceProperty =
        AvaloniaProperty.Register<BranchGraph, IEnumerable<BranchGraphCommit>?>(nameof(ItemsSource));

    /// <summary>Puts the newest commit on top, as <c>git log</c> does (default false: oldest on top, so the story grows downward).</summary>
    public static readonly StyledProperty<bool> IsNewestFirstProperty =
        AvaloniaProperty.Register<BranchGraph, bool>(nameof(IsNewestFirst));

    /// <summary>Opens on the whole history instead of playing it (Play still replays the story). Use it for a plain graph.</summary>
    public static readonly StyledProperty<bool> StartsFinishedProperty =
        AvaloniaProperty.Register<BranchGraph, bool>(nameof(StartsFinished));

    /// <summary>The commit whose history is highlighted; null for none. Clicks and the arrow keys on the graph set it.</summary>
    public static readonly StyledProperty<BranchGraphCommit?> SelectedCommitProperty =
        AvaloniaProperty.Register<BranchGraph, BranchGraphCommit?>(nameof(SelectedCommit), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>Every word the card shows or announces.</summary>
    public static readonly StyledProperty<BranchGraphLabels> LabelsProperty =
        AvaloniaProperty.Register<BranchGraph, BranchGraphLabels>(nameof(Labels), new BranchGraphLabels());

    /// <summary>How many commits the story has drawn so far.</summary>
    public static readonly DirectProperty<BranchGraph, int> RevealedCountProperty =
        AvaloniaProperty.RegisterDirect<BranchGraph, int>(nameof(RevealedCount), o => o.RevealedCount);

    /// <summary>Raised as each commit is drawn, with the commit, its index and what it did.</summary>
    public static readonly RoutedEvent<BranchGraphCommitEventArgs> CommitDrawnEvent =
        RoutedEvent.Register<BranchGraph, BranchGraphCommitEventArgs>(nameof(CommitDrawn), RoutingStrategies.Bubble);

    internal static readonly StyledProperty<Thickness> BodyPaddingProperty =
        AvaloniaProperty.Register<BranchGraph, Thickness>(nameof(BodyPadding), new Thickness(24, 24, 24, 0));

    internal static readonly StyledProperty<Thickness> FooterPaddingProperty =
        AvaloniaProperty.Register<BranchGraph, Thickness>(nameof(FooterPadding), new Thickness(24, 16, 24, 24));

    /// <summary>Seconds per commit at speed 1.</summary>
    internal const double CommitSeconds = 1.6;

    /// <summary>When, within a commit's beat, its edges finish drawing and its dot appears.</summary>
    internal const double EdgeSeconds = 0.5;

    private static readonly IReadOnlyList<BranchGraphCommit> s_sample = BranchGraphSamples.CreateCommits();
    private readonly ExplainerChapterKit _kit;
    private INotifyCollectionChanged? _observedSource;
    private BranchGraphStage? _stage;
    private IReadOnlyList<BranchGraphCommit> _history = Array.Empty<BranchGraphCommit>();
    private BranchGraphPlan _plan = BranchGraphPlan.Empty;
    private readonly List<double> _revealedAt = new();
    private int _revealed;
    private string? _caption;
    private bool _rebuildPending;
    private bool _pauseCheckPending;

    static BranchGraph()
    {
        TitleProperty.OverrideDefaultValue<BranchGraph>("Branch history");
        AutomationProperties.ControlTypeOverrideProperty.OverrideDefaultValue<BranchGraph>(AutomationControlType.Group);
    }

    /// <summary>Creates a graph that plays the sample history until it is given commits.</summary>
    public BranchGraph()
    {
        _kit = new ExplainerChapterKit(this);
        Commits.CollectionChanged += (_, _) =>
        {
            if (ItemsSource is null)
            {
                ScheduleRebuild();
            }
        };
        AutomationProperties.SetName(this, Title);
    }

    /// <summary>The history, oldest first (the XAML content). While it is empty and <see cref="ItemsSource"/> is null, an invented sample history plays.</summary>
    [Content]
    public AvaloniaList<BranchGraphCommit> Commits { get; } = new();

    /// <summary>The history from a view model, oldest first; when set it replaces <see cref="Commits"/>.</summary>
    public IEnumerable<BranchGraphCommit>? ItemsSource { get => GetValue(ItemsSourceProperty); set => SetValue(ItemsSourceProperty, value); }

    /// <summary>Puts the newest commit on top, as <c>git log</c> does (default false: oldest on top, so the story grows downward).</summary>
    public bool IsNewestFirst { get => GetValue(IsNewestFirstProperty); set => SetValue(IsNewestFirstProperty, value); }

    /// <summary>Opens on the whole history instead of playing it (Play still replays the story). Use it for a plain graph.</summary>
    public bool StartsFinished { get => GetValue(StartsFinishedProperty); set => SetValue(StartsFinishedProperty, value); }

    /// <summary>The commit whose history is highlighted; null for none. Clicks and the arrow keys on the graph set it.</summary>
    public BranchGraphCommit? SelectedCommit { get => GetValue(SelectedCommitProperty); set => SetValue(SelectedCommitProperty, value); }

    /// <summary>Every word the card shows or announces.</summary>
    public BranchGraphLabels Labels { get => GetValue(LabelsProperty); set => SetValue(LabelsProperty, value); }

    /// <summary>How many commits the story has drawn so far.</summary>
    public int RevealedCount { get => _revealed; private set => SetAndRaise(RevealedCountProperty, ref _revealed, value); }

    /// <summary>Raised as each commit is drawn, with the commit, its index and what it did.</summary>
    public event EventHandler<BranchGraphCommitEventArgs>? CommitDrawn
    {
        add => AddHandler(CommitDrawnEvent, value);
        remove => RemoveHandler(CommitDrawnEvent, value);
    }

    internal Thickness BodyPadding { get => GetValue(BodyPaddingProperty); set => SetValue(BodyPaddingProperty, value); }

    internal Thickness FooterPadding { get => GetValue(FooterPaddingProperty); set => SetValue(FooterPaddingProperty, value); }

    internal BranchGraphPlan Plan => _plan;

    internal double StoryTime => Now;

    internal bool Animates => IsTravelEnabled;

    /// <summary>When each drawn commit started drawing (story time).</summary>
    internal IReadOnlyList<double> RevealedAt => _revealedAt;

    internal string? CaptionText => _kit.CaptionText;

    internal string? StatusText => _kit.StatusText;

    internal BranchGraphStage? Stage => _stage;

    private protected override double StepSpeed => 2 * Math.Max(1, Speed);

    /// <summary>The words for a commit: message, id, branch, author, and its tag.</summary>
    internal string Describe(BranchGraphNode node)
    {
        var text = Labels.Describe(node.Commit.Message ?? string.Empty, node.Id, node.Branch, node.Commit.Author ?? string.Empty);
        if (!string.IsNullOrEmpty(node.Commit.Tag))
        {
            text += ", " + node.Commit.Tag;
        }
        return text;
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _kit.Apply(e.NameScope);
        _stage = e.NameScope.Find<BranchGraphStage>(StagePart);
        _stage?.Attach(this);
        _kit.SetSpeedLabel(Labels.Speed);
        UpdateStatus(force: true);
        _kit.SetCaption(CurrentCaption(), animate: false);
        UpdateSizing(Bounds.Width);
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        if (StartsFinished && !IsFinished && _revealed == 0)
        {
            SetCurrentValue(AutoPlayProperty, false);
            JumpToEnd();
        }
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ItemsSourceProperty)
        {
            ObserveSource(IsAttached ? ItemsSource : null);
            ScheduleRebuild();
        }
        else if (change.Property == IsNewestFirstProperty)
        {
            _stage?.Relayout();
        }
        else if (change.Property == SelectedCommitProperty)
        {
            _stage?.OnSelectionChanged();
        }
        else if (change.Property == StartsFinishedProperty && StartsFinished && IsAttached && _revealed == 0)
        {
            SetCurrentValue(AutoPlayProperty, false);
            JumpToEnd();
        }
        else if (change.Property == LabelsProperty)
        {
            _kit.SetSpeedLabel(Labels.Speed);
            UpdateStatus(force: true);
            _kit.SetCaption(CurrentCaption(), animate: false);
            _stage?.Changed();
        }
        else if (change.Property == TitleProperty)
        {
            AutomationProperties.SetName(this, Title);
        }
        else if (change.Property == SpeedProperty)
        {
            _kit.UpdateSpeed();
        }
        else if (change.Property == IsSteppingProperty || change.Property == IsFinishedProperty)
        {
            UpdateStatus();
        }
        else if (change.Property == BoundsProperty)
        {
            UpdateSizing(change.GetNewValue<Rect>().Width);
        }
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
            var finished = IsFinished || StartsFinished;
            Rebuild();
            if (finished && !IsPlaying && _revealed == 0 && (StartsFinished || IsAttached))
            {
                JumpToEnd();
            }
        }, DispatcherPriority.Background);
    }

    private protected override void BuildStory(ExplainerScript script)
    {
        if (ItemsSource is { } source)
        {
            _history = source.Where(c => c is not null).ToArray();
        }
        else if (Commits.Count > 0)
        {
            _history = Commits.ToArray();
        }
        else
        {
            _history = s_sample;
        }
        _plan = BranchGraphPlan.Build(_history);
        _stage?.SetPlan(_plan);
        var t = 0.4;
        for (var i = 0; i < _plan.Nodes.Length; i++)
        {
            var index = i;
            script.At(t, () => Reveal(index));
            script.At(t + CommitSeconds - 0.3, AnnounceCaption, isStop: true);
            t += CommitSeconds;
        }
        script.At(t, Finish, isStop: true);
        script.Length = t;
    }

    private void Reveal(int index)
    {
        var node = _plan.Nodes[index];
        while (_revealedAt.Count <= index)
        {
            _revealedAt.Add(Now);
        }
        _revealedAt[index] = Now;
        RevealedCount = index + 1;
        _caption = CaptionOf(node);
        _kit.SetCaption(_caption, Animates);
        _stage?.OnReveal(index);
        UpdateStatus();
        if (Animates)
        {
            RequestFrames(CommitSeconds);
        }
        RaiseEvent(new BranchGraphCommitEventArgs(CommitDrawnEvent, this, node.Commit, index, node.Kind));
    }

    private string CaptionOf(BranchGraphNode node)
    {
        var labels = Labels;
        var message = node.Commit.Message ?? node.Id;
        var text = node.Kind switch
        {
            BranchGraphCommitKind.Branch => labels.BranchCaption(node.Branch, node.Other ?? string.Empty, _plan.Nodes[node.Parents[0]].Id),
            BranchGraphCommitKind.Merge => labels.MergeCaption(node.Other ?? string.Empty, node.Branch),
            _ when node.Index == 0 => labels.FirstCaption(node.Branch),
            _ => labels.CommitCaption(message, node.Branch)
        };
        if (!string.IsNullOrEmpty(node.Commit.Tag))
        {
            text += labels.TagCaption(node.Commit.Tag);
        }
        return text;
    }

    private string DoneCaption() => Labels.DoneCaption(_plan.Nodes.Length, _plan.Branches.Length, _plan.MergeCount);

    private void Finish()
    {
        _caption = DoneCaption();
        _kit.SetCaption(_caption, Animates);
        UpdateStatus();
        _stage?.Changed();
    }

    private void AnnounceCaption() => AnnounceBeat(_caption);

    private string CurrentCaption() => _caption ?? Labels.ReadyCaption(_plan.Nodes.Length);

    private protected override void ResetScene()
    {
        var had = _revealed > 0;
        _revealedAt.Clear();
        RevealedCount = 0;
        _caption = null;
        _kit.SetCaption(CurrentCaption(), Animates && had);
        _stage?.Changed();
        UpdateStatus();
        if (SelectedCommit is { } selected && !_history.Contains(selected))
        {
            SetCurrentValue(SelectedCommitProperty, null);
        }
    }

    private protected override void OnTimeChanged()
    {
        _stage?.Tick();
        UpdateStatus();
    }

    private protected override void OnFinished()
    {
        UpdateStatus();
        Announce(DoneCaption());
    }

    private protected override void OnPlaybackChanged()
    {
        UpdateStatus();
        if (!IsPlaying && !_pauseCheckPending)
        {
            _pauseCheckPending = true;
            Dispatcher.UIThread.Post(() =>
            {
                _pauseCheckPending = false;
                if (!IsPlaying && !IsStepping && !IsFinished && _revealed > 0 && IsReaderDriven)
                {
                    Announce(_caption);
                }
            }, DispatcherPriority.Background);
        }
    }

    private void UpdateStatus(bool force = false)
    {
        var phase = _kit.PhaseOf(_revealed > 0);
        var labels = Labels;
        var text = phase switch
        {
            ExplainerChapterPhase.Playing => labels.Drawing,
            ExplainerChapterPhase.Paused => labels.Paused,
            ExplainerChapterPhase.Done => labels.Done,
            _ => labels.Ready
        };
        _kit.SetStatus(phase, text, force);
    }

    private void UpdateSizing(double width)
    {
        if (width <= 0)
        {
            return;
        }
        var padding = ExplainerChapterKit.CardPadding(width);
        var body = new Thickness(padding, padding, padding, AreControlsVisible ? 0 : padding);
        if (body != BodyPadding)
        {
            SetCurrentValue(BodyPaddingProperty, body);
        }
        var footer = new Thickness(padding, 16, padding, padding);
        if (footer != FooterPadding)
        {
            SetCurrentValue(FooterPaddingProperty, footer);
        }
        _kit.SetSpeedLabelVisible(width >= 420);
    }
}

/// <summary>The invented sample history a <see cref="BranchGraph"/> plays when it is given no commits.</summary>
public static class BranchGraphSamples
{
    /// <summary>
    /// A fresh copy of the sample history: nine commits of a small garden app on main, a feature branch and a hotfix
    /// branch, with two merges and a release tag. The names and messages are invented.
    /// </summary>
    public static IReadOnlyList<BranchGraphCommit> CreateCommits() =>
    [
        new("a1f3c0", "main", "Initial commit", author: "Mira"),
        new("b7d2e4", "main", "Add plant list", author: "Mira"),
        new("c4e9a1", "feature/search", "Add search box", author: "Teo"),
        new("d2a8f7", "main", "Fix watering times", author: "Mira"),
        new("e6b1c3", "feature/search", "Filter by season", author: "Teo"),
        new("f9c5d2", "hotfix/login", "Fix login redirect", parents: "d2a8f7", author: "Ines"),
        new("0a7e4b", "main", "Merge hotfix/login", parents: "d2a8f7, f9c5d2", author: "Ines"),
        new("1b3f6c", "feature/search", "Show search results", author: "Teo"),
        new("2c8d0e", "main", "Merge feature/search", parents: "0a7e4b, 1b3f6c", author: "Mira", tag: "v1.2.0")
    ];
}
