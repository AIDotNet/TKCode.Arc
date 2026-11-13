namespace TKCode.Arc.Mobile;

/// <summary>Geometry of <see cref="Picker"/>; the rest of the row is a <see cref="ListItem"/>'s (<c>ListItemToken</c>).</summary>
[ControlDesignToken]
internal sealed class PickerToken : AbstractControlDesignToken
{
    /// <summary>The widest the value at the row's end gets before it ends with an ellipsis, so the title keeps room: 160.</summary>
    public double ValueMaxWidth { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        ValueMaxWidth = 160;
    }
}
