using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Controls.Primitives;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using AvaloniaButton = Avalonia.Controls.Button;

namespace TKCode.Arc;

/// <summary>
/// The pause ring of an <see cref="AnnouncementBar"/>: a toggle button (pressed = paused) whose 20px ring fills
/// linearly over the interval and stops where it is while paused.
/// </summary>
internal sealed class AnnouncementBarRing : ToggleButton
{
    public static readonly StyledProperty<double> ProgressProperty =
        AvaloniaProperty.Register<AnnouncementBarRing, double>(nameof(Progress));

    public double Progress
    {
        get => GetValue(ProgressProperty);
        set => SetValue(ProgressProperty, value);
    }

    protected override Type StyleKeyOverride => typeof(AnnouncementBarRing);
}

/// <summary>Draws the ring track (22% opacity), the progress arc and the pause / play glyph (filled, 9px).</summary>
internal sealed class AnnouncementBarRingGlyph : Control
{
    public static readonly StyledProperty<double> ProgressProperty =
        AvaloniaProperty.Register<AnnouncementBarRingGlyph, double>(nameof(Progress));

    public static readonly StyledProperty<bool?> IsPausedProperty =
        AvaloniaProperty.Register<AnnouncementBarRingGlyph, bool?>(nameof(IsPaused));

    public static readonly StyledProperty<IBrush?> ForegroundProperty =
        TextElement.ForegroundProperty.AddOwner<AnnouncementBarRingGlyph>();

    public double Progress { get => GetValue(ProgressProperty); set => SetValue(ProgressProperty, value); }
    public bool? IsPaused { get => GetValue(IsPausedProperty); set => SetValue(IsPausedProperty, value); }
    public IBrush? Foreground { get => GetValue(ForegroundProperty); set => SetValue(ForegroundProperty, value); }

    private const double Radius = 8;
    private const double Stroke = 1.75;
    private Pen? _trackPen;
    private Pen? _fillPen;
    private Pen? _glyphPen;
    private IBrush? _brush;
    private DashStyle? _dash;
    private static readonly double Circumference = 2 * Math.PI * Radius;

    static AnnouncementBarRingGlyph()
    {
        AffectsRender<AnnouncementBarRingGlyph>(ProgressProperty, IsPausedProperty, ForegroundProperty);
        IsHitTestVisibleProperty.OverrideDefaultValue<AnnouncementBarRingGlyph>(false);
    }

    protected override Size MeasureOverride(Size availableSize) => new(20, 20);

    public override void Render(DrawingContext context)
    {
        var brush = Foreground;
        if (brush is null)
        {
            return;
        }
        if (!ReferenceEquals(brush, _brush) || _trackPen is null)
        {
            _brush = brush;
            var color = brush is ISolidColorBrush solid ? solid.Color : Colors.Gray;
            _trackPen = new Pen(new ImmutableSolidColorBrush(color, 0.22 * (brush.Opacity)), Stroke);
            // stroke-dasharray / stroke-dashoffset: one dash as long as the ring, shifted by the remaining part.
            _dash = new DashStyle(new[] { Circumference / Stroke, Circumference / Stroke }, 0);
            _fillPen = new Pen(brush, Stroke, _dash, lineCap: PenLineCap.Round);
            _glyphPen = new Pen(brush, 2.4 * 9 / 24, lineCap: PenLineCap.Round, lineJoin: PenLineJoin.Round);
        }
        var center = new Point(Bounds.Width / 2, Bounds.Height / 2);
        context.DrawEllipse(null, _trackPen, center, Radius, Radius);
        var p = Math.Clamp(Progress, 0, 1);
        if (p > 0.001)
        {
            if (p >= 0.999)
            {
                context.DrawEllipse(null, _fillPen, center, Radius, Radius);
            }
            else
            {
                _dash!.Offset = Circumference * (1 - p) / Stroke;
                // The ring starts at 12 o'clock (the SVG is rotated -90°).
                using (context.PushTransform(Matrix.CreateTranslation(-center.X, -center.Y) *
                                             Matrix.CreateRotation(-Math.PI / 2) *
                                             Matrix.CreateTranslation(center.X, center.Y)))
                {
                    context.DrawEllipse(null, _fillPen, center, Radius, Radius);
                }
            }
        }
        // 9px Lucide pause / play, filled with the stroke color.
        var glyph = IsPaused == true ? ArcIcons.Play : ArcIcons.Pause;
        var scale = 9.0 / 24.0;
        using (context.PushTransform(Matrix.CreateScale(scale, scale) * Matrix.CreateTranslation(center.X - 4.5, center.Y - 4.5)))
        {
            context.DrawGeometry(brush, _glyphPen, glyph);
        }
    }
}

/// <summary>The call to action of an announcement: inline text with an arrow that nudges 2px on hover.</summary>
internal sealed class AnnouncementBarLink : AvaloniaButton
{
    protected override Type StyleKeyOverride => typeof(AnnouncementBarLink);
}
