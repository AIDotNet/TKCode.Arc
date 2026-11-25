// Ported from vscode (src/vs/editor/common/model/textModelSearch.ts: SearchParams, commit b57d5c9e), MIT license.

namespace TKCode.Arc.Editor.Model;

/// <summary>A find request before it is compiled: what to search for and how.</summary>
internal sealed class SearchParams
{
    internal SearchParams(string searchString, bool isRegex, bool matchCase, string? wordSeparators)
    {
        SearchString = searchString;
        IsRegex = isRegex;
        MatchCase = matchCase;
        WordSeparators = wordSeparators;
    }

    internal string SearchString { get; }

    internal bool IsRegex { get; }

    internal bool MatchCase { get; }

    /// <summary>The characters that separate words, or null to ignore word boundaries.</summary>
    internal string? WordSeparators { get; }

    /// <summary>Compiles the request, or returns null when it cannot be used (empty or invalid pattern).</summary>
    internal SearchData? ParseSearchRequest()
    {
        if (SearchString.Length == 0)
        {
            return null;
        }

        bool multiline;
        if (IsRegex)
        {
            multiline = TextModelSearch.IsMultilineRegexSource(SearchString);
        }
        else
        {
            multiline = SearchString.IndexOf('\n') >= 0;
        }

        JsRegex regex;
        try
        {
            regex = JsRegex.Create(SearchString, IsRegex, MatchCase, multiline);
        }
        catch (ArgumentException)
        {
            return null;
        }

        var canUseSimpleSearch = !IsRegex && !multiline;
        if (canUseSimpleSearch
            && !string.Equals(SearchString.ToLowerInvariant(), SearchString.ToUpperInvariant(), StringComparison.Ordinal))
        {
            // casing might make a difference
            canUseSimpleSearch = MatchCase;
        }

        return new SearchData(
            regex,
            WordSeparators is not null ? WordCharacterClassifier.GetMapForWordSeparators(WordSeparators) : null,
            canUseSimpleSearch ? SearchString : null);
    }
}
