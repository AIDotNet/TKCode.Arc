using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;

namespace TKCode.Arc;

/// <summary>
/// The hidden live region of the Developer blocks (Arc's visually hidden <c>role="status"</c> footer): screen readers say
/// its name whenever it changes, and saying the same text twice still says it again (a second "Key copied." after a second
/// copy). It takes no space and no input. Polite by default; <see cref="IsAssertive"/> interrupts (a scope conflict).
/// </summary>
internal sealed class DeveloperAnnouncer : Control
{
    /// <summary>True interrupts the screen reader; false (default) waits politely.</summary>
    public static readonly StyledProperty<bool> IsAssertiveProperty =
        AvaloniaProperty.Register<DeveloperAnnouncer, bool>(nameof(IsAssertive));

    private const int HistoryLimit = 64;

    private string? _last;
    private bool _toggle;

    public DeveloperAnnouncer()
    {
        AutomationProperties.SetLiveSetting(this, AutomationLiveSetting.Polite);
        IsHitTestVisible = false;
        Focusable = false;
    }

    /// <inheritdoc cref="IsAssertiveProperty"/>
    public bool IsAssertive { get => GetValue(IsAssertiveProperty); set => SetValue(IsAssertiveProperty, value); }

    /// <summary>The last announced text (without the repeat marker), or null after <see cref="Clear"/>.</summary>
    public string? Announcement => _last;

    /// <summary>Every announcement since the control was created, oldest first (at most 64; tests and demos read it).</summary>
    public IReadOnlyList<string> History => _history;

    private readonly List<string> _history = new();

    /// <summary>Raised after every announcement with its text (an event log in a demo).</summary>
    public event Action<string>? Announced;

    /// <summary>Announces <paramref name="text"/>; null or empty text is ignored.</summary>
    public void Announce(string? text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return;
        }
        // A repeated message toggles an invisible marker so the name still changes and is read again.
        _toggle = text == _last && !_toggle;
        _last = text;
        _history.Add(text);
        if (_history.Count > HistoryLimit)
        {
            _history.RemoveAt(0);
        }
        AutomationProperties.SetName(this, _toggle ? text + "​" : text);
        Announced?.Invoke(text);
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
