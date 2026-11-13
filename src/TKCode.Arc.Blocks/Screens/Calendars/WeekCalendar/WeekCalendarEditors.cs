using TKCode.Arc.DesignTokens;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.VisualTree;
using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;
using AvaloniaRadioButton = Avalonia.Controls.RadioButton;

namespace TKCode.Arc;

/// <summary>
/// The layer over the whole calendar that holds its popovers and its undo toast. Arc keeps them inside the block (not in a
/// window popup), which is what makes its placement rules relative to the calendar and lets a press outside only close
/// them. Each child is placed by a function of its size and the layer's; the layer takes no room of its own.
/// </summary>
internal sealed class WeekCalendarEditorLayer : Panel
{
    private readonly Dictionary<Control, Func<Size, Size, Point>> _placements = new();

    /// <summary>Adds (or re-places) <paramref name="child"/>, placed by <paramref name="place"/>(child size, layer size).</summary>
    public void Show(Control child, Func<Size, Size, Point> place)
    {
        _placements[child] = place;
        if (!Children.Contains(child))
        {
            Children.Add(child);
        }
        InvalidateMeasure();
        InvalidateArrange();
    }

    /// <summary>Removes <paramref name="child"/>.</summary>
    public void Hide(Control child)
    {
        _placements.Remove(child);
        Children.Remove(child);
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        foreach (var child in Children)
        {
            child.Measure(Size.Infinity);
        }
        return default;
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        foreach (var child in Children)
        {
            var size = child.DesiredSize;
            var point = _placements.TryGetValue(child, out var place) ? place(size, finalSize) : default;
            child.Arrange(new Rect(point, size));
        }
        return finalSize;
    }
}

/// <summary>
/// The borderless title (and location) box of the popovers (notes B §3.5): the main package's <see cref="Input"/> without
/// its frame, 16px medium text that lines up with the rows under it, a muted fill only while focused.
/// </summary>
internal sealed class WeekCalendarTitleBox : Input
{
    protected override Type StyleKeyOverride => typeof(WeekCalendarTitleBox);
}

/// <summary>
/// One calendar in the popovers' calendar choice (notes B §3.5): a pill with the calendar's dot and name; the chosen one
/// takes the calendar's tint and a calendar-tinted border. A radio button, so screen readers hear the choice.
/// </summary>
internal sealed class WeekCalendarCalendarChip : AvaloniaRadioButton
{
    /// <summary>The calendar's dot.</summary>
    public static readonly StyledProperty<IBrush?> DotBrushProperty =
        AvaloniaProperty.Register<WeekCalendarCalendarChip, IBrush?>(nameof(DotBrush));

    /// <summary>The calendar's name.</summary>
    public static readonly StyledProperty<string?> LabelProperty =
        AvaloniaProperty.Register<WeekCalendarCalendarChip, string?>(nameof(Label));

    private IBrush? _restBorder;
    private IBrush? _restText;
    private IBrush? _chosenFill;
    private IBrush? _chosenBorder;
    private IBrush? _chosenText;

    public WeekCalendarCalendarChip(string calendarId)
    {
        CalendarId = calendarId;
    }

    protected override Type StyleKeyOverride => typeof(WeekCalendarCalendarChip);

    public string CalendarId { get; }

    public IBrush? DotBrush { get => GetValue(DotBrushProperty); set => SetValue(DotBrushProperty, value); }

    public string? Label { get => GetValue(LabelProperty); set => SetValue(LabelProperty, value); }

    /// <summary>Takes the colors of the calendar and the theme (rest and chosen).</summary>
    public void Paint(WeekCalendarPaint paint, WeekCalendarBrushes brushes)
    {
        DotBrush = brushes.Bar;
        _restBorder = paint.Border;
        _restText = paint.TextSecondary;
        _chosenFill = brushes.ChipFill;
        _chosenBorder = brushes.ChipBorder;
        _chosenText = paint.Foreground;
        UpdateColors();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IsCheckedProperty)
        {
            UpdateColors();
        }
    }

    // Colors are set here rather than in the theme: they depend on the calendar. The theme's transitions animate them.
    private void UpdateColors()
    {
        var chosen = IsChecked == true;
        Background = chosen ? _chosenFill : Brushes.Transparent;
        BorderBrush = chosen ? _chosenBorder : _restBorder;
        Foreground = chosen ? _chosenText : _restText;
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        // The arrow keys belong to the choice (one tab stop, arrows pick), not to the radio button.
        if (e.Key is Key.Left or Key.Right or Key.Up or Key.Down && Parent is WeekCalendarCalendarChoice choice)
        {
            e.Handled = choice.Step(this, e.Key is Key.Right or Key.Down ? 1 : -1);
            return;
        }
        base.OnKeyDown(e);
    }
}

/// <summary>
/// The calendar choice of the popovers: the calendars as pills that wrap, 6 apart. One tab stop (the chosen pill); the
/// arrow keys move the choice (Arc gives every pill its own tab stop; a radio group reads better).
/// </summary>
internal sealed class WeekCalendarCalendarChoice : Control
{
    private readonly List<WeekCalendarCalendarChip> _chips = new();
    private WeekCalendar? _owner;
    private bool _updating;

    public WeekCalendarCalendarChoice()
    {
        Focusable = false;
    }

    /// <summary>The space between pills (6).</summary>
    public double Gap { get; set; } = 6;

    /// <summary>The id of the chosen calendar.</summary>
    public string? SelectedId { get; private set; }

    /// <summary>The pills, in the calendars' order.</summary>
    public IReadOnlyList<WeekCalendarCalendarChip> Chips => _chips;

    /// <summary>Raised with the id of the calendar picked.</summary>
    public event Action<string>? Picked;

    /// <summary>Shows the calendars of <paramref name="owner"/> with <paramref name="selectedId"/> chosen.</summary>
    public void Bind(WeekCalendar owner, string? selectedId)
    {
        _owner = owner;
        foreach (var chip in _chips)
        {
            chip.IsCheckedChanged -= OnChipChecked;
            LogicalChildren.Remove(chip);
            VisualChildren.Remove(chip);
        }
        _chips.Clear();
        var paint = owner.Paint;
        paint.EnsureReady();
        foreach (var source in owner.EffectiveCalendars)
        {
            if (string.IsNullOrEmpty(source.Id))
            {
                continue;
            }
            var chip = new WeekCalendarCalendarChip(source.Id) { Label = source.Name, GroupName = null };
            AutomationProperties.SetName(chip, source.Name);
            chip.Paint(paint, paint.For(source.Id));
            chip.IsCheckedChanged += OnChipChecked;
            _chips.Add(chip);
            LogicalChildren.Add(chip);
            VisualChildren.Add(chip);
        }
        AutomationProperties.SetName(this, owner.Labels.CalendarChoice);
        Select(selectedId);
        InvalidateMeasure();
    }

    /// <summary>Re-reads the calendars' colors (theme change).</summary>
    public void Repaint()
    {
        if (_owner is not { } owner)
        {
            return;
        }
        var paint = owner.Paint;
        paint.EnsureReady();
        foreach (var chip in _chips)
        {
            chip.Paint(paint, paint.For(chip.CalendarId));
        }
    }

    /// <summary>Chooses <paramref name="id"/> without raising <see cref="Picked"/>.</summary>
    public void Select(string? id)
    {
        _updating = true;
        try
        {
            SelectedId = id;
            foreach (var chip in _chips)
            {
                var chosen = chip.CalendarId == id;
                chip.IsChecked = chosen;
                KeyboardNavigation.SetIsTabStop(chip, chosen);
            }
            if (_chips.Count > 0 && _chips.All(c => c.IsChecked != true))
            {
                KeyboardNavigation.SetIsTabStop(_chips[0], true);
            }
        }
        finally
        {
            _updating = false;
        }
    }

    /// <summary>The arrow keys: picks the next (or previous) calendar and moves the focus to it.</summary>
    public bool Step(WeekCalendarCalendarChip from, int direction)
    {
        var index = _chips.IndexOf(from);
        if (index < 0 || _chips.Count == 0 || !IsEffectivelyEnabled)
        {
            return false;
        }
        var next = _chips[(index + direction + _chips.Count) % _chips.Count];
        next.IsChecked = true;
        next.Focus(NavigationMethod.Directional);
        return true;
    }

    private void OnChipChecked(object? sender, RoutedEventArgs e)
    {
        if (_updating || sender is not WeekCalendarCalendarChip { IsChecked: true } chip)
        {
            return;
        }
        Select(chip.CalendarId);
        Picked?.Invoke(chip.CalendarId);
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var width = double.IsFinite(availableSize.Width) ? availableSize.Width : double.PositiveInfinity;
        double x = 0, y = 0, line = 0, widest = 0;
        foreach (var chip in _chips)
        {
            chip.Measure(Size.Infinity);
            var size = chip.DesiredSize;
            if (x > 0 && x + size.Width > width)
            {
                x = 0;
                y += line + Gap;
                line = 0;
            }
            x += size.Width + Gap;
            widest = Math.Max(widest, x - Gap);
            line = Math.Max(line, size.Height);
        }
        return new Size(double.IsFinite(width) ? Math.Min(width, widest) : widest, _chips.Count == 0 ? 0 : y + line);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        double x = 0, y = 0, line = 0;
        foreach (var chip in _chips)
        {
            var size = chip.DesiredSize;
            if (x > 0 && x + size.Width > finalSize.Width)
            {
                x = 0;
                y += line + Gap;
                line = 0;
            }
            chip.Arrange(new Rect(new Point(x, y), size));
            x += size.Width + Gap;
            line = Math.Max(line, size.Height);
        }
        return finalSize;
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new WeekCalendarGroupPeer(this, AutomationControlType.Group);
}

/// <summary>
/// The shell shared by the quick create and details popovers: their surface comes from the theme; this class adds the
/// entrance and exit (code-driven: the corner it grows from depends on where it was placed), Escape and Enter, and the
/// dialog role. Tab cycles inside (Arc lets it leave; a non-modal dialog that keeps the focus reads better).
/// </summary>
internal abstract class WeekCalendarEditor : TemplatedControl
{
    private readonly MatrixTransform _transform = new();
    private MotionValue<double>? _scale;
    private MotionValue<double>? _fade;

    protected WeekCalendarEditor(WeekCalendar owner)
    {
        Owner = owner;
        Focusable = false;
        ClipToBounds = false;
        RenderTransform = _transform;
        KeyboardNavigation.SetTabNavigation(this, KeyboardNavigationMode.Cycle);
        ArcSurface.SetElevation(this, ArcSurfaceElevation.Raised);
    }

    public WeekCalendar Owner { get; }

    /// <summary>True once it started to leave (it no longer takes input).</summary>
    public bool IsClosing { get; private set; }

    /// <summary>The event (or draft) rect it was placed beside, in the calendar's coordinates.</summary>
    public Rect Anchor { get; set; }

    /// <summary>The bottom of the visible hours, in the calendar's coordinates.</summary>
    public double Bottom { get; set; } = double.PositiveInfinity;

    /// <summary>True on calendars below 640 wide (placed under or over the event).</summary>
    public bool IsCompactPlacement { get; set; }

    /// <summary>Where it went and the corner it grows from (set at its first arrange, then kept: it does not follow its event).</summary>
    public WeekCalendarPopoverPlacement? Placement { get; private set; }

    /// <summary>True when it was opened from the keyboard (the focus comes back with a ring).</summary>
    public bool OpenedByKeyboard { get; set; }

    /// <summary>The scale and opacity now (tests).</summary>
    public double CurrentScale => _scale?.Value ?? 1;

    /// <summary>The box that takes the focus when it opens.</summary>
    public abstract WeekCalendarTitleBox? TitleBox { get; }

    /// <summary>Measures the anchor and the bottom of the grid at the first placement (after any scroll into view).</summary>
    public Func<Rect>? AnchorProvider { get; set; }

    public Func<double>? BottomProvider { get; set; }

    /// <summary>Places the popover (once; later calls keep the first placement's side and spot).</summary>
    public Point Place(Size size, Size bounds)
    {
        if (Placement is null)
        {
            Anchor = AnchorProvider?.Invoke() ?? Anchor;
            Bottom = BottomProvider?.Invoke() ?? Bottom;
            Placement = WeekCalendarEditMath.PlacePopover(Anchor, size, bounds, Bottom, IsCompactPlacement);
            RenderTransformOrigin = Placement.Value.Origin;
        }
        return Placement.Value.TopLeft;
    }

    /// <summary>
    /// Enters: the opacity over 160ms (ease-out) and, at Full motion, the scale from 0.96 on the Smooth spring from the edge
    /// facing the event. Reduced motion keeps only the fade; Off shows it at once.
    /// </summary>
    public void Enter()
    {
        _fade ??= NewValue(1, v => Opacity = v);
        _scale ??= NewValue(1, _ => Apply());
        if (Placement is { } placement)
        {
            RenderTransformOrigin = placement.Origin;
        }
        var level = ArcMotion.GetEffectiveLevel(this);
        if (level == ArcMotionLevel.Off)
        {
            _fade.Jump(1);
            _scale.Jump(1);
            return;
        }
        _fade.Jump(0);
        _ = _fade.AnimateTo(1, WeekCalendarMotion.PopoverFadeIn());
        if (level == ArcMotionLevel.Full)
        {
            _scale.Jump(0.96);
            _ = _scale.AnimateTo(1, MotionSpec.Spring(WeekCalendarMotion.PopoverScale));
        }
    }

    /// <summary>Leaves in 120ms (fading, and shrinking to 0.98 at Full motion); completes at once with Off.</summary>
    public async Task Exit()
    {
        IsClosing = true;
        IsHitTestVisible = false;
        var level = ArcMotion.GetEffectiveLevel(this);
        if (level == ArcMotionLevel.Off || _fade is null || _scale is null || VisualRoot is null)
        {
            return;
        }
        if (level == ArcMotionLevel.Full)
        {
            _ = _scale.AnimateTo(0.98, WeekCalendarMotion.PopoverExit());
        }
        await _fade.AnimateTo(0, WeekCalendarMotion.PopoverExit());
    }

    private MotionValue<double> NewValue(double initial, Action<double> changed)
    {
        var value = new MotionValue<double>(this, initial, MotionVectors.Double);
        value.Changed += changed;
        return value;
    }

    private void Apply()
    {
        var s = _scale?.Value ?? 1;
        _transform.Matrix = Matrix.CreateScale(s, s);
    }

    /// <summary>Enter in one of its text boxes.</summary>
    protected abstract void OnEnterInText();

    /// <summary>The theme changed: repaint what is drawn with calendar colors.</summary>
    public abstract void OnThemeChanged();

    /// <summary>The event (or the calendars) changed while it is open.</summary>
    public virtual void Refresh()
    {
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Handled || IsClosing)
        {
            return;
        }
        if (e.Key == Key.Escape && e.KeyModifiers == KeyModifiers.None)
        {
            Owner.CloseEditor();
            e.Handled = true;
        }
        else if (e.Key == Key.Enter && e.KeyModifiers == KeyModifiers.None && e.Source is Visual source &&
                 source.FindAncestorOfType<WeekCalendarTitleBox>(includeSelf: true) is not null)
        {
            OnEnterInText();
            e.Handled = true;
        }
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new WeekCalendarGroupPeer(this, AutomationControlType.Window);
}

/// <summary>
/// The quick create popover (notes B §3.5): a title box, the day and times of the draft with its length, the calendar
/// choice, a quiet "Enter to save" and Cancel / Save. Enter in the title saves (an empty title saves as untitled);
/// Escape, Cancel or a press outside drop the draft.
/// </summary>
[TemplatePart(TitlePart, typeof(WeekCalendarTitleBox))]
[TemplatePart(WhenPart, typeof(TextBlock))]
[TemplatePart(CalendarsPart, typeof(WeekCalendarCalendarChoice))]
[TemplatePart(HintPart, typeof(TextBlock))]
[TemplatePart(CancelPart, typeof(Button))]
[TemplatePart(SavePart, typeof(Button))]
internal sealed class WeekCalendarQuickCreate : WeekCalendarEditor
{
    public const string TitlePart = "PART_Title";
    public const string WhenPart = "PART_When";
    public const string CalendarsPart = "PART_Calendars";
    public const string HintPart = "PART_Hint";
    public const string CancelPart = "PART_Cancel";
    public const string SavePart = "PART_Save";

    private WeekCalendarTitleBox? _title;
    private TextBlock? _when;
    private WeekCalendarCalendarChoice? _calendars;
    private TextBlock? _hint;
    private Button? _cancel;
    private Button? _save;

    public WeekCalendarQuickCreate(WeekCalendar owner, WeekCalendarGridPane pane, DateOnly day, int start, int end, string? calendarId)
        : base(owner)
    {
        Pane = pane;
        Day = day;
        Start = start;
        End = end;
        CalendarId = calendarId;
        AutomationProperties.SetName(this, owner.Labels.QuickCreateName);
    }

    protected override Type StyleKeyOverride => typeof(WeekCalendarQuickCreate);

    public WeekCalendarGridPane Pane { get; }

    public DateOnly Day { get; }

    public int Start { get; }

    public int End { get; }

    /// <summary>The calendar picked (the draft takes its colors).</summary>
    public string? CalendarId { get; private set; }

    /// <summary>The title typed.</summary>
    public string Title => _title?.Text ?? string.Empty;

    public override WeekCalendarTitleBox? TitleBox => _title;

    /// <summary>The calendar choice (tests).</summary>
    public WeekCalendarCalendarChoice? Calendars => _calendars;

    /// <summary>The text of the day and time line (tests).</summary>
    public string WhenText { get; private set; } = string.Empty;

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        if (_save is not null)
        {
            _save.Click -= OnSave;
        }
        if (_cancel is not null)
        {
            _cancel.Click -= OnCancel;
        }
        if (_calendars is not null)
        {
            _calendars.Picked -= OnPicked;
        }
        _title = e.NameScope.Find<WeekCalendarTitleBox>(TitlePart);
        _when = e.NameScope.Find<TextBlock>(WhenPart);
        _calendars = e.NameScope.Find<WeekCalendarCalendarChoice>(CalendarsPart);
        _hint = e.NameScope.Find<TextBlock>(HintPart);
        _cancel = e.NameScope.Find<Button>(CancelPart);
        _save = e.NameScope.Find<Button>(SavePart);
        var labels = Owner.Labels;
        if (_title is not null)
        {
            _title.Placeholder = labels.TitlePlaceholder;
            AutomationProperties.SetName(_title, labels.TitleLabel);
        }
        if (_hint is not null)
        {
            _hint.Text = labels.SaveHint;
        }
        if (_cancel is not null)
        {
            _cancel.Content = labels.Cancel;
            _cancel.Click += OnCancel;
        }
        if (_save is not null)
        {
            _save.Content = labels.Save;
            _save.Click += OnSave;
        }
        if (_calendars is not null)
        {
            _calendars.Bind(Owner, CalendarId);
            _calendars.Picked += OnPicked;
        }
        UpdateWhen();
    }

    private void UpdateWhen()
    {
        var culture = Owner.EffectiveCulture;
        var labels = Owner.Labels;
        var day = WeekCalendarTime.ShortDayName(Day, culture);
        var rest = WeekCalendarTime.TimeRange(Start, End, culture) + ", " + labels.Duration(End - Start);
        WhenText = day + " " + rest;
        if (_when is null)
        {
            return;
        }
        var paint = Owner.Paint;
        paint.EnsureReady();
        _when.Inlines = new InlineCollection
        {
            new Run(day) { Foreground = paint.Foreground },
            new Run(" " + rest) { Foreground = paint.TextSecondary },
        };
    }

    private void OnPicked(string id)
    {
        CalendarId = id;
        // The draft takes the color at once (Arc); its title stays "New event" while typing.
        Pane.Draft?.Set(Day, Start, End, id);
    }

    private void OnSave(object? sender, RoutedEventArgs e) => Owner.SaveQuickCreate(this);

    private void OnCancel(object? sender, RoutedEventArgs e) => Owner.CloseEditor();

    protected override void OnEnterInText() => Owner.SaveQuickCreate(this);

    public override void OnThemeChanged()
    {
        _calendars?.Repaint();
        UpdateWhen();
    }
}

/// <summary>
/// The details popover of an event (notes B §3.6): its calendar's bar, the title, a close button; the date and the start
/// and end times (15-minute steps) with the length; the place; the people; the calendar choice; Delete and Done. Times, the
/// date and the calendar apply as they are picked; the title and the place when the popover closes. An all-day event shows
/// its dates as text; a read-only calendar shows everything as text and no Delete.
/// </summary>
[TemplatePart(BarPart, typeof(Border))]
[TemplatePart(TitlePart, typeof(WeekCalendarTitleBox))]
[TemplatePart(ClosePart, typeof(Button))]
[TemplatePart(SchedulePart, typeof(Control))]
[TemplatePart(DatePart, typeof(DatePicker))]
[TemplatePart(StartPart, typeof(Select))]
[TemplatePart(SeparatorPart, typeof(TextBlock))]
[TemplatePart(EndPart, typeof(Select))]
[TemplatePart(DurationPart, typeof(TextBlock))]
[TemplatePart(SummaryRowPart, typeof(Control))]
[TemplatePart(SummaryPart, typeof(TextBlock))]
[TemplatePart(LocationRowPart, typeof(Control))]
[TemplatePart(LocationPart, typeof(WeekCalendarTitleBox))]
[TemplatePart(PeopleRowPart, typeof(Control))]
[TemplatePart(PeoplePart, typeof(ProjectsAvatarStack))]
[TemplatePart(PeopleTextPart, typeof(TextBlock))]
[TemplatePart(CalendarsPart, typeof(WeekCalendarCalendarChoice))]
[TemplatePart(DeletePart, typeof(Button))]
[TemplatePart(DeleteTextPart, typeof(TextBlock))]
[TemplatePart(DonePart, typeof(Button))]
internal sealed class WeekCalendarDetails : WeekCalendarEditor
{
    public const string BarPart = "PART_Bar";
    public const string TitlePart = "PART_Title";
    public const string ClosePart = "PART_Close";
    public const string SchedulePart = "PART_Schedule";
    public const string DatePart = "PART_Date";
    public const string StartPart = "PART_Start";
    public const string SeparatorPart = "PART_Separator";
    public const string EndPart = "PART_End";
    public const string DurationPart = "PART_Duration";
    public const string SummaryRowPart = "PART_SummaryRow";
    public const string SummaryPart = "PART_Summary";
    public const string LocationRowPart = "PART_LocationRow";
    public const string LocationPart = "PART_Location";
    public const string PeopleRowPart = "PART_PeopleRow";
    public const string PeoplePart = "PART_People";
    public const string PeopleTextPart = "PART_PeopleText";
    public const string CalendarsPart = "PART_Calendars";
    public const string DeletePart = "PART_Delete";
    public const string DeleteTextPart = "PART_DeleteText";
    public const string DonePart = "PART_Done";

    private Border? _bar;
    private WeekCalendarTitleBox? _title;
    private Button? _close;
    private Control? _schedule;
    private DatePicker? _date;
    private Select? _start;
    private TextBlock? _separator;
    private Select? _end;
    private TextBlock? _duration;
    private Control? _summaryRow;
    private TextBlock? _summary;
    private Control? _locationRow;
    private WeekCalendarTitleBox? _location;
    private Control? _peopleRow;
    private ProjectsAvatarStack? _people;
    private TextBlock? _peopleText;
    private WeekCalendarCalendarChoice? _calendars;
    private Button? _delete;
    private Button? _done;
    private bool _updating;
    private int _endOptionsFrom = -1;

    public WeekCalendarDetails(WeekCalendar owner, WeekCalendarEvent calendarEvent, bool readOnly) : base(owner)
    {
        Event = calendarEvent;
        IsEventReadOnly = readOnly;
        AutomationProperties.SetName(this, owner.Labels.DetailsName);
    }

    protected override Type StyleKeyOverride => typeof(WeekCalendarDetails);

    public WeekCalendarEvent Event { get; }

    /// <summary>True when the calendar is read only: everything shows as text.</summary>
    public bool IsEventReadOnly { get; }

    public override WeekCalendarTitleBox? TitleBox => _title;

    /// <summary>The title in the box (committed when the popover closes).</summary>
    public string TitleText => _title?.Text ?? string.Empty;

    /// <summary>The place in the box (committed when the popover closes), or null when the event has no place row.</summary>
    public string? LocationText => _locationRow?.IsVisible == true ? _location?.Text : null;

    /// <summary>The parts, for tests.</summary>
    public DatePicker? DateField => _date;

    public Select? StartField => _start;

    public Select? EndField => _end;

    public WeekCalendarCalendarChoice? Calendars => _calendars;

    public Button? DeleteButton => _delete;

    public Button? DoneButton => _done;

    public Button? CloseButton => _close;

    public string? SummaryText => _summaryRow?.IsVisible == true ? _summary?.Tag as string : null;

    public string? DurationText => _duration?.Text;

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        Unhook();
        var scope = e.NameScope;
        _bar = scope.Find<Border>(BarPart);
        _title = scope.Find<WeekCalendarTitleBox>(TitlePart);
        _close = scope.Find<Button>(ClosePart);
        _schedule = scope.Find<Control>(SchedulePart);
        _date = scope.Find<DatePicker>(DatePart);
        _start = scope.Find<Select>(StartPart);
        _separator = scope.Find<TextBlock>(SeparatorPart);
        _end = scope.Find<Select>(EndPart);
        _duration = scope.Find<TextBlock>(DurationPart);
        _summaryRow = scope.Find<Control>(SummaryRowPart);
        _summary = scope.Find<TextBlock>(SummaryPart);
        _locationRow = scope.Find<Control>(LocationRowPart);
        _location = scope.Find<WeekCalendarTitleBox>(LocationPart);
        _peopleRow = scope.Find<Control>(PeopleRowPart);
        _people = scope.Find<ProjectsAvatarStack>(PeoplePart);
        _peopleText = scope.Find<TextBlock>(PeopleTextPart);
        _calendars = scope.Find<WeekCalendarCalendarChoice>(CalendarsPart);
        _delete = scope.Find<Button>(DeletePart);
        if (scope.Find<TextBlock>(DeleteTextPart) is { } deleteText)
        {
            deleteText.Text = Owner.Labels.Delete;
        }
        _done = scope.Find<Button>(DonePart);
        var labels = Owner.Labels;
        if (_title is not null)
        {
            _title.Placeholder = labels.Untitled;
            _title.IsReadOnly = IsEventReadOnly;
            AutomationProperties.SetName(_title, labels.TitleLabel);
        }
        if (_location is not null)
        {
            _location.IsReadOnly = IsEventReadOnly;
            AutomationProperties.SetName(_location, labels.LocationLabel);
        }
        if (_close is not null)
        {
            AutomationProperties.SetName(_close, labels.CloseDetails);
            _close.Click += OnClose;
        }
        if (_done is not null)
        {
            _done.Content = labels.Done;
            _done.Click += OnClose;
        }
        if (_delete is not null)
        {
            AutomationProperties.SetName(_delete, labels.Delete);
            _delete.IsVisible = !IsEventReadOnly;
            _delete.Click += OnDelete;
        }
        if (_separator is not null)
        {
            _separator.Text = labels.RangeSeparator;
        }
        if (_date is not null)
        {
            AutomationProperties.SetName(_date, labels.DateLabel);
            _date.Culture = Owner.EffectiveCulture;
            _date.FirstDayOfWeek = Owner.FirstDayOfWeek;
            _date.TemplateApplied += OnFieldTemplateApplied;
            _date.SelectedDateChanged += OnDateChanged;
        }
        foreach (var select in new[] { _start, _end })
        {
            if (select is not null)
            {
                select.TemplateApplied += OnFieldTemplateApplied;
                select.SelectionChanged += OnTimeChanged;
            }
        }
        if (_start is not null)
        {
            AutomationProperties.SetName(_start, labels.StartLabel);
            _start.ItemsSource = TimeOptions(0, 24 * 60 - WeekCalendarEditMetrics.SnapMinutes);
        }
        if (_end is not null)
        {
            AutomationProperties.SetName(_end, labels.EndLabel);
        }
        if (_calendars is not null)
        {
            _calendars.Picked += OnPicked;
        }
        Refresh();
        if (_title is not null)
        {
            _title.Text = Event.Title;
        }
        if (_location is not null)
        {
            _location.Text = Event.Location;
        }
    }

    private void Unhook()
    {
        if (_close is not null)
        {
            _close.Click -= OnClose;
        }
        if (_done is not null)
        {
            _done.Click -= OnClose;
        }
        if (_delete is not null)
        {
            _delete.Click -= OnDelete;
        }
        if (_date is not null)
        {
            _date.TemplateApplied -= OnFieldTemplateApplied;
            _date.SelectedDateChanged -= OnDateChanged;
        }
        foreach (var select in new[] { _start, _end })
        {
            if (select is not null)
            {
                select.TemplateApplied -= OnFieldTemplateApplied;
                select.SelectionChanged -= OnTimeChanged;
            }
        }
        if (_calendars is not null)
        {
            _calendars.Picked -= OnPicked;
        }
    }

    // The date and time fields are 32 high with radius 10 in Arc's details; the main package's are 36–44 high.
    private static void OnFieldTemplateApplied(object? sender, TemplateAppliedEventArgs e)
    {
        if (sender is Select)
        {
            if (e.NameScope.Find<Border>(Select.TriggerPart) is { } trigger)
            {
                trigger.MinHeight = 32;
                trigger.BindCornerRadius(ArcDesignTokenKind.RadiusStep10);
                trigger.Padding = new Thickness(9, 0, 8, 0);
            }
        }
        else if (e.NameScope.Find<FieldFrame>(FieldTextBox.FramePart) is { } frame)
        {
            frame.MinHeight = 32;
            frame.BindCornerRadius(ArcDesignTokenKind.RadiusStep10);
            frame.Padding = new Thickness(8, 0);
        }
    }

    private List<SelectOption> TimeOptions(int from, int to)
    {
        var culture = Owner.EffectiveCulture;
        var options = new List<SelectOption>();
        for (var minute = from; minute <= to; minute += WeekCalendarEditMetrics.SnapMinutes)
        {
            options.Add(new SelectOption(minute, WeekCalendarTime.FullTime(minute, culture)));
        }
        return options;
    }

    /// <summary>The event's wall-clock start and end in the calendar's zone.</summary>
    private (DateTime Start, DateTime End) Wall()
    {
        var zone = Owner.Zone;
        var start = WeekCalendarTime.ToWall(Event.Start, zone);
        var end = WeekCalendarTime.ToWall(Event.End > Event.Start ? Event.End : Event.Start.AddMinutes(WeekCalendarEditMetrics.SnapMinutes), zone);
        return (start, end);
    }

    /// <summary>Shows the event's values (after any change made outside or by an undo).</summary>
    public override void Refresh()
    {
        _updating = true;
        try
        {
            var culture = Owner.EffectiveCulture;
            var labels = Owner.Labels;
            var paint = Owner.Paint;
            paint.EnsureReady();
            if (_bar is not null)
            {
                _bar.Background = paint.For(Event.CalendarId).Bar;
            }
            var (start, end) = Wall();
            var startDay = DateOnly.FromDateTime(start);
            var startMinute = (int)Math.Round(start.TimeOfDay.TotalMinutes);
            var minutes = (int)Math.Round((end - start).TotalMinutes);
            var endMinute = startMinute + minutes;
            var editable = !IsEventReadOnly && !Event.IsAllDay;
            if (_schedule is not null)
            {
                _schedule.IsVisible = editable;
            }
            if (_summaryRow is not null && _summary is not null)
            {
                _summaryRow.IsVisible = !editable;
                if (Event.IsAllDay)
                {
                    var first = DateOnly.FromDateTime(Event.Start.DateTime);
                    var last = DateOnly.FromDateTime(Event.End.DateTime).AddDays(Event.End.DateTime.TimeOfDay > TimeSpan.Zero ? 0 : -1);
                    var days = last > first
                        ? WeekCalendarTime.DayName(first, culture) + " – " + WeekCalendarTime.DayName(last, culture)
                        : WeekCalendarTime.DayName(first, culture);
                    SetSummary(days, labels.AllDayTime, paint);
                }
                else
                {
                    SetSummary(WeekCalendarTime.ShortDayName(startDay, culture),
                        WeekCalendarTime.TimeRange(startMinute, endMinute, culture) + ", " + labels.Duration(minutes), paint);
                }
            }
            if (_date is not null)
            {
                _date.SelectedDate = startDay;
            }
            if (_start is not null)
            {
                _start.SelectedValue = WeekCalendarTimeGrid.SnapDown(startMinute);
            }
            if (_end is not null)
            {
                var from = WeekCalendarTimeGrid.SnapDown(startMinute) + WeekCalendarEditMetrics.SnapMinutes;
                if (from != _endOptionsFrom)
                {
                    _endOptionsFrom = from;
                    _end.ItemsSource = TimeOptions(from, Math.Max(from, 24 * 60));
                }
                _end.SelectedValue = Math.Clamp(WeekCalendarTimeGrid.SnapNearest(endMinute), from, 24 * 60);
            }
            if (_duration is not null)
            {
                _duration.Text = labels.Duration(minutes);
            }
            if (_locationRow is not null)
            {
                _locationRow.IsVisible = !string.IsNullOrWhiteSpace(Event.Location);
            }
            var people = Event.Attendees;
            if (_peopleRow is not null)
            {
                _peopleRow.IsVisible = people is { Count: > 0 };
            }
            if (people is { Count: > 0 })
            {
                if (_people is not null)
                {
                    _people.People = people;
                }
                if (_peopleText is not null)
                {
                    _peopleText.Text = labels.Attendees(people[0].Name, people.Count - 1);
                }
            }
            if (_calendars is not null)
            {
                if (_calendars.Chips.Count != Owner.EffectiveCalendars.Count(c => !string.IsNullOrEmpty(c.Id)))
                {
                    _calendars.Bind(Owner, Event.CalendarId);
                }
                _calendars.Select(Event.CalendarId);
                _calendars.IsVisible = !IsEventReadOnly && _calendars.Chips.Count > 0;
            }
        }
        finally
        {
            _updating = false;
        }
    }

    private void SetSummary(string first, string rest, WeekCalendarPaint paint)
    {
        _summary!.Inlines = new InlineCollection
        {
            new Run(first) { Foreground = paint.Foreground },
            new Run(" " + rest) { Foreground = paint.TextSecondary },
        };
        _summary.Tag = first + " " + rest;
        AutomationProperties.SetName(_summary, first + " " + rest);
    }

    private void OnTimeChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_updating || IsClosing || sender is not Select { SelectedValue: int minute } select)
        {
            return;
        }
        var (start, end) = Wall();
        var day = DateOnly.FromDateTime(start).ToDateTime(TimeOnly.MinValue);
        if (ReferenceEquals(select, _start))
        {
            // A new start keeps the length (Arc).
            Owner.ApplyDetailsTimes(this, day.AddMinutes(minute), day.AddMinutes(minute) + (end - start));
        }
        else
        {
            Owner.ApplyDetailsTimes(this, start, day.AddMinutes(minute));
        }
    }

    private void OnDateChanged(object? sender, DatePickerSelectedDateChangedEventArgs e)
    {
        if (_updating || IsClosing || _date?.SelectedDate is not { } date)
        {
            return;
        }
        var (start, end) = Wall();
        var shift = date.DayNumber - DateOnly.FromDateTime(start).DayNumber;
        if (shift != 0)
        {
            Owner.ApplyDetailsTimes(this, start.AddDays(shift), end.AddDays(shift));
        }
    }

    private void OnPicked(string id)
    {
        if (!_updating && !IsClosing)
        {
            Owner.ApplyDetailsCalendar(this, id);
        }
    }

    private void OnClose(object? sender, RoutedEventArgs e) => Owner.CloseEditor();

    private void OnDelete(object? sender, RoutedEventArgs e) => Owner.DeleteEvent(Event, keyboard: Owner.LastInputWasKeyboard);

    protected override void OnEnterInText() => Owner.CloseEditor();

    public override void OnThemeChanged()
    {
        _calendars?.Repaint();
        Refresh();
    }
}
