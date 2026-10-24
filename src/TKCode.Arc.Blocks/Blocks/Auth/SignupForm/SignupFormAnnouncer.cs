// Ported from Arc's free signup-form block (MIT, Copyright (c) 2026 Elia Kuratli): the visually hidden live regions.
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;

namespace TKCode.Arc;

/// <summary>
/// A hidden live region of <see cref="SignupForm"/> (Arc's visually hidden <c>role="status" aria-live="polite"</c>
/// paragraph, and <c>role="alert"</c> for errors): screen readers say its name whenever it changes, and the same text said
/// twice is read again. Takes no space and no input.
/// </summary>
internal sealed class SignupFormAnnouncer : Control
{
    /// <summary>True interrupts the screen reader (errors); false waits politely.</summary>
    public static readonly StyledProperty<bool> IsAssertiveProperty =
        AvaloniaProperty.Register<SignupFormAnnouncer, bool>(nameof(IsAssertive));

    private string? _last;
    private bool _toggle;

    public SignupFormAnnouncer()
    {
        AutomationProperties.SetLiveSetting(this, AutomationLiveSetting.Polite);
        IsHitTestVisible = false;
        Focusable = false;
    }

    /// <inheritdoc cref="IsAssertiveProperty"/>
    public bool IsAssertive { get => GetValue(IsAssertiveProperty); set => SetValue(IsAssertiveProperty, value); }

    /// <summary>The last announced text, or null.</summary>
    public string? Announcement => _last;

    /// <summary>Every announcement, oldest first (at most 64; tests read it).</summary>
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
