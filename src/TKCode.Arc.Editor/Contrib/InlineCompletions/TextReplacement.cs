// Ported from vscode (commit b57d5c9e), MIT license:
//  - src/vs/editor/common/core/edits/textEdit.ts: TextReplacement (the members the inline completions use) and
//    TextEdit.applyToString / fromParallelReplacementsUnsorted as ApplyToString;
//  - src/vs/editor/common/core/text/textLength.ts: TextLength.ofText(...).addToPosition as AddTextToPosition;
//  - src/vs/base/common/strings.ts: commonPrefixLength, commonSuffixLength, getLeadingWhitespace, splitLines;
//  - src/vs/editor/contrib/inlineCompletions/browser/model/singleTextEditHelpers.ts.
//
// Differences: TextReplacement lives in the inline completions' namespace (the engine has no core edits module yet);
// the text it reads is a TextModel (upstream's AbstractText); applying replacements to a string works on line/column
// positions directly (upstream goes through a position-to-offset transformer).

using TKCode.Arc.Editor.Model;

namespace TKCode.Arc.Editor.Contrib.InlineSuggestions;

/// <summary>Replaces a range of a model with a text (upstream's TextReplacement).</summary>
internal sealed record TextReplacement(TextRange Range, string Text)
{
    public static TextReplacement Delete(TextRange range) => new(range, "");

    public bool IsEmpty => Range.IsEmpty && Text.Length == 0;

    public TextReplacement RemoveCommonPrefixAndSuffix(TextModel text) => RemoveCommonPrefix(text).RemoveCommonSuffix(text);

    public TextReplacement RemoveCommonPrefix(TextModel text)
    {
        var normalizedOriginalText = text.GetValueInRange(Range).Replace("\r\n", "\n", StringComparison.Ordinal);
        var normalizedModifiedText = Text.Replace("\r\n", "\n", StringComparison.Ordinal);

        var commonPrefixLen = TextHelpers.CommonPrefixLength(normalizedOriginalText, normalizedModifiedText);
        var start = TextHelpers.AddTextToPosition(normalizedOriginalText[..commonPrefixLen], Range.Start);

        var newText = normalizedModifiedText[commonPrefixLen..];
        return new TextReplacement(new TextRange(start, Range.End), newText);
    }

    public TextReplacement RemoveCommonSuffix(TextModel text)
    {
        var normalizedOriginalText = text.GetValueInRange(Range).Replace("\r\n", "\n", StringComparison.Ordinal);
        var normalizedModifiedText = Text.Replace("\r\n", "\n", StringComparison.Ordinal);

        var commonSuffixLen = TextHelpers.CommonSuffixLength(normalizedOriginalText, normalizedModifiedText);
        var end = TextHelpers.AddTextToPosition(normalizedOriginalText[..(normalizedOriginalText.Length - commonSuffixLen)], Range.Start);

        var newText = normalizedModifiedText[..(normalizedModifiedText.Length - commonSuffixLen)];
        return new TextReplacement(new TextRange(Range.Start, end), newText);
    }

    /// <summary>The engine's edit operation for this replacement.</summary>
    public SingleEditOperation ToSingleEditOperation() => new(Range, Text);

    /// <summary>
    /// Upstream's TextEdit.fromParallelReplacementsUnsorted(replacements).applyToString(text): applies replacements that
    /// do not overlap to a text with \n line breaks.
    /// </summary>
    public static string ApplyToString(string text, IEnumerable<TextReplacement> replacements)
    {
        var lineStarts = new List<int> { 0 };
        for (var i = 0; i < text.Length; i++)
        {
            if (text[i] == '\n')
            {
                lineStarts.Add(i + 1);
            }
        }

        int Offset(TextPosition p) => Math.Min(text.Length, lineStarts[Math.Clamp(p.LineNumber - 1, 0, lineStarts.Count - 1)] + p.Column - 1);

        var sorted = replacements.OrderBy(r => r.Range.Start).ToList();
        var result = new System.Text.StringBuilder();
        var last = 0;
        foreach (var r in sorted)
        {
            var start = Offset(r.Range.Start);
            var end = Offset(r.Range.End);
            result.Append(text, last, start - last);
            result.Append(r.Text);
            last = end;
        }

        result.Append(text, last, text.Length - last);
        return result.ToString();
    }
}

/// <summary>String and position helpers of the inline completions (upstream's strings.ts and TextLength).</summary>
internal static class TextHelpers
{
    public static int CommonPrefixLength(string a, string b)
    {
        var len = Math.Min(a.Length, b.Length);
        for (var i = 0; i < len; i++)
        {
            if (a[i] != b[i])
            {
                return i;
            }
        }

        return len;
    }

    public static int CommonSuffixLength(string a, string b)
    {
        var len = Math.Min(a.Length, b.Length);
        var aLastIndex = a.Length - 1;
        var bLastIndex = b.Length - 1;
        for (var i = 0; i < len; i++)
        {
            if (a[aLastIndex - i] != b[bLastIndex - i])
            {
                return i;
            }
        }

        return len;
    }

    public static string GetLeadingWhitespace(string str)
    {
        for (var i = 0; i < str.Length; i++)
        {
            if (str[i] != ' ' && str[i] != '\t')
            {
                return str[..i];
            }
        }

        return str;
    }

    /// <summary>Upstream's splitLines: on \r\n, \r or \n.</summary>
    public static string[] SplitLines(string str) => str.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n').Split('\n');

    /// <summary>Upstream's TextLength.ofText(text).addToPosition(position).</summary>
    public static TextPosition AddTextToPosition(string text, TextPosition position)
    {
        var lineCount = 0;
        var lastLineStart = 0;
        for (var i = 0; i < text.Length; i++)
        {
            if (text[i] == '\n')
            {
                lineCount++;
                lastLineStart = i + 1;
            }
        }

        var lastLineLength = text.Length - lastLineStart;
        return lineCount == 0
            ? new TextPosition(position.LineNumber, position.Column + lastLineLength)
            : new TextPosition(position.LineNumber + lineCount, lastLineLength + 1);
    }
}

/// <summary>Upstream's singleTextEditHelpers.ts.</summary>
internal static class SingleTextEditHelpers
{
    public static TextReplacement SingleTextRemoveCommonPrefix(TextReplacement edit, TextModel model, TextRange? validModelRange = null)
    {
        var modelRange = validModelRange is { } valid ? edit.Range.IntersectRanges(valid) : edit.Range;
        if (modelRange is not { } range)
        {
            return edit;
        }

        var normalizedText = edit.Text.Replace("\r\n", "\n", StringComparison.Ordinal);
        var valueToReplace = model.GetValueInRange(range, EndOfLinePreference.LF);
        var commonPrefixLen = TextHelpers.CommonPrefixLength(valueToReplace, normalizedText);
        var start = TextHelpers.AddTextToPosition(valueToReplace[..commonPrefixLen], edit.Range.Start);
        var text = normalizedText[commonPrefixLen..];
        return new TextReplacement(new TextRange(start, edit.Range.End), text);
    }

    /// <summary>The augmented completion must replace the base range, but can replace even more.</summary>
    public static bool SingleTextEditAugments(TextReplacement edit, TextReplacement @base) =>
        edit.Text.StartsWith(@base.Text, StringComparison.Ordinal) && RangeExtends(edit.Range, @base.Range);

    private static bool RangeExtends(TextRange extendingRange, TextRange rangeToExtend) =>
        rangeToExtend.Start == extendingRange.Start && rangeToExtend.End.IsBeforeOrEqual(extendingRange.End);
}
