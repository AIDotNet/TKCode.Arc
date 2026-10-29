
namespace TKCode.Arc.Mobile;

/// <summary>Geometry of <see cref="IconButton"/>: a 44px touch target, a 56px floating variant and the press feedback.</summary>
[ControlDesignToken]
internal sealed class IconButtonToken : AbstractControlDesignToken
{
    /// <summary>Width and height of the button: 44.</summary>
    public double TouchTarget { get; set; }

    /// <summary>Width and height of the floating variant: 56.</summary>
    public double FabSize { get; set; }

    /// <summary>Size of the icon: 22.</summary>
    public double IconExtent { get; set; }

    /// <summary>Size of the icon of the floating variant: 24.</summary>
    public double FabIconExtent { get; set; }

    /// <summary>Opacity while pressed: 0.85.</summary>
    public double PressedOpacity { get; set; }

    /// <summary>Opacity when disabled: 0.5.</summary>
    public double DisabledOpacity { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        TouchTarget = 44;
        FabSize = 56;
        IconExtent = 22;
        FabIconExtent = 24;
        PressedOpacity = 0.85;
        DisabledOpacity = 0.5;
    }
}
