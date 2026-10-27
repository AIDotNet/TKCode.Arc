using System.Buffers;
using System.Globalization;
using System.Text;
using System.Text.Json;

namespace TKCode.Arc;

/// <summary>Pure rules of <see cref="Webhooks"/>: rates, counts, formats, URL checks and the traffic chart's buckets.</summary>
internal static class WebhookMath
{
    /// <summary>A status the endpoint did not accept: no answer (0) or 400 and up.</summary>
    public static bool IsFailureCode(int code) => code == 0 || code >= 400;

    /// <summary>The host of <paramref name="url"/> ("relay.tallowfield.dev"); the text itself when it has no host.</summary>
    public static string HostOf(string? url)
    {
        var (host, _) = SplitUrl(url);
        return host;
    }

    /// <summary>
    /// The URL without its scheme, cut into the host (with a non-default port) and the rest ("/hooks/corvid/v2"), as the
    /// endpoint row shows it.
    /// </summary>
    public static (string Host, string Path) SplitUrl(string? url)
    {
        var text = (url ?? string.Empty).Trim();
        var scheme = text.IndexOf("://", StringComparison.Ordinal);
        if (scheme >= 0)
        {
            text = text[(scheme + 3)..];
        }
        var slash = text.IndexOfAny(['/', '?', '#']);
        return slash < 0 ? (text, string.Empty) : (text[..slash], text[slash..]);
    }

    /// <summary>The sums of a traffic chart.</summary>
    public static (int Total, int Failed) Sum(IEnumerable<WebhookVolumeBucket> volume)
    {
        var total = 0;
        var failed = 0;
        foreach (var bucket in volume)
        {
            total += Math.Max(0, bucket.Total);
            failed += Math.Max(0, bucket.Failed);
        }
        return (total, failed);
    }

    /// <summary>
    /// The delivered share, rounded down to a tenth of a percent (Arc: 294 of 297 shows 98.9%, not 99.0%). It counts the
    /// chart when it has events, else the deliveries; null when there is neither.
    /// </summary>
    public static double? Rate(int total, int failed, int deliveries, int failingDeliveries)
    {
        if (total > 0)
        {
            return Floor(total - Math.Min(failed, total), total);
        }
        if (deliveries > 0)
        {
            return Floor(deliveries - Math.Min(failingDeliveries, deliveries), deliveries);
        }
        return null;
    }

    private static double Floor(int good, int all) => Math.Floor(good * 1000.0 / all + 1e-9) / 1000.0;

    /// <summary>"98.9%", or "–" when nothing was sent.</summary>
    public static string FormatRate(double? rate, CultureInfo? culture = null)
    {
        if (rate is not { } value)
        {
            return "–";
        }
        return (value * 100).ToString("0.0", culture ?? CultureInfo.CurrentCulture) + "%";
    }

    /// <summary>"2,318 ms" below ten seconds, "30.0 s" from ten seconds up (Arc).</summary>
    public static string FormatDuration(TimeSpan duration, CultureInfo? culture = null)
    {
        var c = culture ?? CultureInfo.CurrentCulture;
        return duration.TotalMilliseconds >= 10000
            ? duration.TotalSeconds.ToString("0.0", c) + " s"
            : Math.Round(Math.Max(0, duration.TotalMilliseconds)).ToString("N0", c) + " ms";
    }

    /// <summary>"15:42:10".</summary>
    public static string FormatTime(DateTimeOffset time) => time.ToString("HH:mm:ss", CultureInfo.InvariantCulture);

    /// <summary>"15:30".</summary>
    public static string FormatClock(DateTimeOffset time) => time.ToString("HH:mm", CultureInfo.InvariantCulture);

    /// <summary>The usual reason phrase of an HTTP status; empty for codes it does not know.</summary>
    public static string StatusTextFor(int code) => code switch
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
        405 => "Method Not Allowed",
        408 => "Request Timeout",
        409 => "Conflict",
        410 => "Gone",
        413 => "Content Too Large",
        422 => "Unprocessable Content",
        429 => "Too Many Requests",
        500 => "Internal Server Error",
        501 => "Not Implemented",
        502 => "Bad Gateway",
        503 => "Service Unavailable",
        504 => "Gateway Timeout",
        _ => string.Empty,
    };

    /// <summary>The status text of <paramref name="delivery"/>: its own, the standard phrase, or <paramref name="noResponse"/> for 0.</summary>
    public static string StatusTextOf(WebhookDelivery delivery, string noResponse)
    {
        if (!string.IsNullOrWhiteSpace(delivery.StatusText))
        {
            return delivery.StatusText!;
        }
        return delivery.StatusCode == 0 ? noResponse : StatusTextFor(delivery.StatusCode);
    }

    /// <summary>The badge text of a status: the code, or "—" when the endpoint did not answer.</summary>
    public static string CodeText(int code) => code == 0 ? "—" : code.ToString(CultureInfo.InvariantCulture);

    /// <summary>
    /// The built-in URL check (Arc): empty → <paramref name="required"/>; not https, no host, or a localhost host →
    /// <paramref name="invalid"/>; otherwise null. IP addresses and test domains are accepted, as in Arc.
    /// </summary>
    public static string? ValidateUrl(string? url, string required, string invalid)
    {
        var text = (url ?? string.Empty).Trim();
        if (text.Length == 0)
        {
            return required;
        }
        if (!Uri.TryCreate(text, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps ||
            string.IsNullOrEmpty(uri.Host))
        {
            return invalid;
        }
        var host = uri.Host.ToLowerInvariant();
        if (host == "localhost" || host.EndsWith(".localhost", StringComparison.Ordinal))
        {
            return invalid;
        }
        return null;
    }

    /// <summary>The index of the bucket that holds <paramref name="time"/>: the last one for later times, the first for earlier ones; -1 when empty.</summary>
    public static int BucketIndexFor(IReadOnlyList<WebhookVolumeBucket> volume, DateTimeOffset time)
    {
        if (volume.Count == 0)
        {
            return -1;
        }
        for (var i = 0; i < volume.Count; i++)
        {
            if (time >= volume[i].Start && time < volume[i].End)
            {
                return i;
            }
        }
        return time < volume[0].Start ? 0 : volume.Count - 1;
    }

    /// <summary>The busiest bucket (the first of equals); -1 when there is no traffic.</summary>
    public static int Busiest(IReadOnlyList<WebhookVolumeBucket> volume)
    {
        var best = -1;
        var max = 0;
        for (var i = 0; i < volume.Count; i++)
        {
            if (volume[i].Total > max)
            {
                max = volume[i].Total;
                best = i;
            }
        }
        return best;
    }

    /// <summary>The first <paramref name="max"/> events and how many more there are ("+N").</summary>
    public static (IReadOnlyList<string> Shown, int More) Chips(IReadOnlyList<string> events, int max)
    {
        var limit = Math.Max(0, max);
        if (events.Count <= limit)
        {
            return (events, 0);
        }
        return (events.Take(limit).ToList(), events.Count - limit);
    }

    /// <summary>The delivery the inspector shows first: the first open failure, else the first delivery.</summary>
    public static WebhookDelivery? DefaultDelivery(IReadOnlyList<WebhookDelivery> deliveries)
    {
        foreach (var delivery in deliveries)
        {
            if (delivery.IsOpenFailure)
            {
                return delivery;
            }
        }
        return deliveries.Count > 0 ? deliveries[0] : null;
    }

    /// <summary>The headers as "Name: value" lines (the inspector's Copy on the Headers tab).</summary>
    public static string HeadersText(IEnumerable<WebhookHeader> headers)
    {
        var builder = new StringBuilder();
        foreach (var header in headers)
        {
            if (builder.Length > 0)
            {
                builder.Append('\n');
            }
            builder.Append(header.Name).Append(": ").Append(header.Value);
        }
        return builder.ToString();
    }

    /// <summary>A random signing secret for the simulated create: <paramref name="prefix"/> and 32 letters and digits.</summary>
    public static string RandomSecret(string prefix = "sig_", Random? random = null)
    {
        const string alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnopqrstuvwxyz23456789";
        var r = random ?? Random.Shared;
        var builder = new StringBuilder(prefix);
        for (var i = 0; i < 32; i++)
        {
            builder.Append(alphabet[r.Next(alphabet.Length)]);
        }
        return builder.ToString();
    }

    /// <summary>48 empty half-hour buckets ending at the half hour after <paramref name="now"/> (a new endpoint's chart).</summary>
    public static IEnumerable<WebhookVolumeBucket> EmptyDay(DateTimeOffset now, int count = 48)
    {
        var end = new DateTimeOffset(now.Year, now.Month, now.Day, now.Hour, now.Minute >= 30 ? 30 : 0, 0, now.Offset).AddMinutes(30);
        for (var i = count - 1; i >= 0; i--)
        {
            var start = end.AddMinutes(-30 * (i + 1));
            yield return new WebhookVolumeBucket { Start = start, End = start.AddMinutes(30) };
        }
    }
}

/// <summary>The kind of a piece of an indented JSON line (its color in the payload view).</summary>
internal enum WebhookJsonKind
{
    /// <summary>Braces, brackets, colons, commas and indentation (TextMuted).</summary>
    Punctuation,

    /// <summary>A property name with its quotes (Foreground).</summary>
    Key,

    /// <summary>A string value with its quotes (TextSecondary).</summary>
    String,

    /// <summary>A number (Accent).</summary>
    Number,

    /// <summary>true, false or null (Accent).</summary>
    Literal
}

/// <summary>A piece of an indented JSON line.</summary>
internal readonly record struct WebhookJsonSpan(string Text, WebhookJsonKind Kind);

/// <summary>Indents a JSON payload (two spaces) into colored lines without reflection (Utf8JsonReader).</summary>
internal static class WebhookJson
{
    /// <summary>The lines of <paramref name="json"/> indented, or null when it is not JSON.</summary>
    public static IReadOnlyList<IReadOnlyList<WebhookJsonSpan>>? Format(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }
        var lines = new List<List<WebhookJsonSpan>>();
        var bytes = Encoding.UTF8.GetBytes(json);
        var reader = new Utf8JsonReader(bytes, new JsonReaderOptions
        {
            CommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true,
        });
        // Per open container: whether it has no item yet.
        var empty = new Stack<bool>();
        var afterName = false;
        try
        {
            while (reader.Read())
            {
                var depth = empty.Count;
                switch (reader.TokenType)
                {
                    case JsonTokenType.StartObject:
                    case JsonTokenType.StartArray:
                    {
                        var open = reader.TokenType == JsonTokenType.StartObject ? "{" : "[";
                        if (afterName)
                        {
                            lines[^1].Add(new WebhookJsonSpan(open, WebhookJsonKind.Punctuation));
                        }
                        else
                        {
                            NewItemLine(lines, empty, depth).Add(new WebhookJsonSpan(open, WebhookJsonKind.Punctuation));
                        }
                        afterName = false;
                        empty.Push(true);
                        break;
                    }
                    case JsonTokenType.EndObject:
                    case JsonTokenType.EndArray:
                    {
                        var close = reader.TokenType == JsonTokenType.EndObject ? "}" : "]";
                        var wasEmpty = empty.Pop();
                        if (wasEmpty)
                        {
                            lines[^1].Add(new WebhookJsonSpan(close, WebhookJsonKind.Punctuation));
                        }
                        else
                        {
                            var line = new List<WebhookJsonSpan>();
                            Indent(line, empty.Count);
                            line.Add(new WebhookJsonSpan(close, WebhookJsonKind.Punctuation));
                            lines.Add(line);
                        }
                        break;
                    }
                    case JsonTokenType.PropertyName:
                    {
                        var line = NewItemLine(lines, empty, depth);
                        line.Add(new WebhookJsonSpan("\"" + Raw(ref reader) + "\"", WebhookJsonKind.Key));
                        line.Add(new WebhookJsonSpan(": ", WebhookJsonKind.Punctuation));
                        afterName = true;
                        break;
                    }
                    default:
                    {
                        var span = reader.TokenType switch
                        {
                            JsonTokenType.String => new WebhookJsonSpan("\"" + Raw(ref reader) + "\"", WebhookJsonKind.String),
                            JsonTokenType.Number => new WebhookJsonSpan(Raw(ref reader), WebhookJsonKind.Number),
                            JsonTokenType.True => new WebhookJsonSpan("true", WebhookJsonKind.Literal),
                            JsonTokenType.False => new WebhookJsonSpan("false", WebhookJsonKind.Literal),
                            _ => new WebhookJsonSpan("null", WebhookJsonKind.Literal),
                        };
                        if (afterName)
                        {
                            lines[^1].Add(span);
                        }
                        else
                        {
                            NewItemLine(lines, empty, depth).Add(span);
                        }
                        afterName = false;
                        break;
                    }
                }
            }
        }
        catch (JsonException)
        {
            return null;
        }
        if (empty.Count != 0 || lines.Count == 0)
        {
            return null;
        }
        return lines;
    }

    /// <summary>The indented text of <paramref name="lines"/> (what Copy puts on the clipboard).</summary>
    public static string ToText(IReadOnlyList<IReadOnlyList<WebhookJsonSpan>> lines)
    {
        var builder = new StringBuilder();
        for (var i = 0; i < lines.Count; i++)
        {
            if (i > 0)
            {
                builder.Append('\n');
            }
            foreach (var span in lines[i])
            {
                builder.Append(span.Text);
            }
        }
        return builder.ToString();
    }

    private static List<WebhookJsonSpan> NewItemLine(List<List<WebhookJsonSpan>> lines, Stack<bool> empty, int depth)
    {
        if (empty.Count > 0)
        {
            if (!empty.Peek())
            {
                lines[^1].Add(new WebhookJsonSpan(",", WebhookJsonKind.Punctuation));
            }
            empty.Pop();
            empty.Push(false);
        }
        var line = new List<WebhookJsonSpan>();
        Indent(line, depth);
        lines.Add(line);
        return line;
    }

    private static void Indent(List<WebhookJsonSpan> line, int depth)
    {
        if (depth > 0)
        {
            line.Add(new WebhookJsonSpan(new string(' ', depth * 2), WebhookJsonKind.Punctuation));
        }
    }

    private static string Raw(ref Utf8JsonReader reader) =>
        reader.HasValueSequence
            ? Encoding.UTF8.GetString(reader.ValueSequence.ToArray())
            : Encoding.UTF8.GetString(reader.ValueSpan);
}
