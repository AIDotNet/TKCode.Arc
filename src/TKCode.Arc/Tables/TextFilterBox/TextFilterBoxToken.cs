namespace TKCode.Arc;

/// <summary>Geometry of <see cref="TextFilterBox"/>.</summary>
[ControlDesignToken]
internal sealed class TextFilterBoxToken : AbstractControlDesignToken
{
    /// <summary>Gap between the label, the operator, the text field and the extra content.</summary>
    public double ColumnGap { get; set; }

    /// <summary>Width of the operator field (a symbol and the chevron).</summary>
    public double OperatorWidth { get; set; }

    /// <summary>Narrowest the text field gets; it takes the remaining width beyond that.</summary>
    public double InputMinWidth { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        ColumnGap = 12;
        OperatorWidth = 84;
        InputMinWidth = 160;
    }
}
