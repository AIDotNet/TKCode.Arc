// Ported from Arc's free changelog-feed block (MIT, Copyright (c) 2026 Elia Kuratli): changelog-feed.module.css.
using Avalonia;
using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>Geometry of <see cref="ChangelogFeed"/> and the colors it derives from the neutrals; accent colors come from ArcDesign tokens in the theme.</summary>
[ControlDesignToken]
internal sealed class ChangelogFeedToken : AbstractControlDesignToken
{
    /// <summary>The block is at most this wide (920).</summary>
    public double FeedMaxWidth { get; set; }

    /// <summary>The scrolling list's height (560).</summary>
    public double ScrollerHeight { get; set; }

    /// <summary>The scrolling list's height in a narrow block (520).</summary>
    public double ScrollerHeightNarrow { get; set; }

    /// <summary>The summary and the details are at most this wide (62ch of small text).</summary>
    public double TextMaxWidth { get; set; }

    /// <summary>The padding of an open entry's panel (0 72 28 156).</summary>
    public Thickness PanelInset { get; set; }

    /// <summary>The padding of an open entry's panel in a narrow block (0 20 24).</summary>
    public Thickness PanelInsetNarrow { get; set; }

    /// <summary>The count on a chip that is on: the background at 62%.</summary>
    public Color ChipCountOn { get; set; }

    /// <summary>A hovered row: the muted surface at 60%.</summary>
    public Color RowHoverBackground { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        var n = ArcPalette.Neutrals(isDarkMode);
        FeedMaxWidth = 920;
        ScrollerHeight = 560;
        ScrollerHeightNarrow = 520;
        TextMaxWidth = 484;
        PanelInset = new Thickness(156, 0, 72, 28);
        PanelInsetNarrow = new Thickness(20, 0, 20, 24);
        ChipCountOn = Oklch.WithAlpha(n.Background, 0.62);
        RowHoverBackground = Oklch.WithAlpha(n.SurfaceMuted, 0.6);
    }
}
