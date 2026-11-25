// Ported from vscode (src/vs/editor/common/languages/supports/richEditBrackets.ts, commit b57d5c9e), MIT license.
// Differences from upstream:
//  - the regular expressions are .NET Regex built by RegExps.CreateRegExp (case-insensitive, JavaScript semantics);
//  - toReversedString keeps no cache of its last input (upstream's is module state shared by every caller);
//  - textIsBracket and textIsOpenBracket are dictionaries; the module functions are static members of
//    RichEditBrackets (createBracketOrRegExp and toReversedString public, the rest private);
//  - the line texts are cut with JavaScript's substring semantics (indices clamped).

using System.Text.RegularExpressions;
using TKCode.Arc.Editor.Model;

namespace TKCode.Arc.Editor.Languages;

/// <summary>
/// Represents a grouping of colliding bracket pairs.
///
/// Most of the times this contains a single bracket pair,
/// but sometimes this contains multiple bracket pairs in cases
/// where the same string appears as a closing bracket for multiple
/// bracket pairs, or the same string appears an opening bracket for
/// multiple bracket pairs.
///
/// e.g. of a group containing a single pair:
///   open: ['{'], close: ['}']
///
/// e.g. of a group containing multiple pairs:
///   open: ['if', 'for'], close: ['end', 'end']
/// </summary>
internal sealed class RichEditBracket
{
    private readonly HashSet<string> _openSet;
    private readonly HashSet<string> _closeSet;

    public RichEditBracket(string languageId, int index, IReadOnlyList<string> open, IReadOnlyList<string> close, Regex forwardRegex, Regex reversedRegex)
    {
        LanguageId = languageId;
        Index = index;
        Open = open;
        Close = close;
        ForwardRegex = forwardRegex;
        ReversedRegex = reversedRegex;
        _openSet = new HashSet<string>(Open, StringComparer.Ordinal);
        _closeSet = new HashSet<string>(Close, StringComparer.Ordinal);
    }

    public string LanguageId { get; }

    /// <summary>
    /// A 0-based consecutive unique identifier for this bracket pair.
    /// If a language has 5 bracket pairs, out of which 2 are grouped together,
    /// it is expected that the <c>index</c> goes from 0 to 4.
    /// </summary>
    public int Index { get; }

    /// <summary>
    /// The open sequence for each bracket pair contained in this group.
    /// The open sequence at a specific index corresponds to the closing sequence at the same index.
    /// </summary>
    public IReadOnlyList<string> Open { get; }

    /// <summary>
    /// The close sequence for each bracket pair contained in this group.
    /// The close sequence at a specific index corresponds to the opening sequence at the same index.
    /// </summary>
    public IReadOnlyList<string> Close { get; }

    /// <summary>
    /// A regular expression that is useful to search for this bracket pair group in a string.
    /// This regular expression is built in a way that it is aware of the other bracket
    /// pairs defined for the language, so it might match brackets from other groups.
    /// </summary>
    public Regex ForwardRegex { get; }

    /// <summary>
    /// A regular expression that is useful to search for this bracket pair group in a string backwards.
    /// This regular expression is built in a way that it is aware of the other bracket
    /// pairs defined for the language, so it might match brackets from other groups.
    /// </summary>
    public Regex ReversedRegex { get; }

    /// <summary>Check if the provided <paramref name="text"/> is an open bracket in this group.</summary>
    public bool IsOpen(string text)
    {
        return _openSet.Contains(text);
    }

    /// <summary>Check if the provided <paramref name="text"/> is a close bracket in this group.</summary>
    public bool IsClose(string text)
    {
        return _closeSet.Contains(text);
    }
}

/// <summary>The bracket groups of a language and the regular expressions that find them.</summary>
internal sealed class RichEditBrackets
{
    private sealed record InternalBracket(List<string> Open, List<string> Close);

    public RichEditBrackets(string languageId, IReadOnlyList<CharacterPair> brackets)
    {
        var groupedBrackets = GroupFuzzyBrackets(brackets);

        var richBrackets = new List<RichEditBracket>(groupedBrackets.Count);
        for (var index = 0; index < groupedBrackets.Count; index++)
        {
            var b = groupedBrackets[index];
            richBrackets.Add(new RichEditBracket(
                languageId,
                index,
                b.Open,
                b.Close,
                GetRegexForBracketPair(b.Open, b.Close, groupedBrackets, index),
                GetReversedRegexForBracketPair(b.Open, b.Close, groupedBrackets, index)));
        }
        Brackets = richBrackets;

        ForwardRegex = GetRegexForBrackets(Brackets);
        ReversedRegex = GetReversedRegexForBrackets(Brackets);

        var textIsBracket = new Dictionary<string, RichEditBracket>(StringComparer.Ordinal);
        var textIsOpenBracket = new Dictionary<string, bool>(StringComparer.Ordinal);

        MaxBracketLength = 0;
        foreach (var bracket in Brackets)
        {
            foreach (var open in bracket.Open)
            {
                textIsBracket[open] = bracket;
                textIsOpenBracket[open] = true;
                MaxBracketLength = Math.Max(MaxBracketLength, open.Length);
            }
            foreach (var close in bracket.Close)
            {
                textIsBracket[close] = bracket;
                textIsOpenBracket[close] = false;
                MaxBracketLength = Math.Max(MaxBracketLength, close.Length);
            }
        }

        TextIsBracket = textIsBracket;
        TextIsOpenBracket = textIsOpenBracket;
    }

    /// <summary>All groups of brackets defined for this language.</summary>
    public IReadOnlyList<RichEditBracket> Brackets { get; }

    /// <summary>A regular expression that is useful to search for all bracket pairs in a string.</summary>
    public Regex ForwardRegex { get; }

    /// <summary>A regular expression that is useful to search for all bracket pairs in a string backwards.</summary>
    public Regex ReversedRegex { get; }

    /// <summary>The length (i.e. str.length) for the longest bracket pair.</summary>
    public int MaxBracketLength { get; }

    /// <summary>A map useful for decoding a regex match and finding which bracket group was matched.</summary>
    public IReadOnlyDictionary<string, RichEditBracket> TextIsBracket { get; }

    /// <summary>A set useful for decoding if a regex match is the open bracket of a bracket pair.</summary>
    public IReadOnlyDictionary<string, bool> TextIsOpenBracket { get; }

    /// <summary>
    /// Groups together brackets that have equal open or close sequences.
    ///
    /// For example, if the following brackets are defined:
    ///   ['IF','END']
    ///   ['for','end']
    ///   ['{','}']
    ///
    /// Then the grouped brackets would be:
    ///   { open: ['if', 'for'], close: ['end', 'end'] }
    ///   { open: ['{'], close: ['}'] }
    /// </summary>
    private static List<InternalBracket> GroupFuzzyBrackets(IReadOnlyList<CharacterPair> brackets)
    {
        var n = brackets.Count;

        var lowered = brackets.Select(b => new CharacterPair(b.Open.ToLowerInvariant(), b.Close.ToLowerInvariant())).ToArray();

        var group = new int[n];
        for (var i = 0; i < n; i++)
        {
            group[i] = i;
        }

        static bool AreOverlapping(CharacterPair a, CharacterPair b)
        {
            var (aOpen, aClose) = a;
            var (bOpen, bClose) = b;
            return aOpen == bOpen || aOpen == bClose || aClose == bOpen || aClose == bClose;
        }

        void MergeGroups(int g1, int g2)
        {
            var newG = Math.Min(g1, g2);
            var oldG = Math.Max(g1, g2);
            for (var i = 0; i < n; i++)
            {
                if (group[i] == oldG)
                {
                    group[i] = newG;
                }
            }
        }

        // group together brackets that have the same open or the same close sequence
        for (var i = 0; i < n; i++)
        {
            var a = lowered[i];
            for (var j = i + 1; j < n; j++)
            {
                var b = lowered[j];
                if (AreOverlapping(a, b))
                {
                    MergeGroups(group[i], group[j]);
                }
            }
        }

        var result = new List<InternalBracket>();
        for (var g = 0; g < n; g++)
        {
            var currentOpen = new List<string>();
            var currentClose = new List<string>();
            for (var i = 0; i < n; i++)
            {
                if (group[i] == g)
                {
                    var (open, close) = lowered[i];
                    currentOpen.Add(open);
                    currentClose.Add(close);
                }
            }
            if (currentOpen.Count > 0)
            {
                result.Add(new InternalBracket(currentOpen, currentClose));
            }
        }
        return result;
    }

    private static void CollectSuperstrings(string str, List<InternalBracket> brackets, int currentIndex, List<string> dest)
    {
        for (int i = 0, len = brackets.Count; i < len; i++)
        {
            if (i == currentIndex)
            {
                continue;
            }
            var bracket = brackets[i];
            foreach (var open in bracket.Open)
            {
                if (open.Contains(str, StringComparison.Ordinal))
                {
                    dest.Add(open);
                }
            }
            foreach (var close in bracket.Close)
            {
                if (close.Contains(str, StringComparison.Ordinal))
                {
                    dest.Add(close);
                }
            }
        }
    }

    private static List<string> Unique(List<string> arr)
    {
        if (arr.Count <= 1)
        {
            return arr;
        }
        var result = new List<string>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var element in arr)
        {
            if (!seen.Add(element))
            {
                continue;
            }
            result.Add(element);
        }
        return result;
    }

    // Upstream: pieces.sort(lengthcmp); pieces.reverse(). Array.prototype.sort is stable, then the whole array is
    // reversed, so equal lengths end up in reverse order of appearance.
    private static List<string> SortByLengthThenReverse(List<string> pieces)
    {
        var sorted = pieces.Select((piece, index) => (piece, index))
            .OrderBy(p => p.piece.Length)
            .ThenBy(p => p.index)
            .Select(p => p.piece)
            .ToList();
        sorted.Reverse();
        return sorted;
    }

    /// <summary>
    /// Create a regular expression that can be used to search forward in a piece of text
    /// for a group of bracket pairs. But this regex must be built in a way in which
    /// it is aware of the other bracket pairs defined for the language.
    ///
    /// For example, if a language contains the following bracket pairs:
    ///   ['begin', 'end']
    ///   ['if', 'end if']
    /// The two bracket pairs do not collide because no open or close brackets are equal.
    /// So the function getRegexForBracketPair is called twice, once with
    /// the ['begin'], ['end'] group consisting of one bracket pair, and once with
    /// the ['if'], ['end if'] group consiting of the other bracket pair.
    ///
    /// But there could be a situation where an occurrence of 'end if' is mistaken
    /// for an occurrence of 'end'.
    ///
    /// Therefore, for the bracket pair ['begin', 'end'], the regex will also
    /// target 'end if'. The regex will be something like:
    ///   /(\bend if\b)|(\bend\b)|(\bif\b)/
    ///
    /// The regex also searches for "superstrings" (other brackets that might be mistaken with the current bracket).
    /// </summary>
    private static Regex GetRegexForBracketPair(List<string> open, List<string> close, List<InternalBracket> brackets, int currentIndex)
    {
        return CreateBracketOrRegExp(CollectPiecesForBracketPair(open, close, brackets, currentIndex));
    }

    /// <summary>
    /// Matching a regular expression in JS can only be done "forwards". So JS offers natively only
    /// methods to find the first match of a regex in a string. But sometimes, it is useful to
    /// find the last match of a regex in a string. For such a situation, a nice solution is to
    /// simply reverse the string and then search for a reversed regex.
    ///
    /// This function also has the fine details of <c>getRegexForBracketPair</c>. For the same example
    /// given above, the regex produced here would look like:
    ///   /(\bfi dne\b)|(\bdne\b)|(\bfi\b)/
    /// </summary>
    private static Regex GetReversedRegexForBracketPair(List<string> open, List<string> close, List<InternalBracket> brackets, int currentIndex)
    {
        var pieces = CollectPiecesForBracketPair(open, close, brackets, currentIndex);
        return CreateBracketOrRegExp(pieces.Select(ToReversedString).ToList());
    }

    private static List<string> CollectPiecesForBracketPair(List<string> open, List<string> close, List<InternalBracket> brackets, int currentIndex)
    {
        // search in all brackets for other brackets that are a superstring of these brackets
        var pieces = new List<string>();
        pieces.AddRange(open);
        pieces.AddRange(close);
        for (int i = 0, len = pieces.Count; i < len; i++)
        {
            CollectSuperstrings(pieces[i], brackets, currentIndex, pieces);
        }
        pieces = Unique(pieces);
        return SortByLengthThenReverse(pieces);
    }

    /// <summary>
    /// Creates a regular expression that targets all bracket pairs.
    ///
    /// e.g. for the bracket pairs:
    ///  ['{','}']
    ///  ['begin,'end']
    ///  ['for','end']
    /// the regex would look like:
    ///  /(\{)|(\})|(\bbegin\b)|(\bend\b)|(\bfor\b)/
    /// </summary>
    private static Regex GetRegexForBrackets(IReadOnlyList<RichEditBracket> brackets)
    {
        return CreateBracketOrRegExp(Unique(CollectAllPieces(brackets)));
    }

    /// <summary>
    /// The reversed form of <see cref="GetRegexForBrackets"/>, e.g. for the bracket pairs:
    ///  ['{','}']
    ///  ['begin,'end']
    ///  ['for','end']
    /// the regex would look like:
    ///  /(\{)|(\})|(\bnigeb\b)|(\bdne\b)|(\brof\b)/
    /// </summary>
    private static Regex GetReversedRegexForBrackets(IReadOnlyList<RichEditBracket> brackets)
    {
        return CreateBracketOrRegExp(Unique(CollectAllPieces(brackets)).Select(ToReversedString).ToList());
    }

    private static List<string> CollectAllPieces(IReadOnlyList<RichEditBracket> brackets)
    {
        var pieces = new List<string>();
        foreach (var bracket in brackets)
        {
            foreach (var open in bracket.Open)
            {
                pieces.Add(open);
            }
            foreach (var close in bracket.Close)
            {
                pieces.Add(close);
            }
        }
        return pieces;
    }

    private static string PrepareBracketForRegExp(string str)
    {
        // This bracket pair uses letters like e.g. "begin" - "end"
        var insertWordBoundaries = str.Length > 0 && str.All(ch => ch == ' ' || RegExps.IsWordCharacter(ch));
        str = SearchHelpers.EscapeRegExpCharacters(str);
        return insertWordBoundaries ? $"\\b{str}\\b" : str;
    }

    /// <summary>A case-insensitive (unless <paramref name="options"/> say otherwise) alternation of the pieces.</summary>
    public static Regex CreateBracketOrRegExp(IReadOnlyList<string> pieces, RegExpOptions options = default)
    {
        var regexStr = $"({string.Join(")|(", pieces.Select(PrepareBracketForRegExp))})";
        return RegExps.CreateRegExp(regexStr, isRegex: true, options);
    }

    /// <summary>The UTF-16 code units of <paramref name="str"/> in reverse order.</summary>
    public static string ToReversedString(string str)
    {
        var arr = str.ToCharArray();
        Array.Reverse(arr);
        return new string(arr);
    }
}

/// <summary>Finds brackets in a line with the regular expressions of <see cref="RichEditBrackets"/>.</summary>
internal static class BracketsUtils
{
    private static TextRange? FindPrevBracketInText(Regex reversedBracketRegex, int lineNumber, string reversedText, int offset)
    {
        var m = reversedBracketRegex.Match(reversedText);

        if (!m.Success)
        {
            return null;
        }

        var matchOffset = reversedText.Length - m.Index;
        var matchLength = m.Length;
        var absoluteMatchOffset = offset + matchOffset;

        return new TextRange(lineNumber, absoluteMatchOffset - matchLength + 1, lineNumber, absoluteMatchOffset + 1);
    }

    public static TextRange? FindPrevBracketInRange(Regex reversedBracketRegex, int lineNumber, string lineText, int startOffset, int endOffset)
    {
        // Because JS does not support backwards regex search, we search forwards in a reversed string with a reversed regex ;)
        var reversedLineText = RichEditBrackets.ToReversedString(lineText);
        var reversedSubstr = SearchHelpers.JsSubstring(reversedLineText, lineText.Length - endOffset, lineText.Length - startOffset).ToString();
        return FindPrevBracketInText(reversedBracketRegex, lineNumber, reversedSubstr, startOffset);
    }

    public static TextRange? FindNextBracketInText(Regex bracketRegex, int lineNumber, string text, int offset)
    {
        var m = bracketRegex.Match(text);

        if (!m.Success)
        {
            return null;
        }

        var matchOffset = m.Index;
        var matchLength = m.Length;
        if (matchLength == 0)
        {
            return null;
        }
        var absoluteMatchOffset = offset + matchOffset;

        return new TextRange(lineNumber, absoluteMatchOffset + 1, lineNumber, absoluteMatchOffset + 1 + matchLength);
    }

    public static TextRange? FindNextBracketInRange(Regex bracketRegex, int lineNumber, string lineText, int startOffset, int endOffset)
    {
        var substr = SearchHelpers.JsSubstring(lineText, startOffset, endOffset).ToString();
        return FindNextBracketInText(bracketRegex, lineNumber, substr, startOffset);
    }
}
