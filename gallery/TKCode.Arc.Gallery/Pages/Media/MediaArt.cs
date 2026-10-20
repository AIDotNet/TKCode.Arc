using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace TKCode.Arc.Gallery.Pages;

/// <summary>
/// Offline "photography" for the Media pages: a layered landscape (sky gradient, sun with a glow, three ridges and a
/// lake with ripples) drawn in code, so every example looks complete without network access. <see cref="Graded"/>
/// false renders the flat, desaturated original for before / after comparisons. Geometry and brushes are rebuilt
/// only when the size changes.
/// </summary>
internal sealed class MediaArt : Control
{
    private static readonly string[][] s_palettes =
    [
        // sky top, sky bottom, sun, far ridge, middle ridge, near ridge, lake
        ["#1E3A5F", "#F6A96B", "#FFE2B8", "#5B5F8A", "#3E4A6E", "#1B2A3A", "#2C4A6B"],
        ["#5BA8D9", "#EAF6FF", "#FFF6D6", "#8DB5A4", "#4E7D6B", "#244538", "#4D8DB0"],
        ["#F29F58", "#F7D39B", "#FFF3D1", "#D07A4E", "#A4512F", "#5E2A1A", "#B9673F"],
        ["#3BA8D8", "#C2F1EA", "#FFFFFF", "#5FB3A6", "#2A8C80", "#0E4F4A", "#2F9DB0"],
        ["#2B1466", "#F05D8E", "#FFD8E6", "#7A3E9D", "#4B2A80", "#1F0F3D", "#5A2C7E"],
        ["#A9D3B5", "#F7F1DE", "#FFFBEA", "#8DB87A", "#55804A", "#26452A", "#6E9F86"]
    ];

    private readonly Color[] _colors = new Color[7];
    private Size _builtFor;
    private LinearGradientBrush? _sky;
    private RadialGradientBrush? _glow;
    private IBrush? _sun;
    private readonly IBrush?[] _ridgeBrushes = new IBrush?[3];
    private readonly StreamGeometry?[] _ridges = new StreamGeometry?[3];
    private LinearGradientBrush? _lake;
    private Pen? _ripple;
    private FormattedText? _badgeText;

    public MediaArt()
    {
        ClipToBounds = true;
    }

    /// <summary>Palette (0–5, wraps).</summary>
    public int Scene { get; init; }

    /// <summary>True for the vivid, graded look; false for the flat original.</summary>
    public bool Graded { get; init; } = true;

    /// <summary>Optional large caption drawn in the sky (slide numbers).</summary>
    public string? Badge { get; init; }

    protected override Size MeasureOverride(Size availableSize) => default;

    private void Build(Size size)
    {
        _builtFor = size;
        var palette = s_palettes[((Scene % s_palettes.Length) + s_palettes.Length) % s_palettes.Length];
        for (var i = 0; i < palette.Length; i++)
        {
            var color = Color.Parse(palette[i]);
            _colors[i] = Graded ? color : Flatten(color);
        }
        _sky = new LinearGradientBrush
        {
            StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
            EndPoint = new RelativePoint(0, 1, RelativeUnit.Relative),
            GradientStops = { new GradientStop(_colors[0], 0), new GradientStop(_colors[1], 0.72) }
        };
        _sun = new SolidColorBrush(_colors[2]);
        _glow = new RadialGradientBrush
        {
            GradientStops =
            {
                new GradientStop(Color.FromArgb((byte)(Graded ? 150 : 70), _colors[2].R, _colors[2].G, _colors[2].B), 0),
                new GradientStop(Color.FromArgb(0, _colors[2].R, _colors[2].G, _colors[2].B), 1)
            }
        };
        var w = size.Width;
        var h = size.Height;
        double[] heights = [0.52, 0.62, 0.74];
        double[] waves = [0.08, 0.06, 0.05];
        for (var layer = 0; layer < 3; layer++)
        {
            _ridgeBrushes[layer] = new SolidColorBrush(_colors[3 + layer]);
            var geometry = new StreamGeometry();
            using (var g = geometry.Open())
            {
                var baseY = h * heights[layer];
                var amplitude = h * waves[layer];
                var phase = Scene * 1.7 + layer * 2.1;
                g.BeginFigure(new Point(0, h), true);
                const int steps = 24;
                for (var i = 0; i <= steps; i++)
                {
                    var x = w * i / steps;
                    var t = (double)i / steps * Math.PI * 2;
                    var y = baseY - amplitude * (0.6 * Math.Sin(t * (1.2 + layer * 0.35) + phase) + 0.4 * Math.Sin(t * 2.7 + phase * 1.3));
                    g.LineTo(new Point(x, y));
                }
                g.LineTo(new Point(w, h));
                g.EndFigure(true);
            }
            _ridges[layer] = geometry;
        }
        _lake = new LinearGradientBrush
        {
            StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
            EndPoint = new RelativePoint(0, 1, RelativeUnit.Relative),
            GradientStops = { new GradientStop(_colors[6], 0), new GradientStop(_colors[5], 1) }
        };
        _ripple = new Pen(new SolidColorBrush(Color.FromArgb(70, 255, 255, 255)), 1.5, lineCap: PenLineCap.Round);
        _badgeText = Badge is null
            ? null
            : new FormattedText(Badge, System.Globalization.CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
                new Typeface(FontFamily.Default, FontStyle.Normal, FontWeight.SemiBold), Math.Max(18, h * 0.22),
                new SolidColorBrush(Color.FromArgb(235, 255, 255, 255)));
    }

    /// <summary>The "original": most of the saturation gone, contrast lifted toward a cool grey.</summary>
    private static Color Flatten(Color color)
    {
        var luma = 0.2126 * color.R + 0.7152 * color.G + 0.0722 * color.B;
        double Mix(double channel, double tint) => (channel * 0.28 + luma * 0.72) * 0.82 + tint * 0.18;
        return Color.FromRgb((byte)Math.Clamp(Mix(color.R, 150), 0, 255), (byte)Math.Clamp(Mix(color.G, 156), 0, 255),
            (byte)Math.Clamp(Mix(color.B, 166), 0, 255));
    }

    public override void Render(DrawingContext context)
    {
        var size = Bounds.Size;
        if (size.Width <= 0 || size.Height <= 0)
        {
            return;
        }
        if (size != _builtFor || _sky is null)
        {
            Build(size);
        }
        var w = size.Width;
        var h = size.Height;
        context.DrawRectangle(_sky, null, new Rect(size));
        var sun = new Point(w * (0.62 + (Scene % 3) * 0.08), h * 0.36);
        var radius = Math.Min(w, h) * 0.09;
        context.DrawEllipse(_glow, null, sun, radius * 3.2, radius * 3.2);
        context.DrawEllipse(_sun, null, sun, radius, radius);
        if (_badgeText is not null)
        {
            context.DrawText(_badgeText, new Point(w * 0.08, h * 0.1));
        }
        for (var layer = 0; layer < 3; layer++)
        {
            context.DrawGeometry(_ridgeBrushes[layer], null, _ridges[layer]!);
        }
        var lakeTop = h * 0.82;
        context.DrawRectangle(_lake, null, new Rect(0, lakeTop, w, h - lakeTop));
        for (var i = 0; i < 4; i++)
        {
            var y = lakeTop + (h - lakeTop) * (0.25 + i * 0.18);
            var inset = w * (0.18 + 0.06 * ((i + Scene) % 3));
            context.DrawLine(_ripple!, new Point(inset, y), new Point(w - inset * 1.4, y));
        }
    }
}
