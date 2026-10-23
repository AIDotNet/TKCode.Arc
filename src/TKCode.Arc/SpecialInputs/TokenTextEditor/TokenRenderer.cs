using Avalonia;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.TextFormatting;
using Avalonia.Utilities;

namespace TKCode.Arc;

/// <summary>
/// The box a <see cref="TokenRenderer"/> gives a token on its line: <see cref="Width"/> wide, reaching
/// <see cref="Ascent"/> above the baseline and <see cref="Descent"/> below it. Keep both within the text's own
/// (<see cref="TokenRenderContext.Ascent"/>, <see cref="TokenRenderContext.Descent"/>) so lines with and without
/// tokens are equally tall; the renderer may paint outside the box.
/// </summary>
/// <param name="Width">The advance the token takes on its line.</param>
/// <param name="Ascent">Height above the baseline.</param>
/// <param name="Descent">Depth below the baseline.</param>
public readonly record struct TokenInlineSize(double Width, double Ascent, double Descent);

/// <summary>The editor and the text metrics a <see cref="TokenRenderer"/> measures and paints with.</summary>
/// <param name="Editor">The editor the token belongs to.</param>
/// <param name="Typeface">The editor's typeface.</param>
/// <param name="FontSize">The editor's font size.</param>
/// <param name="LetterSpacing">The editor's letter spacing.</param>
/// <param name="Foreground">The editor's text color.</param>
/// <param name="Ascent">The font's ascent at <paramref name="FontSize"/> (positive).</param>
/// <param name="Descent">The font's descent at <paramref name="FontSize"/> (positive).</param>
public readonly record struct TokenRenderContext(TokenTextEditor Editor, Typeface Typeface, double FontSize, double LetterSpacing,
                                                 IBrush? Foreground, double Ascent, double Descent);

/// <summary>
/// Draws tokens of a <see cref="TokenTextEditor"/> as single inline objects (chips) instead of their characters. A token
/// the renderer measures (<see cref="Measure"/> returns a size) takes that width on its line and none of its characters
/// are shown: the caret steps over it in one move, Backspace and Delete remove it whole, a click lands on its nearer
/// edge, a line wraps before it rather than through it, and <see cref="Render"/> paints it behind the editor's
/// transparent text. The text itself, and so copy, undo and the <see cref="TokenTextEditor.Text"/> value, keeps the
/// token's characters.
/// </summary>
/// <remarks>
/// <see cref="Measure"/> runs on every layout of the text (each keystroke), so cache what it computes.
/// <see cref="Render"/> runs on every paint: call <see cref="Invalidate"/> when the look changes (a state, an animation
/// frame) and <see cref="InvalidateMeasure"/> when sizes change.
/// </remarks>
public abstract class TokenRenderer
{
    /// <summary>
    /// The box of <paramref name="token"/>, or null to leave it as text (painted with its <see cref="TokenStyle"/>).
    /// </summary>
    public abstract TokenInlineSize? Measure(TokenSpan token, in TokenRenderContext context);

    /// <summary>
    /// Paints <paramref name="token"/>: <paramref name="bounds"/> is its measured box (in the coordinates of the
    /// editor's text, scrolling included) and <paramref name="baseline"/> the y of the line's baseline.
    /// </summary>
    public abstract void Render(DrawingContext drawingContext, TokenSpan token, Rect bounds, double baseline, in TokenRenderContext context);

    /// <summary>The pointer shape over a token this renderer draws (null keeps the text cursor).</summary>
    public Cursor? Cursor { get; set; }

    /// <summary>Raised by <see cref="Invalidate"/>: the editor paints its tokens again.</summary>
    public event EventHandler? Invalidated;

    /// <summary>Raised by <see cref="InvalidateMeasure"/>: the editor lays its text out again.</summary>
    public event EventHandler? MeasureInvalidated;

    /// <summary>Asks the editors using this renderer to paint their tokens again.</summary>
    protected void Invalidate() => Invalidated?.Invoke(this, EventArgs.Empty);

    /// <summary>Asks the editors using this renderer to measure their tokens and lay their text out again.</summary>
    protected void InvalidateMeasure() => MeasureInvalidated?.Invoke(this, EventArgs.Empty);
}

/// <summary>
/// A token laid out as one unit: <see cref="TextRun.Length"/> covers all of its characters, so the text formatter, hit
/// testing and caret navigation treat it atomically. The editor's own (transparent) layout draws nothing; the mirror's
/// layout asks the renderer to paint it.
/// </summary>
internal sealed class TokenInlineRun : DrawableTextRun
{
    private readonly int _length;
    private readonly TokenInlineSize _size;
    private readonly TokenTextMirror? _painter;

    public TokenInlineRun(TokenSpan token, int length, TokenInlineSize size, TextRunProperties properties, TokenTextMirror? painter)
    {
        Token = token;
        _length = length;
        _size = size;
        Properties = properties;
        _painter = painter;
    }

    /// <summary>The token this run stands for (its offsets in the editor's text, without an IME preedit).</summary>
    public TokenSpan Token { get; }

    public override int Length => _length;

    // A run needs properties: the line reads their baseline alignment.
    public override TextRunProperties Properties { get; }

    public override Size Size => new(_size.Width, _size.Ascent + _size.Descent);

    public override double Baseline => _size.Ascent;

    public override void Draw(DrawingContext drawingContext, Point origin) =>
        _painter?.DrawInlineToken(drawingContext, Token, new Rect(origin, Size), origin.Y + _size.Ascent);
}

/// <summary>
/// The text of a <see cref="TokenTextPresenter"/> with inline tokens: plain runs (with the per-token and preedit
/// properties of the ordinary layout) and one <see cref="TokenInlineRun"/> per inline token. Slices runs for any index
/// the formatter asks for (lines start mid-run after a wrap or a line break).
/// </summary>
internal sealed class TokenInlineTextSource : ITextSource
{
    private readonly string _text;
    private readonly List<(int Start, int Length, TextRunProperties? Properties, TokenInlineRun? Inline)> _segments;

    public TokenInlineTextSource(string text, TextRunProperties defaults, IReadOnlyList<ValueSpan<TextRunProperties>> spans,
                                 IReadOnlyList<(int Start, TokenInlineRun Run)> inlines)
    {
        _text = text;
        _segments = new List<(int, int, TextRunProperties?, TokenInlineRun?)>(spans.Count + inlines.Count * 2 + 1);
        var at = 0;
        var span = 0;
        foreach (var (start, run) in inlines)
        {
            AddText(at, start, defaults, spans, ref span);
            _segments.Add((start, run.Length, null, run));
            at = start + run.Length;
        }
        AddText(at, text.Length, defaults, spans, ref span);
    }

    /// <summary>Adds <paramref name="from"/>..<paramref name="to"/> as plain segments, split where the styled spans begin and end.</summary>
    private void AddText(int from, int to, TextRunProperties defaults, IReadOnlyList<ValueSpan<TextRunProperties>> spans, ref int span)
    {
        var at = from;
        while (at < to)
        {
            while (span < spans.Count && spans[span].Start + spans[span].Length <= at)
            {
                span++;
            }
            if (span < spans.Count && spans[span].Start <= at)
            {
                var end = Math.Min(to, spans[span].Start + spans[span].Length);
                _segments.Add((at, end - at, spans[span].Value, null));
                at = end;
            }
            else
            {
                var end = span < spans.Count ? Math.Min(to, spans[span].Start) : to;
                _segments.Add((at, end - at, defaults, null));
                at = end;
            }
        }
    }

    public TextRun? GetTextRun(int textSourceIndex)
    {
        if (textSourceIndex >= _text.Length)
        {
            return null;
        }
        // Few segments (a prompt holds tens of tokens): a linear scan is enough.
        foreach (var (start, length, properties, inline) in _segments)
        {
            if (textSourceIndex >= start + length)
            {
                continue;
            }
            if (inline is not null)
            {
                // The formatter only starts inside a token when a line begins there, which is its start.
                return inline;
            }
            return new TextCharacters(_text.AsMemory(textSourceIndex, start + length - textSourceIndex), properties!);
        }
        return null;
    }
}
