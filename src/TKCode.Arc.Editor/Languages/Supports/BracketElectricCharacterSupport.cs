// Ported from vscode (src/vs/editor/common/languages/supports/electricCharacter.ts, commit b57d5c9e), MIT license.
// Differences from upstream: IElectricAction is the record ElectricAction; `/^\s*$/` is a check for JavaScript
// whitespace (RegExps.CreateRegExp); the line text is cut with JavaScript's substring semantics.

using System.Text.RegularExpressions;
using TKCode.Arc.Editor.Model;

namespace TKCode.Arc.Editor.Languages;

/// <summary>What typing an electric character does (upstream's <c>IElectricAction</c>).</summary>
/// <param name="MatchOpenBracket">
/// The line will be indented at the same level of the line which contains the matching given bracket type.
/// </param>
internal sealed record ElectricAction(string MatchOpenBracket);

/// <summary>Re-indents a line when a closing bracket is typed as its first text.</summary>
internal sealed class BracketElectricCharacterSupport
{
    private static readonly Regex s_whitespaceOnly = RegExps.CreateRegExp("^\\s*$", isRegex: true, new RegExpOptions(MatchCase: true));

    private readonly RichEditBrackets? _richEditBrackets;

    public BracketElectricCharacterSupport(RichEditBrackets? richEditBrackets)
    {
        _richEditBrackets = richEditBrackets;
    }

    public IReadOnlyList<string> GetElectricCharacters()
    {
        var result = new List<string>();

        if (_richEditBrackets is not null)
        {
            foreach (var bracket in _richEditBrackets.Brackets)
            {
                foreach (var close in bracket.Close)
                {
                    var lastChar = close.Length > 0 ? close[^1].ToString() : "";
                    result.Add(lastChar);
                }
            }
        }

        // arrays.distinct: the first occurrence of each, in order
        return result.Distinct(StringComparer.Ordinal).ToList();
    }

    public ElectricAction? OnElectricCharacter(string character, ScopedLineTokens context, int column)
    {
        if (_richEditBrackets is null || _richEditBrackets.Brackets.Count == 0)
        {
            return null;
        }

        var tokenIndex = context.FindTokenIndexAtOffset(column - 1);
        if (Supports.IgnoreBracketsInToken(context.GetStandardTokenType(tokenIndex)))
        {
            return null;
        }

        var reversedBracketRegex = _richEditBrackets.ReversedRegex;
        var text = SearchHelpers.JsSubstring(context.GetLineContent(), 0, column - 1).ToString() + character;

        var r = BracketsUtils.FindPrevBracketInRange(reversedBracketRegex, 1, text, 0, text.Length);
        if (r is not { } range)
        {
            return null;
        }

        var bracketText = text.Substring(range.StartColumn - 1, range.EndColumn - range.StartColumn).ToLowerInvariant();

        var isOpen = _richEditBrackets.TextIsOpenBracket.TryGetValue(bracketText, out var open) && open;
        if (isOpen)
        {
            return null;
        }

        var textBeforeBracket = context.GetActualLineContentBefore(range.StartColumn - 1);
        if (!s_whitespaceOnly.IsMatch(textBeforeBracket))
        {
            // There is other text on the line before the bracket
            return null;
        }

        return new ElectricAction(bracketText);
    }
}
