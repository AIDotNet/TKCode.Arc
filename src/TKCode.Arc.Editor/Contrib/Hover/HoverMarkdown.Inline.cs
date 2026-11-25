// The inline half of the hover Markdown parser (see HoverMarkdown.cs): code spans, links and autolinks, emphasis
// (** __ * _ ~~), backslash escapes, character references and line breaks, flattened into runs.

using System.Globalization;
using System.Net;
using System.Text;

namespace TKCode.Arc.Editor.Contrib.Hover;

internal static partial class HoverMarkdown
{
    /// <summary>Parses the inline Markdown of a paragraph or heading into runs.</summary>
    public static IReadOnlyList<MarkdownRun> ParseInlines(string text)
    {
        var runs = new List<MarkdownRun>();
        ParseInlines(text, MarkdownRunStyle.None, null, null, runs);
        return Merge(runs);
    }

    private static void ParseInlines(string text, MarkdownRunStyle style, string? href, string? title, List<MarkdownRun> runs)
    {
        var sb = new StringBuilder();

        void Flush()
        {
            if (sb.Length > 0)
            {
                runs.Add(new MarkdownRun(sb.ToString(), MarkdownRunKind.Text, style) { Href = href, Title = title });
                sb.Clear();
            }
        }

        var i = 0;
        while (i < text.Length)
        {
            var ch = text[i];

            // backslash escape, or a hard line break
            if (ch == '\\' && i + 1 < text.Length)
            {
                var next = text[i + 1];
                if (next == '\n')
                {
                    Flush();
                    runs.Add(new MarkdownRun("\n", MarkdownRunKind.LineBreak, style) { Href = href });
                    i += 2;
                    continue;
                }

                if (char.IsAsciiLetterOrDigit(next) || next > 127 || char.IsWhiteSpace(next))
                {
                    sb.Append(ch);
                    i++;
                    continue;
                }

                sb.Append(next);
                i += 2;
                continue;
            }

            if (ch == '\n')
            {
                // two or more spaces before a newline: a hard break; else a soft break (a space)
                var trailing = 0;
                while (sb.Length - trailing > 0 && sb[sb.Length - 1 - trailing] == ' ')
                {
                    trailing++;
                }

                sb.Length -= trailing;
                if (trailing >= 2)
                {
                    Flush();
                    runs.Add(new MarkdownRun("\n", MarkdownRunKind.LineBreak, style) { Href = href });
                }
                else
                {
                    sb.Append(' ');
                }

                i++;
                while (i < text.Length && text[i] == ' ')
                {
                    i++;
                }

                continue;
            }

            if (ch == '`')
            {
                var open = CountRun(text, i, '`');
                var close = FindBacktickRun(text, i + open, open);
                if (close >= 0)
                {
                    Flush();
                    var code = text[(i + open)..close].Replace('\n', ' ');
                    if (code.Length >= 2 && code[0] == ' ' && code[^1] == ' ' && code.Trim().Length > 0)
                    {
                        code = code[1..^1];
                    }

                    runs.Add(new MarkdownRun(code, MarkdownRunKind.Code, style) { Href = href, Title = title });
                    i = close + open;
                    continue;
                }

                sb.Append(text, i, open);
                i += open;
                continue;
            }

            if (ch == '<')
            {
                var end = text.IndexOf('>', i + 1);
                if (end > i)
                {
                    var inner = text[(i + 1)..end];
                    if (IsAutolink(inner))
                    {
                        Flush();
                        var target = inner.Contains('@') && !inner.Contains(':') ? "mailto:" + inner : inner;
                        runs.Add(new MarkdownRun(inner, MarkdownRunKind.Text, style) { Href = target });
                        i = end + 1;
                        continue;
                    }

                    if (inner.Trim().TrimEnd('/').Trim().Equals("br", StringComparison.OrdinalIgnoreCase))
                    {
                        Flush();
                        runs.Add(new MarkdownRun("\n", MarkdownRunKind.LineBreak, style) { Href = href });
                        i = end + 1;
                        continue;
                    }
                }
            }

            if (ch == '!' && i + 1 < text.Length && text[i + 1] == '[' && href is null
                && TryLink(text, i + 1, out var imageAlt, out _, out _, out var imageEnd))
            {
                // images are not shown: their alt text is
                sb.Append(imageAlt);
                i = imageEnd;
                continue;
            }

            if (ch == '[' && href is null && TryLink(text, i, out var label, out var destination, out var linkTitle, out var linkEnd))
            {
                Flush();
                ParseInlines(label, style, destination, linkTitle, runs);
                i = linkEnd;
                continue;
            }

            if (ch == '&' && TryEntity(text, i, out var decoded, out var entityEnd))
            {
                sb.Append(decoded);
                i = entityEnd;
                continue;
            }

            if (ch is '*' or '_' or '~')
            {
                var count = CountRun(text, i, ch);
                if (TryEmphasis(text, i, ch, count, out var inner, out var addStyle, out var emphasisEnd))
                {
                    Flush();
                    ParseInlines(inner, style | addStyle, href, title, runs);
                    i = emphasisEnd;
                    continue;
                }

                sb.Append(text, i, count);
                i += count;
                continue;
            }

            sb.Append(ch);
            i++;
        }

        Flush();
    }

    // joins neighbouring runs of the same kind, style and link
    private static List<MarkdownRun> Merge(List<MarkdownRun> runs)
    {
        var result = new List<MarkdownRun>(runs.Count);
        foreach (var run in runs)
        {
            if (result.Count > 0 && result[^1] is var last
                && last.Kind == MarkdownRunKind.Text && run.Kind == MarkdownRunKind.Text
                && last.Style == run.Style && last.Href == run.Href && last.Title == run.Title)
            {
                result[^1] = last with { Text = last.Text + run.Text };
            }
            else
            {
                result.Add(run);
            }
        }

        return result;
    }

    private static int CountRun(string text, int i, char ch)
    {
        var n = 0;
        while (i + n < text.Length && text[i + n] == ch)
        {
            n++;
        }

        return n;
    }

    private static int FindBacktickRun(string text, int from, int length)
    {
        var i = from;
        while (i < text.Length)
        {
            if (text[i] == '`')
            {
                var n = CountRun(text, i, '`');
                if (n == length)
                {
                    return i;
                }

                i += n;
            }
            else
            {
                i++;
            }
        }

        return -1;
    }

    private static bool IsAutolink(string inner)
    {
        if (inner.Length == 0 || inner.Contains(' ') || inner.Contains('<'))
        {
            return false;
        }

        var colon = inner.IndexOf(':');
        if (colon >= 2 && colon <= 32 && char.IsAsciiLetter(inner[0]))
        {
            for (var k = 1; k < colon; k++)
            {
                if (!(char.IsAsciiLetterOrDigit(inner[k]) || inner[k] is '+' or '.' or '-'))
                {
                    return false;
                }
            }

            return true;
        }

        var at = inner.IndexOf('@');
        return at > 0 && inner.IndexOf('.', at) > at + 1;
    }

    // [label](destination "title") starting at the [
    private static bool TryLink(string text, int open, out string label, out string destination, out string? title, out int end)
    {
        label = destination = string.Empty;
        title = null;
        end = open;

        // the matching ] (brackets nest; code spans and escapes are skipped)
        var depth = 0;
        var i = open;
        var close = -1;
        while (i < text.Length)
        {
            var c = text[i];
            if (c == '\\')
            {
                i += 2;
                continue;
            }

            if (c == '`')
            {
                var n = CountRun(text, i, '`');
                var codeEnd = FindBacktickRun(text, i + n, n);
                i = codeEnd >= 0 ? codeEnd + n : i + n;
                continue;
            }

            if (c == '[')
            {
                depth++;
            }
            else if (c == ']')
            {
                depth--;
                if (depth == 0)
                {
                    close = i;
                    break;
                }
            }

            i++;
        }

        if (close < 0 || close + 1 >= text.Length || text[close + 1] != '(')
        {
            return false;
        }

        // the destination: <...> or up to a space or the balancing )
        var j = close + 2;
        while (j < text.Length && text[j] is ' ' or '\n')
        {
            j++;
        }

        var dest = new StringBuilder();
        if (j < text.Length && text[j] == '<')
        {
            var gt = text.IndexOf('>', j + 1);
            if (gt < 0)
            {
                return false;
            }

            dest.Append(text, j + 1, gt - j - 1);
            j = gt + 1;
        }
        else
        {
            var parens = 0;
            while (j < text.Length)
            {
                var c = text[j];
                if (c == '\\' && j + 1 < text.Length && !char.IsAsciiLetterOrDigit(text[j + 1]))
                {
                    dest.Append(text[j + 1]);
                    j += 2;
                    continue;
                }

                if (c is ' ' or '\n')
                {
                    break;
                }

                if (c == '(')
                {
                    parens++;
                }
                else if (c == ')')
                {
                    if (parens == 0)
                    {
                        break;
                    }

                    parens--;
                }

                dest.Append(c);
                j++;
            }
        }

        while (j < text.Length && text[j] is ' ' or '\n')
        {
            j++;
        }

        if (j < text.Length && text[j] is '"' or '\'' or '(')
        {
            var closeQuote = text[j] == '(' ? ')' : text[j];
            var titleEnd = text.IndexOf(closeQuote, j + 1);
            if (titleEnd < 0)
            {
                return false;
            }

            title = text[(j + 1)..titleEnd];
            j = titleEnd + 1;
            while (j < text.Length && text[j] is ' ' or '\n')
            {
                j++;
            }
        }

        if (j >= text.Length || text[j] != ')')
        {
            return false;
        }

        label = text[(open + 1)..close];
        destination = dest.ToString();
        end = j + 1;
        return true;
    }

    private static bool TryEntity(string text, int i, out string decoded, out int end)
    {
        decoded = string.Empty;
        end = i;
        var semicolon = text.IndexOf(';', i + 1);
        if (semicolon < 0 || semicolon - i > 32)
        {
            return false;
        }

        var entity = text[i..(semicolon + 1)];
        if (entity.Length < 3)
        {
            return false;
        }

        if (entity[1] == '#')
        {
            var isHex = entity.Length > 3 && entity[2] is 'x' or 'X';
            var digits = entity[(isHex ? 3 : 2)..^1];
            if (digits.Length == 0
                || !int.TryParse(digits, isHex ? NumberStyles.AllowHexSpecifier : NumberStyles.None, CultureInfo.InvariantCulture, out var code)
                || code < 0 || code > 0x10FFFF)
            {
                return false;
            }

            decoded = code == 0 ? "�" : char.ConvertFromUtf32(code is >= 0xD800 and <= 0xDFFF ? 0xFFFD : code);
            end = semicolon + 1;
            return true;
        }

        for (var k = 1; k < entity.Length - 1; k++)
        {
            if (!char.IsAsciiLetterOrDigit(entity[k]))
            {
                return false;
            }
        }

        var value = WebUtility.HtmlDecode(entity);
        if (value == entity)
        {
            return false;
        }

        decoded = value;
        end = semicolon + 1;
        return true;
    }

    // ** __ (bold), * _ (italic), *** ___ (both), ~~ (strikethrough): an opener followed by a non-space, a closer of
    // the same run length preceded by a non-space; _ does not open or close inside a word
    private static bool TryEmphasis(string text, int i, char ch, int count, out string inner, out MarkdownRunStyle style, out int end)
    {
        inner = string.Empty;
        style = MarkdownRunStyle.None;
        end = i;

        int length;
        if (ch == '~')
        {
            if (count != 2)
            {
                return false;
            }

            length = 2;
            style = MarkdownRunStyle.Strikethrough;
        }
        else
        {
            length = Math.Min(count, 3);
            style = length switch
            {
                1 => MarkdownRunStyle.Italic,
                2 => MarkdownRunStyle.Bold,
                _ => MarkdownRunStyle.Bold | MarkdownRunStyle.Italic,
            };
        }

        var after = i + count;
        if (after >= text.Length || char.IsWhiteSpace(text[after]))
        {
            return false;
        }

        if (ch == '_' && i > 0 && char.IsLetterOrDigit(text[i - 1]))
        {
            return false;
        }

        // the opener uses the last `length` characters of the run: leading extra ones stay text
        var openEnd = i + count;
        var k = openEnd;
        while (k < text.Length)
        {
            var c = text[k];
            if (c == '\\')
            {
                k += 2;
                continue;
            }

            if (c == '`')
            {
                var n = CountRun(text, k, '`');
                var codeEnd = FindBacktickRun(text, k + n, n);
                k = codeEnd >= 0 ? codeEnd + n : k + n;
                continue;
            }

            if (c == ch)
            {
                var n = CountRun(text, k, ch);
                var precededBySpace = char.IsWhiteSpace(text[k - 1]);
                var followedByWord = k + n < text.Length && char.IsLetterOrDigit(text[k + n]);
                if (n == length && !precededBySpace && !(ch == '_' && followedByWord) && k > openEnd)
                {
                    if (count > length)
                    {
                        // e.g. **** with length 3: keep it simple, not emphasis
                        return false;
                    }

                    inner = text[openEnd..k];
                    end = k + length;
                    return true;
                }

                k += n;
                continue;
            }

            k++;
        }

        return false;
    }
}
