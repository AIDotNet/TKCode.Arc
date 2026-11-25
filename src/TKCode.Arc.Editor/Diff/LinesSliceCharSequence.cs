// Ported from vscode (src/vs/editor/common/diff/defaultLinesDiffComputer/linesSliceCharSequence.ts, commit b57d5c9e),
// MIT license.
// Differences: the elements are a List<int> of UTF-16 code units; trimming follows JavaScript's whitespace.

using TKCode.Arc.Editor.Diff.Algorithms;

namespace TKCode.Arc.Editor.Diff;

/// <summary>A range of lines as a sequence of characters (\n between lines), optionally without leading and trailing whitespace.</summary>
internal sealed class LinesSliceCharSequence : ISequence
{
    private readonly List<int> _elements = [];
    private readonly List<int> _firstElementOffsetByLineIdx = [];
    private readonly List<int> _lineStartOffsets = [];
    private readonly List<int> _trimmedWsLengthsByLineIdx = [];
    private readonly TextRange _range;

    public LinesSliceCharSequence(IReadOnlyList<string> lines, TextRange range, bool considerWhitespaceChanges)
    {
        Lines = lines;
        _range = range;
        ConsiderWhitespaceChanges = considerWhitespaceChanges;

        _firstElementOffsetByLineIdx.Add(0);
        for (var lineNumber = range.StartLineNumber; lineNumber <= range.EndLineNumber; lineNumber++)
        {
            var line = lines[lineNumber - 1];
            var lineStartOffset = 0;
            if (lineNumber == range.StartLineNumber && range.StartColumn > 1)
            {
                lineStartOffset = range.StartColumn - 1;
                line = lineStartOffset >= line.Length ? string.Empty : line.Substring(lineStartOffset);
            }

            _lineStartOffsets.Add(lineStartOffset);

            var trimmedWsLength = 0;
            if (!considerWhitespaceChanges)
            {
                var trimmedStartLine = DiffUtils.JsTrimStart(line);
                trimmedWsLength = line.Length - trimmedStartLine.Length;
                line = DiffUtils.JsTrimEnd(trimmedStartLine);
            }

            _trimmedWsLengthsByLineIdx.Add(trimmedWsLength);

            var lineLength = lineNumber == range.EndLineNumber
                ? Math.Min((int)Math.Min((long)range.EndColumn - 1 - lineStartOffset - trimmedWsLength, int.MaxValue), line.Length)
                : line.Length;
            for (var i = 0; i < lineLength; i++)
            {
                _elements.Add(line[i]);
            }

            if (lineNumber < range.EndLineNumber)
            {
                _elements.Add('\n');
                _firstElementOffsetByLineIdx.Add(_elements.Count);
            }
        }
    }

    public IReadOnlyList<string> Lines { get; }

    public bool ConsiderWhitespaceChanges { get; }

    public override string ToString() => $"Slice: \"{Text}\"";

    public string Text => GetText(new OffsetRange(0, Length));

    public string GetText(OffsetRange range)
    {
        var chars = new char[range.Length];
        for (var i = 0; i < chars.Length; i++)
        {
            chars[i] = (char)_elements[range.Start + i];
        }

        return new string(chars);
    }

    public int GetElement(int offset) => _elements[offset];

    public int Length => _elements.Count;

    public bool HasBoundaryScore => true;

    public int GetBoundaryScore(int length)
    {
        //   a   b   c   ,           d   e   f
        // 11  0   0   12  15  6   13  0   0   11

        var prevCategory = GetCategory(length > 0 ? _elements[length - 1] : -1);
        var nextCategory = GetCategory(length < _elements.Count ? _elements[length] : -1);

        if (prevCategory == CharBoundaryCategory.LineBreakCR && nextCategory == CharBoundaryCategory.LineBreakLF)
        {
            // don't break between \r and \n
            return 0;
        }

        if (prevCategory == CharBoundaryCategory.LineBreakLF)
        {
            // prefer the linebreak before the change
            return 150;
        }

        var score = 0;
        if (prevCategory != nextCategory)
        {
            score += 10;
            if (prevCategory == CharBoundaryCategory.WordLower && nextCategory == CharBoundaryCategory.WordUpper)
            {
                score += 1;
            }
        }

        score += GetCategoryBoundaryScore(prevCategory);
        score += GetCategoryBoundaryScore(nextCategory);
        return score;
    }

    public TextPosition TranslateOffset(int offset, bool preferLeft = false)
    {
        // find smallest i, so that lineBreakOffsets[i] <= offset using binary search
        var i = DiffArrays.FindLastIdxMonotonous(_firstElementOffsetByLineIdx, value => value <= offset);
        var lineOffset = offset - _firstElementOffsetByLineIdx[i];
        return new TextPosition(
            _range.StartLineNumber + i,
            1 + _lineStartOffsets[i] + lineOffset + (lineOffset == 0 && preferLeft ? 0 : _trimmedWsLengthsByLineIdx[i]));
    }

    public TextRange TranslateRange(OffsetRange range)
    {
        var pos1 = TranslateOffset(range.Start, preferLeft: false);
        var pos2 = TranslateOffset(range.EndExclusive, preferLeft: true);
        if (pos2.IsBefore(pos1))
        {
            return new TextRange(pos2, pos2);
        }

        return new TextRange(pos1, pos2);
    }

    /// <summary>Finds the word that contains the character at the given offset.</summary>
    public OffsetRange? FindWordContaining(int offset)
    {
        if (offset < 0 || offset >= _elements.Count)
        {
            return null;
        }

        if (!IsWordChar(_elements[offset]))
        {
            return null;
        }

        var start = offset;
        while (start > 0 && IsWordChar(_elements[start - 1]))
        {
            start--;
        }

        var end = offset;
        while (end < _elements.Count && IsWordChar(_elements[end]))
        {
            end++;
        }

        return new OffsetRange(start, end);
    }

    /// <summary>fooBar has the two sub-words foo and Bar.</summary>
    public OffsetRange? FindSubWordContaining(int offset)
    {
        if (offset < 0 || offset >= _elements.Count)
        {
            return null;
        }

        if (!IsWordChar(_elements[offset]))
        {
            return null;
        }

        var start = offset;
        while (start > 0 && IsWordChar(_elements[start - 1]) && !IsUpperCase(_elements[start]))
        {
            start--;
        }

        var end = offset;
        while (end < _elements.Count && IsWordChar(_elements[end]) && !IsUpperCase(_elements[end]))
        {
            end++;
        }

        return new OffsetRange(start, end);
    }

    public int CountLinesIn(OffsetRange range) => TranslateOffset(range.EndExclusive).LineNumber - TranslateOffset(range.Start).LineNumber;

    public bool IsStronglyEqual(int offset1, int offset2) => _elements[offset1] == _elements[offset2];

    public OffsetRange ExtendToFullLines(OffsetRange range)
    {
        var startIdx = DiffArrays.FindLastIdxMonotonous(_firstElementOffsetByLineIdx, x => x <= range.Start);
        var start = startIdx == -1 ? 0 : _firstElementOffsetByLineIdx[startIdx];
        var endIdx = DiffArrays.FindFirstIdxMonotonousOrArrLen(_firstElementOffsetByLineIdx, x => range.EndExclusive <= x);
        var end = endIdx == _firstElementOffsetByLineIdx.Count ? _elements.Count : _firstElementOffsetByLineIdx[endIdx];
        return new OffsetRange(start, end);
    }

    private static bool IsWordChar(int charCode) =>
        charCode is >= 'a' and <= 'z' or >= 'A' and <= 'Z' or >= '0' and <= '9';

    private static bool IsUpperCase(int charCode) => charCode is >= 'A' and <= 'Z';

    private enum CharBoundaryCategory
    {
        WordLower,
        WordUpper,
        WordNumber,
        End,
        Other,
        Separator,
        Space,
        LineBreakCR,
        LineBreakLF,
    }

    private static int GetCategoryBoundaryScore(CharBoundaryCategory category) => category switch
    {
        CharBoundaryCategory.WordLower => 0,
        CharBoundaryCategory.WordUpper => 0,
        CharBoundaryCategory.WordNumber => 0,
        CharBoundaryCategory.End => 10,
        CharBoundaryCategory.Other => 2,
        CharBoundaryCategory.Separator => 30,
        CharBoundaryCategory.Space => 3,
        CharBoundaryCategory.LineBreakCR => 10,
        CharBoundaryCategory.LineBreakLF => 10,
        _ => 0,
    };

    private static CharBoundaryCategory GetCategory(int charCode)
    {
        if (charCode == '\n')
        {
            return CharBoundaryCategory.LineBreakLF;
        }

        if (charCode == '\r')
        {
            return CharBoundaryCategory.LineBreakCR;
        }

        if (DiffUtils.IsSpace(charCode))
        {
            return CharBoundaryCategory.Space;
        }

        if (charCode is >= 'a' and <= 'z')
        {
            return CharBoundaryCategory.WordLower;
        }

        if (charCode is >= 'A' and <= 'Z')
        {
            return CharBoundaryCategory.WordUpper;
        }

        if (charCode is >= '0' and <= '9')
        {
            return CharBoundaryCategory.WordNumber;
        }

        if (charCode == -1)
        {
            return CharBoundaryCategory.End;
        }

        if (charCode is ',' or ';')
        {
            return CharBoundaryCategory.Separator;
        }

        return CharBoundaryCategory.Other;
    }
}
