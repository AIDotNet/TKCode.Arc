using System.Collections.Concurrent;
using System.Globalization;
using System.Text;

namespace TKCode.Arc;

/// <summary>One row of the built-in ISO 4217 table: Intl's narrow symbol, minor digits and en-US display name.</summary>
internal sealed record MoneyInputCurrencyEntry(string Code, string Symbol, int Digits, string Name);

/// <summary>
/// Compact ISO 4217 table. .NET has no lookup of a currency's narrow symbol, minor digits and display name by code
/// (Arc reads them from <c>Intl.NumberFormat(..., { currencyDisplay: "narrowSymbol" })</c> and
/// <c>Intl.DisplayNames</c>), so the common currencies are listed with the values Intl gives for en-US. Unknown codes
/// fall back to .NET region data, then to the code itself with two digits.
/// </summary>
internal static class MoneyInputCurrencyTable
{
    private static readonly Dictionary<string, MoneyInputCurrencyEntry> s_entries = Build(
        new("USD", "$", 2, "US Dollar"),
        new("EUR", "€", 2, "Euro"),
        new("GBP", "£", 2, "British Pound"),
        new("JPY", "¥", 0, "Japanese Yen"),
        new("CAD", "$", 2, "Canadian Dollar"),
        new("AUD", "$", 2, "Australian Dollar"),
        new("CHF", "CHF", 2, "Swiss Franc"),
        new("INR", "₹", 2, "Indian Rupee"),
        new("CNY", "¥", 2, "Chinese Yuan"),
        new("HKD", "$", 2, "Hong Kong Dollar"),
        new("NZD", "$", 2, "New Zealand Dollar"),
        new("SGD", "$", 2, "Singapore Dollar"),
        new("SEK", "kr", 2, "Swedish Krona"),
        new("NOK", "kr", 2, "Norwegian Krone"),
        new("DKK", "kr", 2, "Danish Krone"),
        new("ISK", "kr", 0, "Icelandic Króna"),
        new("PLN", "zł", 2, "Polish Zloty"),
        new("CZK", "Kč", 2, "Czech Koruna"),
        new("HUF", "Ft", 2, "Hungarian Forint"),
        new("KRW", "₩", 0, "South Korean Won"),
        new("TWD", "$", 2, "New Taiwan Dollar"),
        new("THB", "฿", 2, "Thai Baht"),
        new("IDR", "Rp", 2, "Indonesian Rupiah"),
        new("MYR", "RM", 2, "Malaysian Ringgit"),
        new("PHP", "₱", 2, "Philippine Peso"),
        new("VND", "₫", 0, "Vietnamese Dong"),
        new("MXN", "$", 2, "Mexican Peso"),
        new("BRL", "R$", 2, "Brazilian Real"),
        new("CLP", "$", 0, "Chilean Peso"),
        new("COP", "$", 2, "Colombian Peso"),
        new("ZAR", "R", 2, "South African Rand"),
        new("TRY", "₺", 2, "Turkish Lira"),
        new("ILS", "₪", 2, "Israeli New Shekel"),
        new("AED", "AED", 2, "UAE Dirham"),
        new("SAR", "SAR", 2, "Saudi Riyal"),
        new("KWD", "KWD", 3, "Kuwaiti Dinar"),
        new("EGP", "E£", 2, "Egyptian Pound"),
        new("NGN", "₦", 2, "Nigerian Naira"),
        new("UAH", "₴", 2, "Ukrainian Hryvnia"),
        new("RUB", "₽", 2, "Russian Ruble"));

    private static readonly ConcurrentDictionary<string, MoneyInputCurrencyEntry> s_fallbacks = new(StringComparer.Ordinal);

    private static Dictionary<string, MoneyInputCurrencyEntry> Build(params MoneyInputCurrencyEntry[] entries)
    {
        var map = new Dictionary<string, MoneyInputCurrencyEntry>(entries.Length, StringComparer.OrdinalIgnoreCase);
        foreach (var entry in entries)
        {
            map[entry.Code] = entry;
        }
        return map;
    }

    /// <summary>The entry for <paramref name="code"/> (case-insensitive); never null.</summary>
    public static MoneyInputCurrencyEntry Lookup(string? code)
    {
        var normalized = string.IsNullOrWhiteSpace(code) ? "USD" : code.Trim().ToUpperInvariant();
        if (s_entries.TryGetValue(normalized, out var entry))
        {
            return entry;
        }
        return s_fallbacks.GetOrAdd(normalized, FromRegions);
    }

    private static MoneyInputCurrencyEntry FromRegions(string code)
    {
        try
        {
            foreach (var culture in CultureInfo.GetCultures(CultureTypes.SpecificCultures))
            {
                RegionInfo region;
                try
                {
                    region = new RegionInfo(culture.Name);
                }
                catch (ArgumentException)
                {
                    continue;
                }
                if (string.Equals(region.ISOCurrencySymbol, code, StringComparison.OrdinalIgnoreCase))
                {
                    return new MoneyInputCurrencyEntry(code, code, culture.NumberFormat.CurrencyDecimalDigits, region.CurrencyEnglishName);
                }
            }
        }
        catch (Exception ex) when (ex is CultureNotFoundException or NotSupportedException or PlatformNotSupportedException)
        {
            // Invariant globalization: no region data, keep the code.
        }
        return new MoneyInputCurrencyEntry(code, code, 2, code);
    }
}

/// <summary>
/// A currency resolved for a culture: the narrow symbol and where it sits, minor digits, display name and the two
/// formatters Arc uses (<c>full</c>: "$1,234.50", <c>whole</c>: "$10"). Cached per culture and code.
/// </summary>
internal sealed class MoneyInputCurrencyInfo
{
    private static readonly ConcurrentDictionary<(string Culture, string Code), MoneyInputCurrencyInfo> s_cache = new();

    private readonly NumberFormatInfo _numbers;
    private readonly int _pattern;

    private MoneyInputCurrencyInfo(MoneyInputCurrencyEntry entry, CultureInfo culture)
    {
        Code = entry.Code;
        Symbol = entry.Symbol;
        Digits = entry.Digits;
        Name = entry.Name.Length > 0 ? char.ToUpper(entry.Name[0], CultureInfo.InvariantCulture) + entry.Name[1..] : entry.Code;
        _numbers = culture.NumberFormat;
        _pattern = _numbers.CurrencyPositivePattern;
        // en-US always puts the symbol first; other cultures follow their positive currency pattern ("n $" for de-DE).
        IsSuffix = _pattern is 1 or 3;
        MinorFactor = MoneyInputDraft.Pow10(Digits);
    }

    /// <summary>ISO 4217 code ("USD").</summary>
    public string Code { get; }

    /// <summary>Narrow symbol ("$", "€", "CHF").</summary>
    public string Symbol { get; }

    /// <summary>Minor digits (2 for USD, 0 for JPY).</summary>
    public int Digits { get; }

    /// <summary>English display name ("US Dollar").</summary>
    public string Name { get; }

    /// <summary>True when the culture writes the symbol after the amount.</summary>
    public bool IsSuffix { get; }

    /// <summary>10^Digits.</summary>
    public decimal MinorFactor { get; }

    public static MoneyInputCurrencyInfo Get(string? code, CultureInfo culture)
    {
        var entry = MoneyInputCurrencyTable.Lookup(code);
        return s_cache.GetOrAdd((culture.Name, entry.Code), static (_, state) => new MoneyInputCurrencyInfo(state.entry, state.culture),
            (entry, culture));
    }

    /// <summary>The full currency string of a major amount ("$1,234.50"); the sign is dropped as in Arc's drafts.</summary>
    public string Format(decimal major) => Compose(Math.Abs(major).ToString("N" + Digits.ToString(CultureInfo.InvariantCulture), _numbers));

    /// <summary>The amount in minor units as a full currency string; empty for null.</summary>
    public string FormatMinor(decimal? minor) => minor is { } value ? Format(value / MinorFactor) : string.Empty;

    /// <summary>A whole-unit currency string ("$10"), used by the quick add chips.</summary>
    public string FormatWhole(decimal major)
        => Compose(Math.Round(Math.Abs(major), 0, MidpointRounding.AwayFromZero).ToString("N0", _numbers));

    private string Compose(string number)
    {
        // CLDR currency spacing: a letter symbol next to the digits gets a no-break space ("CHF 12.00").
        var builder = new StringBuilder(number.Length + Symbol.Length + 1);
        if (IsSuffix)
        {
            builder.Append(number);
            if (_pattern == 3 || NeedsSpacing(Symbol[0]))
            {
                builder.Append(' ');
            }
            builder.Append(Symbol);
        }
        else
        {
            builder.Append(Symbol);
            if (_pattern == 2 || NeedsSpacing(Symbol[^1]))
            {
                builder.Append(' ');
            }
            builder.Append(number);
        }
        return builder.ToString();
    }

    private static bool NeedsSpacing(char adjacent)
        => char.GetUnicodeCategory(adjacent) is not (UnicodeCategory.CurrencySymbol or UnicodeCategory.SpaceSeparator);
}
