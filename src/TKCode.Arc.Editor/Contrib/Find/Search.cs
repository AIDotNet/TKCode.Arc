// Ported from vscode (src/vs/base/common/search.ts, commit b57d5c9e), MIT license.
// Differences: JavaScript's toUpperCase / toLowerCase are the invariant culture's; strings.containsUppercaseCharacter
// (on the first character only) is char.IsUpper of that character.

namespace TKCode.Arc.Editor.Contrib.Find;

/// <summary>Case-preserving replace strings (upstream's base/common/search.ts).</summary>
internal static class Search
{
    /// <summary>
    /// The replace string cased like the match (<paramref name="matches"/>[0]): all upper, all lower, a capital first
    /// letter, and per part when both are split by '-' or '_' into as many parts.
    /// </summary>
    public static string BuildReplaceStringWithCasePreserved(IReadOnlyList<string?>? matches, string pattern)
    {
        if (matches is { Count: > 0 } && matches[0] is { Length: > 0 } match)
        {
            var containsHyphens = ValidateSpecificSpecialCharacter(match, pattern, '-');
            var containsUnderscores = ValidateSpecificSpecialCharacter(match, pattern, '_');
            if (containsHyphens && !containsUnderscores)
            {
                return BuildReplaceStringForSpecificSpecialCharacter(match, pattern, '-');
            }
            else if (!containsHyphens && containsUnderscores)
            {
                return BuildReplaceStringForSpecificSpecialCharacter(match, pattern, '_');
            }

            if (string.Equals(match.ToUpperInvariant(), match, StringComparison.Ordinal))
            {
                return pattern.ToUpperInvariant();
            }
            else if (string.Equals(match.ToLowerInvariant(), match, StringComparison.Ordinal))
            {
                return pattern.ToLowerInvariant();
            }
            else if (char.IsUpper(match[0]) && pattern.Length > 0)
            {
                return char.ToUpperInvariant(pattern[0]) + pattern.Substring(1);
            }
            else if (char.ToUpperInvariant(match[0]) != match[0] && pattern.Length > 0)
            {
                return char.ToLowerInvariant(pattern[0]) + pattern.Substring(1);
            }
            else
            {
                // we don't understand its pattern yet.
                return pattern;
            }
        }

        return pattern;
    }

    private static bool ValidateSpecificSpecialCharacter(string match, string pattern, char specialCharacter)
    {
        var doesContainSpecialCharacter = match.Contains(specialCharacter) && pattern.Contains(specialCharacter);
        return doesContainSpecialCharacter && match.Split(specialCharacter).Length == pattern.Split(specialCharacter).Length;
    }

    private static string BuildReplaceStringForSpecificSpecialCharacter(string match, string pattern, char specialCharacter)
    {
        var splitPatternAtSpecialCharacter = pattern.Split(specialCharacter);
        var splitMatchAtSpecialCharacter = match.Split(specialCharacter);
        var parts = new string[splitPatternAtSpecialCharacter.Length];
        for (var index = 0; index < splitPatternAtSpecialCharacter.Length; index++)
        {
            parts[index] = BuildReplaceStringWithCasePreserved([splitMatchAtSpecialCharacter[index]], splitPatternAtSpecialCharacter[index]);
        }

        return string.Join(specialCharacter, parts);
    }
}
