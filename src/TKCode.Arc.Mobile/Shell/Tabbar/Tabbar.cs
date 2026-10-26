using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.VisualTree;
using TKCode.Arc.Primitives;

namespace TKCode.Arc.Mobile;

/// <summary>
/// The bottom tab bar of a <see cref="Page"/>. Put <see cref="TabbarItem"/>s in it; the selected one is highlighted
/// and the items share the width equally. One accent pill glides behind the icon of the selected item on a spring,
/// and jumps with reduced motion.
/// </summary>
[TemplatePart("PART_Glider", typeof(GlidingIndicator))]
public class Tabbar : ListBox
{
    private SelectionGlider? _glider;

    protected override Type StyleKeyOverride => typeof(Tabbar);

    protected override bool NeedsContainerOverride(object? item, int index, out object? recycleKey)
        => NeedsContainer<TabbarItem>(item, out recycleKey);

    protected override Control CreateContainerForItemOverride(object? item, int index, object? recycleKey)
        => new TabbarItem();

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _glider?.Detach();
        _glider = e.NameScope.Find<GlidingIndicator>("PART_Glider") is { } glider
            ? new SelectionGlider(this, glider, static container => (container as TabbarItem)?.IndicatorHost)
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

    /// <summary>The pill indicator, for tests.</summary>
    internal GlidingIndicator? Glider => _glider?.Glider;
}
