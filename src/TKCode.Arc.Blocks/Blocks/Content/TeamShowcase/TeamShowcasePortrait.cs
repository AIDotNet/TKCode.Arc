using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using TKCode.Arc.DesignTokens;

namespace TKCode.Arc;

/// <summary>
/// A generated portrait: a head-and-shoulders figure drawn from a seed in the theme's series colors, used where a person
/// has no photo (the team showcase, the testimonial stage and the hero signup's proof row reuse it). The same seed always
/// gives the same figure: the seed picks the background and body tint, the face tint and one of five hair shapes. An
/// <see cref="Image"/> replaces the drawing. The shape is a rounded square (<see cref="CornerRadius"/>) or a circle (a
/// radius of at least half the size). Decorative: hidden from screen readers and not hit-testable.
/// </summary>
internal sealed class TeamShowcasePortrait : Control
{
    /// <summary>Chooses the figure; equal seeds draw equal figures.</summary>
    public static readonly StyledProperty<int> SeedProperty =
        AvaloniaProperty.Register<TeamShowcasePortrait, int>(nameof(Seed));

    /// <summary>A picture drawn instead of the generated figure, cropped to fill.</summary>
    public static readonly StyledProperty<IImage?> ImageProperty =
        AvaloniaProperty.Register<TeamShowcasePortrait, IImage?>(nameof(Image));

    /// <summary>The corner radius of the frame (bound to an ArcDesign radius by the owner).</summary>
    public static readonly StyledProperty<CornerRadius> CornerRadiusProperty =
        Border.CornerRadiusProperty.AddOwner<TeamShowcasePortrait>();

    internal static readonly StyledProperty<IBrush?> Series1Property =
        AvaloniaProperty.Register<TeamShowcasePortrait, IBrush?>("Series1");

    internal static readonly StyledProperty<IBrush?> Series2Property =
        AvaloniaProperty.Register<TeamShowcasePortrait, IBrush?>("Series2");

    internal static readonly StyledProperty<IBrush?> Series3Property =
        AvaloniaProperty.Register<TeamShowcasePortrait, IBrush?>("Series3");

    internal static readonly StyledProperty<IBrush?> Series4Property =
        AvaloniaProperty.Register<TeamShowcasePortrait, IBrush?>("Series4");

    internal static readonly StyledProperty<IBrush?> SurfaceProperty =
        AvaloniaProperty.Register<TeamShowcasePortrait, IBrush?>("Surface");

    internal static readonly StyledProperty<IBrush?> InkProperty =
        AvaloniaProperty.Register<TeamShowcasePortrait, IBrush?>("Ink");

    private IBrush? _background;
    private IBrush? _body;
    private IBrush? _face;
    private IBrush? _hair;

    static TeamShowcasePortrait()
    {
        AffectsRender<TeamShowcasePortrait>(ImageProperty, CornerRadiusProperty);
        IsHitTestVisibleProperty.OverrideDefaultValue<TeamShowcasePortrait>(false);
        FocusableProperty.OverrideDefaultValue<TeamShowcasePortrait>(false);
    }

    public TeamShowcasePortrait()
    {
        AutomationProperties.SetAccessibilityView(this, AccessibilityView.Raw);
        ArcTokens.Bind(this, Series1Property, ArcDesignTokenKind.Series1);
        ArcTokens.Bind(this, Series2Property, ArcDesignTokenKind.Series2);
        ArcTokens.Bind(this, Series3Property, ArcDesignTokenKind.Series3);
        ArcTokens.Bind(this, Series4Property, ArcDesignTokenKind.Series4);
        ArcTokens.Bind(this, SurfaceProperty, ArcDesignTokenKind.Surface);
        ArcTokens.Bind(this, InkProperty, ArcDesignTokenKind.Foreground);
    }

    /// <inheritdoc cref="SeedProperty"/>
    public int Seed { get => GetValue(SeedProperty); set => SetValue(SeedProperty, value); }

    /// <inheritdoc cref="ImageProperty"/>
    public IImage? Image { get => GetValue(ImageProperty); set => SetValue(ImageProperty, value); }

    /// <inheritdoc cref="CornerRadiusProperty"/>
    public CornerRadius CornerRadius { get => GetValue(CornerRadiusProperty); set => SetValue(CornerRadiusProperty, value); }

    /// <summary>The hair shape the seed picks (0 short, 1 long, 2 bun, 3 fringe, 4 close crop); tests read it.</summary>
    internal int HairShape => Positive(Seed / 7) % 5;

    /// <summary>A stable seed for a name, so a person keeps their figure wherever they appear.</summary>
    public static int SeedOf(string? name)
    {
        // FNV-1a: string.GetHashCode is randomized per process, and a figure must not change between runs.
        var hash = 2166136261u;
        foreach (var c in name ?? string.Empty)
        {
            hash = (hash ^ c) * 16777619u;
        }
        return (int)(hash & 0x7FFFFFFF);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == SeedProperty || change.Property == Series1Property || change.Property == Series2Property
            || change.Property == Series3Property || change.Property == Series4Property || change.Property == SurfaceProperty
            || change.Property == InkProperty)
        {
            _background = null;
            InvalidateVisual();
        }
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var w = double.IsInfinity(availableSize.Width) ? 48 : availableSize.Width;
        var h = double.IsInfinity(availableSize.Height) ? w : availableSize.Height;
        var edge = Math.Min(w, h);
        return double.IsNaN(Width) && double.IsNaN(Height) ? new Size(edge, edge) : base.MeasureOverride(availableSize);
    }

    public override void Render(DrawingContext context)
    {
        var bounds = new Rect(Bounds.Size);
        if (bounds.Width <= 0 || bounds.Height <= 0)
        {
            return;
        }
        var radius = CornerRadius;
        using var clip = context.PushClip(new RoundedRect(bounds, radius));
        if (Image is { } image)
        {
            DrawCover(context, image, bounds);
            return;
        }
        EnsureBrushes();
        context.FillRectangle(_background!, bounds);

        var s = Math.Min(bounds.Width, bounds.Height);
        var cx = bounds.Width / 2;
        var top = (bounds.Height - s) / 2;
        var hair = HairShape;

        // Long hair falls behind the shoulders, so it is drawn first.
        if (hair == 1)
        {
            context.DrawRectangle(_hair, null, new RoundedRect(new Rect(cx - s * 0.23, top + s * 0.25, s * 0.46, s * 0.5), s * 0.2));
        }
        // Shoulders: a wide ellipse rising from the bottom edge, and the neck above it.
        context.DrawEllipse(_body, null, new Point(cx, top + s * 1.04), s * 0.37, s * 0.31);
        context.FillRectangle(_face!, new Rect(cx - s * 0.065, top + s * 0.55, s * 0.13, s * 0.2));
        // Short, bun and fringe hair sit behind the head and show around its top.
        if (hair is 0 or 2 or 3)
        {
            context.DrawEllipse(_hair, null, new Point(cx, top + s * 0.4), s * 0.195, s * 0.205);
        }
        if (hair == 2)
        {
            context.DrawEllipse(_hair, null, new Point(cx, top + s * 0.2), s * 0.08, s * 0.075);
        }
        context.DrawEllipse(_face, null, new Point(cx, top + s * 0.445), s * 0.17, s * 0.195);
        if (hair == 3)
        {
            context.DrawEllipse(_hair, null, new Point(cx - s * 0.055, top + s * 0.31), s * 0.145, s * 0.07);
        }
        if (hair == 4)
        {
            context.DrawEllipse(_hair, null, new Point(cx, top + s * 0.3), s * 0.12, s * 0.045);
        }
    }

    private static void DrawCover(DrawingContext context, IImage image, Rect bounds)
    {
        var size = image.Size;
        if (size.Width <= 0 || size.Height <= 0)
        {
            return;
        }
        var scale = Math.Max(bounds.Width / size.Width, bounds.Height / size.Height);
        var w = bounds.Width / scale;
        var h = bounds.Height / scale;
        var source = new Rect((size.Width - w) / 2, (size.Height - h) / 2, w, h);
        context.DrawImage(image, source, bounds);
    }

    private void EnsureBrushes()
    {
        if (_background is not null)
        {
            return;
        }
        var seed = Positive(Seed);
        var a = SeriesColor(seed % 4);
        var b = SeriesColor((seed / 4 + 1) % 4);
        var surface = ColorOf(SurfaceProperty, Colors.White);
        var ink = ColorOf(InkProperty, Colors.Black);
        _background = new ImmutableSolidColorBrush(Oklch.MixOklab(a, surface, 0.8));
        _body = new ImmutableSolidColorBrush(Oklch.MixOklab(a, surface, 0.12));
        _face = new ImmutableSolidColorBrush(Oklch.MixOklab(b, surface, 0.58));
        _hair = new ImmutableSolidColorBrush(Oklch.MixOklab(a, ink, 0.55));
    }

    private Color SeriesColor(int index) => index switch
    {
        0 => ColorOf(Series1Property, Color.FromRgb(0x4F, 0x6B, 0xED)),
        1 => ColorOf(Series2Property, Color.FromRgb(0x2F, 0xA8, 0x8C)),
        2 => ColorOf(Series3Property, Color.FromRgb(0xE0, 0x8A, 0x3C)),
        _ => ColorOf(Series4Property, Color.FromRgb(0xB0, 0x5C, 0xC8))
    };

    private Color ColorOf(StyledProperty<IBrush?> property, Color fallback)
        => GetValue(property) is ISolidColorBrush brush ? brush.Color : fallback;

    private static int Positive(int value) => value == int.MinValue ? 0 : Math.Abs(value);
}
