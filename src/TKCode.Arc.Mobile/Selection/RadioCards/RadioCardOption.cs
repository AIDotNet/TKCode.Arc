using Avalonia;

namespace TKCode.Arc.Mobile;

/// <summary>
/// One option of a mobile <see cref="RadioCards"/>: a label with an optional description and icon. Write options inline
/// in the group or bind a list of them to <c>ItemsSource</c>; the cards follow later changes of their properties.
/// </summary>
public class RadioCardOption : AvaloniaObject
{
    /// <summary>The value the group reports as its <c>SelectedValue</c> while this option is selected.</summary>
    public static readonly StyledProperty<object?> ValueProperty =
        AvaloniaProperty.Register<RadioCardOption, object?>(nameof(Value));

    /// <summary>The card's title (text or any content); falls back to <see cref="Value"/>.</summary>
    public static readonly StyledProperty<object?> LabelProperty =
        AvaloniaProperty.Register<RadioCardOption, object?>(nameof(Label));

    /// <summary>One or two short lines under the label.</summary>
    public static readonly StyledProperty<string?> DescriptionProperty =
        AvaloniaProperty.Register<RadioCardOption, string?>(nameof(Description));

    /// <summary>
    /// Shown above the label: a <see cref="Avalonia.Media.Geometry"/> (for example a Lucide icon from <c>ArcIcons</c>) is
    /// drawn as a 22px stroke glyph; any other content (a color swatch, a shape) is shown as it is.
    /// </summary>
    public static readonly StyledProperty<object?> IconProperty =
        AvaloniaProperty.Register<RadioCardOption, object?>(nameof(Icon));

    /// <summary>Whether the card can be chosen. A disabled card is dimmed and skipped by the arrow keys.</summary>
    public static readonly StyledProperty<bool> IsEnabledProperty =
        AvaloniaProperty.Register<RadioCardOption, bool>(nameof(IsEnabled), true);

    /// <summary>
    /// Whether the card is shown. A hidden card takes no room and is skipped by the arrow keys; it stays in the group's
    /// items, so when it is the selected one the selection is kept and only the selection ring is not drawn.
    /// </summary>
    public static readonly StyledProperty<bool> IsVisibleProperty =
        AvaloniaProperty.Register<RadioCardOption, bool>(nameof(IsVisible), true);

    /// <summary>Creates an empty option.</summary>
    public RadioCardOption()
    {
    }

    /// <summary>Creates an option with a value, a label and an optional description.</summary>
    public RadioCardOption(object? value, object? label, string? description = null)
    {
        Value = value;
        Label = label;
        Description = description;
    }

    /// <inheritdoc cref="ValueProperty"/>
    public object? Value
    {
        get => GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    /// <inheritdoc cref="LabelProperty"/>
    public object? Label
    {
        get => GetValue(LabelProperty);
        set => SetValue(LabelProperty, value);
    }

    /// <inheritdoc cref="DescriptionProperty"/>
    public string? Description
    {
        get => GetValue(DescriptionProperty);
        set => SetValue(DescriptionProperty, value);
    }

    /// <inheritdoc cref="IconProperty"/>
    public object? Icon
    {
        get => GetValue(IconProperty);
        set => SetValue(IconProperty, value);
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

    /// <inheritdoc />
    public override string ToString() => Label?.ToString() ?? Value?.ToString() ?? string.Empty;
}
