using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;

namespace TKCode.Arc;

/// <summary>
/// The hidden live region of the Support blocks: screen readers say its name whenever it changes, and the same text said
/// twice is said again. It takes no space and no input. Polite.
/// </summary>
internal sealed class SupportKitAnnouncer : Control
{
    private string? _last;
    private bool _toggle;

    public SupportKitAnnouncer()
    {
        AutomationProperties.SetLiveSetting(this, AutomationLiveSetting.Polite);
        IsHitTestVisible = false;
        Focusable = false;
    }

    /// <summary>The last announced text, or null before the first.</summary>
    public string? Announcement => _last;

    /// <summary>Announces <paramref name="text"/>; null or empty text is ignored.</summary>
    public void Announce(string? text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return;
        }
        _toggle = text == _last && !_toggle;
        _last = text;
        // A zero-width space makes a repeated message a new name, so it is read again.
        AutomationProperties.SetName(this, _toggle ? text + "​" : text);
    }

    protected override Size MeasureOverride(Size availableSize) => default;
}
