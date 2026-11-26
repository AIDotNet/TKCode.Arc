using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Media;

namespace TKCode.Arc.Mobile;

/// <summary>One tab of a <see cref="Tabbar"/>: an <see cref="Icon"/> above its label (the content), with optional <see cref="Badge"/> text or unread <see cref="ShowBadgeDot"/> indicator. It is at least 44 high and has no hover state.</summary>
[TemplatePart("PART_Indicator", typeof(Control))]
[PseudoClasses(":has-badge", ":has-badge-dot")]
public class TabbarItem : ListBoxItem
{
    /// <summary>The glyph geometry shown above the label, for example a Lucide icon.</summary>
    public static readonly StyledProperty<Geometry?> IconProperty =
        AvaloniaProperty.Register<TabbarItem, Geometry?>(nameof(Icon));

    /// <summary>Optional short badge text (for example "3" or "99+") anchored over the top-right corner of the icon.</summary>
    public static readonly StyledProperty<string?> BadgeProperty =
        AvaloniaProperty.Register<TabbarItem, string?>(nameof(Badge));

    /// <summary>Whether to show a compact unread dot over the top-right corner of the icon when <see cref="Badge"/> is empty: false.</summary>
    public static readonly StyledProperty<bool> ShowBadgeDotProperty =
        AvaloniaProperty.Register<TabbarItem, bool>(nameof(ShowBadgeDot));

    static TabbarItem()
    {
        BadgeProperty.Changed.AddClassHandler<TabbarItem>((item, _) => item.UpdateBadgePseudoClasses());
        ShowBadgeDotProperty.Changed.AddClassHandler<TabbarItem>((item, _) => item.UpdateBadgePseudoClasses());
    }

    /// <inheritdoc cref="IconProperty"/>
    public Geometry? Icon
    {
        get => GetValue(IconProperty);
        set => SetValue(IconProperty, value);
    }

    /// <inheritdoc cref="BadgeProperty"/>
    public string? Badge
    {
        get => GetValue(BadgeProperty);
        set => SetValue(BadgeProperty, value);
    }

    /// <inheritdoc cref="ShowBadgeDotProperty"/>
    public bool ShowBadgeDot
    {
        get => GetValue(ShowBadgeDotProperty);
        set => SetValue(ShowBadgeDotProperty, value);
    }

    /// <summary>The element behind the icon that the tab bar's selection pill covers.</summary>
    internal Control? IndicatorHost { get; private set; }

    protected override Type StyleKeyOverride => typeof(TabbarItem);

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        IndicatorHost = e.NameScope.Find<Control>("PART_Indicator");
        UpdateBadgePseudoClasses();
    }

    private void UpdateBadgePseudoClasses()
    {
        var hasText = !string.IsNullOrEmpty(Badge);
        PseudoClasses.Set(":has-badge", hasText);
        PseudoClasses.Set(":has-badge-dot", !hasText && ShowBadgeDot);
    }
}
