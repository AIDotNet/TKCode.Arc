// Ported from vscode (src/vs/base/common/filters.ts, commit b57d5c9e), MIT license.
// Differences from upstream:
//  - IMatch is the record struct FilterMatch; IFilter is the delegate FilterFn; a null result is "no match".
//  - FuzzyScore ([score, wordStart, ...matches]) is a record with Score, WordStart and Matches (the match positions in
//    upstream's order: the last match first). Code that assigns score[0] uses `with { Score = ... }`.
//  - fuzzyScore's tables (_table, _arrows, _diag, _minWordMatchPos, _maxWordMatchPos) are module-level in upstream;
//    here they are per thread ([ThreadStatic] scratch tables), so scoring on several threads at once is safe and no
//    state is shared between editors.
//  - The LRU caches of matchesFuzzy's regular expressions and of tryNormalizeToBase are left out (the engine keeps no
//    shared mutable state); the values are computed on each call.
//  - The Korean alternate characters of matchesWords (naturalLanguage/korean.ts) are not ported: a character matches
//    only itself or, for word separators, another separator.
//  - String case mapping is .NET's ToLowerInvariant (upstream: toLowerCase); both keep the UTF-16 length.

using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace TKCode.Arc.Editor;

/// <summary>A matched range of a word, start inclusive and end exclusive (upstream's IMatch).</summary>
internal readonly record struct FilterMatch(int Start, int End);

/// <summary>A filter: the matches of <c>word</c> in <c>wordToMatchAgainst</c>, or null (upstream's IFilter).</summary>
internal delegate List<FilterMatch>? FilterFn(string word, string wordToMatchAgainst);

/// <summary>
/// The result of <see cref="Filters.FuzzyScore"/> (upstream's FuzzyScore tuple): the score, the offset in the word at
/// which matching started and the matched positions relative to it, last match first.
/// </summary>
internal sealed record FuzzyScore(int Score, int WordStart, int[] Matches)
{

    /// <summary>No matches and value <c>-100</c>.</summary>
    public static readonly FuzzyScore Default = new(-100, 0, []);

    /// <summary>Whether <paramref name="score"/> is null or <see cref="Default"/>.</summary>
    public static bool IsDefault(FuzzyScore? score) =>
        score is null || (score.Matches.Length == 0 && score.Score == -100 && score.WordStart == 0);
}

/// <summary>Options of <see cref="Filters.FuzzyScore"/> (upstream's FuzzyScoreOptions).</summary>
internal sealed record FuzzyScoreOptions(bool FirstMatchCanBeWeak, bool BoostFullMatch)
{
    public static readonly FuzzyScoreOptions Default = new(FirstMatchCanBeWeak: false, BoostFullMatch: true);
}

/// <summary>A fuzzy scorer such as <see cref="Filters.FuzzyScore"/> (upstream's FuzzyScorer).</summary>
internal delegate FuzzyScore? FuzzyScorer(string pattern, string lowPattern, int patternPos, string word, string lowWord, int wordPos, FuzzyScoreOptions? options);

/// <summary>String filters and the fuzzy scorer of IntelliSense (upstream's filters.ts).</summary>
internal static class Filters
{
    /// <summary>Shared matches for an empty query; do not change.</summary>
    public static IReadOnlyList<FilterMatch> EmptyMatches { get; } = [new FilterMatch(0, 0)];

    // ---- combined filters

    /// <summary>A filter which combines the given ones with an or: the first that matches decides.</summary>
    public static FilterFn Or(params FilterFn[] filters) => (word, wordToMatchAgainst) =>
    {
        foreach (var filter in filters)
        {
            var match = filter(word, wordToMatchAgainst);
            if (match is not null)
            {
                return match;
            }
        }
        return null;
    };

    // ---- prefix

    public static readonly FilterFn MatchesStrictPrefix = (word, target) => MatchesPrefixCore(false, word, target);

    public static readonly FilterFn MatchesPrefix = (word, target) => MatchesPrefixCore(true, word, target);

    private static List<FilterMatch>? MatchesPrefixCore(bool ignoreCase, string word, string wordToMatchAgainst)
    {
        if (string.IsNullOrEmpty(wordToMatchAgainst) || wordToMatchAgainst.Length < word.Length)
        {
            return null;
        }

        var matches = ignoreCase
            ? wordToMatchAgainst.StartsWith(word, StringComparison.OrdinalIgnoreCase)
            : wordToMatchAgainst.StartsWith(word, StringComparison.Ordinal);
        if (!matches)
        {
            return null;
        }

        return word.Length > 0 ? [new FilterMatch(0, word.Length)] : [];
    }

    // ---- contiguous substring

    public static List<FilterMatch>? MatchesContiguousSubString(string word, string wordToMatchAgainst)
    {
        if (word.Length == 0)
        {
            return [.. EmptyMatches];
        }

        if (word.Length > wordToMatchAgainst.Length)
        {
            return null;
        }

        var index = wordToMatchAgainst.ToLowerInvariant().IndexOf(word.ToLowerInvariant(), StringComparison.Ordinal);
        if (index == -1)
        {
            return null;
        }

        return [new FilterMatch(index, index + word.Length)];
    }

    public static List<FilterMatch>? MatchesBaseContiguousSubString(string word, string wordToMatchAgainst)
    {
        if (word.Length == 0)
        {
            return [.. EmptyMatches];
        }

        if (word.Length > wordToMatchAgainst.Length)
        {
            return null;
        }

        word = TryNormalizeToBase(word);
        wordToMatchAgainst = TryNormalizeToBase(wordToMatchAgainst);
        var index = wordToMatchAgainst.IndexOf(word, StringComparison.Ordinal);
        if (index == -1)
        {
            return null;
        }

        return [new FilterMatch(index, index + word.Length)];
    }

    // ---- substring

    public static List<FilterMatch>? MatchesSubString(string word, string wordToMatchAgainst)
    {
        if (word.Length > wordToMatchAgainst.Length)
        {
            return null;
        }

        return MatchesSubStringCore(word.ToLowerInvariant(), wordToMatchAgainst.ToLowerInvariant(), 0, 0);
    }

    private static List<FilterMatch>? MatchesSubStringCore(string word, string wordToMatchAgainst, int i, int j)
    {
        // upstream recurses on j; iterate over the skipped characters instead (same result, no deep recursion)
        while (true)
        {
            if (i == word.Length)
            {
                return [];
            }
            if (j == wordToMatchAgainst.Length)
            {
                return null;
            }
            if (word[i] == wordToMatchAgainst[j])
            {
                var result = MatchesSubStringCore(word, wordToMatchAgainst, i + 1, j + 1);
                if (result is not null)
                {
                    return Join(new FilterMatch(j, j + 1), result);
                }
                return null;
            }
            j++;
        }
    }

    // ---- camel case

    private static bool IsLower(int code) => code >= 'a' && code <= 'z';

    public static bool IsUpper(int code) => code >= 'A' && code <= 'Z';

    private static bool IsNumber(int code) => code >= '0' && code <= '9';

    private static bool IsWhitespace(int code) => code is ' ' or '\t' or '\n' or '\r';

    // natural word separators of written text; a subset of the editor's word separators
    private const string WordSeparators = "()[]{}<>`'\"-/;:,.?!";

    private static bool IsWordSeparator(int code) => IsWhitespace(code) || (code < 128 && WordSeparators.Contains((char)code));

    private static bool CharactersMatch(int codeA, int codeB) => codeA == codeB || (IsWordSeparator(codeA) && IsWordSeparator(codeB));

    private static bool IsAlphanumeric(int code) => IsLower(code) || IsUpper(code) || IsNumber(code);

    private static List<FilterMatch> Join(FilterMatch head, List<FilterMatch> tail)
    {
        if (tail.Count == 0)
        {
            tail.Add(head);
        }
        else if (head.End == tail[0].Start)
        {
            tail[0] = tail[0] with { Start = head.Start };
        }
        else
        {
            tail.Insert(0, head);
        }
        return tail;
    }

    private static int NextAnchor(string camelCaseWord, int start)
    {
        for (var i = start; i < camelCaseWord.Length; i++)
        {
            var c = camelCaseWord[i];
            if (IsUpper(c) || IsNumber(c) || (i > 0 && !IsAlphanumeric(camelCaseWord[i - 1])))
            {
                return i;
            }
        }
        return camelCaseWord.Length;
    }

    private static List<FilterMatch>? MatchesCamelCaseCore(string word, string camelCaseWord, int i, int j)
    {
        if (i == word.Length)
        {
            return [];
        }
        if (j == camelCaseWord.Length)
        {
            return null;
        }
        if (word[i] != char.ToLowerInvariant(camelCaseWord[j]))
        {
            return null;
        }

        var nextUpperIndex = j + 1;
        var result = MatchesCamelCaseCore(word, camelCaseWord, i + 1, j + 1);
        while (result is null && (nextUpperIndex = NextAnchor(camelCaseWord, nextUpperIndex)) < camelCaseWord.Length)
        {
            result = MatchesCamelCaseCore(word, camelCaseWord, i + 1, nextUpperIndex);
            nextUpperIndex++;
        }
        return result is null ? null : Join(new FilterMatch(j, j + 1), result);
    }

    private readonly record struct CamelCaseAnalysis(double UpperPercent, double LowerPercent, double AlphaPercent, double NumericPercent);

    // Heuristic to avoid computing camel case matcher for words that don't look like camelCaseWords.
    private static CamelCaseAnalysis AnalyzeCamelCaseWord(string word)
    {
        int upper = 0, lower = 0, alpha = 0, numeric = 0;
        foreach (var code in word)
        {
            if (IsUpper(code))
            {
                upper++;
            }
            if (IsLower(code))
            {
                lower++;
            }
            if (IsAlphanumeric(code))
            {
                alpha++;
            }
            if (IsNumber(code))
            {
                numeric++;
            }
        }

        double length = word.Length;
        return new CamelCaseAnalysis(upper / length, lower / length, alpha / length, numeric / length);
    }

    private static bool IsUpperCaseWord(CamelCaseAnalysis analysis) => analysis.LowerPercent == 0 && analysis.UpperPercent > 0.6;

    private static bool IsCamelCaseWord(CamelCaseAnalysis analysis) =>
        analysis.LowerPercent > 0.2 && analysis.UpperPercent < 0.8 && analysis.AlphaPercent > 0.6 && analysis.NumericPercent < 0.2;

    // Heuristic to avoid computing camel case matcher for words that don't look like camel case patterns.
    private static bool IsCamelCasePattern(string word)
    {
        int upper = 0, lower = 0, whitespace = 0;
        foreach (var code in word)
        {
            if (IsUpper(code))
            {
                upper++;
            }
            if (IsLower(code))
            {
                lower++;
            }
            if (IsWhitespace(code))
            {
                whitespace++;
            }
        }

        if ((upper == 0 || lower == 0) && whitespace == 0)
        {
            return word.Length <= 30;
        }
        return upper <= 5;
    }

    public static List<FilterMatch>? MatchesCamelCase(string word, string camelCaseWord)
    {
        if (string.IsNullOrEmpty(camelCaseWord))
        {
            return null;
        }

        camelCaseWord = camelCaseWord.Trim();
        if (camelCaseWord.Length == 0)
        {
            return null;
        }

        if (!IsCamelCasePattern(word))
        {
            return null;
        }

        if (camelCaseWord.Length > 60)
        {
            camelCaseWord = camelCaseWord[..60];
        }

        var analysis = AnalyzeCamelCaseWord(camelCaseWord);
        if (!IsCamelCaseWord(analysis))
        {
            if (!IsUpperCaseWord(analysis))
            {
                return null;
            }
            camelCaseWord = camelCaseWord.ToLowerInvariant();
        }

        List<FilterMatch>? result = null;
        var i = 0;
        word = word.ToLowerInvariant();
        while (i < camelCaseWord.Length && (result = MatchesCamelCaseCore(word, camelCaseWord, 0, i)) is null)
        {
            i = NextAnchor(camelCaseWord, i + 1);
        }
        return result;
    }

    // ---- words

    /// <summary>
    /// Matches beginnings of words, also of non-ASCII languages. When <paramref name="contiguous"/> is true, "pul"
    /// matches "Git: Pull"; otherwise also substrings of the word with beginnings of words: "gp" or "g p" match it.
    /// </summary>
    public static List<FilterMatch>? MatchesWords(string word, string target, bool contiguous = false)
    {
        if (string.IsNullOrEmpty(target))
        {
            return null;
        }

        List<FilterMatch>? result = null;
        var targetIndex = 0;
        word = TryNormalizeToBase(word);
        target = TryNormalizeToBase(target);

        // memoize recursive calls within one top-level invocation: separators match each other, which would otherwise
        // make the recursion explode for inputs like `editor.action`
        var memo = new Dictionary<int, List<FilterMatch>?>();
        while (targetIndex < target.Length)
        {
            result = MatchesWordsCore(word, target, 0, targetIndex, contiguous, memo);
            if (result is not null)
            {
                break;
            }
            targetIndex = NextWord(target, targetIndex + 1);
        }
        return result;
    }

    private static List<FilterMatch>? MatchesWordsCore(string word, string target, int wordIndex, int targetIndex, bool contiguous, Dictionary<int, List<FilterMatch>?> memo)
    {
        if (wordIndex == word.Length)
        {
            return [];
        }
        if (targetIndex == target.Length)
        {
            return null;
        }

        var memoKey = wordIndex * (target.Length + 1) + targetIndex;
        if (memo.TryGetValue(memoKey, out var cached))
        {
            // the caller (Join) changes the returned list, so always return a copy
            return cached is null ? null : [.. cached];
        }

        var computed = MatchesWordsCompute(word, target, wordIndex, targetIndex, contiguous, memo);
        memo[memoKey] = computed is null ? null : [.. computed];
        return computed;
    }

    private static List<FilterMatch>? MatchesWordsCompute(string word, string target, int wordIndex, int targetIndex, bool contiguous, Dictionary<int, List<FilterMatch>?> memo)
    {
        if (!CharactersMatch(word[wordIndex], target[targetIndex]))
        {
            return null;
        }

        var nextWordIndex = targetIndex + 1;
        var result = MatchesWordsCore(word, target, wordIndex + 1, nextWordIndex, contiguous, memo);
        if (!contiguous)
        {
            while (result is null && (nextWordIndex = NextWord(target, nextWordIndex)) < target.Length)
            {
                result = MatchesWordsCore(word, target, wordIndex + 1, nextWordIndex, contiguous, memo);
                nextWordIndex++;
            }
        }

        if (result is null)
        {
            return null;
        }

        // characters that differ are word separators: keep the target but do not highlight them
        if (word[wordIndex] != target[targetIndex])
        {
            return result;
        }

        return Join(new FilterMatch(targetIndex, targetIndex + 1), result);
    }

    private static int NextWord(string word, int start)
    {
        for (var i = start; i < word.Length; i++)
        {
            if (IsWordSeparator(word[i]) || (i > 0 && IsWordSeparator(word[i - 1])))
            {
                return i;
            }
        }
        return word.Length;
    }

    // ---- fuzzy

    private static readonly FilterFn s_fuzzyContiguousFilter = Or(MatchesPrefix, MatchesCamelCase, MatchesContiguousSubString);
    private static readonly FilterFn s_fuzzySeparateFilter = Or(MatchesPrefix, MatchesCamelCase, MatchesSubString);

    public static List<FilterMatch>? MatchesFuzzy(string word, string wordToMatchAgainst, bool enableSeparateSubstringMatching = false)
    {
        if (word.Length == 0)
        {
            return [.. EmptyMatches];
        }

        // RegExp filter for wildcard matches
        var regexp = new Regex(ConvertSimple2RegExpPattern(word), RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        var match = regexp.Match(wordToMatchAgainst);
        if (match.Success)
        {
            return [new FilterMatch(match.Index, match.Index + match.Length)];
        }

        // default filter
        return enableSeparateSubstringMatching ? s_fuzzySeparateFilter(word, wordToMatchAgainst) : s_fuzzyContiguousFilter(word, wordToMatchAgainst);
    }

    /// <summary>Matches <paramref name="pattern"/> against <paramref name="word"/> like IntelliSense does.</summary>
    public static List<FilterMatch>? MatchesFuzzy2(string pattern, string word)
    {
        var score = FuzzyScore(pattern, pattern.ToLowerInvariant(), 0, word, word.ToLowerInvariant(), 0, new FuzzyScoreOptions(FirstMatchCanBeWeak: true, BoostFullMatch: true));
        return score is not null ? CreateMatches(score) : null;
    }

    /// <summary>A score for any match, trying the pattern from later starts; <c>[0, wordPos]</c> when none.</summary>
    public static FuzzyScore AnyScore(string pattern, string lowPattern, int patternPos, string word, string lowWord, int wordPos)
    {
        var max = Math.Min(13, pattern.Length);
        for (; patternPos < max; patternPos++)
        {
            var result = FuzzyScore(pattern, lowPattern, patternPos, word, lowWord, wordPos, new FuzzyScoreOptions(FirstMatchCanBeWeak: true, BoostFullMatch: true));
            if (result is not null)
            {
                return result;
            }
        }
        return new FuzzyScore(0, wordPos, []);
    }

    // ---- fuzzyScore

    /// <summary>The matched ranges of a <see cref="FuzzyScore"/> (consecutive positions merged).</summary>
    public static List<FilterMatch> CreateMatches(FuzzyScore? score)
    {
        var res = new List<FilterMatch>();
        if (score is null)
        {
            return res;
        }

        var wordPos = score.WordStart;
        for (var i = score.Matches.Length - 1; i >= 0; i--)
        {
            var pos = score.Matches[i] + wordPos;
            if (res.Count > 0 && res[^1].End == pos)
            {
                res[^1] = res[^1] with { End = pos + 1 };
            }
            else
            {
                res.Add(new FilterMatch(pos, pos + 1));
            }
        }
        return res;
    }

    private const int MaxLen = 128;

    private enum Arrow
    {
        Diag = 1,
        Left = 2,
        LeftLeft = 3,
    }

    // upstream's module-level tables, one set per thread
    private sealed class Tables
    {
        public readonly int[] MinWordMatchPos = new int[2 * MaxLen + 1]; // min word position for a certain pattern position
        public readonly int[] MaxWordMatchPos = new int[2 * MaxLen + 1]; // max word position for a certain pattern position
        public readonly int[,] Diag = new int[MaxLen + 1, MaxLen + 1]; // the length of a contiguous diagonal match
        public readonly int[,] Table = new int[MaxLen + 1, MaxLen + 1];
        public readonly Arrow[,] Arrows = new Arrow[MaxLen + 1, MaxLen + 1];
    }

    [ThreadStatic]
    private static Tables? t_tables;

    private static bool IsSeparatorAtPos(string value, int index)
    {
        if (index < 0 || index >= value.Length)
        {
            return false;
        }

        var code = CodePointAt(value, index);
        switch (code)
        {
            case '_':
            case '-':
            case '.':
            case ' ':
            case '/':
            case '\\':
            case '\'':
            case '"':
            case ':':
            case '$':
            case '<':
            case '>':
            case '(':
            case ')':
            case '[':
            case ']':
            case '{':
            case '}':
                return true;
            default:
                return Strings.IsEmojiImprecise(code);
        }
    }

    private static int CodePointAt(string value, int index)
    {
        // JavaScript's codePointAt: a lone surrogate is its own code
        var c = value[index];
        if (char.IsHighSurrogate(c) && index + 1 < value.Length && char.IsLowSurrogate(value[index + 1]))
        {
            return char.ConvertToUtf32(c, value[index + 1]);
        }
        return c;
    }

    private static bool IsWhitespaceAtPos(string value, int index)
    {
        if (index < 0 || index >= value.Length)
        {
            return false;
        }
        return value[index] is ' ' or '\t';
    }

    private static bool IsUpperCaseAtPos(int pos, string word, string wordLow) => word[pos] != wordLow[pos];

    public static bool IsPatternInWord(string patternLow, int patternPos, int patternLen, string wordLow, int wordPos, int wordLen) =>
        IsPatternInWord(null, patternLow, patternPos, patternLen, wordLow, wordPos, wordLen);

    private static bool IsPatternInWord(Tables? fillMinWordPos, string patternLow, int patternPos, int patternLen, string wordLow, int wordPos, int wordLen)
    {
        while (patternPos < patternLen && wordPos < wordLen)
        {
            if (patternLow[patternPos] == wordLow[wordPos])
            {
                if (fillMinWordPos is not null)
                {
                    // remember the min word position for each pattern position
                    fillMinWordPos.MinWordMatchPos[patternPos] = wordPos;
                }
                patternPos += 1;
            }
            wordPos += 1;
        }
        return patternPos == patternLen; // pattern must be exhausted
    }

    /// <summary>
    /// Scores how well <paramref name="pattern"/> (from <paramref name="patternStart"/>) matches
    /// <paramref name="word"/> (from <paramref name="wordStart"/>); null when it does not.
    /// </summary>
    public static FuzzyScore? FuzzyScore(string pattern, string patternLow, int patternStart, string word, string wordLow, int wordStart, FuzzyScoreOptions? options = null)
    {
        options ??= FuzzyScoreOptions.Default;
        var patternLen = pattern.Length > MaxLen ? MaxLen : pattern.Length;
        var wordLen = word.Length > MaxLen ? MaxLen : word.Length;

        if (patternStart >= patternLen || wordStart >= wordLen || (patternLen - patternStart) > (wordLen - wordStart))
        {
            return null;
        }

        var t = t_tables ??= new Tables();

        // Run a simple check if the characters of pattern occur (in order) at all in word. If that isn't the case we
        // stop because no match will be possible
        if (!IsPatternInWord(t, patternLow, patternStart, patternLen, wordLow, wordStart, wordLen))
        {
            return null;
        }

        // Find the max matching word position for each pattern position
        // NOTE: the min matching word position was filled in above, in the `IsPatternInWord` call
        FillInMaxWordMatchPos(t, patternLen, wordLen, patternStart, wordStart, patternLow, wordLow);

        int row;
        int column = 1;
        int patternPos;
        int wordPos;

        var hasStrongFirstMatch = false;
        var table = t.Table;
        var diag = t.Diag;
        var arrows = t.Arrows;

        // There will be a match, fill in tables
        for (row = 1, patternPos = patternStart; patternPos < patternLen; row++, patternPos++)
        {
            // Reduce search space to possible matching word positions and to possible access from next row
            var minWordMatchPos = t.MinWordMatchPos[patternPos];
            var maxWordMatchPos = t.MaxWordMatchPos[patternPos];
            var nextMaxWordMatchPos = patternPos + 1 < patternLen ? t.MaxWordMatchPos[patternPos + 1] : wordLen;

            for (column = minWordMatchPos - wordStart + 1, wordPos = minWordMatchPos; wordPos < nextMaxWordMatchPos; column++, wordPos++)
            {
                var score = int.MinValue;
                var canComeDiag = false;

                if (wordPos <= maxWordMatchPos)
                {
                    score = DoScore(
                        pattern, patternLow, patternPos, patternStart,
                        word, wordLow, wordPos, wordLen, wordStart,
                        diag[row - 1, column - 1] == 0,
                        ref hasStrongFirstMatch);
                }

                var diagScore = 0;
                if (score != int.MinValue)
                {
                    canComeDiag = true;
                    diagScore = score + table[row - 1, column - 1];
                }

                var canComeLeft = wordPos > minWordMatchPos;
                var leftScore = canComeLeft ? table[row, column - 1] + (diag[row, column - 1] > 0 ? -5 : 0) : 0; // penalty for a gap start

                var canComeLeftLeft = wordPos > minWordMatchPos + 1 && diag[row, column - 1] > 0;
                var leftLeftScore = canComeLeftLeft ? table[row, column - 2] + (diag[row, column - 2] > 0 ? -5 : 0) : 0; // penalty for a gap start

                if (canComeLeftLeft && (!canComeLeft || leftLeftScore >= leftScore) && (!canComeDiag || leftLeftScore >= diagScore))
                {
                    // always prefer choosing left left to jump over a diagonal because that means a match is earlier in the word
                    table[row, column] = leftLeftScore;
                    arrows[row, column] = Arrow.LeftLeft;
                    diag[row, column] = 0;
                }
                else if (canComeLeft && (!canComeDiag || leftScore >= diagScore))
                {
                    // always prefer choosing left since that means a match is earlier in the word
                    table[row, column] = leftScore;
                    arrows[row, column] = Arrow.Left;
                    diag[row, column] = 0;
                }
                else if (canComeDiag)
                {
                    table[row, column] = diagScore;
                    arrows[row, column] = Arrow.Diag;
                    diag[row, column] = diag[row - 1, column - 1] + 1;
                }
                else
                {
                    throw new InvalidOperationException("not possible");
                }
            }
        }

        if (!hasStrongFirstMatch && !options.FirstMatchCanBeWeak)
        {
            return null;
        }

        row--;
        column--;

        var resultScore = table[row, column];
        var matches = new List<int>(patternLen - patternStart);

        var backwardsDiagLength = 0;
        var maxMatchColumn = 0;

        while (row >= 1)
        {
            // Find the column where we go diagonally up
            var diagColumn = column;
            do
            {
                var arrow = arrows[row, diagColumn];
                if (arrow == Arrow.LeftLeft)
                {
                    diagColumn -= 2;
                }
                else if (arrow == Arrow.Left)
                {
                    diagColumn -= 1;
                }
                else
                {
                    // found the diagonal
                    break;
                }
            }
            while (diagColumn >= 1);

            // Overturn the "forwards" decision if keeping the "backwards" diagonal would give a better match
            if (backwardsDiagLength > 1 // only if we would have a contiguous match of 3 characters
                && patternLow[patternStart + row - 1] == wordLow[wordStart + column - 1] // only if we can do a contiguous match diagonally
                && !IsUpperCaseAtPos(diagColumn + wordStart - 1, word, wordLow) // only if the forwards chose diagonal is not an uppercase
                && backwardsDiagLength + 1 > diag[row, diagColumn]) // only if our contiguous match would be longer than the "forwards" contiguous match
            {
                diagColumn = column;
            }

            if (diagColumn == column)
            {
                // this is a contiguous match
                backwardsDiagLength++;
            }
            else
            {
                backwardsDiagLength = 1;
            }

            if (maxMatchColumn == 0)
            {
                // remember the last matched column
                maxMatchColumn = diagColumn;
            }

            row--;
            column = diagColumn - 1;
            matches.Add(column);
        }

        if (wordLen - wordStart == patternLen && options.BoostFullMatch)
        {
            // the word matches the pattern with all characters! giving the score a total match boost (to come up
            // ahead other words)
            resultScore += 2;
        }

        // Add 1 penalty for each skipped character in the word
        var skippedCharsCount = maxMatchColumn - patternLen;
        resultScore -= skippedCharsCount;

        return new FuzzyScore(resultScore, wordStart, [.. matches]);
    }

    private static void FillInMaxWordMatchPos(Tables t, int patternLen, int wordLen, int patternStart, int wordStart, string patternLow, string wordLow)
    {
        var patternPos = patternLen - 1;
        var wordPos = wordLen - 1;
        while (patternPos >= patternStart && wordPos >= wordStart)
        {
            if (patternLow[patternPos] == wordLow[wordPos])
            {
                t.MaxWordMatchPos[patternPos] = wordPos;
                patternPos--;
            }
            wordPos--;
        }
    }

    private static int DoScore(
        string pattern, string patternLow, int patternPos, int patternStart,
        string word, string wordLow, int wordPos, int wordLen, int wordStart,
        bool newMatchStart,
        ref bool outFirstMatchStrong)
    {
        if (patternLow[patternPos] != wordLow[wordPos])
        {
            return int.MinValue;
        }

        var score = 1;
        var isGapLocation = false;
        if (wordPos == patternPos - patternStart)
        {
            // common prefix: `foobar <-> foobaz`
            //                            ^^^^^
            score = pattern[patternPos] == word[wordPos] ? 7 : 5;
        }
        else if (IsUpperCaseAtPos(wordPos, word, wordLow) && (wordPos == 0 || !IsUpperCaseAtPos(wordPos - 1, word, wordLow)))
        {
            // hitting upper-case: `foo <-> forOthers`
            //                              ^^ ^
            score = pattern[patternPos] == word[wordPos] ? 7 : 5;
            isGapLocation = true;
        }
        else if (IsSeparatorAtPos(wordLow, wordPos) && (wordPos == 0 || !IsSeparatorAtPos(wordLow, wordPos - 1)))
        {
            // hitting a separator: `. <-> foo.bar`
            //                                ^
            score = 5;
        }
        else if (IsSeparatorAtPos(wordLow, wordPos - 1) || IsWhitespaceAtPos(wordLow, wordPos - 1))
        {
            // post separator: `foo <-> bar_foo`
            //                              ^^^
            score = 5;
            isGapLocation = true;
        }

        if (score > 1 && patternPos == patternStart)
        {
            outFirstMatchStrong = true;
        }

        if (!isGapLocation)
        {
            isGapLocation = IsUpperCaseAtPos(wordPos, word, wordLow) || IsSeparatorAtPos(wordLow, wordPos - 1) || IsWhitespaceAtPos(wordLow, wordPos - 1);
        }

        if (patternPos == patternStart)
        {
            // first character in pattern
            if (wordPos > wordStart)
            {
                // the first pattern character would match a word character that is not at the word start so
                // introduce a penalty to account for the gap preceding this match
                score -= isGapLocation ? 3 : 5;
            }
        }
        else
        {
            if (newMatchStart)
            {
                // this would be the beginning of a new match (i.e. there would be a gap before this location)
                score += isGapLocation ? 2 : 0;
            }
            else
            {
                // this is part of a contiguous match, so give it a slight bonus, but do so only if it would not be a
                // preferred gap location
                score += isGapLocation ? 0 : 1;
            }
        }

        if (wordPos + 1 == wordLen)
        {
            // we always penalize gaps, but this gives unfair advantages to a match that would match the last character
            // in the word so pretend there is a gap after the last character in the word to normalize things
            score -= isGapLocation ? 3 : 5;
        }

        return score;
    }

    // ---- graceful

    public static FuzzyScore? FuzzyScoreGracefulAggressive(string pattern, string lowPattern, int patternPos, string word, string lowWord, int wordPos, FuzzyScoreOptions? options = null) =>
        FuzzyScoreWithPermutations(pattern, lowPattern, patternPos, word, lowWord, wordPos, aggressive: true, options);

    public static FuzzyScore? FuzzyScoreGraceful(string pattern, string lowPattern, int patternPos, string word, string lowWord, int wordPos, FuzzyScoreOptions? options = null) =>
        FuzzyScoreWithPermutations(pattern, lowPattern, patternPos, word, lowWord, wordPos, aggressive: false, options);

    private static FuzzyScore? FuzzyScoreWithPermutations(string pattern, string lowPattern, int patternPos, string word, string lowWord, int wordPos, bool aggressive, FuzzyScoreOptions? options)
    {
        var top = FuzzyScore(pattern, lowPattern, patternPos, word, lowWord, wordPos, options);

        if (top is not null && !aggressive)
        {
            // when using the original pattern yield a result we return it unless we are aggressive and try to find a
            // better alignment, e.g. `cno` -> `^co^ns^ole` or `^c^o^nsole`.
            return top;
        }

        if (pattern.Length >= 3)
        {
            // When the pattern is long enough then try a few (max 7) permutations of the pattern to find a better
            // match. The permutations only swap neighbouring characters, e.g `cnoso` becomes `conso`, `cnsoo`, `cnoos`.
            var tries = Math.Min(7, pattern.Length - 1);
            for (var movingPatternPos = patternPos + 1; movingPatternPos < tries; movingPatternPos++)
            {
                var newPattern = NextTypoPermutation(pattern, movingPatternPos);
                if (newPattern is not null)
                {
                    var candidate = FuzzyScore(newPattern, newPattern.ToLowerInvariant(), patternPos, word, lowWord, wordPos, options);
                    if (candidate is not null)
                    {
                        candidate = candidate with { Score = candidate.Score - 3 }; // permutation penalty
                        if (top is null || candidate.Score > top.Score)
                        {
                            top = candidate;
                        }
                    }
                }
            }
        }

        return top;
    }

    private static string? NextTypoPermutation(string pattern, int patternPos)
    {
        if (patternPos + 1 >= pattern.Length)
        {
            return null;
        }

        var swap1 = pattern[patternPos];
        var swap2 = pattern[patternPos + 1];
        if (swap1 == swap2)
        {
            return null;
        }

        return string.Concat(pattern.AsSpan(0, patternPos), new string([swap2, swap1]), pattern.AsSpan(patternPos + 2));
    }

    // ---- helpers from strings.ts and normalization.ts

    /// <summary>Escapes a string for a regular expression, turning <c>*</c> into <c>.*</c> (upstream's convertSimple2RegExpPattern).</summary>
    public static string ConvertSimple2RegExpPattern(string pattern)
    {
        var sb = new StringBuilder(pattern.Length + 8);
        foreach (var c in pattern)
        {
            if (c == '*')
            {
                sb.Append(".*");
            }
            else if ("\\{}()[]^$.|?+-".Contains(c))
            {
                sb.Append('\\').Append(c);
            }
            else
            {
                sb.Append(c);
            }
        }
        return sb.ToString();
    }

    /// <summary>
    /// The string in Unicode base form (NFD, accents removed, lower case); only lower cased when removing the accents
    /// would change the length (upstream's tryNormalizeToBase).
    /// </summary>
    public static string TryNormalizeToBase(string str)
    {
        var normalized = str;
        foreach (var c in str)
        {
            if (c > 0x80)
            {
                normalized = str.Normalize(NormalizationForm.FormD);
                break;
            }
        }

        var sb = new StringBuilder(normalized.Length);
        foreach (var c in normalized)
        {
            if (c is < '̀' or > 'ͯ')
            {
                sb.Append(c);
            }
        }

        var noAccents = sb.ToString();
        return (noAccents.Length == str.Length ? noAccents : str).ToLower(CultureInfo.InvariantCulture);
    }
}
