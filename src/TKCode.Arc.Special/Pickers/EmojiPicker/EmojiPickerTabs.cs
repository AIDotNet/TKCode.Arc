using TKCode.Arc.Motion;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Automation;
using Avalonia.Input;
using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>
/// The category strip of an <see cref="EmojiPicker"/>: one glyph per section, spread across the width, with a short bar
/// under the section in view that glides to the next one. It is one tab stop: Left, Right, Home and End move between
/// categories and jump the grid there, Enter or Space jumps to the focused one, and a click jumps to the one clicked.
/// </summary>
internal sealed class EmojiPickerTabs : Control
{
    public static readonly StyledProperty<double> TabSizeProperty =
        AvaloniaProperty.Register<EmojiPickerTabs, double>(nameof(TabSize), 32);

    public static readonly StyledProperty<double> GlyphSizeProperty =
        AvaloniaProperty.Register<EmojiPickerTabs, double>(nameof(GlyphSize), 18);

    public static readonly StyledProperty<IBrush?> GlyphBrushProperty =
        AvaloniaProperty.Register<EmojiPickerTabs, IBrush?>(nameof(GlyphBrush));

    public static readonly StyledProperty<IBrush?> ActiveGlyphBrushProperty =
        AvaloniaProperty.Register<EmojiPickerTabs, IBrush?>(nameof(ActiveGlyphBrush));

    public static readonly StyledProperty<IBrush?> IndicatorBrushProperty =
        AvaloniaProperty.Register<EmojiPickerTabs, IBrush?>(nameof(IndicatorBrush));

    public static readonly StyledProperty<IBrush?> HoverFillProperty =
        AvaloniaProperty.Register<EmojiPickerTabs, IBrush?>(nameof(HoverFill));

    public static readonly StyledProperty<IBrush?> FocusRingProperty =
        AvaloniaProperty.Register<EmojiPickerTabs, IBrush?>(nameof(FocusRing));

    public static readonly StyledProperty<CornerRadius> TabCornerRadiusProperty =
        AvaloniaProperty.Register<EmojiPickerTabs, CornerRadius>(nameof(TabCornerRadius));

    private static readonly Dictionary<EmojiCategory, Geometry> s_glyphs = new()
    {
        [EmojiCategory.Recent] = ArcIcons.Clock,
        [EmojiCategory.Smileys] = ArcIcons.Smile,
        [EmojiCategory.People] = ArcIcons.Users,
        // Lucide "paw-print", "apple", "plane", "volleyball" (ISC license), outside Core's subset.
        [EmojiCategory.Nature] = ArcIcons.Parse("M9 4a2 2 0 1 0 4 0a2 2 0 1 0 -4 0z M16 8a2 2 0 1 0 4 0a2 2 0 1 0 -4 0z M18 16a2 2 0 1 0 4 0a2 2 0 1 0 -4 0z M9 10a5 5 0 0 1 5 5v3.5a3.5 3.5 0 0 1-6.84 1.045Q6.52 17.48 4.46 16.84A3.5 3.5 0 0 1 5.5 10Z"),
        [EmojiCategory.Food] = ArcIcons.Parse("M12 6.528V3a1 1 0 0 1 1-1h0 M18.237 21A15 15 0 0 0 22 11a6 6 0 0 0-10-4.472A6 6 0 0 0 2 11a15.1 15.1 0 0 0 3.763 10 3 3 0 0 0 3.648.648 5.5 5.5 0 0 1 5.178 0A3 3 0 0 0 18.237 21"),
        [EmojiCategory.Travel] = ArcIcons.Parse("M17.8 19.2 16 11l3.5-3.5C21 6 21.5 4 21 3c-1-.5-3 0-4.5 1.5L13 8 4.8 6.2c-.5-.1-.9.1-1.1.5l-.3.5c-.2.5-.1 1 .3 1.3L9 12l-2 3H4l-1 1 3 2 2 3 1-1v-3l3-2 3.5 5.3c.3.4.8.5 1.3.3l.5-.2c.4-.3.6-.7.5-1.2z"),
        [EmojiCategory.Activities] = ArcIcons.Parse("M11 7a16 16 20 0 1 10.98 4.362 M12 12a13 13 0 0 1-8.66 5 M16.83 13.634a16 16 0 0 1-9.267 7.328 M20.66 17A13 13 0 0 0 12 12a13 13 0 0 1 0-10 M8.17 15.366a16 16 0 0 1-1.713-11.69 M2 12a10 10 0 1 0 20 0a10 10 0 1 0 -20 0z"),
        [EmojiCategory.Objects] = ArcIcons.Lightbulb,
        [EmojiCategory.Symbols] = ArcIcons.Heart,
        [EmojiCategory.Flags] = ArcIcons.Flag,
    };

    private IReadOnlyList<EmojiCategory> _categories = Array.Empty<EmojiCategory>();
    private Func<EmojiCategory, string> _title = static c => c.ToString();
    private int _active;
    private int _focusIndex;
    private int _hover = -1;
    private MotionValue<double>? _indicator;
    private double _indicatorX = double.NaN;
    private Pen? _glyphPen;
    private Pen? _activePen;
    private Pen? _focusPen;

    static EmojiPickerTabs()
    {
        FocusableProperty.OverrideDefaultValue<EmojiPickerTabs>(true);
        AffectsMeasure<EmojiPickerTabs>(TabSizeProperty);
        AffectsRender<EmojiPickerTabs>(GlyphBrushProperty, ActiveGlyphBrushProperty, IndicatorBrushProperty, HoverFillProperty,
            FocusRingProperty, TabCornerRadiusProperty, GlyphSizeProperty);
    }

    public double TabSize { get => GetValue(TabSizeProperty); set => SetValue(TabSizeProperty, value); }

    public double GlyphSize { get => GetValue(GlyphSizeProperty); set => SetValue(GlyphSizeProperty, value); }

    public IBrush? GlyphBrush { get => GetValue(GlyphBrushProperty); set => SetValue(GlyphBrushProperty, value); }

    public IBrush? ActiveGlyphBrush { get => GetValue(ActiveGlyphBrushProperty); set => SetValue(ActiveGlyphBrushProperty, value); }

    public IBrush? IndicatorBrush { get => GetValue(IndicatorBrushProperty); set => SetValue(IndicatorBrushProperty, value); }

    public IBrush? HoverFill { get => GetValue(HoverFillProperty); set => SetValue(HoverFillProperty, value); }

    public IBrush? FocusRing { get => GetValue(FocusRingProperty); set => SetValue(FocusRingProperty, value); }

    public CornerRadius TabCornerRadius { get => GetValue(TabCornerRadiusProperty); set => SetValue(TabCornerRadiusProperty, value); }

    /// <summary>Raised with the category a click or key chose.</summary>
    public event Action<EmojiCategory>? Invoked;

    public IReadOnlyList<EmojiCategory> Categories => _categories;

    public EmojiCategory? ActiveCategory => _active >= 0 && _active < _categories.Count ? _categories[_active] : null;

    internal int FocusIndex => _focusIndex;

    public void SetCategories(IReadOnlyList<EmojiCategory> categories, Func<EmojiCategory, string> title)
    {
        var previous = ActiveCategory;
        _categories = categories;
        _title = title;
        _active = previous is { } p && IndexOf(p) is var i and >= 0 ? i : 0;
        _focusIndex = _active;
        _indicatorX = double.NaN;
        UpdateAutomation();
        InvalidateVisual();
    }

    /// <summary>Marks the category in view (the bar glides to it).</summary>
    public void SetActive(EmojiCategory category)
    {
        var index = IndexOf(category);
        if (index < 0 || index == _active)
        {
            return;
        }
        _active = index;
        if (!IsFocused)
        {
            _focusIndex = index;
        }
        MoveIndicator();
        UpdateAutomation();
        InvalidateVisual();
    }

    private int IndexOf(EmojiCategory category)
    {
        for (var i = 0; i < _categories.Count; i++)
        {
            if (_categories[i] == category)
            {
                return i;
            }
        }
        return -1;
    }

    private double Pitch => _categories.Count == 0 ? 0 : Bounds.Width / _categories.Count;

    private Rect TabRect(int index)
    {
        var pitch = Pitch;
        var size = Math.Min(TabSize, Bounds.Height);
        return new Rect(index * pitch + (pitch - size) / 2, (Bounds.Height - size) / 2, size, size);
    }

    private void MoveIndicator()
    {
        var target = TabRect(_active).X;
        if (double.IsNaN(_indicatorX) || !ArcMotion.IsFull(this) || !IsLoaded)
        {
            _indicator?.Jump(target);
            _indicatorX = target;
            InvalidateVisual();
            return;
        }
        if (_indicator is null)
        {
            // Code-driven: the bar is drawn by this control, so its position is a drawing state rather than a styled property.
            _indicator = new MotionValue<double>(this, _indicatorX, MotionVectors.Double);
            _indicator.Changed += v =>
            {
                _indicatorX = v;
                InvalidateVisual();
            };
        }
        _ = _indicator.AnimateTo(target, MotionSpec.Spring(ArcSpring.Snappy));
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var width = double.IsInfinity(availableSize.Width) ? TabSize * Math.Max(1, _categories.Count) : availableSize.Width;
        return new Size(width, TabSize + 4);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == BoundsProperty)
        {
            _indicator?.Stop();
            _indicatorX = double.NaN;
        }
        else if (change.Property == GlyphBrushProperty || change.Property == GlyphSizeProperty)
        {
            _glyphPen = null;
            _activePen = null;
        }
        else if (change.Property == ActiveGlyphBrushProperty)
        {
            _activePen = null;
        }
        else if (change.Property == FocusRingProperty)
        {
            _focusPen = null;
        }
    }

    public override void Render(DrawingContext context)
    {
        context.FillRectangle(Brushes.Transparent, new Rect(Bounds.Size));
        if (_categories.Count == 0)
        {
            return;
        }
        if (double.IsNaN(_indicatorX))
        {
            _indicatorX = TabRect(_active).X;
        }
        var focusVisible = IsFocused && Classes.Contains(":focus-visible");
        var scale = GlyphSize / 24;
        var thickness = 1.75 / Math.Max(0.01, scale);
        _glyphPen ??= new Pen(GlyphBrush ?? Brushes.Gray, thickness, lineCap: PenLineCap.Round, lineJoin: PenLineJoin.Round);
        _activePen ??= new Pen(ActiveGlyphBrush ?? GlyphBrush ?? Brushes.Black, thickness, lineCap: PenLineCap.Round, lineJoin: PenLineJoin.Round);
        for (var i = 0; i < _categories.Count; i++)
        {
            var rect = TabRect(i);
            if (i == _hover && HoverFill is { } hover)
            {
                context.DrawRectangle(hover, null, new RoundedRect(rect, TabCornerRadius));
            }
            if (focusVisible && i == _focusIndex && FocusRing is { } ring)
            {
                _focusPen ??= new Pen(ring, 2);
                context.DrawRectangle(null, _focusPen, new RoundedRect(rect, TabCornerRadius));
            }
            var matrix = Matrix.CreateTranslation(-12, -12) * Matrix.CreateScale(scale, scale) * Matrix.CreateTranslation(rect.Center.X, rect.Center.Y);
            using (context.PushTransform(matrix))
            {
                context.DrawGeometry(null, i == _active ? _activePen : _glyphPen, s_glyphs[_categories[i]]);
            }
        }
        if (IndicatorBrush is { } indicator)
        {
            var size = Math.Min(TabSize, Bounds.Height);
            var bar = new Rect(_indicatorX + size * 0.25, Bounds.Height - 2, size * 0.5, 2);
            context.DrawRectangle(indicator, null, new RoundedRect(bar, 1));
        }
    }

    private int IndexAt(Point point)
    {
        var pitch = Pitch;
        if (pitch <= 0)
        {
            return -1;
        }
        var index = (int)Math.Floor(point.X / pitch);
        return index >= 0 && index < _categories.Count ? index : -1;
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        var index = IndexAt(e.GetPosition(this));
        if (index != _hover)
        {
            _hover = index;
            InvalidateVisual();
        }
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        if (_hover >= 0)
        {
            _hover = -1;
            InvalidateVisual();
        }
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        var index = IndexAt(e.GetPosition(this));
        if (index >= 0 && e.InitialPressMouseButton == MouseButton.Left)
        {
            _focusIndex = index;
            Invoke(index);
            e.Handled = true;
        }
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Handled || _categories.Count == 0)
        {
            return;
        }
        var next = e.Key switch
        {
            Key.Left => (_focusIndex - 1 + _categories.Count) % _categories.Count,
            Key.Right => (_focusIndex + 1) % _categories.Count,
            Key.Home => 0,
            Key.End => _categories.Count - 1,
            Key.Enter or Key.Space => _focusIndex,
            _ => -1
        };
        if (next < 0)
        {
            return;
        }
        e.Handled = true;
        _focusIndex = next;
        Invoke(next);
    }

    private void Invoke(int index)
    {
        UpdateAutomation();
        InvalidateVisual();
        Invoked?.Invoke(_categories[index]);
    }

    protected override void OnGotFocus(FocusChangedEventArgs e)
    {
        base.OnGotFocus(e);
        _focusIndex = _active;
        UpdateAutomation();
        InvalidateVisual();
    }

    protected override void OnLostFocus(FocusChangedEventArgs e)
    {
        base.OnLostFocus(e);
        InvalidateVisual();
    }

    private void UpdateAutomation()
    {
        if (_focusIndex >= 0 && _focusIndex < _categories.Count)
        {
            AutomationProperties.SetName(this, _title(_categories[_focusIndex]));
        }
    }
}
