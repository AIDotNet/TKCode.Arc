using Avalonia.Interactivity;

namespace TKCode.Arc;

/// <summary>Event data of <see cref="CoverFlow.ItemInvoked"/>: the cover that was opened.</summary>
public sealed class CoverFlowItemInvokedEventArgs : RoutedEventArgs
{
    internal CoverFlowItemInvokedEventArgs(int index, CoverFlowItem item)
        : base(CoverFlow.ItemInvokedEvent)
    {
        Index = index;
        Item = item;
    }

    /// <summary>The position of the cover.</summary>
    public int Index { get; }

    /// <summary>The cover.</summary>
    public CoverFlowItem Item { get; }
}
