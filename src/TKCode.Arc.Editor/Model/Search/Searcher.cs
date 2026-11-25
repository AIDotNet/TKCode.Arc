// Ported from vscode (src/vs/editor/common/model/textModelSearch.ts: Searcher, commit b57d5c9e), MIT license.
//
// Difference from upstream: the text may be a view (a span) of a buffer or of a line, which is searched without
// copying it. A span search gives where a match is and how long, not a Match, so it has no capture groups; when the
// captures are wanted and the expression has groups, the caller makes a string of the text first (NeedsString) and
// passes both. All the ways to ask for the next match run the same loop.

using System.Text.RegularExpressions;

namespace TKCode.Arc.Editor.Model;

/// <summary>
/// Walks the matches of one regular expression like JavaScript's global <c>exec</c>: it searches from a mutable
/// index, skips zero-length matches (stepping over a surrogate pair), and enforces whole-word matches.
/// </summary>
internal sealed class Searcher
{
    private readonly Regex _searchRegex;
    private int _lastIndex;
    private int _prevMatchStartIndex;
    private int _prevMatchLength;
    private bool? _hasCaptureGroups;

    internal Searcher(WordCharacterClassifier? wordSeparators, Regex searchRegex)
    {
        WordSeparators = wordSeparators;
        _searchRegex = searchRegex;
        _prevMatchStartIndex = -1;
        _prevMatchLength = 0;
        _lastIndex = 0;
    }

    internal WordCharacterClassifier? WordSeparators { get; }

    /// <summary>
    /// Whether the expression has groups besides the whole match. Without them the captures of a match are just its
    /// text, so the span search can report them.
    /// </summary>
    internal bool HasCaptureGroups => _hasCaptureGroups ??= _searchRegex.GetGroupNumbers().Length > 1;

    /// <summary>
    /// Whether the text must be handed to <see cref="Next(ReadOnlySpan{char}, string, bool, out int, out int, out string[])"/>
    /// as a string too: only when captures are wanted and there are groups to capture.
    /// </summary>
    internal bool NeedsString(bool captureMatches) => captureMatches && HasCaptureGroups;

    /// <summary>Starts the next search at <paramref name="lastIndex"/>.</summary>
    internal void Reset(int lastIndex)
    {
        _lastIndex = lastIndex;
        _prevMatchStartIndex = -1;
        _prevMatchLength = 0;
    }

    /// <summary>The next match, or null at the end of the text (or when a match timed out).</summary>
    internal Match? Next(string text) => NextCore(text, text, out var match, out _, out _) ? match : null;

    /// <summary>
    /// The next match in a view of the text: where it starts and how long it is. Returns false at the end of the text
    /// (or when a match timed out). It finds the same matches as <see cref="Next(string)"/> but has no capture groups.
    /// </summary>
    internal bool Next(ReadOnlySpan<char> text, out int matchStartIndex, out int matchLength) =>
        NextCore(null, text, out _, out matchStartIndex, out matchLength);

    /// <summary>
    /// The next match in <paramref name="text"/> and, when <paramref name="captureMatches"/>, its captures the way
    /// <see cref="FindMatch.Matches"/> holds them. <paramref name="input"/> is the same text as a string when
    /// <see cref="NeedsString"/> asks for it, and null otherwise.
    /// </summary>
    internal bool Next(
        ReadOnlySpan<char> text,
        string? input,
        bool captureMatches,
        out int matchStartIndex,
        out int matchLength,
        out string?[]? matches)
    {
        if (!NextCore(input, input is not null ? input : text, out var match, out matchStartIndex, out matchLength))
        {
            matches = null;
            return false;
        }

        if (!captureMatches)
        {
            matches = null;
        }
        else if (match is not null)
        {
            matches = TextModelSearch.GetCaptures(match);
        }
        else
        {
            // No groups besides the whole match (NeedsString was false): the match is the only capture.
            matches = [new string(text.Slice(matchStartIndex, matchLength))];
        }

        return true;
    }

    private bool NextCore(
        string? input,
        ReadOnlySpan<char> text,
        out Match? match,
        out int matchStartIndex,
        out int matchLength)
    {
        match = null;
        matchStartIndex = 0;
        matchLength = 0;
        var textLength = text.Length;

        while (true)
        {
            if (_prevMatchStartIndex + _prevMatchLength == textLength)
            {
                // Reached the end of the line
                return false;
            }

            if (_lastIndex > textLength)
            {
                // JavaScript's exec returns null when lastIndex is past the end.
                return false;
            }

            Match? current = null;
            try
            {
                if (input is not null)
                {
                    current = _searchRegex.Match(input, _lastIndex);
                    if (!current.Success)
                    {
                        return false;
                    }

                    matchStartIndex = current.Index;
                    matchLength = current.Length;
                }
                else
                {
                    var enumerator = _searchRegex.EnumerateMatches(text, _lastIndex);
                    if (!enumerator.MoveNext())
                    {
                        return false;
                    }

                    matchStartIndex = enumerator.Current.Index;
                    matchLength = enumerator.Current.Length;
                }
            }
            catch (RegexMatchTimeoutException)
            {
                // A pathological user pattern: stop searching instead of hanging the caller.
                return false;
            }

            _lastIndex = matchStartIndex + matchLength;

            if (matchStartIndex == _prevMatchStartIndex && matchLength == _prevMatchLength)
            {
                if (matchLength == 0)
                {
                    // The search result is an empty string and won't advance the index, so the next search would stick
                    // here; recover by advancing by two if a surrogate pair is found and by one otherwise.
                    if (SearchHelpers.GetNextCodePoint(text, textLength, _lastIndex) > 0xFFFF)
                    {
                        _lastIndex += 2;
                    }
                    else
                    {
                        _lastIndex += 1;
                    }

                    continue;
                }

                // Exit early if the regex matches the same range twice
                return false;
            }

            _prevMatchStartIndex = matchStartIndex;
            _prevMatchLength = matchLength;

            if (WordSeparators is null
                || SearchMatchValidation.IsValidMatch(WordSeparators, text, textLength, matchStartIndex, matchLength))
            {
                match = current;
                return true;
            }
        }
    }
}
