using Avalonia.Controls;

namespace TKCode.Arc.Mobile;

/// <summary>
/// Mobile segmented control: a short row of <see cref="SegmentedItem"/>s that share the width, one selected, for
/// switching between a few views of the same content. It derives from Avalonia's <see cref="ListBox"/>, so
/// <c>SelectedIndex</c>, <c>SelectionChanged</c> and binding work as they do there. It has no hover state.
/// </summary>
public class Segmented : ListBox
{
    protected override Type StyleKeyOverride => typeof(Segmented);

    protected override bool NeedsContainerOverride(object? item, int index, out object? recycleKey)
        => NeedsContainer<SegmentedItem>(item, out recycleKey);

    protected override Control CreateContainerForItemOverride(object? item, int index, object? recycleKey)
        => new SegmentedItem();
}
