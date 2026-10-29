using Avalonia;

namespace TKCode.Arc;

[ControlDesignToken]
internal sealed class BottomSheetSurfaceToken : AbstractControlDesignToken
{
    public double Extension { get; set; }
    public Thickness SheetMargin { get; set; }
    public Thickness SheetPadding { get; set; }
    public double PanelMaxWidth { get; set; }
    public double NarrowBreakpoint { get; set; }
    public Thickness HeaderPadding { get; set; }
    public Thickness NarrowHeaderPadding { get; set; }
    public Thickness BodyPadding { get; set; }
    public Thickness NarrowBodyPadding { get; set; }
    public double HeaderGap { get; set; }
    public Thickness DescriptionMargin { get; set; }
    public Thickness ActionsMargin { get; set; }
    public double GrabberHitWidth { get; set; }
    public double GrabberHitHeight { get; set; }
    public Thickness GrabberMargin { get; set; }
    public double GrabberBarWidth { get; set; }
    public double GrabberBarHeight { get; set; }
    public double CloseButtonSize { get; set; }
    public Thickness CloseButtonMargin { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        Extension = 160;
        SheetMargin = new Thickness(0, 0, 0, -160);
        SheetPadding = new Thickness(0, 0, 0, 160);
        PanelMaxWidth = 576;
        NarrowBreakpoint = 640;
        HeaderPadding = new Thickness(20, 8, 20, 16);
        NarrowHeaderPadding = new Thickness(16, 8, 16, 16);
        BodyPadding = new Thickness(20, 4, 20, 24);
        NarrowBodyPadding = new Thickness(16, 4, 16, 24);
        HeaderGap = 16;
        DescriptionMargin = new Thickness(0, 2, 0, 0);
        ActionsMargin = new Thickness(0, 20, 0, 0);
        GrabberHitWidth = 56;
        GrabberHitHeight = 20;
        GrabberMargin = new Thickness(0, 0, 0, 4);
        GrabberBarWidth = 36;
        GrabberBarHeight = 5;
        CloseButtonSize = 32;
        CloseButtonMargin = new Thickness(0, -2, 0, 0);
    }
}
