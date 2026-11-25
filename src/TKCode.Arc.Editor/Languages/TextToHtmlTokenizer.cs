// Ported from vscode (src/vs/editor/common/languages/textToHtmlTokenizer.ts: tokenizeLineToHTML, commit b57d5c9e),
// MIT license. Only tokenizeLineToHTML (the view model's rich text to copy); tokenizeToString and _tokenizeToString need
// the tokenization registry and come with tokenization.
// Differences from upstream: the HTML is built in a StringBuilder; String.fromCharCode of a UTF-16 code unit is the
// char itself.

using System.Text;
using TKCode.Arc.Editor.Tokens;

namespace TKCode.Arc.Editor.Languages;

/// <summary>Turns tokenized text into HTML with inline styles.</summary>
internal static class TextToHtmlTokenizer
{
    public static string TokenizeLineToHTML(
        string text,
        IViewLineTokens viewLineTokens,
        IReadOnlyList<string> colorMap,
        int startOffset,
        int endOffset,
        int tabSize,
        bool useNbsp)
    {
        var result = new StringBuilder("<div>");
        var charIndex = 0;
        var width = 0;

        var prevIsSpace = true;

        for (int tokenIndex = 0, tokenCount = viewLineTokens.GetCount(); tokenIndex < tokenCount; tokenIndex++)
        {
            var tokenEndIndex = viewLineTokens.GetEndOffset(tokenIndex);
            var partContent = new StringBuilder();

            for (; charIndex < tokenEndIndex && charIndex < endOffset; charIndex++)
            {
                var charCode = text[charIndex];
                var isTab = charCode == '\t';

                width += Strings.IsFullWidthCharacter(charCode) ? 2 : (isTab ? 0 : 1);

                if (charIndex < startOffset)
                {
                    if (isTab)
                    {
                        var remainder = width % tabSize;
                        width += remainder == 0 ? tabSize : tabSize - remainder;
                    }
                    continue;
                }

                switch (charCode)
                {
                    case '\t':
                    {
                        var remainder = width % tabSize;
                        var insertSpacesCount = remainder == 0 ? tabSize : tabSize - remainder;
                        width += insertSpacesCount;
                        var spacesRemaining = insertSpacesCount;
                        while (spacesRemaining > 0)
                        {
                            if (useNbsp && prevIsSpace)
                            {
                                partContent.Append("&#160;");
                                prevIsSpace = false;
                            }
                            else
                            {
                                partContent.Append(' ');
                                prevIsSpace = true;
                            }
                            spacesRemaining--;
                        }
                        break;
                    }
                    case '<':
                        partContent.Append("&lt;");
                        prevIsSpace = false;
                        break;

                    case '>':
                        partContent.Append("&gt;");
                        prevIsSpace = false;
                        break;

                    case '&':
                        partContent.Append("&amp;");
                        prevIsSpace = false;
                        break;

                    case '\0':
                        partContent.Append("&#00;");
                        prevIsSpace = false;
                        break;

                    case '\uFEFF':
                    case '\u2028':
                    case '\u2029':
                    case '\u0085':
                        partContent.Append('\uFFFD');
                        prevIsSpace = false;
                        break;

                    case '\r':
                        // zero width space, because carriage return would introduce a line break
                        partContent.Append("&#8203");
                        prevIsSpace = false;
                        break;

                    case ' ':
                        if (useNbsp && prevIsSpace)
                        {
                            partContent.Append("&#160;");
                            prevIsSpace = false;
                        }
                        else
                        {
                            partContent.Append(' ');
                            prevIsSpace = true;
                        }
                        break;

                    default:
                        partContent.Append(charCode);
                        prevIsSpace = false;
                        break;
                }
            }

            if (tokenEndIndex <= startOffset)
            {
                continue;
            }

            result.Append("<span style=\"").Append(viewLineTokens.GetInlineStyle(tokenIndex, colorMap)).Append("\">").Append(partContent).Append("</span>");

            if (tokenEndIndex > endOffset || charIndex >= endOffset || startOffset >= endOffset)
            {
                break;
            }
        }

        result.Append("</div>");
        return result.ToString();
    }
}
