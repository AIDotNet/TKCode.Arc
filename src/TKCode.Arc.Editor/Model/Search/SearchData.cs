// Ported from vscode (src/vs/editor/common/model.ts: SearchData, commit b57d5c9e), MIT license.

namespace TKCode.Arc.Editor.Model;

/// <summary>What a parsed search request runs: the regular expression, optional word separators and simple search.</summary>
internal sealed class SearchData
{
    internal SearchData(JsRegex regex, WordCharacterClassifier? wordSeparators, string? simpleSearch)
    {
        Regex = regex;
        WordSeparators = wordSeparators;
        SimpleSearch = simpleSearch;
    }

    /// <summary>The regular expression to search for. Always defined.</summary>
    internal JsRegex Regex { get; }

    /// <summary>The word separator classifier, when the search must match whole words.</summary>
    internal WordCharacterClassifier? WordSeparators { get; }

    /// <summary>The simple string to search for (ordinal), when the request allows it.</summary>
    internal string? SimpleSearch { get; }
}
