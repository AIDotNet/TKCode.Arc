using System.Globalization;
using TKCode.Arc.Motion;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using AvaloniaButton = Avalonia.Controls.Button;

namespace TKCode.Arc;

/// <summary>A preset in the rail; <c>:active</c> while the draft equals its range (Arc: aria-pressed).</summary>
[PseudoClasses(ActivePseudoClass)]
internal sealed class DateRangePickerPresetButton : AvaloniaButton
{
    public const string ActivePseudoClass = ":active";

    protected override Type StyleKeyOverride => typeof(DateRangePickerPresetButton);

    public int Index { get; set; }

    public void SetActive(bool active) => PseudoClasses.Set(ActivePseudoClass, active);
}

/// <summary>The preset highlight: one rounded fill that glides between presets (GLIDE spring) and fades out when none matches.</summary>
internal sealed class DateRangePickerRailHighlight : Control
{
    public static readonly StyledProperty<IBrush?> FillProperty =
        AvaloniaProperty.Register<DateRangePickerRailHighlight, IBrush?>(nameof(Fill));

    public IBrush? Fill { get => GetValue(FillProperty); set => SetValue(FillProperty, value); }

    private MotionValue<Rect>? _rect;
    private MotionValue<double>? _opacity;

    static DateRangePickerRailHighlight()
    {
        IsHitTestVisibleProperty.OverrideDefaultValue<DateRangePickerRailHighlight>(false);
        AffectsRender<DateRangePickerRailHighlight>(FillProperty);
    }

    public Rect Target => _rect?.Target ?? default;

    public double Visibility => _opacity?.Value ?? 0;

    public void MoveTo(Rect rect, bool animate)
    {
        _rect ??= Create(rect, MotionVectors.Rect);
        _opacity ??= Create(0.0, MotionVectors.Double);
        var motion = animate && IsLoaded && ArcMotion.IsFull(this);
        if (_opacity.Value < 0.05 || !motion)
        {
            _rect.Jump(rect);
        }
        else if (_rect.Target != rect)
        {
            _ = _rect.AnimateTo(rect, MotionSpec.Spring(DateRangePickerMonthPane.Glide));
        }
        if (motion)
        {
            _ = _opacity.AnimateTo(1, MotionSpec.Tween(TimeSpan.FromMilliseconds(160), ArcEasings.Standard));
        }
        else
        {
            _opacity.Jump(1);
        }
    }

    public void Hide(bool animate)
    {
        _opacity ??= Create(0.0, MotionVectors.Double);
        if (animate && IsLoaded && ArcMotion.IsFull(this))
        {
            _ = _opacity.AnimateTo(0, MotionSpec.Tween(TimeSpan.FromMilliseconds(140), ArcEasings.Standard));
        }
        else
        {
            _opacity.Jump(0);
        }
    }

    private MotionValue<T> Create<T>(T initial, IMotionVector<T> vector) where T : struct
    {
        var value = new MotionValue<T>(this, initial, vector);
        value.Changed += _ => InvalidateVisual();
        return value;
    }

    public override void Render(DrawingContext context)
    {
        if (_rect is null || _opacity is null || _opacity.Value <= 0.001 || Fill is not { } fill)
        {
            return;
        }
        using (context.PushOpacity(Math.Clamp(_opacity.Value, 0, 1)))
        {
            context.DrawRectangle(fill, null, new RoundedRect(_rect.Value, 12));
        }
    }
}

/// <summary>
/// The DateRangePicker panel: presets (a vertical rail beside two months, or a scrolling row above one month), the
/// months with their range highlight, and the footer (the summary slot the flying value lands in, the day count,
/// Cancel and Apply). It owns the draft: picking a start anchors the range, hovering or moving with the keyboard
/// previews it, and the second pick completes it; nothing is committed until Apply.
/// </summary>
[TemplatePart("PART_Rail", typeof(StackPanel))]
[PseudoClasses(CompactPseudoClass)]
internal sealed class DateRangePickerPanel : TemplatedControl, IDateRangePickerMonthHost
{
    public const string CompactPseudoClass = ":compact";

    private readonly List<DateRangePickerPresetButton> _presetButtons = new();
    private DateRangePickerMonthsView? _months;
    private StackPanel? _rail;
    private ScrollViewer? _railScroll;
    private DateRangePickerRailHighlight? _railHighlight;
    private Decorator? _monthsHost;
    private Control? _calendars;
    private AvaloniaButton? _previous;
    private AvaloniaButton? _next;
    private AvaloniaButton? _cancel;
    private AvaloniaButton? _apply;
    private TextBlock? _live;
    private int _activePreset = -1;
    private bool _compact;

    public DateRangePickerPanel(DateRangePicker owner)
    {
        Owner = owner;
    }

    public DateRangePicker Owner { get; }

    public DateRangePickerPalette Palette { get; } = new();

    public DateRange? Draft { get; private set; }
    public DateOnly? Anchor { get; private set; }
    public DateOnly? HoverDate { get; private set; }
    public DateOnly View { get; private set; }
    public DateOnly? FocusDate { get; private set; }
    public DateOnly Today { get; private set; }
    public bool IsCompact => _compact;
    public int MonthCount => _compact ? 1 : 2;

    public DateRangePickerMonthsView? Months => _months;
    public Control? Body { get; private set; }
    public Control? Actions { get; private set; }
    public PickerRollText? CountText { get; private set; }
    public Control? SummarySlot { get; private set; }
    public AvaloniaButton? ApplyButton => _apply;
    public AvaloniaButton? CancelButton => _cancel;
    public AvaloniaButton? PreviousButton => _previous;
    public AvaloniaButton? NextButton => _next;
    public IReadOnlyList<DateRangePickerPresetButton> PresetButtons => _presetButtons;
    public int ActivePreset => _activePreset;

    /// <summary>What the footer shows: the anchored preview while picking, else the draft.</summary>
    public DateRange? Shown => Anchor is { } a ? DateRange.Ordered(a, HoverDate ?? a) : Draft;

    /// <summary>The draft or preview changed (the owner rolls the flying summary).</summary>
    public event EventHandler? ShownChanged;

    public event EventHandler? ApplyRequested;

    public event EventHandler? CancelRequested;

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _rail = e.NameScope.Find<StackPanel>("PART_Rail");
        _railScroll = e.NameScope.Find<ScrollViewer>("PART_RailScroll");
        _railHighlight = e.NameScope.Find<DateRangePickerRailHighlight>("PART_RailHighlight");
        _monthsHost = e.NameScope.Find<Decorator>("PART_MonthsHost");
        _calendars = e.NameScope.Find<Control>("PART_Calendars");
        _previous = e.NameScope.Find<AvaloniaButton>("PART_Previous");
        _next = e.NameScope.Find<AvaloniaButton>("PART_Next");
        _cancel = e.NameScope.Find<AvaloniaButton>("PART_Cancel");
        _apply = e.NameScope.Find<AvaloniaButton>("PART_Apply");
        _live = e.NameScope.Find<TextBlock>("PART_Live");
        Body = e.NameScope.Find<Control>("PART_Body");
        Actions = e.NameScope.Find<Control>("PART_Actions");
        CountText = e.NameScope.Find<PickerRollText>("PART_Count");
        SummarySlot = e.NameScope.Find<Control>("PART_SummarySlot");
        _months = new DateRangePickerMonthsView(this);
        if (_monthsHost is not null)
        {
            _monthsHost.Child = _months;
        }
        if (_previous is not null)
        {
            _previous.Click += (_, _) => GoTo(CalendarMath.AddMonths(View, -1));
        }
        if (_next is not null)
        {
            _next.Click += (_, _) => GoTo(CalendarMath.AddMonths(View, 1));
        }
        if (_cancel is not null)
        {
            _cancel.Click += (_, _) => CancelRequested?.Invoke(this, EventArgs.Empty);
        }
        if (_apply is not null)
        {
            _apply.Click += (_, _) => ApplyRequested?.Invoke(this, EventArgs.Empty);
        }
        if (_calendars is not null)
        {
            _calendars.PointerExited += (_, _) =>
            {
                if (Anchor is not null && HoverDate is not null)
                {
                    HoverDate = null;
                    Refresh(animate: true);
                }
            };
        }
        if (_rail is not null)
        {
            _rail.LayoutUpdated += (_, _) => SyncRailHighlight(animate: false, onlyIfIdle: true);
            _rail.KeyDown += OnRailKeyDown;
        }
    }

    /// <summary>Starts a session: the draft is the applied range; the view shows its last month on the right.</summary>
    public void Begin(DateRange? committed, DateOnly today, bool compact, double width, double cell)
    {
        Today = today;
        SetLayout(compact, width, cell);
        Draft = committed;
        Anchor = null;
        HoverDate = null;
        View = ViewFor(committed, compact, today);
        FocusDate = committed?.Start ?? today;
        BuildPresets();
        _begun = true;
        _shownKey = committed is { } c ? c.Start.DayNumber * 2L + c.End.DayNumber : null;
        _railHighlight?.Hide(animate: false);
        Refresh(animate: false, direction: 0);
    }

    /// <summary>Applies a new layout (window resize): one month and a preset row below 712px, two months otherwise.</summary>
    public void SetLayout(bool compact, double width, double cell)
    {
        var changed = compact != _compact;
        _compact = compact;
        PseudoClasses.Set(CompactPseudoClass, compact);
        Width = compact ? width : double.NaN;
        if (_rail is not null)
        {
            _rail.Orientation = compact ? Orientation.Horizontal : Orientation.Vertical;
        }
        if (_railScroll is not null)
        {
            _railScroll.HorizontalScrollBarVisibility = compact ? ScrollBarVisibility.Hidden : ScrollBarVisibility.Disabled;
        }
        _months?.SetLayout(compact ? 1 : 2, cell);
        if (changed)
        {
            InvalidateMeasure();
        }
    }

    public void SetToday(DateOnly today)
    {
        Today = today;
        Refresh(animate: false, direction: 0);
    }

    public static DateOnly ViewFor(DateRange? range, bool single, DateOnly fallback)
    {
        var end = CalendarMath.MonthStart(range?.End ?? fallback);
        return single ? end : CalendarMath.AddMonths(end, -1);
    }

    private static int MonthDiff(DateOnly a, DateOnly b) => (a.Year - b.Year) * 12 + a.Month - b.Month;

    private void BuildPresets()
    {
        if (_rail is null)
        {
            return;
        }
        var presets = Owner.EffectivePresets;
        while (_presetButtons.Count < presets.Count)
        {
            var button = new DateRangePickerPresetButton();
            button.Click += OnPresetClick;
            _presetButtons.Add(button);
            _rail.Children.Add(button);
        }
        for (var i = 0; i < _presetButtons.Count; i++)
        {
            var button = _presetButtons[i];
            button.IsVisible = i < presets.Count;
            if (i < presets.Count)
            {
                button.Index = i;
                button.Content = presets[i].Label;
            }
        }
    }

    private void OnPresetClick(object? sender, RoutedEventArgs e)
    {
        if (sender is DateRangePickerPresetButton button && button.Index < Owner.EffectivePresets.Count)
        {
            ChoosePreset(Owner.EffectivePresets[button.Index]);
        }
    }

    public void ChoosePreset(DateRangePreset preset)
    {
        var range = preset.Range(Today);
        Draft = range;
        Anchor = null;
        HoverDate = null;
        FocusDate = range.Start;
        Reveal(range);
        Refresh(animate: true);
    }

    /// <summary>Moves the view to <paramref name="next"/> (the first visible month), sliding toward it.</summary>
    public void GoTo(DateOnly next)
    {
        var delta = MonthDiff(next, View);
        if (delta == 0)
        {
            return;
        }
        View = CalendarMath.MonthStart(next);
        Refresh(animate: true, direction: Math.Sign(delta));
    }

    /// <summary>Keeps a range on screen: nothing moves when it already fits, otherwise its last month lands on the right.</summary>
    private void Reveal(DateRange range)
    {
        var last = CalendarMath.AddMonths(View, MonthCount - 1);
        if (MonthDiff(range.Start, View) >= 0 && MonthDiff(range.End, last) <= 0)
        {
            return;
        }
        GoTo(ViewFor(range, _compact, range.End));
    }

    public void Pick(DateOnly date)
    {
        FocusDate = date;
        if (Anchor is not { } anchor)
        {
            Anchor = date;
            HoverDate = date;
        }
        else
        {
            Draft = DateRange.Ordered(anchor, date);
            Anchor = null;
            HoverDate = null;
        }
        Refresh(animate: true);
    }

    void IDateRangePickerMonthHost.OnDayHovered(DateOnly date)
    {
        if (Anchor is not null && HoverDate != date)
        {
            HoverDate = date;
            Refresh(animate: true);
        }
    }

    void IDateRangePickerMonthHost.OnDayPicked(DateOnly date) => Pick(date);

    void IDateRangePickerMonthHost.OnDayFocused(DateOnly date) => FocusDate = date;

    /// <summary>The day that owns the tab stop: the focused day when on screen, else the range start, else the 1st.</summary>
    public DateOnly Tabbable
    {
        get
        {
            bool OnScreen(DateOnly d) => MonthDiff(d, View) >= 0 && MonthDiff(d, View) < MonthCount;
            if (FocusDate is { } f && OnScreen(f))
            {
                return f;
            }
            if (Shown is { } s && OnScreen(s.Start))
            {
                return s.Start;
            }
            return View;
        }
    }

    private int _pendingDirection;
    private long? _shownKey;

    /// <summary>Which way the shown range last moved (1 later, -1 earlier), so labels roll in time's direction.</summary>
    public int ShownDirection { get; private set; } = 1;

    private bool _begun;

    public void Refresh(bool animate, int direction = 0)
    {
        if (!_begun)
        {
            return;
        }
        if (direction != 0)
        {
            _pendingDirection = direction;
        }
        var owner = Owner;
        var culture = owner.EffectiveCulture;
        _months?.Show(View, _pendingDirection, owner.FirstDayOfWeek, Shown, Tabbable, Today, owner.MinDate, owner.MaxDate, culture, animate);
        _pendingDirection = 0;
        var presets = owner.EffectivePresets;
        _activePreset = -1;
        if (Anchor is null && Draft is { } draft)
        {
            for (var i = 0; i < presets.Count; i++)
            {
                if (presets[i].Range(Today) == draft)
                {
                    _activePreset = i;
                    break;
                }
            }
        }
        for (var i = 0; i < _presetButtons.Count; i++)
        {
            _presetButtons[i].SetActive(i == _activePreset);
        }
        SyncRailHighlight(animate, onlyIfIdle: false);
        if (_previous is not null)
        {
            _previous.IsEnabled = !(owner.MinDate is { } min && MonthDiff(View, min) <= 0);
            AutomationProperties.SetName(_previous, owner.PreviousMonthLabel);
        }
        if (_next is not null)
        {
            _next.IsEnabled = !(owner.MaxDate is { } max && MonthDiff(CalendarMath.AddMonths(View, MonthCount - 1), max) >= 0);
            AutomationProperties.SetName(_next, owner.NextMonthLabel);
        }
        if (_apply is not null)
        {
            _apply.IsEnabled = Shown is not null;
            _apply.Content = owner.ApplyText;
        }
        if (_cancel is not null)
        {
            _cancel.Content = owner.CancelText;
        }
        var shown = Shown;
        long? key = shown is { } k ? k.Start.DayNumber * 2L + k.End.DayNumber : null;
        if (key != _shownKey)
        {
            ShownDirection = key is null || _shownKey is null || key >= _shownKey ? 1 : -1;
            _shownKey = key;
        }
        var countText = shown is { } r
            ? Anchor is { } a && HoverDate == a ? owner.PickEndText : owner.FormatDays(r.Days)
            : string.Empty;
        CountText?.ShowWords(countText, placeholder: false, ShownDirection, animate);
        if (_live is not null)
        {
            _live.Text = Anchor is { } start
                ? $"Start {owner.FormatDate(start)}. Choose an end date."
                : shown is { } s ? $"{owner.FormatRange(s)}, {owner.FormatDays(s.Days)}" : string.Empty;
        }
        ShownChanged?.Invoke(this, EventArgs.Empty);
    }

    private void SyncRailHighlight(bool animate, bool onlyIfIdle)
    {
        if (_railHighlight is null)
        {
            return;
        }
        if (_activePreset < 0 || _activePreset >= _presetButtons.Count)
        {
            if (!onlyIfIdle)
            {
                _railHighlight.Hide(animate);
            }
            return;
        }
        var button = _presetButtons[_activePreset];
        if (button.Bounds.Width <= 0)
        {
            return;
        }
        var rect = button.Bounds;
        if (onlyIfIdle && rect == _railHighlight.Target && _railHighlight.Visibility > 0.5)
        {
            return;
        }
        _railHighlight.MoveTo(rect, animate && !onlyIfIdle);
        if (_compact && _railScroll is not null && !onlyIfIdle)
        {
            var offset = _railScroll.Offset.X;
            var viewport = _railScroll.Viewport.Width;
            if (rect.Left < offset || rect.Right > offset + viewport)
            {
                _railScroll.Offset = new Vector(Math.Max(0, rect.Left - 12), 0);
            }
        }
    }

    private void OnRailKeyDown(object? sender, KeyEventArgs e)
    {
        var back = _compact ? Key.Left : Key.Up;
        var forward = _compact ? Key.Right : Key.Down;
        if (e.Key != back && e.Key != forward)
        {
            return;
        }
        var visible = _presetButtons.Where(b => b.IsVisible).ToList();
        var at = visible.FindIndex(b => b.IsFocused);
        if (at < 0)
        {
            return;
        }
        e.Handled = true;
        var next = (at + (e.Key == forward ? 1 : -1) + visible.Count) % visible.Count;
        visible[next].Focus(NavigationMethod.Directional);
    }

    /// <summary>Arrows by day / week, Home / End to the week's ends, PageUp / PageDown by month (Shift: year).</summary>
    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Handled || e.Source is not DateRangePickerDayCell cell)
        {
            return;
        }
        var date = cell.Date;
        var shift = e.KeyModifiers.HasFlag(KeyModifiers.Shift);
        var column = CalendarMath.Column(date, Owner.FirstDayOfWeek);
        DateOnly? move = e.Key switch
        {
            Key.Left => date.AddDays(-1),
            Key.Right => date.AddDays(1),
            Key.Up => date.AddDays(-7),
            Key.Down => date.AddDays(7),
            Key.Home => date.AddDays(-column),
            Key.End => date.AddDays(6 - column),
            Key.PageUp => CalendarMath.ShiftMonths(date, shift ? -12 : -1),
            Key.PageDown => CalendarMath.ShiftMonths(date, shift ? 12 : 1),
            _ => null
        };
        if (move is not { } target)
        {
            return;
        }
        e.Handled = true;
        var next = Owner.MinDate is { } min && target < min ? min : Owner.MaxDate is { } max && target > max ? max : target;
        FocusDate = next;
        if (Anchor is not null)
        {
            HoverDate = next;
        }
        var last = CalendarMath.AddMonths(View, MonthCount - 1);
        if (MonthDiff(next, View) < 0)
        {
            GoTo(CalendarMath.MonthStart(next));
        }
        else if (MonthDiff(next, last) > 0)
        {
            GoTo(CalendarMath.AddMonths(CalendarMath.MonthStart(next), -(MonthCount - 1)));
        }
        else
        {
            Refresh(animate: true);
        }
        FocusDay(next);
    }

    public bool FocusDay(DateOnly date, NavigationMethod method = NavigationMethod.Directional)
    {
        var cell = _months?.FindCell(date);
        return cell is not null && cell.Focus(method);
    }

    /// <summary>Copies theme brushes into the palette the panes draw with.</summary>
    public void UpdatePalette(IBrush? accent, IBrush? accentSubtle, IBrush? accentForeground, IBrush? foreground, IBrush? muted,
                              IBrush? hover, FontFamily family, double fontSize, double smallSize, FontWeight medium)
    {
        Palette.Accent = accent ?? Brushes.Transparent;
        Palette.AccentSubtle = accentSubtle ?? Brushes.Transparent;
        Palette.AccentForeground = PickerDrawing.ColorOf(accentForeground);
        Palette.Foreground = PickerDrawing.ColorOf(foreground);
        Palette.Muted = PickerDrawing.ColorOf(muted);
        Palette.HoverFill = hover ?? Brushes.Transparent;
        Palette.FontFamily = family;
        Palette.FontSize = fontSize;
        Palette.SmallFontSize = smallSize;
        Palette.MediumWeight = medium;
        Palette.Version++;
        if (_months is not null)
        {
            Refresh(animate: false);
            foreach (var pane in _months.Panes)
            {
                pane.InvalidateVisual();
                foreach (var day in pane.DayCells)
                {
                    day.InvalidateVisual();
                }
            }
        }
    }
}
