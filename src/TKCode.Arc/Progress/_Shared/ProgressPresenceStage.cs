using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>
/// Stacks <see cref="PresenceHost"/> children (Skeleton placeholder / content, Countdown digits / live label). Only the
/// present ones decide the size, so a child that is leaving fades out of layout (AnimatePresence popLayout).
/// </summary>
internal sealed class ProgressPresenceStage : Panel
{
    protected override Size MeasureOverride(Size availableSize)
    {
        var size = default(Size);
        var any = false;
        foreach (var child in Children)
        {
            child.Measure(availableSize);
            if (child is PresenceHost { IsPresent: true })
            {
                size = new Size(Math.Max(size.Width, child.DesiredSize.Width), Math.Max(size.Height, child.DesiredSize.Height));
                any = true;
            }
        }
        if (!any)
        {
            foreach (var child in Children)
            {
                size = new Size(Math.Max(size.Width, child.DesiredSize.Width), Math.Max(size.Height, child.DesiredSize.Height));
            }
        }
        return size;
    }

    public static readonly StyledProperty<bool> IsCenteredProperty =
        AvaloniaProperty.Register<ProgressPresenceStage, bool>(nameof(IsCentered));

    /// <summary>Centers each child in the stage (Countdown) instead of stretching it from the top-left (Skeleton).</summary>
    public bool IsCentered { get => GetValue(IsCenteredProperty); set => SetValue(IsCenteredProperty, value); }

    protected override Size ArrangeOverride(Size finalSize)
    {
        foreach (var child in Children)
        {
            var size = child.DesiredSize;
            child.Arrange(IsCentered
                ? new Rect((finalSize.Width - size.Width) / 2, (finalSize.Height - size.Height) / 2, size.Width, size.Height)
                : new Rect(0, 0, finalSize.Width, Math.Max(size.Height, 0)));
        }
        return finalSize;
    }
}
