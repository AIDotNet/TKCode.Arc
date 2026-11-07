namespace TKCode.Arc;

/// <summary>
/// Every word a <see cref="SheetStack"/> shows or gives its buttons as an accessible name, in English by default. Replace
/// the instance (or set its properties) to translate or reword.
/// </summary>
public sealed class SheetStackLabels
{
    /// <summary>The close button while one sheet is open.</summary>
    public string Close { get; set; } = "Close";

    /// <summary>The close button while more than one sheet is open: it closes them all.</summary>
    public string CloseAll { get; set; } = "Close all";

    /// <summary>
    /// The back button's accessible name from the label it shows (the sheet below's <see cref="SheetStackSheet.BackLabel"/>
    /// or title). The default is the label itself, so the button reads exactly what it says.
    /// </summary>
    public Func<string, string> BackName { get; set; } = label => label;
}
