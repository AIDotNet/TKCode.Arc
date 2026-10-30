using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace TKCode.Arc;

/// <summary>Signing of test events: HMAC SHA-256 of <c>{timestamp}.{body}</c>, sent as <c>t=…,v1=…</c>.</summary>
internal static class WebhookTesterSigner
{
    /// <summary>The lowercase hex HMAC SHA-256 of <c>timestamp.body</c> under <paramref name="secret"/>.</summary>
    public static string Sign(string secret, long timestamp, string body)
    {
        var key = Encoding.UTF8.GetBytes(secret ?? string.Empty);
        var message = Encoding.UTF8.GetBytes(timestamp.ToString(CultureInfo.InvariantCulture) + "." + body);
        return Convert.ToHexString(HMACSHA256.HashData(key, message)).ToLowerInvariant();
    }

    /// <summary>The header value for a signature: <c>t=timestamp,v1=hex</c>.</summary>
    public static string HeaderValue(long timestamp, string signature) =>
        $"t={timestamp.ToString(CultureInfo.InvariantCulture)},v1={signature}";
}

/// <summary>The outcome of checking a payload.</summary>
internal readonly record struct WebhookTesterJsonCheck(bool IsValid, bool IsEmpty, int Bytes, int Line, string? Message);

/// <summary>The kinds of text the editor tints.</summary>
internal enum WebhookTesterSyntax
{
    Plain,
    Key,
    String,
    Number,
    Literal,
    Punctuation
}

/// <summary>JSON helpers of the payload editor: validation, formatting, tinting and indentation.</summary>
internal static class WebhookTesterJson
{
    /// <summary>Parses <paramref name="text"/> and reports its size in bytes and, when it does not parse, the line (1-based) and message.</summary>
    public static WebhookTesterJsonCheck Check(string? text)
    {
        text ??= string.Empty;
        var bytes = Encoding.UTF8.GetByteCount(text);
        if (string.IsNullOrWhiteSpace(text))
        {
            return new WebhookTesterJsonCheck(false, true, bytes, 1, null);
        }
        try
        {
            using var _ = JsonDocument.Parse(text);
            return new WebhookTesterJsonCheck(true, false, bytes, 0, null);
        }
        catch (JsonException ex)
        {
            return new WebhookTesterJsonCheck(false, false, bytes, (int)(ex.LineNumber ?? 0) + 1, ShortMessage(ex.Message));
        }
    }

    /// <summary>The parser's message without its position suffix ("... LineNumber: 3 | BytePositionInLine: 4.").</summary>
    private static string ShortMessage(string message)
    {
        var cut = message.IndexOf(" Path:", StringComparison.Ordinal);
        if (cut < 0)
        {
            cut = message.IndexOf(" LineNumber:", StringComparison.Ordinal);
        }
        return (cut > 0 ? message[..cut] : message).TrimEnd('.', ' ');
    }

    /// <summary>Pretty prints valid JSON with two-space indentation; text that does not parse is returned unchanged.</summary>
    public static string Format(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return text ?? string.Empty;
        }
        try
        {
            using var document = JsonDocument.Parse(text);
            using var stream = new MemoryStream();
            using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions
            {
                Indented = true,
                Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            }))
            {
                document.WriteTo(writer);
            }
            return Encoding.UTF8.GetString(stream.ToArray()).Replace("\r\n", "\n");
        }
        catch (JsonException)
        {
            return text;
        }
    }

    /// <summary>
    /// What Enter inserts at <paramref name="caret"/>: a line break and the indentation of the current line, two spaces deeper
    /// right after an opening brace or bracket.
    /// </summary>
    public static string NewLine(string text, int caret)
    {
        caret = Math.Clamp(caret, 0, text.Length);
        var lineStart = caret == 0 ? 0 : text.LastIndexOf('\n', caret - 1) + 1;
        var indent = 0;
        while (lineStart + indent < caret && text[lineStart + indent] == ' ')
        {
            indent++;
        }
        var before = caret - 1;
        while (before >= lineStart && text[before] == ' ')
        {
            before--;
        }
        var opens = before >= lineStart && (text[before] == '{' || text[before] == '[');
        return "\n" + new string(' ', indent + (opens ? 2 : 0));
    }

    /// <summary>Splits <paramref name="text"/> into tinted runs in one pass; the runs cover the whole text, in order.</summary>
    public static List<(int Start, int Length, WebhookTesterSyntax Kind)> Tokenize(string text)
    {
        var runs = new List<(int, int, WebhookTesterSyntax)>();
        var i = 0;
        while (i < text.Length)
        {
            var c = text[i];
            var start = i;
            WebhookTesterSyntax kind;
            if (c == '"')
            {
                i++;
                while (i < text.Length && text[i] != '"' && text[i] != '\n')
                {
                    i += text[i] == '\\' && i + 1 < text.Length && text[i + 1] != '\n' ? 2 : 1;
                }
                if (i < text.Length && text[i] == '"')
                {
                    i++;
                }
                var next = i;
                while (next < text.Length && (text[next] == ' ' || text[next] == '\t'))
                {
                    next++;
                }
                kind = next < text.Length && text[next] == ':' ? WebhookTesterSyntax.Key : WebhookTesterSyntax.String;
            }
            else if (c is '-' or (>= '0' and <= '9'))
            {
                i++;
                while (i < text.Length && (char.IsAsciiDigit(text[i]) || text[i] is '.' or 'e' or 'E' or '+' or '-'))
                {
                    i++;
                }
                kind = WebhookTesterSyntax.Number;
            }
            else if (char.IsAsciiLetter(c))
            {
                i++;
                while (i < text.Length && char.IsAsciiLetter(text[i]))
                {
                    i++;
                }
                var word = text.AsSpan(start, i - start);
                kind = word.SequenceEqual("true") || word.SequenceEqual("false") || word.SequenceEqual("null")
                    ? WebhookTesterSyntax.Literal
                    : WebhookTesterSyntax.Plain;
            }
            else if (c is '{' or '}' or '[' or ']' or ':' or ',')
            {
                i++;
                kind = WebhookTesterSyntax.Punctuation;
            }
            else
            {
                i++;
                while (i < text.Length && text[i] is ' ' or '\t' or '\n' or '\r')
                {
                    i++;
                }
                kind = WebhookTesterSyntax.Plain;
            }
            runs.Add((start, i - start, kind));
        }
        return runs;
    }

    /// <summary>The number of lines of <paramref name="text"/> (an empty text has one).</summary>
    public static int LineCount(string? text)
    {
        var lines = 1;
        if (text is not null)
        {
            foreach (var c in text)
            {
                if (c == '\n')
                {
                    lines++;
                }
            }
        }
        return lines;
    }

    /// <summary>Whether <paramref name="url"/> is an https URL, or an http URL for localhost.</summary>
    public static bool IsAllowedEndpoint(string? url)
    {
        if (string.IsNullOrWhiteSpace(url) || !Uri.TryCreate(url.Trim(), UriKind.Absolute, out var uri))
        {
            return false;
        }
        if (uri.Scheme == Uri.UriSchemeHttps)
        {
            return true;
        }
        return uri.Scheme == Uri.UriSchemeHttp
               && (uri.IsLoopback || uri.Host.Equals("localhost", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>The standard reason phrase of the statuses a test endpoint usually answers; empty for others.</summary>
    public static string Reason(int status) => status switch
    {
        200 => "OK",
        201 => "Created",
        202 => "Accepted",
        204 => "No Content",
        301 => "Moved Permanently",
        302 => "Found",
        400 => "Bad Request",
        401 => "Unauthorized",
        403 => "Forbidden",
        404 => "Not Found",
        408 => "Request Timeout",
        410 => "Gone",
        422 => "Unprocessable Entity",
        429 => "Too Many Requests",
        500 => "Internal Server Error",
        502 => "Bad Gateway",
        503 => "Service Unavailable",
        504 => "Gateway Timeout",
        _ => string.Empty
    };
}
