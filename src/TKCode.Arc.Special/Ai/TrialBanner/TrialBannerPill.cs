using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Media;
using AvaloniaButton = Avalonia.Controls.Button;

namespace TKCode.Arc;

/// <summary>
/// The trial pill of a <see cref="TrialBanner"/>: a small ring with the lock or check in it and a label whose width
/// springs to its text. In the pill variant it opens the card in a popover; a folded banner shows it to open the banner
/// again. Its fill is the banner's surface (one surface morphs between the faces), so it only draws a hover tint.
/// </summary>
[PseudoClasses(WarningPseudoClass, ExpiredPseudoClass, UpgradedPseudoClass)]
internal sealed class TrialBannerPill : AvaloniaButton
{
    internal const string WarningPseudoClass = ":warning";
    internal const string ExpiredPseudoClass = ":expired";
    internal const string UpgradedPseudoClass = ":upgraded";

    /// <summary>The ring's fill, 0 to 1.</summary>
    public static readonly StyledProperty<double> RingValueProperty =
        AvaloniaProperty.Register<TrialBannerPill, double>(nameof(RingValue));

    /// <summary>The label ("9 days left").</summary>
    public static readonly StyledProperty<string?> LabelProperty =
        AvaloniaProperty.Register<TrialBannerPill, string?>(nameof(Label));

    /// <summary>The glyph in the ring (the lock once expired, the check once upgraded), or null.</summary>
    public static readonly StyledProperty<Geometry?> MarkProperty =
        AvaloniaProperty.Register<TrialBannerPill, Geometry?>(nameof(Mark));

    /// <summary>The tone the ring and the glyph take.</summary>
    public static readonly StyledProperty<TrialStatus> ToneProperty =
        AvaloniaProperty.Register<TrialBannerPill, TrialStatus>(nameof(Tone));

    public double RingValue { get => GetValue(RingValueProperty); set => SetValue(RingValueProperty, value); }

    public string? Label { get => GetValue(LabelProperty); set => SetValue(LabelProperty, value); }

    public Geometry? Mark { get => GetValue(MarkProperty); set => SetValue(MarkProperty, value); }

    public TrialStatus Tone { get => GetValue(ToneProperty); set => SetValue(ToneProperty, value); }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ToneProperty)
        {
            var tone = Tone;
            PseudoClasses.Set(WarningPseudoClass, tone == TrialStatus.Warning);
            PseudoClasses.Set(ExpiredPseudoClass, tone == TrialStatus.Expired);
            PseudoClasses.Set(UpgradedPseudoClass, tone == TrialStatus.Upgraded);
        }
    }
}
