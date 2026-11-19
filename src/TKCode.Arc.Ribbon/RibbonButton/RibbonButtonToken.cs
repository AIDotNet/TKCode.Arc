using Avalonia;
using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>
/// Geometry of ribbon commands (self-designed: Arc has no ribbon). A large command is at least 44px wide with a 24px icon over a
/// 12px label; medium and small commands are 24px rows with a 16px icon. Colors and radius come from the ArcDesign tokens
/// in the theme; the pressed fill is a neutral mix.
/// </summary>
[ControlDesignToken]
internal sealed class RibbonButtonToken : AbstractControlDesignToken
{
    /// <summary>Label size: 12.</summary>
    public double FontSize { get; set; }

    /// <summary>Icon of a large command: 24.</summary>
    public double LargeIconSize { get; set; }

    /// <summary>Icon of a medium or small command: 16.</summary>
    public double SmallIconSize { get; set; }

    /// <summary>Height of a medium or small command (one row of a group): 24.</summary>
    public double RowHeight { get; set; }

    /// <summary>Narrowest large command: 44.</summary>
    public double LargeMinWidth { get; set; }

    /// <summary>Padding of a large command: 6 at the sides and top, 4 at the bottom.</summary>
    public Thickness LargePadding { get; set; }

    /// <summary>Padding of a medium command: 6 before the icon, 8 after the label.</summary>
    public Thickness MediumPadding { get; set; }

    /// <summary>Padding of a small command: 4 at the sides.</summary>
    public Thickness SmallPadding { get; set; }

    /// <summary>Space between the icon and the label of a medium command: 6 before the label.</summary>
    public Thickness LabelMargin { get; set; }

    /// <summary>Space between the icon and the label of a large command: 4 above the label.</summary>
    public Thickness LargeLabelMargin { get; set; }

    /// <summary>Border of every command (transparent until a state draws it), so states never change the size: 1.</summary>
    public Thickness BorderThickness { get; set; }

    /// <summary>Fill of a pressed command: the foreground mixed 12% (dark: 16%) into the muted surface.</summary>
    public Color PressedBackground { get; set; }

    /// <summary>Opacity of a disabled command: 0.4.</summary>
    public double DisabledOpacity { get; set; }

    /// <summary>The keyboard focus ring sits 2px outside the command.</summary>
    public Thickness FocusRingMargin { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        var neutrals = ArcPalette.Neutrals(isDarkMode);
        FontSize = 12;
        LargeIconSize = 24;
        SmallIconSize = 16;
        RowHeight = 24;
        LargeMinWidth = 44;
        LargePadding = new Thickness(6, 6, 6, 4);
        MediumPadding = new Thickness(6, 0, 8, 0);
        SmallPadding = new Thickness(4, 0);
        LabelMargin = new Thickness(6, 0, 0, 0);
        LargeLabelMargin = new Thickness(0, 4, 0, 0);
        BorderThickness = new Thickness(1);
        PressedBackground = Oklch.MixOklab(neutrals.SurfaceMuted, neutrals.Foreground, isDarkMode ? 0.16 : 0.12);
        DisabledOpacity = 0.4;
        FocusRingMargin = new Thickness(-2);
    }
}
