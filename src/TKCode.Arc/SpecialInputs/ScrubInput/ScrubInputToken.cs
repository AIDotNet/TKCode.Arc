using Avalonia;
using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>
/// Geometry and accent-independent washes of <see cref="ScrubInput"/> (Arc <c>scrub-input.module.css</c>). Colors that
/// follow the theme (surfaces, borders, text) come from ArcDesign tokens in the theme.
/// </summary>
[ControlDesignToken]
internal sealed class ScrubInputToken : AbstractControlDesignToken
{
    /// <summary>Small row height (<c>data-size="sm"</c>: 30px). Medium uses <c>ControlSizeSM</c> (36px).</summary>
    public double ShellHeightSM { get; set; }

    /// <summary>Shell radius: <c>calc(var(--radius-control) - 6px)</c> = 12px.</summary>
    public CornerRadius ShellRadius { get; set; }

    /// <summary>Small shell radius: 9px.</summary>
    public CornerRadius ShellRadiusSM { get; set; }

    /// <summary>The label handle is at least 30px wide.</summary>
    public double HandleMinWidth { get; set; }

    /// <summary>Handle padding: <c>0 4px 0 10px</c>.</summary>
    public Thickness HandlePadding { get; set; }

    /// <summary>Body padding: 10px on the right.</summary>
    public Thickness BodyPadding { get; set; }

    /// <summary>The 2px gap between the number and the unit.</summary>
    public Thickness UnitMargin { get; set; }

    /// <summary>Number padding: 4px on the left.</summary>
    public Thickness EditorPadding { get; set; }

    /// <summary>Unit chip: at least 18 x 22px.</summary>
    public double UnitMinWidth { get; set; }

    public double UnitHeight { get; set; }

    /// <summary>Unit chip radius: 6px.</summary>
    public CornerRadius UnitRadius { get; set; }

    /// <summary>Unit chip padding: <c>0 4px</c>.</summary>
    public Thickness UnitPadding { get; set; }

    /// <summary>S7 ring around the unit button: 2px wide, 2px outside.</summary>
    public Thickness UnitFocusRingMargin { get; set; }

    public CornerRadius UnitFocusRingRadius { get; set; }

    /// <summary>The modifier tag sits 6px above the field at left 6 (20px tall pill: line 16 + padding 2).</summary>
    public Thickness TagMargin { get; set; }

    /// <summary>Tag padding: <c>2px 7px</c>.</summary>
    public Thickness TagPadding { get; set; }

    /// <summary>Tag line height: 16px.</summary>
    public double TagLineHeight { get; set; }

    /// <summary>Glyph labels are drawn at 14px (<c>.handle svg</c>).</summary>
    public double GlyphLabelSize { get; set; }

    /// <summary>Disabled opacity: 0.5.</summary>
    public double DisabledOpacity { get; set; }

    /// <summary>Track wash: <c>color-mix(in oklab, var(--foreground) 6%, transparent)</c>.</summary>
    public Color TrackFill { get; set; }

    /// <summary>Track wash while scrubbing: foreground 10%.</summary>
    public Color TrackFillActive { get; set; }

    /// <summary>Unit button hover: foreground 7%.</summary>
    public Color UnitHoverFill { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        var n = ArcPalette.Neutrals(isDarkMode);
        ShellHeightSM = 30;
        ShellRadius = new CornerRadius(12);
        ShellRadiusSM = new CornerRadius(9);
        HandleMinWidth = 30;
        HandlePadding = new Thickness(10, 0, 4, 0);
        BodyPadding = new Thickness(0, 0, 10, 0);
        UnitMargin = new Thickness(2, 0, 0, 0);
        EditorPadding = new Thickness(4, 0, 0, 0);
        UnitMinWidth = 18;
        UnitHeight = 22;
        UnitRadius = new CornerRadius(6);
        UnitPadding = new Thickness(4, 0);
        UnitFocusRingMargin = new Thickness(-4);
        UnitFocusRingRadius = new CornerRadius(10);
        TagLineHeight = 16;
        TagPadding = new Thickness(7, 2);
        TagMargin = new Thickness(6, -(TagLineHeight + 2 * 2 + 6), 0, 0);
        GlyphLabelSize = 14;
        DisabledOpacity = 0.5;
        TrackFill = Oklch.WithAlpha(n.Foreground, 0.06);
        TrackFillActive = Oklch.WithAlpha(n.Foreground, 0.10);
        UnitHoverFill = Oklch.WithAlpha(n.Foreground, 0.07);
    }
}
