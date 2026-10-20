using TKCode.Arc.Motion;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Media;

namespace TKCode.Arc.Primitives;

/// <summary>
/// P16: a stroked 24×24 glyph that morphs to new path data instead of swapping (menu to close, play to pause, chart
/// marks). Set <see cref="Data"/> to SVG path data; changes after the first morph on <see cref="Spring"/>, and toggling
/// quickly reverses from the outline on screen. <see cref="StrokeThickness"/> is in viewBox units, so the stroke scales
/// with <see cref="Size"/> exactly like <see cref="ArcGlyph"/> (1.75 × size / 24).
/// </summary>
public class MorphGlyph : Control
{
    public static readonly StyledProperty<string?> DataProperty =
        AvaloniaProperty.Register<MorphGlyph, string?>(nameof(Data));

    public static readonly StyledProperty<double> SizeProperty =
        AvaloniaProperty.Register<MorphGlyph, double>(nameof(Size), 16);

    public static readonly StyledProperty<double> StrokeThicknessProperty =
        AvaloniaProperty.Register<MorphGlyph, double>(nameof(StrokeThickness), 1.75);

    /// <summary>Fills the outline with the foreground as well as stroking it (solid play / pause glyphs).</summary>
    public static readonly StyledProperty<bool> IsFilledProperty =
        AvaloniaProperty.Register<MorphGlyph, bool>(nameof(IsFilled));

    public static readonly StyledProperty<ArcSpring> SpringProperty =
        AvaloniaProperty.Register<MorphGlyph, ArcSpring>(nameof(Spring), ArcSpring.Morph);

    public static readonly StyledProperty<IBrush?> ForegroundProperty =
        TextElement.ForegroundProperty.AddOwner<MorphGlyph>();

    /// <summary>SVG path data in a 24×24 viewBox.</summary>
    public string? Data { get => GetValue(DataProperty); set => SetValue(DataProperty, value); }

    /// <summary>Rendered size in px (default 16).</summary>
    public double Size { get => GetValue(SizeProperty); set => SetValue(SizeProperty, value); }

    /// <summary>Stroke width in viewBox units (default 1.75).</summary>
    public double StrokeThickness { get => GetValue(StrokeThicknessProperty); set => SetValue(StrokeThicknessProperty, value); }

    public bool IsFilled { get => GetValue(IsFilledProperty); set => SetValue(IsFilledProperty, value); }

    /// <summary>Spring of the morph (default Morph).</summary>
    public ArcSpring Spring { get => GetValue(SpringProperty); set => SetValue(SpringProperty, value); }

    public IBrush? Foreground { get => GetValue(ForegroundProperty); set => SetValue(ForegroundProperty, value); }

    private PathMorph? _morph;
    private Geometry? _geometry;
    private Pen? _pen;
    private IBrush? _penBrush;
    private double _penThickness;

    static MorphGlyph()
    {
        AffectsMeasure<MorphGlyph>(SizeProperty);
        AffectsRender<MorphGlyph>(StrokeThicknessProperty, ForegroundProperty, IsFilledProperty);
        IsHitTestVisibleProperty.OverrideDefaultValue<MorphGlyph>(false);
    }

    /// <summary>The outline engine (created with the first shape).</summary>
    public PathMorph? Morph => _morph;

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == DataProperty && !_settingData)
        {
            Apply(change.GetNewValue<string?>(), animate: IsLoaded);
        }
    }

    /// <summary>Morphs (or jumps, with <paramref name="animate"/> false) to <paramref name="data"/>.</summary>
    public Task<bool> MorphTo(string data, bool animate = true)
    {
        _settingData = true;
        try
        {
            SetCurrentValue(DataProperty, data);
        }
        finally
        {
            _settingData = false;
        }
        Apply(data, animate && IsLoaded);
        return _lastMorph;
    }

    private Task<bool> _lastMorph = Task.FromResult(true);
    private bool _settingData;

    private void Apply(string? data, bool animate)
    {
        if (string.IsNullOrWhiteSpace(data))
        {
            _geometry = null;
            InvalidateVisual();
            return;
        }
        var shape = PathMorphShape.Parse(data);
        if (_morph is null)
        {
            _morph = new PathMorph(this);
            _morph.Changed += OnMorphChanged;
            _morph.Jump(shape);
            _lastMorph = Task.FromResult(true);
            return;
        }
        if (!animate)
        {
            _morph.Jump(shape);
            _lastMorph = Task.FromResult(true);
            return;
        }
        _lastMorph = _morph.MorphTo(shape, Spring);
    }

    private void OnMorphChanged()
    {
        _geometry = _morph?.BuildGeometry();
        InvalidateVisual();
    }

    protected override Size MeasureOverride(Size availableSize) => new(Size, Size);

    public override void Render(DrawingContext context)
    {
        var geometry = _geometry;
        var brush = Foreground;
        if (geometry is null || brush is null)
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
            context.DrawGeometry(IsFilled ? brush : null, _pen, geometry);
        }
    }
}
