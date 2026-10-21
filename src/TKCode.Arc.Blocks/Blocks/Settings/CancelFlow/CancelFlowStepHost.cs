using Avalonia;
using Avalonia.Controls;

namespace TKCode.Arc;

/// <summary>
/// Carries one step view of a <see cref="CancelFlow"/> into the stage. Each step change gets a new host, so a view can
/// move into the new layer while the old host is still sliding out. The host always takes the full width it is given:
/// a leaving step keeps its own size, and a narrower one would move its right-aligned buttons as it leaves.
/// </summary>
internal sealed class CancelFlowStepHost : Decorator
{
    protected override Size MeasureOverride(Size availableSize)
    {
        var size = base.MeasureOverride(availableSize);
        return double.IsInfinity(availableSize.Width) ? size : size.WithWidth(availableSize.Width);
    }
}
