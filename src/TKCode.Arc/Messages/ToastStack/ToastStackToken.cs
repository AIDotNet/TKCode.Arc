using Avalonia;

namespace TKCode.Arc;

[ControlDesignToken]
internal sealed class ToastStackToken : AbstractControlDesignToken
{
    /// <summary>Widest list (22.5rem); the window keeps 16px on both sides.</summary>
    public double MaxListWidth { get; set; }

    /// <summary>Card content padding (space-3, space-4 on the icon side).</summary>
    public Thickness ContentPadding { get; set; }

    /// <summary>Icon slot (18 × 20) and the gap after it.</summary>
    public double IconSlotWidth { get; set; }

    public double IconSlotHeight { get; set; }

    public Thickness IconMargin { get; set; }

    /// <summary>Copy line height (20px).</summary>
    public double CopyLineHeight { get; set; }

    /// <summary>Action button: 28px pill, its slot lifted 4px.</summary>
    public double ActionHeight { get; set; }

    public Thickness ActionPadding { get; set; }

    public Thickness ActionSlotMargin { get; set; }

    public Thickness ActionMargin { get; set; }

    /// <summary>Narrow cards put the action under the copy, 8px below.</summary>
    public Thickness NarrowActionSlotMargin { get; set; }

    /// <summary>Close button: 28px round.</summary>
    public double CloseDiameter { get; set; }

    public Thickness CloseMargin { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        MaxListWidth = 360;
        ContentPadding = new Thickness(16, 12, 12, 12);
        IconSlotWidth = 18;
        IconSlotHeight = 20;
        IconMargin = new Thickness(0, 0, 12, 0);
        CopyLineHeight = 20;
        ActionHeight = 28;
        ActionPadding = new Thickness(12, 0);
        ActionSlotMargin = new Thickness(0, -4, 0, 0);
        ActionMargin = new Thickness(12, 0, 0, 0);
        NarrowActionSlotMargin = new Thickness(0, 8, 0, 0);
        CloseDiameter = 28;
        CloseMargin = new Thickness(12, -4, -2, 0);
    }
}
