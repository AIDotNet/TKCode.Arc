using Avalonia;
using Avalonia.Input;

namespace TKCode.Arc;

/// <summary>
/// A shortcut shown on a <see cref="KeycapKeyboard"/>: while its modifiers are held, its key shows <see cref="Label"/>.
/// </summary>
public class KeycapBinding : AvaloniaObject
{
    /// <summary>The shortcut, such as "Ctrl+S", "Cmd+Shift+P" or "⌘K". A key with no modifiers shows when none are held.</summary>
    public static readonly StyledProperty<string?> GestureProperty =
        AvaloniaProperty.Register<KeycapBinding, string?>(nameof(Gesture));

    /// <summary>What the shortcut does, in a word or two, printed small on its key.</summary>
    public static readonly StyledProperty<string?> LabelProperty =
        AvaloniaProperty.Register<KeycapBinding, string?>(nameof(Label));

    /// <inheritdoc cref="GestureProperty"/>
    public string? Gesture
    {
        get => GetValue(GestureProperty);
        set => SetValue(GestureProperty, value);
    }

    /// <inheritdoc cref="LabelProperty"/>
    public string? Label
    {
        get => GetValue(LabelProperty);
        set => SetValue(LabelProperty, value);
    }

    /// <summary>The modifiers of <see cref="Gesture"/>; None when it has none or cannot be read.</summary>
    public KeyModifiers Modifiers => KeycapKeyboardLayout.TryParse(Gesture, out var modifiers, out _) ? modifiers : KeyModifiers.None;

    /// <summary>The key of <see cref="Gesture"/>; None when it names no key or cannot be read.</summary>
    public PhysicalKey Key => KeycapKeyboardLayout.TryParse(Gesture, out _, out var key) ? key : PhysicalKey.None;
}
