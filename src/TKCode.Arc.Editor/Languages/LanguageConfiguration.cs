// Ported from vscode (src/vs/editor/common/languages/languageConfiguration.ts, commit b57d5c9e), MIT license.
// Differences from upstream:
//  - the configuration interfaces are immutable records with init properties; optional members are nullable;
//  - JavaScript RegExp members are .NET Regex (build them with RegExps.CreateRegExp to get JavaScript's \s, $ and .);
//    a Regex keeps no lastIndex, so upstream's resets of lastIndex have no equivalent;
//  - CommentRule.lineComment (`string | LineCommentConfig | null`) is a LineCommentConfig, which a string converts to
//    implicitly; an empty string converts to null, as upstream treats it as absent (falsy);
//  - CharacterPair (a [string, string] tuple upstream) is a record struct with Open and Close, which a (string, string)
//    tuple converts to implicitly;
//  - IAutoClosingPair and IAutoClosingPairConditional stay interfaces (StandardAutoClosingPairConditional is an
//    IAutoClosingPair too); AutoClosingPair and AutoClosingPairConditional are records implementing them;
//  - IDocComment is the record DocComment, and `__electricCharacterSupport` is ElectricCharacterSupport;
//  - the maps of AutoClosingPairs are keyed by one-character strings, as upstream's (an empty open or close gives "");
//  - ExplicitLanguageConfiguration (a TypeScript-only type) has no equivalent.

using System.Text.RegularExpressions;
using TKCode.Arc.Editor.Tokens;

namespace TKCode.Arc.Editor.Languages;

/// <summary>Configuration for line comments.</summary>
internal sealed record LineCommentConfig(string Comment)
{
    /// <summary>Whether the comment token should not be indented and placed at the first column. Defaults to false.</summary>
    public bool? NoIndent { get; init; }

    /// <summary>A line comment token alone, like <c>//</c>; an empty or null string means no line comment.</summary>
    public static implicit operator LineCommentConfig?(string? comment)
    {
        return string.IsNullOrEmpty(comment) ? null : new LineCommentConfig(comment);
    }
}

/// <summary>Describes how comments for a language work.</summary>
internal sealed record CommentRule
{
    /// <summary>The line comment token, like <c>// this is a comment</c>.</summary>
    public LineCommentConfig? LineComment { get; init; }

    /// <summary>The block comment character pair, like <c>/* block comment */</c>.</summary>
    public CharacterPair? BlockComment { get; init; }
}

/// <summary>
/// The language configuration interface defines the contract between extensions and
/// various editor features, like automatic bracket insertion, automatic indentation etc.
/// </summary>
internal sealed record LanguageConfiguration
{
    /// <summary>The language's comment settings.</summary>
    public CommentRule? Comments { get; init; }

    /// <summary>
    /// The language's brackets.
    /// This configuration implicitly affects pressing Enter around these brackets.
    /// </summary>
    public IReadOnlyList<CharacterPair>? Brackets { get; init; }

    /// <summary>
    /// The language's word definition.
    /// If the language supports Unicode identifiers (e.g. JavaScript), it is preferable
    /// to provide a word definition that uses exclusion of known separators.
    /// </summary>
    public Regex? WordPattern { get; init; }

    /// <summary>The language's indentation settings.</summary>
    public IndentationRule? IndentationRules { get; init; }

    /// <summary>The language's rules to be evaluated when pressing Enter.</summary>
    public IReadOnlyList<OnEnterRule>? OnEnterRules { get; init; }

    /// <summary>
    /// The language's auto closing pairs. The 'close' character is automatically inserted with the
    /// 'open' character is typed. If not set, the configured brackets will be used.
    /// </summary>
    public IReadOnlyList<IAutoClosingPairConditional>? AutoClosingPairs { get; init; }

    /// <summary>
    /// The language's surrounding pairs. When the 'open' character is typed on a selection, the
    /// selected string is surrounded by the open and close characters. If not set, the autoclosing pairs
    /// settings will be used.
    /// </summary>
    public IReadOnlyList<IAutoClosingPair>? SurroundingPairs { get; init; }

    /// <summary>
    /// Defines a list of bracket pairs that are colorized depending on their nesting level.
    /// If not set, the configured brackets will be used.
    /// </summary>
    public IReadOnlyList<CharacterPair>? ColorizedBracketPairs { get; init; }

    /// <summary>
    /// Defines what characters must be after the cursor for bracket or quote autoclosing to occur when using the
    /// 'languageDefined' autoclosing setting.
    /// This is typically the set of characters which can not start an expression, such as whitespace, closing
    /// brackets, non-unary operators, etc.
    /// </summary>
    public string? AutoCloseBefore { get; init; }

    /// <summary>The language's folding rules.</summary>
    public FoldingRules? Folding { get; init; }

    /// <summary><b>Deprecated</b> upstream (<c>__electricCharacterSupport</c>): will be replaced by a better API.</summary>
    public ElectricCharacterSupportOptions? ElectricCharacterSupport { get; init; }
}

/// <summary>The legacy electric character options (<c>__electricCharacterSupport</c>).</summary>
internal sealed record ElectricCharacterSupportOptions
{
    public DocComment? DocComment { get; init; }
}

/// <summary>Describes indentation rules for a language.</summary>
internal sealed record IndentationRule(Regex DecreaseIndentPattern, Regex IncreaseIndentPattern)
{
    /// <summary>
    /// If a line matches this pattern, then all the lines after it should be unindented once (until another rule
    /// matches).
    /// </summary>
    public Regex DecreaseIndentPattern { get; init; } = DecreaseIndentPattern;

    /// <summary>
    /// If a line matches this pattern, then all the lines after it should be indented once (until another rule
    /// matches).
    /// </summary>
    public Regex IncreaseIndentPattern { get; init; } = IncreaseIndentPattern;

    /// <summary>If a line matches this pattern, then <b>only the next line</b> after it should be indented once.</summary>
    public Regex? IndentNextLinePattern { get; init; }

    /// <summary>
    /// If a line matches this pattern, then its indentation should not be changed and it should not be evaluated
    /// against the other rules.
    /// </summary>
    public Regex? UnIndentedLinePattern { get; init; }
}

/// <summary>
/// Describes language specific folding markers such as '#region' and '#endregion'.
/// The start and end regexes will be tested against the contents of all lines and must be designed efficiently:
/// the regex should start with '^'.
/// </summary>
internal sealed record FoldingMarkers(Regex Start, Regex End);

/// <summary>Describes folding rules for a language.</summary>
internal sealed record FoldingRules
{
    /// <summary>
    /// Used by the indentation based strategy to decide whether empty lines belong to the previous or the next block.
    /// A language adheres to the off-side rule if blocks in that language are expressed by their indentation.
    /// If not set, <c>false</c> is used and empty lines belong to the previous block.
    /// </summary>
    public bool? OffSide { get; init; }

    /// <summary>Region markers used by the language.</summary>
    public FoldingMarkers? Markers { get; init; }
}

/// <summary>Describes a rule to be evaluated when pressing Enter.</summary>
internal sealed class OnEnterRule(Regex beforeText, EnterAction action)
{
    /// <summary>This rule will only execute if the text before the cursor matches this regular expression.</summary>
    public Regex BeforeText { get; init; } = beforeText;

    /// <summary>This rule will only execute if the text after the cursor matches this regular expression.</summary>
    public Regex? AfterText { get; init; }

    /// <summary>This rule will only execute if the text above the this line matches this regular expression.</summary>
    public Regex? PreviousLineText { get; init; }

    /// <summary>The action to execute.</summary>
    public EnterAction Action { get; init; } = action;
}

/// <summary>Definition of documentation comments (e.g. Javadoc/JSdoc).</summary>
internal sealed record DocComment(string Open)
{
    /// <summary>The string that starts a doc comment (e.g. '/**').</summary>
    public string Open { get; init; } = Open;

    /// <summary>The string that appears on the last line and closes the doc comment (e.g. ' */').</summary>
    public string? Close { get; init; }
}

/// <summary>A tuple of two characters, like a pair of opening and closing brackets.</summary>
internal readonly record struct CharacterPair(string Open, string Close)
{
    public static implicit operator CharacterPair((string Open, string Close) pair)
    {
        return new CharacterPair(pair.Open, pair.Close);
    }
}

/// <summary>A pair of an open and a close sequence.</summary>
internal interface IAutoClosingPair
{
    string Open { get; }

    string Close { get; }
}

/// <summary>An auto closing pair with the conditions under which it auto closes.</summary>
internal interface IAutoClosingPairConditional : IAutoClosingPair
{
    /// <summary>The token types ("string", "comment", "regex") in which the pair does not auto close.</summary>
    IReadOnlyList<string>? NotIn { get; }

    /// <summary>The pair auto closes only when the text before the cursor matches this.</summary>
    Regex? BeforeText { get; }
}

/// <summary>A pair of an open and a close sequence.</summary>
internal sealed record AutoClosingPair(string Open, string Close) : IAutoClosingPair;

/// <summary>An auto closing pair with the conditions under which it auto closes.</summary>
internal sealed record AutoClosingPairConditional(string Open, string Close) : IAutoClosingPairConditional
{
    public IReadOnlyList<string>? NotIn { get; init; }

    public Regex? BeforeText { get; init; }
}

/// <summary>Describes what to do with the indentation when pressing Enter.</summary>
internal enum IndentAction
{
    /// <summary>Insert new line and copy the previous line's indentation.</summary>
    None = 0,

    /// <summary>Insert new line and indent once (relative to the previous line's indentation).</summary>
    Indent = 1,

    /// <summary>
    /// Insert two new lines:
    ///  - the first one indented which will hold the cursor
    ///  - the second one at the same indentation level
    /// </summary>
    IndentOutdent = 2,

    /// <summary>Insert new line and outdent once (relative to the previous line's indentation).</summary>
    Outdent = 3,
}

/// <summary>Describes what to do when pressing Enter.</summary>
internal sealed class EnterAction(IndentAction indentAction)
{
    /// <summary>Describe what to do with the indentation.</summary>
    public IndentAction IndentAction { get; init; } = indentAction;

    /// <summary>Describes text to be appended after the new line and after the indentation.</summary>
    public string? AppendText { get; init; }

    /// <summary>Describes the number of characters to remove from the new line's indentation.</summary>
    public int? RemoveText { get; init; }
}

/// <summary>An <see cref="EnterAction"/> with every member resolved.</summary>
/// <param name="IndentAction">Describe what to do with the indentation.</param>
/// <param name="AppendText">Describes text to be appended after the new line and after the indentation.</param>
/// <param name="RemoveText">Describes the number of characters to remove from the new line's indentation.</param>
/// <param name="Indentation">The line's indentation minus removeText.</param>
internal sealed record CompleteEnterAction(IndentAction IndentAction, string AppendText, int RemoveText, string Indentation);

/// <summary>An auto closing pair, with its <c>notIn</c> list resolved to the token types it may close in.</summary>
internal sealed class StandardAutoClosingPairConditional : IAutoClosingPair
{
    private readonly bool _inString;
    private readonly bool _inComment;
    private readonly bool _inRegEx;
    private string? _neutralCharacter;
    private bool _neutralCharacterSearched;

    public StandardAutoClosingPairConditional(IAutoClosingPair source)
    {
        Open = source.Open;
        Close = source.Close;

        // initially allowed in all tokens
        _inString = true;
        _inComment = true;
        _inRegEx = true;

        if (source is not IAutoClosingPairConditional conditional)
        {
            return;
        }

        BeforeText = conditional.BeforeText;

        if (conditional.NotIn is { } notInList)
        {
            for (int i = 0, len = notInList.Count; i < len; i++)
            {
                var notIn = notInList[i];
                switch (notIn)
                {
                    case "string":
                        _inString = false;
                        break;
                    case "comment":
                        _inComment = false;
                        break;
                    case "regex":
                        _inRegEx = false;
                        break;
                }
            }
        }
    }

    public string Open { get; }

    public string Close { get; }

    public Regex? BeforeText { get; }

    public bool ShouldAutoCloseBefore(string text)
    {
        if (BeforeText is null)
        {
            return true;
        }
        return BeforeText.IsMatch(text);
    }

    public bool IsOK(StandardTokenType standardToken)
    {
        switch (standardToken)
        {
            case StandardTokenType.Other:
                return true;
            case StandardTokenType.Comment:
                return _inComment;
            case StandardTokenType.String:
                return _inString;
            case StandardTokenType.RegEx:
                return _inRegEx;
            default:
                // Upstream falls off the switch and returns undefined, which is falsy.
                return false;
        }
    }

    public bool ShouldAutoClose(ScopedLineTokens context, int column)
    {
        // Always complete on empty line
        if (context.GetTokenCount() == 0)
        {
            return true;
        }

        var tokenIndex = context.FindTokenIndexAtOffset(column - 2);
        var standardTokenType = context.GetStandardTokenType(tokenIndex);
        return IsOK(standardTokenType);
    }

    private string? FindNeutralCharacterInRange(char fromCharCode, char toCharCode)
    {
        for (var charCode = fromCharCode; charCode <= toCharCode; charCode++)
        {
            var character = charCode;
            if (!Open.Contains(character) && !Close.Contains(character))
            {
                return character.ToString();
            }
        }
        return null;
    }

    /// <summary>Find a character in the range [0-9a-zA-Z] that does not appear in the open or close.</summary>
    public string? FindNeutralCharacter()
    {
        if (!_neutralCharacterSearched)
        {
            _neutralCharacterSearched = true;
            _neutralCharacter ??= FindNeutralCharacterInRange('0', '9');
            _neutralCharacter ??= FindNeutralCharacterInRange('a', 'z');
            _neutralCharacter ??= FindNeutralCharacterInRange('A', 'Z');
        }
        return _neutralCharacter;
    }
}

/// <summary>The auto closing pairs of a language, indexed by either end of open and close.</summary>
internal sealed class AutoClosingPairs
{
    // it is useful to be able to get pairs using either end of open and close

    public AutoClosingPairs(IReadOnlyList<StandardAutoClosingPairConditional> autoClosingPairs)
    {
        foreach (var pair in autoClosingPairs)
        {
            AppendEntry(AutoClosingPairsOpenByStart, CharAt(pair.Open, 0), pair);
            AppendEntry(AutoClosingPairsOpenByEnd, CharAt(pair.Open, pair.Open.Length - 1), pair);
            AppendEntry(AutoClosingPairsCloseByStart, CharAt(pair.Close, 0), pair);
            AppendEntry(AutoClosingPairsCloseByEnd, CharAt(pair.Close, pair.Close.Length - 1), pair);
            if (pair.Close.Length == 1 && pair.Open.Length == 1)
            {
                AppendEntry(AutoClosingPairsCloseSingleChar, pair.Close, pair);
            }
        }
    }

    /// <summary>Key is first character of open.</summary>
    public Dictionary<string, List<StandardAutoClosingPairConditional>> AutoClosingPairsOpenByStart { get; } = new(StringComparer.Ordinal);

    /// <summary>Key is last character of open.</summary>
    public Dictionary<string, List<StandardAutoClosingPairConditional>> AutoClosingPairsOpenByEnd { get; } = new(StringComparer.Ordinal);

    /// <summary>Key is first character of close.</summary>
    public Dictionary<string, List<StandardAutoClosingPairConditional>> AutoClosingPairsCloseByStart { get; } = new(StringComparer.Ordinal);

    /// <summary>Key is last character of close.</summary>
    public Dictionary<string, List<StandardAutoClosingPairConditional>> AutoClosingPairsCloseByEnd { get; } = new(StringComparer.Ordinal);

    /// <summary>Key is close. Only has pairs that are a single character.</summary>
    public Dictionary<string, List<StandardAutoClosingPairConditional>> AutoClosingPairsCloseSingleChar { get; } = new(StringComparer.Ordinal);

    // String.prototype.charAt: the empty string for an index out of range.
    private static string CharAt(string text, int index)
    {
        return index >= 0 && index < text.Length ? text[index].ToString() : "";
    }

    private static void AppendEntry<TKey, TValue>(Dictionary<TKey, List<TValue>> target, TKey key, TValue value)
        where TKey : notnull
    {
        if (target.TryGetValue(key, out var list))
        {
            list.Add(value);
        }
        else
        {
            target[key] = [value];
        }
    }
}
