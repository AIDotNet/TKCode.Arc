using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Layout;

namespace TKCode.Arc;

/// <summary>
/// A visually hidden polite status region (Arc's <c>role="status"</c> span): the text is separate from what is on
/// screen, and repeating the same message toggles a zero-width space so it is announced again.
/// </summary>
internal sealed class ButtonsLiveRegion : TextBlock
{
    private int _count;

    public ButtonsLiveRegion()
    {
        AutomationProperties.SetLiveSetting(this, AutomationLiveSetting.Polite);
        IsHitTestVisible = false;
        Focusable = false;
        Opacity = 0;
        Width = 1;
        Height = 1;
        ClipToBounds = true;
        HorizontalAlignment = HorizontalAlignment.Left;
        VerticalAlignment = VerticalAlignment.Top;
    }

    /// <summary>The last message announced (without the repeat marker).</summary>
    public string Message { get; private set; } = string.Empty;

    /// <summary>Announces <paramref name="message"/>; empty clears the region silently.</summary>
    public void Announce(string? message)
    {
        Message = message ?? string.Empty;
        if (string.IsNullOrEmpty(Message))
        {
            Text = string.Empty;
            return;
        }
        _count++;
        Text = _count % 2 == 0 ? Message + "​" : Message;
    }
}
