// Ported from vscode (src/vs/editor/common/languages/textToHtmlTokenizer.ts: tokenizeToString and _tokenizeToString;
// src/vs/editor/browser/widget/markdownRenderer/browser/editorMarkdownCodeBlockRenderer.ts: the language of a code
// block, commit b57d5c9e), MIT license.
// Differences from upstream: the result is the tokens of each line (text and metadata) instead of HTML with mtk
// classes; the controls colour them with the theme's color map. The tokenizer is resolved by the caller (upstream
// awaits TokenizationRegistry.getOrCreate); without one every line is one token in the default colours.

using TKCode.Arc.Editor.Languages;
using TKCode.Arc.Editor.Tokens;

namespace TKCode.Arc.Editor.Contrib.Hover;

/// <summary>A piece of a tokenized code line: its text and its token metadata (colour, font style).</summary>
internal readonly record struct CodeToken(string Text, uint Metadata)
{
    public ColorId Foreground => TokenMetadata.GetForeground(Metadata);

    public FontStyle FontStyle => TokenMetadata.GetFontStyle(Metadata);
}

/// <summary>Tokenizes the code of a Markdown code block with the editor's tokenizers.</summary>
internal static class MarkdownCodeTokenizer
{
    /// <summary>
    /// The language a code block is in (upstream's EditorMarkdownCodeBlockRenderer): the fence's language name or alias,
    /// else the editor's language, else plaintext.
    /// </summary>
    public static string GetLanguageId(LanguageService languageService, string? languageAlias, string? editorLanguageId)
    {
        string? languageId = null;
        if (!string.IsNullOrEmpty(languageAlias))
        {
            languageId = languageService.GetLanguageIdByLanguageName(languageAlias)
                ?? (languageService.IsRegisteredLanguageId(languageAlias) ? languageAlias : null);
        }
        else
        {
            languageId = editorLanguageId;
        }

        return string.IsNullOrEmpty(languageId) ? LanguageIds.PlaintextLanguageId : languageId;
    }

    /// <summary>The tokens of each line of <paramref name="text"/> (upstream's _tokenizeToString).</summary>
    public static IReadOnlyList<IReadOnlyList<CodeToken>> Tokenize(string text, ITokenizationSupport? tokenizationSupport, LanguageId fallbackLanguageId = default)
    {
        var lines = text.Split(["\r\n", "\r", "\n"], StringSplitOptions.None);
        var result = new List<IReadOnlyList<CodeToken>>(lines.Length);
        IState? currentState = tokenizationSupport?.GetInitialState();
        foreach (var line in lines)
        {
            EncodedTokenizationResult tokenizationResult;
            if (tokenizationSupport is not null && currentState is not null)
            {
                try
                {
                    tokenizationResult = tokenizationSupport.TokenizeEncoded(line, true, currentState);
                }
                catch (Exception e)
                {
                    UnexpectedErrors.Report(e);
                    tokenizationResult = NullTokenize.TokenizeEncoded(fallbackLanguageId, currentState);
                }
            }
            else
            {
                tokenizationResult = NullTokenize.TokenizeEncoded(fallbackLanguageId, currentState);
            }

            var tokens = tokenizationResult.Tokens;
            var lineTokens = new List<CodeToken>();
            var count = tokens.Length >> 1;
            for (var j = 0; j < count; j++)
            {
                var start = Math.Min((int)tokens[2 * j], line.Length);
                var end = j + 1 < count ? Math.Min((int)tokens[2 * (j + 1)], line.Length) : line.Length;
                if (end > start || (count == 1 && line.Length == 0))
                {
                    lineTokens.Add(new CodeToken(line[start..end], tokens[(2 * j) + 1]));
                }
            }

            result.Add(lineTokens);
            currentState = tokenizationResult.EndState;
        }

        return result;
    }
}
