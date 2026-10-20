using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;

namespace TKCode.Arc;

/// <summary>
/// A hidden live region (Arc's <c>role="status"</c> / <c>role="alert"</c> with <c>aria-live</c>). Screen readers
/// announce its name whenever it changes; announcing the same text twice still announces it again.
/// </summary>
internal sealed class MessagesLiveRegion : Control
{
    private string? _last;
    private bool _toggle;

    public MessagesLiveRegion()
        : this(AutomationLiveSetting.Polite)
    {
    }

    public MessagesLiveRegion(AutomationLiveSetting setting)
    {
        AutomationProperties.SetLiveSetting(this, setting);
        IsHitTestVisible = false;
        Focusable = false;
    }

    /// <summary>The last announced text (without the repeat marker).</summary>
    public string? LastAnnouncement => _last;

    public void Announce(string? text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return;
        }
        // A repeated message toggles an invisible marker so the name still changes and is read again.
        _toggle = text == _last && !_toggle;
        _last = text;
        AutomationProperties.SetName(this, _toggle ? text + "​" : text);
    }

    protected override Size MeasureOverride(Size availableSize) => default;
}
