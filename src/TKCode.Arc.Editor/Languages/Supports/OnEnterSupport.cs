// Ported from vscode (src/vs/editor/common/languages/supports/onEnter.ts, commit b57d5c9e), MIT license.
// Differences from upstream:
//  - IOnEnterSupportOptions is the record OnEnterSupportOptions; the support takes a LanguageConfiguration too, as
//    upstream passes one (it has the same two members);
//  - a .NET Regex keeps no lastIndex, so the reset that disables the effect of the "g" flag is not needed;
//  - the bracket expressions are built by RegExps.CreateRegExp (case-sensitive, JavaScript's \s and $); a bracket
//    that gives an invalid expression is reported to UnexpectedErrors and skipped, as upstream does.

using System.Text.RegularExpressions;
using TKCode.Arc.Editor.Config;
using TKCode.Arc.Editor.Model;

namespace TKCode.Arc.Editor.Languages;

/// <summary>The options of an <see cref="OnEnterSupport"/> (upstream's <c>IOnEnterSupportOptions</c>).</summary>
internal sealed record OnEnterSupportOptions
{
    public IReadOnlyList<CharacterPair>? Brackets { get; init; }

    public IReadOnlyList<OnEnterRule>? OnEnterRules { get; init; }
}

/// <summary>Decides what pressing Enter does from the language's on-enter rules and brackets.</summary>
internal sealed class OnEnterSupport
{
    private sealed record ProcessedBracketPair(string Open, string Close, Regex OpenRegExp, Regex CloseRegExp);

    private static readonly CharacterPair[] s_defaultBrackets =
    [
        new CharacterPair("(", ")"),
        new CharacterPair("{", "}"),
        new CharacterPair("[", "]"),
    ];

    private readonly List<ProcessedBracketPair> _brackets;
    private readonly IReadOnlyList<OnEnterRule> _regExpRules;

    public OnEnterSupport(OnEnterSupportOptions? opts)
        : this(opts?.Brackets, opts?.OnEnterRules)
    {
    }

    public OnEnterSupport(LanguageConfiguration config)
        : this(config.Brackets, config.OnEnterRules)
    {
    }

    private OnEnterSupport(IReadOnlyList<CharacterPair>? brackets, IReadOnlyList<OnEnterRule>? onEnterRules)
    {
        brackets ??= s_defaultBrackets;

        _brackets = new List<ProcessedBracketPair>();
        foreach (var bracket in brackets)
        {
            var openRegExp = CreateOpenBracketRegExp(bracket.Open);
            var closeRegExp = CreateCloseBracketRegExp(bracket.Close);
            if (openRegExp is not null && closeRegExp is not null)
            {
                _brackets.Add(new ProcessedBracketPair(bracket.Open, bracket.Close, openRegExp, closeRegExp));
            }
        }
        _regExpRules = onEnterRules ?? [];
    }

    public EnterAction? OnEnter(EditorAutoIndentStrategy autoIndent, string previousLineText, string beforeEnterText, string afterEnterText)
    {
        // (1): `regExpRules`
        if (autoIndent >= EditorAutoIndentStrategy.Advanced)
        {
            for (int i = 0, len = _regExpRules.Count; i < len; i++)
            {
                var rule = _regExpRules[i];
                var regResult = Test(rule.BeforeText, beforeEnterText)
                    && Test(rule.AfterText, afterEnterText)
                    && Test(rule.PreviousLineText, previousLineText);

                if (regResult)
                {
                    return rule.Action;
                }
            }
        }

        // (2): Special indent-outdent
        if (autoIndent >= EditorAutoIndentStrategy.Brackets)
        {
            if (beforeEnterText.Length > 0 && afterEnterText.Length > 0)
            {
                for (int i = 0, len = _brackets.Count; i < len; i++)
                {
                    var bracket = _brackets[i];
                    if (bracket.OpenRegExp.IsMatch(beforeEnterText) && bracket.CloseRegExp.IsMatch(afterEnterText))
                    {
                        return new EnterAction(IndentAction.IndentOutdent);
                    }
                }
            }
        }

        // (4): Open bracket based logic
        if (autoIndent >= EditorAutoIndentStrategy.Brackets)
        {
            if (beforeEnterText.Length > 0)
            {
                for (int i = 0, len = _brackets.Count; i < len; i++)
                {
                    var bracket = _brackets[i];
                    if (bracket.OpenRegExp.IsMatch(beforeEnterText))
                    {
                        return new EnterAction(IndentAction.Indent);
                    }
                }
            }
        }

        return null;
    }

    private static bool Test(Regex? reg, string text)
    {
        if (reg is null)
        {
            return true;
        }
        return reg.IsMatch(text);
    }

    private static Regex? CreateOpenBracketRegExp(string bracket)
    {
        var str = SearchHelpers.EscapeRegExpCharacters(bracket);
        if (str.Length > 0 && RegExps.IsWordCharacter(str[0]))
        {
            str = "\\b" + str;
        }
        str += "\\s*$";
        return SafeRegExp(str);
    }

    private static Regex? CreateCloseBracketRegExp(string bracket)
    {
        var str = SearchHelpers.EscapeRegExpCharacters(bracket);
        if (str.Length > 0 && RegExps.IsWordCharacter(str[^1]))
        {
            str += "\\b";
        }
        str = "^\\s*" + str;
        return SafeRegExp(str);
    }

    private static Regex? SafeRegExp(string def)
    {
        try
        {
            return RegExps.CreateRegExp(def, isRegex: true, new RegExpOptions(MatchCase: true));
        }
        catch (ArgumentException err)
        {
            UnexpectedErrors.Report(err);
            return null;
        }
    }
}
