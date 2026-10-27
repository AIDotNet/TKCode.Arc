using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;

namespace TKCode.Arc;

/// <summary>
/// A hidden live region for the Finance blocks (a visually hidden <c>role="status"</c>):
/// screen readers say its name whenever it changes, and saying the same text twice still says it again. It takes no space
/// and no input. Polite by default.
/// </summary>
internal sealed class FinanceAnnouncer : Control
{
    /// <summary>True interrupts the screen reader; false waits politely.</summary>
    public static readonly StyledProperty<bool> IsAssertiveProperty =
        AvaloniaProperty.Register<FinanceAnnouncer, bool>(nameof(IsAssertive));

    private string? _last;
    private bool _toggle;

    public FinanceAnnouncer()
    {
        AutomationProperties.SetLiveSetting(this, AutomationLiveSetting.Polite);
        IsHitTestVisible = false;
        Focusable = false;
    }

    /// <inheritdoc cref="IsAssertiveProperty"/>
    public bool IsAssertive { get => GetValue(IsAssertiveProperty); set => SetValue(IsAssertiveProperty, value); }

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
        AutomationProperties.SetName(this, _toggle ? text + "\u200B" : text);
    }

    /// <summary>Empties the region without saying anything.</summary>
    public void Clear()
    {
        _last = null;
        _toggle = false;
        AutomationProperties.SetName(this, string.Empty);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IsAssertiveProperty)
        {
            AutomationProperties.SetLiveSetting(this, IsAssertive ? AutomationLiveSetting.Assertive : AutomationLiveSetting.Polite);
        }
    }

    protected override Size MeasureOverride(Size availableSize) => default;
}
