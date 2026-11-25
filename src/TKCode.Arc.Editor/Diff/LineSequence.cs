// Ported from vscode (src/vs/editor/common/diff/defaultLinesDiffComputer/lineSequence.ts and the isSpace /
// LineRangeFragment of defaultLinesDiffComputer/utils.ts, commit b57d5c9e), MIT license.
// Differences: LineRangeFragment counts characters per instance in a dictionary keyed by the character instead of
// upstream's static map from characters to histogram slots (shared mutable state); the similarity is the same.

using TKCode.Arc.Editor.Diff.Algorithms;

namespace TKCode.Arc.Editor.Diff;

/// <summary>Lines as a sequence of hashes of their trimmed text.</summary>
internal sealed class LineSequence(int[] trimmedHash, IReadOnlyList<string> lines) : ISequence
{
    public int GetElement(int offset) => trimmedHash[offset];

    public int Length => trimmedHash.Length;

    public bool HasBoundaryScore => true;

    public int GetBoundaryScore(int length)
    {
        var indentationBefore = length == 0 ? 0 : GetIndentation(lines[length - 1]);
        var indentationAfter = length == lines.Count ? 0 : GetIndentation(lines[length]);
        return 1000 - (indentationBefore + indentationAfter);
    }

    public string GetText(OffsetRange range)
    {
        var parts = new string[range.Length];
        for (var i = 0; i < parts.Length; i++)
        {
            parts[i] = lines[range.Start + i];
        }

        return string.Join('\n', parts);
    }

    public bool IsStronglyEqual(int offset1, int offset2) => string.Equals(lines[offset1], lines[offset2], StringComparison.Ordinal);

    private static int GetIndentation(string str)
    {
        var i = 0;
        while (i < str.Length && (str[i] == ' ' || str[i] == '\t'))
        {
            i++;
        }

        return i;
    }
}

/// <summary>The diff's helpers of utils.ts.</summary>
internal static class DiffUtils
{
    public static bool IsSpace(int charCode) => charCode == ' ' || charCode == '\t';

    /// <summary>JavaScript's whitespace (String.prototype.trim, \s): .NET's plus the byte order mark.</summary>
    public static bool IsJsWhitespace(char c) => char.IsWhiteSpace(c) || c == '﻿';

    /// <summary>JavaScript's String.prototype.trim.</summary>
    public static string JsTrim(string s)
    {
        var start = 0;
        var end = s.Length;
        while (start < end && IsJsWhitespace(s[start]))
        {
            start++;
        }

        while (end > start && IsJsWhitespace(s[end - 1]))
        {
            end--;
        }

        return start == 0 && end == s.Length ? s : s.Substring(start, end - start);
    }

    /// <summary>JavaScript's String.prototype.trimStart.</summary>
    public static string JsTrimStart(string s)
    {
        var start = 0;
        while (start < s.Length && IsJsWhitespace(s[start]))
        {
            start++;
        }

        return start == 0 ? s : s.Substring(start);
    }

    /// <summary>JavaScript's String.prototype.trimEnd.</summary>
    public static string JsTrimEnd(string s)
    {
        var end = s.Length;
        while (end > 0 && IsJsWhitespace(s[end - 1]))
        {
            end--;
        }

        return end == s.Length ? s : s.Substring(0, end);
    }
}

/// <summary>The lines of a change with a histogram of their characters, to compare changes for move detection.</summary>
internal sealed class LineRangeFragment
{
    private readonly int _totalCount;
    private readonly Dictionary<char, int> _histogram = [];

    public LineRangeFragment(LineRange range, IReadOnlyList<string> lines, DetailedLineRangeMapping source)
    {
        Range = range;
        Lines = lines;
        Source = source;
        var counter = 0;
        for (var i = range.StartLineNumber - 1; i < range.EndLineNumberExclusive - 1; i++)
        {
            var line = lines[i];
            foreach (var chr in line)
            {
                counter++;
                _histogram[chr] = _histogram.GetValueOrDefault(chr) + 1;
            }

            counter++;
            _histogram['\n'] = _histogram.GetValueOrDefault('\n') + 1;
        }

        _totalCount = counter;
    }

    public LineRange Range { get; }

    public IReadOnlyList<string> Lines { get; }

    public DetailedLineRangeMapping Source { get; }

    public double ComputeSimilarity(LineRangeFragment other)
    {
        var sumDifferences = 0;
        foreach (var (chr, count) in _histogram)
        {
            sumDifferences += Math.Abs(count - other._histogram.GetValueOrDefault(chr));
        }

        foreach (var (chr, count) in other._histogram)
        {
            if (!_histogram.ContainsKey(chr))
            {
                sumDifferences += count;
            }
        }

        return 1 - (sumDifferences / (double)(_totalCount + other._totalCount));
    }
}
