using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Media;

namespace TKCode.Arc.Mobile;

/// <summary>One tab of a <see cref="Tabbar"/>: an <see cref="Icon"/> above its label (the content). It is at least 44 high and has no hover state.</summary>
[TemplatePart("PART_Indicator", typeof(Control))]
public class TabbarItem : ListBoxItem
{
    /// <summary>The glyph geometry shown above the label, for example a Lucide icon.</summary>
    public static readonly StyledProperty<Geometry?> IconProperty =
        AvaloniaProperty.Register<TabbarItem, Geometry?>(nameof(Icon));

    /// <inheritdoc cref="IconProperty"/>
    public Geometry? Icon
    {
        get => GetValue(IconProperty);
        set => SetValue(IconProperty, value);
    }

    /// <summary>The element behind the icon that the tab bar's selection pill covers.</summary>
    internal Control? IndicatorHost { get; private set; }

    protected override Type StyleKeyOverride => typeof(TabbarItem);

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        IndicatorHost = e.NameScope.Find<Control>("PART_Indicator");
    }
}
