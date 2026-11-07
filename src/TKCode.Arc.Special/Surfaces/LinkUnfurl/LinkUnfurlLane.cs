using Avalonia;
using Avalonia.Controls;

namespace TKCode.Arc;

/// <summary>
/// The lane under the message where the cards live, in the order of their links: each card is at most 440 wide (it
/// fills a narrower lane) and owns a slot of (card height + 10)·ph, so the lane, and the toolbar under it, grow and
/// shrink with the unfurls on the same progress. Each layout pass also places every travelling card on its link (the
/// link's box is read from the editor here, after the text is laid out).
/// </summary>
internal sealed class LinkUnfurlLane : Panel
{
    public const double CardMaxWidth = 440;
    public const double Gap = 10;

    public LinkUnfurlLane()
    {
        ClipToBounds = false;
    }

    /// <summary>The composer that owns the lane (reads the links' boxes).</summary>
    internal LinkUnfurl? Owner { get; set; }

    /// <summary>The width of a card in a lane <paramref name="width"/> wide.</summary>
    public static double CardWidth(double width) => Math.Max(0, Math.Min(CardMaxWidth, width));

    protected override Size MeasureOverride(Size availableSize)
    {
        var width = CardWidth(double.IsInfinity(availableSize.Width) ? CardMaxWidth : availableSize.Width);
        var height = 0.0;
        foreach (var child in Children)
        {
            child.Measure(new Size(width, double.PositiveInfinity));
            if (child is LinkUnfurlCardHost host && host.IsVisible)
            {
                height += (child.DesiredSize.Height + Gap) * LinkUnfurlMorph.Deep(host.Entry.Progress.Value);
            }
        }
        return new Size(double.IsInfinity(availableSize.Width) ? width : availableSize.Width, height);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var width = CardWidth(finalSize.Width);
        var y = 0.0;
        foreach (var child in Children)
        {
            if (child is not LinkUnfurlCardHost host)
            {
                child.Arrange(new Rect(0, y, width, child.DesiredSize.Height));
                continue;
            }
            var height = child.DesiredSize.Height;
            var slot = new Rect(0, y, width, height);
            child.Arrange(slot);
            if (host.IsVisible)
            {
                var p = host.Entry.Progress.Value;
                y += (height + Gap) * LinkUnfurlMorph.Deep(p);
                Owner?.PlaceCard(host, slot, this);
            }
        }
        return finalSize;
    }
}
