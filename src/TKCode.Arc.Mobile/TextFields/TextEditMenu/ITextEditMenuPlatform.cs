using Avalonia;
using Avalonia.Controls;

namespace TKCode.Arc.Mobile;

/// <summary>
/// A platform's own text edit menu, which <see cref="TextEditMenu"/> shows in place of its Arc surface once it is set as
/// <see cref="TextEditMenu.Platform"/>. <c>TKCode.Arc.Mobile.iOS</c> implements it with the iOS edit menu.
/// </summary>
public interface ITextEditMenuPlatform
{
    /// <summary>
    /// Shows the platform menu for <paramref name="box"/> next to <paramref name="anchor"/>, above it when there is room,
    /// without covering it (box coordinates: the finger's column over the field's height at a long press, otherwise the
    /// selection or the caret). Returns false when the platform cannot show it now, and the Arc surface shows instead.
    /// </summary>
    bool TryShow(TextBox box, Rect anchor);

    /// <summary>Closes the platform menu if it is showing for <paramref name="box"/>: the box's text changed or it lost focus.</summary>
    void Hide(TextBox box);
}
