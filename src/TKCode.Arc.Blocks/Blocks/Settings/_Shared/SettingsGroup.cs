using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;

namespace TKCode.Arc;

/// <summary>How a <see cref="SettingsGroup"/> is drawn.</summary>
public enum SettingsGroupAppearance
{
    /// <summary>A card: surface, 1px border, panel radius, inner padding.</summary>
    Card,

    /// <summary>No frame or padding: rows on the page itself, for sections separated by hairlines.</summary>
    Plain
}

/// <summary>
/// A group of related settings: an optional header with a description and actions, a stack of rows (usually
/// <see cref="SettingsRow"/>, written inline or bound through <see cref="ItemsControl.ItemsSource"/>), and an optional
/// footer strip for a note or a save button. Drawn as a card by default, or plain inside a page.
/// </summary>
[PseudoClasses(PlainPseudoClass)]
public class SettingsGroup : HeaderedItemsControl
{
    public const string PlainPseudoClass = ":plain";

    /// <summary>A sentence under the header.</summary>
    public static readonly StyledProperty<string?> DescriptionProperty =
        AvaloniaProperty.Register<SettingsGroup, string?>(nameof(Description));

    /// <summary>Content at the right of the header, such as a button.</summary>
    public static readonly StyledProperty<object?> HeaderActionsProperty =
        AvaloniaProperty.Register<SettingsGroup, object?>(nameof(HeaderActions));

    /// <summary>Content of the strip under the rows, such as a note and a save button.</summary>
    public static readonly StyledProperty<object?> FooterProperty =
        AvaloniaProperty.Register<SettingsGroup, object?>(nameof(Footer));

    /// <summary>Card (default) or Plain.</summary>
    public static readonly StyledProperty<SettingsGroupAppearance> AppearanceProperty =
        AvaloniaProperty.Register<SettingsGroup, SettingsGroupAppearance>(nameof(Appearance));

    /// <inheritdoc cref="DescriptionProperty"/>
    public string? Description
    {
        get => GetValue(DescriptionProperty);
        set => SetValue(DescriptionProperty, value);
    }

    /// <inheritdoc cref="HeaderActionsProperty"/>
    public object? HeaderActions
    {
        get => GetValue(HeaderActionsProperty);
        set => SetValue(HeaderActionsProperty, value);
    }

    /// <inheritdoc cref="FooterProperty"/>
    public object? Footer
    {
        get => GetValue(FooterProperty);
        set => SetValue(FooterProperty, value);
    }

    /// <inheritdoc cref="AppearanceProperty"/>
    public SettingsGroupAppearance Appearance
    {
        get => GetValue(AppearanceProperty);
        set => SetValue(AppearanceProperty, value);
    }

    static SettingsGroup()
    {
        FocusableProperty.OverrideDefaultValue<SettingsGroup>(false);
    }

    protected override Type StyleKeyOverride => typeof(SettingsGroup);

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == AppearanceProperty)
        {
            PseudoClasses.Set(PlainPseudoClass, Appearance == SettingsGroupAppearance.Plain);
        }
    }
}
