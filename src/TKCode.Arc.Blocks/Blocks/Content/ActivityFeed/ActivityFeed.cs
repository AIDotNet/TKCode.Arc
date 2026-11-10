using System.Collections.ObjectModel;
using System.Collections.Specialized;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Metadata;
using TKCode.Arc.DesignTokens;
using TKCode.Arc.Primitives;

namespace TKCode.Arc;

/// <summary>
/// What happened in a workspace, newest first and grouped by day: who did what, with an icon per kind of change,
/// filter chips by kind and a person picker, and new items that slide in at the top while the feed is live. Pause it
/// and new items wait behind a "new updates" pill instead of moving the list under the reader.
/// <code>
/// &lt;arc:ActivityFeed x:Name="Feed" Title="Workspace activity" /&gt;
/// </code>
/// <code language="csharp">
/// Feed.Push(new ActivityFeedItem("e42", DateTimeOffset.Now, "Rosa Lind", "commented on Launch plan", ActivityKind.Comment));
/// </code>
/// Rows are a <see cref="Timeline"/>: days stay pinned while their rows scroll, and rows with a detail expand in place.
/// A feed with no items when it first loads shows a sample.
/// </summary>
[TemplatePart(HostPart, typeof(Decorator))]
[PseudoClasses(LivePseudoClass)]
public class ActivityFeed : TemplatedControl
{
    public const string HostPart = "PART_Host";

    /// <summary>Set while new items go straight into the list.</summary>
    public const string LivePseudoClass = ":live";

    #region Properties

    /// <summary>The heading ("Activity").</summary>
    public static readonly StyledProperty<string> TitleProperty =
        AvaloniaProperty.Register<ActivityFeed, string>(nameof(Title), "Activity");

    /// <summary>The kind shown (two-way); null shows every kind.</summary>
    public static readonly StyledProperty<ActivityKind?> KindFilterProperty =
        AvaloniaProperty.Register<ActivityFeed, ActivityKind?>(nameof(KindFilter), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>The person shown (two-way); null shows everyone.</summary>
    public static readonly StyledProperty<string?> ActorFilterProperty =
        AvaloniaProperty.Register<ActivityFeed, string?>(nameof(ActorFilter), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>Whether pushed items go straight into the list (two-way, default true); false holds them behind the pill.</summary>
    public static readonly StyledProperty<bool> IsLiveProperty =
        AvaloniaProperty.Register<ActivityFeed, bool>(nameof(IsLive), true, defaultBindingMode: BindingMode.TwoWay);

    /// <summary>Whether a feed with no items when it first loads gets the sample (default true).</summary>
    public static readonly StyledProperty<bool> UsesSampleDataProperty =
        AvaloniaProperty.Register<ActivityFeed, bool>(nameof(UsesSampleData), true);

    /// <summary>Items pushed while paused, waiting behind the pill.</summary>
    public static readonly DirectProperty<ActivityFeed, int> PendingCountProperty =
        AvaloniaProperty.RegisterDirect<ActivityFeed, int>(nameof(PendingCount), o => o.PendingCount);

    /// <inheritdoc cref="TitleProperty"/>
    public string Title { get => GetValue(TitleProperty); set => SetValue(TitleProperty, value); }

    /// <inheritdoc cref="KindFilterProperty"/>
    public ActivityKind? KindFilter { get => GetValue(KindFilterProperty); set => SetValue(KindFilterProperty, value); }

    /// <inheritdoc cref="ActorFilterProperty"/>
    public string? ActorFilter { get => GetValue(ActorFilterProperty); set => SetValue(ActorFilterProperty, value); }

    /// <inheritdoc cref="IsLiveProperty"/>
    public bool IsLive { get => GetValue(IsLiveProperty); set => SetValue(IsLiveProperty, value); }

    /// <inheritdoc cref="UsesSampleDataProperty"/>
    public bool UsesSampleData { get => GetValue(UsesSampleDataProperty); set => SetValue(UsesSampleDataProperty, value); }

    /// <inheritdoc cref="PendingCountProperty"/>
    public int PendingCount { get => _pending.Count; }

    /// <summary>Everything that happened, in any order (the feed shows the newest first).</summary>
    [Content]
    public AvaloniaList<ActivityFeedItem> Items { get; } = new();

    #endregion

    internal static readonly StyledProperty<double> FeedMaxHeightProperty =
        AvaloniaProperty.Register<ActivityFeed, double>(nameof(FeedMaxHeight), 560);

    internal double FeedMaxHeight { get => GetValue(FeedMaxHeightProperty); set => SetValue(FeedMaxHeightProperty, value); }

    private const string Everyone = "Everyone";

    private static readonly (ActivityKind? Kind, string Label)[] s_kinds =
    [
        (null, "All"), (ActivityKind.Comment, "Comments"), (ActivityKind.Change, "Changes"),
        (ActivityKind.Deploy, "Deploys"), (ActivityKind.Member, "People"),
    ];

    private readonly List<ActivityFeedItem> _pending = new();
    private readonly ObservableCollection<TimelineEvent> _events = new();
    private readonly Grid _root;
    private readonly TextBlock _titleText;
    private readonly ActivityFeedLiveDot _dot;
    private readonly TextBlock _liveText;
    private readonly SupportKitChip _liveChip;
    private readonly List<(SupportKitChip Chip, ActivityKind? Kind)> _chips = new();
    private readonly Select _actor;
    private readonly Timeline _timeline;
    private readonly PresenceHost _pillHost;
    private readonly Button _pill;
    private readonly TextBlock _empty;
    private readonly SupportKitAnnouncer _announcer = new();
    private Decorator? _host;
    private bool _loaded;
    private bool _syncingActor;

    /// <summary>Creates the feed.</summary>
    public ActivityFeed()
    {
        BlockFocusModality.Watch();
        _titleText = SupportKit.Heading(Title, ArcDesignTokenKind.TypeLG);
        _dot = new ActivityFeedLiveDot { IsLive = true };
        _liveText = SupportKit.Text("Live", ArcDesignTokenKind.Foreground, ArcDesignTokenKind.TypeXS, FontWeight.Medium);
        _liveChip = new SupportKitChip
        {
            IsSelfToggling = false,
            Content = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4, Children = { _dot, _liveText } },
            Padding = new Thickness(8, 0, 12, 0),
        };
        _liveChip.Click += (_, _) => SetCurrentValue(IsLiveProperty, !IsLive);
        var header = SupportKit.Grid("*,Auto", columnSpacing: 12);
        header.Children.Add(_titleText.At(0));
        header.Children.Add(_liveChip.At(1));

        var chips = new WrapPanel { ItemSpacing = 6, LineSpacing = 6, VerticalAlignment = VerticalAlignment.Center };
        foreach (var (kind, label) in s_kinds)
        {
            var chip = new SupportKitChip { Content = label, Item = kind };
            chip.Click += (_, _) => SetCurrentValue(KindFilterProperty, kind);
            _chips.Add((chip, kind));
            chips.Children.Add(chip);
        }
        AutomationProperties.SetName(chips, "Kind of activity");
        _actor = new Select { Size = ControlSize.Small, MinWidth = 160, VerticalAlignment = VerticalAlignment.Center };
        AutomationProperties.SetName(_actor, "Person");
        _actor.SelectionChanged += (_, _) =>
        {
            if (!_syncingActor)
            {
                var actor = _actor.SelectedItem as string;
                SetCurrentValue(ActorFilterProperty, actor == Everyone ? null : actor);
            }
        };
        var filters = new WrapPanel { ItemSpacing = 12, LineSpacing = 8, Children = { chips, _actor } };

        _timeline = new Timeline { ItemsSource = _events, Label = Title };
        _empty = SupportKit.Text("Nothing matches these filters.", ArcDesignTokenKind.TextMuted, wrap: true);
        _empty.HorizontalAlignment = HorizontalAlignment.Center;
        _empty.Margin = new Thickness(0, 24);
        _empty.IsVisible = false;

        _pill = SupportKit.Button("New updates", ButtonVariant.Primary, ArcIcons.ArrowUp);
        _pill.Click += (_, _) => ShowPending();
        _pillHost = new PresenceHost
        {
            Mode = PresenceMode.Drop,
            Offset = 8,
            Child = _pill,
            IsPresent = false,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Top,
            Margin = new Thickness(0, 4, 0, 0),
            ZIndex = 1,
        };
        var body = new Panel { Children = { _timeline, _empty, _pillHost } };

        _root = SupportKit.Grid("*", "Auto,Auto,*", rowSpacing: 14);
        _root.Children.Add(header.At(0, 0));
        _root.Children.Add(filters.At(0, 1));
        _root.Children.Add(body.At(0, 2));
        _root.Children.Add(_announcer.At(0, 2));

        Items.CollectionChanged += OnItemsChanged;
        UpdateLive();
        Rebuild();
    }

    #region Internals for tests

    internal Timeline Timeline => _timeline;

    internal IReadOnlyList<TimelineEvent> ShownEvents => _events;

    internal bool IsPillShown => _pillHost.IsPresent;

    internal ActivityFeedLiveDot LiveDot => _dot;

    internal Select ActorPicker => _actor;

    internal SupportKitChip ChipFor(ActivityKind? kind) => _chips.First(c => c.Kind == kind).Chip;

    internal string? LastAnnouncement => _announcer.Announcement;

    internal Func<DateTimeOffset> Clock { get; set; } = static () => DateTimeOffset.Now;

    #endregion

    /// <summary>
    /// Adds an item as it happens: while live it slides in at the top (if it passes the filters); while paused it waits
    /// behind the pill until <see cref="ShowPending"/>.
    /// </summary>
    public void Push(ActivityFeedItem item)
    {
        if (IsLive)
        {
            Items.Add(item);
            return;
        }
        _pending.Add(item);
        RaisePropertyChanged(PendingCountProperty, _pending.Count - 1, _pending.Count);
        UpdatePill();
        _announcer.Announce(SupportKit.Count(_pending.Count, "new update", "new updates"));
    }

    /// <summary>Moves the waiting items into the list and scrolls to the top.</summary>
    public void ShowPending()
    {
        if (_pending.Count == 0)
        {
            return;
        }
        var count = _pending.Count;
        var waiting = _pending.ToList();
        _pending.Clear();
        RaisePropertyChanged(PendingCountProperty, count, 0);
        Items.AddRange(waiting);
        UpdatePill();
        _timeline.ScrollToTop();
    }

    #region Template and properties

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _host = e.NameScope.Find<Decorator>(HostPart);
        SupportKit.Mount(_host, _root);
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        if (!_loaded)
        {
            _loaded = true;
            if (UsesSampleData && Items.Count == 0)
            {
                Items.AddRange(ActivityFeedSamples.Create(Clock()));
            }
        }
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        var property = change.Property;
        if (property == TitleProperty)
        {
            _titleText.Text = Title;
            _timeline.Label = Title;
        }
        else if (property == KindFilterProperty || property == ActorFilterProperty)
        {
            Rebuild();
        }
        else if (property == IsLiveProperty)
        {
            UpdateLive();
            if (IsLive)
            {
                ShowPending();
            }
            _announcer.Announce(IsLive ? "Live updates on." : "Live updates paused. New items wait at the top.");
        }
        else if (property == FeedMaxHeightProperty)
        {
            _timeline.MaxHeight = FeedMaxHeight;
        }
    }

    private void UpdateLive()
    {
        PseudoClasses.Set(LivePseudoClass, IsLive);
        _dot.IsLive = IsLive;
        _liveText.Text = IsLive ? "Live" : "Paused";
        _liveChip.IsChecked = false;
        AutomationProperties.SetName(_liveChip, IsLive ? "Live updates on. Pause" : "Live updates paused. Resume");
    }

    private void UpdatePill()
    {
        _pillHost.IsPresent = _pending.Count > 0;
        var text = SupportKit.Count(_pending.Count, "new update", "new updates");
        if (_pill.Content is StackPanel { Children: [_, TextBlock label] })
        {
            label.Text = text;
        }
        AutomationProperties.SetName(_pill, $"Show {text}");
    }

    #endregion

    #region Items

    private bool Passes(ActivityFeedItem item) =>
        (KindFilter is not { } kind || item.Kind == kind) && (ActorFilter is not { } actor || item.Actor == actor);

    private void OnItemsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.Action == NotifyCollectionChangedAction.Add && e.NewItems is not null)
        {
            // New items go in as they are, so the timeline slides them in at the top.
            foreach (ActivityFeedItem item in e.NewItems)
            {
                if (Passes(item))
                {
                    _events.Add(ToEvent(item));
                }
            }
            UpdateActors();
            _empty.IsVisible = _events.Count == 0 && Items.Count > 0;
            return;
        }
        Rebuild();
    }

    private void Rebuild()
    {
        _timeline.Now = Clock();
        _events.Clear();
        foreach (var item in Items)
        {
            if (Passes(item))
            {
                _events.Add(ToEvent(item));
            }
        }
        foreach (var (chip, kind) in _chips)
        {
            chip.IsChecked = kind == KindFilter;
        }
        UpdateActors();
        _empty.IsVisible = _events.Count == 0 && Items.Count > 0;
    }

    private void UpdateActors()
    {
        var actors = Items.Select(i => i.Actor).OfType<string>().Distinct().Order(StringComparer.CurrentCulture).Prepend(Everyone).ToList();
        _syncingActor = true;
        try
        {
            if (_actor.ItemsSource is not List<string> current || !current.SequenceEqual(actors))
            {
                _actor.ItemsSource = actors;
            }
            _actor.SelectedItem = ActorFilter is { } actor && actors.Contains(actor) ? actor : Everyone;
        }
        finally
        {
            _syncingActor = false;
        }
    }

    internal static TimelineEvent ToEvent(ActivityFeedItem item) => new(item.Id, item.At, item.Action, item.Actor)
    {
        Meta = item.Target,
        Avatar = item.Avatar,
        Detail = item.Detail,
        Tone = item.Tone,
        Icon = item.Avatar is null ? IconOf(item.Kind) : null,
    };

    internal static Geometry IconOf(ActivityKind kind) => kind switch
    {
        ActivityKind.Comment => ArcIcons.MessageSquare,
        ActivityKind.Change => ArcIcons.PenLine,
        ActivityKind.Deploy => SupportKit.Rocket,
        ActivityKind.Member => SupportKit.UserPlus,
        _ => ArcIcons.Activity,
    };

    #endregion
}

/// <summary>The sample workspace of an <see cref="ActivityFeed"/> that has no items when it first loads.</summary>
internal static class ActivityFeedSamples
{
    public static IEnumerable<ActivityFeedItem> Create(DateTimeOffset now) =>
    [
        new("a1", now.AddMinutes(-3), "Rosa Lind", "commented on Launch checklist", ActivityKind.Comment)
        {
            Target = "Spring release",
            Detail = "Can we move the store screenshots before the copy review? They take longer to approve.",
        },
        new("a2", now.AddMinutes(-18), null, "Deploy to production succeeded", ActivityKind.Deploy) { Target = "web · build 1482", Tone = TimelineTone.Success },
        new("a3", now.AddMinutes(-40), "Dev Patel", "moved Pricing page to Review", ActivityKind.Change) { Target = "Marketing site" },
        new("a4", now.AddHours(-2), "Yuki Sato", "joined the workspace as Editor", ActivityKind.Member),
        new("a5", now.AddHours(-3), "Dev Patel", "closed 4 issues in Billing", ActivityKind.Change) { Target = "Sprint 31" },
        new("a6", now.AddDays(-1).AddHours(-1), null, "Deploy to staging failed: tests did not pass", ActivityKind.Deploy)
        {
            Target = "api · build 1477",
            Tone = TimelineTone.Danger,
            Detail = "3 of 412 tests failed in invoices.spec. The previous build stays live.",
        },
        new("a7", now.AddDays(-1).AddHours(-4), "Rosa Lind", "renamed Q3 roadmap to Second half roadmap", ActivityKind.Change),
        new("a8", now.AddDays(-2), "Amir Haddad", "replied to Onboarding emails", ActivityKind.Comment)
        {
            Target = "Growth",
            Detail = "Agreed. Let us drop the third email and test a shorter subject line.",
        },
        new("a9", now.AddDays(-3), "Yuki Sato", "invited Lena Ortiz", ActivityKind.Member),
    ];
}
