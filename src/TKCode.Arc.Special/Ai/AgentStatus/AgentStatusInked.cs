using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Media;
using Avalonia.Media.TextFormatting;

namespace TKCode.Arc;

/// <summary>
/// Base of the self-drawn parts of <see cref="AgentStatus"/>: "ink" brushes set in the control theme from the ArcDesign
/// tokens (they follow theme and accent switches), turned into colors for drawing. Parts that live in the overlay layer
/// while the panel is open still get their inks, because each has its own ControlTheme.
/// </summary>
internal abstract class AgentStatusInked : Control
{
    public static readonly StyledProperty<IBrush?> ForegroundInkProperty = AvaloniaProperty.Register<AgentStatusInked, IBrush?>(nameof(ForegroundInk));
    public static readonly StyledProperty<IBrush?> SecondaryInkProperty = AvaloniaProperty.Register<AgentStatusInked, IBrush?>(nameof(SecondaryInk));
    public static readonly StyledProperty<IBrush?> MutedInkProperty = AvaloniaProperty.Register<AgentStatusInked, IBrush?>(nameof(MutedInk));
    public static readonly StyledProperty<IBrush?> BorderInkProperty = AvaloniaProperty.Register<AgentStatusInked, IBrush?>(nameof(BorderInk));
    public static readonly StyledProperty<IBrush?> AccentInkProperty = AvaloniaProperty.Register<AgentStatusInked, IBrush?>(nameof(AccentInk));
    public static readonly StyledProperty<IBrush?> SuccessInkProperty = AvaloniaProperty.Register<AgentStatusInked, IBrush?>(nameof(SuccessInk));
    public static readonly StyledProperty<IBrush?> DangerInkProperty = AvaloniaProperty.Register<AgentStatusInked, IBrush?>(nameof(DangerInk));
    public static readonly StyledProperty<IBrush?> SurfaceInkProperty = AvaloniaProperty.Register<AgentStatusInked, IBrush?>(nameof(SurfaceInk));
    public static readonly StyledProperty<IBrush?> ShimmerBaseInkProperty = AvaloniaProperty.Register<AgentStatusInked, IBrush?>(nameof(ShimmerBaseInk));
    public static readonly StyledProperty<IBrush?> ActiveSegmentInkProperty = AvaloniaProperty.Register<AgentStatusInked, IBrush?>(nameof(ActiveSegmentInk));

    public static readonly StyledProperty<FontFamily> FontFamilyProperty = TextElement.FontFamilyProperty.AddOwner<AgentStatusInked>();
    public static readonly StyledProperty<double> FontSizeProperty = TextElement.FontSizeProperty.AddOwner<AgentStatusInked>();
    public static readonly StyledProperty<FontWeight> FontWeightProperty = TextElement.FontWeightProperty.AddOwner<AgentStatusInked>();

    private static readonly AvaloniaProperty[] s_inkProperties =
    [
        ForegroundInkProperty, SecondaryInkProperty, MutedInkProperty, BorderInkProperty, AccentInkProperty,
        SuccessInkProperty, DangerInkProperty, SurfaceInkProperty, ShimmerBaseInkProperty, ActiveSegmentInkProperty,
        FontFamilyProperty, FontSizeProperty, FontWeightProperty
    ];

    static AgentStatusInked()
    {
        AffectsRender<AgentStatusInked>(ForegroundInkProperty, SecondaryInkProperty, MutedInkProperty, BorderInkProperty,
            AccentInkProperty, SuccessInkProperty, DangerInkProperty, SurfaceInkProperty, ShimmerBaseInkProperty,
            ActiveSegmentInkProperty);
        AffectsMeasure<AgentStatusInked>(FontFamilyProperty, FontSizeProperty, FontWeightProperty);
        IsHitTestVisibleProperty.OverrideDefaultValue<AgentStatusInked>(false);
        FocusableProperty.OverrideDefaultValue<AgentStatusInked>(false);
    }

    public IBrush? ForegroundInk { get => GetValue(ForegroundInkProperty); set => SetValue(ForegroundInkProperty, value); }
    public IBrush? SecondaryInk { get => GetValue(SecondaryInkProperty); set => SetValue(SecondaryInkProperty, value); }
    public IBrush? MutedInk { get => GetValue(MutedInkProperty); set => SetValue(MutedInkProperty, value); }
    public IBrush? BorderInk { get => GetValue(BorderInkProperty); set => SetValue(BorderInkProperty, value); }
    public IBrush? AccentInk { get => GetValue(AccentInkProperty); set => SetValue(AccentInkProperty, value); }
    public IBrush? SuccessInk { get => GetValue(SuccessInkProperty); set => SetValue(SuccessInkProperty, value); }
    public IBrush? DangerInk { get => GetValue(DangerInkProperty); set => SetValue(DangerInkProperty, value); }
    public IBrush? SurfaceInk { get => GetValue(SurfaceInkProperty); set => SetValue(SurfaceInkProperty, value); }
    public IBrush? ShimmerBaseInk { get => GetValue(ShimmerBaseInkProperty); set => SetValue(ShimmerBaseInkProperty, value); }
    public IBrush? ActiveSegmentInk { get => GetValue(ActiveSegmentInkProperty); set => SetValue(ActiveSegmentInkProperty, value); }
    public FontFamily FontFamily { get => GetValue(FontFamilyProperty); set => SetValue(FontFamilyProperty, value); }
    public double FontSize { get => GetValue(FontSizeProperty); set => SetValue(FontSizeProperty, value); }
    public FontWeight FontWeight { get => GetValue(FontWeightProperty); set => SetValue(FontWeightProperty, value); }

    /// <summary>The tabular figures every number of the pill uses.</summary>
    internal static readonly FontFeatureCollection Tabular = new() { FontFeature.Parse("tnum") };

    protected Typeface Typeface => new(FontFamily, FontStyle.Normal, FontWeight);

    /// <summary>The color of a solid ink brush, or <paramref name="fallback"/>.</summary>
    protected static Color ColorOf(IBrush? ink, Color fallback) => ink is ISolidColorBrush solid ? solid.Color : fallback;

    /// <summary>Called when an ink or a font property changed: drop whatever was built from it.</summary>
    protected virtual void OnInksChanged()
    {
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (Array.IndexOf(s_inkProperties, change.Property) >= 0)
        {
            OnInksChanged();
        }
    }
}
