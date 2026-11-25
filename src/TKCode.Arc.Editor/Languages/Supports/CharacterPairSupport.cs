// Ported from vscode (src/vs/editor/common/languages/supports/characterPair.ts, commit b57d5c9e), MIT license.
// Differences from upstream: the pair lists are returned as read-only lists.

namespace TKCode.Arc.Editor.Languages;

/// <summary>The auto closing and surrounding pairs of a language.</summary>
internal sealed class CharacterPairSupport
{
    public const string DefaultAutocloseBeforeLanguageDefinedQuotes = ";:.,=}])> \n\t";
    public const string DefaultAutocloseBeforeLanguageDefinedBrackets = "'\"`;:.,=}])> \n\t";
    public const string DefaultAutocloseBeforeWhitespace = " \n\t";

    private readonly List<StandardAutoClosingPairConditional> _autoClosingPairs;
    private readonly IReadOnlyList<IAutoClosingPair> _surroundingPairs;
    private readonly string _autoCloseBeforeForQuotes;
    private readonly string _autoCloseBeforeForBrackets;

    public CharacterPairSupport(LanguageConfiguration config)
    {
        if (config.AutoClosingPairs is { } autoClosingPairs)
        {
            _autoClosingPairs = autoClosingPairs.Select(el => new StandardAutoClosingPairConditional(el)).ToList();
        }
        else if (config.Brackets is { } brackets)
        {
            _autoClosingPairs = brackets.Select(b => new StandardAutoClosingPairConditional(new AutoClosingPair(b.Open, b.Close))).ToList();
        }
        else
        {
            _autoClosingPairs = [];
        }

        if (config.ElectricCharacterSupport?.DocComment is { } docComment)
        {
            // IDocComment is legacy, only partially supported
            _autoClosingPairs.Add(new StandardAutoClosingPairConditional(new AutoClosingPair(docComment.Open, docComment.Close ?? "")));
        }

        _autoCloseBeforeForQuotes = config.AutoCloseBefore ?? DefaultAutocloseBeforeLanguageDefinedQuotes;
        _autoCloseBeforeForBrackets = config.AutoCloseBefore ?? DefaultAutocloseBeforeLanguageDefinedBrackets;

        _surroundingPairs = config.SurroundingPairs ?? _autoClosingPairs;
    }

    public IReadOnlyList<StandardAutoClosingPairConditional> GetAutoClosingPairs()
    {
        return _autoClosingPairs;
    }

    public string GetAutoCloseBeforeSet(bool forQuotes)
    {
        return forQuotes ? _autoCloseBeforeForQuotes : _autoCloseBeforeForBrackets;
    }

    public IReadOnlyList<IAutoClosingPair> GetSurroundingPairs()
    {
        return _surroundingPairs;
    }
}
