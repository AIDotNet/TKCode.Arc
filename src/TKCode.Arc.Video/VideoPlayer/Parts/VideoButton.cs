using Avalonia.Controls.Primitives;
using AvaloniaButton = Avalonia.Controls.Button;

namespace TKCode.Arc;

/// <summary>
/// A control bar button over the picture (speed, captions menu triggers; the big play and replay buttons with class
/// <c>big</c>): a 32×32 transparent square with a white 0.12 plate on hover (spec section 7, MediaPlayer's button size).
/// Pressing scales it to 0.94.
/// </summary>
internal class VideoButton : AvaloniaButton
{
    protected override Type StyleKeyOverride => typeof(VideoButton);
}

/// <summary>
/// A two-state control bar button (play, mute, captions, full screen; spec section 12): a toggle button for screen readers
/// whose state the player sets. Clicking does not flip <see cref="ToggleButton.IsChecked"/> by itself; the player acts on
/// the click and writes the resulting state back, so the button never disagrees with the video.
/// </summary>
internal sealed class VideoToggleButton : ToggleButton
{
    protected override Type StyleKeyOverride => typeof(VideoToggleButton);

    protected override void Toggle()
    {
    }
}
