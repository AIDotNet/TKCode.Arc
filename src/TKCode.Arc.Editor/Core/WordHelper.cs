// Ported from vscode (src/vs/editor/common/core/wordHelper.ts, commit b57d5c9e), MIT license.
// Differences from upstream:
//  - Word definitions are .NET regular expressions. .NET has no global flag and no lastIndex: a match is searched for
//    from a start index (Regex.Match(text, startat)), so ensureValidWordDefinition only substitutes the default for
//    null. createWordRegExp builds its JavaScript source as upstream and translates it with JsRegex (ASCII \d and \w,
//    JavaScript's \s), so the default word definition matches what Monaco's does.
//  - setDefaultGetWordAtTextConfig is left out: it changes a process-wide default, and the engine keeps no static
//    mutable state. Callers pass a GetWordAtTextConfig instead; DefaultGetWordAtTextConfig is upstream's default.
//  - IWordAtPosition and IGetWordAtTextConfig are the records WordAtPosition and GetWordAtTextConfig.
//  - maxLen / 2 is an integer division (upstream divides as a double; the default maxLen of 1000 is even).
//  - A word definition that matches the empty string makes upstream's search loop forever; here the search moves on
//    by one code unit after an empty match.

using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;
using TKCode.Arc.Editor.Model;

namespace TKCode.Arc.Editor;

/// <summary>Word inside a model.</summary>
/// <param name="Word">The word.</param>
/// <param name="StartColumn">The column where the word starts.</param>
/// <param name="EndColumn">The column where the word ends.</param>
internal sealed record WordAtPosition(string Word, int StartColumn, int EndColumn);

/// <summary>How hard <see cref="WordHelper.GetWordAtText"/> tries on long lines.</summary>
/// <param name="MaxLen">Text longer than this is cut to a window of this length around the column.</param>
/// <param name="WindowSize">How far back each search step starts before the previous one.</param>
/// <param name="TimeBudget">Milliseconds after which the search stops with what it has.</param>
internal sealed record GetWordAtTextConfig(int MaxLen, int WindowSize, int TimeBudget);

internal static class WordHelper
{
    public const string UsualWordSeparators = "`~!@#$%^&*()-=+[{]}\\|;:'\",.<>/?";

    /// <summary>Upstream's default <see cref="GetWordAtTextConfig"/>.</summary>
    public static readonly GetWordAtTextConfig DefaultGetWordAtTextConfig = new(MaxLen: 1000, WindowSize: 15, TimeBudget: 150);

    /// <summary>
    /// Catches numbers (including floating numbers) in the first group, and alphanum in the second. In JavaScript:
    /// <c>/(-?\d*\.\d\w*)|([^\`\~\!\@\#\$\%\^\&amp;\*\(\)\-\=\+\[\{\]\}\\\|\;\:\'\"\,\.\&lt;\&gt;\/\?\s]+)/g</c>.
    /// </summary>
    public static readonly Regex DefaultWordRegExp = CreateWordRegExp();

    /// <summary>
    /// Create a word definition regular expression based on default word separators. Optionally provide allowed
    /// separators that should be included in words.
    /// </summary>
    public static Regex CreateWordRegExp(string allowInWords = "")
    {
        var source = new StringBuilder("(-?\\d*\\.\\d\\w*)|([^");
        foreach (var sep in UsualWordSeparators)
        {
            if (allowInWords.Contains(sep, StringComparison.Ordinal))
            {
                continue;
            }
            source.Append('\\').Append(sep);
        }
        source.Append("\\s]+)");
        return JsRegex.Create(source.ToString(), isRegex: true, matchCase: true, multiline: false).Regex;
    }

    /// <summary>The word definition to use: <paramref name="wordDefinition"/>, or the default when it is null.</summary>
    public static Regex EnsureValidWordDefinition(Regex? wordDefinition) => wordDefinition ?? DefaultWordRegExp;

    /// <summary>
    /// The word that contains or touches the 1-based <paramref name="column"/>, or null. <paramref name="text"/>
    /// starts <paramref name="textOffset"/> code units into the line, and the returned columns are line columns.
    /// </summary>
    public static WordAtPosition? GetWordAtText(int column, Regex? wordDefinition, string text, int textOffset, GetWordAtTextConfig? config = null)
    {
        var regex = EnsureValidWordDefinition(wordDefinition);
        config ??= DefaultGetWordAtTextConfig;

        if (text.Length > config.MaxLen)
        {
            // don't throw strings that long at the regexp
            // but use a sub-string in which a word must occur
            var start = column - config.MaxLen / 2;
            if (start < 0)
            {
                start = 0;
            }
            else
            {
                textOffset += start;
            }
            text = JsSubstring(text, start, column + config.MaxLen / 2);
            return GetWordAtText(column, regex, text, textOffset, config);
        }

        var t1 = Stopwatch.GetTimestamp();
        var pos = column - 1 - textOffset;

        var prevRegexIndex = -1;
        Match? match = null;

        for (var i = 1; ; i++)
        {
            // check time budget
            if (Stopwatch.GetElapsedTime(t1).TotalMilliseconds >= config.TimeBudget)
            {
                break;
            }

            // reset the index at which the regexp should start matching, also know where it
            // should stop so that subsequent search don't repeat previous searches
            var regexIndex = pos - config.WindowSize * i;
            var thisMatch = FindRegexMatchEnclosingPosition(regex, text, Math.Max(0, regexIndex), pos, prevRegexIndex);

            if (thisMatch is null && match is not null)
            {
                // stop: we have something
                break;
            }

            match = thisMatch;

            // stop: searched at start
            if (regexIndex <= 0)
            {
                break;
            }
            prevRegexIndex = regexIndex;
        }

        if (match is not null)
        {
            return new WordAtPosition(
                match.Value,
                textOffset + 1 + match.Index,
                textOffset + 1 + match.Index + match.Length);
        }

        return null;
    }

    private static Match? FindRegexMatchEnclosingPosition(Regex wordDefinition, string text, int lastIndex, int pos, int stopPos)
    {
        while (lastIndex <= text.Length)
        {
            var match = wordDefinition.Match(text, lastIndex);
            if (!match.Success)
            {
                return null;
            }

            var matchIndex = match.Index;
            var matchEnd = match.Index + match.Length;
            if (matchIndex <= pos && matchEnd >= pos)
            {
                return match;
            }
            else if (stopPos > 0 && matchIndex > stopPos)
            {
                return null;
            }

            lastIndex = match.Length == 0 ? matchEnd + 1 : matchEnd;
        }
        return null;
    }

    // JavaScript's String.prototype.substring: both ends are clamped to the string and swapped when reversed.
    private static string JsSubstring(string text, int start, int end)
    {
        start = Math.Clamp(start, 0, text.Length);
        end = Math.Clamp(end, 0, text.Length);
        if (start > end)
        {
            (start, end) = (end, start);
        }
        return text.Substring(start, end - start);
    }
}
