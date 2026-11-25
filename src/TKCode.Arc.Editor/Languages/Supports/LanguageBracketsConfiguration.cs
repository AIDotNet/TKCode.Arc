// Ported from vscode (src/vs/editor/common/languages/supports/languageBracketsConfiguration.ts, commit b57d5c9e),
// MIT license.
// Differences from upstream:
//  - the ReadonlySets of bracket kinds are lists without duplicates (they keep insertion order, as a JavaScript Set
//    does); CachedFunction is an insertion-ordered dictionary inside the constructor;
//  - BracketKindBase.config is internal, because ClosingBracketKind reads it from an OpeningBracketKind (upstream
//    reads the protected member with an index expression);
//  - BracketKind (a union type) is the base class BracketKindBase;
//  - getBracketRegExp caches its expression per options (the configuration is immutable): building a .NET Regex is
//    far dearer than a JavaScript RegExp, above all in the browser's interpreter, and auto-indent asks for it on Enter.

using System.Collections.Concurrent;
using System.Text.RegularExpressions;

namespace TKCode.Arc.Editor.Languages;

/// <summary>
/// Captures all bracket related configurations for a single language.
/// Immutable.
/// </summary>
internal sealed class LanguageBracketsConfiguration
{
    private readonly Dictionary<string, OpeningBracketKind> _openingBrackets = new(StringComparer.Ordinal);
    private readonly Dictionary<string, ClosingBracketKind> _closingBrackets = new(StringComparer.Ordinal);
    private readonly List<string> _openingBracketsOrder = new();
    private readonly List<string> _closingBracketsOrder = new();
    private readonly ConcurrentDictionary<RegExpOptions, Regex> _bracketRegExps = new();

    public LanguageBracketsConfiguration(string languageId, LanguageConfiguration config)
    {
        LanguageId = languageId;

        var bracketPairs = config.Brackets is { } brackets ? FilterValidBrackets(brackets) : [];

        var openingBracketInfos = new Dictionary<string, (OpeningBracketKind Info, List<ClosingBracketKind> Closing)>(StringComparer.Ordinal);
        var closingBracketInfos = new Dictionary<string, (ClosingBracketKind Info, List<OpeningBracketKind> Opening, List<OpeningBracketKind> OpeningColorized)>(StringComparer.Ordinal);

        (OpeningBracketKind Info, List<ClosingBracketKind> Closing) GetOpening(string bracket)
        {
            if (!openingBracketInfos.TryGetValue(bracket, out var value))
            {
                var closing = new List<ClosingBracketKind>();
                value = (new OpeningBracketKind(this, bracket, closing), closing);
                openingBracketInfos[bracket] = value;
                _openingBracketsOrder.Add(bracket);
            }
            return value;
        }

        (ClosingBracketKind Info, List<OpeningBracketKind> Opening, List<OpeningBracketKind> OpeningColorized) GetClosing(string bracket)
        {
            if (!closingBracketInfos.TryGetValue(bracket, out var value))
            {
                var opening = new List<OpeningBracketKind>();
                var openingColorized = new List<OpeningBracketKind>();
                value = (new ClosingBracketKind(this, bracket, opening, openingColorized), opening, openingColorized);
                closingBracketInfos[bracket] = value;
                _closingBracketsOrder.Add(bracket);
            }
            return value;
        }

        foreach (var (open, close) in bracketPairs)
        {
            var opening = GetOpening(open);
            var closing = GetClosing(close);

            AddToSet(opening.Closing, closing.Info);
            AddToSet(closing.Opening, opening.Info);
        }

        // Treat colorized brackets as brackets, and mark them as colorized.
        var colorizedBracketPairs = config.ColorizedBracketPairs is { } colorized
            ? FilterValidBrackets(colorized)
            // If not configured: Take all brackets except `<` ... `>`
            // Many languages set < ... > as bracket pair, even though they also use it as comparison operator.
            // This leads to problems when colorizing this bracket, so we exclude it if not explicitly configured otherwise.
            // https://github.com/microsoft/vscode/issues/132476
            : bracketPairs.Where(p => !(p.Open == "<" && p.Close == ">")).ToList();
        foreach (var (open, close) in colorizedBracketPairs)
        {
            var opening = GetOpening(open);
            var closing = GetClosing(close);

            AddToSet(opening.Closing, closing.Info);
            AddToSet(closing.OpeningColorized, opening.Info);
            AddToSet(closing.Opening, opening.Info);
        }

        foreach (var (key, value) in openingBracketInfos)
        {
            _openingBrackets[key] = value.Info;
        }
        foreach (var (key, value) in closingBracketInfos)
        {
            _closingBrackets[key] = value.Info;
        }
    }

    public string LanguageId { get; }

    /// <summary>No two brackets have the same bracket text.</summary>
    public IReadOnlyList<OpeningBracketKind> OpeningBrackets => _openingBracketsOrder.Select(k => _openingBrackets[k]).ToList();

    /// <summary>No two brackets have the same bracket text.</summary>
    public IReadOnlyList<ClosingBracketKind> ClosingBrackets => _closingBracketsOrder.Select(k => _closingBrackets[k]).ToList();

    public OpeningBracketKind? GetOpeningBracketInfo(string bracketText)
    {
        return _openingBrackets.GetValueOrDefault(bracketText);
    }

    public ClosingBracketKind? GetClosingBracketInfo(string bracketText)
    {
        return _closingBrackets.GetValueOrDefault(bracketText);
    }

    public BracketKindBase? GetBracketInfo(string bracketText)
    {
        return (BracketKindBase?)GetOpeningBracketInfo(bracketText) ?? GetClosingBracketInfo(bracketText);
    }

    public Regex GetBracketRegExp(RegExpOptions options = default) =>
        _bracketRegExps.GetOrAdd(options, static (o, self) =>
            RichEditBrackets.CreateBracketOrRegExp(self._openingBracketsOrder.Concat(self._closingBracketsOrder).ToList(), o), this);

    private static List<CharacterPair> FilterValidBrackets(IReadOnlyList<CharacterPair> bracketPairs)
    {
        return bracketPairs.Where(p => p.Open != "" && p.Close != "").ToList();
    }

    private static void AddToSet<T>(List<T> set, T value)
        where T : class
    {
        if (!set.Contains(value))
        {
            set.Add(value);
        }
    }
}

/// <summary>An opening or closing bracket of a language.</summary>
internal abstract class BracketKindBase
{
    protected BracketKindBase(LanguageBracketsConfiguration config, string bracketText)
    {
        Config = config;
        BracketText = bracketText;
    }

    internal LanguageBracketsConfiguration Config { get; }

    public string BracketText { get; }

    public string LanguageId => Config.LanguageId;

    public abstract bool IsOpeningBracket { get; }
}

/// <summary>An opening bracket and the closing brackets that close it.</summary>
internal sealed class OpeningBracketKind : BracketKindBase
{
    public OpeningBracketKind(LanguageBracketsConfiguration config, string bracketText, IReadOnlyList<ClosingBracketKind> openedBrackets)
        : base(config, bracketText)
    {
        OpenedBrackets = openedBrackets;
    }

    public override bool IsOpeningBracket => true;

    public IReadOnlyList<ClosingBracketKind> OpenedBrackets { get; }
}

/// <summary>A closing bracket and the opening brackets it closes.</summary>
internal sealed class ClosingBracketKind : BracketKindBase
{
    private readonly IReadOnlyList<OpeningBracketKind> _openingColorizedBrackets;

    public ClosingBracketKind(
        LanguageBracketsConfiguration config,
        string bracketText,
        IReadOnlyList<OpeningBracketKind> openingBrackets,
        IReadOnlyList<OpeningBracketKind> openingColorizedBrackets)
        : base(config, bracketText)
    {
        OpeningBrackets = openingBrackets;
        _openingColorizedBrackets = openingColorizedBrackets;
    }

    public override bool IsOpeningBracket => false;

    /// <summary>Non empty array of all opening brackets this bracket closes.</summary>
    public IReadOnlyList<OpeningBracketKind> OpeningBrackets { get; }

    /// <summary>
    /// Checks if this bracket closes the given other bracket.
    /// If the bracket infos come from different configurations, this method will return false.
    /// </summary>
    public bool Closes(OpeningBracketKind other)
    {
        if (other.Config != Config)
        {
            return false;
        }
        return OpeningBrackets.Contains(other);
    }

    public bool ClosesColorized(OpeningBracketKind other)
    {
        if (other.Config != Config)
        {
            return false;
        }
        return _openingColorizedBrackets.Contains(other);
    }

    public IReadOnlyList<OpeningBracketKind> GetOpeningBrackets()
    {
        return OpeningBrackets.ToList();
    }
}
