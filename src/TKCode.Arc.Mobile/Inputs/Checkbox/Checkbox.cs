using Avalonia.Controls.Primitives;

namespace TKCode.Arc.Mobile;

/// <summary>
/// Mobile checkbox: a 22px rounded box with a check, and the label (the content) beside it, in a row at least 44 high
/// so the whole row is the touch target. A finger on it dims it; there is no hover state. With <c>IsThreeState</c> it
/// also has an indeterminate state, shown as a dash.
/// </summary>
public class Checkbox : ToggleButton
{
    protected override Type StyleKeyOverride => typeof(Checkbox);
}
