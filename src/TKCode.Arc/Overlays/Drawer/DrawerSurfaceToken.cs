using Avalonia;
using Avalonia.Media;

namespace TKCode.Arc;

[ControlDesignToken]
internal sealed class DrawerSurfaceToken : AbstractControlDesignToken
{
    public double PanelSize { get; set; }
    public double PanelMaxHeight { get; set; }
    public double ViewportGutter { get; set; }
    public double NarrowViewportGutter { get; set; }
    public double NarrowBreakpoint { get; set; }
    public CornerRadius RadiusRight { get; set; }
    public CornerRadius RadiusLeft { get; set; }
    public CornerRadius RadiusTop { get; set; }
    public CornerRadius RadiusBottom { get; set; }
    public CornerRadius NarrowRadiusRight { get; set; }
    public CornerRadius NarrowRadiusLeft { get; set; }
    public CornerRadius NarrowRadiusTop { get; set; }
    public CornerRadius NarrowRadiusBottom { get; set; }
    public Thickness SectionPadding { get; set; }
    public Thickness NarrowSectionPadding { get; set; }
    public double HeaderGap { get; set; }
    public Thickness DescriptionMargin { get; set; }
    public double DescriptionMaxWidth { get; set; }
    public Thickness ActionsMargin { get; set; }
    public double CloseButtonSize { get; set; }
    public Thickness CloseButtonMargin { get; set; }

    /// <summary>Close button rest fill: surface-muted at 65%.</summary>
    public Color CloseButtonBackground { get; set; }

    public double GrabberWidth { get; set; }
    public double GrabberHeight { get; set; }
    public Thickness GrabberMargin { get; set; }
    public double GrabberOpacity { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        PanelSize = 480;
        PanelMaxHeight = 512;
        ViewportGutter = 16;
        NarrowViewportGutter = 12;
        NarrowBreakpoint = 640;
        RadiusRight = new CornerRadius(22, 0, 0, 22);
        RadiusLeft = new CornerRadius(0, 22, 22, 0);
        RadiusTop = new CornerRadius(0, 0, 22, 22);
        RadiusBottom = new CornerRadius(22, 22, 0, 0);
        NarrowRadiusRight = new CornerRadius(18, 0, 0, 18);
        NarrowRadiusLeft = new CornerRadius(0, 18, 18, 0);
        NarrowRadiusTop = new CornerRadius(0, 0, 18, 18);
        NarrowRadiusBottom = new CornerRadius(18, 18, 0, 0);
        SectionPadding = new Thickness(24);
        NarrowSectionPadding = new Thickness(20);
        HeaderGap = 20;
        DescriptionMargin = new Thickness(0, 8, 0, 0);
        DescriptionMaxWidth = 512;
        ActionsMargin = new Thickness(0, 24, 0, 0);
        CloseButtonSize = 32;
        var lineHeight = 18 * 1.4;
        CloseButtonMargin = new Thickness(0, (lineHeight - 32) / 2, 0, 0);
        CloseButtonBackground = Oklch.WithAlpha(ArcPalette.Neutrals(isDarkMode).SurfaceMuted, 0.65);
        GrabberWidth = 36;
        GrabberHeight = 4;
        GrabberMargin = new Thickness(0, 9, 0, 0);
        GrabberOpacity = 0.75;
    }
}
