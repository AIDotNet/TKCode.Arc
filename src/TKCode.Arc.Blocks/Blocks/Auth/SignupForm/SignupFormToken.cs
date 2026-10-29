// Ported from Arc's free signup-form block (MIT, Copyright (c) 2026 Elia Kuratli): the geometry of signup-form.module.css.
using Avalonia;

namespace TKCode.Arc;

/// <summary>Geometry of <see cref="SignupForm"/> (Arc's signup-form.module.css).</summary>
[ControlDesignToken]
internal sealed class SignupFormToken : AbstractControlDesignToken
{
    /// <summary>The card's widest width: <c>min(100%, 432px)</c>.</summary>
    public double CardMaxWidth { get; set; }

    /// <summary>At or below this card width the narrow layout applies (Arc's <c>@container signup (max-width: 380px)</c>).</summary>
    public double NarrowWidth { get; set; }

    /// <summary>Space inside a step: <c>var(--space-8)</c>.</summary>
    public Thickness StepInset { get; set; }

    /// <summary>Space inside a step when narrow: <c>var(--space-6) var(--space-5)</c>.</summary>
    public Thickness StepInsetNarrow { get; set; }

    /// <summary>Space inside the footer: <c>var(--space-3) var(--space-8)</c>.</summary>
    public Thickness FooterInset { get; set; }

    /// <summary>Space inside the footer when narrow (20 at the sides).</summary>
    public Thickness FooterInsetNarrow { get; set; }

    /// <summary>The footer's least height.</summary>
    public double FooterMinHeight { get; set; }

    /// <summary>Width of a strength bar.</summary>
    public double MeterBarWidth { get; set; }

    /// <summary>Width of a strength bar when narrow.</summary>
    public double MeterBarWidthNarrow { get; set; }

    /// <summary>Height of a strength bar.</summary>
    public double MeterBarHeight { get; set; }

    /// <summary>Gap between strength bars.</summary>
    public double MeterBarGap { get; set; }

    /// <summary>Side of the completion mark (ring and check).</summary>
    public double MarkDiameter { get; set; }

    /// <summary>Line height of 12px copy (1.4 × text-xs).</summary>
    public double SmallLineHeight { get; set; }

    /// <summary>Line height of 14px copy (1.4 × text-sm).</summary>
    public double BodyLineHeight { get; set; }

    /// <summary>Letter spacing of all body copy: the block's −0.01em at 16px, inherited as −0.16px.</summary>
    public double BodyTracking { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        CardMaxWidth = 432;
        NarrowWidth = 380;
        StepInset = new Thickness(32);
        StepInsetNarrow = new Thickness(20, 24);
        FooterInset = new Thickness(32, 12);
        FooterInsetNarrow = new Thickness(20, 12);
        FooterMinHeight = 48;
        MeterBarWidth = 20;
        MeterBarWidthNarrow = 14;
        MeterBarHeight = 3;
        MeterBarGap = 3;
        MarkDiameter = 48;
        SmallLineHeight = 16.8;
        BodyLineHeight = 19.6;
        BodyTracking = -0.16;
    }
}
