// Ported from vscode (src/vs/editor/common/model/bracketPairsTextModelPart/bracketPairsTree/brackets.ts, commit
// b57d5c9e), MIT license.
//
// Differences: the regular expression is a .NET Regex with ECMAScript (\b is ASCII-based, as in JavaScript) and
// IgnoreCase; it has no global state (upstream resets lastIndex before each use).

using System.Text.RegularExpressions;
using TKCode.Arc.Editor.Languages;
using static TKCode.Arc.Editor.Model.BracketPairs.LengthHelpers;
using Token = TKCode.Arc.Editor.Model.BracketPairs.Token;

namespace TKCode.Arc.Editor.Model.BracketPairs;

/// <summary>The bracket tokens of one language, by their text in lower case.</summary>
internal sealed class BracketTokens(Dictionary<string, Token> map)
{
    private static readonly Regex s_startsWithWord = new(@"^[\w ]+", RegexOptions.ECMAScript | RegexOptions.CultureInvariant);
    private static readonly Regex s_endsWithWord = new(@"[\w ]+$", RegexOptions.ECMAScript | RegexOptions.CultureInvariant);

    private bool _hasRegExp;
    private Regex? _regExpGlobal;

    public static BracketTokens CreateFromLanguage(ResolvedLanguageConfiguration configuration, DenseKeyProvider<string> denseKeyProvider)
    {
        int GetId(BracketKindBase bracketInfo) => denseKeyProvider.GetKey($"{bracketInfo.LanguageId}:::{bracketInfo.BracketText}");

        var map = new Dictionary<string, Token>(StringComparer.Ordinal);
        foreach (var openingBracket in configuration.BracketsNew.OpeningBrackets)
        {
            var length = ToLength(0, openingBracket.BracketText.Length);
            var openingTextId = GetId(openingBracket);
            var bracketIds = SmallImmutableSet<int>.GetEmpty().Add(openingTextId, IdentityKeyProvider.Instance);
            map[openingBracket.BracketText] = new Token(
                length,
                TokenKind.OpeningBracket,
                openingTextId,
                bracketIds,
                BracketAstNode.Create(length, openingBracket, bracketIds));
        }

        foreach (var closingBracket in configuration.BracketsNew.ClosingBrackets)
        {
            var length = ToLength(0, closingBracket.BracketText.Length);
            var bracketIds = SmallImmutableSet<int>.GetEmpty();
            var closingBrackets = closingBracket.GetOpeningBrackets();
            foreach (var bracket in closingBrackets)
            {
                bracketIds = bracketIds.Add(GetId(bracket), IdentityKeyProvider.Instance);
            }

            map[closingBracket.BracketText] = new Token(
                length,
                TokenKind.ClosingBracket,
                GetId(closingBrackets[0]),
                bracketIds,
                BracketAstNode.Create(length, closingBracket, bracketIds));
        }

        return new BracketTokens(map);
    }

    internal static Regex CreateRegex(string pattern) =>
        new(pattern, RegexOptions.ECMAScript | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, JsRegex.MatchTimeout);

    public string? GetRegExpStr()
    {
        if (IsEmpty)
        {
            return null;
        }

        var keys = map.Keys.ToList();
        keys.Sort(StringComparer.Ordinal);
        keys.Reverse();
        return string.Join("|", keys.Select(PrepareBracketForRegExp));
    }

    /// <summary>Returns null if there is no such regexp (because there are no brackets).</summary>
    public Regex? RegExpGlobal
    {
        get
        {
            if (!_hasRegExp)
            {
                var regExpStr = GetRegExpStr();
                _regExpGlobal = regExpStr is not null ? CreateRegex(regExpStr) : null;
                _hasRegExp = true;
            }

            return _regExpGlobal;
        }
    }

    public Token? GetToken(string value) => map.GetValueOrDefault(value.ToLowerInvariant());

    public string? FindClosingTokenText(SmallImmutableSet<int> openingBracketIds)
    {
        foreach (var (closingText, info) in map)
        {
            if (info.Kind == TokenKind.ClosingBracket && info.BracketIds.Intersects(openingBracketIds))
            {
                return closingText;
            }
        }

        return null;
    }

    public bool IsEmpty => map.Count == 0;

    private static string PrepareBracketForRegExp(string str)
    {
        var escaped = SearchHelpers.EscapeRegExpCharacters(str);
        // These bracket pair delimiters start or end with letters
        // see https://github.com/microsoft/vscode/issues/132162 https://github.com/microsoft/vscode/issues/150440
        if (s_startsWithWord.IsMatch(str))
        {
            escaped = "\\b" + escaped;
        }

        if (s_endsWithWord.IsMatch(str))
        {
            escaped += "\\b";
        }

        return escaped;
    }
}

/// <summary>The bracket tokens of every language, created on first use.</summary>
internal sealed class LanguageAgnosticBracketTokens(DenseKeyProvider<string> denseKeyProvider, Func<string, ResolvedLanguageConfiguration> getLanguageConfiguration)
{
    private readonly Dictionary<string, BracketTokens> _languageIdToBracketTokens = new(StringComparer.Ordinal);

    public bool DidLanguageChange(string languageId) =>
        // Report a change whenever the language configuration updates.
        _languageIdToBracketTokens.ContainsKey(languageId);

    public BracketTokens GetSingleLanguageBracketTokens(string languageId)
    {
        if (!_languageIdToBracketTokens.TryGetValue(languageId, out var singleLanguageBracketTokens))
        {
            singleLanguageBracketTokens = BracketTokens.CreateFromLanguage(getLanguageConfiguration(languageId), denseKeyProvider);
            _languageIdToBracketTokens[languageId] = singleLanguageBracketTokens;
        }

        return singleLanguageBracketTokens;
    }

    public Token? GetToken(string value, string languageId) => GetSingleLanguageBracketTokens(languageId).GetToken(value);
}
