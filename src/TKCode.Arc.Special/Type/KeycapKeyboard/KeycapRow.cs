using Avalonia;

namespace TKCode.Arc;

/// <summary>
/// One row of a custom <see cref="KeycapKeyboard"/> board (<see cref="KeycapKeyboard.Rows"/>): its keys written as tokens
/// separated by spaces, such as "k e y backspace".
/// </summary>
public class KeycapRow : AvaloniaObject
{
    /// <summary>
    /// The keys, left to right, separated by spaces: single characters, or "space", "backspace", "enter", "shift", "tab"
    /// and "caps". Unknown words are skipped.
    /// </summary>
    public static readonly StyledProperty<string?> KeysProperty =
        AvaloniaProperty.Register<KeycapRow, string?>(nameof(Keys));

    /// <summary>Whether the row's keys can be pressed with the pointer or by keyboard focus. Real key presses still light them.</summary>
    public static readonly StyledProperty<bool> IsEnabledProperty =
        AvaloniaProperty.Register<KeycapRow, bool>(nameof(IsEnabled), true);

    /// <summary>Whether the row is shown. A hidden row takes no space and its keys do not light up.</summary>
    public static readonly StyledProperty<bool> IsVisibleProperty =
        AvaloniaProperty.Register<KeycapRow, bool>(nameof(IsVisible), true);

    /// <summary>Creates an empty row; set <see cref="Keys"/>.</summary>
    public KeycapRow()
    {
    }

    /// <summary>Creates a row of the given keys, written as in <see cref="Keys"/>.</summary>
    public KeycapRow(string keys)
    {
        Keys = keys;
    }

    /// <inheritdoc cref="KeysProperty"/>
    public string? Keys
    {
        get => GetValue(KeysProperty);
        set => SetValue(KeysProperty, value);
    }

    /// <inheritdoc cref="IsEnabledProperty"/>
    public bool IsEnabled
    {
        get => GetValue(IsEnabledProperty);
        set => SetValue(IsEnabledProperty, value);
    }

    /// <inheritdoc cref="IsVisibleProperty"/>
    public bool IsVisible
    {
        get => GetValue(IsVisibleProperty);
        set => SetValue(IsVisibleProperty, value);
    }
}
