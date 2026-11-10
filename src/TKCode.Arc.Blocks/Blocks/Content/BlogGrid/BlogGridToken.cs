// Ported from Arc's free blog-grid block (MIT, Copyright (c) 2026 Elia Kuratli): blog-grid.module.css.
using Avalonia;

namespace TKCode.Arc;

/// <summary>Geometry of <see cref="BlogGrid"/>; its colors, radii and fonts come from ArcDesign tokens in the theme.</summary>
[ControlDesignToken]
internal sealed class BlogGridToken : AbstractControlDesignToken
{
    /// <summary>The content is at most this wide (1180).</summary>
    public double ContentMaxWidth { get; set; }

    /// <summary>The header is at most this wide (600).</summary>
    public double HeaderMaxWidth { get; set; }

    /// <summary>The padding around the content (64 24).</summary>
    public Thickness ContentInset { get; set; }

    /// <summary>The padding around the content in a small block (48 16).</summary>
    public Thickness ContentInsetSmall { get; set; }



    /// <summary>The byline's portrait (24).</summary>
    public double AvatarSize { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        ContentMaxWidth = 1180;
        HeaderMaxWidth = 600;
        ContentInset = new Thickness(24, 64);
        ContentInsetSmall = new Thickness(16, 48);
        AvatarSize = 24;
    }
}
