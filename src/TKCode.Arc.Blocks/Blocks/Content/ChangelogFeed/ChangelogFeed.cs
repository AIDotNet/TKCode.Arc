// Ported from Arc's free changelog-feed block (MIT, Copyright (c) 2026 Elia Kuratli): changelog-feed.tsx.
using System.Globalization;
using TKCode.Arc.DesignTokens;
using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>
/// Release notes you can filter, open in place and scroll through month by month: chips by kind of change, a month bar that follows
/// the scroll and jumps on a press, rows that open into details and media, and a pill that turns into an email field and then a
/// confirmation. Zero configuration shows original sample notes of an invented product.
/// </summary>
/// <remarks>Ported from Arc's free changelog-feed block (MIT, Copyright (c) 2026 Elia Kuratli).</remarks>
[TemplatePart(ScrollerPart, typeof(ScrollViewer))]
[TemplatePart(FlowPart, typeof(ChangelogFlowPanel))]
[TemplatePart(ChipsPart, typeof(Panel))]
[TemplatePart(MonthNavPart, typeof(GlidingPillBar))]
[TemplatePart(SubscribePart, typeof(ChangelogSubscribe))]
[TemplatePart(AnnouncerPart, typeof(SiteAnnouncer))]
[PseudoClasses(CompactPseudoClass, TinyPseudoClass)]
public class ChangelogFeed : TemplatedControl
{
    public const string ScrollerPart = "PART_Scroller";
    public const string FlowPart = "PART_Flow";
    public const string ChipsPart = "PART_Chips";
    public const string MonthNavPart = "PART_MonthNav";
    public const string SubscribePart = "PART_Subscribe";
    public const string AnnouncerPart = "PART_Announcer";

    /// <summary>Set at 640 wide or less: a stacked header, and rows with date and version above the text.</summary>
    public const string CompactPseudoClass = ":compact";

    /// <summary>Set at 480 wide or less: no counts in the toolbar and month bar, and no year.</summary>
    public const string TinyPseudoClass = ":tiny";

    /// <summary>The release notes, newest first. Null shows the samples.</summary>
    public static readonly StyledProperty<IReadOnlyList<ChangelogEntry>?> EntriesProperty =
        AvaloniaProperty.Register<ChangelogFeed, IReadOnlyList<ChangelogEntry>?>(nameof(Entries));

    /// <summary>The months of the month bar, newest first. Null shows the samples' months.</summary>
    public static readonly StyledProperty<IReadOnlyList<ChangelogMonth>?> MonthsProperty =
        AvaloniaProperty.Register<ChangelogFeed, IReadOnlyList<ChangelogMonth>?>(nameof(Months));

    /// <summary>The heading ("Changelog").</summary>
    public static readonly StyledProperty<string?> TitleProperty =
        AvaloniaProperty.Register<ChangelogFeed, string?>(nameof(Title), ChangelogFeedSamples.Title);

    /// <summary>One line under the heading, such as the latest release. Empty hides it.</summary>
    public static readonly StyledProperty<string?> SubtitleProperty =
        AvaloniaProperty.Register<ChangelogFeed, string?>(nameof(Subtitle), ChangelogFeedSamples.Subtitle);

    /// <summary>The closing line at the end of the list.</summary>
    public static readonly StyledProperty<string?> EndNoteProperty =
        AvaloniaProperty.Register<ChangelogFeed, string?>(nameof(EndNote), ChangelogFeedSamples.EndNote);

    /// <summary>The ids of the entries open at first. Null opens the newest entry.</summary>
    public static readonly StyledProperty<IReadOnlyList<string>?> DefaultOpenProperty =
        AvaloniaProperty.Register<ChangelogFeed, IReadOnlyList<string>?>(nameof(DefaultOpen));

    /// <summary>The label of the New chip and row ("New").</summary>
    public static readonly StyledProperty<string> NewLabelProperty =
        AvaloniaProperty.Register<ChangelogFeed, string>(nameof(NewLabel), "New");

    /// <summary>The label of the Improved chip and row ("Improved").</summary>
    public static readonly StyledProperty<string> ImprovedLabelProperty =
        AvaloniaProperty.Register<ChangelogFeed, string>(nameof(ImprovedLabel), "Improved");

    /// <summary>The label of the Fixed chip and row ("Fixed").</summary>
    public static readonly StyledProperty<string> FixedLabelProperty =
        AvaloniaProperty.Register<ChangelogFeed, string>(nameof(FixedLabel), "Fixed");

    /// <summary>The button that clears the filters ("Show all").</summary>
    public static readonly StyledProperty<string> ShowAllLabelProperty =
        AvaloniaProperty.Register<ChangelogFeed, string>(nameof(ShowAllLabel), "Show all");

    /// <summary>The word after one ("update").</summary>
    public static readonly StyledProperty<string> UpdateWordProperty =
        AvaloniaProperty.Register<ChangelogFeed, string>(nameof(UpdateWord), "update");

    /// <summary>The word after any other number ("updates").</summary>
    public static readonly StyledProperty<string> UpdatesWordProperty =
        AvaloniaProperty.Register<ChangelogFeed, string>(nameof(UpdatesWord), "updates");

    /// <summary>What follows the number while filters are on; {0} is the total ("of {0} updates").</summary>
    public static readonly StyledProperty<string> OfUpdatesFormatProperty =
        AvaloniaProperty.Register<ChangelogFeed, string>(nameof(OfUpdatesFormat), "of {0} updates");

    /// <summary>The month bar's text when nothing matches ("No updates").</summary>
    public static readonly StyledProperty<string> NoUpdatesLabelProperty =
        AvaloniaProperty.Register<ChangelogFeed, string>(nameof(NoUpdatesLabel), "No updates");

    /// <summary>A month button's screen reader name; {0} is the month ("Jump to {0}").</summary>
    public static readonly StyledProperty<string> JumpFormatProperty =
        AvaloniaProperty.Register<ChangelogFeed, string>(nameof(JumpFormat), "Jump to {0}");

    /// <summary>The pill's text ("Subscribe").</summary>
    public static readonly StyledProperty<string> SubscribeLabelProperty =
        AvaloniaProperty.Register<ChangelogFeed, string>(nameof(SubscribeLabel), "Subscribe");

    /// <summary>The placeholder of the email field ("you@example.com").</summary>
    public static readonly StyledProperty<string> EmailPlaceholderProperty =
        AvaloniaProperty.Register<ChangelogFeed, string>(nameof(EmailPlaceholder), "you@example.com");

    /// <summary>Where the subscribe control stands (read only).</summary>
    public static readonly DirectProperty<ChangelogFeed, ChangelogSubscribeState> SubscribeStateProperty =
        AvaloniaProperty.RegisterDirect<ChangelogFeed, ChangelogSubscribeState>(nameof(SubscribeState), o => o.SubscribeState);

    /// <summary>A valid address was submitted. Post it to your mailing list here.</summary>
    public static readonly RoutedEvent<ChangelogSubscribeEventArgs> SubscribedEvent =
        RoutedEvent.Register<ChangelogFeed, ChangelogSubscribeEventArgs>(nameof(Subscribed), RoutingStrategies.Bubble);

    /// <summary>The confirmation's Undo was pressed.</summary>
    public static readonly RoutedEvent<ChangelogSubscribeEventArgs> SubscriptionUndoneEvent =
        RoutedEvent.Register<ChangelogFeed, ChangelogSubscribeEventArgs>(nameof(SubscriptionUndone), RoutingStrategies.Bubble);

    private ChangelogSubscribeState _subscribeState;

    /// <inheritdoc cref="EntriesProperty"/>
    public IReadOnlyList<ChangelogEntry>? Entries { get => GetValue(EntriesProperty); set => SetValue(EntriesProperty, value); }

    /// <inheritdoc cref="MonthsProperty"/>
    public IReadOnlyList<ChangelogMonth>? Months { get => GetValue(MonthsProperty); set => SetValue(MonthsProperty, value); }

    /// <inheritdoc cref="TitleProperty"/>
    public string? Title { get => GetValue(TitleProperty); set => SetValue(TitleProperty, value); }

    /// <inheritdoc cref="SubtitleProperty"/>
    public string? Subtitle { get => GetValue(SubtitleProperty); set => SetValue(SubtitleProperty, value); }

    /// <inheritdoc cref="EndNoteProperty"/>
    public string? EndNote { get => GetValue(EndNoteProperty); set => SetValue(EndNoteProperty, value); }

    /// <inheritdoc cref="DefaultOpenProperty"/>
    public IReadOnlyList<string>? DefaultOpen { get => GetValue(DefaultOpenProperty); set => SetValue(DefaultOpenProperty, value); }

    /// <inheritdoc cref="NewLabelProperty"/>
    public string NewLabel { get => GetValue(NewLabelProperty); set => SetValue(NewLabelProperty, value); }

    /// <inheritdoc cref="ImprovedLabelProperty"/>
    public string ImprovedLabel { get => GetValue(ImprovedLabelProperty); set => SetValue(ImprovedLabelProperty, value); }

    /// <inheritdoc cref="FixedLabelProperty"/>
    public string FixedLabel { get => GetValue(FixedLabelProperty); set => SetValue(FixedLabelProperty, value); }

    /// <inheritdoc cref="ShowAllLabelProperty"/>
    public string ShowAllLabel { get => GetValue(ShowAllLabelProperty); set => SetValue(ShowAllLabelProperty, value); }

    /// <inheritdoc cref="UpdateWordProperty"/>
    public string UpdateWord { get => GetValue(UpdateWordProperty); set => SetValue(UpdateWordProperty, value); }

    /// <inheritdoc cref="UpdatesWordProperty"/>
    public string UpdatesWord { get => GetValue(UpdatesWordProperty); set => SetValue(UpdatesWordProperty, value); }

    /// <inheritdoc cref="OfUpdatesFormatProperty"/>
    public string OfUpdatesFormat { get => GetValue(OfUpdatesFormatProperty); set => SetValue(OfUpdatesFormatProperty, value); }

    /// <inheritdoc cref="NoUpdatesLabelProperty"/>
    public string NoUpdatesLabel { get => GetValue(NoUpdatesLabelProperty); set => SetValue(NoUpdatesLabelProperty, value); }

    /// <inheritdoc cref="JumpFormatProperty"/>
    public string JumpFormat { get => GetValue(JumpFormatProperty); set => SetValue(JumpFormatProperty, value); }

    /// <inheritdoc cref="SubscribeLabelProperty"/>
    public string SubscribeLabel { get => GetValue(SubscribeLabelProperty); set => SetValue(SubscribeLabelProperty, value); }

    /// <inheritdoc cref="EmailPlaceholderProperty"/>
    public string EmailPlaceholder { get => GetValue(EmailPlaceholderProperty); set => SetValue(EmailPlaceholderProperty, value); }

    /// <inheritdoc cref="SubscribeStateProperty"/>
    public ChangelogSubscribeState SubscribeState { get => _subscribeState; private set => SetAndRaise(SubscribeStateProperty, ref _subscribeState, value); }

    /// <inheritdoc cref="SubscribedEvent"/>
    public event EventHandler<ChangelogSubscribeEventArgs>? Subscribed
    {
        add => AddHandler(SubscribedEvent, value);
        remove => RemoveHandler(SubscribedEvent, value);
    }

    /// <inheritdoc cref="SubscriptionUndoneEvent"/>
    public event EventHandler<ChangelogSubscribeEventArgs>? SubscriptionUndone
    {
        add => AddHandler(SubscriptionUndoneEvent, value);
        remove => RemoveHandler(SubscriptionUndoneEvent, value);
    }

    private sealed class GroupView
    {
        public required ChangelogMonth Month { get; init; }
        public required Control Title { get; init; }
        public required List<ChangelogEntryView> Rows { get; init; }
    }

    private static readonly BlockSwapMotion s_rollUp = Roll(1);
    private static readonly BlockSwapMotion s_rollDown = Roll(-1);
    private static readonly BlockSwapMotion s_riseDown = Rise(1);
    private static readonly BlockSwapMotion s_riseUp = Rise(-1);

    private static readonly BlockSwapMotion s_word = new()
    {
        EnterBlur = 2,
        EnterFade = MotionSpec.Tween(ArcMotionPresets.Standard, ArcEasings.Standard),
        EnterMove = MotionSpec.Instant,
        Exit = MotionSpec.Tween(ArcMotionPresets.Instant, ArcEasings.Standard),
    };

    private static readonly BlockSwapMotion s_clear = new()
    {
        EnterX = -6,
        EnterBlur = 2,
        ExitX = -4,
        ExitBlur = 2,
        EnterFade = MotionSpec.Spring(ArcSpring.Snappy),
        EnterMove = MotionSpec.Spring(ArcSpring.Snappy),
        Exit = MotionSpec.Tween(ArcMotionPresets.Instant, ArcEasings.Standard),
    };

    private static BlockSwapMotion Roll(int direction) => new()
    {
        EnterYFraction = direction * 0.55,
        EnterBlur = 2,
        ExitYFraction = direction * -0.55,
        ExitBlur = 2,
        EnterFade = MotionSpec.Spring(ArcSpring.Snappy),
        EnterMove = MotionSpec.Spring(ArcSpring.Snappy),
        Exit = MotionSpec.Spring(ArcSpring.Snappy),
    };

    private static BlockSwapMotion Rise(int direction) => new()
    {
        EnterY = direction * 14,
        EnterBlur = 4,
        ExitY = direction * -14,
        ExitBlur = 4,
        EnterFade = MotionSpec.Spring(ArcSpring.Morph),
        EnterMove = MotionSpec.Spring(ArcSpring.Morph),
        Exit = MotionSpec.Tween(ArcMotionPresets.Exit, ArcEasings.Standard),
    };

    private readonly HashSet<ChangelogKind> _filters = new();
    private readonly HashSet<string> _open = new();
    private readonly List<GroupView> _allGroups = new();
    private readonly List<GroupView> _visibleGroups = new();
    private readonly List<ChangelogEntryView> _rows = new();
    private readonly List<ChangelogChip> _chips = new();
    private readonly BlockScroller _jump;
    private IReadOnlyList<ChangelogEntry> _entries = [];
    private IReadOnlyList<ChangelogMonth> _months = [];
    private ScrollViewer? _scroller;
    private ChangelogFlowPanel? _flow;
    private Panel? _chipsHost;
    private GlidingPillBar? _monthNav;
    private ChangelogSubscribe? _subscribe;
    private SiteAnnouncer? _announcer;
    private BlockSwap? _clear;
    private BlockSwap? _shownCount;
    private BlockSwap? _shownWord;
    private BlockSwap? _monthText;
    private BlockSwap? _monthCount;
    private TextBlock? _monthWord;
    private Control? _endCap;
    private ChangelogClearButton? _clearButton;
    private string? _activeKey;
    private int _shownValue = -1;
    private int _monthValue = -1;
    private bool _shownFiltered;
    private bool _compact;
    private bool _tiny;

    static ChangelogFeed()
    {
        FocusableProperty.OverrideDefaultValue<ChangelogFeed>(false);
        AutomationProperties.LandmarkTypeProperty.OverrideDefaultValue<ChangelogFeed>(AutomationLandmarkType.Region);
    }

    /// <summary>Creates the block.</summary>
    public ChangelogFeed()
    {
        _jump = new BlockScroller(this);
    }

    #region Test hooks

    internal IReadOnlyList<ChangelogChip> Chips => _chips;

    internal GlidingPillBar? MonthNav => _monthNav;

    internal ChangelogSubscribe? Subscribe => _subscribe;

    internal ScrollViewer? Scroller => _scroller;

    internal ChangelogFlowPanel? Flow => _flow;

    internal SiteAnnouncer? Announcer => _announcer;

    internal string? ActiveMonthKey => _activeKey;

    internal IEnumerable<ChangelogEntryView> VisibleRows => _rows.Where(r => _flow?.IsPresent(r) == true);

    internal IReadOnlyList<ChangelogEntryView> AllRows => _rows;

    internal int ShownCount => _shownValue;

    internal bool IsClearVisible => _clear?.Current is not null;

    internal ChangelogClearButton? ClearButton => _clearButton;

    /// <summary>Whether the title of a month's group is in the flow (the first group's title is in the month bar instead).</summary>
    internal bool IsMonthTitleShown(string key) => _flow is not null && _allGroups.FirstOrDefault(g => g.Month.Key == key) is { } group && _flow.IsPresent(group.Title);

    #endregion

    /// <summary>Whether an entry is open.</summary>
    public bool IsEntryOpen(string id) => _open.Contains(id);

    /// <summary>Opens or closes an entry.</summary>
    public void ToggleEntry(string id)
    {
        if (!_open.Add(id))
        {
            _open.Remove(id);
        }
        foreach (var row in _rows)
        {
            if (row.Entry.Id == id)
            {
                row.IsOpen = _open.Contains(id);
            }
        }
    }

    /// <summary>Turns a filter on or off.</summary>
    public void ToggleFilter(ChangelogKind kind)
    {
        if (!_filters.Add(kind))
        {
            _filters.Remove(kind);
        }
        ApplyFilters(animate: true);
    }

    /// <summary>Turns every filter off.</summary>
    public void ClearFilters()
    {
        _filters.Clear();
        ApplyFilters(animate: true);
    }

    /// <summary>Scrolls to a month by its key.</summary>
    public void JumpToMonth(string key)
    {
        if (_scroller is null || _flow is null || _visibleGroups.FirstOrDefault(g => g.Month.Key == key) is not { } group)
        {
            return;
        }
        _jump.To(_scroller, Math.Max(0, TopOfGroup(group) - 1));
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        if (_scroller is not null)
        {
            _scroller.ScrollChanged -= OnScrollChanged;
        }
        if (_monthNav is not null)
        {
            _monthNav.ItemPressed -= OnMonthPressed;
        }
        if (_subscribe is not null)
        {
            _subscribe.Subscribed -= OnSubscribed;
            _subscribe.Undone -= OnUndone;
            _subscribe.StateChanged -= OnSubscribeStateChanged;
        }
        _scroller = e.NameScope.Find<ScrollViewer>(ScrollerPart);
        _flow = e.NameScope.Find<ChangelogFlowPanel>(FlowPart);
        _chipsHost = e.NameScope.Find<Panel>(ChipsPart);
        _monthNav = e.NameScope.Find<GlidingPillBar>(MonthNavPart);
        _subscribe = e.NameScope.Find<ChangelogSubscribe>(SubscribePart);
        _announcer = e.NameScope.Find<SiteAnnouncer>(AnnouncerPart);
        _clear = null;
        _shownCount = e.NameScope.Find<BlockSwap>("PART_ShownCount");
        _shownWord = e.NameScope.Find<BlockSwap>("PART_ShownWord");
        _monthText = e.NameScope.Find<BlockSwap>("PART_MonthText");
        _monthCount = e.NameScope.Find<BlockSwap>("PART_MonthCount");
        _monthWord = e.NameScope.Find<TextBlock>("PART_MonthWord");
        if (_scroller is not null)
        {
            _scroller.ScrollChanged += OnScrollChanged;
        }
        if (_monthNav is not null)
        {
            _monthNav.ItemPressed += OnMonthPressed;
        }
        if (_subscribe is not null)
        {
            _subscribe.Subscribed += OnSubscribed;
            _subscribe.Undone += OnUndone;
            _subscribe.StateChanged += OnSubscribeStateChanged;
            ApplySubscribeText();
        }
        if (_flow is not null)
        {
            _flow.SizeChanged += (_, _) => Sync();
        }
        Rebuild();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        var property = change.Property;
        if (property == EntriesProperty || property == MonthsProperty || property == DefaultOpenProperty
            || property == NewLabelProperty || property == ImprovedLabelProperty || property == FixedLabelProperty
            || property == ShowAllLabelProperty || property == EndNoteProperty)
        {
            Rebuild();
        }
        else if (property == UpdateWordProperty || property == UpdatesWordProperty || property == OfUpdatesFormatProperty || property == NoUpdatesLabelProperty)
        {
            _shownValue = -1;
            _monthValue = -1;
            ApplyCounts(animate: false);
            ApplyMonthBar(0, animate: false);
        }
        else if (property == SubscribeLabelProperty || property == EmailPlaceholderProperty)
        {
            ApplySubscribeText();
        }
        else if (property == TitleProperty)
        {
            AutomationProperties.SetName(this, Title);
        }
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        AutomationProperties.SetName(this, Title);
    }

    protected override void OnSizeChanged(SizeChangedEventArgs e)
    {
        base.OnSizeChanged(e);
        _compact = e.NewSize.Width <= 640;
        _tiny = e.NewSize.Width <= 480;
        PseudoClasses.Set(CompactPseudoClass, _compact);
        PseudoClasses.Set(TinyPseudoClass, _tiny);
        _subscribe?.SetCompact(_compact);
        foreach (var row in _rows)
        {
            row.SetCompact(_compact);
        }
        var inset = _compact ? 20 : 28;
        foreach (var group in _allGroups)
        {
            if (group.Title is Border title)
            {
                title.Padding = new Thickness(inset, 30, inset, 10);
            }
        }
    }

    private void ApplySubscribeText()
    {
        if (_subscribe is not null)
        {
            _subscribe.SubscribeLabel = SubscribeLabel;
            _subscribe.Placeholder = EmailPlaceholder;
        }
    }

    private string KindLabel(ChangelogKind kind) => kind switch
    {
        ChangelogKind.Improved => ImprovedLabel,
        ChangelogKind.Fixed => FixedLabel,
        _ => NewLabel
    };

    #region Building

    private void Rebuild()
    {
        if (_flow is null || _chipsHost is null)
        {
            return;
        }
        _entries = Entries ?? ChangelogFeedSamples.Entries();
        _months = Months ?? ChangelogFeedSamples.Months();
        _open.Clear();
        foreach (var id in DefaultOpen ?? (_entries.Count > 0 ? [_entries[0].Id] : []))
        {
            _open.Add(id);
        }
        BuildChips();
        _rows.Clear();
        _allGroups.Clear();
        _flow.Children.Clear();
        foreach (var month in _months)
        {
            var entries = _entries.Where(e => e.Month == month.Key).ToList();
            var title = BlockText.Create(month.Label, ArcDesignTokenKind.TypeSM, 14, ArcDesignTokenKind.TextMuted, medium: true);
            var titleBox = new Border { Child = title, Padding = new Thickness(_compact ? 20 : 28, 30, _compact ? 20 : 28, 10) };
            ArcTokens.Bind(titleBox, Border.BorderBrushProperty, ArcDesignTokenKind.BorderSubtle);
            ArcTokens.Bind(titleBox, Border.BorderThicknessProperty, ArcDesignTokenKind.EdgeThicknessBottom);
            AutomationProperties.SetHeadingLevel(title, 3);
            _flow.Children.Add(titleBox);
            var rows = new List<ChangelogEntryView>();
            foreach (var entry in entries)
            {
                var row = new ChangelogEntryView(entry, KindLabel(entry.Kind)) { IsOpen = _open.Contains(entry.Id) };
                row.SetCompact(_compact);
                row.Toggled += view => ToggleEntry(view.Entry.Id);
                rows.Add(row);
                _rows.Add(row);
                _flow.Children.Add(row);
            }
            _allGroups.Add(new GroupView { Month = month, Title = titleBox, Rows = rows });
        }
        var end = BlockText.Create(EndNote, ArcDesignTokenKind.TypeSM, 14, ArcDesignTokenKind.TextMuted);
        end.TextAlignment = TextAlignment.Center;
        end.Margin = new Thickness(28, 32, 28, 56);
        _endCap = end;
        _flow.Children.Add(end);
        _shownValue = -1;
        _monthValue = -1;
        _activeKey = null;
        ApplyFilters(animate: false);
    }

    private void BuildChips()
    {
        _chips.Clear();
        _chipsHost!.Children.Clear();
        foreach (var kind in new[] { ChangelogKind.New, ChangelogKind.Improved, ChangelogKind.Fixed })
        {
            var chip = new ChangelogChip(kind)
            {
                Label = KindLabel(kind),
                Count = _entries.Count(e => e.Kind == kind),
                IsChecked = _filters.Contains(kind)
            };
            AutomationProperties.SetName(chip, chip.Label);
            chip.Pressed += ToggleFilter;
            _chips.Add(chip);
            _chipsHost.Children.Add(chip);
        }
        _clearButton = new ChangelogClearButton { Content = ShowAllLabel };
        _clearButton.Click += (_, _) => ClearFilters();
        AutomationProperties.SetName(_clearButton, ShowAllLabel);
        _clear = new BlockSwap { VerticalContentAlignment = VerticalAlignment.Center };
        _chipsHost.Children.Add(_clear);
    }

    #endregion

    #region Filters

    private void ApplyFilters(bool animate)
    {
        if (_flow is null)
        {
            return;
        }
        foreach (var chip in _chips)
        {
            chip.IsChecked = _filters.Contains(chip.Kind);
        }
        if (animate)
        {
            _flow.ArmFlip();
        }
        _visibleGroups.Clear();
        foreach (var group in _allGroups)
        {
            var any = false;
            foreach (var row in group.Rows)
            {
                var show = _filters.Count == 0 || _filters.Contains(row.Entry.Kind);
                _flow.SetPresent(row, show, animate, shrinks: true);
                any |= show;
            }
            if (any)
            {
                _visibleGroups.Add(group);
            }
        }
        foreach (var group in _allGroups)
        {
            // The first month's name is in the month bar, so its title stays out of the list.
            var titled = _visibleGroups.Contains(group) && !ReferenceEquals(_visibleGroups[0], group);
            _flow.SetPresent(group.Title, titled, animate, shrinks: false);
        }
        var clearNext = _filters.Count > 0 ? _clearButton : null;
        if (_clear is not null)
        {
            if (animate)
            {
                _clear.Swap(clearNext, s_clear);
            }
            else
            {
                _clear.Set(clearNext);
            }
        }
        ApplyCounts(animate);
        _monthNav?.SetItems(_visibleGroups.Select(g => g.Month.ShortLabel).ToArray(), i => string.Format(JumpFormat, _visibleGroups[i].Month.Label));
        if (_activeKey is null || _visibleGroups.All(g => g.Month.Key != _activeKey))
        {
            SetActive(_visibleGroups.FirstOrDefault()?.Month.Key, 1, animate);
        }
        else
        {
            ApplyMonthBar(1, animate);
        }
        if (animate)
        {
            _announcer?.Announce(ShownSentence());
        }
    }

    private int VisibleCount => _entries.Count(e => _filters.Count == 0 || _filters.Contains(e.Kind));

    private string ShownSentence() => _filters.Count == 0
        ? $"Showing {_entries.Count} {UpdatesWord}"
        : $"Showing {VisibleCount} {string.Format(OfUpdatesFormat, _entries.Count)}";

    private void ApplyCounts(bool animate)
    {
        var count = VisibleCount;
        var filtered = _filters.Count > 0;
        if (_shownCount is not null && count != _shownValue)
        {
            var next = NumberText(count);
            if (animate && _shownValue >= 0)
            {
                _shownCount.Swap(next, count > _shownValue ? s_rollUp : s_rollDown);
            }
            else
            {
                _shownCount.Set(next);
            }
        }
        var wordChanged = filtered != _shownFiltered || _shownValue < 0;
        _shownValue = count;
        _shownFiltered = filtered;
        if (_shownWord is not null && wordChanged)
        {
            var word = filtered ? string.Format(OfUpdatesFormat, _entries.Count) : UpdatesWord;
            var text = new TextBlock { Text = word };
            if (animate)
            {
                _shownWord.Swap(text, s_word);
            }
            else
            {
                _shownWord.Set(text);
            }
        }
    }

    private static TextBlock NumberText(int value) => new() { Text = value.ToString(CultureInfo.InvariantCulture) };

    #endregion

    #region Month bar

    private void OnMonthPressed(int index)
    {
        if (index >= 0 && index < _visibleGroups.Count)
        {
            JumpToMonth(_visibleGroups[index].Month.Key);
        }
    }

    private double TopOfGroup(GroupView group)
    {
        Control first = _flow!.IsPresent(group.Title) ? group.Title : group.Rows.First(r => _flow.IsPresent(r));
        return _flow.TopOf(first);
    }

    private void OnScrollChanged(object? sender, ScrollChangedEventArgs e) => Sync();

    /// <summary>Follows the scroll: the month whose top passed 28 below the top of the list, or the last at the very bottom.</summary>
    private void Sync()
    {
        if (_scroller is null || _flow is null || _visibleGroups.Count == 0)
        {
            return;
        }
        var line = _scroller.Offset.Y + 28;
        var key = _visibleGroups[0].Month.Key;
        foreach (var group in _visibleGroups)
        {
            if (TopOfGroup(group) <= line)
            {
                key = group.Month.Key;
            }
        }
        if (_scroller.Offset.Y > 0 && _scroller.Offset.Y + _scroller.Viewport.Height >= _scroller.Extent.Height - 2)
        {
            key = _visibleGroups[^1].Month.Key;
        }
        if (key != _activeKey)
        {
            var direction = MonthOrder(key) > MonthOrder(_activeKey) ? 1 : -1;
            SetActive(key, direction, animate: true);
        }
    }

    private int MonthOrder(string? key)
    {
        for (var i = 0; i < _months.Count; i++)
        {
            if (_months[i].Key == key)
            {
                return i;
            }
        }
        return -1;
    }

    private void SetActive(string? key, int direction, bool animate)
    {
        var changed = key != _activeKey;
        _activeKey = key;
        ApplyMonthBar(direction, animate, changed);
    }

    private void ApplyMonthBar(int direction, bool animate, bool labelChanged = false)
    {
        var group = _visibleGroups.FirstOrDefault(g => g.Month.Key == _activeKey) ?? _visibleGroups.FirstOrDefault();
        if (_monthNav is not null)
        {
            _monthNav.SelectedIndex = group is null ? -1 : _visibleGroups.IndexOf(group);
        }
        if (_monthText is not null && (labelChanged || _monthText.Current is null))
        {
            var content = MonthLabel(group);
            if (animate && _monthText.Current is not null)
            {
                _monthText.Swap(content, direction >= 0 ? s_riseDown : s_riseUp);
            }
            else
            {
                _monthText.Set(content);
            }
        }
        var count = group?.Rows.Count(r => _flow?.IsPresent(r) == true) ?? 0;
        if (_monthCount is not null && count != _monthValue)
        {
            if (animate && _monthValue >= 0)
            {
                _monthCount.Swap(NumberText(count), count > _monthValue ? s_rollUp : s_rollDown);
            }
            else
            {
                _monthCount.Set(NumberText(count));
            }
        }
        _monthValue = count;
        if (_monthWord is not null)
        {
            _monthWord.Text = count == 1 ? UpdateWord : UpdatesWord;
        }
    }

    private Control MonthLabel(GroupView? group)
    {
        if (group is null)
        {
            return new TextBlock { Text = NoUpdatesLabel };
        }
        var label = group.Month.Label;
        var space = label.IndexOf(' ');
        var row = new StackPanel { Orientation = Orientation.Horizontal };
        row.Children.Add(new TextBlock { Text = space > 0 ? label[..space] : label });
        if (space > 0)
        {
            var year = new TextBlock { Text = label[space..] };
            year.Classes.Add("month-year");
            year.IsVisible = !_tiny;
            row.Children.Add(year);
        }
        return row;
    }

    #endregion

    #region Subscribe

    private void OnSubscribed(string email) => RaiseEvent(new ChangelogSubscribeEventArgs(SubscribedEvent, this, email));

    private void OnUndone(string email) => RaiseEvent(new ChangelogSubscribeEventArgs(SubscriptionUndoneEvent, this, email));

    private void OnSubscribeStateChanged(ChangelogSubscribeState state)
    {
        SubscribeState = state;
        if (state == ChangelogSubscribeState.Done && _subscribe is { } subscribe)
        {
            _announcer?.Announce(subscribe.DoneMessage);
        }
    }

    #endregion
}
