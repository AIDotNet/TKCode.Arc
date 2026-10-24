using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using TKCode.Arc.Gestures;

namespace TKCode.Arc;

/// <summary>
/// The canvas's dot grid (Arc HeroRelay <c>.hero::before</c>: a 1px dot every 22px). The dots sit on the workflow's
/// coordinates, so they pan and spread with the view; their radius stays one screen pixel, and they hide when zooming out
/// would crowd them. One dot is drawn into a tile one step wide (again only when the step changes) and an image brush
/// tiles it over the canvas: a geometry of every dot on screen (about 11,000 at 40%) cost a rebuild and a bounds pass on
/// every frame of a zoom.
/// </summary>
internal sealed class WorkflowGridLayer : Control
{
    public static readonly StyledProperty<double> SpacingProperty =
        AvaloniaProperty.Register<WorkflowGridLayer, double>(nameof(Spacing), 22);

    public static readonly StyledProperty<double> DotRadiusProperty =
        AvaloniaProperty.Register<WorkflowGridLayer, double>(nameof(DotRadius), 1);

    public static readonly StyledProperty<IBrush?> DotBrushProperty =
        AvaloniaProperty.Register<WorkflowGridLayer, IBrush?>(nameof(DotBrush));

    public static readonly StyledProperty<double> MinScreenSpacingProperty =
        AvaloniaProperty.Register<WorkflowGridLayer, double>(nameof(MinScreenSpacing), 8);

    /// <summary>Pixels per unit of the dot tile.</summary>
    private const double TileScale = 2;

    private PanZoomTransform _view = PanZoomTransform.Identity;
    // Tiles by step in eighths of a pixel: a zoom changes the step every frame, and a bitmap per frame would pile up native
    // memory until the collector found their finalizers. The tile is drawn at the real step, so it is at most 1/16 px off.
    private readonly Dictionary<double, ImageBrush> _tiles = new();
    private double _tilesRadius;
    private IBrush? _tilesDotBrush;
    private const int MaxTiles = 24;

    static WorkflowGridLayer()
    {
        // Decorative: the card's name says it all.
        AutomationProperties.AccessibilityViewProperty.OverrideDefaultValue<WorkflowGridLayer>(AccessibilityView.Raw);
        AffectsRender<WorkflowGridLayer>(SpacingProperty, DotRadiusProperty, DotBrushProperty, MinScreenSpacingProperty);
        IsHitTestVisibleProperty.OverrideDefaultValue<WorkflowGridLayer>(false);
    }

    public double Spacing
    {
        get => GetValue(SpacingProperty);
        set => SetValue(SpacingProperty, value);
    }

    public double DotRadius
    {
        get => GetValue(DotRadiusProperty);
        set => SetValue(DotRadiusProperty, value);
    }

    public IBrush? DotBrush
    {
        get => GetValue(DotBrushProperty);
        set => SetValue(DotBrushProperty, value);
    }

    public double MinScreenSpacing
    {
        get => GetValue(MinScreenSpacingProperty);
        set => SetValue(MinScreenSpacingProperty, value);
    }

    /// <summary>The canvas's view; the grid follows it.</summary>
    public PanZoomTransform View
    {
        get => _view;
        set
        {
            if (_view != value)
            {
                _view = value;
                InvalidateVisual();
            }
        }
    }

    public override void Render(DrawingContext context)
    {
        var step = Spacing * _view.Zoom;
        var size = Bounds.Size;
        if (DotBrush is not { } brush || step < MinScreenSpacing || step <= 0 || size.Width <= 0 || size.Height <= 0)
        {
            return;
        }
        var radius = DotRadius;
        if (_tilesRadius != radius || !ReferenceEquals(_tilesDotBrush, brush))
        {
            _tiles.Clear();
            _tilesRadius = radius;
            _tilesDotBrush = brush;
        }
        var key = Math.Round(step * 8) / 8;
        if (!_tiles.TryGetValue(key, out var tile))
        {
            if (_tiles.Count >= MaxTiles)
            {
                _tiles.Clear();
            }
            _tiles[key] = tile = Tile(key, radius, brush);
        }
        tile.DestinationRect = new RelativeRect(0, 0, step, step, RelativeUnit.Absolute);
        // World (0, 0) lands on a dot: the tile's middle sits on the view's origin, shifted within one step.
        var origin = _view.WorldToScreen(new Point());
        var x = (origin.X - step / 2) % step;
        var y = (origin.Y - step / 2) % step;
        using var _ = context.PushTransform(Matrix.CreateTranslation(x > 0 ? x - step : x, y > 0 ? y - step : y));
        context.FillRectangle(tile, new Rect(0, 0, size.Width + step, size.Height + step));
    }

    /// <summary>A brush that tiles one dot, centered in a square <paramref name="step"/> wide, drawn at twice the size.</summary>
    private static ImageBrush Tile(double step, double radius, IBrush brush)
    {
        var pixels = Math.Max(1, (int)Math.Ceiling(step * TileScale));
        var bitmap = new RenderTargetBitmap(new PixelSize(pixels, pixels), new Vector(96 * TileScale, 96 * TileScale));
        using (var context = bitmap.CreateDrawingContext())
        {
            var middle = pixels / TileScale / 2;
            context.DrawEllipse(brush, null, new Point(middle, middle), radius, radius);
        }
        return new ImageBrush(bitmap)
        {
            TileMode = TileMode.Tile,
            Stretch = Stretch.Fill,
            DestinationRect = new RelativeRect(0, 0, step, step, RelativeUnit.Absolute)
        };
    }
}
