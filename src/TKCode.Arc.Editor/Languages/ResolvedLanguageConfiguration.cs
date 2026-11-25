// Ported from vscode (src/vs/editor/common/languages/languageConfigurationRegistry.ts: ResolvedLanguageConfiguration
// and ICommentsConfiguration, commit b57d5c9e), MIT license.
// Differences from upstream:
//  - ICommentsConfiguration is the record CommentsConfiguration;
//  - the lazily created brackets and electric character support are published with Interlocked, because one resolved
//    configuration is shared by every model of the language and may be read off the UI thread; both are immutable
//    once built, so a lost race only builds one twice;
//  - a .NET Regex has no global flag, so GetWordDefinition returns the word definition as it is.

using System.Text.RegularExpressions;
using TKCode.Arc.Editor.Config;

namespace TKCode.Arc.Editor.Languages;

/// <summary>Interface used to support insertion of mode specific comments.</summary>
internal sealed record CommentsConfiguration
{
    public string? LineCommentToken { get; init; }

    public bool? LineCommentNoIndent { get; init; }

    public string? BlockCommentStartToken { get; init; }

    public string? BlockCommentEndToken { get; init; }
}

/// <summary>A language's configuration with its supports built. Immutable.</summary>
internal sealed class ResolvedLanguageConfiguration
{
    private RichEditBrackets? _brackets;
    private BracketElectricCharacterSupport? _electricCharacter;
    private readonly OnEnterSupport? _onEnterSupport;

    public ResolvedLanguageConfiguration(string languageId, LanguageConfiguration underlyingConfig)
    {
        LanguageId = languageId;
        UnderlyingConfig = underlyingConfig;

        _brackets = null;
        _electricCharacter = null;
        _onEnterSupport =
            UnderlyingConfig.Brackets is not null
            || UnderlyingConfig.IndentationRules is not null
            || UnderlyingConfig.OnEnterRules is not null
                ? new OnEnterSupport(UnderlyingConfig)
                : null;
        Comments = HandleComments(UnderlyingConfig);
        CharacterPair = new CharacterPairSupport(UnderlyingConfig);

        WordDefinition = UnderlyingConfig.WordPattern ?? WordHelper.DefaultWordRegExp;
        IndentationRules = UnderlyingConfig.IndentationRules;
        if (UnderlyingConfig.IndentationRules is { } indentationRules)
        {
            IndentRulesSupport = new IndentRulesSupport(indentationRules);
        }
        else
        {
            IndentRulesSupport = null;
        }
        FoldingRules = UnderlyingConfig.Folding ?? new FoldingRules();

        BracketsNew = new LanguageBracketsConfiguration(languageId, UnderlyingConfig);
    }

    public string LanguageId { get; }

    public LanguageConfiguration UnderlyingConfig { get; }

    public CommentsConfiguration? Comments { get; }

    public CharacterPairSupport CharacterPair { get; }

    public Regex WordDefinition { get; }

    public IndentRulesSupport? IndentRulesSupport { get; }

    public IndentationRule? IndentationRules { get; }

    public FoldingRules FoldingRules { get; }

    public LanguageBracketsConfiguration BracketsNew { get; }

    public Regex GetWordDefinition()
    {
        return WordHelper.EnsureValidWordDefinition(WordDefinition);
    }

    public RichEditBrackets? Brackets
    {
        get
        {
            if (_brackets is null && UnderlyingConfig.Brackets is { } brackets)
            {
                Interlocked.CompareExchange(ref _brackets, new RichEditBrackets(LanguageId, brackets), null);
            }
            return _brackets;
        }
    }

    public BracketElectricCharacterSupport ElectricCharacter
    {
        get
        {
            if (_electricCharacter is null)
            {
                Interlocked.CompareExchange(ref _electricCharacter, new BracketElectricCharacterSupport(Brackets), null);
            }
            return _electricCharacter;
        }
    }

    public EnterAction? OnEnter(
        EditorAutoIndentStrategy autoIndent,
        string previousLineText,
        string beforeEnterText,
        string afterEnterText)
    {
        if (_onEnterSupport is null)
        {
            return null;
        }
        return _onEnterSupport.OnEnter(
            autoIndent,
            previousLineText,
            beforeEnterText,
            afterEnterText);
    }

    public AutoClosingPairs GetAutoClosingPairs()
    {
        return new AutoClosingPairs(CharacterPair.GetAutoClosingPairs());
    }

    public string GetAutoCloseBeforeSet(bool forQuotes)
    {
        return CharacterPair.GetAutoCloseBeforeSet(forQuotes);
    }

    public IReadOnlyList<IAutoClosingPair> GetSurroundingPairs()
    {
        return CharacterPair.GetSurroundingPairs();
    }

    private static CommentsConfiguration? HandleComments(LanguageConfiguration conf)
    {
        var commentRule = conf.Comments;
        if (commentRule is null)
        {
            return null;
        }

        // comment configuration
        var comments = new CommentsConfiguration();

        if (commentRule.LineComment is { } lineComment)
        {
            comments = comments with
            {
                LineCommentToken = lineComment.Comment,
                LineCommentNoIndent = lineComment.NoIndent,
            };
        }
        if (commentRule.BlockComment is { } blockComment)
        {
            var (blockStart, blockEnd) = blockComment;
            comments = comments with
            {
                BlockCommentStartToken = blockStart,
                BlockCommentEndToken = blockEnd,
            };
        }

        return comments;
    }
}
