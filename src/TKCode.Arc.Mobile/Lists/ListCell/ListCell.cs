using Avalonia.Controls;

namespace TKCode.Arc.Mobile;

/// <summary>
/// A row of a <see cref="ListGroup"/> that does not respond to a press: it holds any content, such as a paragraph of text,
/// a QR code, a custom button or a <see cref="RadioCards"/> grid. It has the frame of a <see cref="ListItem"/> (the same
/// smallest height, side padding and separator, hidden on the last row of the group) but no press shade and no hover
/// state, and it is not focusable itself (its content can be). <see cref="ListGroup"/> uses it as the row as it is,
/// written inline or as the root of the item template.
/// </summary>
public class ListCell : ContentControl
{
    protected override Type StyleKeyOverride => typeof(ListCell);
}
