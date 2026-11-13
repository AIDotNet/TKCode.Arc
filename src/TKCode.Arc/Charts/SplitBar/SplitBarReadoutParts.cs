using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using TKCode.Arc.Primitives;

namespace TKCode.Arc;

/// <summary>
/// The header of a <see cref="SplitBar"/>: the heading (title and description) on the left and the readout on the right,
/// both on the heading's last line. When they do not fit side by side the readout wraps under the heading, still on the right.
/// </summary>
internal sealed class SplitBarHeadPanel : Panel
{
    internal static readonly StyledProperty<double> ColumnGapProperty =
        AvaloniaProperty.Register<SplitBarHeadPanel, double>(nameof(ColumnGap), 16);

    internal static readonly StyledProperty<double> RowGapProperty =
        AvaloniaProperty.Register<SplitBarHeadPanel, double>(nameof(RowGap), 8);

    static SplitBarHeadPanel()
    {
        AffectsMeasure<SplitBarHeadPanel>(ColumnGapProperty, RowGapProperty);
    }

    public double ColumnGap { get => GetValue(ColumnGapProperty); set => SetValue(ColumnGapProperty, value); }

    public double RowGap { get => GetValue(RowGapProperty); set => SetValue(RowGapProperty, value); }

    private bool _wrapped;

    protected override Size MeasureOverride(Size availableSize)
    {
        var heading = Children.Count > 0 ? Children[0] : null;
        var readout = Children.Count > 1 ? Children[1] : null;
        var infinite = new Size(double.PositiveInfinity, availableSize.Height);
        readout?.Measure(infinite);
        var r = readout is { IsVisible: true } ? readout.DesiredSize : default;
        heading?.Measure(infinite);
        var h = heading is { IsVisible: true } ? heading.DesiredSize : default;
        var width = availableSize.Width;
        _wrapped = h.Width > 0 && r.Width > 0 && h.Width + ColumnGap + r.Width > width;
        if (_wrapped)
        {
            heading!.Measure(new Size(width, availableSize.Height));
            h = heading.DesiredSize;
            return new Size(Math.Max(h.Width, r.Width), h.Height + RowGap + r.Height);
        }
        var w = h.Width + r.Width + (h.Width > 0 && r.Width > 0 ? ColumnGap : 0);
        return new Size(w, Math.Max(h.Height, r.Height));
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var heading = Children.Count > 0 ? Children[0] : null;
        var readout = Children.Count > 1 ? Children[1] : null;
        var h = heading is { IsVisible: true } ? heading.DesiredSize : default;
        var r = readout is { IsVisible: true } ? readout.DesiredSize : default;
        if (_wrapped)
        {
            heading?.Arrange(new Rect(0, 0, finalSize.Width, h.Height));
            readout?.Arrange(new Rect(finalSize.Width - r.Width, h.Height + RowGap, r.Width, r.Height));
        }
        else
        {
            var height = finalSize.Height;
            heading?.Arrange(new Rect(0, height - h.Height, Math.Max(0, finalSize.Width - r.Width - ColumnGap), h.Height));
            readout?.Arrange(new Rect(finalSize.Width - r.Width, height - r.Height, r.Width, r.Height));
        }
        return finalSize;
    }
}

/// <summary>
/// The readout of a <see cref="SplitBar"/>: its lines (the total, and two that take turns showing the highlighted part)
/// lie over each other, right-aligned; the panel is as wide as the line that shows now.
/// </summary>
internal sealed class SplitBarReadout : Panel
{
    private int _current;

    public int Current
    {
        get => _current;
        set
        {
            if (_current != value)
            {
                _current = value;
                InvalidateMeasure();
            }
        }
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var infinite = new Size(double.PositiveInfinity, availableSize.Height);
        double height = 0;
        var width = 0.0;
        for (var i = 0; i < Children.Count; i++)
        {
            var child = Children[i];
            child.Measure(infinite);
            height = Math.Max(height, child.DesiredSize.Height);
            if (i == _current)
            {
                width = child.DesiredSize.Width;
            }
        }
        return new Size(width, height);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        foreach (var child in Children)
        {
            var size = child.DesiredSize;
            child.Arrange(new Rect(finalSize.Width - size.Width, finalSize.Height - size.Height, size.Width, size.Height));
        }
        return finalSize;
    }
}

/// <summary>A readout line for the highlighted part: its color, name, share and value.</summary>
[TemplatePart("PART_Swatch", typeof(SplitBarSwatch))]
[TemplatePart("PART_Name", typeof(TextBlock))]
[TemplatePart("PART_Share", typeof(RollingDigits))]
[TemplatePart("PART_Value", typeof(RollingDigits))]
internal sealed class SplitBarReadoutLine : TemplatedControl
{
    private SplitBarSwatch? _swatch;
    private TextBlock? _name;
    private RollingDigits? _share;
    private RollingDigits? _value;

    /// <summary>The name shown (tests).</summary>
    public string? NameText => _name?.Text;

    /// <summary>The share the digits roll to (0 to 100) and the value (tests).</summary>
    public double ShareValue => _share?.Value ?? 0;

    public double ValueValue => _value?.Value ?? 0;

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _swatch = e.NameScope.Find<SplitBarSwatch>("PART_Swatch");
        _name = e.NameScope.Find<TextBlock>("PART_Name");
        _share = e.NameScope.Find<RollingDigits>("PART_Share");
        _value = e.NameScope.Find<RollingDigits>("PART_Value");
        if (_share is not null)
        {
            _share.WheelMotion = SplitBar.DigitMotion;
        }
        if (_value is not null)
        {
            _value.WheelMotion = SplitBar.DigitMotion;
        }
    }

    public void Show(SplitBar owner, SplitBarSlot slot, bool instant)
    {
        ApplyTemplate();
        if (_swatch is not null)
        {
            _swatch.Brush = owner.Track?.CapBrush(slot);
        }
        if (_name is not null)
        {
            _name.Text = slot.Segment.Label;
        }
        if (_share is not null)
        {
            _share.IsInstant = instant;
            _share.Value = Math.Round(slot.BaseShare * 1000) / 10;
        }
        if (_value is not null)
        {
            _value.Decimals = owner.ValueDecimals;
            _value.Prefix = owner.ValuePrefix;
            _value.Suffix = string.IsNullOrEmpty(owner.Unit) ? owner.ValueSuffix : owner.ValueSuffix + " " + owner.Unit;
            _value.IsInstant = instant;
            _value.Value = Math.Max(0, slot.Segment.Value);
        }
    }
}

/// <summary>
/// A color swatch: the part's fill (its color mixed into the surface) with a solid strip at the end, as the part is drawn
/// in the bar; solid (the readout); or a hollow ring for a part that is off and for the empty rest.
/// </summary>
internal sealed class SplitBarSwatch : Control
{
    internal static readonly StyledProperty<IBrush?> BrushProperty =
        AvaloniaProperty.Register<SplitBarSwatch, IBrush?>(nameof(Brush));

    internal static readonly StyledProperty<IBrush?> SurfaceProperty =
        AvaloniaProperty.Register<SplitBarSwatch, IBrush?>(nameof(Surface));

    internal static readonly StyledProperty<IBrush?> RingBrushProperty =
        AvaloniaProperty.Register<SplitBarSwatch, IBrush?>(nameof(RingBrush));

    internal static readonly StyledProperty<double> SizeProperty =
        AvaloniaProperty.Register<SplitBarSwatch, double>(nameof(Size), 12);

    internal static readonly StyledProperty<double> CapWidthProperty =
        AvaloniaProperty.Register<SplitBarSwatch, double>(nameof(CapWidth), 3);

    internal static readonly StyledProperty<double> FillMixProperty =
        AvaloniaProperty.Register<SplitBarSwatch, double>(nameof(FillMix), 0.42);

    internal static readonly StyledProperty<CornerRadius> CornerRadiusProperty =
        AvaloniaProperty.Register<SplitBarSwatch, CornerRadius>(nameof(CornerRadius));

    internal static readonly StyledProperty<bool> IsSolidProperty =
        AvaloniaProperty.Register<SplitBarSwatch, bool>(nameof(IsSolid));

    internal static readonly StyledProperty<bool> IsHollowProperty =
        AvaloniaProperty.Register<SplitBarSwatch, bool>(nameof(IsHollow));

    private IBrush? _fill;
    private IBrush? _fillFor;
    private IBrush? _fillSurface;
    private double _fillMix = double.NaN;
    private IPen? _ring;
    private IBrush? _ringFor;

    static SplitBarSwatch()
    {
        AffectsRender<SplitBarSwatch>(BrushProperty, SurfaceProperty, RingBrushProperty, CapWidthProperty, FillMixProperty,
            CornerRadiusProperty, IsSolidProperty, IsHollowProperty);
        AffectsMeasure<SplitBarSwatch>(SizeProperty);
    }

    public IBrush? Brush { get => GetValue(BrushProperty); set => SetValue(BrushProperty, value); }

    public IBrush? Surface { get => GetValue(SurfaceProperty); set => SetValue(SurfaceProperty, value); }

    public IBrush? RingBrush { get => GetValue(RingBrushProperty); set => SetValue(RingBrushProperty, value); }

    public double Size { get => GetValue(SizeProperty); set => SetValue(SizeProperty, value); }

    public double CapWidth { get => GetValue(CapWidthProperty); set => SetValue(CapWidthProperty, value); }

    public double FillMix { get => GetValue(FillMixProperty); set => SetValue(FillMixProperty, value); }

    public CornerRadius CornerRadius { get => GetValue(CornerRadiusProperty); set => SetValue(CornerRadiusProperty, value); }

    public bool IsSolid { get => GetValue(IsSolidProperty); set => SetValue(IsSolidProperty, value); }

    public bool IsHollow { get => GetValue(IsHollowProperty); set => SetValue(IsHollowProperty, value); }

    protected override Size MeasureOverride(Size availableSize) => new(Size, Size);

    public override void Render(DrawingContext context)
    {
        var rect = new Rect(0, 0, Size, Size);
        var radius = Math.Min(CornerRadius.TopLeft, Size / 2);
        if (IsHollow)
        {
            if (RingBrush is { } ringBrush)
            {
                if (!ReferenceEquals(ringBrush, _ringFor))
                {
                    _ringFor = ringBrush;
                    _ring = new ImmutablePen(ringBrush.ToImmutable(), 1.5);
                }
                // An inset ring: the stroke sits inside the square.
                context.DrawRectangle(null, _ring, rect.Deflate(0.75), Math.Max(0, radius - 0.75), Math.Max(0, radius - 0.75));
            }
            return;
        }
        if (Brush is not { } brush)
        {
            return;
        }
        if (IsSolid)
        {
            context.DrawRectangle(brush, null, rect, radius, radius);
            return;
        }
        context.DrawRectangle(FillBrush(brush), null, rect, radius, radius);
        using (context.PushClip(new RoundedRect(rect, radius)))
        {
            context.FillRectangle(brush, new Rect(rect.Right - CapWidth, 0, CapWidth, Size));
        }
    }

    private IBrush FillBrush(IBrush brush)
    {
        if (_fill is null || !ReferenceEquals(brush, _fillFor) || !ReferenceEquals(Surface, _fillSurface) || _fillMix != FillMix)
        {
            _fillFor = brush;
            _fillSurface = Surface;
            _fillMix = FillMix;
            _fill = SplitBarColors.Mix(brush, Surface, FillMix);
        }
        return _fill;
    }
}

/// <summary>Color helpers of the split bar.</summary>
internal static class SplitBarColors
{
    /// <summary>
    /// The chart series colors of the default palette as they are specified, in OKLCH, before sRGB gamut mapping. Arc mixes
    /// the specified color, which is more saturated than the mapped one for orange and teal, so the fills are mixed from it.
    /// </summary>
    private static readonly Dictionary<uint, (double L, double C, double H)> s_specified = new()
    {
        [0xFF55ADFF] = (0.73, 0.15, 250),
        [0xFF9C37BE] = (0.54, 0.21, 316),
        [0xFFE66E00] = (0.665, 0.21, 65),
        [0xFF00A861] = (0.63, 0.18, 160),
        [0xFF1F7DCF] = (0.58, 0.15, 250),
        [0xFF7128A5] = (0.45, 0.19, 307),
        [0xFFE58F00] = (0.72, 0.18, 74),
        [0xFF009E84] = (0.58, 0.21, 182),
    };

    /// <summary>
    /// The fill of a part: <paramref name="mix"/> of its color over the surface, mixed in OKLab like Arc's
    /// <c>color-mix</c>, channels clipped to sRGB. A brush that is not a solid color is drawn at that opacity instead.
    /// </summary>
    public static IBrush Mix(IBrush color, IBrush? surface, double mix)
    {
        if (color is ISolidColorBrush solid && surface is ISolidColorBrush ground)
        {
            var c = solid.Color;
            var (l1, a1, b1) = ToOklab(ground.Color);
            double l2, a2, b2;
            if (s_specified.TryGetValue(c.ToUInt32(), out var lch))
            {
                var h = lch.H * Math.PI / 180;
                (l2, a2, b2) = (lch.L, lch.C * Math.Cos(h), lch.C * Math.Sin(h));
            }
            else
            {
                (l2, a2, b2) = ToOklab(c);
            }
            var alpha = solid.Opacity * c.A / 255.0;
            var t = mix * alpha;
            return new ImmutableSolidColorBrush(FromOklab(l1 + (l2 - l1) * t, a1 + (a2 - a1) * t, b1 + (b2 - b1) * t));
        }
        if (color is ISolidColorBrush only)
        {
            return new ImmutableSolidColorBrush(only.Color, mix * only.Opacity);
        }
        return color;
    }

    private static (double L, double A, double B) ToOklab(Color color)
    {
        var r = Decode(color.R / 255.0);
        var g = Decode(color.G / 255.0);
        var b = Decode(color.B / 255.0);
        var l = Math.Cbrt(0.4122214708 * r + 0.5363325363 * g + 0.0514459929 * b);
        var m = Math.Cbrt(0.2119034982 * r + 0.6806995451 * g + 0.1073969566 * b);
        var s = Math.Cbrt(0.0883024619 * r + 0.2817188376 * g + 0.6299787005 * b);
        return (0.2104542553 * l + 0.7936177850 * m - 0.0040720468 * s,
            1.9779984951 * l - 2.4285922050 * m + 0.4505937099 * s,
            0.0259040371 * l + 0.7827717662 * m - 0.8086757660 * s);
    }

    private static Color FromOklab(double lightness, double a, double b)
    {
        var l = Math.Pow(lightness + 0.3963377774 * a + 0.2158037573 * b, 3);
        var m = Math.Pow(lightness - 0.1055613458 * a - 0.0638541728 * b, 3);
        var s = Math.Pow(lightness - 0.0894841775 * a - 1.2914855480 * b, 3);
        var r = 4.0767416621 * l - 3.3077115913 * m + 0.2309699292 * s;
        var g = -1.2684380046 * l + 2.6097574011 * m - 0.3413193965 * s;
        var bl = -0.0041960863 * l - 0.7034186147 * m + 1.7076147010 * s;
        return Color.FromRgb(Encode(r), Encode(g), Encode(bl));
    }

    private static double Decode(double v) => v <= 0.04045 ? v / 12.92 : Math.Pow((v + 0.055) / 1.055, 2.4);

    private static byte Encode(double v)
    {
        v = Math.Clamp(v, 0, 1);
        var e = v <= 0.0031308 ? 12.92 * v : 1.055 * Math.Pow(v, 1 / 2.4) - 0.055;
        return (byte)Math.Round(Math.Clamp(e, 0, 1) * 255);
    }
}
