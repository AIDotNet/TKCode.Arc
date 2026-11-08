using Avalonia.Automation;
using Avalonia.Controls;

namespace TKCode.Arc;

/// <summary>
/// The hidden polite live region of <see cref="SeatManager"/> (Arc's visually hidden <c>role="status"</c>): screen readers
/// read its name whenever it changes. The same sentence twice is read again (an invisible marker toggles).
/// </summary>
internal sealed class SeatManagerAnnouncer : Control
{
    private bool _toggle;

    public SeatManagerAnnouncer()
    {
        AutomationProperties.SetLiveSetting(this, AutomationLiveSetting.Polite);
        IsHitTestVisible = false;
        Focusable = false;
    }

    /// <summary>The last sentence announced, or null.</summary>
    public string? Announcement { get; private set; }

    /// <summary>Every sentence announced, oldest first (at most 64; tests read it).</summary>
    public List<string> History { get; } = new();

    public void Announce(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return;
        }
        _toggle = !_toggle;
        Announcement = text;
        if (History.Count == 64)
        {
            History.RemoveAt(0);
        }
        History.Add(text);
        AutomationProperties.SetName(this, _toggle ? text : text + "​");
    }
}
