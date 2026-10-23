using System.Text;
using TKCode.Arc.Primitives;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Media;
using Avalonia.Media.Immutable;

namespace TKCode.Arc;

/// <summary>One piece of a user message: plain text, or the content of an inline code span.</summary>
/// <param name="Text">The text shown (for code, without its backticks).</param>
/// <param name="IsCode">True for the content of a code span.</param>
internal readonly record struct AiInlineSegment(string Text, bool IsCode);

/// <summary>
/// Splits a user message into plain text and inline code, the only Markdown a user bubble renders. The rules are
/// CommonMark's code spans: a run of n backticks opens a span that the next run of exactly n backticks closes; a run
/// with no partner is plain text; line endings inside a span become spaces, and one space is stripped from each end
/// of a span that starts and ends with a space and is not all spaces. Outside spans, a backslash before a backtick
/// makes it a literal backtick (the backslash is dropped); every other character, other backslashes included, is kept
/// as typed. Line endings outside spans are normalized to <c>\n</c>.
/// </summary>
internal static class AiInlineCode
{
    /// <summary>The segments of <paramref name="text"/> in order; adjacent plain text is merged, and nothing is empty.</summary>
    public static List<AiInlineSegment> Parse(string? text)
    {
        var segments = new List<AiInlineSegment>();
        if (string.IsNullOrEmpty(text))
        {
            return segments;
        }
        var plain = new StringBuilder();
        var i = 0;
        while (i < text.Length)
        {
            var c = text[i];
            if (c == '\\' && i + 1 < text.Length && text[i + 1] == '`')
            {
                plain.Append('`');
                i += 2;
                continue;
            }
            if (c == '\r')
            {
                plain.Append('\n');
                i += i + 1 < text.Length && text[i + 1] == '\n' ? 2 : 1;
                continue;
            }
            if (c != '`')
            {
                plain.Append(c);
                i++;
                continue;
            }
            var open = RunLength(text, i);
            var close = FindClosing(text, i + open, open);
            if (close < 0)
            {
                // No partner: the whole run is text.
                plain.Append('`', open);
                i += open;
                continue;
            }
            if (plain.Length > 0)
            {
                segments.Add(new AiInlineSegment(plain.ToString(), false));
                plain.Clear();
            }
            segments.Add(new AiInlineSegment(CodeContent(text, i + open, close), true));
            i = close + open;
        }
        if (plain.Length > 0)
        {
            segments.Add(new AiInlineSegment(plain.ToString(), false));
        }
        return segments;
    }

    /// <summary>The text a reader hears: the segments joined, without the backticks.</summary>
    public static string DisplayText(string? text)
    {
        var segments = Parse(text);
        if (segments.Count == 1)
        {
            return segments[0].Text;
        }
        var builder = new StringBuilder();
        foreach (var segment in segments)
        {
            builder.Append(segment.Text);
        }
        return builder.ToString();
    }

    private static int RunLength(string text, int start)
    {
        var end = start;
        while (end < text.Length && text[end] == '`')
        {
            end++;
        }
        return end - start;
    }

    /// <summary>The start of the next run of exactly <paramref name="length"/> backticks at or after <paramref name="from"/>, or -1.</summary>
    private static int FindClosing(string text, int from, int length)
    {
        var i = from;
        while (i < text.Length)
        {
            if (text[i] != '`')
            {
                i++;
                continue;
            }
            var run = RunLength(text, i);
            if (run == length)
            {
                return i;
            }
            i += run;
        }
        return -1;
    }

    private static string CodeContent(string text, int start, int end)
    {
        var builder = new StringBuilder(end - start);
        for (var i = start; i < end; i++)
        {
            var c = text[i];
            if (c == '\r')
            {
                builder.Append(' ');
                if (i + 1 < end && text[i + 1] == '\n')
                {
                    i++;
                }
            }
            else
            {
                builder.Append(c == '\n' ? ' ' : c);
            }
        }
        if (builder.Length >= 2 && builder[0] == ' ' && builder[^1] == ' ')
        {
            var allSpaces = true;
            for (var i = 0; i < builder.Length; i++)
            {
                if (builder[i] != ' ')
                {
                    allSpaces = false;
                    break;
                }
            }
            if (!allSpaces)
            {
                builder.Remove(builder.Length - 1, 1);
                builder.Remove(0, 1);
            }
        }
        return builder.ToString();
    }
}

/// <summary>
/// The text of a user bubble (Arc 16 / 24 in Foreground): plain text as typed, line breaks kept, and inline code as a
/// chip, the code in the monospace font at 0.86em on a rounded fill of Foreground at 7% (radius 6, padding 0.08em
/// 0.36em of the code size). Nothing else of Markdown is rendered.
/// </summary>
/// <remarks>
/// The chips are <see cref="TextRangeDecoration"/>s on a <see cref="DecoratedTextBlock"/>, so they wrap and repeat per
/// line like CSS inline backgrounds and never relayout. The horizontal padding has to take room in the line as it does
/// in CSS, so each code run sits between two empty <see cref="InlineUIContainer"/> spacers 0.36em wide (a spacer counts
/// as one character for the decoration's range; it has no height, so it does not change the line). The fill's height is
/// the code font's ascent and descent plus the vertical padding: the decoration measures the line's text box (the
/// largest ascent and descent of its runs, the body font's on a line with plain text), so the outsets correct it to
/// the code font's box. Selecting text and copying it drops the spacers' placeholder characters.
/// </remarks>
internal sealed class AiUserBubbleText : DecoratedTextBlock
{
    /// <summary>Code size relative to the text (Arc <c>0.86em</c>).</summary>
    internal const double CodeScale = 0.86;

    /// <summary>Horizontal padding of a chip, relative to the code size (Arc <c>0.36em</c>).</summary>
    internal const double CodePaddingX = 0.36;

    /// <summary>Vertical padding of a chip, relative to the code size (Arc <c>0.08em</c>).</summary>
    internal const double CodePaddingY = 0.08;

    /// <summary>Corner radius of a chip.</summary>
    internal const double CodeRadius = 6;

    /// <summary>Share of the text color in a chip's fill (Arc: 7% Foreground in both themes).</summary>
    internal const double CodeFillAlpha = 0.07;

    /// <summary>The placeholder character a spacer stands for in the layout's text.</summary>
    internal const char SpacerChar = '￼';

    /// <summary>The raw message text (the Markdown subset); null shows nothing.</summary>
    public static readonly StyledProperty<string?> SourceProperty =
        AvaloniaProperty.Register<AiUserBubbleText, string?>(nameof(Source));

    /// <summary>The monospace family of inline code (the theme binds <c>MonoFontFamily</c>).</summary>
    public static readonly StyledProperty<FontFamily> CodeFontFamilyProperty =
        AvaloniaProperty.Register<AiUserBubbleText, FontFamily>(nameof(CodeFontFamily), FontFamily.Default);

    private readonly List<TextRangeDecoration> _chips = new();
    private List<AiInlineSegment> _segments = new();
    private IBrush? _chipBrush;
    private string _displayText = string.Empty;

    static AiUserBubbleText()
    {
        // Selectable with the pointer like web text, but not a stop on the Tab path (Copy and Edit are).
        KeyboardNavigation.IsTabStopProperty.OverrideDefaultValue<AiUserBubbleText>(false);
    }

    protected override Type StyleKeyOverride => typeof(AiUserBubbleText);

    /// <inheritdoc cref="SourceProperty"/>
    public string? Source { get => GetValue(SourceProperty); set => SetValue(SourceProperty, value); }

    /// <inheritdoc cref="CodeFontFamilyProperty"/>
    public FontFamily CodeFontFamily { get => GetValue(CodeFontFamilyProperty); set => SetValue(CodeFontFamilyProperty, value); }

    /// <summary>The segments shown (tests).</summary>
    internal IReadOnlyList<AiInlineSegment> Segments => _segments;

    /// <summary>The chip decorations in use, one per code span, in order (tests).</summary>
    internal IReadOnlyList<TextRangeDecoration> Chips => Decorations;

    /// <summary>The text without backticks (what the bubble's accessible name reads).</summary>
    internal string DisplayText => _displayText;

    /// <summary>The fill of the chips: the text color at 7%.</summary>
    internal IBrush? ChipBrush => _chipBrush;

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == SourceProperty)
        {
            _segments = AiInlineCode.Parse(change.GetNewValue<string?>());
            Rebuild();
        }
        else if (change.Property == FontSizeProperty || change.Property == CodeFontFamilyProperty)
        {
            Rebuild();
        }
        else if (change.Property == FontFamilyProperty || change.Property == FontStyleProperty || change.Property == FontWeightProperty)
        {
            UpdateChipOutsets();
            InvalidateDecorations();
        }
        else if (change.Property == ForegroundProperty)
        {
            _chipBrush = change.GetNewValue<IBrush?>() is ISolidColorBrush solid
                ? new ImmutableSolidColorBrush(Color.FromArgb((byte)Math.Round(solid.Color.A * solid.Opacity * CodeFillAlpha),
                    solid.Color.R, solid.Color.G, solid.Color.B))
                : null;
            foreach (var chip in _chips)
            {
                chip.Background = _chipBrush;
            }
            InvalidateDecorations();
        }
    }

    /// <summary>
    /// Builds the inlines from the segments: a run per plain segment, and for code a spacer, a monospace run and a
    /// spacer under one chip decoration. Chip decorations are reused from build to build.
    /// </summary>
    private void Rebuild()
    {
        var inlines = Inlines;
        if (inlines is null)
        {
            Inlines = inlines = new InlineCollection();
        }
        inlines.Clear();
        var builder = new StringBuilder();
        var codeSize = FontSize * CodeScale;
        var spacerWidth = codeSize * CodePaddingX;
        var index = 0;
        var used = 0;
        foreach (var segment in _segments)
        {
            builder.Append(segment.Text);
            if (!segment.IsCode)
            {
                inlines.Add(new Run(segment.Text));
                index += segment.Text.Length;
                continue;
            }
            var start = index;
            inlines.Add(Spacer(spacerWidth));
            inlines.Add(new Run(segment.Text) { FontFamily = CodeFontFamily, FontSize = codeSize });
            inlines.Add(Spacer(spacerWidth));
            index += segment.Text.Length + 2;
            Chip(used++, start, index - start);
        }
        _displayText = builder.ToString();
        var decorations = Decorations;
        if (decorations.Count > used)
        {
            decorations.RemoveRange(used, decorations.Count - used);
        }
        UpdateChipOutsets();
        InvalidateDecorations();
    }

    private static InlineUIContainer Spacer(double width) =>
        new(new Control { Width = width, Height = 0, IsHitTestVisible = false, Focusable = false });

    private void Chip(int slot, int start, int length)
    {
        TextRangeDecoration chip;
        if (slot < _chips.Count)
        {
            chip = _chips[slot];
        }
        else
        {
            chip = new TextRangeDecoration { CornerRadius = CodeRadius, Background = _chipBrush };
            _chips.Add(chip);
        }
        chip.Start = start;
        chip.Length = length;
        if (slot >= Decorations.Count)
        {
            Decorations.Add(chip);
        }
    }

    /// <summary>
    /// Sets the chips' vertical outsets so a fill covers the code font's ascent and descent plus the padding. The
    /// decoration's own box is the line's text box (the larger of the body and code fonts on a mixed line), so the
    /// outset is the difference plus the padding; the horizontal padding is the spacers.
    /// </summary>
    private void UpdateChipOutsets()
    {
        if (_chips.Count == 0)
        {
            return;
        }
        var codeSize = FontSize * CodeScale;
        var (bodyAscent, bodyDescent) = Metrics(new Typeface(FontFamily, FontStyle, FontWeight), FontSize);
        var (codeAscent, codeDescent) = Metrics(new Typeface(CodeFontFamily), codeSize);
        var lineAscent = Math.Max(bodyAscent, codeAscent);
        var lineDescent = Math.Max(bodyDescent, codeDescent);
        var pad = codeSize * CodePaddingY;
        var outset = new Thickness(0, codeAscent + pad - lineAscent, 0, codeDescent + pad - lineDescent);
        foreach (var chip in _chips)
        {
            chip.Outset = outset;
        }
    }

    /// <summary>The ascent (above the baseline, positive) and descent of <paramref name="typeface"/> at <paramref name="size"/>.</summary>
    internal static (double Ascent, double Descent) Metrics(Typeface typeface, double size)
    {
        var metrics = typeface.GlyphTypeface.Metrics;
        var scale = size / Math.Max(1, (double)metrics.DesignEmHeight);
        return (-metrics.Ascent * scale, metrics.Descent * scale);
    }

    /// <summary>
    /// CSS shrink-to-fit: a bubble whose text wraps is as wide as it may be, not as wide as its longest line (Arc's
    /// two-line bubbles are exactly 544). Text that only breaks at its own line breaks keeps its width.
    /// </summary>
    protected override Size MeasureOverride(Size availableSize)
    {
        var size = base.MeasureOverride(availableSize);
        if (double.IsInfinity(availableSize.Width) || !IsSoftWrapped())
        {
            return size;
        }
        return size.WithWidth(Math.Max(size.Width, availableSize.Width));
    }

    /// <summary>True when a line other than the last ends without a line break of the text (the width wrapped it).</summary>
    internal bool IsSoftWrapped()
    {
        var lines = TextLayout.TextLines;
        for (var i = 0; i < lines.Count - 1; i++)
        {
            if (lines[i].NewLineLength == 0)
            {
                return true;
            }
        }
        return false;
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        // Copying a selection that crosses a chip would carry the spacers' placeholder characters: copy the clean text.
        if (!e.Handled && SelectedText is { } selected && selected.IndexOf(SpacerChar) >= 0 &&
            Application.Current?.PlatformSettings?.HotkeyConfiguration is { } hotkeys && Matches(hotkeys.Copy, e))
        {
            e.Handled = true;
            _ = ArcClipboard.CopyTextAsync(this, selected.Replace(SpacerChar.ToString(), string.Empty));
            return;
        }
        base.OnKeyDown(e);
    }

    private static bool Matches(List<KeyGesture> gestures, KeyEventArgs e)
    {
        foreach (var gesture in gestures)
        {
            if (gesture.Matches(e))
            {
                return true;
            }
        }
        return false;
    }
}
