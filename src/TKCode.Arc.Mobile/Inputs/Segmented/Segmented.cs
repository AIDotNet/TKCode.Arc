using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.VisualTree;
using TKCode.Arc.Primitives;

namespace TKCode.Arc.Mobile;

/// <summary>
/// Mobile segmented control: a short row of <see cref="SegmentedItem"/>s that share the width, one selected, for
/// switching between a few views of the same content. It derives from Avalonia's <see cref="ListBox"/>, so
/// <c>SelectedIndex</c>, <c>SelectionChanged</c> and binding work as they do there. It has no hover state. The selected
/// block slides between segments on a spring (and jumps with reduced motion) while the label colors cross-fade.
/// </summary>
[TemplatePart("PART_Glider", typeof(GlidingIndicator))]
public class Segmented : ListBox
{
    private SelectionGlider? _glider;

    protected override Type StyleKeyOverride => typeof(Segmented);

    protected override bool NeedsContainerOverride(object? item, int index, out object? recycleKey)
        => NeedsContainer<SegmentedItem>(item, out recycleKey);

    protected override Control CreateContainerForItemOverride(object? item, int index, object? recycleKey)
        => new SegmentedItem();

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _glider?.Detach();
        _glider = e.NameScope.Find<GlidingIndicator>("PART_Glider") is { } glider
            ? new SelectionGlider(this, glider, static container => container)
            : null;
        if (this.IsAttachedToVisualTree())
        {
            _glider?.Attach();
        }
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _glider?.Attach();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        _glider?.Detach();
        base.OnDetachedFromVisualTree(e);
    }

    /// <summary>The sliding selected block, for tests.</summary>
    internal GlidingIndicator? Glider => _glider?.Glider;
}
