using Avalonia.Controls;

namespace TKCode.Arc.Mobile;

/// <summary>
/// The bottom tab bar of a <see cref="Page"/>. Put <see cref="TabbarItem"/>s in it; the selected one is highlighted
/// and the items share the width equally.
/// </summary>
public class Tabbar : ListBox
{
    protected override Type StyleKeyOverride => typeof(Tabbar);

    protected override bool NeedsContainerOverride(object? item, int index, out object? recycleKey)
        => NeedsContainer<TabbarItem>(item, out recycleKey);

    protected override Control CreateContainerForItemOverride(object? item, int index, object? recycleKey)
        => new TabbarItem();
}
