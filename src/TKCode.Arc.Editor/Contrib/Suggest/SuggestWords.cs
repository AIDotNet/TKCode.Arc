// Ported from vscode, commit b57d5c9e, MIT license:
//  - src/vs/editor/common/model/textModel.ts: getWordAtPosition, getWordUntilPosition;
//  - src/vs/editor/common/services/textModelSync/textModelSync.impl.ts: words, getLineWords, _wordenize;
//  - src/vs/editor/common/services/editorWebWorker.ts: $textualSuggest, $computeWordRanges.
// Differences from upstream:
//  - the word at a position uses the word definition of the model's language (upstream also looks at the language of
//    the token at the position, for embedded languages);
//  - there is no web worker: the words are read from a TextSnapshot, which any thread may read;
//  - isNaN(Number(word)) is JsNumber.IsNumber.

using System.Globalization;
using System.Text.RegularExpressions;
using TKCode.Arc.Editor.Languages;
using TKCode.Arc.Editor.Model;

namespace TKCode.Arc.Editor.Contrib.Suggest;

/// <summary>Words of a model for the suggest feature.</summary>
internal static class SuggestWords
{
    /// <summary>The word definition of the model's language, or the default one.</summary>
    public static Regex GetWordDefinition(TextModel model, LanguageConfigurationService? languageConfigurationService) =>
        languageConfigurationService?.GetLanguageConfiguration(model.GetLanguageId()).GetWordDefinition() ?? WordHelper.DefaultWordRegExp;

    /// <summary>The word at or touching <paramref name="position"/>, or null (upstream's ITextModel.getWordAtPosition).</summary>
    public static WordAtPosition? GetWordAtPosition(TextModel model, TextPosition position, LanguageConfigurationService? languageConfigurationService)
    {
        position = model.ValidatePosition(position);
        var lineContent = model.GetLineContent(position.LineNumber);
        return WordHelper.GetWordAtText(position.Column, GetWordDefinition(model, languageConfigurationService), lineContent, 0);
    }

    /// <summary>The part of the word at <paramref name="position"/> before it; an empty word when there is none (upstream's getWordUntilPosition).</summary>
    public static WordAtPosition GetWordUntilPosition(TextModel model, TextPosition position, LanguageConfigurationService? languageConfigurationService)
    {
        var wordAtPosition = GetWordAtPosition(model, position, languageConfigurationService);
        if (wordAtPosition is null)
        {
            return new WordAtPosition(string.Empty, position.Column, position.Column);
        }

        return new WordAtPosition(
            wordAtPosition.Word[..(position.Column - wordAtPosition.StartColumn)],
            wordAtPosition.StartColumn,
            position.Column);
    }

    /// <summary>The ranges of the words in <paramref name="content"/> (upstream's _wordenize).</summary>
    public static List<(int Start, int End)> Wordenize(string content, Regex wordDefinition)
    {
        var result = new List<(int Start, int End)>();
        var index = 0;
        while (index <= content.Length)
        {
            var match = wordDefinition.Match(content, index);
            if (!match.Success || match.Length == 0)
            {
                // it did match the empty string
                break;
            }
            result.Add((match.Index, match.Index + match.Length));
            index = match.Index + match.Length;
        }
        return result;
    }

    /// <summary>
    /// The distinct words of a snapshot, except <paramref name="leadingWord"/> and numbers, at most
    /// <see cref="SuggestionsLimit"/> + 1 (upstream's $textualSuggest).
    /// </summary>
    public static List<string> TextualSuggest(TextSnapshot snapshot, string? leadingWord, Regex wordDefinition, CancellationToken token = default)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var words = new List<string>();
        for (var lineNumber = 1; lineNumber <= snapshot.LineCount; lineNumber++)
        {
            if ((lineNumber & 1023) == 0)
            {
                token.ThrowIfCancellationRequested();
            }

            var lineText = snapshot.GetLineContent(lineNumber);
            foreach (var (start, end) in Wordenize(lineText, wordDefinition))
            {
                var word = lineText[start..end];
                if (word == leadingWord || JsNumber.IsNumber(word))
                {
                    continue;
                }
                if (seen.Add(word))
                {
                    words.Add(word);
                }
                if (seen.Count > SuggestionsLimit)
                {
                    return words;
                }
            }
        }
        return words;
    }

    /// <summary>Upstream's EditorWorker._suggestionsLimit.</summary>
    public const int SuggestionsLimit = 10000;

    /// <summary>
    /// The ranges of every word on the lines from <paramref name="range"/>'s start line up to, not including, its end
    /// line, by word (upstream's $computeWordRanges).
    /// </summary>
    public static Dictionary<string, List<TextRange>> ComputeWordRanges(TextModel model, TextRange range, Regex wordDefinition)
    {
        var result = new Dictionary<string, List<TextRange>>(StringComparer.Ordinal);
        for (var line = range.StartLineNumber; line < range.EndLineNumber; line++)
        {
            var content = model.GetLineContent(line);
            foreach (var (start, end) in Wordenize(content, wordDefinition))
            {
                var word = content[start..end];
                if (JsNumber.IsNumber(word))
                {
                    continue;
                }
                if (!result.TryGetValue(word, out var array))
                {
                    array = [];
                    result[word] = array;
                }
                array.Add(new TextRange(line, start + 1, line, end + 1));
            }
        }
        return result;
    }
}

/// <summary>JavaScript's <c>!isNaN(Number(text))</c>.</summary>
internal static class JsNumber
{
    public static bool IsNumber(string text)
    {
        var s = text.Trim();
        if (s.Length == 0)
        {
            return true; // Number('') is 0
        }

        if (s.Length > 2 && s[0] == '0')
        {
            var radix = s[1] switch
            {
                'x' or 'X' => 16,
                'o' or 'O' => 8,
                'b' or 'B' => 2,
                _ => 0,
            };
            if (radix != 0)
            {
                for (var i = 2; i < s.Length; i++)
                {
                    var c = char.ToLowerInvariant(s[i]);
                    var digit = c is >= '0' and <= '9' ? c - '0' : c is >= 'a' and <= 'z' ? c - 'a' + 10 : 99;
                    if (digit >= radix)
                    {
                        return false;
                    }
                }
                return true;
            }
        }

        if (s is "Infinity" or "+Infinity" or "-Infinity")
        {
            return true;
        }

        foreach (var c in s)
        {
            // double.TryParse also takes "NaN", currency and group separators; JavaScript's Number does not
            if (!(c is >= '0' and <= '9' or '.' or 'e' or 'E' or '+' or '-'))
            {
                return false;
            }
        }

        return double.TryParse(s, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint | NumberStyles.AllowExponent, CultureInfo.InvariantCulture, out _);
    }
}
