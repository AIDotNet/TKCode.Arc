using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;

namespace TKCode.Arc;

/// <summary>Template parts, the toolbar, the breakpoints and the grid height of <see cref="WeekCalendar"/>.</summary>
public partial class WeekCalendar
{
    private WeekCalendarToolbar? _toolbar;
    private Button? _todayButton;
    private Button? _previousButton;
    private Button? _nextButton;
    private TextBlock? _titleText;
    private SegmentedControl? _views;
    private Popover? _calendarsPopover;
    private Button? _calendarsButton;
    private WeekCalendarSwatches? _swatches;
    private TextBlock? _calendarsLabel;
    private Button? _newEventButton;
    private TextBlock? _newEventLabel;
    private Control? _sidebar;
    private TextBlock? _calendarsHeading;
    private Control? _calendarsSection;
    private WeekCalendarMiniMonth? _miniMonth;
    private WeekCalendarSourceList? _sidebarList;
    private WeekCalendarSourceList? _popoverList;
    private Panel? _main;
    private ProjectsAnnouncer? _announcer;
    private WeekCalendarGridView? _gridView;
    private WeekCalendarAgendaView? _agendaView;
    private TopLevel? _topLevel;
    private bool _updatingViews;
    private bool _sidebarShown;
    private MotionValue<double>? _viewFade;

    /// <summary>The grid view (header, all-day row, hours) once the template is applied.</summary>
    internal WeekCalendarGridView? GridView => _gridView;

    /// <summary>The agenda once the template is applied.</summary>
    internal WeekCalendarAgendaView? AgendaView => _agendaView;

    /// <summary>The live region (tests read its history).</summary>
    internal ProjectsAnnouncer? Announcer => _announcer;

    /// <summary>The month in the sidebar.</summary>
    internal WeekCalendarMiniMonth? MiniMonth => _miniMonth;

    /// <summary>The Calendars popover of narrower calendars.</summary>
    internal Popover? CalendarsPopover => _calendarsPopover;

    /// <summary>True while the sidebar shows.</summary>
    internal bool IsSidebarShown => _sidebarShown;

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        FlushData();
        Unhook();
        var scope = e.NameScope;
        _toolbar = scope.Find<WeekCalendarToolbar>(ToolbarPart);
        _todayButton = scope.Find<Button>(TodayPart);
        _previousButton = scope.Find<Button>(PreviousPart);
        _nextButton = scope.Find<Button>(NextPart);
        _titleText = scope.Find<TextBlock>(TitlePart);
        _views = scope.Find<SegmentedControl>(ViewsPart);
        _calendarsPopover = scope.Find<Popover>(CalendarsPopoverPart);
        _calendarsButton = scope.Find<Button>(CalendarsButtonPart);
        _swatches = scope.Find<WeekCalendarSwatches>("PART_Swatches");
        _calendarsLabel = scope.Find<TextBlock>("PART_CalendarsLabel");
        _newEventButton = scope.Find<Button>(NewEventPart);
        _newEventLabel = scope.Find<TextBlock>("PART_NewEventLabel");
        _sidebar = scope.Find<Control>(SidebarPart);
        _calendarsHeading = scope.Find<TextBlock>("PART_CalendarsHeading");
        _calendarsSection = scope.Find<Control>("PART_CalendarsSection");
        _miniMonth = scope.Find<WeekCalendarMiniMonth>(MiniMonthPart);
        _sidebarList = scope.Find<WeekCalendarSourceList>(SidebarListPart);
        _popoverList = scope.Find<WeekCalendarSourceList>(PopoverListPart);
        _main = scope.Find<Panel>(MainPart);
        _announcer = scope.Find<ProjectsAnnouncer>(AnnouncerPart);
        ApplyEditingTemplate(scope);

        if (_todayButton is not null)
        {
            _todayButton.Click += OnTodayClick;
        }
        if (_previousButton is not null)
        {
            _previousButton.Click += OnPreviousClick;
        }
        if (_nextButton is not null)
        {
            _nextButton.Click += OnNextClick;
        }
        if (_newEventButton is not null)
        {
            _newEventButton.Click += OnNewEventClick;
        }
        if (_views is not null)
        {
            _views.SelectionChanged += OnViewsSelectionChanged;
        }
        _miniMonth?.Attach(this);
        _sidebarList?.Attach(this);
        _popoverList?.Attach(this);
        _swatches?.Attach(this);
        if (_main is not null)
        {
            _gridView ??= new WeekCalendarGridView(this);
            _agendaView ??= new WeekCalendarAgendaView(this);
            _main.Children.Clear();
            _main.Children.Add(_gridView);
            _main.Children.Add(_agendaView);
            var agenda = EffectiveView == WeekCalendarView.Agenda;
            _gridView.IsVisible = !agenda;
            _agendaView.IsVisible = agenda;
            _gridView.Opacity = 1;
            _agendaView.Opacity = 1;
            _gridView.Show(_range, Date, WeekCalendarTransition.None, 0);
            _agendaView.Show(_range, WeekCalendarTransition.None, 0);
        }
        UpdateTexts();
        UpdateViewOptions();
        UpdateRangeState();
        UpdateBreakpoints(Bounds.Width);
        UpdateGridHeight();
        OnDataRebuilt();
    }

    private void Unhook()
    {
        if (_todayButton is not null)
        {
            _todayButton.Click -= OnTodayClick;
        }
        if (_previousButton is not null)
        {
            _previousButton.Click -= OnPreviousClick;
        }
        if (_nextButton is not null)
        {
            _nextButton.Click -= OnNextClick;
        }
        if (_newEventButton is not null)
        {
            _newEventButton.Click -= OnNewEventClick;
        }
        if (_views is not null)
        {
            _views.SelectionChanged -= OnViewsSelectionChanged;
        }
        _main?.Children.Clear();
    }

    private void OnTodayClick(object? sender, RoutedEventArgs e) => GoToToday();

    private void OnPreviousClick(object? sender, RoutedEventArgs e) => Previous();

    private void OnNextClick(object? sender, RoutedEventArgs e) => Next();

    private void OnNewEventClick(object? sender, RoutedEventArgs e)
    {
        // New event drafts at the next free hour of the focused day unless the app handled the request.
        if (!RequestNewEvent().Handled)
        {
            BeginCreate(Date);
        }
    }

    /// <summary>
    /// New event: raises <see cref="NewEventRequested"/> for the focused day and runs <see cref="NewEventCommand"/>; the
    /// button then drafts an event unless the request was handled.
    /// </summary>
    internal WeekCalendarNewEventRequestedEventArgs RequestNewEvent()
    {
        var args = new WeekCalendarNewEventRequestedEventArgs(NewEventRequestedEvent, Date);
        RaiseEvent(args);
        if (NewEventCommand is { } command && command.CanExecute(Date))
        {
            command.Execute(Date);
        }
        return args;
    }

    private void OnViewsSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_updatingViews || _views?.SelectedValue is not WeekCalendarView view)
        {
            return;
        }
        if (view != EffectiveView)
        {
            SetCurrentValue(ViewProperty, view);
        }
    }

    /// <summary>The view switch offers Week / Work week / Day, or 3 days / Day / Agenda when narrow; the shown view is selected.</summary>
    private void UpdateViewOptions()
    {
        if (_views is null)
        {
            return;
        }
        _updatingViews = true;
        try
        {
            var offered = WeekCalendarRange.Offered(_compact);
            var current = _views.ItemsSource as IReadOnlyList<SegmentedOption>;
            var same = current is not null && current.Count == offered.Length;
            if (same)
            {
                for (var i = 0; i < offered.Length; i++)
                {
                    if (!Equals(current![i].Value, offered[i]) || current[i].Label != ViewName(offered[i]))
                    {
                        same = false;
                        break;
                    }
                }
            }
            if (!same)
            {
                _views.ItemsSource = offered.Select(v => new SegmentedOption(v, ViewName(v))).ToList();
            }
            _views.SelectedValue = EffectiveView;
            AutomationProperties.SetName(_views, Labels.ViewSwitch);
        }
        finally
        {
            _updatingViews = false;
        }
    }

    private void UpdateTexts()
    {
        var labels = Labels;
        if (_todayButton is not null)
        {
            _todayButton.Content = labels.Today;
            AutomationProperties.SetAcceleratorKey(_todayButton, "T");
        }
        if (_newEventButton is not null)
        {
            AutomationProperties.SetName(_newEventButton, labels.NewEvent);
        }
        if (_newEventLabel is not null)
        {
            _newEventLabel.Text = labels.NewEvent;
        }
        if (_calendarsLabel is not null)
        {
            _calendarsLabel.Text = labels.Calendars;
        }
        if (_calendarsButton is not null)
        {
            AutomationProperties.SetName(_calendarsButton, labels.Calendars);
        }
        if (_calendarsHeading is not null)
        {
            _calendarsHeading.Text = labels.Calendars;
        }
        if (_sidebar is not null)
        {
            AutomationProperties.SetName(_sidebar, labels.Sidebar);
        }
        if (_sidebarList is not null)
        {
            AutomationProperties.SetName(_sidebarList, labels.Calendars);
        }
        if (_popoverList is not null)
        {
            AutomationProperties.SetName(_popoverList, labels.Calendars);
        }
        UpdateStepNames();
    }

    private void UpdateStepNames()
    {
        var labels = Labels;
        var (previous, next) = EffectiveView switch
        {
            WeekCalendarView.Day => (labels.PreviousDay, labels.NextDay),
            WeekCalendarView.ThreeDay => (labels.PreviousThreeDays, labels.NextThreeDays),
            _ => (labels.PreviousWeek, labels.NextWeek),
        };
        if (_previousButton is not null)
        {
            AutomationProperties.SetName(_previousButton, previous);
            Tooltip.SetTip(_previousButton, previous);
        }
        if (_nextButton is not null)
        {
            AutomationProperties.SetName(_nextButton, next);
            Tooltip.SetTip(_nextButton, next);
        }
    }

    // Breakpoints --------------------------------------------------------------------------------------------------------

    protected override void OnSizeChanged(SizeChangedEventArgs e)
    {
        base.OnSizeChanged(e);
        UpdateBreakpoints(e.NewSize.Width);
    }

    private void UpdateBreakpoints(double width)
    {
        if (!(width > 0))
        {
            return;
        }
        var wide = width >= WeekCalendarMetrics.WideWidth;
        var compact = width < WeekCalendarMetrics.CompactWidth;
        PseudoClasses.Set(WidePseudoClass, wide);
        PseudoClasses.Set(CompactPseudoClass, compact);
        PseudoClasses.Set(RegularPseudoClass, !wide && !compact);
        PseudoClasses.Set(TightPseudoClass, width < WeekCalendarMetrics.TightWidth);
        var swatchOnly = width < WeekCalendarMetrics.SwatchOnlyWidth;
        PseudoClasses.Set(SwatchOnlyPseudoClass, swatchOnly);
        if (_calendarsLabel is not null)
        {
            // Set here: the label sits in the popover's trigger, which the theme's template selectors do not reach.
            _calendarsLabel.IsVisible = !swatchOnly;
        }
        var stacked = width < WeekCalendarMetrics.StackedWidth;
        PseudoClasses.Set(StackedPseudoClass, stacked);
        if (_toolbar is not null)
        {
            _toolbar.IsStacked = stacked;
        }
        _gridView?.SetStacked(stacked, width < WeekCalendarMetrics.TightWidth);
        UpdateSidebar(wide);
        if (compact != _compact)
        {
            _compact = compact;
            ApplyEffectiveView(WeekCalendarRangeChangeReason.Resize, animate: false);
            UpdateViewOptions();
        }
    }

    private void UpdateSidebar(bool wide)
    {
        var shown = IsSidebarVisible ?? wide;
        _sidebarShown = shown;
        PseudoClasses.Set(SidebarPseudoClass, shown);
        if (_sidebar is not null)
        {
            _sidebar.IsVisible = shown;
        }
        if (shown && _calendarsPopover?.IsOpen == true)
        {
            _calendarsPopover.Close();
        }
        UpdateCalendarsVisibility();
    }

    private void UpdateCalendarsVisibility()
    {
        var any = _calendars.Count > 0;
        if (_calendarsPopover is not null)
        {
            _calendarsPopover.IsVisible = any && !_sidebarShown;
        }
        if (_calendarsSection is not null)
        {
            _calendarsSection.IsVisible = any;
        }
    }

    // Grid height -------------------------------------------------------------------------------------------------------

    /// <summary>The height of the grid: <see cref="GridHeight"/>, or 72% of the window between 480 and 760.</summary>
    internal double EffectiveGridHeight
    {
        get
        {
            if (GridHeight > 0 && double.IsFinite(GridHeight))
            {
                return GridHeight;
            }
            return WeekCalendarMetrics.GridHeightFor(_topLevel?.ClientSize.Height ?? double.NaN);
        }
    }

    private void UpdateGridHeight()
    {
        if (_main is null)
        {
            return;
        }
        var height = EffectiveGridHeight;
        if (double.IsNaN(_main.Height) || Math.Abs(_main.Height - height) > 0.01)
        {
            _main.Height = height;
        }
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _topLevel = TopLevel.GetTopLevel(this);
        if (_topLevel is not null)
        {
            _topLevel.PropertyChanged += OnTopLevelPropertyChanged;
        }
        UpdateGridHeight();
        UpdateMinuteTimer();
        ArcToday.Changed += OnTodayChanged;
        ActualThemeVariantChanged += OnThemeChanged;
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        if (_topLevel is not null)
        {
            _topLevel.PropertyChanged -= OnTopLevelPropertyChanged;
            _topLevel = null;
        }
        _minuteTimer?.Stop();
        ArcToday.Changed -= OnTodayChanged;
        ActualThemeVariantChanged -= OnThemeChanged;
        OnEditingDetached();
    }

    private void OnTopLevelPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == TopLevel.ClientSizeProperty)
        {
            UpdateGridHeight();
        }
    }

    private void OnTodayChanged(object? sender, EventArgs e) => Dispatcher.UIThread.Post(OnNowTick);

    private void OnThemeChanged(object? sender, EventArgs e) => OnThemeResourcesChanged();

    /// <summary>The theme or accent changed: drop every cached brush and redraw.</summary>
    internal void OnThemeResourcesChanged()
    {
        Paint.Invalidate();
        _gridView?.OnThemeChanged();
        _agendaView?.OnThemeChanged();
        _miniMonth?.OnThemeChanged();
        _sidebarList?.Refresh();
        _popoverList?.Refresh();
        _swatches?.Refresh();
        _editor?.OnThemeChanged();
    }

    // Grid and agenda swap ---------------------------------------------------------------------------------------------------

    /// <summary>Crossfades between the time grid and the agenda (the old one 160ms out, the new one 240ms in).</summary>
    private void SwapViews(bool toAgenda, bool animate)
    {
        if (_gridView is null || _agendaView is null)
        {
            return;
        }
        Control incoming = toAgenda ? _agendaView : _gridView;
        Control outgoing = toAgenda ? _gridView : _agendaView;
        if (!animate || ArcMotion.GetEffectiveLevel(this) == ArcMotionLevel.Off || VisualRoot is null)
        {
            outgoing.IsVisible = false;
            outgoing.Opacity = 1;
            incoming.IsVisible = true;
            incoming.Opacity = 1;
            return;
        }
        incoming.IsVisible = true;
        incoming.Opacity = 0;
        outgoing.IsHitTestVisible = false;
        incoming.IsHitTestVisible = true;
        _ = FadeOutAsync(outgoing);
        // The view crossfade is driven in code: two hosts trade places only for its length, and a later swap reverses it.
        _viewFade?.Dispose();
        _viewFade = new MotionValue<double>(this, 0, MotionVectors.Double);
        _viewFade.Changed += v => incoming.Opacity = v;
        _ = _viewFade.AnimateTo(1, WeekCalendarMotion.FadeIn());
    }

    private async Task FadeOutAsync(Control outgoing)
    {
        var fade = new MotionValue<double>(this, outgoing.Opacity, MotionVectors.Double);
        fade.Changed += v => outgoing.Opacity = v;
        var done = await fade.AnimateTo(0, WeekCalendarMotion.FadeOut());
        fade.Dispose();
        if (done && outgoing.Opacity < 0.01)
        {
            var stillOut = ReferenceEquals(outgoing, _gridView)
                ? EffectiveView == WeekCalendarView.Agenda
                : EffectiveView != WeekCalendarView.Agenda;
            if (stillOut)
            {
                outgoing.IsVisible = false;
                outgoing.Opacity = 1;
                outgoing.IsHitTestVisible = true;
            }
        }
    }

    // Property dispatch -------------------------------------------------------------------------------------------------

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        var property = change.Property;
        if (property == EventsProperty || property == EventsSourceProperty || property == CalendarsProperty ||
            property == CalendarsSourceProperty)
        {
            OnEventsPropertyChanged(change);
        }
        else if (property == DateProperty)
        {
            OnDatePropertyChanged();
        }
        else if (property == ViewProperty)
        {
            OnViewPropertyChanged(change.GetOldValue<WeekCalendarView>(), change.GetNewValue<WeekCalendarView>());
        }
        else if (property == FirstDayOfWeekProperty)
        {
            var range = WeekCalendarRange.For(EffectiveView, Date, FirstDayOfWeek);
            Navigate(range, Date, WeekCalendarRangeChangeReason.Programmatic, animate: false);
            _miniMonth?.Refresh();
        }
        else if (property == TimeZoneProperty || property == CultureProperty)
        {
            OnZoneOrCultureChanged();
        }
        else if (property == NowProperty)
        {
            OnNowPropertyChanged();
        }
        else if (property == IsSidebarVisibleProperty)
        {
            UpdateSidebar(Bounds.Width >= WeekCalendarMetrics.WideWidth);
        }
        else if (property == GridHeightProperty)
        {
            UpdateGridHeight();
        }
        else if (property == ShowNowIndicatorProperty || property == ShowWeekendShadingProperty)
        {
            _gridView?.Refresh();
        }
        else if (property == AllDayLaneLimitProperty || property == IsAllDayExpandedProperty)
        {
            _gridView?.OnAllDayLayoutChanged(property == IsAllDayExpandedProperty);
        }
        else if (property == LabelsProperty)
        {
            UpdateTexts();
            UpdateViewOptions();
            _gridView?.Refresh();
            _agendaView?.Refresh();
            _miniMonth?.Refresh();
        }
        OnEditingPropertyChanged(change);
    }
}
