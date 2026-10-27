using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using TKCode.Arc.DesignTokens;
using AvaloniaButton = Avalonia.Controls.Button;

namespace TKCode.Arc;

/// <summary>
/// The Compare toggle (notes 3.1): 34 high, radius 9, padding (10, 0), the <c>git-compare-arrows</c> icon and the text,
/// 12px 500. On: AccentSubtle with an edge of Accent mixed 40% into Border (OKLab) and AccentStrong text; off: Surface,
/// Border, TextSecondary; 160ms colour; pressing scales to .97. <see cref="IsIconOnly"/> (compact) keeps a 36px icon
/// button; its spoken name stays (Arc dropped it).
/// </summary>
[PseudoClasses(IconOnlyPseudoClass)]
internal sealed class KpiCompareButton : ToggleButton
{
    internal const string IconOnlyPseudoClass = ":icon-only";

    /// <summary>The edge when on (derived from Accent and Border, so it is computed here, not a token).</summary>
    public static readonly StyledProperty<IBrush?> OnBorderBrushProperty =
        AvaloniaProperty.Register<KpiCompareButton, IBrush?>(nameof(OnBorderBrush));

    /// <summary>Only the icon (compact layout).</summary>
    public static readonly StyledProperty<bool> IsIconOnlyProperty =
        AvaloniaProperty.Register<KpiCompareButton, bool>(nameof(IsIconOnly));

    public KpiCompareButton()
    {
        ActualThemeVariantChanged += (_, _) => RefreshEdge();
        ResourcesChanged += (_, _) => RefreshEdge();
        AttachedToVisualTree += (_, _) => RefreshEdge();
    }

    public IBrush? OnBorderBrush { get => GetValue(OnBorderBrushProperty); set => SetValue(OnBorderBrushProperty, value); }

    public bool IsIconOnly { get => GetValue(IsIconOnlyProperty); set => SetValue(IsIconOnlyProperty, value); }

    protected override Type StyleKeyOverride => typeof(KpiCompareButton);

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IsIconOnlyProperty)
        {
            PseudoClasses.Set(IconOnlyPseudoClass, IsIconOnly);
        }
    }

    private void RefreshEdge()
    {
        if (VisualRoot is null)
        {
            return;
        }
        var accent = ArcTokens.GetColor(this, ArcDesignTokenKind.Accent);
        var border = ArcTokens.GetColor(this, ArcDesignTokenKind.Border);
        SetCurrentValue(OnBorderBrushProperty, new ImmutableSolidColorBrush(AnalyticsColors.Mix(accent, 0.4, border)));
    }
}

/// <summary>The detail view's close button: 30 square, radius 9, the Lucide <c>x</c> at 16, TextMuted turning Foreground on SurfaceMuted when pointed at.</summary>
internal sealed class KpiCloseButton : AvaloniaButton
{
    protected override Type StyleKeyOverride => typeof(KpiCloseButton);
}

/// <summary>A legend swatch: a 14 × 1 line, solid Accent (current) or dashed TextMuted (prior).</summary>
internal sealed class KpiLegendSwatch : Control
{
    private readonly AnalyticsPalette _palette = new();

    public KpiLegendSwatch()
    {
        Width = 14;
        Height = 2;
        ActualThemeVariantChanged += (_, _) => Refresh();
        ResourcesChanged += (_, _) => Refresh();
    }

    /// <summary>Dashed TextMuted (the prior period) instead of solid Accent.</summary>
    public bool IsPrior { get; set; }

    private void Refresh()
    {
        if (_palette.IsReady)
        {
            _palette.Refresh(this);
        }
        InvalidateVisual();
    }

    public override void Render(DrawingContext context)
    {
        if (!_palette.IsReady)
        {
            _palette.Refresh(this);
        }
        var y = Math.Round(Bounds.Height / 2) + 0.5;
        var pen = IsPrior
            ? _palette.Pen(_palette.TextMuted, 1, 1, 3, 2, PenLineCap.Flat)
            : _palette.Pen(_palette.Accent, 1, cap: PenLineCap.Flat);
        context.DrawLine(pen, new Point(0, y), new Point(Bounds.Width, y));
    }
}
