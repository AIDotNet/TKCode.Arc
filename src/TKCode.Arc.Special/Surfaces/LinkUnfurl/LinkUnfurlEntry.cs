using Avalonia.Media.TextFormatting;
using TKCode.Arc.Motion;

namespace TKCode.Arc;

/// <summary>
/// Everything a <see cref="LinkUnfurl"/> keeps about one link: the public view, the pending fetch, the chip's animated
/// values (width, title roll, favicon pop, accent tint, exit) and the card with its unfurl progress.
/// </summary>
internal sealed class LinkUnfurlEntry : IDisposable
{
    public LinkUnfurlEntry(LinkUnfurl owner, LinkUnfurlLink link)
    {
        Link = link;
        Width = new MotionValue<double>(owner, 0, MotionVectors.Double);
        Reveal = new MotionValue<double>(owner, 1, MotionVectors.Double);
        Pop = new MotionValue<double>(owner, 1, MotionVectors.Double);
        Hot = new MotionValue<double>(owner, 0, MotionVectors.Double);
        Exit = new MotionValue<double>(owner, 1, MotionVectors.Double);
        Progress = new MotionValue<double>(owner, 0, MotionVectors.Double);
    }

    public LinkUnfurlLink Link { get; }

    public string Id => Link.Id;

    /// <summary>The fetch in flight; cancelled when the link is removed or the message is sent.</summary>
    public CancellationTokenSource? Fetch { get; set; }

    /// <summary>The chip's width (padding included, margins excluded); NaN until it was first measured.</summary>
    public double ShownWidth { get; set; } = double.NaN;

    /// <summary>The width the chip is heading for (its label's natural width, at most 224).</summary>
    public double TargetWidth { get; set; } = double.NaN;

    public MotionValue<double> Width { get; }

    /// <summary>The title's roll-in (0: 5px low, blurred and clear; 1: in place).</summary>
    public MotionValue<double> Reveal { get; }

    /// <summary>The favicon's pop (0: at 0.6 and clear; 1: in place).</summary>
    public MotionValue<double> Pop { get; }

    /// <summary>The accent tint that ties an unfurled link to its card (0: neutral, 1: accent).</summary>
    public MotionValue<double> Hot { get; }

    /// <summary>The chip's presence while it leaves after the remove button (1: shown, 0: gone at 0.9 scale).</summary>
    public MotionValue<double> Exit { get; }

    /// <summary>The card's unfurl progress (0: folded into the link, 1: open).</summary>
    public MotionValue<double> Progress { get; }

    /// <summary>The card (made when the preview arrives).</summary>
    public LinkUnfurlCardHost? Card { get; set; }

    /// <summary>The link left the text (deleted or removed): its card folds in place and goes.</summary>
    public bool IsRemoved { get; set; }

    /// <summary>The remove button was pressed: the card folds into the link, then the link leaves.</summary>
    public bool IsLeaving { get; set; }

    /// <summary>The card is being dragged: its progress follows the pointer.</summary>
    public bool IsDragging { get; set; }

    /// <summary>Where a card whose link left the text folds to: a 132 × 26 pill at its own top left.</summary>
    public bool FoldsInPlace { get; set; }

    /// <summary>The label's text and its shaped layout (rebuilt when the text, color or width changes).</summary>
    public string Label { get; set; } = string.Empty;

    public TextLayout? LabelLayout { get; set; }

    public string? LabelLayoutText { get; set; }

    public double LabelLayoutWidth { get; set; } = -1;

    public int LabelLayoutVersion { get; set; } = -1;

    /// <summary>The monogram's shaped letter.</summary>
    public TextLayout? MonogramLayout { get; set; }

    public int MonogramVersion { get; set; } = -1;

    public void ClearLayouts()
    {
        LabelLayout?.Dispose();
        LabelLayout = null;
        LabelLayoutText = null;
        MonogramLayout?.Dispose();
        MonogramLayout = null;
    }

    public void Dispose()
    {
        Fetch?.Cancel();
        Fetch?.Dispose();
        Fetch = null;
        Width.Dispose();
        Reveal.Dispose();
        Pop.Dispose();
        Hot.Dispose();
        Exit.Dispose();
        Progress.Dispose();
        ClearLayouts();
    }
}
