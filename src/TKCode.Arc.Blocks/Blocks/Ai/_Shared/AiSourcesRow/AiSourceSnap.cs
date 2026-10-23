namespace TKCode.Arc;

/// <summary>
/// The scroll arithmetic of the narrow sources row, kept pure so it can be tested without a window. Arc's row is a CSS
/// <c>scroll-snap-type: x mandatory</c> list whose cards snap their start to the scroll padding; Avalonia has no scroll
/// snapping, so the row springs to <see cref="Target"/> once a scroll comes to rest, and to <see cref="Reveal"/> when the
/// owner lights a card that is out of view.
/// </summary>
internal static class AiSourceSnap
{
    /// <summary>
    /// The resting offset after a scroll: the snap position nearest to <paramref name="offset"/>. A card's snap position is
    /// its start less the scroll padding (<paramref name="cardStarts"/> are content coordinates); positions past the end
    /// are clamped to <paramref name="maxOffset"/>, which is itself a snap position (the end of the row can always be
    /// reached, as in a browser), and so is the start. Ties go to the lower offset.
    /// </summary>
    public static double Target(double offset, double maxOffset, ReadOnlySpan<double> cardStarts, double scrollPadding)
    {
        var max = Math.Max(0, maxOffset);
        var best = Math.Clamp(offset, 0, max);
        var bestDistance = double.PositiveInfinity;
        Consider(0);
        Consider(max);
        foreach (var start in cardStarts)
        {
            Consider(Math.Clamp(start - scrollPadding, 0, max));
        }
        return best;

        void Consider(double candidate)
        {
            var distance = Math.Abs(candidate - offset);
            if (distance < bestDistance - 0.001 || (Math.Abs(distance - bestDistance) <= 0.001 && candidate < best))
            {
                best = candidate;
                bestDistance = distance;
            }
        }
    }

    /// <summary>
    /// The offset that shows the card spanning [<paramref name="cardStart"/>, <paramref name="cardEnd"/>) (content
    /// coordinates) inside a viewport of <paramref name="viewport"/> at <paramref name="offset"/>: unchanged when the card
    /// already lies whole between the scroll paddings, otherwise its start aligned to the left padding (the snap position,
    /// as Arc's smooth scroll ends), clamped to [0, <paramref name="maxOffset"/>].
    /// </summary>
    public static double Reveal(double offset, double viewport, double maxOffset, double cardStart, double cardEnd, double scrollPadding)
    {
        var max = Math.Max(0, maxOffset);
        var left = offset + scrollPadding;
        var right = offset + viewport - scrollPadding;
        if (cardStart >= left - 0.5 && cardEnd <= right + 0.5)
        {
            return Math.Clamp(offset, 0, max);
        }
        return Math.Clamp(cardStart - scrollPadding, 0, max);
    }

    /// <summary>A narrow row's card width: Arc's <c>min(248px, 76%)</c> of the list's content width.</summary>
    public static double CardWidth(double contentWidth) => Math.Max(0, Math.Min(MaxCardWidth, contentWidth * CardShare));

    /// <summary>The widest a card of the narrow row gets.</summary>
    public const double MaxCardWidth = 248;

    /// <summary>A card of the narrow row takes this share of the list's width when that is less.</summary>
    public const double CardShare = 0.76;
}
