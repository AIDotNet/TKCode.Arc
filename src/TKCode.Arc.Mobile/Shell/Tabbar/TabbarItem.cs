using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace TKCode.Arc.Mobile;

/// <summary>One tab of a <see cref="Tabbar"/>: an <see cref="Icon"/> above its label (the content). It is at least 44 high and has no hover state.</summary>
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

    protected override Type StyleKeyOverride => typeof(TabbarItem);
}
