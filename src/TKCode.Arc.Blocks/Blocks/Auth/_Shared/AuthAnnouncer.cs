// Ported from Arc's free sign-in, login-centered and signup-form blocks (MIT, Copyright (c) 2026 Elia Kuratli): the
// visually hidden live regions.
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;

namespace TKCode.Arc;

/// <summary>
/// A hidden live region of the Auth blocks (Arc's visually hidden <c>role="status" aria-live="polite"</c> paragraph, and
/// <c>role="alert"</c> for errors): screen readers say its name whenever it changes. Takes no space and no input.
/// Polite by default; <see cref="IsAssertive"/> for errors. By default the same text said twice is read again (an
/// invisible marker toggles so the name still changes); <see cref="RepeatsSameText"/> false skips it instead, as a React
/// re-render would. Empty text is ignored, or empties the region when <see cref="ClearsOnEmpty"/> is true.
/// </summary>
internal sealed class AuthAnnouncer : Control
{
    /// <summary>True interrupts the screen reader (errors); false waits politely.</summary>
    public static readonly StyledProperty<bool> IsAssertiveProperty =
        AvaloniaProperty.Register<AuthAnnouncer, bool>(nameof(IsAssertive));

    private string? _last;
    private bool _toggle;

    public AuthAnnouncer()
    {
        AutomationProperties.SetLiveSetting(this, AutomationLiveSetting.Polite);
        IsHitTestVisible = false;
        Focusable = false;
    }

    /// <inheritdoc cref="IsAssertiveProperty"/>
    public bool IsAssertive { get => GetValue(IsAssertiveProperty); set => SetValue(IsAssertiveProperty, value); }

    /// <summary>True (default): the same text again is read again; false: the same text again is not repeated.</summary>
    public bool RepeatsSameText { get; set; } = true;

    /// <summary>True: empty text empties the region silently (<see cref="Clear"/>); false (default): empty text is ignored.</summary>
    public bool ClearsOnEmpty { get; set; }

    /// <summary>The last announced text, or null.</summary>
    public string? Announcement => _last;

    /// <summary>Every announcement since the control was created, oldest first (at most 64; tests read it).</summary>
    public List<string> History { get; } = new();

    /// <summary>Announces <paramref name="text"/>.</summary>
    public void Announce(string? text)
    {
        if (string.IsNullOrEmpty(text))
        {
            if (ClearsOnEmpty)
            {
                Clear();
            }
            return;
        }
        if (!RepeatsSameText && text == _last)
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
