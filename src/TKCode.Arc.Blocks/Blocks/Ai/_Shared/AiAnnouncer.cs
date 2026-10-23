using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;

namespace TKCode.Arc;

/// <summary>
/// A hidden polite live region for the AI blocks (Arc's <c>role="status"</c> with <c>aria-live="polite"</c>): screen readers
/// say its name whenever it changes, and saying the same text twice still says it again. It takes no space and no input.
/// </summary>
internal sealed class AiAnnouncer : Control
{
    private string? _last;
    private bool _toggle;

    public AiAnnouncer()
    {
        AutomationProperties.SetLiveSetting(this, AutomationLiveSetting.Polite);
        IsHitTestVisible = false;
        Focusable = false;
    }

    /// <summary>The last announced text (without the repeat marker), or null.</summary>
    public string? Announcement => _last;

    /// <summary>Announces <paramref name="text"/>; empty text is ignored (the last announcement stays, as Arc keeps it until the next change).</summary>
    /// <param name="text">What to say.</param>
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
