namespace TKCode.Arc;

/// <summary>
/// Geometry of A2UI surfaces (self-designed): 8px between the children of rows, columns and lists, 16px card padding,
/// and the image sizes of the basic catalog's <c>Image.variant</c>. Colors and type come from the ArcDesign tokens.
/// </summary>
[ControlDesignToken]
internal sealed class A2UISurfaceViewToken : AbstractControlDesignToken
{
    /// <summary>Gap between the children of Row, Column and List: 8.</summary>
    public double ContainerGap { get; set; }

    /// <summary>Padding inside a Card: 16.</summary>
    public double CardPadding { get; set; }

    /// <summary>Image variant <c>icon</c>: 24.</summary>
    public double ImageIconSize { get; set; }

    /// <summary>Image variant <c>avatar</c>: 40.</summary>
    public double ImageAvatarSize { get; set; }

    /// <summary>Image variant <c>smallFeature</c>: 64.</summary>
    public double ImageSmallFeature { get; set; }

    /// <summary>Image variant <c>mediumFeature</c> (height): 160.</summary>
    public double ImageMediumFeature { get; set; }

    /// <summary>Image variant <c>largeFeature</c> (height): 280.</summary>
    public double ImageLargeFeature { get; set; }

    /// <summary>Image variant <c>header</c> (height): 200.</summary>
    public double ImageHeader { get; set; }

    /// <summary>Icon size: 20.</summary>
    public double IconSize { get; set; }

    /// <summary>How far a component that arrives while an answer streams rises as it fades in: 4.</summary>
    public double EnterOffset { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        ContainerGap = 8;
        CardPadding = 16;
        ImageIconSize = 24;
        ImageAvatarSize = 40;
        ImageSmallFeature = 64;
        ImageMediumFeature = 160;
        ImageLargeFeature = 280;
        ImageHeader = 200;
        IconSize = 20;
        EnterOffset = 4;
    }
}
