using Avalonia;

namespace TKCode.Arc;

/// <summary>
/// Geometry of <see cref="ShareAccess"/>: the dialog's width limit, padding and narrow breakpoint, the gaps between its
/// sections and rows, the role pickers' widths, the people list's height limit and the link-access icon tile. Colors,
/// radii, the shadow and the fonts come from the ArcDesign tokens in the theme.
/// </summary>
[ControlDesignToken]
internal sealed class ShareAccessToken : AbstractControlDesignToken
{
    /// <summary>The dialog is fluid up to this width.</summary>
    public double DialogMaxWidth { get; set; }

    /// <summary>Space between the dialog's edge and its content.</summary>
    public Thickness DialogPadding { get; set; }

    /// <summary>The padding in narrow dialogs.</summary>
    public Thickness NarrowDialogPadding { get; set; }

    /// <summary>Below this width the invite row stacks and the footer buttons share the row evenly.</summary>
    public double NarrowWidth { get; set; }

    /// <summary>Space between the dialog's sections.</summary>
    public double SectionGap { get; set; }

    /// <summary>Space between a section heading and its content.</summary>
    public double HeadingGap { get; set; }

    /// <summary>Space between neighbouring controls in a row.</summary>
    public double InlineGap { get; set; }

    /// <summary>Height of one person row.</summary>
    public double RowHeight { get; set; }

    /// <summary>Space between the avatar and the name in a person row.</summary>
    public double AvatarGap { get; set; }

    /// <summary>The list of people scrolls beyond this height.</summary>
    public double ListMaxHeight { get; set; }

    /// <summary>Width of the role picker beside the address field.</summary>
    public double InviteRoleWidth { get; set; }

    /// <summary>Width of the link role picker.</summary>
    public double LinkRoleWidth { get; set; }

    /// <summary>Side of the round tile that shows the link scope's icon.</summary>
    public double ScopeTileSize { get; set; }

    /// <summary>Size of the icon inside the scope tile.</summary>
    public double ScopeIconSize { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        DialogMaxWidth = 520;
        DialogPadding = new Thickness(24);
        NarrowDialogPadding = new Thickness(16);
        NarrowWidth = 460;
        SectionGap = 22;
        HeadingGap = 8;
        InlineGap = 8;
        RowHeight = 52;
        AvatarGap = 12;
        ListMaxHeight = 260;
        InviteRoleWidth = 136;
        LinkRoleWidth = 128;
        ScopeTileSize = 36;
        ScopeIconSize = 18;
    }
}
