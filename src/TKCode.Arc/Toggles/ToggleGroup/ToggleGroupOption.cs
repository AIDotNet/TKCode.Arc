using Avalonia;
using Avalonia.Media;
using Avalonia.Metadata;

namespace TKCode.Arc;

/// <summary>
/// One option of a <see cref="ToggleGroup"/> (Arc <c>ToggleGroup.Item</c>): an icon, content or both. Its properties can
/// be bound in XAML; the group follows later changes.
/// </summary>
public class ToggleGroupOption : AvaloniaObject, ITogglesValueSource
{
    /// <summary>The value reported by the group's <c>SelectedValue</c> and <c>SelectedValues</c>.</summary>
    public static readonly StyledProperty<object?> ValueProperty =
        AvaloniaProperty.Register<ToggleGroupOption, object?>(nameof(Value));

    /// <summary>The accessible name, required when the option shows only an icon. It is also the tooltip unless <c>Title</c> is set; with no icon or content it is the visible text.</summary>
    public static readonly StyledProperty<string?> LabelProperty =
        AvaloniaProperty.Register<ToggleGroupOption, string?>(nameof(Label));

    /// <summary>A stroked 24 by 24 glyph, drawn at 16 px (14 px in the small size) before the content.</summary>
    public static readonly StyledProperty<Geometry?> IconProperty =
        AvaloniaProperty.Register<ToggleGroupOption, Geometry?>(nameof(Icon));

    /// <summary>What the option shows, usually a short text.</summary>
    public static readonly StyledProperty<object?> ContentProperty =
        AvaloniaProperty.Register<ToggleGroupOption, object?>(nameof(Content));

    /// <summary>The tooltip, when it should differ from <c>Label</c>.</summary>
    public static readonly StyledProperty<string?> TitleProperty =
        AvaloniaProperty.Register<ToggleGroupOption, string?>(nameof(Title));

    /// <summary>A disabled option cannot be pressed and is skipped by the arrow keys.</summary>
    public static readonly StyledProperty<bool> IsDisabledProperty =
        AvaloniaProperty.Register<ToggleGroupOption, bool>(nameof(IsDisabled));

    public ToggleGroupOption()
    {
    }

    public ToggleGroupOption(object? value, string? label, Geometry? icon = null)
    {
        Value = value;
        Label = label;
        Icon = icon;
    }

    /// <inheritdoc cref="ValueProperty"/>
    public object? Value { get => GetValue(ValueProperty); set => SetValue(ValueProperty, value); }

    /// <inheritdoc cref="LabelProperty"/>
    public string? Label { get => GetValue(LabelProperty); set => SetValue(LabelProperty, value); }

    /// <inheritdoc cref="IconProperty"/>
    public Geometry? Icon { get => GetValue(IconProperty); set => SetValue(IconProperty, value); }

    /// <inheritdoc cref="ContentProperty"/>
    [Content]
    public object? Content { get => GetValue(ContentProperty); set => SetValue(ContentProperty, value); }

    /// <inheritdoc cref="TitleProperty"/>
    public string? Title { get => GetValue(TitleProperty); set => SetValue(TitleProperty, value); }

    /// <inheritdoc cref="IsDisabledProperty"/>
    public bool IsDisabled { get => GetValue(IsDisabledProperty); set => SetValue(IsDisabledProperty, value); }

    object? ITogglesValueSource.ToggleValue => Value;

    public override string ToString() => Label ?? Value?.ToString() ?? string.Empty;
}
