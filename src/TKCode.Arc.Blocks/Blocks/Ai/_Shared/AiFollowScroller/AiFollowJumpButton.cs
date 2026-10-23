using Avalonia.Controls;
using Avalonia.Input;
using AvaloniaButton = Avalonia.Controls.Button;

namespace TKCode.Arc;

/// <summary>
/// The round "scroll to latest" button of an <see cref="AiFollowScroller"/>: 34px, 1px border on the surface with the
/// raised shadow and an ArrowDown 16 in the secondary text color; hover tints it. The scroller shows and hides it, names it
/// and handles its click.
/// </summary>
internal sealed class AiFollowJumpButton : AvaloniaButton
{
    protected override Type StyleKeyOverride => typeof(AiFollowJumpButton);

    protected override void OnPointerEntered(PointerEventArgs e)
    {
        base.OnPointerEntered(e);
        // Touch never leaves a hover look behind (Arc: hover: hover).
        PseudoClasses.Set(":hover", e.Pointer.Type != PointerType.Touch);
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        PseudoClasses.Set(":hover", false);
    }
}
