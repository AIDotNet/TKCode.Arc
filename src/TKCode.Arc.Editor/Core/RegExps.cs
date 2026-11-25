// Adaptation of vscode (src/vs/base/common/strings.ts: RegExpOptions, createRegExp, commit b57d5c9e), MIT license.
// The expression is translated by JsRegex (see its header for how JavaScript semantics map onto .NET), so \s, $ and .
// behave as in JavaScript. Differences from upstream:
//  - the global flag has no meaning in .NET (a Regex keeps no lastIndex), and the expression always gets JsRegex's
//    u-flag semantics (. matches a whole code point), whether or not Unicode is set;
//  - an invalid pattern throws ArgumentException (RegexParseException derives from it), where JavaScript throws
//    SyntaxError.

using System.Text.RegularExpressions;
using TKCode.Arc.Editor.Model;

namespace TKCode.Arc.Editor;

/// <summary>The flags of <see cref="RegExps.CreateRegExp"/>.</summary>
internal readonly record struct RegExpOptions(
    bool MatchCase = false,
    bool WholeWord = false,
    bool Multiline = false,
    bool Global = false,
    bool Unicode = false);

/// <summary>Builds .NET regular expressions from JavaScript regular expression sources.</summary>
internal static class RegExps
{
    /// <summary>
    /// Builds a regular expression the way <c>strings.createRegExp</c> does: <paramref name="searchString"/> is escaped
    /// unless <paramref name="isRegex"/>, and is case-insensitive unless <see cref="RegExpOptions.MatchCase"/>.
    /// </summary>
    public static Regex CreateRegExp(string searchString, bool isRegex, RegExpOptions options = default)
    {
        if (string.IsNullOrEmpty(searchString))
        {
            throw new ArgumentException("Cannot create regex from empty string", nameof(searchString));
        }
        if (!isRegex)
        {
            searchString = SearchHelpers.EscapeRegExpCharacters(searchString);
        }
        if (options.WholeWord)
        {
            if (IsWordCharacter(searchString[0]))
            {
                searchString = "\\b" + searchString;
            }
            if (IsWordCharacter(searchString[^1]))
            {
                searchString += "\\b";
            }
        }

        return JsRegex.Create(searchString, isRegex: true, options.MatchCase, options.Multiline).Regex;
    }

    /// <summary>
    /// Whether JavaScript's <c>\w</c> matches <paramref name="ch"/>; upstream asks <c>!/\B/.test(ch)</c>, which is the
    /// same for one character.
    /// </summary>
    public static bool IsWordCharacter(char ch)
    {
        return ch is (>= 'a' and <= 'z') or (>= 'A' and <= 'Z') or (>= '0' and <= '9') or '_';
    }
}
