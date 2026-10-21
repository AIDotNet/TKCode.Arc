using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>
/// One section of a <see cref="SettingsPage"/>: its <see cref="HeaderedContentControl.Header"/>, <see cref="Icon"/> and
/// <see cref="Badge"/> appear in the page's navigation, and while it is selected the section itself (header,
/// <see cref="Description"/> and content) fills the page's content area. Set <see cref="IsDirty"/> while it has unsaved
/// changes to show the page's save bar.
/// </summary>
[PseudoClasses(DirtyPseudoClass)]
public class SettingsSection : HeaderedContentControl
{
    public const string DirtyPseudoClass = ":dirty";

    /// <summary>A 24×24 stroke icon shown before the header in the navigation.</summary>
    public static readonly StyledProperty<Geometry?> IconProperty =
        AvaloniaProperty.Register<SettingsSection, Geometry?>(nameof(Icon));

    /// <summary>A sentence under the section title in the content area.</summary>
    public static readonly StyledProperty<string?> DescriptionProperty =
        AvaloniaProperty.Register<SettingsSection, string?>(nameof(Description));

    /// <summary>A short marker at the end of the navigation item, such as "New" or a count.</summary>
    public static readonly StyledProperty<string?> BadgeProperty =
        AvaloniaProperty.Register<SettingsSection, string?>(nameof(Badge));

    /// <summary>Whether the section has unsaved changes: the page shows its save bar while this section is selected.</summary>
    public static readonly StyledProperty<bool> IsDirtyProperty =
        AvaloniaProperty.Register<SettingsSection, bool>(nameof(IsDirty));

    /// <inheritdoc cref="IconProperty"/>
    public Geometry? Icon
    {
        get => GetValue(IconProperty);
        set => SetValue(IconProperty, value);
    }

    /// <inheritdoc cref="DescriptionProperty"/>
    public string? Description
    {
        get => GetValue(DescriptionProperty);
        set => SetValue(DescriptionProperty, value);
    }

    /// <inheritdoc cref="BadgeProperty"/>
    public string? Badge
    {
        get => GetValue(BadgeProperty);
        set => SetValue(BadgeProperty, value);
    }

    /// <inheritdoc cref="IsDirtyProperty"/>
    public bool IsDirty
    {
        get => GetValue(IsDirtyProperty);
        set => SetValue(IsDirtyProperty, value);
    }

    static SettingsSection()
    {
        FocusableProperty.OverrideDefaultValue<SettingsSection>(false);
    }

    protected override Type StyleKeyOverride => typeof(SettingsSection);

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IsDirtyProperty)
        {
            PseudoClasses.Set(DirtyPseudoClass, IsDirty);
        }
        if (change.Property == HeaderProperty || change.Property == IconProperty || change.Property == BadgeProperty ||
            change.Property == IsDirtyProperty || change.Property == IsEnabledProperty)
        {
            (Parent as SettingsPage)?.OnSectionChanged(this);
        }
    }
}
