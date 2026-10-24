// Ported from Arc's free sign-in block (MIT, Copyright (c) 2026 Elia Kuratli): the sizes of sign-in.module.css.
using AtomUI.Theme.DesignTokens;
using Avalonia;

namespace TKCode.Arc;

/// <summary>Geometry of <see cref="SignIn"/> (Arc's sign-in card; colours come from the ArcDesign tokens).</summary>
[ControlDesignToken]
internal sealed class SignInToken : AbstractControlDesignToken
{
    /// <summary>The widest the card gets (Arc's <c>width: min(100%, 432px)</c>).</summary>
    public double CardMaxWidth { get; set; }

    /// <summary>Padding of each step (space-8).</summary>
    public Thickness StepPadding { get; set; }

    /// <summary>Padding of each step while the card is at most 380 wide (space-6 space-5).</summary>
    public Thickness StepPaddingNarrow { get; set; }

    /// <summary>Padding of the footer (space-3 space-8).</summary>
    public Thickness FooterPadding { get; set; }

    /// <summary>Padding of the footer while narrow (space-3 space-5).</summary>
    public Thickness FooterPaddingNarrow { get; set; }

    /// <summary>Padding of the account row on the code step (space-2 space-2 space-2 space-3).</summary>
    public Thickness IdentityPadding { get; set; }

    /// <summary>Padding of the account row while narrow (space-2 all round).</summary>
    public Thickness IdentityPaddingNarrow { get; set; }

    /// <summary>Diameter of the avatar in the account row.</summary>
    public double AvatarSmallSize { get; set; }

    /// <summary>Diameter of the avatar once signed in (inset 4 in its 96 stage).</summary>
    public double AvatarLargeSize { get; set; }

    /// <summary>Side of the square stage that holds the signed in avatar, its ring and badge.</summary>
    public double AvatarStageSize { get; set; }

    /// <summary>Diameter of the check badge on the signed in avatar.</summary>
    public double BadgeSize { get; set; }

    /// <summary>Stroke of the success ring around the signed in avatar.</summary>
    public double RingThickness { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        CardMaxWidth = 432;
        StepPadding = new Thickness(32);
        StepPaddingNarrow = new Thickness(20, 24);
        FooterPadding = new Thickness(32, 12);
        FooterPaddingNarrow = new Thickness(20, 12);
        IdentityPadding = new Thickness(12, 8, 8, 8);
        IdentityPaddingNarrow = new Thickness(8);
        AvatarSmallSize = 32;
        AvatarLargeSize = 88;
        AvatarStageSize = 96;
        BadgeSize = 26;
        RingThickness = 1.5;
    }
}
