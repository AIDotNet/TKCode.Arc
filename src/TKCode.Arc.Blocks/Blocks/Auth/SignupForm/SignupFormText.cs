// Ported from Arc's free signup-form block (MIT, Copyright (c) 2026 Elia Kuratli): text-wrap balance / pretty of signup-form.module.css.
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;

namespace TKCode.Arc;

/// <summary>How a <see cref="SignupFormText"/> breaks its lines.</summary>
internal enum SignupFormWrap
{
    /// <summary>Greedy wrapping.</summary>
    Normal,

    /// <summary>CSS <c>text-wrap: balance</c>: the narrowest width that keeps the same number of lines (headings).</summary>
    Balance,

    /// <summary>CSS <c>text-wrap: pretty</c>: never a single word alone on the last line (body copy).</summary>
    Pretty
}

/// <summary>
/// A wrapping text block with the browser's <c>text-wrap: balance</c> and <c>pretty</c> line breaking. It narrows its own
/// width to get the better breaks, so it sits at the start of its slot (<see cref="Layoutable.HorizontalAlignment"/> Left).
/// </summary>
internal sealed class SignupFormText : TextBlock
{
    public static readonly StyledProperty<SignupFormWrap> WrapProperty =
        AvaloniaProperty.Register<SignupFormText, SignupFormWrap>(nameof(Wrap));

    static SignupFormText()
    {
        AffectsMeasure<SignupFormText>(WrapProperty);
        TextWrappingProperty.OverrideDefaultValue<SignupFormText>(Avalonia.Media.TextWrapping.Wrap);
        HorizontalAlignmentProperty.OverrideDefaultValue<SignupFormText>(HorizontalAlignment.Left);
    }

    protected override Type StyleKeyOverride => typeof(TextBlock);

    /// <inheritdoc cref="WrapProperty"/>
    public SignupFormWrap Wrap { get => GetValue(WrapProperty); set => SetValue(WrapProperty, value); }

    protected override Size MeasureOverride(Size availableSize)
    {
        var full = availableSize.Width;
        var size = base.MeasureOverride(availableSize);
        if (Wrap == SignupFormWrap.Normal || double.IsInfinity(full) || string.IsNullOrEmpty(Text))
        {
            return size;
        }
        var lines = TextLayout.TextLines.Count;
        if (lines < 2)
        {
            return size;
        }
        if (Wrap == SignupFormWrap.Pretty)
        {
            if (LastLineWords() >= 2)
            {
                return size;
            }
            // Pretty: just narrower than the line before the last, so its last word joins the orphan.
            var before = TextLayout.TextLines[^2];
            var narrower = Math.Floor(before.Width) - 1;
            var pretty = base.MeasureOverride(availableSize.WithWidth(narrower));
            if (TextLayout.TextLines.Count == lines && LastLineWords() >= 2)
            {
                return pretty;
            }
            return base.MeasureOverride(availableSize);
        }
        // Balance: the narrowest width that keeps the same number of lines.
        var low = full / lines;
        var high = full;
        for (var i = 0; i < 8 && high - low > 1; i++)
        {
            var mid = (low + high) / 2;
            base.MeasureOverride(availableSize.WithWidth(mid));
            if (TextLayout.TextLines.Count > lines)
            {
                low = mid;
            }
            else
            {
                high = mid;
            }
        }
        // A pixel of slack so layout rounding never re-wraps the balanced lines.
        var width = Math.Min(full, Math.Ceiling(high) + 1);
        size = base.MeasureOverride(availableSize.WithWidth(width));
        if (TextLayout.TextLines.Count != lines)
        {
            size = base.MeasureOverride(availableSize);
        }
        return size;
    }

    private int LastLineWords()
    {
        var text = Text ?? string.Empty;
        var line = TextLayout.TextLines[^1];
        var start = Math.Clamp(line.FirstTextSourceIndex, 0, text.Length);
        var length = Math.Clamp(line.Length, 0, text.Length - start);
        var words = 0;
        var inWord = false;
        for (var i = start; i < start + length; i++)
        {
            var space = char.IsWhiteSpace(text[i]);
            if (!space && !inWord)
            {
                words++;
            }
            inWord = !space;
        }
        return words;
    }
}
