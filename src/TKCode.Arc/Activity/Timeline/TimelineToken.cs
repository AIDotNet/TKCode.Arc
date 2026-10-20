using AtomUI.Theme.DesignTokens;
using Avalonia;
using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>
/// Timeline geometry (Arc <c>timeline.module.css</c>): a 28px marker column 8px below each row's top, the line centred
/// under it with 4px gaps, rows whose first text line centres on the marker. Colors come from ArcDesign tokens in the
/// theme; the marker ring is the accent-independent 10% foreground hairline.
/// </summary>
[ControlDesignToken]
internal sealed class TimelineToken : AbstractControlDesignToken
{
    /// <summary>Marker diameter (<c>--marker: 28px</c>).</summary>
    public double MarkerSize { get; set; }

    /// <summary>Marker offset from the row top (<c>--marker-top: 8px</c>).</summary>
    public Thickness MarkerMargin { get; set; }

    /// <summary>Icon size inside a system marker (14px, stroke 2).</summary>
    public double MarkerGlyphSize { get; set; }

    /// <summary>Icon stroke width inside a system marker.</summary>
    public double MarkerGlyphStroke { get; set; }

    /// <summary>
    /// The connecting line: x = 13.5 (marker centre − 0.5), from 40px (marker top + marker + 4px gap) to 4px past the
    /// row bottom (just above the next marker).
    /// </summary>
    public Thickness SegmentMargin { get; set; }

    /// <summary>Content inset from the marker column (<c>calc(var(--marker) + var(--space-2))</c>).</summary>
    public Thickness ContentMargin { get; set; }

    /// <summary>Trigger padding (<c>12px var(--space-3)</c>).</summary>
    public Thickness TriggerPadding { get; set; }

    /// <summary>Trigger padding below 420px (<c>padding-inline: var(--space-2)</c>).</summary>
    public Thickness TriggerPaddingNarrow { get; set; }

    /// <summary>Column gap between text, time and chevron (space-3; space-2 below 420px).</summary>
    public double TriggerColumnGap { get; set; }

    /// <summary>Column gap below 420px.</summary>
    public double TriggerColumnGapNarrow { get; set; }

    /// <summary>Time column gap (space-3).</summary>
    public Thickness TimeMargin { get; set; }

    /// <summary>Time column gap below 420px (space-2).</summary>
    public Thickness TimeMarginNarrow { get; set; }

    /// <summary>Chevron column gap; the 16px glyph centres on the 20px first line.</summary>
    public Thickness ChevronMargin { get; set; }

    /// <summary>Chevron column gap below 420px.</summary>
    public Thickness ChevronMarginNarrow { get; set; }

    /// <summary>Trigger focus ring: 2px, 2px outside.</summary>
    public Thickness FocusRingMargin { get; set; }

    /// <summary>Trigger focus ring radius (control radius + 2).</summary>
    public CornerRadius FocusRingRadius { get; set; }

    /// <summary>Day label line height (<c>var(--leading-body)</c> at 14px).</summary>
    public double DayLineHeight { get; set; }

    /// <summary>Meta and detail line height (<c>var(--leading-body)</c> at 12px).</summary>
    public double MetaLineHeight { get; set; }

    /// <summary>Lifts the 12px count onto the label's baseline.</summary>
    public Thickness DayCountMargin { get; set; }

    /// <summary>The space between the rolling count and "updates".</summary>
    public double DayCountSpacing { get; set; }

    /// <summary>Title line height (20px).</summary>
    public double TitleLineHeight { get; set; }

    /// <summary>Gap between title and meta (2px).</summary>
    public double TextGap { get; set; }

    /// <summary>The time column keeps a steady width (2.75rem) so "9m" → "10m" never rewraps the title.</summary>
    public double TimeMinWidth { get; set; }

    /// <summary>Chevron size (16px, stroke 1.75).</summary>
    public double ChevronSize { get; set; }

    /// <summary>Detail padding (<c>0 var(--space-3) var(--space-4)</c>).</summary>
    public Thickness DetailPadding { get; set; }

    /// <summary>Detail padding below 420px.</summary>
    public Thickness DetailPaddingNarrow { get; set; }

    /// <summary>Day label padding (<c>var(--space-2) var(--space-3) var(--space-2) 0</c>).</summary>
    public Thickness DayPadding { get; set; }

    /// <summary>Space after each day's list (<c>margin-bottom: var(--space-3)</c>).</summary>
    public double DayGap { get; set; }

    /// <summary>Marker hairline: <c>color-mix(in oklab, var(--foreground) 10%, transparent)</c>.</summary>
    public Color MarkerRingColor { get; set; }

    /// <summary>Tone tint over the surface (<c>color-mix(var(--success) 12%, var(--surface))</c>).</summary>
    public double ToneTintOpacity { get; set; }

    /// <summary>Bottom edge feather while more updates wait below (36px).</summary>
    public double FeatherHeight { get; set; }

    /// <summary>Width below which paddings tighten (420px).</summary>
    public double NarrowWidth { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        MarkerSize = 28;
        MarkerMargin = new Thickness(0, 8, 0, 0);
        MarkerGlyphSize = 14;
        MarkerGlyphStroke = 2;
        SegmentMargin = new Thickness(13.5, 40, 0, -4);
        ContentMargin = new Thickness(36, 0, 0, 0);
        TriggerPadding = new Thickness(12);
        TriggerPaddingNarrow = new Thickness(8, 12);
        TriggerColumnGap = 12;
        TriggerColumnGapNarrow = 8;
        TimeMargin = new Thickness(12, 0, 0, 0);
        TimeMarginNarrow = new Thickness(8, 0, 0, 0);
        ChevronMargin = new Thickness(12, 2, 0, 0);
        ChevronMarginNarrow = new Thickness(8, 2, 0, 0);
        FocusRingMargin = new Thickness(-2);
        FocusRingRadius = new CornerRadius(20);
        DayLineHeight = 19.6;
        MetaLineHeight = 16.8;
        DayCountMargin = new Thickness(0, 2, 0, 0);
        DayCountSpacing = 3;
        TitleLineHeight = 20;
        TextGap = 2;
        TimeMinWidth = 44;
        ChevronSize = 16;
        DetailPadding = new Thickness(12, 0, 12, 16);
        DetailPaddingNarrow = new Thickness(8, 0, 8, 16);
        DayPadding = new Thickness(0, 8, 12, 8);
        DayGap = 12;
        MarkerRingColor = Oklch.WithAlpha(ArcPalette.Neutrals(isDarkMode).Foreground, 0.10);
        ToneTintOpacity = 0.12;
        FeatherHeight = 36;
        NarrowWidth = 420;
    }
}
