using System.Globalization;
using TKCode.Arc.Primitives;
using Avalonia.Controls;

namespace TKCode.Arc;

/// <summary>
/// A formatted slider value whose digits roll in the direction it moved (P14, Core <see cref="RollingDigits"/>). Any
/// text around the number (currency, units) stays put as prefix or suffix; text without a plain number falls back to a
/// static label. Reduced and Off motion are handled by RollingDigits.
/// </summary>
internal sealed class SliderNumberText : Panel
{
    private static readonly CultureInfo s_culture = CultureInfo.GetCultureInfo("en-US");
    private readonly RollingDigits _digits = new() { CultureName = "en-US" };
    private readonly TextBlock _text = new() { IsVisible = false };

    public SliderNumberText()
    {
        Children.Add(_digits);
        Children.Add(_text);
    }

    public string Text { get; private set; } = string.Empty;

    internal RollingDigits Digits => _digits;

    public void SetText(string text)
    {
        Text = text;
        if (TryParse(text, out var prefix, out var number, out var decimals, out var grouping, out var suffix))
        {
            _digits.Prefix = string.IsNullOrEmpty(prefix) ? null : prefix;
            _digits.Suffix = string.IsNullOrEmpty(suffix) ? null : suffix;
            _digits.UseGrouping = grouping;
            _digits.Decimals = decimals;
            _digits.Value = number;
            _digits.IsVisible = true;
            _text.IsVisible = false;
            return;
        }
        _text.Text = text;
        _text.IsVisible = true;
        _digits.IsVisible = false;
    }

    /// <summary>Splits "$1,250.5 /mo" into "$", 1250.5 (1 decimal, grouped) and " /mo".</summary>
    internal static bool TryParse(string text, out string prefix, out double number, out int decimals, out bool grouping, out string suffix)
    {
        prefix = suffix = string.Empty;
        number = 0;
        decimals = 0;
        grouping = false;
        var first = -1;
        var last = -1;
        for (var i = 0; i < text.Length; i++)
        {
            if (char.IsAsciiDigit(text[i]))
            {
                first = first < 0 ? i : first;
                last = i;
            }
        }
        if (first < 0)
        {
            return false;
        }
        var negative = first > 0 && (text[first - 1] == '-' || text[first - 1] == '−');
        var core = text.Substring(first, last - first + 1);
        foreach (var c in core)
        {
            if (!char.IsAsciiDigit(c) && c != ',' && c != '.')
            {
                return false;
            }
        }
        grouping = core.Contains(',');
        var dot = core.IndexOf('.');
        decimals = dot < 0 ? 0 : core.Length - dot - 1;
        if (!double.TryParse(core.Replace(",", string.Empty), NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out number))
        {
            return false;
        }
        // Only take it when RollingDigits would print exactly the same digits.
        if (number.ToString((grouping ? "N" : "F") + decimals, s_culture) != core)
        {
            return false;
        }
        if (negative)
        {
            number = -number;
            first--;
        }
        prefix = text[..first];
        suffix = text[(last + 1)..];
        return true;
    }
}
