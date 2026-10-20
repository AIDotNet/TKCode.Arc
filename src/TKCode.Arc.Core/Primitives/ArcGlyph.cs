using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>
/// Draws a stroked Lucide glyph (24x24 viewBox). <see cref="StrokeThickness"/> is in viewBox units, so the
/// rendered stroke scales with <see cref="Size"/> exactly like lucide-react without absoluteStrokeWidth.
/// </summary>
public class ArcGlyph : Control
{
    public static readonly StyledProperty<Geometry?> DataProperty =
        AvaloniaProperty.Register<ArcGlyph, Geometry?>(nameof(Data));

    public static readonly StyledProperty<double> SizeProperty =
        AvaloniaProperty.Register<ArcGlyph, double>(nameof(Size), 16);

    public static readonly StyledProperty<double> StrokeThicknessProperty =
        AvaloniaProperty.Register<ArcGlyph, double>(nameof(StrokeThickness), 1.75);

    public static readonly StyledProperty<IBrush?> ForegroundProperty =
        TextElement.ForegroundProperty.AddOwner<ArcGlyph>();

    static ArcGlyph()
    {
        AffectsMeasure<ArcGlyph>(SizeProperty);
        AffectsRender<ArcGlyph>(DataProperty, StrokeThicknessProperty, ForegroundProperty);
        IsHitTestVisibleProperty.OverrideDefaultValue<ArcGlyph>(false);
    }

    public Geometry? Data
    {
        get => GetValue(DataProperty);
        set => SetValue(DataProperty, value);
    }

    public double Size
    {
        get => GetValue(SizeProperty);
        set => SetValue(SizeProperty, value);
    }

    public double StrokeThickness
    {
        get => GetValue(StrokeThicknessProperty);
        set => SetValue(StrokeThicknessProperty, value);
    }

    public IBrush? Foreground
    {
        get => GetValue(ForegroundProperty);
        set => SetValue(ForegroundProperty, value);
    }

    private Pen? _pen;
    private IBrush? _penBrush;
    private double _penThickness;

    protected override Size MeasureOverride(Size availableSize) => new(Size, Size);

    public override void Render(DrawingContext context)
    {
        var data = Data;
        var brush = Foreground;
        if (data is null || brush is null)
        {
            return;
        }
        if (_pen is null || !ReferenceEquals(_penBrush, brush) || Math.Abs(_penThickness - StrokeThickness) > double.Epsilon)
        {
            _pen = new Pen(brush, StrokeThickness, lineCap: PenLineCap.Round, lineJoin: PenLineJoin.Round);
            _penBrush = brush;
            _penThickness = StrokeThickness;
        }
        var scale = Math.Min(Bounds.Width, Bounds.Height) / 24.0;
        var offsetX = (Bounds.Width - 24 * scale) / 2;
        var offsetY = (Bounds.Height - 24 * scale) / 2;
        using (context.PushTransform(Matrix.CreateScale(scale, scale) * Matrix.CreateTranslation(offsetX, offsetY)))
        {
            context.DrawGeometry(null, _pen, data);
        }
    }
}
