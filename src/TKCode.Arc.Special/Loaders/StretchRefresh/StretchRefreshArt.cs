using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Immutable;

namespace TKCode.Arc;

/// <summary>Seeded colours for the generated avatars and photos (soft, saturated OKLCH hues).</summary>
internal static class StretchRefreshArtPalette
{
    public static double Hue(int seed, int k) => ((seed * 83.0) + k * (95 + seed % 4 * 20) + 3600) % 360;

    public static Color Tint(int seed, int k, double l = 0.78, double c = 0.16, double alpha = 1)
        => Oklch.ToColor(l, c, Hue(seed, k), alpha);
}

/// <summary>
/// A generated avatar: a 32 px disc of soft overlapping colour fields (a base diagonal blend and three radial glows,
/// all from the seed), standing in for a person's photo. No image files; the brushes are rebuilt only when the seed
/// changes.
/// </summary>
internal sealed class StretchRefreshAvatar : Control
{
    public static readonly StyledProperty<int> SeedProperty =
        AvaloniaProperty.Register<StretchRefreshAvatar, int>(nameof(Seed));

    private IBrush? _base;
    private EllipseGeometry? _clip;
    private readonly IBrush?[] _glows = new IBrush?[3];
    private static readonly RelativePoint[] s_centres =
    {
        new(0.25, 0.3, RelativeUnit.Relative),
        new(0.8, 0.35, RelativeUnit.Relative),
        new(0.45, 0.85, RelativeUnit.Relative)
    };

    static StretchRefreshAvatar()
    {
        AffectsRender<StretchRefreshAvatar>(SeedProperty);
    }

    public int Seed
    {
        get => GetValue(SeedProperty);
        set => SetValue(SeedProperty, value);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == SeedProperty)
        {
            _base = null;
        }
    }

    private void Build()
    {
        var seed = Seed;
        _base = new ImmutableLinearGradientBrush(
            new[]
            {
                new ImmutableGradientStop(0, StretchRefreshArtPalette.Tint(seed, 0, 0.8, 0.14)),
                new ImmutableGradientStop(1, StretchRefreshArtPalette.Tint(seed, 3, 0.72, 0.17))
            },
            startPoint: new RelativePoint(0, 0, RelativeUnit.Relative),
            endPoint: new RelativePoint(1, 1, RelativeUnit.Relative));
        for (var i = 0; i < _glows.Length; i++)
        {
            var color = StretchRefreshArtPalette.Tint(seed, i + 1, 0.76, 0.19);
            _glows[i] = new ImmutableRadialGradientBrush(
                new[]
                {
                    new ImmutableGradientStop(0, color),
                    new ImmutableGradientStop(1, Color.FromArgb(0, color.R, color.G, color.B))
                },
                center: s_centres[(i + seed) % s_centres.Length],
                gradientOrigin: s_centres[(i + seed) % s_centres.Length],
                radiusX: new RelativeScalar(0.6, RelativeUnit.Relative),
                radiusY: new RelativeScalar(0.6, RelativeUnit.Relative));
        }
    }

    public override void Render(DrawingContext context)
    {
        var size = Math.Min(Bounds.Width, Bounds.Height);
        if (size <= 0)
        {
            return;
        }
        if (_base is null)
        {
            Build();
        }
        var rect = new Rect((Bounds.Width - size) / 2, (Bounds.Height - size) / 2, size, size);
        if (_clip is null || _clip.Rect != rect)
        {
            _clip = new EllipseGeometry(rect);
        }
        using (context.PushGeometryClip(_clip))
        {
            context.DrawRectangle(_base, null, rect);
            foreach (var glow in _glows)
            {
                context.DrawRectangle(glow, null, rect);
            }
        }
    }
}

/// <summary>
/// A generated photo attachment: a 4:3 landscape (sky blend, a low sun, two ridges and a lake band) from the seed, with
/// 12 px corners and a 1 px inner line at 8% of the foreground. At most <c>MaxWidth</c> wide; it shrinks with
/// a narrow row and keeps its aspect.
/// </summary>
internal sealed class StretchRefreshPhoto : Control
{
    public static readonly StyledProperty<int> SeedProperty =
        AvaloniaProperty.Register<StretchRefreshPhoto, int>(nameof(Seed));

    public static readonly StyledProperty<IBrush?> OutlineInkProperty =
        AvaloniaProperty.Register<StretchRefreshPhoto, IBrush?>(nameof(OutlineInk));

    public static readonly StyledProperty<double> CornerProperty =
        AvaloniaProperty.Register<StretchRefreshPhoto, double>(nameof(Corner), 12);

    private IBrush? _sky;
    private IBrush? _sun;
    private IBrush? _far;
    private IBrush? _near;
    private IBrush? _lake;
    private IPen? _outline;
    private Size _built;
    private Geometry? _farShape;
    private Geometry? _nearShape;

    static StretchRefreshPhoto()
    {
        AffectsRender<StretchRefreshPhoto>(SeedProperty, OutlineInkProperty, CornerProperty);
        AffectsMeasure<StretchRefreshPhoto>(SeedProperty);
    }

    public int Seed
    {
        get => GetValue(SeedProperty);
        set => SetValue(SeedProperty, value);
    }

    public IBrush? OutlineInk
    {
        get => GetValue(OutlineInkProperty);
        set => SetValue(OutlineInkProperty, value);
    }

    public double Corner
    {
        get => GetValue(CornerProperty);
        set => SetValue(CornerProperty, value);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == SeedProperty)
        {
            _sky = null;
            _built = default;
        }
        else if (change.Property == OutlineInkProperty)
        {
            _outline = null;
        }
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var max = double.IsNaN(MaxWidth) || double.IsInfinity(MaxWidth) ? 240 : MaxWidth;
        var w = Math.Min(max, double.IsInfinity(availableSize.Width) ? max : availableSize.Width);
        return new Size(w, w * 0.75);
    }

    private void Build(Size size)
    {
        var seed = Seed;
        var w = size.Width;
        var h = size.Height;
        _sky ??= new ImmutableLinearGradientBrush(
            new[]
            {
                new ImmutableGradientStop(0, StretchRefreshArtPalette.Tint(seed, 0, 0.82, 0.08)),
                new ImmutableGradientStop(0.7, StretchRefreshArtPalette.Tint(seed, 1, 0.9, 0.06))
            },
            startPoint: new RelativePoint(0, 0, RelativeUnit.Relative),
            endPoint: new RelativePoint(0, 1, RelativeUnit.Relative));
        _sun = new ImmutableSolidColorBrush(StretchRefreshArtPalette.Tint(seed, 2, 0.93, 0.09));
        _far = new ImmutableSolidColorBrush(StretchRefreshArtPalette.Tint(seed, 3, 0.62, 0.07));
        _near = new ImmutableSolidColorBrush(StretchRefreshArtPalette.Tint(seed, 3, 0.44, 0.06));
        _lake = new ImmutableSolidColorBrush(StretchRefreshArtPalette.Tint(seed, 0, 0.7, 0.07));
        var shift = (seed % 5) / 10.0;
        _farShape = Ridge(w, h, 0.52, new[] { 0.18 + shift / 3, 0.42, 0.66 - shift / 4, 0.86 }, new[] { 0.3, 0.44, 0.34, 0.47 });
        _nearShape = Ridge(w, h, 0.72, new[] { 0.08, 0.34 + shift / 4, 0.58, 0.9 - shift / 5 }, new[] { 0.58, 0.5, 0.62, 0.55 });
        _built = size;
    }

    /// <summary>A ridge line through the peaks (x, y as fractions), closed along the bottom.</summary>
    private static Geometry Ridge(double w, double h, double baseY, double[] xs, double[] ys)
    {
        var g = new StreamGeometry();
        using (var c = g.Open())
        {
            c.BeginFigure(new Point(0, h * baseY), true);
            for (var i = 0; i < xs.Length; i++)
            {
                c.LineTo(new Point(w * xs[i], h * ys[i]));
            }
            c.LineTo(new Point(w, h * baseY));
            c.LineTo(new Point(w, h));
            c.LineTo(new Point(0, h));
            c.EndFigure(true);
        }
        return g;
    }

    public override void Render(DrawingContext context)
    {
        var size = Bounds.Size;
        if (size.Width <= 0 || size.Height <= 0)
        {
            return;
        }
        if (_sky is null || _built != size)
        {
            Build(size);
        }
        var rect = new Rect(size);
        var shape = new RoundedRect(rect, Corner);
        using (context.PushClip(shape))
        {
            context.DrawRectangle(_sky, null, rect);
            var r = size.Width * 0.08;
            context.DrawEllipse(_sun, null, new Point(size.Width * 0.7, size.Height * 0.3), r, r);
            context.DrawGeometry(_far, null, _farShape!);
            context.DrawGeometry(_near, null, _nearShape!);
            context.DrawRectangle(_lake, null, new Rect(0, size.Height * 0.84, size.Width, size.Height * 0.16));
        }
        if (OutlineInk is ISolidColorBrush ink)
        {
            _outline ??= new ImmutablePen(new ImmutableSolidColorBrush(ink.Color, 0.08), 1);
            context.DrawRectangle(null, _outline, new RoundedRect(rect.Deflate(0.5), Math.Max(0, Corner - 0.5)));
        }
    }
}
