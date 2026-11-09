using System.Globalization;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Avalonia.Media.TextFormatting;

namespace TKCode.Arc;

/// <summary>
/// The avatar row of <see cref="SeatManager"/> (Arc Seat manager, design reference; notes 3): the first four members as
/// 28px circles with a 1px Border edge on SurfaceMuted and a 2px Surface ring, each overlapping the previous by 6, then a
/// "+N" circle of the same size for the rest. A member without a picture gets a two-tone gradient generated from the name
/// (never a photo). Drawn in one pass; the row is one image for screen readers.
/// </summary>
internal sealed class SeatManagerAvatars : Control
{
    public static readonly StyledProperty<double> DiameterProperty =
        AvaloniaProperty.Register<SeatManagerAvatars, double>(nameof(Diameter), 28);

    public static readonly StyledProperty<double> OverlapProperty =
        AvaloniaProperty.Register<SeatManagerAvatars, double>(nameof(Overlap), 6);

    public static readonly StyledProperty<double> RingProperty =
        AvaloniaProperty.Register<SeatManagerAvatars, double>(nameof(Ring), 2);

    public static readonly StyledProperty<IBrush?> RingBrushProperty =
        AvaloniaProperty.Register<SeatManagerAvatars, IBrush?>(nameof(RingBrush));

    public static readonly StyledProperty<IBrush?> EdgeBrushProperty =
        AvaloniaProperty.Register<SeatManagerAvatars, IBrush?>(nameof(EdgeBrush));

    public static readonly StyledProperty<IBrush?> FillBrushProperty =
        AvaloniaProperty.Register<SeatManagerAvatars, IBrush?>(nameof(FillBrush));

    public static readonly StyledProperty<IBrush?> CountBrushProperty =
        AvaloniaProperty.Register<SeatManagerAvatars, IBrush?>(nameof(CountBrush));

    public static readonly StyledProperty<FontFamily> FontFamilyProperty =
        Avalonia.Controls.Documents.TextElement.FontFamilyProperty.AddOwner<SeatManagerAvatars>();

    /// <summary>Most avatars drawn before the "+N" circle.</summary>
    public const int MaxVisible = 4;

    private readonly List<(SeatManagerMember Member, IBrush Base, IBrush Glow)> _faces = new();
    private int _rest;
    private TextLayout? _countLayout;

    static SeatManagerAvatars()
    {
        AffectsMeasure<SeatManagerAvatars>(DiameterProperty, OverlapProperty, RingProperty);
        AffectsRender<SeatManagerAvatars>(RingBrushProperty, EdgeBrushProperty, FillBrushProperty, CountBrushProperty);
    }

    public double Diameter { get => GetValue(DiameterProperty); set => SetValue(DiameterProperty, value); }
    public double Overlap { get => GetValue(OverlapProperty); set => SetValue(OverlapProperty, value); }
    public double Ring { get => GetValue(RingProperty); set => SetValue(RingProperty, value); }
    public IBrush? RingBrush { get => GetValue(RingBrushProperty); set => SetValue(RingBrushProperty, value); }
    public IBrush? EdgeBrush { get => GetValue(EdgeBrushProperty); set => SetValue(EdgeBrushProperty, value); }
    public IBrush? FillBrush { get => GetValue(FillBrushProperty); set => SetValue(FillBrushProperty, value); }
    public IBrush? CountBrush { get => GetValue(CountBrushProperty); set => SetValue(CountBrushProperty, value); }
    public FontFamily FontFamily { get => GetValue(FontFamilyProperty); set => SetValue(FontFamilyProperty, value); }

    /// <summary>How many circles are drawn, the "+N" one included (tests).</summary>
    internal int CircleCount => _faces.Count + (_rest > 0 ? 1 : 0);

    /// <summary>The "+N" count, 0 when everyone fits (tests).</summary>
    internal int Rest => _rest;

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == CountBrushProperty || change.Property == FontFamilyProperty)
        {
            _countLayout = null;
        }
    }

    /// <summary>Shows the first members and "+N" for the rest of <paramref name="total"/>; <paramref name="name"/> is the accessible name.</summary>
    public void Show(IReadOnlyList<SeatManagerMember> members, int total, string name)
    {
        _faces.Clear();
        foreach (var member in members.Take(MaxVisible))
        {
            var (b, g) = Gradient(member.Name);
            _faces.Add((member, b, g));
        }
        _rest = Math.Max(0, total - _faces.Count);
        _countLayout = null;
        AutomationProperties.SetName(this, name);
        IsVisible = CircleCount > 0;
        InvalidateMeasure();
        InvalidateVisual();
    }

    /// <summary>A stable hue from the name (FNV-1a), so a person keeps their colors.</summary>
    internal static double HueOf(string? name)
    {
        var hash = 2166136261u;
        foreach (var ch in name ?? string.Empty)
        {
            hash = (hash ^ ch) * 16777619u;
        }
        return hash % 360;
    }

    /// <summary>A diagonal two-hue gradient and a soft off-center glow of a third hue, generated from the name.</summary>
    internal static (IBrush Base, IBrush Glow) Gradient(string? name)
    {
        var h = HueOf(name);
        var a = Oklch.ToColor(0.62, 0.19, h);
        var b = Oklch.ToColor(0.74, 0.17, (h + 80) % 360);
        var c = Oklch.ToColor(0.88, 0.15, (h + 170) % 360);
        IBrush baseBrush = new ImmutableLinearGradientBrush(
            new[] { new ImmutableGradientStop(0, a), new ImmutableGradientStop(1, b) },
            startPoint: new RelativePoint(0.1, 0.1, RelativeUnit.Relative),
            endPoint: new RelativePoint(0.9, 0.9, RelativeUnit.Relative));
        IBrush glow = new ImmutableRadialGradientBrush(
            new[] { new ImmutableGradientStop(0, c), new ImmutableGradientStop(1, Color.FromArgb(0, c.R, c.G, c.B)) },
            center: new RelativePoint(0.68, 0.32, RelativeUnit.Relative),
            gradientOrigin: new RelativePoint(0.68, 0.32, RelativeUnit.Relative),
            radiusX: new RelativeScalar(0.6, RelativeUnit.Relative),
            radiusY: new RelativeScalar(0.6, RelativeUnit.Relative));
        return (baseBrush, glow);
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var n = CircleCount;
        if (n == 0)
        {
            return default;
        }
        var d = Diameter;
        return new Size(Ring + n * d - (n - 1) * Overlap + Ring, d);
    }

    private IBrush? _edgeSource;
    private ImmutablePen? _edgePen;
    private readonly List<EllipseGeometry> _faceClips = [];

    /// <summary>The edge pen, kept while the brush stays (a mutable brush can change in place, so it is read every time).</summary>
    private ImmutablePen? EdgePen()
    {
        var brush = EdgeBrush;
        if (brush is null)
        {
            _edgeSource = null;
            _edgePen = null;
        }
        else if (_edgePen is null || !ReferenceEquals(_edgeSource, brush) || brush is not IImmutableBrush)
        {
            _edgeSource = brush;
            _edgePen = new ImmutablePen(brush.ToImmutable(), 1);
        }
        return _edgePen;
    }

    private EllipseGeometry FaceClip(int index, Rect inner)
    {
        while (_faceClips.Count <= index)
        {
            _faceClips.Add(new EllipseGeometry(inner));
        }
        var clip = _faceClips[index];
        if (clip.Rect != inner)
        {
            clip = new EllipseGeometry(inner);
            _faceClips[index] = clip;
        }
        return clip;
    }

    public override void Render(DrawingContext context)
    {
        var d = Diameter;
        var step = d - Overlap;
        var ring = Ring;
        var x = ring;
        var edge = EdgePen();
        for (var i = 0; i < CircleCount; i++)
        {
            var center = new Point(x + d / 2, d / 2);
            if (RingBrush is { } r && ring > 0)
            {
                context.DrawEllipse(r, null, center, d / 2 + ring, d / 2 + ring);
            }
            context.DrawEllipse(FillBrush, edge, center, d / 2 - 0.5, d / 2 - 0.5);
            if (i < _faces.Count)
            {
                var face = _faces[i];
                var inner = new Rect(x + 1, 1, d - 2, d - 2);
                if (face.Member.Avatar is { } image)
                {
                    using (context.PushGeometryClip(FaceClip(i, inner)))
                    {
                        context.DrawImage(image, new Rect(image.Size), inner);
                    }
                }
                else
                {
                    context.DrawEllipse(face.Base, null, inner.Center, inner.Width / 2, inner.Height / 2);
                    context.DrawEllipse(face.Glow, null, inner.Center, inner.Width / 2, inner.Height / 2);
                }
            }
            else
            {
                _countLayout ??= new TextLayout("+" + _rest.ToString(CultureInfo.CurrentCulture),
                    new Typeface(FontFamily), 13, CountBrush ?? Brushes.Gray);
                _countLayout.Draw(context, new Point(center.X - _countLayout.WidthIncludingTrailingWhitespace / 2,
                    center.Y - _countLayout.Height / 2));
            }
            x += step;
        }
    }
}
