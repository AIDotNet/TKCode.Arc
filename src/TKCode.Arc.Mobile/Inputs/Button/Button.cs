using AvaloniaButton = Avalonia.Controls.Button;

namespace TKCode.Arc.Mobile;

/// <summary>
/// Mobile button: a touch-first filled button with a press state (it scales down and dims while a finger is on it)
/// and no hover state. It is at least 44 by 44 so it is easy to hit.
/// </summary>
public class Button : AvaloniaButton
{
    /// <summary>The smallest width and height a touch target may have.</summary>
    public const double MinTouchTarget = 44;

    protected override Type StyleKeyOverride => typeof(Button);
}
