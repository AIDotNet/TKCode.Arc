using System.Globalization;
using TKCode.Arc.Motion;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Avalonia.VisualTree;

namespace TKCode.Arc;

/// <summary>
/// The TimePicker's options, drawn in one control: one row per minute step (up to 1440), of which only the rows in
/// the scroll viewport are drawn, with their text shaped once and cached. The active row's fill changes on an 80ms
/// tween (Arc moves the highlight often from the keyboard, so it is almost instant) and the chosen time carries a
/// 6px dot, so only one row is ever highlighted. Pointer movement makes a row active; a click chooses it.
/// </summary>
internal sealed class TimePickerList : Control
{
    public static readonly StyledProperty<FontFamily> FontFamilyProperty = TextElement.FontFamilyProperty.AddOwner<TimePickerList>();
    public static readonly StyledProperty<double> FontSizeProperty = TextElement.FontSizeProperty.AddOwner<TimePickerList>();
    public static readonly StyledProperty<IBrush?> ForegroundProperty = TextElement.ForegroundProperty.AddOwner<TimePickerList>();

    public static readonly StyledProperty<IBrush?> HighlightBrushProperty =
        AvaloniaProperty.Register<TimePickerList, IBrush?>(nameof(HighlightBrush));

    public static readonly StyledProperty<double> RowHeightProperty =
        AvaloniaProperty.Register<TimePickerList, double>(nameof(RowHeight), 36);

    public static readonly StyledProperty<double> RowRadiusProperty =
        AvaloniaProperty.Register<TimePickerList, double>(nameof(RowRadius), 21);

    public static readonly StyledProperty<double> RowInsetProperty =
        AvaloniaProperty.Register<TimePickerList, double>(nameof(RowInset), 12);

    public static readonly StyledProperty<double> DotDiameterProperty =
        AvaloniaProperty.Register<TimePickerList, double>(nameof(DotDiameter), 6);

    public FontFamily FontFamily { get => GetValue(FontFamilyProperty); set => SetValue(FontFamilyProperty, value); }
    public double FontSize { get => GetValue(FontSizeProperty); set => SetValue(FontSizeProperty, value); }
    public IBrush? Foreground { get => GetValue(ForegroundProperty); set => SetValue(ForegroundProperty, value); }
    public IBrush? HighlightBrush { get => GetValue(HighlightBrushProperty); set => SetValue(HighlightBrushProperty, value); }
    public double RowHeight { get => GetValue(RowHeightProperty); set => SetValue(RowHeightProperty, value); }
    public double RowRadius { get => GetValue(RowRadiusProperty); set => SetValue(RowRadiusProperty, value); }
    public double RowInset { get => GetValue(RowInsetProperty); set => SetValue(RowInsetProperty, value); }
    public double DotDiameter { get => GetValue(DotDiameterProperty); set => SetValue(DotDiameterProperty, value); }

    private readonly Dictionary<int, FormattedText> _texts = new();
    private Func<int, string>? _label;
    private int _count;
    private MotionValue<double>? _activeFade;
    private MotionValue<double>? _previousFade;
    private int _previous = -1;
    private ScrollViewer? _scroll;

    static TimePickerList()
    {
        AffectsMeasure<TimePickerList>(RowHeightProperty);
        AffectsRender<TimePickerList>(HighlightBrushProperty, RowRadiusProperty, RowInsetProperty, DotDiameterProperty);
        ClipToBoundsProperty.OverrideDefaultValue<TimePickerList>(false);
    }

    public TimePickerList()
    {
        Cursor = new Cursor(StandardCursorType.Hand);
    }

    public int Count => _count;

    public int ActiveIndex { get; private set; } = -1;

    public int SelectedIndex { get; private set; } = -1;

    /// <summary>The pointer moved onto a row.</summary>
    public event EventHandler<int>? RowHovered;

    /// <summary>A row was clicked.</summary>
    public event EventHandler<int>? RowChosen;

    public void SetOptions(int count, Func<int, string> label)
    {
        _count = count;
        _label = label;
        _texts.Clear();
        ActiveIndex = Math.Min(ActiveIndex, count - 1);
        InvalidateMeasure();
        InvalidateVisual();
    }

    public string LabelAt(int index) => _label?.Invoke(index) ?? string.Empty;

    public void SetSelected(int index)
    {
        SelectedIndex = index;
        InvalidateVisual();
    }

    /// <summary>Moves the highlight; the old row fades out as the new one fades in (80ms).</summary>
    public void SetActive(int index, bool animate)
    {
        if (index == ActiveIndex)
        {
            return;
        }
        _previous = ActiveIndex;
        ActiveIndex = index;
        _activeFade ??= Create(1);
        _previousFade ??= Create(0);
        if (animate && IsLoaded && ArcMotion.GetEffectiveLevel(this) == ArcMotionLevel.Full)
        {
            var spec = MotionSpec.Tween(TimeSpan.FromMilliseconds(80), ArcEasings.Standard);
            _previousFade.Jump(_activeFade.Value);
            _activeFade.Jump(0);
            _ = _activeFade.AnimateTo(1, spec);
            _ = _previousFade.AnimateTo(0, spec);
        }
        else
        {
            _activeFade.Jump(1);
            _previousFade.Jump(0);
        }
        InvalidateVisual();
    }

    private MotionValue<double> Create(double initial)
    {
        var value = new MotionValue<double>(this, initial, MotionVectors.Double);
        value.Changed += _ => InvalidateVisual();
        return value;
    }

    public Rect RowRect(int index) => new(0, index * RowHeight, Bounds.Width, RowHeight);

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _scroll = this.FindAncestorOfType<ScrollViewer>();
        if (_scroll is not null)
        {
            _scroll.ScrollChanged += OnScrollChanged;
        }
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        if (_scroll is not null)
        {
            _scroll.ScrollChanged -= OnScrollChanged;
            _scroll = null;
        }
    }

    private void OnScrollChanged(object? sender, ScrollChangedEventArgs e) => InvalidateVisual();

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == FontFamilyProperty || change.Property == FontSizeProperty || change.Property == ForegroundProperty)
        {
            _texts.Clear();
            InvalidateVisual();
        }
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var width = double.IsInfinity(availableSize.Width) ? 0 : availableSize.Width;
        return new Size(width, _count * RowHeight);
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        var index = IndexAt(e.GetPosition(this));
        if (index >= 0 && index != ActiveIndex)
        {
            RowHovered?.Invoke(this, index);
        }
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            e.Handled = true;
        }
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        if (e.InitialPressMouseButton != MouseButton.Left)
        {
            return;
        }
        var index = IndexAt(e.GetPosition(this));
        if (index >= 0)
        {
            e.Handled = true;
            RowChosen?.Invoke(this, index);
        }
    }

    private int IndexAt(Point point)
    {
        if (point.Y < 0 || point.X < 0 || point.X > Bounds.Width)
        {
            return -1;
        }
        var index = (int)(point.Y / RowHeight);
        return index < _count ? index : -1;
    }

    private FormattedText TextAt(int index)
    {
        if (!_texts.TryGetValue(index, out var text))
        {
            text = new FormattedText(LabelAt(index), CultureInfo.CurrentCulture, FlowDirection.LeftToRight,
                new Typeface(FontFamily), FontSize, Foreground ?? Brushes.Black);
            _texts[index] = text;
        }
        return text;
    }

    public override void Render(DrawingContext context)
    {
        if (_count == 0 || Bounds.Width <= 0)
        {
            return;
        }
        // Transparent fill so the empty part of a row takes pointer input too.
        context.DrawRectangle(Brushes.Transparent, null, new Rect(Bounds.Size));
        // Only the rows inside the viewport are drawn.
        var top = 0.0;
        var height = Bounds.Height;
        if (_scroll is not null)
        {
            top = _scroll.Offset.Y;
            height = _scroll.Viewport.Height > 0 ? _scroll.Viewport.Height : height;
        }
        var first = Math.Max(0, (int)Math.Floor(top / RowHeight) - 1);
        var last = Math.Min(_count - 1, (int)Math.Ceiling((top + height) / RowHeight) + 1);
        var highlight = HighlightBrush;
        if (highlight is not null)
        {
            DrawHighlight(context, highlight, ActiveIndex, _activeFade?.Value ?? 1, first, last);
            DrawHighlight(context, highlight, _previous, _previousFade?.Value ?? 0, first, last);
        }
        for (var i = first; i <= last; i++)
        {
            var text = TextAt(i);
            var y = i * RowHeight + (RowHeight - text.Height) / 2;
            context.DrawText(text, new Point(RowInset, y));
        }
        if (SelectedIndex >= first && SelectedIndex <= last && Foreground is { } dot)
        {
            var r = DotDiameter / 2;
            context.DrawEllipse(dot, null, new Point(Bounds.Width - RowInset - r, SelectedIndex * RowHeight + RowHeight / 2), r, r);
        }
    }

    private void DrawHighlight(DrawingContext context, IBrush brush, int index, double opacity, int first, int last)
    {
        if (index < first || index > last || opacity <= 0.001)
        {
            return;
        }
        using (context.PushOpacity(Math.Clamp(opacity, 0, 1)))
        {
            context.DrawRectangle(brush, null, new RoundedRect(RowRect(index), RowRadius));
        }
    }
}
