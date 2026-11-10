// Ported from Arc's free hero-section block (MIT, Copyright (c) 2026 Elia Kuratli): hero-mesh.tsx and the dot canvas of
// hero-relay.module.css. Arc paints the mesh with a WebGL shader; here each point is a smoothstep radial brush that a transform moves.

using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using TKCode.Arc.Motion;

namespace TKCode.Arc;

/// <summary>One point of a mesh: a position as a fraction of the box and a reach.</summary>
internal readonly record struct HeroMeshPoint(double X, double Y, double Spread);

/// <summary>
/// A mesh gradient: each point composites over the last with a smoothstep falloff, drifting on a seamless 20 second loop with a
/// fine grain over it. Draws at thirty frames a second while on screen, and one still frame below full motion.
/// </summary>
/// <remarks>The drift is a <see cref="LiveLoop"/>: a continuous per-frame loop that a ControlTheme cannot express.</remarks>
internal sealed class HeroMeshView : Control
{
    public static readonly StyledProperty<IBrush?> MeshBaseProperty = AvaloniaProperty.Register<HeroMeshView, IBrush?>(nameof(MeshBase));
    public static readonly StyledProperty<IBrush?> Mesh1Property = AvaloniaProperty.Register<HeroMeshView, IBrush?>(nameof(Mesh1));
    public static readonly StyledProperty<IBrush?> Mesh2Property = AvaloniaProperty.Register<HeroMeshView, IBrush?>(nameof(Mesh2));
    public static readonly StyledProperty<IBrush?> Mesh3Property = AvaloniaProperty.Register<HeroMeshView, IBrush?>(nameof(Mesh3));
    public static readonly StyledProperty<IBrush?> Mesh4Property = AvaloniaProperty.Register<HeroMeshView, IBrush?>(nameof(Mesh4));
    public static readonly StyledProperty<IBrush?> Mesh5Property = AvaloniaProperty.Register<HeroMeshView, IBrush?>(nameof(Mesh5));
    public static readonly StyledProperty<IBrush?> Mesh6Property = AvaloniaProperty.Register<HeroMeshView, IBrush?>(nameof(Mesh6));
    public static readonly StyledProperty<double> GrainProperty = AvaloniaProperty.Register<HeroMeshView, double>(nameof(Grain), 0.35);
    public static readonly StyledProperty<double> SpeedProperty = AvaloniaProperty.Register<HeroMeshView, double>(nameof(Speed), 1);

    private const double Period = 20;
    private const double Travel = 0.075;
    private const double Breath = 0.08;
    private const double FrameSeconds = 1d / 30;

    private readonly LiveLoop _loop;
    private HeroMeshPoint[] _points = [];
    private (double Phase1, double Phase2, int K1, int K2)[] _drift = [];
    private IImmutableBrush?[] _brushes = [];
    private IImmutableBrush? _base;
    private double _time;
    private double _sinceDraw;

    public HeroMeshView()
    {
        IsHitTestVisible = false;
        ClipToBounds = true;
        _loop = new LiveLoop(this, Step);
    }

    public IBrush? MeshBase { get => GetValue(MeshBaseProperty); set => SetValue(MeshBaseProperty, value); }

    public IBrush? Mesh1 { get => GetValue(Mesh1Property); set => SetValue(Mesh1Property, value); }

    public IBrush? Mesh2 { get => GetValue(Mesh2Property); set => SetValue(Mesh2Property, value); }

    public IBrush? Mesh3 { get => GetValue(Mesh3Property); set => SetValue(Mesh3Property, value); }

    public IBrush? Mesh4 { get => GetValue(Mesh4Property); set => SetValue(Mesh4Property, value); }

    public IBrush? Mesh5 { get => GetValue(Mesh5Property); set => SetValue(Mesh5Property, value); }

    public IBrush? Mesh6 { get => GetValue(Mesh6Property); set => SetValue(Mesh6Property, value); }

    /// <summary>Film grain, 0 to 1.</summary>
    public double Grain { get => GetValue(GrainProperty); set => SetValue(GrainProperty, value); }

    /// <summary>Drift speed: 1 loops every 20 seconds, 0 holds the mesh still.</summary>
    public double Speed { get => GetValue(SpeedProperty); set => SetValue(SpeedProperty, value); }

    /// <summary>The loop that drifts the mesh (tests read it).</summary>
    internal LiveLoop Loop => _loop;

    internal int PointCount => _points.Length;

    /// <summary>Sets the points. The drift phases come from each index, so a mesh always starts the same way.</summary>
    public void SetPoints(IReadOnlyList<HeroMeshPoint> points)
    {
        _points = points.ToArray();
        _drift = new (double, double, int, int)[_points.Length];
        for (var i = 0; i < _points.Length; i++)
        {
            var hash = Hash($"mesh-{i}");
            _drift[i] = ((hash % 628) / 100d, ((hash >> 10) % 628) / 100d, 1 + (int)((hash >> 20) % 2), 1 + (int)((hash >> 22) % 2));
        }
        _time = Hash(string.Join(",", _points)) % (uint)Period;
        _brushes = new IImmutableBrush?[_points.Length];
        InvalidateVisual();
    }

    private static uint Hash(string text)
    {
        var h = 2166136261u;
        foreach (var c in text)
        {
            h ^= c;
            h = unchecked(h * 16777619u);
        }
        return h;
    }

    private bool Step(double seconds)
    {
        _sinceDraw += seconds;
        if (_sinceDraw < FrameSeconds)
        {
            return true;
        }
        _time = (_time + Math.Min(0.1, _sinceDraw) * Speed) % Period;
        _sinceDraw = 0;
        InvalidateVisual();
        return true;
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        UpdateLoop();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == SpeedProperty)
        {
            UpdateLoop();
        }
        else if (change.Property == GrainProperty)
        {
            InvalidateVisual();
        }
        else if (change.Property == MeshBaseProperty || change.Property == Mesh1Property || change.Property == Mesh2Property
                 || change.Property == Mesh3Property || change.Property == Mesh4Property || change.Property == Mesh5Property
                 || change.Property == Mesh6Property)
        {
            _base = null;
            Array.Clear(_brushes);
            InvalidateVisual();
        }
    }

    private void UpdateLoop()
    {
        if (Speed > 0)
        {
            _loop.Start();
        }
        else
        {
            _loop.Stop();
        }
    }

    private IBrush? BrushAt(int index) => index switch
    {
        0 => Mesh1,
        1 => Mesh2,
        2 => Mesh3,
        3 => Mesh4,
        4 => Mesh5,
        _ => Mesh6
    };

    private static IImmutableBrush? Radial(IBrush? brush)
    {
        if (brush is not ISolidColorBrush solid)
        {
            return null;
        }
        // The shader's falloff, 1 - smoothstep(0, 1, d), as stops along the radius.
        var stops = new List<ImmutableGradientStop>(9);
        for (var i = 0; i <= 8; i++)
        {
            var t = i / 8d;
            var alpha = 1 - t * t * (3 - 2 * t);
            stops.Add(new ImmutableGradientStop(t, Color.FromArgb((byte)Math.Round(alpha * 255), solid.Color.R, solid.Color.G, solid.Color.B)));
        }
        return new ImmutableRadialGradientBrush(stops, 1, null, null, GradientSpreadMethod.Pad,
            RelativePoint.Center, RelativePoint.Center,
            new RelativeScalar(0.5, RelativeUnit.Relative), new RelativeScalar(0.5, RelativeUnit.Relative));
    }

    public override void Render(DrawingContext context)
    {
        var bounds = new Rect(Bounds.Size);
        if (bounds.Width <= 0 || bounds.Height <= 0)
        {
            return;
        }
        _base ??= MeshBase is ISolidColorBrush solid ? new ImmutableSolidColorBrush(solid.Color) : null;
        if (_base is not null)
        {
            context.FillRectangle(_base, bounds);
        }
        var angle = 2 * Math.PI * _time / Period;
        for (var i = 0; i < _points.Length; i++)
        {
            var brush = _brushes[i] ??= Radial(BrushAt(i));
            if (brush is null)
            {
                continue;
            }
            var point = _points[i];
            var drift = _drift[i];
            var x = point.X + Travel * Math.Sin(angle * drift.K1 + drift.Phase1);
            var y = point.Y + Travel * Math.Cos(angle * drift.K2 + drift.Phase2);
            var spread = point.Spread * (1 + Breath * Math.Sin(angle + drift.Phase1 * 0.7));
            // A unit square with the brush at its center, moved and stretched into the point's ellipse, so a frame builds no brush.
            var rx = spread * bounds.Width;
            var ry = spread * bounds.Height;
            using (context.PushTransform(Matrix.CreateScale(rx * 2, ry * 2) * Matrix.CreateTranslation(x * bounds.Width - rx, y * bounds.Height - ry)))
            {
                context.FillRectangle(brush, new Rect(0, 0, 1, 1));
            }
        }
        if (HeroGrain.Tile(Grain) is { } grain)
        {
            var source = new Rect(0, 0, HeroGrain.Size, HeroGrain.Size);
            for (var y = 0d; y < bounds.Height; y += HeroGrain.Size)
            {
                for (var x = 0d; x < bounds.Width; x += HeroGrain.Size)
                {
                    context.DrawImage(grain, source, new Rect(x, y, HeroGrain.Size, HeroGrain.Size));
                }
            }
        }
    }
}

/// <summary>A small tile of the mesh's still grain: black or white pixels with the shader's noise as their alpha.</summary>
internal static class HeroGrain
{
    public const int Size = 192;
    private static readonly Dictionary<int, Bitmap?> s_cache = new();

    public static Bitmap? Tile(double grain)
    {
        var key = (int)Math.Round(grain * 100);
        if (key <= 0)
        {
            return null;
        }
        if (!s_cache.TryGetValue(key, out var tile))
        {
            tile = Create(grain);
            s_cache[key] = tile;
        }
        return tile;
    }

    private static double Noise(int x, int y, double seed)
    {
        var value = Math.Sin(x * 12.9898 + y * 78.233 + seed) * 43758.5453;
        return value - Math.Floor(value);
    }

    private static Bitmap? Create(double grain)
    {
        // The shader's amplitude at a middling luminance: grain * .1 * (.55 + 1.8 * .25) + .6 / 255.
        var amplitude = grain * 0.1 + 0.6 / 255;
        var pixels = new int[Size * Size];
        for (var y = 0; y < Size; y++)
        {
            for (var x = 0; x < Size; x++)
            {
                var n = Noise(x, y, 0) + Noise(x, y, 19.19) - 1;
                var alpha = (int)Math.Clamp(Math.Round(Math.Abs(n) * amplitude * 255), 0, 255);
                var channel = n > 0 ? alpha : 0;
                pixels[y * Size + x] = (alpha << 24) | (channel << 16) | (channel << 8) | channel;
            }
        }
        var bitmap = new WriteableBitmap(new PixelSize(Size, Size), new Vector(96, 96), PixelFormat.Bgra8888, AlphaFormat.Premul);
        using (var buffer = bitmap.Lock())
        {
            for (var y = 0; y < Size; y++)
            {
                System.Runtime.InteropServices.Marshal.Copy(pixels, y * Size, buffer.Address + y * buffer.RowBytes, Size);
            }
        }
        return bitmap;
    }
}

/// <summary>The workflow hero's canvas: a dot grid over the whole screen, strongest behind the graph.</summary>
internal sealed class HeroDotField : Control
{
    public static readonly StyledProperty<IBrush?> DotBrushProperty =
        AvaloniaProperty.Register<HeroDotField, IBrush?>(nameof(DotBrush));

    private const double Pitch = 22;
    private StreamGeometry? _dots;
    private Size _built;
    private bool _isStacked;

    static HeroDotField()
    {
        AffectsRender<HeroDotField>(DotBrushProperty);
    }

    public HeroDotField()
    {
        IsHitTestVisible = false;
        ClipToBounds = true;
    }

    public IBrush? DotBrush { get => GetValue(DotBrushProperty); set => SetValue(DotBrushProperty, value); }

    /// <summary>In one column the bright part follows the graph, low in the middle.</summary>
    public bool IsStacked
    {
        get => _isStacked;
        set
        {
            if (_isStacked != value)
            {
                _isStacked = value;
                UpdateMask();
            }
        }
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        UpdateMask();
    }

    private void UpdateMask()
    {
        // radial-gradient(70% 80% at 72% 52%, #000 30%, transparent 85%), or 90% 60% at 50% 78% in one column.
        var center = _isStacked ? new RelativePoint(0.5, 0.78, RelativeUnit.Relative) : new RelativePoint(0.72, 0.52, RelativeUnit.Relative);
        OpacityMask = new ImmutableRadialGradientBrush(
            new[] { new ImmutableGradientStop(0.30, Colors.Black), new ImmutableGradientStop(0.85, Colors.Transparent) },
            1, null, null, GradientSpreadMethod.Pad, center, center,
            new RelativeScalar(_isStacked ? 0.9 : 0.7, RelativeUnit.Relative),
            new RelativeScalar(_isStacked ? 0.6 : 0.8, RelativeUnit.Relative));
    }

    public override void Render(DrawingContext context)
    {
        var size = Bounds.Size;
        if (DotBrush is null || size.Width <= 0 || size.Height <= 0)
        {
            return;
        }
        if (_dots is null || _built != size)
        {
            _dots = new StreamGeometry();
            using (var c = _dots.Open())
            {
                for (var y = 1d; y < size.Height; y += Pitch)
                {
                    for (var x = 1d; x < size.Width; x += Pitch)
                    {
                        c.BeginFigure(new Point(x - 1, y), true);
                        c.ArcTo(new Point(x + 1, y), new Size(1, 1), 0, false, SweepDirection.Clockwise);
                        c.ArcTo(new Point(x - 1, y), new Size(1, 1), 0, false, SweepDirection.Clockwise);
                        c.EndFigure(true);
                    }
                }
            }
            _built = size;
        }
        context.DrawGeometry(DotBrush, null, _dots);
    }
}
