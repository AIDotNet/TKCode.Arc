using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>
/// One row of a <see cref="RankedBarList"/>: a model (or anything ranked) with its mark, the series tone of its bar and its
/// value. The list keys rows by <paramref name="Id"/>, so a replay moves the row of the same id.
/// </summary>
/// <param name="Id">The key that matches a row before and after a change.</param>
/// <param name="Name">The visible name.</param>
/// <param name="Mark">The mark drawn by <see cref="ModelMark"/> (a 24 × 24 <see cref="Geometry"/> or an <see cref="IImage"/>); null leaves the slot empty.</param>
/// <param name="MarkColor">The fill of a geometry mark; null uses the foreground.</param>
/// <param name="Tone">The bar's series color, 1 to 4 (<see cref="PromptTone.SeriesColor"/>); other values use tone 4.</param>
/// <param name="Value">The share the bar fills and the number shows as a whole percentage, 0 to 1.</param>
/// <param name="AutomationName">What a screen reader reads for the row ("Rank 1, Lumen 3 Pro, win rate 70%"); empty builds it from the list's texts.</param>
internal sealed record RankedBarItem(
    string Id,
    string Name,
    object? Mark,
    IBrush? MarkColor,
    int Tone,
    double Value,
    string AutomationName)
{
    /// <summary><see cref="Value"/> clamped to 0..1 (NaN counts as 0).</summary>
    public double Ratio => double.IsNaN(Value) ? 0 : Math.Clamp(Value, 0, 1);

    /// <summary>The whole percentage shown (half up: 0.335 shows 34%).</summary>
    public int Percent => (int)Math.Round(Ratio * 100, MidpointRounding.AwayFromZero);
}
