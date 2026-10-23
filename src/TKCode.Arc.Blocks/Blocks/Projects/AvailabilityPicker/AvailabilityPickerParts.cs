using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>
/// One day card of an <see cref="AvailabilityPicker"/>: the short weekday, the day number and how many times suit everyone.
/// A toggle whose state the picker sets (a press only reports itself, <c>aria-pressed</c>); the selected card's fill is the
/// strip's gliding highlight, drawn under every card.
/// </summary>
[PseudoClasses(CompactPseudoClass, NarrowPseudoClass)]
internal sealed class AvailabilityDayButton : ToggleButton
{
    public const string CompactPseudoClass = ":compact";
    public const string NarrowPseudoClass = ":narrow";

    /// <summary>The short weekday ("Mon").</summary>
    public static readonly StyledProperty<string?> DayNameProperty =
        AvaloniaProperty.Register<AvailabilityDayButton, string?>(nameof(DayName));

    /// <summary>The day number ("9").</summary>
    public static readonly StyledProperty<string?> DayNumberProperty =
        AvaloniaProperty.Register<AvailabilityDayButton, string?>(nameof(DayNumber));

    /// <summary>The count line ("3 open").</summary>
    public static readonly StyledProperty<string?> OpenTextProperty =
        AvaloniaProperty.Register<AvailabilityDayButton, string?>(nameof(OpenText));

    public AvailabilityDayButton(AvailabilityDay day)
    {
        Day = day;
    }

    protected override Type StyleKeyOverride => typeof(AvailabilityDayButton);

    public AvailabilityDay Day { get; }

    public string? DayName { get => GetValue(DayNameProperty); set => SetValue(DayNameProperty, value); }

    public string? DayNumber { get => GetValue(DayNumberProperty); set => SetValue(DayNumberProperty, value); }

    public string? OpenText { get => GetValue(OpenTextProperty); set => SetValue(OpenTextProperty, value); }

    public void SetSizeClass(bool compact, bool narrow)
    {
        PseudoClasses.Set(CompactPseudoClass, compact);
        PseudoClasses.Set(NarrowPseudoClass, narrow);
    }

    protected override void Toggle()
    {
        // The picker decides which day is selected; clicking the selected day changes nothing.
    }
}

/// <summary>
/// One time row of an <see cref="AvailabilityPicker"/>: the start, a line saying who is free, one mark per attendee and a
/// check while selected. A toggle whose state the picker sets. The check slides in and pushes the marks aside on one
/// spring (<see cref="ProjectsSprings.Width"/>) with a 160ms fade.
/// </summary>
[TemplatePart(LayoutPart, typeof(AvailabilitySlotLayout))]
[TemplatePart(MarksPart, typeof(AvailabilityMarks))]
[TemplatePart(CheckPart, typeof(Control))]
[PseudoClasses(BusyPseudoClass)]
internal sealed class AvailabilitySlotButton : ToggleButton
{
    public const string LayoutPart = "PART_Layout";
    public const string MarksPart = "PART_Marks";
    public const string CheckPart = "PART_Check";
    public const string BusyPseudoClass = ":busy";

    /// <summary>The start ("11:00").</summary>
    public static readonly StyledProperty<string?> TimeTextProperty =
        AvaloniaProperty.Register<AvailabilitySlotButton, string?>(nameof(TimeText));

    /// <summary>The line next to the time ("Everyone's free", "Noor is busy").</summary>
    public static readonly StyledProperty<string?> NoteTextProperty =
        AvaloniaProperty.Register<AvailabilitySlotButton, string?>(nameof(NoteText));

    /// <summary>True when somebody is busy (the line turns muted).</summary>
    public static readonly StyledProperty<bool> IsBusyProperty =
        AvaloniaProperty.Register<AvailabilitySlotButton, bool>(nameof(IsBusy));

    private AvailabilitySlotLayout? _layout;
    private AvailabilityMarks? _marks;
    private Control? _check;
    private MotionValue<double>? _width;
    private MotionValue<double>? _fade;
    private IReadOnlyList<bool> _busyMarks = Array.Empty<bool>();

    public AvailabilitySlotButton(AvailabilitySlot slot)
    {
        Slot = slot;
    }

    protected override Type StyleKeyOverride => typeof(AvailabilitySlotButton);

    public AvailabilitySlot Slot { get; }

    public string? TimeText { get => GetValue(TimeTextProperty); set => SetValue(TimeTextProperty, value); }

    public string? NoteText { get => GetValue(NoteTextProperty); set => SetValue(NoteTextProperty, value); }

    public bool IsBusy { get => GetValue(IsBusyProperty); set => SetValue(IsBusyProperty, value); }

    /// <summary>One entry per attendee, in order: true when that attendee is busy.</summary>
    public IReadOnlyList<bool> BusyMarks
    {
        get => _busyMarks;
        set
        {
            _busyMarks = value;
            if (_marks is not null)
            {
                _marks.Busy = value;
            }
        }
    }

    /// <summary>How far the check has come in (0 hidden, 1 in place); drives the marks' push.</summary>
    public double CheckProgress => _width?.Value ?? (IsChecked == true ? 1 : 0);

    /// <summary>The check's opacity.</summary>
    public double CheckOpacity => _fade?.Value ?? (IsChecked == true ? 1 : 0);

    protected override void Toggle()
    {
        // The picker decides which time is selected; a selected time stays selected when pressed again.
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _layout = e.NameScope.Find<AvailabilitySlotLayout>(LayoutPart);
        _marks = e.NameScope.Find<AvailabilityMarks>(MarksPart);
        _check = e.NameScope.Find<Control>(CheckPart);
        if (_marks is not null)
        {
            _marks.Busy = _busyMarks;
        }
        UpdateCheck(animate: false);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IsCheckedProperty)
        {
            UpdateCheck(animate: true);
        }
        else if (change.Property == IsBusyProperty)
        {
            PseudoClasses.Set(BusyPseudoClass, IsBusy);
        }
    }

    // The check's width spring and its fade are two curves on two parts (the marks move, the check slides and fades);
    // a ControlTheme transition cannot keep the pair on one spring that retargets with its velocity, so code drives them.
    private void UpdateCheck(bool animate)
    {
        var on = IsChecked == true;
        var target = on ? 1.0 : 0.0;
        if (_width is null)
        {
            _width = new MotionValue<double>(this, target, MotionVectors.Double);
            _width.Changed += v => _layout?.SetCheckProgress(v);
        }
        if (_fade is null)
        {
            _fade = new MotionValue<double>(this, target, MotionVectors.Double);
            _fade.Changed += v =>
            {
                if (_check is not null)
                {
                    _check.Opacity = v;
                }
            };
        }
        if (_layout is not null)
        {
            _layout.IsChecked = on;
        }
        if (animate && IsLoaded && ArcMotion.IsFull(this))
        {
            _ = _width.AnimateTo(target, MotionSpec.Spring(ProjectsSprings.Width));
            _ = _fade.AnimateTo(target, ProjectsSprings.FadeFast());
        }
        else
        {
            _width.Jump(target);
            _fade.Jump(target);
        }
        _layout?.SetCheckProgress(_width.Value);
        if (_check is not null)
        {
            _check.Opacity = _fade.Value;
        }
    }
}

/// <summary>
/// Lays out a time row: the time (at least <see cref="TimeMinWidth"/>), the note (the rest, trimmed), the marks against
/// the right edge and the check behind them. The check's spring moves the marks and the check by their transforms only, so
/// it never relayouts; the note gives up the check's room while the check is in.
/// </summary>
internal sealed class AvailabilitySlotLayout : Panel
{
    /// <summary>Space between the parts (12).</summary>
    public static readonly StyledProperty<double> GapProperty =
        AvaloniaProperty.Register<AvailabilitySlotLayout, double>(nameof(Gap), 12);

    /// <summary>The time's minimum width (42).</summary>
    public static readonly StyledProperty<double> TimeMinWidthProperty =
        AvaloniaProperty.Register<AvailabilitySlotLayout, double>(nameof(TimeMinWidth), 42);

    private readonly TranslateTransform _marksShift = new();
    private readonly TranslateTransform _checkShift = new();
    private bool _isChecked;
    private bool _reserving;
    private double _progress;

    static AvailabilitySlotLayout()
    {
        AffectsMeasure<AvailabilitySlotLayout>(GapProperty, TimeMinWidthProperty);
    }

    public double Gap { get => GetValue(GapProperty); set => SetValue(GapProperty, value); }

    public double TimeMinWidth { get => GetValue(TimeMinWidthProperty); set => SetValue(TimeMinWidthProperty, value); }

    /// <summary>True while the row is selected: the note leaves room for the check.</summary>
    public bool IsChecked
    {
        get => _isChecked;
        set
        {
            _isChecked = value;
            var reserve = value || _progress > 0.02;
            if (reserve != _reserving)
            {
                _reserving = reserve;
                InvalidateMeasure();
            }
        }
    }

    /// <summary>How far the marks are pushed (x −(check + gap)) and the check slid in (from +check), 0–1.</summary>
    public double MarksOffset => _marksShift.X;

    private Control? Time => Children.Count > 0 ? Children[0] : null;
    private Control? Note => Children.Count > 1 ? Children[1] : null;
    private Control? Marks => Children.Count > 2 ? Children[2] : null;
    private Control? Check => Children.Count > 3 ? Children[3] : null;

    public void SetCheckProgress(double progress)
    {
        _progress = progress;
        var check = Check?.DesiredSize.Width ?? 0;
        _marksShift.X = -(check + Gap) * progress;
        _checkShift.X = check * (1 - progress);
        if (!_isChecked && _reserving && progress <= 0.02)
        {
            // The marks are back at the edge: the note may use the check's room again.
            _reserving = false;
            InvalidateMeasure();
        }
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var infinite = new Size(double.PositiveInfinity, availableSize.Height);
        Time?.Measure(infinite);
        Marks?.Measure(infinite);
        Check?.Measure(infinite);
        var time = Math.Max(TimeMinWidth, Time?.DesiredSize.Width ?? 0);
        var marks = Marks?.DesiredSize.Width ?? 0;
        var check = Check?.DesiredSize.Width ?? 0;
        var fixedWidth = time + Gap + Gap + marks + (_reserving ? Gap + check : 0);
        var noteRoom = double.IsInfinity(availableSize.Width) ? double.PositiveInfinity : Math.Max(0, availableSize.Width - fixedWidth);
        Note?.Measure(new Size(noteRoom, availableSize.Height));
        var height = Math.Max(Math.Max(Time?.DesiredSize.Height ?? 0, Note?.DesiredSize.Height ?? 0),
            Math.Max(Marks?.DesiredSize.Height ?? 0, Check?.DesiredSize.Height ?? 0));
        var width = double.IsInfinity(availableSize.Width) ? fixedWidth + (Note?.DesiredSize.Width ?? 0) : availableSize.Width;
        return new Size(width, height);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var time = Math.Max(TimeMinWidth, Time?.DesiredSize.Width ?? 0);
        var marks = Marks?.DesiredSize.Width ?? 0;
        var check = Check?.DesiredSize.Width ?? 0;
        Arrange(Time, 0, time, finalSize.Height);
        var noteX = time + Gap;
        var noteWidth = Math.Max(0, finalSize.Width - noteX - Gap - marks - (_reserving ? Gap + check : 0));
        Arrange(Note, noteX, noteWidth, finalSize.Height);
        Arrange(Marks, finalSize.Width - marks, marks, finalSize.Height);
        Arrange(Check, finalSize.Width - check, check, finalSize.Height);
        if (Marks is { } markControl && !ReferenceEquals(markControl.RenderTransform, _marksShift))
        {
            markControl.RenderTransform = _marksShift;
        }
        if (Check is { } checkControl && !ReferenceEquals(checkControl.RenderTransform, _checkShift))
        {
            checkControl.RenderTransform = _checkShift;
        }
        SetCheckProgress(_progress);
        return finalSize;
    }

    private static void Arrange(Control? child, double x, double width, double height)
    {
        if (child is null)
        {
            return;
        }
        var h = Math.Min(height, child.DesiredSize.Height);
        child.Arrange(new Rect(x, (height - h) / 2, width, h));
    }
}

/// <summary>One mark per attendee (4 × 14, radius 2, 3 apart): green when free, grey when busy. Hidden from screen readers.</summary>
internal sealed class AvailabilityMarks : Control
{
    public static readonly StyledProperty<IBrush?> FreeBrushProperty =
        AvaloniaProperty.Register<AvailabilityMarks, IBrush?>(nameof(FreeBrush));

    public static readonly StyledProperty<IBrush?> BusyBrushProperty =
        AvaloniaProperty.Register<AvailabilityMarks, IBrush?>(nameof(BusyBrush));

    public static readonly StyledProperty<double> MarkWidthProperty =
        AvaloniaProperty.Register<AvailabilityMarks, double>(nameof(MarkWidth), 4);

    public static readonly StyledProperty<double> MarkHeightProperty =
        AvaloniaProperty.Register<AvailabilityMarks, double>(nameof(MarkHeight), 14);

    public static readonly StyledProperty<double> MarkRadiusProperty =
        AvaloniaProperty.Register<AvailabilityMarks, double>(nameof(MarkRadius), 2);

    public static readonly StyledProperty<double> MarkGapProperty =
        AvaloniaProperty.Register<AvailabilityMarks, double>(nameof(MarkGap), 3);

    private IReadOnlyList<bool> _busy = Array.Empty<bool>();

    static AvailabilityMarks()
    {
        AffectsMeasure<AvailabilityMarks>(MarkWidthProperty, MarkHeightProperty, MarkGapProperty);
        AffectsRender<AvailabilityMarks>(FreeBrushProperty, BusyBrushProperty, MarkRadiusProperty);
        IsHitTestVisibleProperty.OverrideDefaultValue<AvailabilityMarks>(false);
        FocusableProperty.OverrideDefaultValue<AvailabilityMarks>(false);
    }

    public AvailabilityMarks()
    {
        AutomationProperties.SetAccessibilityView(this, AccessibilityView.Raw);
    }

    public IBrush? FreeBrush { get => GetValue(FreeBrushProperty); set => SetValue(FreeBrushProperty, value); }

    public IBrush? BusyBrush { get => GetValue(BusyBrushProperty); set => SetValue(BusyBrushProperty, value); }

    public double MarkWidth { get => GetValue(MarkWidthProperty); set => SetValue(MarkWidthProperty, value); }

    public double MarkHeight { get => GetValue(MarkHeightProperty); set => SetValue(MarkHeightProperty, value); }

    public double MarkRadius { get => GetValue(MarkRadiusProperty); set => SetValue(MarkRadiusProperty, value); }

    public double MarkGap { get => GetValue(MarkGapProperty); set => SetValue(MarkGapProperty, value); }

    /// <summary>One entry per attendee: true when busy.</summary>
    public IReadOnlyList<bool> Busy
    {
        get => _busy;
        set
        {
            var count = _busy.Count;
            _busy = value ?? Array.Empty<bool>();
            if (count != _busy.Count)
            {
                InvalidateMeasure();
            }
            InvalidateVisual();
        }
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var n = _busy.Count;
        return n == 0 ? default : new Size(n * MarkWidth + (n - 1) * MarkGap, MarkHeight);
    }

    public override void Render(DrawingContext context)
    {
        var y = (Bounds.Height - MarkHeight) / 2;
        for (var i = 0; i < _busy.Count; i++)
        {
            var rect = new Rect(i * (MarkWidth + MarkGap), y, MarkWidth, MarkHeight);
            context.DrawRectangle(_busy[i] ? BusyBrush : FreeBrush, null, new RoundedRect(rect, MarkRadius));
        }
    }
}

/// <summary>The time rows of one day in <see cref="Columns"/> equal columns, <see cref="Gap"/> apart both ways, row by row.</summary>
internal sealed class AvailabilitySlotGrid : Panel
{
    public static readonly StyledProperty<int> ColumnsProperty =
        AvaloniaProperty.Register<AvailabilitySlotGrid, int>(nameof(Columns), 2);

    public static readonly StyledProperty<double> GapProperty =
        AvaloniaProperty.Register<AvailabilitySlotGrid, double>(nameof(Gap), 8);

    static AvailabilitySlotGrid()
    {
        AffectsMeasure<AvailabilitySlotGrid>(ColumnsProperty, GapProperty);
    }

    public AvailabilitySlotGrid(AvailabilityDay? day)
    {
        Day = day;
    }

    /// <summary>The day whose times these are (null for an empty list).</summary>
    public AvailabilityDay? Day { get; }

    public int Columns { get => GetValue(ColumnsProperty); set => SetValue(ColumnsProperty, value); }

    public double Gap { get => GetValue(GapProperty); set => SetValue(GapProperty, value); }

    public IEnumerable<AvailabilitySlotButton> Buttons => Children.OfType<AvailabilitySlotButton>();

    private int ColumnCount => Math.Max(1, Columns);

    protected override AutomationPeer OnCreateAutomationPeer() => new AvailabilityGroupPeer(this, AutomationControlType.List);

    protected override Size MeasureOverride(Size availableSize)
    {
        var columns = ColumnCount;
        var children = Children;
        if (children.Count == 0)
        {
            return default;
        }
        // A lone child that is not a row (the "no times" line) spans the width.
        if (children.Count == 1 && children[0] is not AvailabilitySlotButton)
        {
            children[0].Measure(availableSize);
            return children[0].DesiredSize;
        }
        var width = double.IsInfinity(availableSize.Width) ? double.PositiveInfinity : Math.Max(0, (availableSize.Width - Gap * (columns - 1)) / columns);
        double widest = 0;
        foreach (var child in children)
        {
            child.Measure(new Size(width, double.PositiveInfinity));
            widest = Math.Max(widest, child.DesiredSize.Width);
        }
        var height = RowsHeight((children.Count + columns - 1) / columns);
        var total = double.IsInfinity(availableSize.Width) ? widest * columns + Gap * (columns - 1) : availableSize.Width;
        return new Size(total, height);
    }

    private double RowsHeight(int rows)
    {
        double height = 0;
        var columns = ColumnCount;
        for (var r = 0; r < rows; r++)
        {
            double row = 0;
            for (var c = 0; c < columns; c++)
            {
                var i = r * columns + c;
                if (i < Children.Count)
                {
                    row = Math.Max(row, Children[i].DesiredSize.Height);
                }
            }
            height += row + (r > 0 ? Gap : 0);
        }
        return height;
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var children = Children;
        if (children.Count == 1 && children[0] is not AvailabilitySlotButton)
        {
            children[0].Arrange(new Rect(finalSize));
            return finalSize;
        }
        var columns = ColumnCount;
        var width = Math.Max(0, (finalSize.Width - Gap * (columns - 1)) / columns);
        double y = 0;
        for (var start = 0; start < children.Count; start += columns)
        {
            double row = 0;
            for (var c = 0; c < columns && start + c < children.Count; c++)
            {
                row = Math.Max(row, children[start + c].DesiredSize.Height);
            }
            for (var c = 0; c < columns && start + c < children.Count; c++)
            {
                children[start + c].Arrange(new Rect(c * (width + Gap), y, width, row));
            }
            y += row + Gap;
        }
        return finalSize;
    }
}

/// <summary>
/// The day cards in equal columns with one <see cref="GlidingIndicator"/> drawn under all of them: the selected card's fill.
/// Choosing another day springs the fill there (<see cref="ProjectsSprings.DayHighlight"/>); resizing moves it at once. The
/// fill follows the selected card's press scale.
/// </summary>
internal sealed class AvailabilityDayStrip : Panel
{
    public static readonly StyledProperty<double> GapProperty =
        AvaloniaProperty.Register<AvailabilityDayStrip, double>(nameof(Gap), 6);

    public static readonly StyledProperty<IBrush?> HighlightFillProperty =
        AvaloniaProperty.Register<AvailabilityDayStrip, IBrush?>(nameof(HighlightFill));

    public static readonly StyledProperty<CornerRadius> HighlightRadiusProperty =
        AvaloniaProperty.Register<AvailabilityDayStrip, CornerRadius>(nameof(HighlightRadius), new CornerRadius(14));

    private readonly GlidingIndicator _highlight = new();
    private readonly List<AvailabilityDayButton> _buttons = new();
    private AvailabilityDayButton? _watched;
    private int _selected = -1;
    private bool _glide;
    private Rect? _placed;

    static AvailabilityDayStrip()
    {
        AffectsMeasure<AvailabilityDayStrip>(GapProperty);
    }

    public AvailabilityDayStrip()
    {
        _highlight.CornerRadius = HighlightRadius;
        Children.Add(_highlight);
    }

    public double Gap { get => GetValue(GapProperty); set => SetValue(GapProperty, value); }

    public IBrush? HighlightFill { get => GetValue(HighlightFillProperty); set => SetValue(HighlightFillProperty, value); }

    public CornerRadius HighlightRadius { get => GetValue(HighlightRadiusProperty); set => SetValue(HighlightRadiusProperty, value); }

    public GlidingIndicator Highlight => _highlight;

    public IReadOnlyList<AvailabilityDayButton> Buttons => _buttons;

    public int SelectedIndex => _selected;

    protected override AutomationPeer OnCreateAutomationPeer() => new AvailabilityGroupPeer(this, AutomationControlType.Group);

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == HighlightFillProperty)
        {
            _highlight.Fill = HighlightFill;
        }
        else if (change.Property == HighlightRadiusProperty)
        {
            _highlight.CornerRadius = HighlightRadius;
        }
    }

    /// <summary>Replaces the cards; nothing is selected afterwards.</summary>
    public void SetButtons(IReadOnlyList<AvailabilityDayButton> buttons)
    {
        Watch(null);
        foreach (var old in _buttons)
        {
            Children.Remove(old);
        }
        _buttons.Clear();
        _buttons.AddRange(buttons);
        Children.AddRange(buttons);
        _selected = -1;
        _glide = false;
        InvalidateMeasure();
    }

    /// <summary>Marks card <paramref name="index"/> selected (−1 for none); <paramref name="animate"/> glides the fill there.</summary>
    public void Select(int index, bool animate)
    {
        if (index >= _buttons.Count)
        {
            index = -1;
        }
        var had = _selected >= 0 && _placed is not null;
        _selected = index;
        for (var i = 0; i < _buttons.Count; i++)
        {
            _buttons[i].IsChecked = i == index;
        }
        _glide = animate && had && index >= 0;
        Watch(index >= 0 ? _buttons[index] : null);
        InvalidateArrange();
    }

    private void Watch(AvailabilityDayButton? button)
    {
        if (_watched is not null)
        {
            _watched.PropertyChanged -= OnSelectedButtonChanged;
        }
        _watched = button;
        if (_watched is not null)
        {
            _watched.PropertyChanged += OnSelectedButtonChanged;
        }
        FollowPress();
    }

    private void OnSelectedButtonChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == RenderTransformProperty)
        {
            FollowPress();
        }
    }

    /// <summary>The fill shrinks with the selected card while it is pressed (scale about the card's centre).</summary>
    private void FollowPress()
    {
        if (_watched is { RenderTransform: { } transform } card)
        {
            _highlight.RenderTransform = transform;
            _highlight.RenderTransformOrigin = new RelativePoint(card.Bounds.Center, RelativeUnit.Absolute);
        }
        else
        {
            _highlight.RenderTransform = null;
        }
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        _highlight.Measure(availableSize);
        var n = _buttons.Count;
        if (n == 0)
        {
            return default;
        }
        var width = double.IsInfinity(availableSize.Width) ? double.PositiveInfinity : Math.Max(0, (availableSize.Width - Gap * (n - 1)) / n);
        double height = 0;
        double widest = 0;
        foreach (var button in _buttons)
        {
            button.Measure(new Size(width, double.PositiveInfinity));
            height = Math.Max(height, button.DesiredSize.Height);
            widest = Math.Max(widest, button.DesiredSize.Width);
        }
        var total = double.IsInfinity(availableSize.Width) ? widest * n + Gap * (n - 1) : availableSize.Width;
        return new Size(total, height);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        _highlight.Arrange(new Rect(finalSize));
        var n = _buttons.Count;
        var width = n == 0 ? 0 : Math.Max(0, (finalSize.Width - Gap * (n - 1)) / n);
        for (var i = 0; i < n; i++)
        {
            _buttons[i].Arrange(new Rect(i * (width + Gap), 0, width, finalSize.Height));
        }
        PlaceHighlight();
        return finalSize;
    }

    private void PlaceHighlight()
    {
        if (_selected < 0 || _selected >= _buttons.Count)
        {
            if (_placed is not null)
            {
                _highlight.Hide(animate: false);
                _placed = null;
            }
            _glide = false;
            return;
        }
        var rect = _buttons[_selected].Bounds;
        if (_glide)
        {
            _highlight.MoveTo(rect, ProjectsSprings.DayHighlight, animate: true);
        }
        else if (_placed != rect)
        {
            _highlight.MoveTo(rect, ProjectsSprings.DayHighlight, animate: false);
        }
        _glide = false;
        _placed = rect;
        FollowPress();
    }
}

/// <summary>Reports a panel as a named group or list to screen readers.</summary>
internal sealed class AvailabilityGroupPeer(Control owner, AutomationControlType type) : ControlAutomationPeer(owner)
{
    protected override AutomationControlType GetAutomationControlTypeCore() => type;

    protected override bool IsContentElementCore() => true;

    protected override bool IsControlElementCore() => true;
}
