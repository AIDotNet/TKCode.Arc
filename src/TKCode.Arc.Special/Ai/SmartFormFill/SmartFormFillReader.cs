using System.Text.RegularExpressions;

namespace TKCode.Arc;

/// <summary>
/// The stand-in reader a <see cref="SmartFormFill"/> uses when the app gives it no extractor: the known result of a sample
/// whose text was pasted, and for anything else a small rule-based parser. It is a simulation helper so the form fills in a
/// demo, not a model; nothing is sent.
/// </summary>
internal static partial class SmartFormFillReader
{
    /// <summary>How long the simulated read takes.</summary>
    public static readonly TimeSpan ReadTime = TimeSpan.FromMilliseconds(1100);

    /// <summary>How long the simulated save takes.</summary>
    public static readonly TimeSpan SaveTime = TimeSpan.FromMilliseconds(700);

    /// <summary>
    /// The simulated read: the <see cref="SmartFormFillSample.Result"/> of a sample whose text this is, else
    /// <see cref="Parse"/>.
    /// </summary>
    public static SmartFormFillResult Read(string text, IReadOnlyList<SmartFormFillField> fields, IReadOnlyList<SmartFormFillSample> samples)
    {
        var trimmed = text.Trim();
        foreach (var sample in samples)
        {
            if (sample?.Result is { } known && sample.Text.Trim() == trimmed)
            {
                return known;
            }
        }
        return Parse(text, fields);
    }

    private static readonly string[] s_titleWords =
    [
        "head", "director", "manager", "engineer", "founder", "co-founder", "ceo", "cto", "cfo", "coo", "vp", "vice president",
        "lead", "designer", "president", "partner", "consultant", "officer", "specialist", "analyst", "architect", "owner",
    ];

    private static readonly string[] s_companyWords =
    [
        "inc", "inc.", "llc", "ltd", "ltd.", "labs", "studio", "studios", "group", "co.", "corp", "corp.", "gmbh", "partners", "freight",
        "systems", "software", "agency",
    ];

    [GeneratedRegex(@"[A-Za-z0-9._%+-]+@[A-Za-z0-9.-]+\.[A-Za-z]{2,}")]
    private static partial Regex EmailPattern();

    [GeneratedRegex(@"\+?\(?\d[\d \t().-]{6,}\d")]
    private static partial Regex PhonePattern();

    [GeneratedRegex(@"\b(?:https?://)?(?:[a-z0-9-]+\.)+(?:com|org|net|io|dev|co|app)\b(?:/[^\s]*)?", RegexOptions.IgnoreCase)]
    private static partial Regex UrlPattern();

    [GeneratedRegex(@"^\d+\s+[A-Za-z]")]
    private static partial Regex AddressPattern();

    [GeneratedRegex(@"^[A-Z][a-zA-Z'’-]+(?:\s+[A-Z][a-zA-Z'’.-]+){1,2}$")]
    private static partial Regex NamePattern();

    /// <summary>
    /// A small rule-based reader for text without a known result: emails, phone numbers and web addresses by pattern,
    /// a name from a line of two or three capitalized words, a job title from a line with a title word, a company from the
    /// rest of that line or a line with a company word, and an address from a line that starts with a number. Confidence
    /// drops when there is more than one candidate or the value is a guess.
    /// </summary>
    public static SmartFormFillResult Parse(string text, IReadOnlyList<SmartFormFillField> fields)
    {
        var found = new Dictionary<string, SmartFormFillValue>(StringComparer.Ordinal);
        var lines = text.Split('\n').Select(l => l.Trim()).Where(l => l.Length > 0).ToArray();

        var emails = EmailPattern().Matches(text);
        if (emails.Count > 0)
        {
            var email = emails[0].Value;
            found["email"] = emails.Count == 1
                ? new SmartFormFillValue(email, 0.97, email)
                : new SmartFormFillValue(email, 0.7, email, "Several addresses in the text; picked the first.");
        }
        var phones = PhonePattern().Matches(text).Where(m => m.Value.Count(char.IsDigit) >= 7).ToArray();
        if (phones.Length > 0)
        {
            var phone = phones[0].Value.Trim();
            found["phone"] = phones.Length == 1
                ? new SmartFormFillValue(phone, 0.88, phone)
                : new SmartFormFillValue(phone, 0.7, phone, "Several numbers in the text; picked the first.");
        }
        var url = UrlPattern().Matches(text).FirstOrDefault(m => !m.Value.Contains('@') && !IsInsideEmail(text, m.Index));
        if (url is not null)
        {
            found["url"] = new SmartFormFillValue(url.Value, 0.9, url.Value);
        }

        string? titleLine = null;
        foreach (var line in lines)
        {
            if (found.ContainsKey("title"))
            {
                break;
            }
            foreach (var segment in Segments(line))
            {
                if (HasWord(segment, s_titleWords) && segment.Length <= 60 && !segment.Contains('@'))
                {
                    found["title"] = new SmartFormFillValue(segment, 0.8, segment);
                    titleLine = line;
                    break;
                }
            }
        }
        foreach (var line in lines)
        {
            if (found.ContainsKey("name"))
            {
                break;
            }
            if (NamePattern().IsMatch(line) && !HasWord(line, s_titleWords) && !HasWord(line, s_companyWords))
            {
                found["name"] = new SmartFormFillValue(line, 0.85, line);
            }
        }
        if (titleLine is not null)
        {
            var rest = Segments(titleLine).FirstOrDefault(s => !HasWord(s, s_titleWords) && s.Length > 1);
            if (rest is not null)
            {
                found["company"] = new SmartFormFillValue(rest, 0.82, rest);
            }
        }
        if (!found.ContainsKey("company"))
        {
            var line = lines.FirstOrDefault(l => HasWord(l, s_companyWords) && !l.Contains('@') && l.Length <= 60);
            if (line is not null)
            {
                found["company"] = new SmartFormFillValue(line, 0.76, line);
            }
            else if (emails.Count > 0)
            {
                var domain = emails[0].Value[(emails[0].Value.IndexOf('@') + 1)..];
                var name = domain.Split('.')[0];
                if (name.Length > 1 && !name.Equals("example", StringComparison.OrdinalIgnoreCase)
                    && !name.Equals("gmail", StringComparison.OrdinalIgnoreCase))
                {
                    found["company"] = new SmartFormFillValue(char.ToUpperInvariant(name[0]) + name[1..], 0.5, domain,
                        "Guessed from the email domain.");
                }
            }
        }
        var address = lines.FirstOrDefault(l => AddressPattern().IsMatch(l) && !PhonePattern().IsMatch(l[..Math.Min(l.Length, 12)]));
        if (address is not null)
        {
            found["address"] = new SmartFormFillValue(address, 0.7, address, "Read from one line; check the city and postcode.");
        }

        // Keys follow the form: a field's id, or for typed fields the first field of that kind.
        var result = new SmartFormFillResult();
        foreach (var field in fields)
        {
            var key = field.Kind switch
            {
                SmartFormFillFieldKind.Email => "email",
                SmartFormFillFieldKind.Phone => "phone",
                SmartFormFillFieldKind.Url => "url",
                SmartFormFillFieldKind.Company => "company",
                _ => field.Id,
            };
            if (found.TryGetValue(key, out var value) && !result.Values.Contains(value))
            {
                result[field.Id] = value;
            }
        }
        return result;
    }

    private static bool IsInsideEmail(string text, int index) => index > 0 && text[index - 1] == '@';

    private static IEnumerable<string> Segments(string line) =>
        line.Split(['|', '·', ','], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .SelectMany(s => s.Split(" at ", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));

    private static bool HasWord(string text, string[] words)
    {
        var lower = " " + text.ToLowerInvariant().Replace(',', ' ').Replace('|', ' ') + " ";
        return words.Any(w => lower.Contains(" " + w + " ", StringComparison.Ordinal));
    }
}
