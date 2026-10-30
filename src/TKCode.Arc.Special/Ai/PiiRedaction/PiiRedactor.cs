using System.Text;
using System.Text.RegularExpressions;

namespace TKCode.Arc;

/// <summary>
/// The pure helpers behind <see cref="PiiRedaction"/>, safe to run on a server without the card. <see cref="Detect"/>
/// finds emails, card numbers (13 to 19 digits that pass the Luhn check), phone numbers (7 to 15 digits) and names (the
/// names you pass, plus simple introductions such as "I'm", "this is" and "my name is"); <see cref="Redact"/> swaps the
/// spans for numbered placeholders and gives a repeated value the same token; <see cref="Restore"/> puts the values back
/// into a reply and leaves unknown tokens as they are. The detector is a baseline: pass a DLP or NER service for
/// anything that has to be compliant.
/// </summary>
public static partial class PiiRedactor
{
    /// <summary>
    /// Finds the personal data in <paramref name="text"/>, sorted by position, without overlaps (an earlier span wins,
    /// and of two spans that start together the longer one). Linear in the text's length.
    /// </summary>
    /// <param name="text">The message to scan.</param>
    /// <param name="names">Names that are always flagged (the signed-in person, their contacts); matched whole-word, ignoring case.</param>
    public static IReadOnlyList<PiiMatch> Detect(string? text, IEnumerable<string>? names = null)
    {
        if (string.IsNullOrEmpty(text))
        {
            return [];
        }
        var found = new List<PiiMatch>();
        foreach (Match m in EmailPattern().Matches(text))
        {
            found.Add(new PiiMatch(PiiType.Email, m.Index, m.Index + m.Length));
        }
        foreach (Match m in DigitsPattern().Matches(text))
        {
            var digits = CountDigits(m.Value);
            if (digits is >= 13 and <= 19 && PassesLuhn(m.Value))
            {
                found.Add(new PiiMatch(PiiType.Card, m.Index, m.Index + m.Length));
            }
            else if (digits is >= 7 and <= 15 && !IsoDatePattern().IsMatch(m.Value))
            {
                found.Add(new PiiMatch(PiiType.Phone, m.Index, m.Index + m.Length));
            }
        }
        if (names is not null)
        {
            foreach (var name in names)
            {
                var trimmed = name?.Trim();
                if (string.IsNullOrEmpty(trimmed))
                {
                    continue;
                }
                var from = 0;
                while (from < text.Length)
                {
                    var at = text.IndexOf(trimmed, from, StringComparison.OrdinalIgnoreCase);
                    if (at < 0)
                    {
                        break;
                    }
                    var end = at + trimmed.Length;
                    if (IsWordBoundary(text, at - 1) && IsWordBoundary(text, end))
                    {
                        found.Add(new PiiMatch(PiiType.Name, at, end));
                    }
                    from = end;
                }
            }
        }
        foreach (Match m in IntroductionPattern().Matches(text))
        {
            var group = m.Groups["name"];
            found.Add(new PiiMatch(PiiType.Name, group.Index, group.Index + group.Length));
        }
        found.Sort(static (a, b) => a.Start != b.Start ? a.Start.CompareTo(b.Start) : b.Length.CompareTo(a.Length));
        var result = new List<PiiMatch>(found.Count);
        var reached = 0;
        foreach (var match in found)
        {
            if (match.Start >= reached && match.Length > 0)
            {
                result.Add(match);
                reached = match.End;
            }
        }
        return result;
    }

    /// <summary>
    /// Swaps every span in <paramref name="matches"/> for a numbered placeholder (<c>[NAME_1]</c>, <c>[EMAIL_2]</c>) and
    /// returns the safe prompt with every entity. Spans are taken in order of position; a span that overlaps an earlier
    /// one, or reaches outside the text, is skipped. The same value (ignoring case, and for phones and cards everything
    /// but the digits) keeps one token.
    /// </summary>
    /// <param name="text">The message to redact.</param>
    /// <param name="matches">Spans from <see cref="Detect"/> or your own detector.</param>
    public static PiiRedactionResult Redact(string? text, IEnumerable<PiiMatch>? matches)
    {
        text ??= string.Empty;
        var ordered = (matches ?? []).Select((m, i) => (Match: m, Index: i))
                                     .OrderBy(x => x.Match.Start).ThenBy(x => x.Index)
                                     .Select(x => x.Match);
        var entities = new List<PiiEntity>();
        var tokens = new Dictionary<(PiiType, string), string>();
        var counters = new int[4];
        var prompt = new StringBuilder(text.Length);
        var copied = 0;
        foreach (var match in ordered)
        {
            if (match.Start < copied || match.Start < 0 || match.End > text.Length || match.Length <= 0)
            {
                continue;
            }
            var value = text.Substring(match.Start, match.Length);
            var key = (match.Type, Normalize(match.Type, value));
            if (!tokens.TryGetValue(key, out var token))
            {
                token = TokenName(match.Type) + "_" + ++counters[(int)match.Type];
                tokens[key] = token;
            }
            prompt.Append(text, copied, match.Start - copied);
            var promptStart = prompt.Length;
            prompt.Append('[').Append(token).Append(']');
            entities.Add(new PiiEntity(match.Type, match.Start, match.End, value, token, promptStart));
            copied = match.End;
        }
        prompt.Append(text, copied, text.Length - copied);
        return new PiiRedactionResult(prompt.ToString(), entities);
    }

    /// <summary>
    /// Puts the original values back into a reply: every placeholder of a known entity (<c>[EMAIL_1]</c>) becomes its value.
    /// Unknown tokens and anything else stay as they are.
    /// </summary>
    /// <param name="reply">The model's reply, placeholders included.</param>
    /// <param name="entities">The entities from <see cref="Redact"/>.</param>
    public static string Restore(string? reply, IEnumerable<PiiEntity>? entities) => RestoreWithRanges(reply, entities, out _);

    /// <summary>The restored reply and where each value it put back now sits (the card marks them).</summary>
    internal static string RestoreWithRanges(string? reply, IEnumerable<PiiEntity>? entities, out List<PiiRestoredRange> ranges)
    {
        ranges = [];
        if (string.IsNullOrEmpty(reply))
        {
            return reply ?? string.Empty;
        }
        var values = new Dictionary<string, PiiEntity>(StringComparer.Ordinal);
        foreach (var entity in entities ?? [])
        {
            values.TryAdd(entity.Token, entity);
        }
        var builder = new StringBuilder(reply.Length);
        var copied = 0;
        foreach (Match m in PlaceholderPattern().Matches(reply))
        {
            var token = m.Groups["token"].Value;
            if (!values.TryGetValue(token, out var entity))
            {
                continue;
            }
            builder.Append(reply, copied, m.Index - copied);
            ranges.Add(new PiiRestoredRange(entity.Type, token, builder.Length, entity.Value.Length));
            builder.Append(entity.Value);
            copied = m.Index + m.Length;
        }
        builder.Append(reply, copied, reply.Length - copied);
        return builder.ToString();
    }

    /// <summary>Where each placeholder of <paramref name="text"/> is, for the types it names (unknown tokens included).</summary>
    internal static List<PiiRestoredRange> FindPlaceholders(string? text)
    {
        var ranges = new List<PiiRestoredRange>();
        if (string.IsNullOrEmpty(text))
        {
            return ranges;
        }
        foreach (Match m in PlaceholderPattern().Matches(text))
        {
            var type = m.Groups["type"].Value switch
            {
                "NAME" => PiiType.Name,
                "EMAIL" => PiiType.Email,
                "PHONE" => PiiType.Phone,
                _ => PiiType.Card,
            };
            ranges.Add(new PiiRestoredRange(type, m.Groups["token"].Value, m.Index, m.Length));
        }
        return ranges;
    }

    /// <summary>The token prefix of a type: <c>NAME</c>, <c>EMAIL</c>, <c>PHONE</c> or <c>CARD</c>.</summary>
    public static string TokenName(PiiType type) => type switch
    {
        PiiType.Name => "NAME",
        PiiType.Email => "EMAIL",
        PiiType.Phone => "PHONE",
        _ => "CARD",
    };

    /// <summary>Whether the digits of <paramref name="value"/> pass the Luhn check (other characters are ignored).</summary>
    public static bool PassesLuhn(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return false;
        }
        var sum = 0;
        var doubled = false;
        var count = 0;
        for (var i = value.Length - 1; i >= 0; i--)
        {
            var c = value[i];
            if (c is < '0' or > '9')
            {
                continue;
            }
            var digit = c - '0';
            if (doubled)
            {
                digit *= 2;
                if (digit > 9)
                {
                    digit -= 9;
                }
            }
            sum += digit;
            doubled = !doubled;
            count++;
        }
        return count > 0 && sum % 10 == 0;
    }

    private static int CountDigits(string value)
    {
        var count = 0;
        foreach (var c in value)
        {
            if (c is >= '0' and <= '9')
            {
                count++;
            }
        }
        return count;
    }

    private static bool IsWordBoundary(string text, int index) =>
        index < 0 || index >= text.Length || !char.IsLetterOrDigit(text[index]);

    private static string Normalize(PiiType type, string value)
    {
        if (type is PiiType.Phone or PiiType.Card)
        {
            var digits = new StringBuilder(value.Length);
            foreach (var c in value)
            {
                if (c is >= '0' and <= '9')
                {
                    digits.Append(c);
                }
            }
            return digits.ToString();
        }
        return value.Trim().ToUpperInvariant();
    }

    [GeneratedRegex(@"[A-Za-z0-9._%+-]+@[A-Za-z0-9-]+(?:\.[A-Za-z0-9-]+)*\.[A-Za-z]{2,}")]
    private static partial Regex EmailPattern();

    /// <summary>A run of digits with single spaces, dots, dashes or brackets between them, optionally after a plus.</summary>
    [GeneratedRegex(@"(?<![\w@.])\+?\(?\d(?:[ .\-]?\(?\)?[ .\-]?\d)+(?![\w@])")]
    private static partial Regex DigitsPattern();

    [GeneratedRegex(@"^\d{4}-\d{2}-\d{2}$")]
    private static partial Regex IsoDatePattern();

    [GeneratedRegex(@"(?i:\b(?:I['’]m|I am|this is|my name is))\s+(?<name>\p{Lu}[\p{Ll}'’]+(?:[ \-]\p{Lu}[\p{Ll}'’]+){0,2})")]
    private static partial Regex IntroductionPattern();

    [GeneratedRegex(@"\[(?<token>(?<type>NAME|EMAIL|PHONE|CARD)_\d+)\]")]
    private static partial Regex PlaceholderPattern();
}
