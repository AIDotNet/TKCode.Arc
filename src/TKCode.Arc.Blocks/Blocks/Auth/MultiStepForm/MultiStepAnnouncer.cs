using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;

namespace TKCode.Arc;

/// <summary>
/// The multi-step form's hidden live region (Arc's visually hidden <c>aria-live="polite"</c> line): screen readers say its
/// name whenever it changes, and the same text said twice is read again. Takes no space and no input.
/// </summary>
internal sealed class MultiStepAnnouncer : Control
{
    private string? _last;
    private bool _toggle;

    public MultiStepAnnouncer()
    {
        AutomationProperties.SetLiveSetting(this, AutomationLiveSetting.Polite);
        IsHitTestVisible = false;
        Focusable = false;
    }

    /// <summary>The last announced text, or null.</summary>
    public string? Announcement => _last;

    /// <summary>Every announcement since the control was created, oldest first (at most 64; tests read it).</summary>
    public List<string> History { get; } = new();

    /// <summary>Announces <paramref name="text"/>; empty text is ignored.</summary>
    public void Announce(string? text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return;
        }
        // A repeated message toggles an invisible marker so the name still changes and is read again.
        _toggle = text == _last && !_toggle;
        _last = text;
        History.Add(text);
        if (History.Count > 64)
        {
            History.RemoveAt(0);
        }
        AutomationProperties.SetName(this, _toggle ? text + "​" : text);
    }

    protected override Size MeasureOverride(Size availableSize) => default;
}
