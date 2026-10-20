using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Metadata;

namespace TKCode.Arc;

/// <summary>
/// Declares one option of a <see cref="Select"/>, <see cref="Combobox"/>, <see cref="MultiSelect"/>,
/// <see cref="MorphSelect"/> or <see cref="ChipGroup"/> inline in XAML:
/// <c>&lt;arc:SelectItem Content="United States" Value="us" /&gt;</c>. The owning control draws the option itself
/// (virtualized and filtered without containers), so this element only carries data; set
/// <c>IsEnabled</c> to false for an option that can be seen but not picked.
/// </summary>
public class SelectItem : Control
{
    /// <summary>Defines the <see cref="Content"/> property.</summary>
    public static readonly StyledProperty<object?> ContentProperty =
        AvaloniaProperty.Register<SelectItem, object?>(nameof(Content));

    /// <summary>Defines the <see cref="Value"/> property.</summary>
    public static readonly StyledProperty<object?> ValueProperty =
        AvaloniaProperty.Register<SelectItem, object?>(nameof(Value));

    /// <summary>Defines the <see cref="Icon"/> property.</summary>
    public static readonly StyledProperty<object?> IconProperty =
        AvaloniaProperty.Register<SelectItem, object?>(nameof(Icon));

    /// <summary>Defines the <see cref="Meta"/> property.</summary>
    public static readonly StyledProperty<string?> MetaProperty =
        AvaloniaProperty.Register<SelectItem, string?>(nameof(Meta));

    /// <summary>Defines the <see cref="Keywords"/> property.</summary>
    public static readonly StyledProperty<string?> KeywordsProperty =
        AvaloniaProperty.Register<SelectItem, string?>(nameof(Keywords));

    /// <summary>Defines the <see cref="Group"/> property.</summary>
    public static readonly StyledProperty<string?> GroupProperty =
        AvaloniaProperty.Register<SelectItem, string?>(nameof(Group));

    /// <summary>The label people see and search (strings are shown as is; other values use ToString()).</summary>
    [Content]
    public object? Content
    {
        get => GetValue(ContentProperty);
        set => SetValue(ContentProperty, value);
    }

    /// <summary>The value stored when this option is picked. Defaults to <see cref="Content"/>.</summary>
    public object? Value
    {
        get => GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    /// <summary>
    /// A 20px mark before the label (<see cref="MorphSelect"/>): a <see cref="Geometry"/> drawn as a 24×24 Lucide
    /// stroke glyph (for example <c>ArcIcons.Globe</c>) or an <see cref="IImage"/> shown as a round avatar.
    /// </summary>
    public object? Icon
    {
        get => GetValue(IconProperty);
        set => SetValue(IconProperty, value);
    }

    /// <summary>Short trailing detail such as an offset or a count (<see cref="MorphSelect"/>).</summary>
    public string? Meta
    {
        get => GetValue(MetaProperty);
        set => SetValue(MetaProperty, value);
    }

    /// <summary>Extra words that match this option when searching (space separated).</summary>
    public string? Keywords
    {
        get => GetValue(KeywordsProperty);
        set => SetValue(KeywordsProperty, value);
    }

    /// <summary>Heading of the run of options this one belongs to (<see cref="MorphSelect"/> groups).</summary>
    public string? Group
    {
        get => GetValue(GroupProperty);
        set => SetValue(GroupProperty, value);
    }

    /// <summary>Raised when any option data changes so the owning list can refresh its rows.</summary>
    internal event EventHandler? OptionChanged;

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ContentProperty || change.Property == ValueProperty || change.Property == IconProperty ||
            change.Property == MetaProperty || change.Property == KeywordsProperty || change.Property == GroupProperty ||
            change.Property == IsEnabledProperty)
        {
            OptionChanged?.Invoke(this, EventArgs.Empty);
        }
    }
}

/// <summary>
/// A plain option record for <c>ItemsSource</c> lists that every select in this category understands without
/// selectors: <c>new SelectOption("us", "United States")</c>.
/// </summary>
public sealed class SelectOption
{
    public SelectOption()
    {
    }

    public SelectOption(object? value, string label, bool isDisabled = false)
    {
        Value = value;
        Label = label;
        IsDisabled = isDisabled;
    }

    /// <summary>The value stored when the option is picked.</summary>
    public object? Value { get; init; }

    /// <summary>The label people see and search.</summary>
    public string Label { get; init; } = string.Empty;

    /// <summary>Shown but not pickable.</summary>
    public bool IsDisabled { get; init; }

    /// <summary>Extra words that match when searching.</summary>
    public string? Keywords { get; init; }

    /// <summary>Short trailing detail (<see cref="MorphSelect"/>).</summary>
    public string? Meta { get; init; }

    /// <summary>A Lucide <see cref="Geometry"/> or an <see cref="IImage"/> avatar (<see cref="MorphSelect"/>).</summary>
    public object? Icon { get; init; }

    /// <summary>Group heading (<see cref="MorphSelect"/>).</summary>
    public string? Group { get; init; }

    public override string ToString() => Label;
}
