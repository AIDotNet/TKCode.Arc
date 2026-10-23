namespace TKCode.Arc.Primitives;

/// <summary>How tall a <see cref="TextRangeDecoration"/> background is on each line, before its outset.</summary>
public enum TextRangeExtent
{
    /// <summary>The whole line box (line height and line spacing included), like a selection highlight.</summary>
    Line,

    /// <summary>
    /// The font's text box: from the line's ascent above the baseline to its descent below it (the CSS inline
    /// background). Stays the same height when <c>LineHeight</c> grows.
    /// </summary>
    Text
}
