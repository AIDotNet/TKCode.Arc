using Avalonia;
using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>
/// Geometry, thresholds and timing of <see cref="OrbitMenu"/>: the trigger, the targeting zones, the label pill and the
/// track. Colors that follow the accent come from the ArcDesign tokens in the theme; the translucent fills here are
/// derived from the neutrals only.
/// </summary>
[ControlDesignToken]
internal sealed class OrbitMenuToken : AbstractControlDesignToken
{
    /// <summary>Diameter of the round trigger.</summary>
    public double TriggerSize { get; set; }

    /// <summary>Size of the trigger's 24 × 24 glyph.</summary>
    public double TriggerGlyphSize { get; set; }

    /// <summary>Margin of the trigger's keyboard focus ring (negative: outside the trigger).</summary>
    public Thickness TriggerFocusRingMargin { get; set; }

    /// <summary>Fill of the closed trigger under a mouse: the foreground mixed a little toward the background.</summary>
    public Color TriggerHoverFill { get; set; }

    /// <summary>Fill of the open trigger and of the label pill (a translucent raised surface).</summary>
    public Color GlassFill { get; set; }

    /// <summary>Edge of the open trigger and of the label pill.</summary>
    public Color GlassEdge { get; set; }

    /// <summary>Fill of the lens under a danger-toned action: the raised surface tinted with the danger color.</summary>
    public Color DangerLensFill { get; set; }

    /// <summary>Radius around the trigger's center where the pointer targets nothing.</summary>
    public double DeadZone { get; set; }

    /// <summary>How far past the arc the pointer may go and still target an action.</summary>
    public double Reach { get; set; }

    /// <summary>Degrees past the first and the last action that still target them.</summary>
    public double EndTolerance { get; set; }

    /// <summary>How far past the arc the hover field reaches while the menu is open.</summary>
    public double FieldPadding { get; set; }

    /// <summary>Distance a press must travel before it counts as a drag.</summary>
    public double DragThreshold { get; set; }

    /// <summary>Longest press that still counts as a tap (holding longer and letting go without dragging closes the menu).</summary>
    public TimeSpan TapDuration { get; set; }

    /// <summary>How long the confirmation stays in the pill after an action fires.</summary>
    public TimeSpan DoneDuration { get; set; }

    /// <summary>Height of the label pill.</summary>
    public double PillHeight { get; set; }

    /// <summary>Padding inside the label pill.</summary>
    public Thickness PillPadding { get; set; }

    /// <summary>Gap between the confirmation check and its text.</summary>
    public double PillSpacing { get; set; }

    /// <summary>Size of the confirmation check in the pill.</summary>
    public double PillGlyphSize { get; set; }

    /// <summary>Stroke of the arc drawn behind the actions.</summary>
    public double TrackThickness { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        var n = ArcPalette.Neutrals(isDarkMode);
        TriggerSize = 56;
        TriggerGlyphSize = 24;
        TriggerFocusRingMargin = new Thickness(-4);
        TriggerHoverFill = Oklch.MixOklab(n.Foreground, n.Background, 0.14);
        // Arc frosts these with a backdrop blur at 76%; without one, a denser fill keeps what is behind from showing.
        GlassFill = Oklch.WithAlpha(n.SurfaceRaised, 0.86);
        GlassEdge = Oklch.WithAlpha(n.BorderStrong, 0.55);
        DangerLensFill = Oklch.MixOklab(n.SurfaceRaised, n.Danger, 0.14);
        DeadZone = 34;
        Reach = 76;
        EndTolerance = 30;
        FieldPadding = 60;
        DragThreshold = 8;
        TapDuration = TimeSpan.FromMilliseconds(350);
        DoneDuration = TimeSpan.FromMilliseconds(1400);
        PillHeight = 32;
        PillPadding = new Thickness(14, 0);
        PillSpacing = 6;
        PillGlyphSize = 15;
        TrackThickness = 1.5;
    }
}

/// <summary>Geometry and fills of an <see cref="OrbitMenuAction"/>; the shadow and the glyph colors come from ArcDesign tokens.</summary>
[ControlDesignToken]
internal sealed class OrbitMenuActionToken : AbstractControlDesignToken
{
    /// <summary>Diameter of an action (and of the lens under it).</summary>
    public double Size { get; set; }

    /// <summary>Size of an action's 24 × 24 glyph.</summary>
    public double GlyphSize { get; set; }

    /// <summary>Fill of an action: a nearly opaque raised surface, so moving actions never need a blur.</summary>
    public Color Fill { get; set; }

    /// <summary>Edge of an action.</summary>
    public Color Edge { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        var n = ArcPalette.Neutrals(isDarkMode);
        Size = 46;
        GlyphSize = 20;
        Fill = Oklch.WithAlpha(n.SurfaceRaised, 0.94);
        Edge = Oklch.WithAlpha(n.BorderStrong, 0.55);
    }
}
