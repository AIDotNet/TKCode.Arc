namespace TKCode.Arc;

/// <summary>One word of a snippet: an identifier or number, a string literal, a comment, or one punctuation mark.</summary>
/// <param name="Text">The characters of the word.</param>
/// <param name="Line">The zero-based line.</param>
/// <param name="Column">The zero-based column of the first character.</param>
internal readonly record struct CodeMagicMoveWord(string Text, int Line, int Column)
{
    /// <summary>The column after the last character.</summary>
    public int End => Column + Text.Length;
}

/// <summary>
/// A snippet split into the words that move: tabs become two spaces, line endings are normalised, and whitespace is not
/// a word (indentation and gaps come from the columns).
/// </summary>
internal sealed class CodeMagicMoveSource
{
    private CodeMagicMoveSource(string text, string[] lines, CodeMagicMoveWord[] words)
    {
        Text = text;
        Lines = lines;
        Words = words;
        var widest = 0;
        foreach (var line in lines)
        {
            widest = Math.Max(widest, line.TrimEnd().Length);
        }
        MaxColumns = widest;
    }

    /// <summary>An empty snippet (one empty line).</summary>
    public static CodeMagicMoveSource Empty { get; } = Parse(string.Empty);

    /// <summary>The normalised code.</summary>
    public string Text { get; }

    /// <summary>The lines of <see cref="Text"/>.</summary>
    public string[] Lines { get; }

    /// <summary>The words in reading order.</summary>
    public CodeMagicMoveWord[] Words { get; }

    /// <summary>The number of lines (at least one).</summary>
    public int LineCount => Lines.Length;

    /// <summary>The length of the longest line without trailing spaces.</summary>
    public int MaxColumns { get; }

    /// <summary>Each word's syntax color as ARGB (0 for the plain text color), or null before highlighting.</summary>
    public uint[]? Colors { get; set; }

    /// <summary>Splits <paramref name="code"/> into lines and words.</summary>
    public static CodeMagicMoveSource Parse(string? code)
    {
        var text = Normalize(code);
        var lines = text.Split('\n');
        var words = new List<CodeMagicMoveWord>();
        for (var line = 0; line < lines.Length; line++)
        {
            Split(lines[line], line, words);
        }
        return new CodeMagicMoveSource(text, lines, words.ToArray());
    }

    /// <summary>
    /// Tabs become two spaces, CR LF and CR become LF, trailing spaces and leading and trailing blank lines are dropped,
    /// and the indentation every line shares is removed.
    /// </summary>
    public static string Normalize(string? code)
    {
        if (string.IsNullOrEmpty(code))
        {
            return string.Empty;
        }
        var text = code.Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n')
            .Replace("\t", "  ", StringComparison.Ordinal);
        var lines = text.TrimEnd('\n', ' ').Split('\n');
        var first = 0;
        while (first < lines.Length - 1 && lines[first].Trim().Length == 0)
        {
            first++;
        }
        var indent = int.MaxValue;
        for (var i = first; i < lines.Length; i++)
        {
            var line = lines[i];
            var trimmed = line.TrimStart(' ');
            if (trimmed.Length > 0)
            {
                indent = Math.Min(indent, line.Length - trimmed.Length);
            }
        }
        if (indent == int.MaxValue)
        {
            indent = 0;
        }
        var builder = new System.Text.StringBuilder(text.Length);
        for (var i = first; i < lines.Length; i++)
        {
            if (i > first)
            {
                builder.Append('\n');
            }
            var line = lines[i].TrimEnd();
            builder.Append(line.Length >= indent ? line.AsSpan(indent) : ReadOnlySpan<char>.Empty);
        }
        return builder.ToString();
    }

    private static void Split(string line, int lineIndex, List<CodeMagicMoveWord> words)
    {
        var i = 0;
        while (i < line.Length)
        {
            var c = line[i];
            if (char.IsWhiteSpace(c))
            {
                i++;
                continue;
            }
            var start = i;
            if (IsWordChar(c))
            {
                while (i < line.Length && IsWordChar(line[i]))
                {
                    i++;
                }
            }
            else if (c is '"' or '\'' or '`')
            {
                // A string literal moves as one word (to the closing quote, or the end of the line).
                i++;
                while (i < line.Length && line[i] != c)
                {
                    i += line[i] == '\\' && i + 1 < line.Length ? 2 : 1;
                }
                i = Math.Min(line.Length, i + 1);
            }
            else if (c == '/' && i + 1 < line.Length && line[i + 1] == '/')
            {
                // A line comment is one word: it stays or it is typed again.
                i = line.TrimEnd().Length;
            }
            else
            {
                i++;
            }
            words.Add(new CodeMagicMoveWord(line[start..i], lineIndex, start));
        }
    }

    /// <summary>Letters, digits, '_' and '$' make up identifiers and numbers.</summary>
    public static bool IsWordChar(char c) => char.IsLetterOrDigit(c) || c is '_' or '$';
}
