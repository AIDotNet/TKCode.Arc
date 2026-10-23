using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;

namespace TKCode.Arc;

/// <summary>
/// A hidden polite live region for the explainers: screen readers say its name whenever it changes, and saying the same
/// text twice still says it again. It takes no space and no input. The explainers speak through it only when the reader
/// drives the story (step, pause, a control change, the end of a stepped run), never while the story plays on its own.
/// </summary>
internal sealed class ExplainerAnnouncer : Control
{
    private string? _last;
    private bool _toggle;

    public ExplainerAnnouncer()
    {
        AutomationProperties.SetLiveSetting(this, AutomationLiveSetting.Polite);
        IsHitTestVisible = false;
        Focusable = false;
    }

    /// <summary>The last announced text (without the repeat marker), or null after <see cref="Clear"/>.</summary>
    public string? Announcement => _last;

    /// <summary>Every announcement since the control was created, oldest first (tests read it).</summary>
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

    /// <summary>Empties the region without saying anything.</summary>
    public void Clear()
    {
        _last = null;
        _toggle = false;
        AutomationProperties.SetName(this, string.Empty);
    }

    protected override Size MeasureOverride(Size availableSize) => default;
}
