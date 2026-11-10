using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using AvaloniaButton = Avalonia.Controls.Button;

namespace TKCode.Arc;

/// <summary>
/// The printed side of the card, drawn in code: the finish gradient, two soft light arcs, the chip, the contactless mark
/// and the invented "Orbit" network mark (a disc inside a tilted ring). Text sits on top in the block's template.
/// </summary>
internal sealed class WalletCardArt : Control
{
    public static readonly StyledProperty<WalletCardFinish> FinishProperty =
        AvaloniaProperty.Register<WalletCardArt, WalletCardFinish>(nameof(Finish));

    public static readonly StyledProperty<CornerRadius> CornerRadiusProperty =
        AvaloniaProperty.Register<WalletCardArt, CornerRadius>(nameof(CornerRadius));

    public static readonly StyledProperty<IBrush?> AccentBrushProperty =
        AvaloniaProperty.Register<WalletCardArt, IBrush?>(nameof(AccentBrush));

    public static readonly StyledProperty<IBrush?> AccentStrongBrushProperty =
        AvaloniaProperty.Register<WalletCardArt, IBrush?>(nameof(AccentStrongBrush));

    // Lucide "nfc" (ISC), the contactless mark.
    private static readonly Geometry s_contactless = ArcIcons.Parse(
        "M6 8.32a7.43 7.43 0 0 1 0 7.36 M9.46 6.21a11.76 11.76 0 0 1 0 11.58 M12.91 4.1a15.91 15.91 0 0 1 .01 15.8 M16.37 2a20.16 20.16 0 0 1 0 20");

    private static readonly IImmutableSolidColorBrush s_glow = new ImmutableSolidColorBrush(Color.FromArgb(26, 255, 255, 255));
    private static readonly IImmutableSolidColorBrush s_glowStrong = new ImmutableSolidColorBrush(Color.FromArgb(40, 255, 255, 255));
    private static readonly ImmutablePen s_markPen = new(new ImmutableSolidColorBrush(Color.FromArgb(230, 255, 255, 255)), 1.8);
    private static readonly ImmutablePen s_contactlessPen = new(new ImmutableSolidColorBrush(Color.FromArgb(200, 255, 255, 255)), 1.6,
        lineCap: PenLineCap.Round);
    private static readonly ImmutablePen s_chipLine = new(new ImmutableSolidColorBrush(Color.FromArgb(110, 92, 70, 30)), 0.9);
    private static readonly IImmutableBrush s_chip = new ImmutableLinearGradientBrush(
        [new ImmutableGradientStop(0, Color.Parse("#F3DFA2")), new ImmutableGradientStop(1, Color.Parse("#C9A44E"))],
        startPoint: new RelativePoint(0, 0, RelativeUnit.Relative), endPoint: new RelativePoint(1, 1, RelativeUnit.Relative));
    private static readonly IImmutableSolidColorBrush s_markFill = new ImmutableSolidColorBrush(Color.FromArgb(235, 255, 255, 255));

    private static readonly Dictionary<WalletCardFinish, IImmutableBrush> s_finishes = new()
    {
        [WalletCardFinish.Midnight] = Diagonal("#1B2133", "#0B0E17", "#2A3552"),
        [WalletCardFinish.Ocean] = Diagonal("#0F766E", "#0B4F6C", "#1F8FA3"),
        [WalletCardFinish.Coral] = Diagonal("#E0654F", "#8E3A6B", "#F08A5D"),
        [WalletCardFinish.Forest] = Diagonal("#355E3B", "#1E3B2A", "#5B7F4A"),
    };

    static WalletCardArt()
    {
        AffectsRender<WalletCardArt>(FinishProperty, CornerRadiusProperty, AccentBrushProperty, AccentStrongBrushProperty);
        IsHitTestVisibleProperty.OverrideDefaultValue<WalletCardArt>(false);
    }

    public WalletCardFinish Finish { get => GetValue(FinishProperty); set => SetValue(FinishProperty, value); }

    public CornerRadius CornerRadius { get => GetValue(CornerRadiusProperty); set => SetValue(CornerRadiusProperty, value); }

    public IBrush? AccentBrush { get => GetValue(AccentBrushProperty); set => SetValue(AccentBrushProperty, value); }

    public IBrush? AccentStrongBrush { get => GetValue(AccentStrongBrushProperty); set => SetValue(AccentStrongBrushProperty, value); }

    /// <summary>The background brush of <paramref name="finish"/> (null for the accent finish, which follows the theme).</summary>
    internal static IImmutableBrush? FinishBrush(WalletCardFinish finish) => s_finishes.GetValueOrDefault(finish);

    public override void Render(DrawingContext context)
    {
        var bounds = new Rect(Bounds.Size);
        if (bounds.Width <= 0 || bounds.Height <= 0)
        {
            return;
        }
        var radius = CornerRadius;
        var shape = new RoundedRect(bounds, radius.TopLeft, radius.TopRight, radius.BottomRight, radius.BottomLeft);
        using (context.PushClip(shape))
        {
            context.DrawRectangle(Background(), null, bounds);
            var w = bounds.Width;
            var h = bounds.Height;
            // Two soft arcs of light sweep across the right half.
            context.DrawEllipse(s_glow, null, new Point(w * 0.92, h * 0.05), w * 0.55, w * 0.55);
            context.DrawEllipse(s_glowStrong, null, new Point(w * 1.02, h * 0.98), w * 0.42, w * 0.42);

            // The chip: a rounded gold plate with its contact lines, a third of the way down on the left.
            var unit = w / 340;
            var chip = new Rect(22 * unit, h * 0.36, 44 * unit, 33 * unit);
            context.DrawRectangle(s_chip, null, chip, 6 * unit, 6 * unit);
            var midY = chip.Center.Y;
            context.DrawLine(s_chipLine, new Point(chip.Left, midY), new Point(chip.Right, midY));
            context.DrawLine(s_chipLine, new Point(chip.Left + chip.Width * 0.36, chip.Top), new Point(chip.Left + chip.Width * 0.36, chip.Bottom));
            context.DrawLine(s_chipLine, new Point(chip.Left + chip.Width * 0.64, chip.Top), new Point(chip.Left + chip.Width * 0.64, chip.Bottom));

            // Contactless, right of the chip.
            using (context.PushTransform(Matrix.CreateScale(unit, unit) * Matrix.CreateTranslation(chip.Right + 10 * unit, midY - 12 * unit)))
            {
                context.DrawGeometry(null, s_contactlessPen, s_contactless);
            }

            // The Orbit network mark, bottom right: a disc inside a tilted ring.
            var center = new Point(w - 40 * unit, h - 34 * unit);
            context.DrawEllipse(s_markFill, null, center, 9 * unit, 9 * unit);
            using (context.PushTransform(Matrix.CreateTranslation(-center.X, -center.Y) * Matrix.CreateRotation(-0.42)
                                         * Matrix.CreateTranslation(center.X, center.Y)))
            {
                context.DrawEllipse(null, s_markPen, center, 19 * unit, 6.5 * unit);
            }
        }
    }

    private IImmutableBrush? _accentFinish;

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == AccentBrushProperty || change.Property == AccentStrongBrushProperty)
        {
            _accentFinish = null;
        }
    }

    private IBrush Background()
    {
        if (Finish != WalletCardFinish.Accent)
        {
            return s_finishes[Finish];
        }
        if (_accentFinish is null)
        {
            var strong = (AccentStrongBrush as ISolidColorBrush)?.Color ?? Color.Parse("#2F3B8F");
            var accent = (AccentBrush as ISolidColorBrush)?.Color ?? Color.Parse("#4F5BD5");
            _accentFinish = new ImmutableLinearGradientBrush(
                [new ImmutableGradientStop(0, accent), new ImmutableGradientStop(1, strong)],
                startPoint: new RelativePoint(0, 0, RelativeUnit.Relative), endPoint: new RelativePoint(1, 1, RelativeUnit.Relative));
        }
        return _accentFinish;
    }

    private static IImmutableBrush Diagonal(string from, string to, string sheen)
        => new ImmutableLinearGradientBrush(
            [
                new ImmutableGradientStop(0, Color.Parse(from)),
                new ImmutableGradientStop(0.55, Color.Parse(to)),
                new ImmutableGradientStop(1, Color.Parse(sheen))
            ],
            startPoint: new RelativePoint(0, 0, RelativeUnit.Relative), endPoint: new RelativePoint(1, 1, RelativeUnit.Relative));
}

/// <summary>Sizes the face: as wide as it may be (up to <see cref="MaxFaceWidth"/>) and 1/1.586 as tall, like a bank card.</summary>
internal sealed class WalletCardFacePanel : Panel
{
    /// <summary>The ISO/IEC 7810 ID-1 ratio of a payment card.</summary>
    public const double Ratio = 1.586;

    public static readonly StyledProperty<double> MaxFaceWidthProperty =
        AvaloniaProperty.Register<WalletCardFacePanel, double>(nameof(MaxFaceWidth), 360);

    static WalletCardFacePanel()
    {
        AffectsMeasure<WalletCardFacePanel>(MaxFaceWidthProperty);
    }

    public double MaxFaceWidth { get => GetValue(MaxFaceWidthProperty); set => SetValue(MaxFaceWidthProperty, value); }

    protected override Size MeasureOverride(Size availableSize)
    {
        var width = double.IsInfinity(availableSize.Width) ? MaxFaceWidth : Math.Min(availableSize.Width, MaxFaceWidth);
        var size = new Size(width, width / Ratio);
        foreach (var child in Children)
        {
            child.Measure(size);
        }
        return size;
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var width = Math.Min(finalSize.Width, MaxFaceWidth);
        var rect = new Rect(0, 0, width, width / Ratio);
        foreach (var child in Children)
        {
            child.Arrange(rect);
        }
        return new Size(width, rect.Height);
    }
}

/// <summary>The monthly limit meter: a pill track and a fill that springs to <see cref="Fraction"/> (danger past the limit).</summary>
internal sealed class WalletCardMeter : Control
{
    public static readonly StyledProperty<double> FractionProperty =
        AvaloniaProperty.Register<WalletCardMeter, double>(nameof(Fraction));

    public static readonly StyledProperty<bool> IsOverProperty =
        AvaloniaProperty.Register<WalletCardMeter, bool>(nameof(IsOver));

    public static readonly StyledProperty<IBrush?> TrackBrushProperty =
        AvaloniaProperty.Register<WalletCardMeter, IBrush?>(nameof(TrackBrush));

    public static readonly StyledProperty<IBrush?> FillBrushProperty =
        AvaloniaProperty.Register<WalletCardMeter, IBrush?>(nameof(FillBrush));

    public static readonly StyledProperty<IBrush?> OverBrushProperty =
        AvaloniaProperty.Register<WalletCardMeter, IBrush?>(nameof(OverBrush));

    static WalletCardMeter()
    {
        AffectsRender<WalletCardMeter>(FractionProperty, IsOverProperty, TrackBrushProperty, FillBrushProperty, OverBrushProperty);
        IsHitTestVisibleProperty.OverrideDefaultValue<WalletCardMeter>(false);
    }

    public double Fraction { get => GetValue(FractionProperty); set => SetValue(FractionProperty, value); }

    public bool IsOver { get => GetValue(IsOverProperty); set => SetValue(IsOverProperty, value); }

    public IBrush? TrackBrush { get => GetValue(TrackBrushProperty); set => SetValue(TrackBrushProperty, value); }

    public IBrush? FillBrush { get => GetValue(FillBrushProperty); set => SetValue(FillBrushProperty, value); }

    public IBrush? OverBrush { get => GetValue(OverBrushProperty); set => SetValue(OverBrushProperty, value); }

    public override void Render(DrawingContext context)
    {
        var size = Bounds.Size;
        if (size.Width <= 0 || size.Height <= 0)
        {
            return;
        }
        var r = size.Height / 2;
        context.DrawRectangle(TrackBrush, null, new Rect(size), r, r);
        var fill = Math.Clamp(Fraction, 0, 1) * size.Width;
        if (fill > 0.5)
        {
            context.DrawRectangle(IsOver ? OverBrush : FillBrush, null, new Rect(0, 0, Math.Max(fill, size.Height), size.Height), r, r);
        }
    }
}

/// <summary>A merchant's round disc with its initials (classes s1–s4 pick the series color, credit the success color).</summary>
internal sealed class WalletCardMerchantDisc : Border
{
    protected override Type StyleKeyOverride => typeof(WalletCardMerchantDisc);
}

/// <summary>One activity row: a focusable button whose content the block builds (disc, merchant, meta, amount).</summary>
internal sealed class WalletCardRow : AvaloniaButton
{
    protected override Type StyleKeyOverride => typeof(WalletCardRow);

    public WalletTransaction? Transaction { get; set; }
}
