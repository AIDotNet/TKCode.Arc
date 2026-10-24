using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>
/// The block's check mark: a two-stroke tick in a 24 × 24 box, from (4, 12.5) down to (9, 17.5) and up to (20, 6.5), with
/// round caps and joins. <see cref="Progress"/> draws it from the start of the short stroke (0 nothing, 1 whole). Drawn as
/// two lines with a cached pen, so a running draw allocates nothing per frame.
/// </summary>
internal sealed class MultiStepCheck : Control
{
    /// <summary>Defines the <see cref="Progress"/> property.</summary>
    public static readonly StyledProperty<double> ProgressProperty =
        AvaloniaProperty.Register<MultiStepCheck, double>(nameof(Progress), 1);

    /// <summary>Defines the <see cref="StrokeThickness"/> property.</summary>
    public static readonly StyledProperty<double> StrokeThicknessProperty =
        AvaloniaProperty.Register<MultiStepCheck, double>(nameof(StrokeThickness), 2);

    /// <summary>Defines the <see cref="Foreground"/> property.</summary>
    public static readonly StyledProperty<IBrush?> ForegroundProperty =
        TextElement.ForegroundProperty.AddOwner<MultiStepCheck>();

    private static readonly Point s_start = new(4, 12.5);
    private static readonly Point s_corner = new(9, 17.5);
    private static readonly Point s_end = new(20, 6.5);

    /// <summary>Length of the short stroke and of the whole tick, in box units.</summary>
    internal static readonly double ShortLength = Distance(s_start, s_corner);
    internal static readonly double TotalLength = ShortLength + Distance(s_corner, s_end);

    private Pen? _pen;
    private IBrush? _penBrush;
    private double _penThickness;

    static MultiStepCheck()
    {
        AffectsRender<MultiStepCheck>(ProgressProperty, StrokeThicknessProperty, ForegroundProperty);
        IsHitTestVisibleProperty.OverrideDefaultValue<MultiStepCheck>(false);
    }

    /// <summary>How much of the tick is drawn, 0 to 1.</summary>
    public double Progress { get => GetValue(ProgressProperty); set => SetValue(ProgressProperty, value); }

    /// <summary>Stroke width in box units (24 = the control's size).</summary>
    public double StrokeThickness { get => GetValue(StrokeThicknessProperty); set => SetValue(StrokeThicknessProperty, value); }

    /// <summary>The stroke color.</summary>
    public IBrush? Foreground { get => GetValue(ForegroundProperty); set => SetValue(ForegroundProperty, value); }

    private static double Distance(Point a, Point b) => Math.Sqrt((b.X - a.X) * (b.X - a.X) + (b.Y - a.Y) * (b.Y - a.Y));

    private static Point Lerp(Point a, Point b, double t) => new(a.X + (b.X - a.X) * t, a.Y + (b.Y - a.Y) * t);

    public override void Render(DrawingContext context)
    {
        var brush = Foreground;
        var progress = Math.Clamp(Progress, 0, 1);
        if (brush is null || progress <= 0 || Bounds.Width <= 0 || Bounds.Height <= 0)
        {
            return;
        }
        var scale = Math.Min(Bounds.Width, Bounds.Height) / 24.0;
        var thickness = StrokeThickness * scale;
        if (_pen is null || !ReferenceEquals(_penBrush, brush) || Math.Abs(_penThickness - thickness) > 1e-6)
        {
            _pen = new Pen(brush, thickness, lineCap: PenLineCap.Round, lineJoin: PenLineJoin.Round);
            _penBrush = brush;
            _penThickness = thickness;
        }
        var offset = new Point((Bounds.Width - 24 * scale) / 2, (Bounds.Height - 24 * scale) / 2);
        Point Map(Point p) => new(offset.X + p.X * scale, offset.Y + p.Y * scale);
        var drawn = progress * TotalLength;
        if (drawn <= ShortLength)
        {
            context.DrawLine(_pen, Map(s_start), Map(Lerp(s_start, s_corner, drawn / ShortLength)));
            return;
        }
        context.DrawLine(_pen, Map(s_start), Map(s_corner));
        var t = (drawn - ShortLength) / (TotalLength - ShortLength);
        context.DrawLine(_pen, Map(s_corner), Map(Lerp(s_corner, s_end, t)));
    }
}
