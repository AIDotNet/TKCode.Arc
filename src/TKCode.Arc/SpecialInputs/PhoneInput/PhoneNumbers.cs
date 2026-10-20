using System.Text;

namespace TKCode.Arc;

/// <summary>Where a national number stands against its country's expected lengths (Arc's <c>PhoneStatus</c>).</summary>
public enum PhoneNumberStatus
{
    /// <summary>No national digits (a typed trunk prefix alone is still empty).</summary>
    Empty,

    /// <summary>Fewer digits than a valid length, or a length between two valid ones.</summary>
    Incomplete,

    /// <summary>A valid length for the country.</summary>
    Valid,

    /// <summary>More digits than the longest valid length.</summary>
    TooLong
}

/// <summary>
/// One entry of the compact phone table used by <see cref="PhoneInput"/> (Arc's <c>PhoneCountry</c>). Patterns format
/// the national significant number (no trunk prefix): <c>#</c> is a digit, anything else a separator. The valid lengths
/// are the digit capacities of the patterns.
/// </summary>
public sealed class PhoneCountry
{
    private readonly int[] _lengths;

    /// <summary>Creates a country entry.</summary>
    /// <param name="iso">ISO 3166-1 alpha-2 code, such as "US".</param>
    /// <param name="name">English name, such as "United States".</param>
    /// <param name="dial">Calling code without the plus, such as "1".</param>
    /// <param name="patterns">National formats from shortest to longest.</param>
    /// <param name="example">A realistic national number (placeholder and typing guide).</param>
    /// <param name="trunk">The national prefix people may type before the number, such as "0"; empty for none.</param>
    /// <param name="areaCodes">Area codes that pick this country when several share a calling code.</param>
    public PhoneCountry(string iso, string name, string dial, IReadOnlyList<string> patterns, string example, string trunk = "0",
                        IReadOnlyList<string>? areaCodes = null)
    {
        ArgumentNullException.ThrowIfNull(iso);
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(dial);
        ArgumentNullException.ThrowIfNull(patterns);
        if (patterns.Count == 0)
        {
            throw new ArgumentException("A country needs at least one pattern.", nameof(patterns));
        }
        Iso = iso;
        Name = name;
        Dial = dial;
        Patterns = patterns;
        Example = example ?? string.Empty;
        Trunk = trunk ?? string.Empty;
        AreaCodes = areaCodes;
        _lengths = new int[patterns.Count];
        for (var i = 0; i < patterns.Count; i++)
        {
            _lengths[i] = PhoneNumbers.Capacity(patterns[i]);
        }
        MinLength = _lengths.Min();
        MaxLength = _lengths.Max();
        Flag = PhoneNumbers.Flag(iso);
    }

    /// <summary>ISO 3166-1 alpha-2 code, such as "US".</summary>
    public string Iso { get; }

    /// <summary>English name, such as "United States".</summary>
    public string Name { get; }

    /// <summary>Calling code without the plus, such as "44".</summary>
    public string Dial { get; }

    /// <summary>National formats from shortest to longest. <c>#</c> is a digit, anything else is a separator.</summary>
    public IReadOnlyList<string> Patterns { get; }

    /// <summary>A national prefix people may type before the number, such as "0" in "07911 123456"; empty for none.</summary>
    public string Trunk { get; }

    /// <summary>A realistic national number, used for the placeholder and the typing guide.</summary>
    public string Example { get; }

    /// <summary>Area codes that pick this country when several share a calling code, such as Canada inside +1.</summary>
    public IReadOnlyList<string>? AreaCodes { get; }

    /// <summary>The flag as regional indicator letters (platforms without flag glyphs show the two letters).</summary>
    public string Flag { get; }

    /// <summary>Valid national lengths (the digit capacity of each pattern).</summary>
    public IReadOnlyList<int> Lengths => _lengths;

    /// <summary>The shortest valid national length.</summary>
    public int MinLength { get; }

    /// <summary>The longest valid national length.</summary>
    public int MaxLength { get; }

    /// <inheritdoc />
    public override string ToString() => $"{Name} +{Dial}";
}

/// <summary>The country and national digits read from an international number.</summary>
/// <param name="Country">The country whose calling code (and area code, for shared codes) matched.</param>
/// <param name="National">National significant digits, capped at the country's longest length.</param>
public readonly record struct PhoneNumberParseResult(PhoneCountry Country, string National);

/// <summary>
/// The phone helpers of <see cref="PhoneInput"/>, ported from Arc: a compact 49-country table (no metadata download, no
/// dependency), as-you-type national formatting, E.164 conversion, status and international number parsing.
/// </summary>
public static class PhoneNumbers
{
    private static readonly string[] s_caAreaCodes =
        ("204 226 236 249 250 263 289 306 343 354 365 367 368 382 387 403 416 418 428 431 437 438 450 468 474 506 514 519 548 " +
         "579 581 584 587 604 613 639 647 672 683 705 709 742 753 778 780 782 807 819 825 867 873 879 902 905").Split(' ');

    private static PhoneCountry C(string iso, string name, string dial, string pattern, string example, string trunk = "0", string[]? areaCodes = null)
        => new(iso, name, dial, new[] { pattern }, example, trunk, areaCodes);

    private static PhoneCountry C(string iso, string name, string dial, string[] patterns, string example, string trunk = "0")
        => new(iso, name, dial, patterns, example, trunk);

    /// <summary>The 49 countries of Arc's table, in Arc's order.</summary>
    public static IReadOnlyList<PhoneCountry> Countries { get; } = new[]
    {
        C("US", "United States", "1", "(###) ###-####", "2015550123", "1"),
        C("CA", "Canada", "1", "(###) ###-####", "5065550123", "1", s_caAreaCodes),
        C("MX", "Mexico", "52", "## #### ####", "2221234567", ""),
        C("BR", "Brazil", "55", new[] { "(##) ####-####", "(##) #####-####" }, "11961234567"),
        C("AR", "Argentina", "54", "## ####-####", "1123456789"),
        C("CO", "Colombia", "57", "### ### ####", "3211234567", ""),
        C("CL", "Chile", "56", "# #### ####", "221234567", ""),
        C("PE", "Peru", "51", "### ### ###", "912345678"),
        C("GB", "United Kingdom", "44", "#### ######", "7400123456"),
        C("IE", "Ireland", "353", "## ### ####", "850123456"),
        C("FR", "France", "33", "# ## ## ## ##", "612345678"),
        C("DE", "Germany", "49", new[] { "### #######", "### ########" }, "15123456789"),
        C("ES", "Spain", "34", "### ## ## ##", "612345678", ""),
        C("IT", "Italy", "39", new[] { "### ### ###", "### ### ####" }, "3123456789", ""),
        C("PT", "Portugal", "351", "### ### ###", "912345678", ""),
        C("NL", "Netherlands", "31", "# ########", "612345678"),
        C("BE", "Belgium", "32", "### ## ## ##", "470123456"),
        C("CH", "Switzerland", "41", "## ### ## ##", "781234567"),
        C("AT", "Austria", "43", new[] { "### #######", "### ########" }, "6641234567"),
        C("SE", "Sweden", "46", "## ### ## ##", "701234567"),
        C("NO", "Norway", "47", "### ## ###", "40612345", ""),
        C("DK", "Denmark", "45", "## ## ## ##", "32123456", ""),
        C("FI", "Finland", "358", new[] { "## ### ####", "## ### #####" }, "412345678"),
        C("PL", "Poland", "48", "### ### ###", "512345678", ""),
        C("CZ", "Czechia", "420", "### ### ###", "601123456", ""),
        C("GR", "Greece", "30", "### ### ####", "6912345678", ""),
        C("UA", "Ukraine", "380", "## ### ## ##", "501234567"),
        C("TR", "Turkey", "90", "### ### ## ##", "5012345678"),
        C("IL", "Israel", "972", "##-###-####", "502345678"),
        C("AE", "United Arab Emirates", "971", "## ### ####", "501234567"),
        C("SA", "Saudi Arabia", "966", "## ### ####", "512345678"),
        C("EG", "Egypt", "20", "### ### ####", "1001234567"),
        C("NG", "Nigeria", "234", "### ### ####", "8021234567"),
        C("KE", "Kenya", "254", "### ######", "712123456"),
        C("ZA", "South Africa", "27", "## ### ####", "711234567"),
        C("IN", "India", "91", "#####-#####", "8123456789"),
        C("PK", "Pakistan", "92", "### #######", "3012345678"),
        C("CN", "China", "86", "### #### ####", "13123456789"),
        C("JP", "Japan", "81", "##-####-####", "9012345678"),
        C("KR", "South Korea", "82", new[] { "##-###-####", "##-####-####" }, "1020000000"),
        C("TW", "Taiwan (China)", "886", "### ### ###", "912345678"),
        C("HK", "Hong Kong (China)", "852", "#### ####", "51234567", ""),
        C("SG", "Singapore", "65", "#### ####", "81234567", ""),
        C("PH", "Philippines", "63", "### ### ####", "9051234567"),
        C("ID", "Indonesia", "62", new[] { "###-###-####", "###-####-####", "###-####-#####" }, "81234567890"),
        C("TH", "Thailand", "66", "## ### ####", "812345678"),
        C("VN", "Vietnam", "84", "## ### ## ##", "912345678"),
        C("AU", "Australia", "61", "### ### ###", "412345678"),
        C("NZ", "New Zealand", "64", new[] { "# ### ####", "## ### ####", "## #### ####" }, "211234567")
    };

    private static readonly Dictionary<string, PhoneCountry> s_byIso = Countries.ToDictionary(c => c.Iso, StringComparer.OrdinalIgnoreCase);

    /// <summary>The table entry for an ISO code (case-insensitive), or null.</summary>
    public static PhoneCountry? Find(string? iso) => iso is not null && s_byIso.TryGetValue(iso, out var country) ? country : null;

    /// <summary>
    /// Regional indicator letters for an ISO code, such as "🇺🇸" for "US"; other input comes back unchanged. Taiwan ("TW"),
    /// Hong Kong ("HK") and Macao ("MO") show the flag of China ("🇨🇳"): they are part of China, and the Taiwan glyph is
    /// missing from the emoji font of some platforms, where it draws as an empty box.
    /// </summary>
    public static string Flag(string iso)
    {
        if (string.IsNullOrEmpty(iso))
        {
            return string.Empty;
        }
        var upper = iso.ToUpperInvariant();
        if (upper is "TW" or "HK" or "MO")
        {
            upper = "CN";
        }
        var builder = new StringBuilder(upper.Length * 2);
        foreach (var ch in upper)
        {
            if (ch is < 'A' or > 'Z')
            {
                return iso;
            }
            builder.Append(char.ConvertFromUtf32(0x1F1E6 + ch - 'A'));
        }
        return builder.ToString();
    }

    /// <summary>
    /// Formats national digits as typed, keeping a typed trunk prefix: "07911123456" in the United Kingdom reads
    /// "07911 123456". Separators only print while digits follow, so a half-typed number never ends in ") " or "-".
    /// </summary>
    public static string FormatNational(PhoneCountry country, string digits)
    {
        ArgumentNullException.ThrowIfNull(country);
        var (trunk, rest) = SplitTrunk(country, digits ?? string.Empty);
        var pattern = PatternFor(country, rest.Length);
        var body = ApplyPattern(rest, pattern);
        if (trunk.Length == 0)
        {
            return body;
        }
        if (rest.Length == 0)
        {
            return trunk;
        }
        return trunk + (pattern.StartsWith('#') ? string.Empty : " ") + body;
    }

    /// <summary>Where <paramref name="digits"/> (national, a trunk prefix allowed) stand against the country's lengths.</summary>
    public static PhoneNumberStatus StatusOf(PhoneCountry country, string digits)
    {
        ArgumentNullException.ThrowIfNull(country);
        var length = SplitTrunk(country, digits ?? string.Empty).Body.Length;
        if (length == 0)
        {
            return PhoneNumberStatus.Empty;
        }
        if (country.Lengths.Contains(length))
        {
            return PhoneNumberStatus.Valid;
        }
        return length > country.MaxLength ? PhoneNumberStatus.TooLong : PhoneNumberStatus.Incomplete;
    }

    /// <summary>The E.164 form ("+" + calling code + national digits without the trunk), or "" without digits.</summary>
    public static string ToE164(PhoneCountry country, string digits)
    {
        ArgumentNullException.ThrowIfNull(country);
        var rest = SplitTrunk(country, digits ?? string.Empty).Body;
        return rest.Length > 0 ? $"+{country.Dial}{rest}" : string.Empty;
    }

    /// <summary>
    /// Reads "+44 (0)7911 123456", "0044 7911…" or "+14165550123" into a country and national digits; the longest
    /// calling code wins, +1 picks Canada by area code, and a trunk zero written after the code is dropped when the rest
    /// is still a full number. Returns null for text that does not start with "+" or "00", or an unknown code.
    /// </summary>
    public static PhoneNumberParseResult? Parse(string input, IEnumerable<PhoneCountry>? pool = null)
    {
        if (input is null)
        {
            return null;
        }
        var candidates = pool as IReadOnlyList<PhoneCountry> ?? pool?.ToList() ?? Countries;
        var trimmed = input.Trim();
        var digits = OnlyDigits(trimmed);
        if (!trimmed.StartsWith('+'))
        {
            if (!trimmed.StartsWith("00", StringComparison.Ordinal))
            {
                return null;
            }
            digits = Slice(digits, 2);
        }
        foreach (var size in s_dialSizes)
        {
            var dial = Slice(digits, 0, size);
            PhoneCountry? byArea = null;
            PhoneCountry? plain = null;
            PhoneCountry? first = null;
            var rest = Slice(digits, size);
            foreach (var entry in candidates)
            {
                if (entry.Dial != dial)
                {
                    continue;
                }
                first ??= entry;
                if (byArea is null && entry.AreaCodes is { } codes && codes.Any(code => rest.StartsWith(code, StringComparison.Ordinal)))
                {
                    byArea = entry;
                }
                if (plain is null && entry.AreaCodes is null)
                {
                    plain = entry;
                }
            }
            if (first is null)
            {
                continue;
            }
            var country = byArea ?? plain ?? first;
            // "+44 (0)7911…" carries a trunk zero it should not; drop it when the rest is still a full number without it.
            if (country.Trunk.Length > 0 && rest.StartsWith(country.Trunk, StringComparison.Ordinal) &&
                rest.Length - country.Trunk.Length >= country.MinLength)
            {
                rest = rest[country.Trunk.Length..];
            }
            return new PhoneNumberParseResult(country, CapDigits(country, rest));
        }
        return null;
    }

    /// <summary>Formats an E.164 number for display, such as "+44 7400 123456"; returns the input when it cannot be read.</summary>
    public static string Format(string e164)
    {
        if (string.IsNullOrEmpty(e164))
        {
            return e164 ?? string.Empty;
        }
        return Parse(e164) is { } parsed ? $"+{parsed.Country.Dial} {FormatNational(parsed.Country, parsed.National)}" : e164;
    }

    // ------------------------------------------------------------------ internals shared with the control

    private static readonly int[] s_dialSizes = { 3, 2, 1 };

    internal static int Capacity(string pattern)
    {
        var count = 0;
        foreach (var ch in pattern)
        {
            if (ch == '#')
            {
                count++;
            }
        }
        return count;
    }

    internal static bool IsDigit(char ch) => ch is >= '0' and <= '9';

    internal static string OnlyDigits(string? text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return string.Empty;
        }
        var builder = new StringBuilder(text.Length);
        foreach (var ch in text)
        {
            if (IsDigit(ch))
            {
                builder.Append(ch);
            }
        }
        return builder.Length == text.Length ? text : builder.ToString();
    }

    internal static int CountDigits(string text, int end)
    {
        var count = 0;
        end = Math.Clamp(end, 0, text.Length);
        for (var i = 0; i < end; i++)
        {
            if (IsDigit(text[i]))
            {
                count++;
            }
        }
        return count;
    }

    /// <summary>JavaScript <c>slice</c> semantics: out-of-range bounds clamp instead of throwing.</summary>
    internal static string Slice(string text, int start, int? end = null)
    {
        var from = Math.Clamp(start, 0, text.Length);
        var to = Math.Clamp(end ?? text.Length, from, text.Length);
        return text.Substring(from, to - from);
    }

    internal static (string Trunk, string Body) SplitTrunk(PhoneCountry country, string digits)
        => country.Trunk.Length > 0 && digits.StartsWith(country.Trunk, StringComparison.Ordinal)
            ? (country.Trunk, digits[country.Trunk.Length..])
            : (string.Empty, digits);

    /// <summary>A typed trunk prefix earns its own room; without one the longest valid length is the cap.</summary>
    internal static string CapDigits(PhoneCountry country, string digits)
        => Slice(digits, 0, SplitTrunk(country, digits).Trunk.Length + country.MaxLength);

    internal static string ApplyPattern(string digits, string pattern)
    {
        var builder = new StringBuilder(pattern.Length + digits.Length);
        var index = 0;
        foreach (var ch in pattern)
        {
            if (index >= digits.Length)
            {
                break;
            }
            builder.Append(ch == '#' ? digits[index++] : ch);
        }
        builder.Append(digits, index, digits.Length - index);
        return builder.ToString();
    }

    internal static string PatternFor(PhoneCountry country, int length)
    {
        foreach (var pattern in country.Patterns)
        {
            if (Capacity(pattern) >= length)
            {
                return pattern;
            }
        }
        return country.Patterns[^1];
    }

    /// <summary>"10", "9 or 10", "9, 10 or 11".</summary>
    internal static string LengthCopy(PhoneCountry country)
    {
        var lengths = country.Lengths;
        if (lengths.Count == 1)
        {
            return lengths[0].ToString(System.Globalization.CultureInfo.InvariantCulture);
        }
        var head = string.Join(", ", lengths.Take(lengths.Count - 1).Select(l => l.ToString(System.Globalization.CultureInfo.InvariantCulture)));
        return $"{head} or {lengths[^1].ToString(System.Globalization.CultureInfo.InvariantCulture)}";
    }

    /// <summary>The rest of the example number in the shape this country expects, drawn faintly after the typed text.</summary>
    internal static string Guide(PhoneCountry country, string digits, string formatted)
    {
        var (trunk, rest) = SplitTrunk(country, digits);
        if (rest.Length >= country.Example.Length)
        {
            return string.Empty;
        }
        var full = FormatNational(country, trunk + rest + country.Example[rest.Length..]);
        return full.StartsWith(formatted, StringComparison.Ordinal) ? full[formatted.Length..] : string.Empty;
    }

    /// <summary>Arc's search: digits match calling codes either way; text matches a name or the exact ISO code.</summary>
    internal static bool MatchesQuery(PhoneCountry country, string needle)
    {
        if (needle.Length == 0)
        {
            return true;
        }
        var digits = OnlyDigits(needle);
        if (digits.Length > 0 && needle.All(ch => ch == '+' || IsDigit(ch) || char.IsWhiteSpace(ch) || ch == '(' || ch == ')' || ch == '-'))
        {
            return country.Dial.StartsWith(digits, StringComparison.Ordinal) || digits.StartsWith(country.Dial, StringComparison.Ordinal);
        }
        var plus = needle.IndexOf('+');
        if ((plus < 0 ? needle : needle.Remove(plus, 1)).Length == 0)
        {
            return true;
        }
        return country.Name.Contains(needle, StringComparison.OrdinalIgnoreCase) ||
               string.Equals(country.Iso, needle, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Position in the formatted text just after the <paramref name="count"/>th digit.</summary>
    internal static int CaretAfterDigits(string text, int count)
    {
        if (count <= 0)
        {
            for (var i = 0; i < text.Length; i++)
            {
                if (IsDigit(text[i]))
                {
                    return i;
                }
            }
            return text.Length;
        }
        var seen = 0;
        for (var i = 0; i < text.Length; i++)
        {
            if (IsDigit(text[i]) && ++seen == count)
            {
                return i + 1;
            }
        }
        return text.Length;
    }
}
