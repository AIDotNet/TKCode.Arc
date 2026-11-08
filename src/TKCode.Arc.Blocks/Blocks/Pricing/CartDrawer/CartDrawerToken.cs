using Avalonia;
using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>
/// Geometry and the accent-independent literal colors of <see cref="CartDrawer"/> (measured on Arc's Cart drawer, design
/// reference, ADR 0039). Colors that follow the accent (badge, fills, checkout) come from ArcDesign tokens in the theme.
/// </summary>
[ControlDesignToken]
internal sealed class CartDrawerToken : AbstractControlDesignToken
{
    /// <summary>Space around the shop (wide blocks): 20 top, 24 sides and bottom.</summary>
    public Thickness ShopPadding { get; set; }

    /// <summary>Space around the shop at 640px and below: 16 top and sides, 20 bottom.</summary>
    public Thickness ShopPaddingCompact { get; set; }

    /// <summary>Gap between grid columns (wide).</summary>
    public double GridColumnGap { get; set; }

    /// <summary>Gap between grid rows (wide).</summary>
    public double GridRowGap { get; set; }

    /// <summary>Gap between grid columns at 640px and below.</summary>
    public double GridColumnGapCompact { get; set; }

    /// <summary>Gap between grid rows at 640px and below.</summary>
    public double GridRowGapCompact { get; set; }

    /// <summary>Block width at and below which the grid has two columns.</summary>
    public double CompactWidth { get; set; }

    /// <summary>Block width at and below which the cart button drops its text and the drawer fills the block.</summary>
    public double NarrowWidth { get; set; }

    /// <summary>Width of the drawer.</summary>
    public double DrawerWidth { get; set; }

    /// <summary>Gap between the drawer and the block's edges (wide).</summary>
    public double DrawerInset { get; set; }

    /// <summary>Gap between the drawer and the block's edges in narrow blocks.</summary>
    public double DrawerInsetNarrow { get; set; }

    /// <summary>Side of a cart row's thumbnail.</summary>
    public double ThumbnailSize { get; set; }

    /// <summary>Side of a cart row's thumbnail in narrow blocks.</summary>
    public double ThumbnailSizeNarrow { get; set; }

    /// <summary>Height of the checkout button.</summary>
    public double CheckoutHeight { get; set; }

    /// <summary>Diameter of the order-placed disc.</summary>
    public double DiscSize { get; set; }

    /// <summary>The scrim over the shop while the drawer is open.</summary>
    public Color ScrimColor { get; set; }

    /// <summary>Free-shipping green: the unlocked bar and truck, the discount, the applied code.</summary>
    public Color SuccessInk { get; set; }

    /// <summary>The applied code chip's fill (the green at 10%).</summary>
    public Color SuccessFill { get; set; }

    /// <summary>The layer behind a row being swiped.</summary>
    public Color RemoveFill { get; set; }

    /// <summary>The layer behind a row swiped past the remove distance.</summary>
    public Color RemoveFillArmed { get; set; }

    /// <summary>The "Remove" label and bin behind a swiped row.</summary>
    public Color RemoveInk { get; set; }

    /// <summary>Hover fill of the small round buttons (foreground at 6%).</summary>
    public Color HoverFill { get; set; }

    /// <summary>Track of the free-shipping bar (foreground at 8%).</summary>
    public Color TrackColor { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        var n = ArcPalette.Neutrals(isDarkMode);
        ShopPadding = new Thickness(24, 20, 24, 24);
        ShopPaddingCompact = new Thickness(16, 16, 16, 20);
        GridColumnGap = 16;
        GridRowGap = 24;
        GridColumnGapCompact = 12;
        GridRowGapCompact = 20;
        CompactWidth = 640;
        NarrowWidth = 380;
        DrawerWidth = 400;
        DrawerInset = 8;
        DrawerInsetNarrow = 4;
        ThumbnailSize = 64;
        ThumbnailSizeNarrow = 52;
        CheckoutHeight = 50;
        DiscSize = 64;
        ScrimColor = Color.FromArgb((byte)Math.Round((isDarkMode ? 0.42 : 0.16) * 255), 0, 0, 0);
        SuccessInk = isDarkMode ? Oklch.ToColor(0.79, 0.14, 162) : Oklch.ToColor(0.5, 0.12, 162);
        SuccessFill = Oklch.WithAlpha(SuccessInk, isDarkMode ? 0.13 : 0.1);
        RemoveFill = isDarkMode ? Oklch.ToColor(0.287, 0.016, 8) : Oklch.ToColor(0.953, 0.018, 10);
        RemoveFillArmed = isDarkMode ? Oklch.ToColor(0.34, 0.045, 8) : Oklch.ToColor(0.897, 0.04, 10);
        RemoveInk = isDarkMode ? Oklch.ToColor(0.71, 0.155, 8) : Oklch.ToColor(0.53, 0.18, 10);
        HoverFill = Oklch.WithAlpha(n.Foreground, 0.06);
        TrackColor = Oklch.WithAlpha(n.Foreground, 0.08);
    }
}
