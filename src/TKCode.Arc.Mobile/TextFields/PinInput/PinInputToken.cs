namespace TKCode.Arc.Mobile;

/// <summary>Geometry of <see cref="PinInput"/>: cell dimensions, spacing, border widths and disabled opacity.</summary>
[ControlDesignToken]
internal sealed class PinInputToken : AbstractControlDesignToken
{
    /// <summary>Height of each PIN/OTP character cell (at least 44px for touch): 48.</summary>
    public double CellHeight { get; set; }

    /// <summary>Maximum width of each character cell: 52.</summary>
    public double CellMaxWidth { get; set; }

    /// <summary>Horizontal spacing between adjacent cells: 8.</summary>
    public double CellSpacing { get; set; }

    /// <summary>Default cell border width: 1.</summary>
    public double BorderWidth { get; set; }

    /// <summary>Active/focused cell border width: 2.</summary>
    public double ActiveBorderWidth { get; set; }

    /// <summary>Opacity while disabled: 0.4.</summary>
    public double DisabledOpacity { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        CellHeight = 48;
        CellMaxWidth = 52;
        CellSpacing = 8;
        BorderWidth = 1;
        ActiveBorderWidth = 2;
        DisabledOpacity = 0.4;
    }
}
