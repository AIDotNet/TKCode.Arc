using TKCode.Arc.Motion;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>
/// One step of a <see cref="Stepper"/>: the marker, the label and the swapping description (template), plus the
/// connector to the next step, drawn underneath. The connector track stays put; only its fill scales along it on a
/// smooth spring, one connector after another (P23).
/// </summary>
[TemplatePart(MarkerPart, typeof(StepperMarker))]
[TemplatePart(HeadPart, typeof(Control))]
[TemplatePart(DetailFramePart, typeof(ProgressHeightFrame))]
[PseudoClasses(CompletePseudoClass, CurrentPseudoClass, UpcomingPseudoClass, ErrorPseudoClass, ClickablePseudoClass,
    VerticalPseudoClass, CompactPseudoClass, LastPseudoClass)]
internal sealed class StepperItem : TemplatedControl
{
    public const string MarkerPart = "PART_Marker";
    public const string HeadPart = "PART_Head";
    public const string DetailFramePart = "PART_DetailFrame";
    public const string CompletePseudoClass = ":complete";
    public const string CurrentPseudoClass = ":current";
    public const string UpcomingPseudoClass = ":upcoming";
    public const string ErrorPseudoClass = ":error";
    public const string ClickablePseudoClass = ":clickable";
    public const string VerticalPseudoClass = ":vertical";
    public const string CompactPseudoClass = ":compact";
    public const string LastPseudoClass = ":last";

    public static readonly StyledProperty<string?> LabelProperty =
        AvaloniaProperty.Register<StepperItem, string?>(nameof(Label));

    public static readonly StyledProperty<string?> DetailProperty =
        AvaloniaProperty.Register<StepperItem, string?>(nameof(Detail));

    public string? Label { get => GetValue(LabelProperty); set => SetValue(LabelProperty, value); }
    public string? Detail { get => GetValue(DetailProperty); set => SetValue(DetailProperty, value); }

    private static Cursor? s_hand;
    private readonly Stepper _owner;
    private StepperMarker? _marker;
    private Control? _head;
    private ProgressHeightFrame? _detailFrame;
    private MotionValue<double>? _fill;
    private IPen? _focusPen;
    private IBrush? _focusBrush;
    private bool _hover;
    private bool _pressed;

    // Last applied state, replayed onto the marker after the template is applied.
    private int _number;
    private StepperGlyphKind _kind;
    private StepperStepStatus _status;
    private bool _current;

    public StepperItem(Stepper owner, StepperStep step)
    {
        _owner = owner;
        Step = step;
    }

    public StepperStep Step { get; }

    public int Index { get; private set; }

    public StepperStepStatus Status => _status;

    public bool IsClickable { get; private set; }

    public bool IsReachable { get; private set; }

    public bool IsVertical { get; private set; }

    public bool IsLast { get; private set; }

    internal StepperMarker? Marker => _marker;

    /// <summary>Connector fill (0–1).</summary>
    internal double Fill => _fill?.Value ?? 0;

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _marker = e.NameScope.Find<StepperMarker>(MarkerPart);
        _head = e.NameScope.Find<Control>(HeadPart);
        _detailFrame = e.NameScope.Find<ProgressHeightFrame>(DetailFramePart);
        if (_owner.Palette is { } palette)
        {
            _marker?.Apply(palette, _number, _kind, _status, _current, _hover && IsClickable, 0, 0, animate: false);
        }
    }

    /// <summary>Applies the step state; delays are in seconds and only used when <paramref name="animate"/> is set.</summary>
    public void Configure(int index, int count, StepperStepStatus status, StepperGlyphKind kind, bool current, bool clickable,
                          bool reachable, bool navigable, bool vertical, bool compact, string? detail, double glyphDelay,
                          double ringDelay, bool connectorFilled, bool animate)
    {
        Index = index;
        IsClickable = clickable;
        IsReachable = reachable;
        IsVertical = vertical;
        IsLast = index == count - 1;
        _number = index + 1;
        _kind = kind;
        _status = status;
        _current = current;
        PseudoClasses.Set(CompletePseudoClass, status == StepperStepStatus.Complete);
        PseudoClasses.Set(CurrentPseudoClass, status == StepperStepStatus.Current);
        PseudoClasses.Set(UpcomingPseudoClass, status == StepperStepStatus.Upcoming);
        PseudoClasses.Set(ErrorPseudoClass, status == StepperStepStatus.Error);
        PseudoClasses.Set(ClickablePseudoClass, clickable);
        PseudoClasses.Set(VerticalPseudoClass, vertical);
        PseudoClasses.Set(CompactPseudoClass, compact);
        PseudoClasses.Set(LastPseudoClass, IsLast);
        Cursor = clickable ? s_hand ??= new Cursor(StandardCursorType.Hand) : Cursor.Default;
        Focusable = navigable && reachable;
        IsTabStop = clickable;
        if (!clickable)
        {
            _hover = false;
            _pressed = false;
        }
        Label = Step.Label;
        if (!string.Equals(Detail, detail, StringComparison.Ordinal))
        {
            _detailFrame?.Arm();
            Detail = detail;
        }
        var statusText = status switch
        {
            StepperStepStatus.Complete => "Completed",
            StepperStepStatus.Upcoming => "Not started",
            StepperStepStatus.Error => "Error",
            _ => null
        };
        AutomationProperties.SetName(this, statusText is null ? Step.Label ?? string.Empty : $"{Step.Label}, {statusText}");
        AutomationProperties.SetItemStatus(this, current ? "Current step" : string.Empty);
        AutomationProperties.SetPositionInSet(this, index + 1);
        AutomationProperties.SetSizeOfSet(this, count);

        if (_owner.Palette is { } palette)
        {
            _marker?.Apply(palette, _number, kind, status, current, _hover && clickable, glyphDelay, ringDelay, animate);
        }

        var target = connectorFilled && !IsLast ? 1.0 : 0.0;
        if (_fill is null)
        {
            _fill = new MotionValue<double>(this, target, MotionVectors.Double);
            _fill.Changed += _ => InvalidateVisual();
        }
        else if (Math.Abs(_fill.Target - target) > 0.001)
        {
            if (animate && IsLoaded)
            {
                _ = _fill.AnimateTo(target, MotionSpec.Spring(ArcSpring.Smooth, TimeSpan.FromSeconds(glyphDelay)));
            }
            else
            {
                _fill.Jump(target);
            }
        }
        InvalidateVisual();
    }

    private void RefreshMarker()
    {
        if (_owner.Palette is { } palette)
        {
            _marker?.Apply(palette, _number, _kind, _status, _current, _hover && IsClickable, 0, 0, animate: true);
        }
    }

    protected override void OnPointerEntered(PointerEventArgs e)
    {
        base.OnPointerEntered(e);
        if (IsClickable && e.Pointer.Type != PointerType.Touch)
        {
            _hover = true;
            RefreshMarker();
        }
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        if (_hover || _pressed)
        {
            _hover = false;
            if (_pressed)
            {
                _pressed = false;
                _marker?.SetPressed(false);
            }
            RefreshMarker();
        }
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (!IsClickable || !e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            return;
        }
        _pressed = true;
        e.Handled = true;
        _marker?.SetPressed(true);
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        if (!_pressed)
        {
            return;
        }
        _pressed = false;
        _marker?.SetPressed(false);
        if (new Rect(Bounds.Size).Contains(e.GetPosition(this)))
        {
            e.Handled = true;
            _owner.Select(Index);
        }
    }

    protected override void OnGotFocus(FocusChangedEventArgs e)
    {
        base.OnGotFocus(e);
        InvalidateVisual();
    }

    protected override void OnLostFocus(FocusChangedEventArgs e)
    {
        base.OnLostFocus(e);
        InvalidateVisual();
    }

    public override void Render(DrawingContext context)
    {
        var palette = _owner.Palette;
        if (palette is null)
        {
            return;
        }
        if (!IsLast)
        {
            // Both ends keep a clear gap from the markers and from the current step's halo.
            const double marker = 28;
            const double halo = 4;
            Rect track;
            if (IsVertical)
            {
                var top = marker + halo + 2;
                track = new Rect(marker / 2 - 1, top, 2, Math.Max(0, Bounds.Height - halo - 2 - top));
            }
            else
            {
                var left = marker + halo + 4;
                track = new Rect(left, marker / 2 - 1, Math.Max(0, Bounds.Width - halo - 4 - left), 2);
            }
            if (track.Width > 0 && track.Height > 0)
            {
                context.DrawRectangle(palette.Track, null, new RoundedRect(track, 1));
                var fill = Math.Clamp(Fill, 0, 1);
                if (fill > 0.001)
                {
                    var filled = IsVertical ? track.WithHeight(track.Height * fill) : track.WithWidth(track.Width * fill);
                    using (context.PushClip(new RoundedRect(track, 1)))
                    {
                        context.DrawRectangle(palette.Accent, null, filled);
                    }
                }
            }
        }
        if (IsFocused && Classes.Contains(":focus-visible") && _head is not null)
        {
            if (!ReferenceEquals(_focusBrush, palette.FocusRing) || _focusPen is null)
            {
                _focusBrush = palette.FocusRing;
                _focusPen = new Pen(palette.FocusRing, 2);
            }
            var origin = _head.TranslatePoint(default, this) ?? default;
            var rect = new Rect(origin, _head.Bounds.Size).Inflate(5);
            context.DrawRectangle(null, _focusPen, new RoundedRect(rect, 13));
        }
    }
}

/// <summary>
/// Lays the steps out: horizontally as equal columns for every step but the last, which takes its own width (Arc:
/// <c>repeat(n − 1, minmax(0, 1fr)) auto</c>); vertically as a stack.
/// </summary>
internal sealed class StepperPanel : Panel
{
    public static readonly StyledProperty<Orientation> OrientationProperty =
        AvaloniaProperty.Register<StepperPanel, Orientation>(nameof(Orientation), Orientation.Horizontal);

    public Orientation Orientation { get => GetValue(OrientationProperty); set => SetValue(OrientationProperty, value); }

    static StepperPanel()
    {
        AffectsMeasure<StepperPanel>(OrientationProperty);
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var count = Children.Count;
        if (count == 0)
        {
            return default;
        }
        if (Orientation == Orientation.Vertical)
        {
            var height = 0.0;
            var width = 0.0;
            foreach (var child in Children)
            {
                child.Measure(availableSize.WithHeight(double.PositiveInfinity));
                height += child.DesiredSize.Height;
                width = Math.Max(width, child.DesiredSize.Width);
            }
            return new Size(width, height);
        }
        var last = Children[count - 1];
        last.Measure(availableSize);
        if (count == 1)
        {
            return last.DesiredSize;
        }
        var total = double.IsInfinity(availableSize.Width) ? (count - 1) * 160 + last.DesiredSize.Width : availableSize.Width;
        var column = Math.Max(0, (total - last.DesiredSize.Width) / (count - 1));
        var tallest = last.DesiredSize.Height;
        for (var i = 0; i < count - 1; i++)
        {
            Children[i].Measure(new Size(column, double.PositiveInfinity));
            tallest = Math.Max(tallest, Children[i].DesiredSize.Height);
        }
        return new Size(total, tallest);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var count = Children.Count;
        if (count == 0)
        {
            return finalSize;
        }
        if (Orientation == Orientation.Vertical)
        {
            var y = 0.0;
            foreach (var child in Children)
            {
                child.Arrange(new Rect(0, y, finalSize.Width, child.DesiredSize.Height));
                y += child.DesiredSize.Height;
            }
            return finalSize;
        }
        var last = Children[count - 1];
        var lastWidth = Math.Min(last.DesiredSize.Width, finalSize.Width);
        var column = count > 1 ? Math.Max(0, (finalSize.Width - lastWidth) / (count - 1)) : 0;
        for (var i = 0; i < count - 1; i++)
        {
            Children[i].Arrange(new Rect(i * column, 0, column, finalSize.Height));
        }
        last.Arrange(new Rect((count - 1) * column, 0, lastWidth, finalSize.Height));
        return finalSize;
    }
}
