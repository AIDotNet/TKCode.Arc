// Ported from vscode (src/vs/editor/contrib/find/browser/replacePattern.ts, commit b57d5c9e), MIT license.
// Differences:
//  - the matches a pattern is built from are a FindMatch's Matches (the full match, then the groups, null for a group
//    that did not participate), never JavaScript's replace-callback arguments (which add the offset and the input);
//  - ReplacePattern and ReplacePiece compare by value (upstream's tests use deepStrictEqual);
//  - case operations are chars, not one-character strings; upper and lower casing is invariant.

using System.Text;

namespace TKCode.Arc.Editor.Contrib.Find;

/// <summary>A parsed replace string (upstream's ReplacePattern): a static value, or pieces with match references.</summary>
internal sealed class ReplacePattern : IEquatable<ReplacePattern>
{
    // upstream's StaticValueReplacePattern / DynamicPiecesReplacePattern
    private readonly string? _staticValue;
    private readonly ReplacePiece[]? _pieces;

    public ReplacePattern(IReadOnlyList<ReplacePiece>? pieces)
    {
        if (pieces is null || pieces.Count == 0)
        {
            _staticValue = string.Empty;
        }
        else if (pieces.Count == 1 && pieces[0].StaticValue is { } staticValue)
        {
            _staticValue = staticValue;
        }
        else
        {
            _pieces = pieces.ToArray();
        }
    }

    public static ReplacePattern FromStaticValue(string value) => new([ReplacePiece.FromStaticValue(value)]);

    /// <summary>Whether the pattern refers to the match or its groups.</summary>
    public bool HasReplacementPatterns => _pieces is not null;

    /// <summary>The replacement for a match; <paramref name="matches"/> are the match and its groups (null for none).</summary>
    public string BuildReplaceString(IReadOnlyList<string?>? matches, bool preserveCase = false)
    {
        if (_pieces is null)
        {
            if (preserveCase)
            {
                return Search.BuildReplaceStringWithCasePreserved(matches, _staticValue!);
            }

            return _staticValue!;
        }

        var result = new StringBuilder();
        foreach (var piece in _pieces)
        {
            if (piece.StaticValue is { } staticValue)
            {
                // static value ReplacePiece
                result.Append(staticValue);
                continue;
            }

            // match index ReplacePiece
            var match = Substitute(piece.MatchIndex, matches);
            if (piece.CaseOps is { Count: > 0 } caseOps)
            {
                var repl = new StringBuilder();
                var lenOps = caseOps.Count;
                var opIdx = 0;
                for (var idx = 0; idx < match.Length; idx++)
                {
                    if (opIdx >= lenOps)
                    {
                        repl.Append(match, idx, match.Length - idx);
                        break;
                    }

                    switch (caseOps[opIdx])
                    {
                        case 'U':
                            repl.Append(char.ToUpperInvariant(match[idx]));
                            break;
                        case 'u':
                            repl.Append(char.ToUpperInvariant(match[idx]));
                            opIdx++;
                            break;
                        case 'L':
                            repl.Append(char.ToLowerInvariant(match[idx]));
                            break;
                        case 'l':
                            repl.Append(char.ToLowerInvariant(match[idx]));
                            opIdx++;
                            break;
                        default:
                            repl.Append(match[idx]);
                            break;
                    }
                }

                match = repl.ToString();
            }

            result.Append(match);
        }

        return result.ToString();
    }

    private static string Substitute(int matchIndex, IReadOnlyList<string?>? matches)
    {
        if (matches is null)
        {
            return string.Empty;
        }
        if (matchIndex == 0)
        {
            return matches[0] ?? string.Empty;
        }

        var remainder = string.Empty;
        while (matchIndex > 0)
        {
            if (matchIndex < matches.Count)
            {
                // A match can be undefined
                var match = matches[matchIndex] ?? string.Empty;
                return match + remainder;
            }

            remainder = (matchIndex % 10).ToString(System.Globalization.CultureInfo.InvariantCulture) + remainder;
            matchIndex /= 10;
        }

        return "$" + remainder;
    }

    public bool Equals(ReplacePattern? other)
    {
        if (other is null)
        {
            return false;
        }
        if (_pieces is null || other._pieces is null)
        {
            return _pieces is null && other._pieces is null && string.Equals(_staticValue, other._staticValue, StringComparison.Ordinal);
        }

        return _pieces.AsSpan().SequenceEqual(other._pieces);
    }

    public override bool Equals(object? obj) => obj is ReplacePattern other && Equals(other);

    public override int GetHashCode() => _pieces is null ? (_staticValue ?? string.Empty).GetHashCode(StringComparison.Ordinal) : _pieces.Length;

    public override string ToString() => _pieces is null ? $"static '{_staticValue}'" : string.Join(", ", _pieces.Select(p => p.ToString()));
}

/// <summary>A replace piece: a static string, or a reference to a match with optional case operations (upstream's ReplacePiece).</summary>
internal sealed class ReplacePiece : IEquatable<ReplacePiece>
{
    private ReplacePiece(string? staticValue, int matchIndex, IReadOnlyList<char>? caseOps)
    {
        StaticValue = staticValue;
        MatchIndex = matchIndex;
        CaseOps = caseOps is null || caseOps.Count == 0 ? null : caseOps.ToArray();
    }

    public string? StaticValue { get; }

    public int MatchIndex { get; }

    public IReadOnlyList<char>? CaseOps { get; }

    public static ReplacePiece FromStaticValue(string value) => new(value, -1, null);

    public static ReplacePiece FromMatchIndex(int index) => new(null, index, null);

    public static ReplacePiece FromCaseOps(int index, IReadOnlyList<char> caseOps) => new(null, index, caseOps);

    public bool Equals(ReplacePiece? other) =>
        other is not null
        && string.Equals(StaticValue, other.StaticValue, StringComparison.Ordinal)
        && MatchIndex == other.MatchIndex
        && (CaseOps ?? []).SequenceEqual(other.CaseOps ?? []);

    public override bool Equals(object? obj) => obj is ReplacePiece other && Equals(other);

    public override int GetHashCode() => HashCode.Combine(StaticValue, MatchIndex);

    public override string ToString() => StaticValue is { } value
        ? $"'{value}'"
        : $"${MatchIndex}{(CaseOps is null ? string.Empty : "[" + new string(CaseOps.ToArray()) + "]")}";
}

/// <summary>Parses replace strings (upstream's parseReplaceString and ReplacePieceBuilder).</summary>
internal static class ReplacePatternParser
{
    /// <summary>
    /// Parses a replace string:
    /// <c>\n</c> a LF, <c>\t</c> a TAB, <c>\\</c> a backslash, <c>\u</c> / <c>\U</c> / <c>\l</c> / <c>\L</c> upper- or
    /// lower-case one / all following characters of the next match reference, <c>$$</c> a dollar, <c>$&amp;</c> and
    /// <c>$0</c> the match, <c>$n</c> / <c>$nn</c> a group; everything else stays untouched.
    /// </summary>
    public static ReplacePattern ParseReplaceString(string? replaceString)
    {
        if (string.IsNullOrEmpty(replaceString))
        {
            return new ReplacePattern(null);
        }

        var caseOps = new List<char>();
        var result = new ReplacePieceBuilder(replaceString);

        for (int i = 0, len = replaceString.Length; i < len; i++)
        {
            var chCode = replaceString[i];

            if (chCode == '\\')
            {
                // move to next char
                i++;

                if (i >= len)
                {
                    // string ends with a \
                    break;
                }

                var nextChCode = replaceString[i];

                switch (nextChCode)
                {
                    case '\\':
                        // \\ => inserts a "\"
                        result.EmitUnchanged(i - 1);
                        result.EmitStatic("\\", i + 1);
                        break;
                    case 'n':
                        // \n => inserts a LF
                        result.EmitUnchanged(i - 1);
                        result.EmitStatic("\n", i + 1);
                        break;
                    case 't':
                        // \t => inserts a TAB
                        result.EmitUnchanged(i - 1);
                        result.EmitStatic("\t", i + 1);
                        break;
                    // Case modification of string replacements, patterned after Boost, but only applied
                    // to the replacement text, not subsequent content.
                    case 'u':
                    // \u => upper-cases one character.
                    case 'U':
                    // \U => upper-cases ALL following characters.
                    case 'l':
                    // \l => lower-cases one character.
                    case 'L':
                        // \L => lower-cases ALL following characters.
                        result.EmitUnchanged(i - 1);
                        result.EmitStatic(string.Empty, i + 1);
                        caseOps.Add(nextChCode);
                        break;
                }

                continue;
            }

            if (chCode == '$')
            {
                // move to next char
                i++;

                if (i >= len)
                {
                    // string ends with a $
                    break;
                }

                var nextChCode = replaceString[i];

                if (nextChCode == '$')
                {
                    // $$ => inserts a "$"
                    result.EmitUnchanged(i - 1);
                    result.EmitStatic("$", i + 1);
                    continue;
                }

                if (nextChCode == '0' || nextChCode == '&')
                {
                    // $& and $0 => inserts the matched substring.
                    result.EmitUnchanged(i - 1);
                    result.EmitMatchIndex(0, i + 1, caseOps);
                    caseOps.Clear();
                    continue;
                }

                if (nextChCode is >= '1' and <= '9')
                {
                    // $n
                    var matchIndex = nextChCode - '0';

                    // peek next char to probe for $nn
                    if (i + 1 < len)
                    {
                        var nextNextChCode = replaceString[i + 1];
                        if (nextNextChCode is >= '0' and <= '9')
                        {
                            // $nn

                            // move to next char
                            i++;
                            matchIndex = matchIndex * 10 + (nextNextChCode - '0');

                            result.EmitUnchanged(i - 2);
                            result.EmitMatchIndex(matchIndex, i + 1, caseOps);
                            caseOps.Clear();
                            continue;
                        }
                    }

                    result.EmitUnchanged(i - 1);
                    result.EmitMatchIndex(matchIndex, i + 1, caseOps);
                    caseOps.Clear();
                    continue;
                }
            }
        }

        return result.Finalize();
    }

    private sealed class ReplacePieceBuilder(string source)
    {
        private readonly List<ReplacePiece> _result = new();
        private int _lastCharIndex;
        private string _currentStaticPiece = string.Empty;

        public void EmitUnchanged(int toCharIndex)
        {
            EmitStaticValue(source.Substring(_lastCharIndex, Math.Max(0, toCharIndex - _lastCharIndex)));
            _lastCharIndex = toCharIndex;
        }

        public void EmitStatic(string value, int toCharIndex)
        {
            EmitStaticValue(value);
            _lastCharIndex = toCharIndex;
        }

        private void EmitStaticValue(string value)
        {
            if (value.Length == 0)
            {
                return;
            }

            _currentStaticPiece += value;
        }

        public void EmitMatchIndex(int index, int toCharIndex, IReadOnlyList<char> caseOps)
        {
            if (_currentStaticPiece.Length != 0)
            {
                _result.Add(ReplacePiece.FromStaticValue(_currentStaticPiece));
                _currentStaticPiece = string.Empty;
            }

            _result.Add(ReplacePiece.FromCaseOps(index, caseOps));
            _lastCharIndex = toCharIndex;
        }

        public ReplacePattern Finalize()
        {
            EmitUnchanged(source.Length);
            if (_currentStaticPiece.Length != 0)
            {
                _result.Add(ReplacePiece.FromStaticValue(_currentStaticPiece));
                _currentStaticPiece = string.Empty;
            }

            return new ReplacePattern(_result);
        }
    }
}
