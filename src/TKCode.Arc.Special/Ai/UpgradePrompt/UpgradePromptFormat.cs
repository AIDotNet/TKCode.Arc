using System.Globalization;

namespace TKCode.Arc;

/// <summary>The numbers of an <see cref="UpgradePrompt"/>: counts, prices, the saving and what is charged today.</summary>
internal static class UpgradePromptFormat
{
    private static readonly CultureInfo s_culture = CultureInfo.GetCultureInfo("en-US");

    /// <summary>A count with group separators and no decimals unless it has some ("2,000", "12.5").</summary>
    public static string Count(double value)
    {
        var rounded = Math.Round(value, 1);
        return rounded == Math.Floor(rounded)
            ? rounded.ToString("N0", s_culture)
            : rounded.ToString("N1", s_culture);
    }

    /// <summary>The currency sign for an ISO code ("USD" → "$"); unknown codes are written with a trailing space.</summary>
    public static string Symbol(string? currency) => (currency ?? "USD").ToUpperInvariant() switch
    {
        "USD" => "$",
        "EUR" => "€",
        "GBP" => "£",
        "JPY" => "¥",
        "CNY" => "¥",
        "" => "$",
        var code => code + " "
    };

    /// <summary>Digits after the decimal point: 0 when both prices are whole, otherwise 2.</summary>
    public static int Decimals(UpgradePromptPlan plan) =>
        plan.MonthlyPrice == decimal.Truncate(plan.MonthlyPrice) && plan.YearlyPrice == decimal.Truncate(plan.YearlyPrice) ? 0 : 2;

    /// <summary>An amount with its sign ("$192", "$9.50").</summary>
    public static string Money(decimal amount, string? currency, int decimals) =>
        Symbol(currency) + amount.ToString("N" + Math.Max(0, decimals), s_culture);

    /// <summary>An amount with its sign and as few decimals as it needs ("$192", "$9.50").</summary>
    public static string Money(decimal amount, string? currency) =>
        Money(amount, currency, amount == decimal.Truncate(amount) ? 0 : 2);

    /// <summary>The per month price shown for an interval.</summary>
    public static decimal PerMonth(UpgradePromptPlan plan, UpgradePromptInterval interval) =>
        interval == UpgradePromptInterval.Yearly ? plan.YearlyPrice : plan.MonthlyPrice;

    /// <summary>The amount charged today: the monthly price, or twelve months at the yearly per month price.</summary>
    public static decimal Charge(UpgradePromptPlan plan, UpgradePromptInterval interval) =>
        interval == UpgradePromptInterval.Yearly ? plan.YearlyPrice * 12 : plan.MonthlyPrice;

    /// <summary>The yearly saving in whole percent of the monthly price (20 for $20 → $16), or 0 when there is none.</summary>
    public static int SavingPercent(UpgradePromptPlan plan)
    {
        if (plan.MonthlyPrice <= 0 || plan.YearlyPrice >= plan.MonthlyPrice)
        {
            return 0;
        }
        return (int)Math.Round((plan.MonthlyPrice - plan.YearlyPrice) / plan.MonthlyPrice * 100, MidpointRounding.AwayFromZero);
    }

    /// <summary>What a year of yearly billing saves against twelve monthly payments ($48 for $20 → $16).</summary>
    public static decimal SavingPerYear(UpgradePromptPlan plan) =>
        Math.Max(0, (plan.MonthlyPrice - plan.YearlyPrice) * 12);

    /// <summary>The share of the limit used, clamped to 0–1; a zero limit reads as full.</summary>
    public static double Fill(double used, double limit) =>
        limit <= 0 ? 1 : Math.Clamp(used / limit, 0, 1);

    /// <summary>True when the usage is at or over the limit.</summary>
    public static bool IsAtLimit(double used, double limit) => used >= limit;

    /// <summary>What is left of the limit, never below zero.</summary>
    public static double Left(double used, double limit) => Math.Max(0, limit - used);
}
