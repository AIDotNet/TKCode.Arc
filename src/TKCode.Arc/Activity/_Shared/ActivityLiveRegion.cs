using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;

namespace TKCode.Arc;

/// <summary>
/// A hidden live region of the Activity controls (Arc's visually hidden <c>role="status"</c> span). Screen readers
/// announce its name whenever it changes; announcing the same text twice still announces it again.
/// </summary>
internal sealed class ActivityLiveRegion : Control
{
    private string? _last;
    private bool _toggle;

    public ActivityLiveRegion()
        : this(AutomationLiveSetting.Polite)
    {
    }

    public ActivityLiveRegion(AutomationLiveSetting setting)
    {
        AutomationProperties.SetLiveSetting(this, setting);
        IsHitTestVisible = false;
        Focusable = false;
    }

    /// <summary>The last announced text (without the repeat marker), or null before the first announcement.</summary>
    public string? LastAnnouncement => _last;

    /// <summary>Announces <paramref name="text"/>; null or empty clears the region without announcing.</summary>
    public void Announce(string? text)
    {
        if (string.IsNullOrEmpty(text))
        {
            _last = null;
            _toggle = false;
            AutomationProperties.SetName(this, string.Empty);
            return;
        }
        // A repeated message toggles an invisible marker so the name still changes and is read again.
        _toggle = text == _last && !_toggle;
        _last = text;
        AutomationProperties.SetName(this, _toggle ? text + "​" : text);
    }

    protected override Size MeasureOverride(Size availableSize) => default;
}
