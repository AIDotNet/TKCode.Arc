namespace TKCode.Arc;

/// <summary>Geometry of <see cref="NumberFilterBox"/>.</summary>
[ControlDesignToken]
internal sealed class NumberFilterBoxToken : AbstractControlDesignToken
{
    /// <summary>Gap between the label, the operator, the value and the extra content.</summary>
    public double ColumnGap { get; set; }

    /// <summary>Width of the operator field (a symbol and the chevron).</summary>
    public double OperatorWidth { get; set; }

    /// <summary>Width of the value field.</summary>
    public double ValueWidth { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        ColumnGap = 12;
        OperatorWidth = 84;
        ValueWidth = 168;
    }
}
