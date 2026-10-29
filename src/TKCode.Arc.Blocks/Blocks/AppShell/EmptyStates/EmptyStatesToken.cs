using Avalonia;

namespace TKCode.Arc;

/// <summary>Geometry of the <see cref="EmptyStates"/> block (empty-states.module.css).</summary>
[ControlDesignToken]
internal sealed class EmptyStatesToken : AbstractControlDesignToken
{
    /// <summary>The card's corner radius (20).</summary>
    public CornerRadius CardRadius { get; set; }

    /// <summary>The tab track: at most 460 wide, radius 12, padding 3, 2 between tabs.</summary>
    public double TabsMaxWidth { get; set; }

    public CornerRadius TrackRadius { get; set; }

    public Thickness TrackInset { get; set; }

    public double TabGap { get; set; }

    /// <summary>A tab: at least 30 high, radius 9.</summary>
    public double TabHeight { get; set; }

    public CornerRadius TabRadius { get; set; }

    /// <summary>The gliding highlight's 1px stroke sits half a pixel inside the tab, like a CSS border.</summary>
    public CornerRadius HighlightRadius { get; set; }

    /// <summary>The keyboard focus ring around a tab (2px, just outside it).</summary>
    public Thickness TabFocusRingMargin { get; set; }

    public CornerRadius TabFocusRingRadius { get; set; }

    /// <summary>The panel under the tabs (illustration and copy) is at most 480 wide.</summary>
    public double PanelMaxWidth { get; set; }

    /// <summary>The illustration is at most 360 wide and keeps its 320 × 216 aspect ratio.</summary>
    public double StageMaxWidth { get; set; }

    /// <summary>The illustration's strokes stay 1.5px at any scale.</summary>
    public double ArtStrokeWidth { get; set; }

    /// <summary>6 between title and description, 8 at the sides of the copy.</summary>
    public double CopyLineGap { get; set; }

    public Thickness CopyInset { get; set; }

    /// <summary>The description wraps at 40ch (40 × the 8.75px advance of "0" in 14px Inter).</summary>
    public double DescriptionMaxWidth { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        CardRadius = new CornerRadius(20);
        TabsMaxWidth = 460;
        TrackRadius = new CornerRadius(12);
        TrackInset = new Thickness(3);
        TabGap = 2;
        TabHeight = 30;
        TabRadius = new CornerRadius(9);
        HighlightRadius = new CornerRadius(8.5);
        TabFocusRingMargin = new Thickness(-2);
        TabFocusRingRadius = new CornerRadius(11);
        PanelMaxWidth = 480;
        StageMaxWidth = 360;
        ArtStrokeWidth = 1.5;
        CopyLineGap = 6;
        CopyInset = new Thickness(8, 0);
        DescriptionMaxWidth = 350;
    }
}
