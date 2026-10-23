using System.Collections.Specialized;
using System.Globalization;
using System.Windows.Input;
using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;

namespace TKCode.Arc;

public partial class CustomerGalaxy : IGalaxyStageHost
{
    private TextBlock? _titleText;
    private TextBlock? _subtitleText;
    private GalaxyTabStrip? _tabs;
    private GalaxyLegendPanel? _legend;
    private GalaxyBodyPanel? _body;
    private GalaxyStage? _stage;
    private GalaxyLabelLayer? _labels;
    private TextBlock? _hint;
    private PresenceHost? _showAllHost;
    private GalaxyChipButton? _showAll;
    private ShowpieceZoomControls? _zoomBar;
    private ShowpieceHoverCard? _card;
    private ShowpieceLiveRegion? _live;
    private GalaxyHiddenTable? _table;
    private GalaxyTotalsPanel? _totals;

    private GalaxyModel _model = GalaxyModel.Empty;
    private GalaxyGroupSet? _layout;
    private string? _layoutKey;
    private readonly Dictionary<string, GalaxyGroupSet> _sets = new(StringComparer.OrdinalIgnoreCase);
    private bool _dirty = true;
    private bool _rebuildPosted;
    private INotifyCollectionChanged? _sourceNotifier;
    private int _focusIndex = -1;
    private int _hoverRow = -1;
    private bool _keyboardCard;
    private bool _cardOnDot;
    private bool _applying;
    private readonly Func<double, string> _mrrFormat;

    static CustomerGalaxy()
    {
        FocusableProperty.OverrideDefaultValue<CustomerGalaxy>(false);
    }

    public CustomerGalaxy()
    {
        _mrrFormat = FormatMrr;
        FlyToGroupCommand = new GalaxyCommand(p => FlyToGroup(p as string), p => p is string);
        ResetViewCommand = new GalaxyCommand(_ => ResetView(), _ => true);
        Items.CollectionChanged += (_, _) =>
        {
            if (ItemsSource is null)
            {
                ScheduleRebuild();
            }
        };
        Groupings.CollectionChanged += (_, _) => ScheduleRebuild();
        HealthStates.CollectionChanged += (_, _) => ScheduleRebuild();
    }

    /// <summary>The stage (tests and the snapshot harness).</summary>
    internal GalaxyStage? Stage => _stage;

    /// <summary>The account snapshot (tests).</summary>
    internal GalaxyModel Model => _model;

    /// <summary>The shown cluster layout, or null for the overview (tests).</summary>
    internal GalaxyGroupSet? LayoutSet => _layout;

    /// <summary>The cluster labels (tests).</summary>
    internal IReadOnlyList<GalaxyClusterLabel> ClusterLabels => _clusterLabels;

    /// <summary>The totals rows in list order (tests).</summary>
    internal IReadOnlyList<GalaxyGroupRow> Rows => _rows is null ? _rowControls : _rows.Order.Count == _rowControls.Count
        ? _rows.Order.Select(i => _rowControls[i]).ToList()
        : _rowControls;

    /// <summary>The hover card (tests).</summary>
    internal ShowpieceHoverCard? Card => _card;

    /// <summary>The live region's last announcement (tests).</summary>
    internal string? LastAnnouncement => _live?.LastAnnouncement;

    /// <summary>The hidden table's rows (tests).</summary>
    internal GalaxyHiddenTable? Table => _table;

    // Template ---------------------------------------------------------------------------------------------------------

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        if (_tabs is not null)
        {
            _tabs.Chosen -= OnTabChosen;
        }
        if (_showAll is not null)
        {
            _showAll.Click -= OnShowAllClick;
        }
        if (_sortMrr is not null)
        {
            _sortMrr.Click -= OnSortClick;
        }
        if (_sortAccounts is not null)
        {
            _sortAccounts.Click -= OnSortClick;
        }
        if (_stage is not null)
        {
            _stage.Host = null;
        }
        var scope = e.NameScope;
        _titleText = scope.Find<TextBlock>(TitlePart);
        _subtitleText = scope.Find<TextBlock>(SubtitlePart);
        _tabs = scope.Find<GalaxyTabStrip>(TabsPart);
        _legend = scope.Find<GalaxyLegendPanel>(LegendPart);
        _body = scope.Find<GalaxyBodyPanel>(BodyPart);
        _stage = scope.Find<GalaxyStage>(StagePart);
        _labels = scope.Find<GalaxyLabelLayer>(LabelsPart);
        _hint = scope.Find<TextBlock>(HintPart);
        _showAllHost = scope.Find<PresenceHost>(ShowAllHostPart);
        _showAll = scope.Find<GalaxyChipButton>(ShowAllPart);
        _zoomBar = scope.Find<ShowpieceZoomControls>(ZoomPart);
        _card = scope.Find<ShowpieceHoverCard>(CardPart);
        _live = scope.Find<ShowpieceLiveRegion>(LivePart);
        _table = scope.Find<GalaxyHiddenTable>(TablePart);
        _totals = scope.Find<GalaxyTotalsPanel>(TotalsPart);
        _scope = scope.Find<ArcContentSwap>(ScopePart);
        _mrrNumber = scope.Find<ShowpieceNumber>(MrrPart);
        _accountsNumber = scope.Find<ShowpieceNumber>(AccountsPart);
        _growthNumber = scope.Find<ShowpieceNumber>(GrowthPart);
        _riskNumber = scope.Find<ShowpieceNumber>(RiskPart);
        _growthDot = scope.Find<Border>(GrowthDotPart);
        _riskDot = scope.Find<Border>(RiskDotPart);
        _growthLabel = scope.Find<TextBlock>(GrowthLabelPart);
        _riskLabel = scope.Find<TextBlock>(RiskLabelPart);
        _listTitle = scope.Find<TextBlock>(ListTitlePart);
        _sortMrr = scope.Find<GalaxySortButton>(SortMrrPart);
        _sortAccounts = scope.Find<GalaxySortButton>(SortAccountsPart);
        _rows = scope.Find<GalaxyRowsPanel>(RowsPart);
        if (_tabs is not null)
        {
            _tabs.Chosen += OnTabChosen;
        }
        if (_showAll is not null)
        {
            _showAll.Click += OnShowAllClick;
        }
        if (_sortMrr is not null)
        {
            _sortMrr.Click += OnSortClick;
        }
        if (_sortAccounts is not null)
        {
            _sortAccounts.Click += OnSortClick;
        }
        if (_stage is not null)
        {
            _stage.Host = this;
            _stage.ColorResolver = ResolveColor;
        }
        if (_zoomBar is not null)
        {
            _zoomBar.Stage = _stage;
        }
        _rowControls.Clear();
        _clusterLabels.Clear();
        ApplyStageOptions();
        ApplyTexts();
        _dirty = true;
        Rebuild();
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        if (_dirty && _stage is not null)
        {
            Rebuild();
        }
        if (_body is not null)
        {
            var width = double.IsFinite(availableSize.Width) ? availableSize.Width : Bounds.Width;
            var side = ShowTotals && (TotalsPlacement == GalaxyTotalsPlacement.End ||
                                      TotalsPlacement == GalaxyTotalsPlacement.Auto && width >= WideBreakpoint);
            if (_body.IsSide != side)
            {
                _body.IsSide = side;
            }
            PseudoClasses.Set(SidePseudoClass, side);
        }
        return base.MeasureOverride(availableSize);
    }

    /// <summary>From this control width on, <see cref="GalaxyTotalsPlacement.Auto"/> puts the totals beside the stage.</summary>
    internal const double WideBreakpoint = 880;

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        ActualThemeVariantChanged += OnThemeChanged;
        ResourcesChanged += OnResourcesChanged;
        // The theme may have changed while the block was away, and a refresh that ran then read transparent colors.
        PostColors();
        if (_dirty)
        {
            ScheduleRebuild();
        }
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        ActualThemeVariantChanged -= OnThemeChanged;
        ResourcesChanged -= OnResourcesChanged;
    }

    private bool _colorsPosted;

    private void OnThemeChanged(object? sender, EventArgs e) => PostColors();

    private void OnResourcesChanged(object? sender, ResourcesChangedEventArgs e) => PostColors();

    private void PostColors()
    {
        if (_colorsPosted)
        {
            return;
        }
        _colorsPosted = true;
        Dispatcher.UIThread.Post(() =>
        {
            _colorsPosted = false;
            // Tokens only resolve inside a window: a block that left meanwhile is refreshed when it comes back.
            if (TopLevel.GetTopLevel(this) is not null)
            {
                RefreshColors();
            }
        }, DispatcherPriority.Loaded);
    }

    // Data -------------------------------------------------------------------------------------------------------------

    /// <summary>
    /// Reads the accounts, groupings and health states again (after changing an account's values in place) and rebuilds
    /// the layout, the totals and the list. The selection and the focus survive when their account and cluster still exist.
    /// </summary>
    public void Refresh()
    {
        _dirty = true;
        Rebuild();
    }

    private void ScheduleRebuild()
    {
        _dirty = true;
        InvalidateMeasure();
        if (_rebuildPosted)
        {
            return;
        }
        _rebuildPosted = true;
        Dispatcher.UIThread.Post(() =>
        {
            _rebuildPosted = false;
            if (_dirty)
            {
                Rebuild();
            }
        }, DispatcherPriority.Loaded);
    }

    private void Rebuild()
    {
        _dirty = false;
        IReadOnlyList<GalaxyAccount> source = ItemsSource is { } items
            ? items as IReadOnlyList<GalaxyAccount> ?? items.ToList()
            : Items;
        _model = GalaxyModel.Build(source, HealthStates);
        _sets.Clear();
        _layoutKey = ResolveGroupingKey(SelectedGroupingKey);
        _layout = _layoutKey is null ? null : SetFor(_layoutKey);
        _focusIndex = _layout?.IndexOf(FocusedGroupKey) ?? -1;
        _hoverRow = -1;
        var selected = _model.IndexOf(SelectedAccount);
        _applying = true;
        try
        {
            if (FocusedGroupKey is not null && _focusIndex < 0)
            {
                SetCurrentValue(FocusedGroupKeyProperty, null);
            }
            if (SelectedAccount is not null && selected < 0)
            {
                SetCurrentValue(SelectedAccountProperty, null);
            }
            else if (selected >= 0 && !ReferenceEquals(SelectedAccount, _model.Accounts[selected]))
            {
                SetCurrentValue(SelectedAccountProperty, _model.Accounts[selected]);
            }
        }
        finally
        {
            _applying = false;
        }
        if (_stage is null)
        {
            return;
        }
        _stage.Load(_model, _layout);
        _stage.InvalidateColors();
        _stage.Select(selected);
        if (_focusIndex >= 0)
        {
            _stage.Dim(_focusIndex);
            _stage.FlyToGroup(_focusIndex, animate: false);
        }
        RefreshTabs(animate: false);
        RefreshLegend();
        RebuildLabels(animateIn: false);
        RebuildList();
        RefreshTotals();
        RefreshSummary();
        RefreshStates();
        UpdateCard();
        SyncFrame();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        var property = change.Property;
        if (property == ItemsSourceProperty)
        {
            if (_sourceNotifier is not null)
            {
                _sourceNotifier.CollectionChanged -= OnSourceChanged;
            }
            _sourceNotifier = ItemsSource as INotifyCollectionChanged;
            if (_sourceNotifier is not null)
            {
                _sourceNotifier.CollectionChanged += OnSourceChanged;
            }
            ScheduleRebuild();
        }
        else if (property == SelectedGroupingKeyProperty)
        {
            OnGroupingKeyChanged(change.GetOldValue<string?>(), change.GetNewValue<string?>());
        }
        else if (property == FocusedGroupKeyProperty)
        {
            OnFocusedGroupChanged(change.GetNewValue<string?>());
        }
        else if (property == SelectedAccountProperty)
        {
            OnSelectedAccountChanged(change.GetNewValue<GalaxyAccount?>());
        }
        else if (property == GroupSortProperty)
        {
            RefreshSortButtons();
            UpdateList(animate: true);
        }
        else if (property == IsAutoRotateProperty || property == RotationSpeedProperty || property == ZoomStepProperty ||
                 property == WheelZoomSensitivityProperty || property == MaxZoomProperty)
        {
            ApplyStageOptions();
        }
        else if (property == TotalsPlacementProperty || property == ShowTotalsProperty)
        {
            InvalidateMeasure();
        }
        else if (property == MrrFormatterProperty || property == CurrencySymbolProperty || property == CultureNameProperty)
        {
            if (_stage is not null)
            {
                RebuildLabels(animateIn: false);
                UpdateList(animate: false);
                RefreshTotals();
                UpdateCard();
            }
        }
        else if (property == AccountTooltipTemplateProperty)
        {
            UpdateCard();
        }
        else if (property == TitleProperty || property == SubtitleProperty || property == SubtitleFormatProperty ||
                 property == AllTextProperty || property == AllAccountsTextProperty || property == SizeLegendTextProperty ||
                 property == ListTitleFormatProperty || property == HealthGroupingTextProperty || property == AccountCountFormatProperty ||
                 property == OneAccountFormatProperty || property == AtRiskShareFormatProperty || property == GroupByTextProperty ||
                 property == SortByTextProperty || property == ZoomInTextProperty || property == ZoomOutTextProperty ||
                 property == FitTextProperty || property == KeyboardHelpTextProperty || property == MrrTextProperty ||
                 property == AccountsTextProperty || property == FlyToClusterTextProperty || property == GroupAndFlyFormatProperty)
        {
            ApplyTexts();
            if (_stage is not null)
            {
                RefreshTabs(animate: false);
                RefreshLegend();
                RebuildLabels(animateIn: false);
                UpdateList(animate: false);
                RefreshTotals();
                RefreshSummary();
            }
        }
    }

    private void OnSourceChanged(object? sender, NotifyCollectionChangedEventArgs e) => ScheduleRebuild();

    private void ApplyStageOptions()
    {
        if (_stage is not null)
        {
            _stage.IsRotating = IsAutoRotate;
            _stage.RotationSpeed = RotationSpeed * Math.PI / 180;
            _stage.KeyZoomFactor = ZoomStep > 1 ? ZoomStep : 1.6;
            _stage.WheelSensitivity = WheelZoomSensitivity;
            _stage.View.MaxZoom = Math.Max(1, MaxZoom);
            _stage.RequestFrames();
        }
        if (_zoomBar is not null)
        {
            _zoomBar.ZoomStep = ZoomStep > 1 ? ZoomStep : 1.6;
        }
    }

    /// <summary>The grouping a key shows: a listed grouping's key, the health key (built in), or null (overview).</summary>
    private string? ResolveGroupingKey(string? key)
    {
        if (string.IsNullOrEmpty(key))
        {
            return null;
        }
        if (FindGrouping(key) is { } grouping)
        {
            return grouping.Key;
        }
        return string.Equals(key, GalaxyGrouping.HealthKey, StringComparison.OrdinalIgnoreCase) ? GalaxyGrouping.HealthKey : null;
    }

    private GalaxyGrouping? FindGrouping(string? key)
    {
        if (key is null)
        {
            return null;
        }
        foreach (var grouping in Groupings)
        {
            if (string.Equals(grouping.Key, key, StringComparison.OrdinalIgnoreCase))
            {
                return grouping;
            }
        }
        return null;
    }

    private GalaxyGroupSet SetFor(string key)
    {
        if (_sets.TryGetValue(key, out var set))
        {
            return set;
        }
        var grouping = FindGrouping(key);
        set = _model.BuildSet(key, grouping?.Groups ?? (IReadOnlyList<GalaxyGroupDefinition>)Array.Empty<GalaxyGroupDefinition>(), OtherText);
        set.EnsureGeometry(_model, LabelSizesFor(set));
        _sets[key] = set;
        return set;
    }

    // Grouping, focus, selection ---------------------------------------------------------------------------------------

    private void OnGroupingKeyChanged(string? oldKey, string? newKey)
    {
        if (_applying)
        {
            return;
        }
        var key = ResolveGroupingKey(newKey);
        if (newKey is not null && key is null && Groupings.Count > 0)
        {
            // An unknown key shows the overview; the property says so.
            _applying = true;
            SetCurrentValue(SelectedGroupingKeyProperty, null);
            _applying = false;
            newKey = null;
        }
        if (_stage is null || _dirty)
        {
            RaiseEvent(new GalaxyGroupingChangedEventArgs(GroupingChangedEvent, oldKey, newKey));
            return;
        }
        if (string.Equals(key, _layoutKey, StringComparison.OrdinalIgnoreCase) && ReferenceEquals(_layout, key is null ? null : SetFor(key)))
        {
            return;
        }
        _layoutKey = key;
        _layout = key is null ? null : SetFor(key);
        _hoverRow = -1;
        _focusIndex = -1;
        if (FocusedGroupKey is not null)
        {
            _applying = true;
            SetCurrentValue(FocusedGroupKeyProperty, null);
            _applying = false;
            RaiseEvent(new GalaxyGroupEventArgs(GroupFocusedEvent, oldKey, null, null));
        }
        _stage.ShowLayout(_layout);
        RefreshTabs(animate: true);
        RebuildLabels(animateIn: true);
        RebuildList();
        RefreshTotals();
        RefreshSummary();
        RefreshStates();
        AnnounceLayout();
        SyncFrame();
        RaiseEvent(new GalaxyGroupingChangedEventArgs(GroupingChangedEvent, oldKey, newKey));
    }

    private void OnFocusedGroupChanged(string? key)
    {
        if (_applying || _stage is null || _dirty)
        {
            return;
        }
        var index = _layout?.IndexOf(key) ?? -1;
        if (key is not null && index < 0)
        {
            _applying = true;
            SetCurrentValue(FocusedGroupKeyProperty, null);
            _applying = false;
        }
        var had = _focusIndex >= 0;
        _focusIndex = index;
        if (index >= 0)
        {
            _hoverRow = -1;
            _stage.Dim(index);
            _stage.FlyToGroup(index);
            Announce(GroupAnnouncement(_layout!.Groups[index]));
        }
        else
        {
            _stage.Dim(_hoverRow);
            if (had)
            {
                _stage.FitView();
            }
        }
        RefreshTotals();
        UpdateList(animate: false);
        RefreshLabelDimming();
        RefreshStates();
        SyncFrame();
        RaiseEvent(new GalaxyGroupEventArgs(GroupFocusedEvent, _layoutKey, index >= 0 ? _layout!.Groups[index].Key : null,
            index >= 0 ? _layout!.Groups[index] : null));
    }

    private void OnSelectedAccountChanged(GalaxyAccount? account)
    {
        if (_applying || _stage is null || _dirty)
        {
            return;
        }
        var index = _model.IndexOf(account);
        if (account is not null && index < 0)
        {
            _applying = true;
            SetCurrentValue(SelectedAccountProperty, null);
            _applying = false;
            account = null;
        }
        _stage.Select(index);
        if (index < 0)
        {
            _keyboardCard = false;
        }
        UpdateCard();
        RaiseEvent(new GalaxyAccountEventArgs(AccountSelectedEvent, account));
    }

    /// <summary>Flies into the cluster <paramref name="key"/> of the shown grouping and focuses it (same as <see cref="FocusedGroupKey"/>).</summary>
    public void FlyToGroup(string? key)
    {
        if (key is null)
        {
            ResetView();
            return;
        }
        if (_layout is null)
        {
            // The overview's list is by health: switch to the health grouping first.
            SetCurrentValue(SelectedGroupingKeyProperty, Groupings.FirstOrDefault(g => g.IsHealth)?.Key ?? GalaxyGrouping.HealthKey);
        }
        if (string.Equals(FocusedGroupKey, key, StringComparison.Ordinal) && _focusIndex >= 0)
        {
            _stage?.FlyToGroup(_focusIndex);
            return;
        }
        SetCurrentValue(FocusedGroupKeyProperty, key);
    }

    /// <summary>Selects <paramref name="account"/> and flies to it (zoomed in at least 3×).</summary>
    public void FlyToAccount(GalaxyAccount? account)
    {
        SetCurrentValue(SelectedAccountProperty, account);
        var index = _model.IndexOf(account);
        if (index >= 0)
        {
            _keyboardCard = true;
            _stage?.FlyToDot(index);
            UpdateCard();
        }
    }

    /// <summary>Returns to every cluster: clears the focus and fits the view (the selection stays).</summary>
    public void ResetView()
    {
        var had = FocusedGroupKey is not null;
        if (had)
        {
            SetCurrentValue(FocusedGroupKeyProperty, null);
        }
        else
        {
            _stage?.FitView();
        }
        AnnounceLayout();
    }

    /// <summary>Zooms by <paramref name="factor"/> about <paramref name="anchor"/> (stage px; null: the center), within 1…<see cref="MaxZoom"/>.</summary>
    public void ZoomBy(double factor, Point? anchor = null)
    {
        _stage?.View.ZoomBy(factor, anchor);
        _stage?.RequestFrames();
    }

    /// <summary>Focuses the next cluster by MRR (the largest when none is focused); false in the overview.</summary>
    public bool FocusNextGroup() => StepGroup(+1);

    /// <summary>Focuses the previous cluster by MRR (the smallest when none is focused); false in the overview.</summary>
    public bool FocusPreviousGroup() => StepGroup(-1);

    private bool StepGroup(int step)
    {
        if (_layout is not { Count: > 0 } layout)
        {
            return false;
        }
        var count = layout.Count;
        var next = _focusIndex < 0 ? (step > 0 ? 0 : count - 1) : ((_focusIndex + step) % count + count) % count;
        SetCurrentValue(FocusedGroupKeyProperty, layout.Groups[next].Key);
        return true;
    }

    /// <summary>Selects the next account by MRR in the focused cluster (in all accounts when none is focused).</summary>
    public bool SelectNextAccount() => StepAccount(+1);

    /// <summary>Selects the previous account by MRR in the focused cluster; stays on the first.</summary>
    public bool SelectPreviousAccount() => StepAccount(-1);

    private bool StepAccount(int step)
    {
        if (_model.Count == 0)
        {
            return false;
        }
        var members = _layout is { } layout && _focusIndex >= 0 ? layout.Members[_focusIndex] : null;
        var count = members?.Length ?? _model.Count;
        if (count == 0)
        {
            return false;
        }
        var current = _model.IndexOf(SelectedAccount);
        var position = current < 0 ? -1 : members is null ? current : Array.IndexOf(members, current);
        var next = position < 0 ? 0 : Math.Clamp(position + step, 0, count - 1);
        var index = members is null ? next : members[next];
        _keyboardCard = true;
        SetCurrentValue(SelectedAccountProperty, _model.Accounts[index]);
        UpdateCard();
        Announce(AccountAnnouncement(index));
        return true;
    }

    // Stage host -------------------------------------------------------------------------------------------------------

    void IGalaxyStageHost.OnStageHover(Point? position, GalaxyAccount? account)
    {
        if (position is not null && account is not null)
        {
            _keyboardCard = false;
        }
        HoveredAccount = account;
        UpdateCard();
    }

    void IGalaxyStageHost.OnStageTap(GalaxyAccount? account)
    {
        _keyboardCard = false;
        SetCurrentValue(SelectedAccountProperty, account);
    }

    void IGalaxyStageHost.OnStageDoubleTap(int group)
    {
        if (group >= 0 && _layout is { } layout && group < layout.Count)
        {
            FlyToGroup(layout.Groups[group].Key);
        }
    }

    bool IGalaxyStageHost.OnStageKey(KeyEventArgs e)
    {
        if (e.KeyModifiers != KeyModifiers.None && e.KeyModifiers != KeyModifiers.Shift)
        {
            return false;
        }
        switch (e.Key)
        {
            case Key.Right:
                return FocusNextGroup();
            case Key.Left:
                return FocusPreviousGroup();
            case Key.Down:
                return SelectNextAccount();
            case Key.Up:
                return SelectPreviousAccount();
            case Key.Escape:
                if (SelectedAccount is not null)
                {
                    SetCurrentValue(SelectedAccountProperty, null);
                    if (_focusIndex >= 0 && _layout is { } layout)
                    {
                        Announce(GroupAnnouncement(layout.Groups[_focusIndex]));
                    }
                    return true;
                }
                if (FocusedGroupKey is not null || _stage is { View.IsAtFit: false })
                {
                    ResetView();
                    return true;
                }
                return false;
        }
        return false;
    }

    void IGalaxyStageHost.OnStageReset() => ResetView();

    string? IGalaxyStageHost.StageSummary => StageSummaryText();

    Size IGalaxyStageHost.LabelSize(int group)
    {
        if (group >= 0 && group < _clusterLabels.Count && _clusterLabels[group].DesiredSize.Width > 0)
        {
            return _clusterLabels[group].DesiredSize;
        }
        if (_layout is { } layout && group >= 0 && group < layout.LabelSizes.Length)
        {
            return layout.LabelSizes[group];
        }
        return new Size(120, GalaxyLayout.LabelHeight);
    }

    /// <summary>The camera or the dots moved: labels, the anchored card and the zoom bar follow.</summary>
    private void SyncFrame()
    {
        if (_stage is null)
        {
            return;
        }
        PositionLabels();
        if (_cardOnDot && SelectedAccount is not null && HoveredAccount is null)
        {
            AnchorCardToDot();
        }
        var camera = _stage.View;
        if (_zoomBar is not null && _zoomBar.IsAtFit != camera.IsAtFit)
        {
            _zoomBar.IsAtFit = camera.IsAtFit;
        }
        var zoom = Math.Round(camera.Zoom, 2);
        if (Math.Abs(zoom - _zoom) >= 0.01 || !camera.IsMoving && zoom != _zoom)
        {
            Zoom = zoom;
        }
    }

    void IGalaxyStageHost.OnStageFrame() => SyncFrame();

    // Card -------------------------------------------------------------------------------------------------------------

    private readonly GalaxyAccountCard _builtinCard = new();

    private void UpdateCard()
    {
        if (_card is null || _stage is null)
        {
            return;
        }
        var account = HoveredAccount ?? SelectedAccount;
        if (account is null || _model.IndexOf(account) < 0)
        {
            _card.IsOpen = false;
            _cardOnDot = false;
            return;
        }
        if (AccountTooltipTemplate is { } template)
        {
            SetCardContent(account, template);
        }
        else
        {
            FillCard(_builtinCard, account);
            SetCardContent(_builtinCard, null);
        }
        var pointer = _stage.PointerPosition;
        if (pointer is { } p && (HoveredAccount is not null || !_keyboardCard))
        {
            _cardOnDot = false;
            SetPlacement(ShowpieceCardPlacement.Galaxy);
            _card.ShowAt(p);
            return;
        }
        if (HoveredAccount is not null && pointer is null)
        {
            _card.IsOpen = false;
            return;
        }
        _cardOnDot = true;
        AnchorCardToDot();
        _card.IsOpen = true;
    }

    /// <summary>
    /// Swaps the card's content. A presenter builds the template it has whenever its content changes, so the template is
    /// cleared first, the content set, and the template attached again only for non-null content: a user template is never
    /// built with null (or with the previous account's type).
    /// </summary>
    private void SetCardContent(object content, Avalonia.Controls.Templates.IDataTemplate? template)
    {
        if (_card is null || ReferenceEquals(_card.Content, content) && ReferenceEquals(_card.ContentTemplate, template))
        {
            return;
        }
        _card.ContentTemplate = null;
        _card.Content = content;
        if (template is not null)
        {
            _card.ContentTemplate = template;
        }
    }

    private void AnchorCardToDot()
    {
        if (_card is null || _stage is null)
        {
            return;
        }
        var index = _model.IndexOf(SelectedAccount);
        if (index < 0)
        {
            return;
        }
        var (center, radius) = _stage.DotOnScreen(index);
        // Pinned beside the dot: its left edge 12 px past the dot's edge, centered on the dot.
        SetPlacement(ShowpieceCardPlacement.Galaxy with { Gap = 12 });
        _card.Anchor = new Point(center.X + radius, center.Y);
    }

    private void SetPlacement(ShowpieceCardPlacement placement)
    {
        if (_card is not null && _card.Placement != placement)
        {
            _card.Placement = placement;
        }
    }

    private void FillCard(GalaxyAccountCard card, GalaxyAccount account)
    {
        var index = _model.IndexOf(account);
        card.Title = account.Name;
        card.Subtitle = CardSubtitle(account);
        card.MrrLabel = MrrText;
        card.MrrValue = ShowpieceFormat.Currency(account.Mrr, 0, CurrencySymbol, Culture);
        card.OwnerLabel = OwnerText;
        card.OwnerValue = account.Owner;
        card.HealthLabel = HealthText;
        if (index >= 0 && _model.States.Length > 0)
        {
            var state = _model.States[_model.Health[index]];
            card.HealthValue = state.DisplayLabel;
            card.HealthBrush = BrushFor(state, state.DotOpacity >= 0.9 ? 1 : state.DotOpacity);
        }
        card.Note = account.Note;
    }

    private string CardSubtitle(GalaxyAccount account)
    {
        var parts = new List<string>();
        foreach (var grouping in Groupings)
        {
            if (grouping.IsHealth)
            {
                continue;
            }
            if (account.GroupValue(grouping.Key) is { Length: > 0 } value)
            {
                var label = value;
                foreach (var definition in grouping.Groups)
                {
                    if (string.Equals(definition.Key, value, StringComparison.Ordinal))
                    {
                        label = definition.Label ?? definition.Key;
                        break;
                    }
                }
                parts.Add(label);
            }
        }
        return string.Join(" · ", parts);
    }

    // Events -----------------------------------------------------------------------------------------------------------

    private void OnTabChosen(int index)
    {
        var key = index <= 0 ? null : index - 1 < Groupings.Count ? Groupings[index - 1].Key : null;
        SetCurrentValue(SelectedGroupingKeyProperty, key);
    }

    private void OnShowAllClick(object? sender, RoutedEventArgs e)
    {
        ResetView();
        _stage?.Focus(NavigationMethod.Tab);
    }

    private void OnSortClick(object? sender, RoutedEventArgs e)
    {
        SetCurrentValue(GroupSortProperty, ReferenceEquals(sender, _sortAccounts) ? GalaxyGroupSort.Accounts : GalaxyGroupSort.Mrr);
    }

    // Announcements ----------------------------------------------------------------------------------------------------

    private void Announce(string? text) => _live?.Announce(text);

    private void AnnounceLayout()
    {
        if (_layout is not { Count: > 0 } layout)
        {
            Announce(OverviewAnnouncementText);
            return;
        }
        var lead = layout.Groups[0];
        Announce(string.Format(Culture, GroupingAnnouncementFormat, layout.Count, GroupingName().ToLower(Culture), lead.Label, FormatMrr(lead.Mrr)));
    }

    private string GroupAnnouncement(GalaxyGroupSummary group) =>
        string.Format(Culture, GroupAnnouncementFormat, group.Label, AccountCountText(group.AccountCount), FormatMrr(group.Mrr), FormatMrr(group.AtRiskMrr));

    private string AccountAnnouncement(int index)
    {
        var account = _model.Accounts[index];
        var health = _model.States.Length > 0 ? _model.States[_model.Health[index]].DisplayLabel : string.Empty;
        return string.Format(Culture, AccountAnnouncementFormat, account.Name, FormatMrr(account.Mrr), health, account.Owner, account.Note).TrimEnd();
    }

    private string GroupingName()
    {
        if (_layoutKey is not null && FindGrouping(_layoutKey) is { } grouping)
        {
            return grouping.DisplayHeader;
        }
        return HealthGroupingName();
    }

    private string StageSummaryText()
    {
        var layout = _layout is null
            ? OverviewLayoutText
            : string.Format(Culture, GroupedLayoutFormat, GroupingName().ToLower(Culture));
        return string.Format(Culture, StageSummaryFormat, ShowpieceFormat.Grouped(_model.Count, 0, Culture), FormatMrr(_model.TotalMrr), layout);
    }

    // Formatting -------------------------------------------------------------------------------------------------------

    private CultureInfo Culture
    {
        get
        {
            try
            {
                return CultureInfo.GetCultureInfo(string.IsNullOrEmpty(CultureName) ? "en-US" : CultureName);
            }
            catch (CultureNotFoundException)
            {
                return ShowpieceFormat.DefaultCulture;
            }
        }
    }

    /// <summary>An amount as the totals show it: <see cref="MrrFormatter"/>, or compact currency.</summary>
    internal string FormatMrr(double value) =>
        MrrFormatter is { } formatter ? formatter(value) : ShowpieceFormat.CompactCurrency(value, CurrencySymbol, Culture);

    private string AccountCountText(int count) =>
        string.Format(Culture, count == 1 ? OneAccountFormat : AccountCountFormat, ShowpieceFormat.Grouped(count, 0, Culture));

    private sealed class GalaxyCommand : ICommand
    {
        private readonly Action<object?> _execute;
        private readonly Func<object?, bool> _canExecute;

        public GalaxyCommand(Action<object?> execute, Func<object?, bool> canExecute)
        {
            _execute = execute;
            _canExecute = canExecute;
        }

        public event EventHandler? CanExecuteChanged
        {
            add { }
            remove { }
        }

        public bool CanExecute(object? parameter) => _canExecute(parameter);

        public void Execute(object? parameter) => _execute(parameter);
    }
}
