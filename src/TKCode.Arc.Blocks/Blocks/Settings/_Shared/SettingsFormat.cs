using System.Globalization;

namespace TKCode.Arc;

/// <summary>
/// Money, date and relative-time text shared by the Settings blocks, so every block formats the same values the same
/// way. The culture comes from each block's <c>Labels.Culture</c>.
/// </summary>
internal static class SettingsFormat
{
    /// <summary>The culture the blocks use unless their labels say otherwise.</summary>
    public static readonly CultureInfo DefaultCulture = CultureInfo.GetCultureInfo("en-US");

    /// <summary>
    /// <paramref name="amount"/> in <paramref name="currency"/> (ISO 4217): <c>$1,234.50</c>, <c>€90.00</c>, <c>¥1,200</c>,
    /// or <c>1,234.50 CHF</c> for currencies without a symbol here.
    /// </summary>
    public static string Money(decimal amount, string? currency, CultureInfo? culture = null)
        => Format(amount, currency, culture, trimWhole: false);

    /// <summary>Like <see cref="Money"/>, but whole amounts drop their minor units: <c>$90</c>, <c>$12.50</c>.</summary>
    public static string MoneyShort(decimal amount, string? currency, CultureInfo? culture = null)
        => Format(amount, currency, culture, trimWhole: true);

    private static string Format(decimal amount, string? currency, CultureInfo? culture, bool trimWhole)
    {
        culture ??= DefaultCulture;
        var code = string.IsNullOrWhiteSpace(currency) ? "USD" : currency.Trim().ToUpperInvariant();
        var noMinorUnits = code is "JPY" or "KRW";
        var whole = decimal.Truncate(amount) == amount;
        var format = noMinorUnits || (trimWhole && whole) ? "N0" : "N2";
        var number = Math.Abs(amount).ToString(format, culture);
        var sign = amount < 0 ? "-" : string.Empty;
        var symbol = code switch
        {
            "USD" => "$",
            "EUR" => "€",
            "GBP" => "£",
            "JPY" => "¥",
            _ => null
        };
        return symbol is null ? $"{sign}{number} {code}" : $"{sign}{symbol}{number}";
    }

    /// <summary>A calendar date such as <c>Nov 1, 2026</c>.</summary>
    public static string Date(DateTime date, CultureInfo? culture = null)
        => date.ToString("MMM d, yyyy", culture ?? DefaultCulture);

    /// <summary>A short date without the year, such as <c>Nov 1</c>.</summary>
    public static string ShortDate(DateTime date, CultureInfo? culture = null)
        => date.ToString("MMM d", culture ?? DefaultCulture);

    /// <summary>
    /// How long ago <paramref name="then"/> was, seen from <paramref name="now"/>: <c>Active now</c> (under a minute),
    /// <c>5 min ago</c>, <c>2 hours ago</c> (under 12 hours, or earlier the same day), <c>Yesterday</c>, <c>3 days ago</c>,
    /// <c>2 weeks ago</c>, <c>3 months ago</c>, then the date.
    /// </summary>
    public static string Relative(DateTimeOffset then, DateTimeOffset now, CultureInfo? culture = null)
    {
        var elapsed = now - then;
        if (elapsed < TimeSpan.FromMinutes(1))
        {
            return "Active now";
        }
        if (elapsed < TimeSpan.FromHours(1))
        {
            return $"{(int)elapsed.TotalMinutes} min ago";
        }
        // Hours for anything this side of half a day, even across midnight (00:30 is not "Yesterday" for 22:30).
        if (elapsed < TimeSpan.FromHours(12) || (elapsed < TimeSpan.FromDays(1) && then.LocalDateTime.Date == now.LocalDateTime.Date))
        {
            var hours = (int)elapsed.TotalHours;
            return hours == 1 ? "1 hour ago" : $"{hours} hours ago";
        }
        var days = (now.LocalDateTime.Date - then.LocalDateTime.Date).Days;
        if (days <= 1)
        {
            return "Yesterday";
        }
        if (days < 7)
        {
            return $"{days} days ago";
        }
        if (days < 28)
        {
            var weeks = days / 7;
            return weeks == 1 ? "1 week ago" : $"{weeks} weeks ago";
        }
        if (days < 365)
        {
            var months = Math.Max(1, days / 30);
            return months == 1 ? "1 month ago" : $"{months} months ago";
        }
        return Date(then.LocalDateTime.Date, culture);
    }

    /// <summary>A count in short form: <c>950</c>, <c>1.2K</c>, <c>3.4M</c>, <c>1.1B</c> (one decimal, trailing <c>.0</c> dropped).</summary>
    public static string Compact(double value, CultureInfo? culture = null)
    {
        culture ??= DefaultCulture;
        var magnitude = Math.Abs(value);
        (double divisor, string suffix) = magnitude switch
        {
            >= 1e9 => (1e9, "B"),
            >= 1e6 => (1e6, "M"),
            >= 1e3 => (1e3, "K"),
            _ => (1, string.Empty)
        };
        var scaled = value / divisor;
        var text = suffix.Length == 0 ? scaled.ToString("#,0.##", culture) : scaled.ToString("0.#", culture);
        return text + suffix;
    }
}
