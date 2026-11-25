using Avalonia;


namespace TKCode.Arc;

/// <summary>Geometry of the snippet choice list (<see cref="SnippetChoiceList"/>) that follows neither the theme nor the accent.</summary>
[ControlDesignToken]
internal sealed class SnippetChoiceListToken : AbstractControlDesignToken
{
    /// <summary>The height of one option, in pixels.</summary>
    public double ItemHeight { get; set; }

    /// <summary>The space around an option's text.</summary>
    public Thickness ItemPadding { get; set; }

    /// <summary>The space between the list's border and its options.</summary>
    public Thickness ListPadding { get; set; }

    /// <summary>How many options show at once; more scroll.</summary>
    public int MaxVisibleItems { get; set; }

    /// <summary>The narrowest the list gets, in pixels.</summary>
    public double MinWidth { get; set; }

    /// <inheritdoc />
    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        ItemHeight = 22;
        ItemPadding = new Thickness(8, 0);
        ListPadding = new Thickness(4);
        MaxVisibleItems = 12;
        MinWidth = 120;
    }
}
