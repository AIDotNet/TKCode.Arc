using Avalonia;
using Avalonia.Media;

namespace TKCode.Arc;

[ControlDesignToken]
internal sealed class CardStackToken : AbstractControlDesignToken
{
    /// <summary>Card height (19rem; 17.5rem on narrow stacks).</summary>
    public double CardHeight { get; set; }

    public double CompactCardHeight { get; set; }

    /// <summary>Card height plus the 24px the cards behind peek out below the top one.</summary>
    public double StageHeight { get; set; }

    public double CompactStageHeight { get; set; }

    /// <summary>Stage width cap: min(100%, 20rem).</summary>
    public double StageMaxWidth { get; set; }

    /// <summary>Gap between the stage and the buttons (<c>--space-5</c>; <c>--space-4</c> when narrow).</summary>
    public double RootGap { get; set; }

    public double CompactRootGap { get; set; }

    /// <summary>Gap between the buttons (<c>--space-2</c>).</summary>
    public double ControlsGap { get; set; }

    /// <summary>Stamp inset from the card corner (<c>--space-4</c>).</summary>
    public Thickness StampMargin { get; set; }

    /// <summary>Stamp: 2rem tall pill, padding 0 12px 0 10px, 6px gap.</summary>
    public double StampHeight { get; set; }

    public Thickness StampPadding { get; set; }

    public double StampGap { get; set; }

    /// <summary>Approving stamp: <c>color-mix(in oklab, var(--success) 32%, var(--border))</c>.</summary>
    public Color KeepStampBorder { get; set; }

    /// <summary>Approving stamp: <c>color-mix(in oklab, var(--success) 9%, var(--surface-raised))</c>.</summary>
    public Color KeepStampFill { get; set; }

    /// <summary>Cards behind darken with depth: black at 3.5% (22% in dark mode).</summary>
    public Color ShadeColor { get; set; }

    /// <summary>Focus outline around the top card: 2px, 3px outside it.</summary>
    public Thickness CardFocusRingMargin { get; set; }

    public CornerRadius CardFocusRingRadius { get; set; }

    /// <summary>Focus outline around the empty state: card height + 2 × 5px.</summary>
    public double EmptyRingHeight { get; set; }

    public double CompactEmptyRingHeight { get; set; }

    /// <summary>Empty state padding (<c>--space-6</c>) and gap (<c>--space-3</c>).</summary>
    public Thickness EmptyPadding { get; set; }

    public double EmptyGap { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        var n = ArcPalette.Neutrals(isDarkMode);
        CardHeight = 304;
        CompactCardHeight = 280;
        StageHeight = 328;
        CompactStageHeight = 304;
        StageMaxWidth = 320;
        RootGap = 20;
        CompactRootGap = 16;
        ControlsGap = 8;
        StampMargin = new Thickness(16);
        StampHeight = 32;
        StampPadding = new Thickness(10, 0, 12, 0);
        StampGap = 6;
        KeepStampBorder = Oklch.MixOklab(n.Border, n.Success, 0.32);
        KeepStampFill = Oklch.MixOklab(n.SurfaceRaised, n.Success, 0.09);
        ShadeColor = Color.FromArgb((byte)Math.Round((isDarkMode ? 0.22 : 0.035) * 255), 0, 0, 0);
        CardFocusRingMargin = new Thickness(-5);
        CardFocusRingRadius = new CornerRadius(39);
        EmptyRingHeight = CardHeight + 10;
        CompactEmptyRingHeight = CompactCardHeight + 10;
        EmptyPadding = new Thickness(24);
        EmptyGap = 12;
    }
}
