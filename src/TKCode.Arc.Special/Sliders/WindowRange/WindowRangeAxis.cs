using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.TextFormatting;

namespace TKCode.Arc;

/// <summary>The tick labels under a <see cref="WindowRange"/>'s chart, each centered on its value.</summary>
internal sealed class WindowRangeAxis : Control
{
    public static readonly StyledProperty<IBrush?> ForegroundProperty =
        AvaloniaProperty.Register<WindowRangeAxis, IBrush?>(nameof(Foreground));

    public static readonly StyledProperty<FontFamily> FontFamilyProperty =
        AvaloniaProperty.Register<WindowRangeAxis, FontFamily>(nameof(FontFamily), FontFamily.Default);

    public static readonly StyledProperty<double> FontSizeProperty =
        AvaloniaProperty.Register<WindowRangeAxis, double>(nameof(FontSize), 12);

    public static readonly StyledProperty<double> LineHeightProperty =
        AvaloniaProperty.Register<WindowRangeAxis, double>(nameof(LineHeight), 16);

    private static readonly FontFeatureCollection s_tabular = new() { FontFeature.Parse("tnum") };

    private readonly List<(double Value, TextLayout Layout)> _labels = [];
    private IReadOnlyList<WindowRangeTick> _ticks = [];
    private bool _dirty = true;

    static WindowRangeAxis()
    {
        AffectsMeasure<WindowRangeAxis>(LineHeightProperty);
        AffectsRender<WindowRangeAxis>(ForegroundProperty, FontFamilyProperty, FontSizeProperty);
    }

    public IBrush? Foreground { get => GetValue(ForegroundProperty); set => SetValue(ForegroundProperty, value); }
    public FontFamily FontFamily { get => GetValue(FontFamilyProperty); set => SetValue(FontFamilyProperty, value); }
    public double FontSize { get => GetValue(FontSizeProperty); set => SetValue(FontSizeProperty, value); }
    public double LineHeight { get => GetValue(LineHeightProperty); set => SetValue(LineHeightProperty, value); }

    /// <summary>The control that maps values to x (its edge inset and domain).</summary>
    internal WindowRange? Owner { get; set; }

    internal IReadOnlyList<(double Value, TextLayout Layout)> Labels
    {
        get
        {
            Build();
            return _labels;
        }
    }

    public void SetTicks(IReadOnlyList<WindowRangeTick> ticks)
    {
        _ticks = ticks;
        _dirty = true;
        InvalidateVisual();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ForegroundProperty || change.Property == FontFamilyProperty || change.Property == FontSizeProperty ||
            change.Property == LineHeightProperty)
        {
            _dirty = true;
        }
    }

    private void Build()
    {
        if (!_dirty)
        {
            return;
        }
        _dirty = false;
        foreach (var (_, layout) in _labels)
        {
            layout.Dispose();
        }
        _labels.Clear();
        var typeface = new Typeface(FontFamily);
        foreach (var tick in _ticks)
        {
            var layout = new TextLayout(tick.Label, typeface, FontSize, Foreground, TextAlignment.Left, TextWrapping.NoWrap,
                TextTrimming.None, null, FlowDirection.LeftToRight, double.PositiveInfinity, double.PositiveInfinity,
                LineHeight, -0.01 * FontSize, 1, s_tabular);
            _labels.Add((tick.Value, layout));
        }
    }

    protected override Size MeasureOverride(Size availableSize) => new(0, LineHeight);

    public override void Render(DrawingContext context)
    {
        if (Owner is not { } owner)
        {
            return;
        }
        Build();
        foreach (var (value, layout) in _labels)
        {
            var x = owner.PixelOfValue(value, Bounds.Width);
            layout.Draw(context, new Point(x - layout.WidthIncludingTrailingWhitespace / 2, 0));
        }
    }
}
