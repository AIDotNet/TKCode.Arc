using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using TKCode.Arc.Primitives;

namespace TKCode.Arc;

public partial class MonthCalendar
{
    private MonthSidebar? _sidebar;
    private MonthMonthView? _monthView;
    private MonthDayPanel? _dayPanel;
    private ScreenSearchBox? _filter;
    private MonthTitleSwap? _title;
    private Control? _bodyContent;
    private MonthCalendarView _builtView = (MonthCalendarView)(-1);
    private bool _compact;

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _root = e.NameScope.Find<Panel>(RootPart);
        if (_root is null)
        {
            return;
        }
        _shell = new ScreenShell
        {
            IsPanelOpen = IsDayPanelOpen,
            PanelWidth = DayPanelWidth,
            IsSidebarCollapsed = IsSidebarCollapsed,
        };
        _shell.LayoutChanged += OnShellLayoutChanged;
        _shell.ScrimPressed += () =>
        {
            _shell.IsSidebarSheetOpen = false;
            _shell.IsPanelSheetOpen = false;
        };
        _shell.PanelWidthChanged += w => SetCurrentValue(DayPanelWidthProperty, w);
        _shell.SidebarCollapseChanged += t => _sidebar?.SetCollapse(t);
        _shell.SizeChanged += (_, _) => UpdateCompact();
        _announcer = new TextBlock { Width = 1, Height = 1, Opacity = 0, IsHitTestVisible = false, ClipToBounds = true };
        AutomationProperties.SetLiveSetting(_announcer, AutomationLiveSetting.Polite);
        _root.Children.Clear();
        _root.Children.Add(_shell);
        _root.Children.Add(_announcer);
        Hook();
        foreach (var s in Calendars)
        {
            s.PropertyChanged -= OnSourcePropertyChanged;
            s.PropertyChanged += OnSourcePropertyChanged;
        }
        _built = true;
        RefreshAll();
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        ActualThemeVariantChanged += OnThemeChanged;
        ArcToday.Changed += OnTodayChanged;
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        ActualThemeVariantChanged -= OnThemeChanged;
        ArcToday.Changed -= OnTodayChanged;
    }

    private void OnTodayChanged(object? sender, EventArgs e) => Dispatcher.UIThread.Post(() => RefreshAll());

    private void OnThemeChanged(object? sender, EventArgs e)
    {
        _tones.Clear();
        _shell?.ApplyPaint();
        _builtView = (MonthCalendarView)(-1);
        RefreshAll();
    }

    private void OnShellLayoutChanged()
    {
        UpdateCompact();
        RefreshHeader();
        RefreshSidebar();
        RefreshPanel(crossfade: false);
    }

    private void UpdateCompact()
    {
        if (_shell is null)
        {
            return;
        }
        // Arc: the month switches to load meters when its card is narrower than 560 (inside the border).
        var compact = _shell.MainWidth > 0 && _shell.MainWidth - 2 < 560 || _shell.IsPhone;
        if (compact != _compact)
        {
            _compact = compact;
            _builtView = (MonthCalendarView)(-1);
            Dispatcher.UIThread.Post(() => RefreshAll());
        }
    }

    internal bool IsCompact => _compact;

    // Refresh ----------------------------------------------------------------------------------------------------------

    internal void RefreshAll(bool crossfadePanel = false)
    {
        if (!_built || _shell is null)
        {
            return;
        }
        RefreshSidebar();
        RefreshHeader();
        RefreshBody();
        RefreshPanel(crossfadePanel);
    }

    internal void RefreshSidebar()
    {
        if (_shell is null)
        {
            return;
        }
        _sidebar ??= new MonthSidebar(this);
        if (!ReferenceEquals(_shell.Sidebar, _sidebar))
        {
            _shell.Sidebar = _sidebar;
        }
        AutomationProperties.SetName(_shell.SidebarHost, string.Format(EffectiveCulture, Labels.SidebarName, WorkspaceName));
        _sidebar.Rebuild();
    }

    internal void RefreshHeader()
    {
        if (_shell is null)
        {
            return;
        }
        var paint = Paint;
        var labels = Labels;
        var header = _shell.Header;
        header.MenuButton ??= ScreenButtons.Icon(paint, ArcIcons.Menu, labels.OpenNavigation, () => _shell.IsSidebarSheetOpen = true);
        header.Crumbs.Children.Clear();
        header.Actions.Clear();
        var calendarLike = View is MonthCalendarView.Calendar or MonthCalendarView.Teammate;
        var phone = _shell.IsPhone;
        if (View == MonthCalendarView.Teammate)
        {
            header.Crumbs.Children.Add(ScreenHeader.Crumb(paint, labels.TeamCalendarsCrumb));
            header.Crumbs.Children.Add(ScreenHeader.Crumb(paint, PersonOf(TeammateId)?.Name ?? TeammateId ?? string.Empty,
                () => GoToToday()));
        }
        else if (calendarLike && !phone)
        {
            header.Crumbs.Children.Add(ScreenHeader.Crumb(paint, labels.Calendar));
        }
        if (calendarLike)
        {
            _title ??= new MonthTitleSwap(this);
            _title.Show(Month, narrow: phone || (_shell.MainWidth > 0 && _shell.MainWidth < 640));
            ArcShrinkRow.SetIsShrinkable(_title, true);
            header.Crumbs.Children.Add(_title);
            if (!phone)
            {
                var (total, matching) = MonthTotals();
                var meta = IsFiltering
                    ? string.Format(EffectiveCulture, labels.MatchCount, matching, total)
                    : string.Format(EffectiveCulture, labels.EventCount, total);
                var metaText = ScreenText.Line(meta, 12, paint.MutedBrush);
                metaText.FontFeatures = FontFeatureCollection.Parse("tnum");
                header.Crumbs.Children.Add(metaText);
            }
        }
        else
        {
            var title = ScreenText.Line(View == MonthCalendarView.Upcoming ? labels.Upcoming : labels.Invitations, 13,
                paint.ForegroundBrush, FontWeight.Medium);
            ArcShrinkRow.SetIsShrinkable(title, true);
            header.Crumbs.Children.Add(title);
            var count = PendingInvitations().Count();
            var meta = View == MonthCalendarView.Upcoming
                ? labels.UpcomingMeta
                : string.Format(EffectiveCulture, labels.InvitationsMeta, count);
            if (!phone)
            {
                header.Crumbs.Children.Add(ScreenText.Line(meta, 12, paint.MutedBrush));
            }
        }

        if (View != MonthCalendarView.Invitations)
        {
            if (_filter is null)
            {
                _filter = new ScreenSearchBox(paint, labels.FilterEvents, labels.Dismiss);
                _filter.TextChanged += text => SetCurrentValue(FilterTextProperty, text);
                _filter.Input.KeyDown += (_, e) =>
                {
                    if (e.Key == Key.Escape)
                    {
                        _filter.Text = string.Empty;
                        e.Handled = true;
                        FocusGridDay();
                    }
                };
            }
            _filter.ApplyPaint(paint);
            _filter.Text = FilterText ?? string.Empty;
            header.Search = _filter;
        }
        else
        {
            header.Search = null;
        }
        if (calendarLike)
        {
            if (!phone)
            {
                header.Actions.Add(ScreenButtons.Text(paint, labels.Today, GoToToday));
            }
            var arrows = new StackPanel { Orientation = Orientation.Horizontal };
            arrows.Children.Add(ScreenButtons.Icon(paint, ArcIcons.ChevronLeft, labels.PreviousMonth, () => StepMonth(-1)));
            arrows.Children.Add(ScreenButtons.Icon(paint, ArcIcons.ChevronRight, labels.NextMonth, () => StepMonth(1)));
            header.Actions.Add(arrows);
        }
        header.Actions.Add(ScreenButtons.Text(paint, labels.NewEvent, () => OpenNewEvent(SelectedDate), primary: true,
            glyph: ArcIcons.Plus, iconOnly: phone));
        var panelShown = _shell.IsPanelDocked ? IsDayPanelOpen : _shell.IsPanelSheetOpen;
        var toggle = ScreenButtons.Icon(paint, ArcIcons.PanelRight, panelShown ? labels.HideDay : labels.ShowDay, TogglePanel,
            toggle: true);
        toggle.IsActive = panelShown;
        header.Actions.Add(toggle);
    }

    private void TogglePanel()
    {
        if (_shell is null)
        {
            return;
        }
        if (_shell.IsPanelDocked)
        {
            IsDayPanelOpen = !IsDayPanelOpen;
        }
        else
        {
            _shell.IsPanelSheetOpen = !_shell.IsPanelSheetOpen;
            RefreshHeader();
        }
    }

    internal void RefreshBody()
    {
        if (_shell is null)
        {
            return;
        }
        var view = View == MonthCalendarView.Teammate ? MonthCalendarView.Calendar : View;
        if (view != _builtView)
        {
            _builtView = view;
            _bodyContent = view switch
            {
                MonthCalendarView.Upcoming => new MonthUpcomingView(this),
                MonthCalendarView.Invitations => new MonthInvitationsView(this),
                _ => _monthView = new MonthMonthView(this),
            };
            if (view != MonthCalendarView.Calendar)
            {
                _monthView = null;
            }
            _shell.Body = _bodyContent;
        }
        RefreshGrid();
        RefreshLists();
    }

    internal void RefreshGrid() => _monthView?.Refresh();

    internal void RefreshLists()
    {
        switch (_bodyContent)
        {
            case MonthUpcomingView up:
                up.Rebuild();
                break;
            case MonthInvitationsView inv:
                inv.Rebuild();
                break;
        }
    }

    internal void RefreshPanel(bool crossfade)
    {
        if (_shell is null)
        {
            return;
        }
        _dayPanel ??= new MonthDayPanel(this);
        if (!ReferenceEquals(_shell.Panel, _dayPanel))
        {
            _shell.Panel = _dayPanel;
        }
        AutomationProperties.SetName(_shell.PanelCard, Labels.DayPanel);
        AutomationProperties.SetName(_shell.ResizeHandle, Labels.ResizeDayPanel);
        _dayPanel.Show(_pane, SelectedDate, _openEvent, _draft, crossfade);
    }

    private void MonthChangedVisual(int direction)
    {
        _title?.Show(Month, _title.IsNarrow, direction);
        _monthView?.ChangeMonth(direction);
    }

    // Focus helpers ----------------------------------------------------------------------------------------------------

    internal DateOnly FocusDate
    {
        get => _focusDate;
        set => _focusDate = value;
    }

    internal void FocusChip(string id) => _monthView?.Grid.FocusChip(id);

    internal void FocusGridDay() => Dispatcher.UIThread.Post(() => _monthView?.FocusDay(_focusDate), DispatcherPriority.Input);

    private void FocusFilter()
    {
        _filter?.FocusInput();
    }

    private void FocusSidebarSearch()
    {
        if (_shell is { IsSidebarDocked: false })
        {
            _shell.IsSidebarSheetOpen = true;
        }
        if (IsSidebarCollapsed)
        {
            IsSidebarCollapsed = false;
        }
        _sidebar?.FocusSearch();
    }

    private void FocusFormTitle() => Dispatcher.UIThread.Post(() => _dayPanel?.FocusTitle(), DispatcherPriority.Input);

    internal MonthPane Pane => _pane;

    internal MonthCalendarEvent? OpenedEvent => _openEvent;

    internal MonthDraft? Draft => _draft;

    internal string SidebarSearch
    {
        get => _sidebarSearch;
        set => _sidebarSearch = value;
    }

    internal ScreenShell? Shell => _shell;

    internal MonthMonthView? MonthView => _monthView;

    internal MonthDayPanel? DayPanel => _dayPanel;

    internal MonthSidebar? SidebarPart => _sidebar;

    internal Control? BodyContent => _bodyContent;

    // Shortcuts --------------------------------------------------------------------------------------------------------

    internal void ShowShortcuts()
    {
        if (_shell is null)
        {
            return;
        }
        var paint = Paint;
        var l = Labels;
        var close = ScreenButtons.Icon(paint, ArcIcons.X, l.Close, () => _shell.CloseDialog());
        var head = new DockPanel { Margin = new Thickness(0, 0, 0, 6), Height = 32 };
        DockPanel.SetDock(close, Dock.Right);
        head.Children.Add(close);
        head.Children.Add(ScreenText.Title(this, l.KeyboardShortcuts, 15, 16.5, -0.45, paint.ForegroundBrush));
        var rows = new StackPanel();
        (string Label, string Key)[] items =
        [
            (l.ShortcutMoveDays, l.ArrowKeys),
            (l.ShortcutOpenDay, "↵"),
            (l.ShortcutMonths, l.PageKeys),
            (l.ShortcutToday, "T"),
            (l.ShortcutNewEvent, "N"),
            (l.ShortcutFilter, "/"),
            (l.ShortcutMoveEvent, l.AltArrows),
            (l.ShortcutDeleteEvent, "⌫"),
            (l.ShortcutBack, "Esc"),
            (l.ShortcutSearch, OperatingSystem.IsMacOS() ? "⌘K" : "Ctrl K"),
        ];
        foreach (var (label, key) in items)
        {
            var row = new DockPanel
            {
                Height = 37,
            };
            var kbd = new ScreenKbd(key);
            DockPanel.SetDock(kbd, Dock.Right);
            row.Children.Add(kbd);
            row.Children.Add(ScreenText.Line(label, 13, paint.SecondaryBrush));
            rows.Children.Add(new Border
            {
                BorderBrush = paint.SubtleBrush,
                BorderThickness = new Thickness(0, 1, 0, 0),
                Child = row,
            });
        }
        var stack = new StackPanel();
        stack.Children.Add(head);
        stack.Children.Add(rows);
        var card = new Border
        {
            Width = 420,
            Padding = new Thickness(18, 14, 18, 10),
            Background = paint.IsDark ? paint.RaisedBrush : paint.SurfaceBrush,
            BorderBrush = paint.BorderBrush,
            BorderThickness = new Thickness(1),
            BoxShadow = new BoxShadows(new BoxShadow { OffsetY = 22, Blur = 55, Color = Color.FromArgb((byte)(paint.IsDark ? 87 : 27), 0, 0, 0) }),
            Child = stack,
        }.BindCornerRadius(DesignTokens.ArcDesignTokenKind.RadiusStep16);
        AutomationProperties.SetName(card, l.KeyboardShortcuts);
        _shell.ShowDialog(card);
        Dispatcher.UIThread.Post(() => close.Focus(NavigationMethod.Tab), DispatcherPriority.Input);
    }
}

/// <summary>
/// The month title in the breadcrumbs: on a month change the old title fades out where it is while the new one fades in
/// and slides 6 px up (forward) or down (back), the grid's curve (Arc: the title crossfades with the month).
/// </summary>
internal sealed class MonthTitleSwap : Panel
{
    private readonly MonthCalendar _owner;
    private TextBlock? _current;
    private DateOnly _month;
    private bool _narrow;

    public MonthTitleSwap(MonthCalendar owner)
    {
        _owner = owner;
        ClipToBounds = false;
        VerticalAlignment = VerticalAlignment.Center;
    }

    public bool IsNarrow => _narrow;

    public void Show(DateOnly month, bool narrow, int direction = 0)
    {
        if (_current is not null && month == _month && narrow == _narrow && direction == 0)
        {
            _current.Foreground = _owner.Paint.ForegroundBrush;
            return;
        }
        _month = month;
        _narrow = narrow;
        var l = _owner.Labels;
        var text = MonthCalendarModel.Format(month, narrow ? l.MonthTitleNarrowFormat : l.MonthTitleFormat, _owner.EffectiveCulture);
        var block = ScreenText.Line(text, 13, _owner.Paint.ForegroundBrush, FontWeight.Medium);
        var old = _current;
        _current = block;
        Children.Add(block);
        if (old is null || direction == 0 || !Motion.ArcMotion.IsFull(this))
        {
            if (old is not null)
            {
                Children.Remove(old);
            }
            return;
        }
        old.IsHitTestVisible = false;
        var oldFade = new Motion.MotionValue<double>(old, 1, Motion.MotionVectors.Double);
        oldFade.Changed += v => old.Opacity = v;
        _ = FadeOut(oldFade, old);
        var shift = new TranslateTransform(0, 6 * direction);
        block.RenderTransform = shift;
        block.Opacity = 0;
        var enter = new Motion.MotionValue<double>(block, 0, Motion.MotionVectors.Double);
        enter.Changed += v =>
        {
            block.Opacity = v;
            shift.Y = (1 - v) * 6 * direction;
        };
        _ = enter.AnimateTo(1, Motion.MotionSpec.Tween(TimeSpan.FromMilliseconds(180), Motion.ArcEasings.Enter));
    }

    private async Task FadeOut(Motion.MotionValue<double> fade, Control old)
    {
        await fade.AnimateTo(0, Motion.MotionSpec.Tween(TimeSpan.FromMilliseconds(150), Motion.ArcEasings.InOut));
        Children.Remove(old);
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        foreach (var c in Children)
        {
            c.Measure(availableSize);
        }
        return _current?.DesiredSize ?? default;
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        foreach (var c in Children)
        {
            c.Arrange(new Rect(0, 0, Math.Max(finalSize.Width, c.DesiredSize.Width), finalSize.Height));
        }
        return finalSize;
    }
}
