using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace TKCode.Arc;

/// <summary>The kind of a piece of a JSON line, which picks its tint.</summary>
internal enum ToolCallJsonTokenKind
{
    /// <summary>Braces, brackets, colons, commas and indentation (TextSecondary).</summary>
    Punctuation,

    /// <summary>An object key with its quotes (Foreground).</summary>
    Key,

    /// <summary>A string value with its quotes (Success mixed into Foreground).</summary>
    String,

    /// <summary>A number (Warning mixed into Foreground).</summary>
    Number,

    /// <summary>true, false and null (TextMuted).</summary>
    Literal
}

/// <summary>A piece of a JSON line: its text and kind.</summary>
internal readonly record struct ToolCallJsonToken(string Text, ToolCallJsonTokenKind Kind);

/// <summary>
/// Formatting and tinting for the JSON views: a node becomes indented text (two spaces, relaxed escaping so quotes and
/// non-ASCII text read as written) and each line is split into tinted pieces by a small tokenizer of our own. Pure
/// functions: no reflection, no UI.
/// </summary>
internal static class ToolCallJson
{
    private static readonly JsonWriterOptions s_writer = new()
    {
        Indented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    /// <summary>The node as indented JSON text ("null" for null), lines separated by '\n'.</summary>
    public static string Format(JsonNode? node)
    {
        if (node is null)
        {
            return "null";
        }
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream, s_writer))
        {
            node.WriteTo(writer);
        }
        return Encoding.UTF8.GetString(stream.ToArray()).Replace("\r\n", "\n");
    }

    /// <summary>A JSON object of string values in the given order (the Input copy text).</summary>
    public static string FormatObject(IEnumerable<KeyValuePair<string, string>> pairs)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream, s_writer))
        {
            writer.WriteStartObject();
            foreach (var (key, value) in pairs)
            {
                writer.WriteString(key, value);
            }
            writer.WriteEndObject();
        }
        return Encoding.UTF8.GetString(stream.ToArray()).Replace("\r\n", "\n");
    }

    /// <summary>Splits indented JSON <paramref name="text"/> into lines of tinted pieces.</summary>
    public static ToolCallJsonToken[][] Tokenize(string text)
    {
        var lines = text.Split('\n');
        var result = new ToolCallJsonToken[lines.Length][];
        for (var i = 0; i < lines.Length; i++)
        {
            result[i] = TokenizeLine(lines[i]);
        }
        return result;
    }

    /// <summary>Splits one line into pieces: runs of punctuation and spaces, strings (keys when a colon follows), numbers
    /// and the literals true, false and null. Text it does not recognize is kept as punctuation, so nothing is lost.</summary>
    public static ToolCallJsonToken[] TokenizeLine(string line)
    {
        var tokens = new List<ToolCallJsonToken>(8);
        var plain = new StringBuilder();
        var i = 0;
        while (i < line.Length)
        {
            var c = line[i];
            if (c == '"')
            {
                var end = i + 1;
                while (end < line.Length && line[end] != '"')
                {
                    end += line[end] == '\\' ? 2 : 1;
                }
                end = Math.Min(end + 1, line.Length);
                var after = end;
                while (after < line.Length && line[after] == ' ')
                {
                    after++;
                }
                var kind = after < line.Length && line[after] == ':' ? ToolCallJsonTokenKind.Key : ToolCallJsonTokenKind.String;
                Flush(tokens, plain);
                tokens.Add(new ToolCallJsonToken(line[i..end], kind));
                i = end;
            }
            else if (c == '-' || char.IsAsciiDigit(c))
            {
                var end = i + 1;
                while (end < line.Length && (char.IsAsciiDigit(line[end]) || line[end] is '.' or 'e' or 'E' or '+' or '-'))
                {
                    end++;
                }
                Flush(tokens, plain);
                tokens.Add(new ToolCallJsonToken(line[i..end], ToolCallJsonTokenKind.Number));
                i = end;
            }
            else if (Literal(line, i) is { } literal)
            {
                Flush(tokens, plain);
                tokens.Add(new ToolCallJsonToken(literal, ToolCallJsonTokenKind.Literal));
                i += literal.Length;
            }
            else
            {
                plain.Append(c);
                i++;
            }
        }
        Flush(tokens, plain);
        return tokens.ToArray();
    }

    private static string? Literal(string line, int at)
    {
        foreach (var word in (ReadOnlySpan<string>)["true", "false", "null"])
        {
            if (string.CompareOrdinal(line, at, word, 0, word.Length) == 0)
            {
                var next = at + word.Length;
                if (next >= line.Length || !char.IsAsciiLetterOrDigit(line[next]))
                {
                    return word;
                }
            }
        }
        return null;
    }

    private static void Flush(List<ToolCallJsonToken> tokens, StringBuilder plain)
    {
        if (plain.Length == 0)
        {
            return;
        }
        tokens.Add(new ToolCallJsonToken(plain.ToString(), ToolCallJsonTokenKind.Punctuation));
        plain.Clear();
    }
}
