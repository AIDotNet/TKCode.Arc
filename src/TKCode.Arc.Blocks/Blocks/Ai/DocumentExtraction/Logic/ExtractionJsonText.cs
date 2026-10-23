using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace TKCode.Arc;

/// <summary>What a piece of a JSON line is, for its color (spec 3.5).</summary>
internal enum JsonTokenKind
{
    /// <summary>Braces, brackets, commas and colons (TextSecondary).</summary>
    Punctuation,

    /// <summary>A property name with its quotes (Foreground).</summary>
    Key,

    /// <summary>A string value with its quotes (78% Success with Foreground).</summary>
    String,

    /// <summary>A number, and <c>true</c> (80% Series1 with Foreground).</summary>
    Number,

    /// <summary><c>null</c> and <c>false</c> (TextMuted).</summary>
    Muted
}

/// <summary>A colored piece of a JSON line: <paramref name="Length"/> characters from <paramref name="Start"/>.</summary>
internal readonly record struct JsonToken(int Start, int Length, JsonTokenKind Kind);

/// <summary>A line of the JSON view: its text, its colored pieces and the value path it shows (null for structure lines).</summary>
internal sealed class ExtractionJsonLine
{
    /// <summary>Creates a line.</summary>
    public ExtractionJsonLine(string text, string? path, JsonToken[] tokens)
    {
        Text = text;
        Path = path;
        Tokens = tokens;
    }

    /// <summary>The text, indentation included.</summary>
    public string Text { get; }

    /// <summary>
    /// The path of the value on this line: a field key (<c>vendor</c>), a cell (<c>lineItems.0.amount</c>) or, on the line
    /// that opens a keyed array or object, its key (<c>lineItems</c>); null for braces, brackets and row objects.
    /// </summary>
    public string? Path { get; }

    /// <summary>The colored pieces in order (spaces are not covered).</summary>
    public IReadOnlyList<JsonToken> Tokens { get; }
}

/// <summary>
/// Cuts indented JSON (as <see cref="ExtractionOutput.ToJson"/> and <see cref="ExtractionSchema.ToJsonSchema"/> write it,
/// one value per line) into lines with colored pieces and, for the output, the value path of each line. The view works
/// from the same text the Copy button copies, so the two never differ.
/// </summary>
internal static class ExtractionJsonText
{
    /// <summary>The lines of <paramref name="json"/>; with <paramref name="mapPaths"/> each value line knows its path.</summary>
    public static ExtractionJsonLine[] Split(string json, bool mapPaths)
    {
        if (json.Length == 0)
        {
            return [];
        }
        var texts = json.Replace("\r\n", "\n").Split('\n');
        var lines = new ExtractionJsonLine[texts.Length];
        var frames = new List<Frame>();
        var tokens = new List<JsonToken>();
        for (var i = 0; i < texts.Length; i++)
        {
            var text = texts[i];
            tokens.Clear();
            Tokenize(text, tokens);
            var path = mapPaths ? Follow(text, tokens, frames) : null;
            lines[i] = new ExtractionJsonLine(text, path, tokens.ToArray());
        }
        return lines;
    }

    private static void Tokenize(string text, List<JsonToken> tokens)
    {
        var i = 0;
        while (i < text.Length)
        {
            var c = text[i];
            if (char.IsWhiteSpace(c))
            {
                i++;
                continue;
            }
            var start = i;
            if (c == '"')
            {
                i++;
                while (i < text.Length && text[i] != '"')
                {
                    i += text[i] == '\\' ? 2 : 1;
                }
                i = Math.Min(text.Length, i + 1);
                var next = i;
                while (next < text.Length && char.IsWhiteSpace(text[next]))
                {
                    next++;
                }
                tokens.Add(new JsonToken(start, i - start, next < text.Length && text[next] == ':' ? JsonTokenKind.Key : JsonTokenKind.String));
            }
            else if (c == '-' || char.IsAsciiDigit(c))
            {
                while (i < text.Length && (char.IsAsciiDigit(text[i]) || text[i] is '.' or 'e' or 'E' or '+' or '-'))
                {
                    i++;
                }
                tokens.Add(new JsonToken(start, i - start, JsonTokenKind.Number));
            }
            else if (char.IsAsciiLetter(c))
            {
                while (i < text.Length && char.IsAsciiLetter(text[i]))
                {
                    i++;
                }
                var word = text.AsSpan(start, i - start);
                tokens.Add(new JsonToken(start, i - start, word.SequenceEqual("true") ? JsonTokenKind.Number : JsonTokenKind.Muted));
            }
            else
            {
                i++;
                tokens.Add(new JsonToken(start, 1, JsonTokenKind.Punctuation));
            }
        }
    }

    /// <summary>Follows the nesting through one line and returns the path of the value it shows, if any.</summary>
    private static string? Follow(string text, List<JsonToken> tokens, List<Frame> frames)
    {
        if (tokens.Count == 0)
        {
            return null;
        }
        var at = 0;
        string? key = null;
        if (tokens[0].Kind == JsonTokenKind.Key)
        {
            key = Unescape(text.AsSpan(tokens[0].Start + 1, Math.Max(0, tokens[0].Length - 2)));
            at = tokens.Count > 1 && IsPunctuation(text, tokens[1], ':') ? 2 : 1;
            if (at >= tokens.Count)
            {
                return null;
            }
        }
        var first = tokens[at];
        if (first.Kind == JsonTokenKind.Punctuation && text[first.Start] is '}' or ']')
        {
            if (frames.Count > 0)
            {
                frames.RemoveAt(frames.Count - 1);
            }
            return null;
        }
        var parent = frames.Count > 0 ? frames[^1] : null;
        string? path = null;
        if (parent is not null)
        {
            if (parent.IsArray)
            {
                path = Join(parent.Path, parent.NextIndex.ToString(CultureInfo.InvariantCulture));
                parent.NextIndex++;
            }
            else if (key is not null)
            {
                path = Join(parent.Path, key);
            }
        }
        if (first.Kind == JsonTokenKind.Punctuation && text[first.Start] is '{' or '[')
        {
            var closesHere = at + 1 < tokens.Count && IsPunctuation(text, tokens[at + 1], text[first.Start] == '{' ? '}' : ']');
            if (!closesHere)
            {
                frames.Add(new Frame(path, text[first.Start] == '['));
            }
            // A keyed container's line is its value's line; a row object of an array is structure.
            return key is not null ? path : null;
        }
        return path;
    }

    private static bool IsPunctuation(string text, JsonToken token, char c) =>
        token.Kind == JsonTokenKind.Punctuation && text[token.Start] == c;

    private static string Join(string? parent, string part) => parent is null ? part : parent + "." + part;

    private static string Unescape(ReadOnlySpan<char> s)
    {
        if (s.IndexOf('\\') < 0)
        {
            return s.ToString();
        }
        var result = new StringBuilder(s.Length);
        for (var i = 0; i < s.Length; i++)
        {
            if (s[i] != '\\' || i + 1 >= s.Length)
            {
                result.Append(s[i]);
                continue;
            }
            var e = s[++i];
            switch (e)
            {
                case 'b':
                    result.Append('\b');
                    break;
                case 'f':
                    result.Append('\f');
                    break;
                case 'n':
                    result.Append('\n');
                    break;
                case 'r':
                    result.Append('\r');
                    break;
                case 't':
                    result.Append('\t');
                    break;
                case 'u' when i + 4 < s.Length && int.TryParse(s.Slice(i + 1, 4), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var code):
                    result.Append((char)code);
                    i += 4;
                    break;
                default:
                    result.Append(e);
                    break;
            }
        }
        return result.ToString();
    }

    /// <summary>An open object or array: the path of its value and, for an array, the index of its next item.</summary>
    private sealed class Frame
    {
        public Frame(string? path, bool isArray)
        {
            Path = path;
            IsArray = isArray;
        }

        public string? Path { get; }

        public bool IsArray { get; }

        public int NextIndex { get; set; }
    }
}
