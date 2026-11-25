// Ported from vscode (src/vs/editor/contrib/linesOperations/browser/linesOperations.ts: the _modifyText of
// UpperCaseAction, LowerCaseAction, TitleCaseAction, SnakeCaseAction, CamelCaseAction, PascalCaseAction and
// KebabCaseAction, commit b57d5c9e), MIT license.
// Differences from upstream:
//  - the regular expressions are .NET ones; every engine supports them, so BackwardsCompatibleRegExp and the
//    isSupported checks are gone;
//  - with the `u` flag \p{L} matches a code point; here it matches a UTF-16 code unit, so letters outside the BMP are
//    not letters;
//  - toLocaleUpperCase / toLocaleLowerCase use the current culture.

using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace TKCode.Arc.Editor.Contrib.LinesOperations;

/// <summary>The text transforms of the case actions.</summary>
internal static class CaseTransforms
{
    private static readonly Regex s_titleBoundary = new(@"(^|[^\p{L}\p{N}']|((^|\P{L})'))\p{L}", RegexOptions.Multiline);
    private static readonly Regex s_snakeCaseBoundary = new(@"(\p{Ll})(\p{Lu})", RegexOptions.Multiline);
    private static readonly Regex s_snakeSingleLetters = new(@"(\p{Lu}|\p{N})(\p{Lu})(\p{Ll})", RegexOptions.Multiline);
    private static readonly Regex s_camelSingleLineWordBoundary = new(@"[_\s-]+", RegexOptions.Multiline);
    private static readonly Regex s_camelMultiLineWordBoundary = new(@"[_-]+", RegexOptions.Multiline);
    private static readonly Regex s_camelValidWordStart = new(@"^(\p{Lu}[^\p{Lu}])", RegexOptions.Multiline);
    private static readonly Regex s_lineBreak = new(@"\r\n|\r|\n");
    private static readonly Regex s_pascalWordBoundary = new(@"[_ \t-]", RegexOptions.Multiline);
    private static readonly Regex s_pascalWordBoundaryToMaintain = new(@"(?<=\.)", RegexOptions.Multiline);
    private static readonly Regex s_pascalUpperCaseWordMatcher = new(@"^\p{Lu}+$", RegexOptions.Multiline);
    private static readonly Regex s_kebabCaseBoundary = new(@"(\p{Ll})(\p{Lu})", RegexOptions.Multiline);
    private static readonly Regex s_kebabSingleLetters = new(@"(\p{Lu}|\p{N})(\p{Lu}\p{Ll})", RegexOptions.Multiline);
    private static readonly Regex s_kebabUnderscoreBoundary = new(@"(\S)(_)(\S)", RegexOptions.Multiline);

    private static string Upper(string text) => text.ToUpper(CultureInfo.CurrentCulture);

    private static string Lower(string text) => text.ToLower(CultureInfo.CurrentCulture);

    /// <summary>UpperCaseAction.</summary>
    public static string ToUpperCase(string text, string wordSeparators) => Upper(text);

    /// <summary>LowerCaseAction.</summary>
    public static string ToLowerCase(string text, string wordSeparators) => Lower(text);

    /// <summary>TitleCaseAction.</summary>
    public static string ToTitleCase(string text, string wordSeparators) =>
        s_titleBoundary.Replace(Lower(text), static b => Upper(b.Value));

    /// <summary>SnakeCaseAction.</summary>
    public static string ToSnakeCase(string text, string wordSeparators) =>
        Lower(s_snakeSingleLetters.Replace(s_snakeCaseBoundary.Replace(text, "$1_$2"), "$1_$2$3"));

    /// <summary>CamelCaseAction.</summary>
    public static string ToCamelCase(string text, string wordSeparators)
    {
        var wordBoundary = s_lineBreak.IsMatch(text) ? s_camelMultiLineWordBoundary : s_camelSingleLineWordBoundary;
        var words = wordBoundary.Split(text);
        var firstWord = s_camelValidWordStart.Replace(words[0], static start => Lower(start.Value));
        var result = new StringBuilder(firstWord);
        for (var i = 1; i < words.Length; i++)
        {
            var word = words[i];
            result.Append(Upper(word.Substring(0, Math.Min(1, word.Length)))).Append(word.Length > 1 ? word.Substring(1) : "");
        }
        return result.ToString();
    }

    /// <summary>PascalCaseAction.</summary>
    public static string ToPascalCase(string text, string wordSeparators)
    {
        var wordsWithMaintainBoundaries = s_pascalWordBoundaryToMaintain.Split(text);
        var words = wordsWithMaintainBoundaries.SelectMany(word => s_pascalWordBoundary.Split(word));

        var result = new StringBuilder();
        foreach (var word in words)
        {
            var normalizedWord = word.Length == 0 ? word : Upper(word.Substring(0, 1)) + word.Substring(1);
            var isAllCaps = normalizedWord.Length > 1 && s_pascalUpperCaseWordMatcher.IsMatch(normalizedWord);
            if (isAllCaps)
            {
                result.Append(normalizedWord[0]).Append(Lower(normalizedWord.Substring(1)));
            }
            else
            {
                result.Append(normalizedWord);
            }
        }
        return result.ToString();
    }

    /// <summary>KebabCaseAction.</summary>
    public static string ToKebabCase(string text, string wordSeparators)
    {
        var result = s_kebabUnderscoreBoundary.Replace(text, "$1-$3");
        result = s_kebabCaseBoundary.Replace(result, "$1-$2");
        result = s_kebabSingleLetters.Replace(result, "$1-$2");
        return Lower(result);
    }
}
