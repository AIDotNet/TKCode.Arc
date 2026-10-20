using Avalonia;
using Avalonia.Controls;

namespace TKCode.Arc;

/// <summary>
/// The card layer of a <see cref="CardStack"/>: realizes only the top four cards plus any card still in flight, no
/// matter how long the stack is, and lays every card out at the same place (the stage's card area). Depth, drag and
/// throws are render transforms on the cards, so the panel only re-lays out when the set of cards changes.
/// </summary>
internal sealed class CardStackPanel : MediaVirtualPanel
{
    public CardStackPanel()
    {
        ClipToBounds = false;
    }

    private CardStack? Owner => ItemsControl as CardStack;

    internal int RealizedCount => Realized.Count;

    internal CardStackCard? CardAt(int index) => Realized.TryGetValue(index, out var card) ? card as CardStackCard : null;

    internal SortedDictionary<int, Control> Cards => Realized;

    protected override bool ShouldRealize(int index) => Owner?.IsCardShown(index) ?? false;

    protected override void OnItemsReset() => Owner?.OnItemsReset();

    protected override void OnRealized(Control container, int index) => Owner?.OnCardRealized(container, index);

    protected override void OnRecycling(Control container, int index) => Owner?.OnCardRecycling(container);

    /// <summary>Re-evaluates which cards are shown after the stack changed.</summary>
    internal void Refresh() => InvalidateMeasure();

    protected override Size MeasureOverride(Size availableSize)
    {
        var owner = Owner;
        var width = double.IsFinite(availableSize.Width) ? availableSize.Width : 320;
        var height = double.IsFinite(availableSize.Height) ? availableSize.Height : 304;
        if (owner is null)
        {
            return new Size(width, height);
        }
        RecycleUnwanted();
        foreach (var index in owner.ShownIndices())
        {
            Realize(index)?.Measure(new Size(width, height));
        }
        return new Size(width, height);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        foreach (var pair in Realized)
        {
            pair.Value.Arrange(new Rect(finalSize));
        }
        return finalSize;
    }
}
