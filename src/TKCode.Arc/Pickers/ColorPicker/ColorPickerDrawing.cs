using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Immutable;

namespace TKCode.Arc;

/// <summary>
/// Shared drawing of the color picker: the checkerboard that makes opacity read as opacity (10px tiles of 5px
/// squares, Arc's repeating conic gradient), the 22px thumb (3px white ring, soft drop shadow, 1px inner line) and the
/// chips' hairline.
/// </summary>
internal static class ColorPickerDrawing
{
    public const double ThumbRadius = 11;

    private static readonly Dictionary<(Color, Color), IBrush> s_checkers = new();

    /// <summary>White 3px ring plus <c>0 2px 8px oklch(0 0 0 / .3)</c>.</summary>
    public static readonly BoxShadows ThumbShadows = new(
        new BoxShadow { Spread = 3, Color = Colors.White },
        [new BoxShadow { OffsetY = 2, Blur = 8, Color = Color.FromArgb(77, 0, 0, 0) }]);

    /// <summary><c>inset 0 0 0 1px oklch(0 0 0 / .12)</c> of the thumb.</summary>
    public static readonly IPen ThumbLine = new ImmutablePen(new ImmutableSolidColorBrush(Color.FromArgb(31, 0, 0, 0)), 1);

    /// <summary><c>inset 0 0 0 1px oklch(0 0 0 / .1)</c> of chips.</summary>
    public static readonly IPen ChipLine = new ImmutablePen(new ImmutableSolidColorBrush(Color.FromArgb(26, 0, 0, 0)), 1);

    /// <summary><c>inset 0 0 0 1px oklch(0 0 0 / .08)</c> of the area and tracks.</summary>
    public static readonly IPen TrackLine = new ImmutablePen(new ImmutableSolidColorBrush(Color.FromArgb(20, 0, 0, 0)), 1);

    /// <summary>The checkerboard from two token brushes (light-theme defaults when unset).</summary>
    public static IBrush Checker(IBrush? first, IBrush? second) => Checker(
        first is ISolidColorBrush a ? a.Color : Color.FromRgb(0xD9, 0xD9, 0xD9),
        second is ISolidColorBrush b ? b.Color : Colors.White);

    /// <summary>A tiled checkerboard: <paramref name="first"/> on the top-right and bottom-left squares.</summary>
    public static IBrush Checker(Color first, Color second)
    {
        if (s_checkers.TryGetValue((first, second), out var brush))
        {
            return brush;
        }
        var squares = new GeometryGroup
        {
            Children =
            {
                new RectangleGeometry(new Rect(5, 0, 5, 5)),
                new RectangleGeometry(new Rect(0, 5, 5, 5))
            }
        };
        var drawing = new DrawingGroup
        {
            Children =
            {
                new GeometryDrawing { Brush = new ImmutableSolidColorBrush(second), Geometry = new RectangleGeometry(new Rect(0, 0, 10, 10)) },
                new GeometryDrawing { Brush = new ImmutableSolidColorBrush(first), Geometry = squares }
            }
        };
        brush = new DrawingBrush(drawing)
        {
            TileMode = TileMode.Tile,
            Stretch = Stretch.None,
            AlignmentX = AlignmentX.Left,
            AlignmentY = AlignmentY.Top,
            DestinationRect = new RelativeRect(0, 0, 10, 10, RelativeUnit.Absolute),
            SourceRect = new RelativeRect(0, 0, 10, 10, RelativeUnit.Absolute)
        };
        s_checkers[(first, second)] = brush;
        return brush;
    }

    /// <summary>Draws a thumb centred on <paramref name="center"/>; <paramref name="checker"/> goes under translucent fills.</summary>
    public static void DrawThumb(DrawingContext context, Point center, double scale, IBrush fill, IBrush? checker, IPen? focusRing)
    {
        var r = ThumbRadius * scale;
        var rect = new Rect(center.X - r, center.Y - r, r * 2, r * 2);
        var round = new RoundedRect(rect, r);
        context.DrawRectangle(checker ?? fill, null, round, ThumbShadows);
        if (checker is not null)
        {
            context.DrawEllipse(fill, null, center, r, r);
        }
        context.DrawEllipse(null, ThumbLine, center, r - 0.5, r - 0.5);
        if (focusRing is not null)
        {
            context.DrawEllipse(null, focusRing, center, r + 5, r + 5);
        }
    }

    /// <summary>A round chip: checkerboard, the color, and a hairline.</summary>
    public static void DrawChip(DrawingContext context, Point center, double radius, IBrush fill, IBrush checker)
    {
        context.DrawEllipse(checker, null, center, radius, radius);
        context.DrawEllipse(fill, null, center, radius, radius);
        context.DrawEllipse(null, ChipLine, center, radius - 0.5, radius - 0.5);
    }
}

/// <summary>A round color chip over a checkerboard (trigger, panel head).</summary>
internal sealed class ColorPickerChip : Control
{
    public static readonly StyledProperty<Color> ColorProperty =
        AvaloniaProperty.Register<ColorPickerChip, Color>(nameof(Color));

    /// <summary>The checkerboard's top-right / bottom-left squares (token brush).</summary>
    public static readonly StyledProperty<IBrush?> CheckerColorProperty =
        AvaloniaProperty.Register<ColorPickerChip, IBrush?>(nameof(CheckerColor));

    /// <summary>The checkerboard's other squares (token brush).</summary>
    public static readonly StyledProperty<IBrush?> CheckerBaseProperty =
        AvaloniaProperty.Register<ColorPickerChip, IBrush?>(nameof(CheckerBase));

    public Color Color { get => GetValue(ColorProperty); set => SetValue(ColorProperty, value); }
    public IBrush? CheckerColor { get => GetValue(CheckerColorProperty); set => SetValue(CheckerColorProperty, value); }
    public IBrush? CheckerBase { get => GetValue(CheckerBaseProperty); set => SetValue(CheckerBaseProperty, value); }

    private ImmutableSolidColorBrush? _fill;

    static ColorPickerChip()
    {
        AffectsRender<ColorPickerChip>(ColorProperty, CheckerColorProperty, CheckerBaseProperty);
    }

    public override void Render(DrawingContext context)
    {
        var size = Bounds.Size;
        var r = Math.Min(size.Width, size.Height) / 2;
        ColorPickerDrawing.DrawChip(context, new Point(size.Width / 2, size.Height / 2), r,
            PickerDrawing.Brush(ref _fill, Color), ColorPickerDrawing.Checker(CheckerColor, CheckerBase));
    }
}

/// <summary>Arc's add-swatch ring: a 1px dashed circle (Avalonia's Border has no dashed stroke).</summary>
internal sealed class ColorPickerDashedRing : Control
{
    public static readonly StyledProperty<IBrush?> StrokeProperty =
        AvaloniaProperty.Register<ColorPickerDashedRing, IBrush?>(nameof(Stroke));

    public IBrush? Stroke { get => GetValue(StrokeProperty); set => SetValue(StrokeProperty, value); }

    private IPen? _pen;
    private IBrush? _penBrush;

    static ColorPickerDashedRing()
    {
        AffectsRender<ColorPickerDashedRing>(StrokeProperty);
        IsHitTestVisibleProperty.OverrideDefaultValue<ColorPickerDashedRing>(false);
    }

    public override void Render(DrawingContext context)
    {
        if (Stroke is not { } stroke)
        {
            return;
        }
        if (_pen is null || !ReferenceEquals(_penBrush, stroke))
        {
            _penBrush = stroke;
            _pen = new Pen(stroke, 1, new DashStyle([3, 2.5], 0));
        }
        var size = Bounds.Size;
        var r = Math.Min(size.Width, size.Height) / 2 - 0.5;
        context.DrawEllipse(null, _pen, new Point(size.Width / 2, size.Height / 2), r, r);
    }
}
