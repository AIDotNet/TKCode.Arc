using Avalonia.Controls;

namespace TKCode.Arc.Mobile;

/// <summary>One segment of a <see cref="Segmented"/>: its label is the content. The selected one is raised on a Surface fill.</summary>
public class SegmentedItem : ListBoxItem
{
    protected override Type StyleKeyOverride => typeof(SegmentedItem);
}
