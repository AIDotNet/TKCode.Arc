using System.Globalization;
using Avalonia;

namespace TKCode.Arc;

/// <summary>How a <see cref="MetricTickerItem"/> shows its last change.</summary>
public enum MetricTickerChangeFormat
{
    /// <summary>The change relative to the previous value, as a percentage ("+3.2%").</summary>
    Percent,

    /// <summary>The change in the metric's own unit ("+12 ms").</summary>
    Absolute,

    /// <summary>No change is shown.</summary>
    None
}

/// <summary>Where a <see cref="MetricTicker"/> lays its metrics out.</summary>
public enum MetricTickerMode
{
    /// <summary>One line that scrolls on its own, like a stock ticker; it pauses on hover and with its pause button.</summary>
    Scroll,

    /// <summary>Metrics in even columns that stay put; values roll in place as they update.</summary>
    Roll
}

/// <summary>
/// One metric of a <see cref="MetricTicker"/>: a label and a live value. Each time <see cref="Value"/> changes the ticker
/// rolls the digits and shows the change since the previous value, colored by whether it went the good way.
/// </summary>
public class MetricTickerItem : AvaloniaObject
{
    /// <summary>The metric's name ("Requests / s").</summary>
    public static readonly StyledProperty<string?> LabelProperty =
        AvaloniaProperty.Register<MetricTickerItem, string?>(nameof(Label));

    /// <summary>The current value; set it as new readings arrive. The first value set is the starting point, later ones are changes.</summary>
    public static readonly StyledProperty<double> ValueProperty =
        AvaloniaProperty.Register<MetricTickerItem, double>(nameof(Value));

    /// <summary>Digits after the decimal point (0 to 6).</summary>
    public static readonly StyledProperty<int> DecimalsProperty =
        AvaloniaProperty.Register<MetricTickerItem, int>(nameof(Decimals), coerce: static (_, v) => Math.Clamp(v, 0, 6));

    /// <summary>Text before the value, such as "$".</summary>
    public static readonly StyledProperty<string?> PrefixProperty =
        AvaloniaProperty.Register<MetricTickerItem, string?>(nameof(Prefix));

    /// <summary>Text after the value, such as " ms" or "%".</summary>
    public static readonly StyledProperty<string?> SuffixProperty =
        AvaloniaProperty.Register<MetricTickerItem, string?>(nameof(Suffix));

    /// <summary>True (default) when a rise is good news (shown in the success color); false for metrics such as latency or errors.</summary>
    public static readonly StyledProperty<bool> IsHigherBetterProperty =
        AvaloniaProperty.Register<MetricTickerItem, bool>(nameof(IsHigherBetter), true);

    /// <summary>How the change since the previous value is shown (percent by default).</summary>
    public static readonly StyledProperty<MetricTickerChangeFormat> ChangeFormatProperty =
        AvaloniaProperty.Register<MetricTickerItem, MetricTickerChangeFormat>(nameof(ChangeFormat));

    /// <summary>The last change of <see cref="Value"/> (new minus previous); NaN until the value has changed once.</summary>
    public static readonly DirectProperty<MetricTickerItem, double> ChangeProperty =
        AvaloniaProperty.RegisterDirect<MetricTickerItem, double>(nameof(Change), o => o.Change);

    /// <summary>The value before the last change; NaN until the value has changed once.</summary>
    public static readonly DirectProperty<MetricTickerItem, double> PreviousValueProperty =
        AvaloniaProperty.RegisterDirect<MetricTickerItem, double>(nameof(PreviousValue), o => o.PreviousValue);

    private double _change = double.NaN;
    private double _previous = double.NaN;
    private bool _hasValue;

    /// <summary>Creates an empty metric.</summary>
    public MetricTickerItem()
    {
    }

    /// <summary>Creates a metric with a label and a first value.</summary>
    public MetricTickerItem(string label, double value, int decimals = 0, string? suffix = null)
    {
        Label = label;
        Value = value;
        Decimals = decimals;
        Suffix = suffix;
    }

    /// <summary>The metric's name ("Requests / s").</summary>
    public string? Label { get => GetValue(LabelProperty); set => SetValue(LabelProperty, value); }

    /// <summary>The current value; set it as new readings arrive. The first value set is the starting point, later ones are changes.</summary>
    public double Value { get => GetValue(ValueProperty); set => SetValue(ValueProperty, value); }

    /// <summary>Digits after the decimal point (0 to 6).</summary>
    public int Decimals { get => GetValue(DecimalsProperty); set => SetValue(DecimalsProperty, value); }

    /// <summary>Text before the value, such as "$".</summary>
    public string? Prefix { get => GetValue(PrefixProperty); set => SetValue(PrefixProperty, value); }

    /// <summary>Text after the value, such as " ms" or "%".</summary>
    public string? Suffix { get => GetValue(SuffixProperty); set => SetValue(SuffixProperty, value); }

    /// <summary>True (default) when a rise is good news (shown in the success color); false for metrics such as latency or errors.</summary>
    public bool IsHigherBetter { get => GetValue(IsHigherBetterProperty); set => SetValue(IsHigherBetterProperty, value); }

    /// <summary>How the change since the previous value is shown (percent by default).</summary>
    public MetricTickerChangeFormat ChangeFormat { get => GetValue(ChangeFormatProperty); set => SetValue(ChangeFormatProperty, value); }

    /// <summary>The last change of <see cref="Value"/> (new minus previous); NaN until the value has changed once.</summary>
    public double Change { get => _change; private set => SetAndRaise(ChangeProperty, ref _change, value); }

    /// <summary>The value before the last change; NaN until the value has changed once.</summary>
    public double PreviousValue { get => _previous; private set => SetAndRaise(PreviousValueProperty, ref _previous, value); }

    /// <summary>The value as shown: prefix, grouped digits, suffix.</summary>
    public string FormatValue(double value)
        => string.Concat(Prefix, value.ToString("N" + Decimals.ToString(CultureInfo.InvariantCulture), CultureInfo.GetCultureInfo("en-US")), Suffix);

    /// <summary>The change as shown ("+3.2%", "−12 ms"), or null when there is none to show.</summary>
    public string? FormatChange()
    {
        var change = Change;
        if (double.IsNaN(change) || ChangeFormat == MetricTickerChangeFormat.None)
        {
            return null;
        }
        double amount;
        string text;
        if (ChangeFormat == MetricTickerChangeFormat.Percent)
        {
            if (double.IsNaN(PreviousValue) || PreviousValue == 0)
            {
                return null;
            }
            amount = change / Math.Abs(PreviousValue) * 100;
            text = Math.Abs(amount).ToString("0.0", CultureInfo.InvariantCulture) + "%";
        }
        else
        {
            amount = change;
            text = string.Concat(Prefix, Math.Abs(amount).ToString("N" + Decimals.ToString(CultureInfo.InvariantCulture), CultureInfo.GetCultureInfo("en-US")), Suffix);
        }
        var sign = Math.Round(amount, ChangeFormat == MetricTickerChangeFormat.Percent ? 1 : Decimals) switch
        {
            > 0 => "+",
            < 0 => "−",
            _ => string.Empty
        };
        return sign + text;
    }

    /// <summary>1 when the last change was good news, -1 when it was bad, 0 when there was none.</summary>
    public int Trend
    {
        get
        {
            var change = Change;
            if (double.IsNaN(change) || change == 0)
            {
                return 0;
            }
            return (change > 0) == IsHigherBetter ? 1 : -1;
        }
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ValueProperty)
        {
            var (old, now) = change.GetOldAndNewValue<double>();
            if (!_hasValue)
            {
                // The first value set is where the metric starts, not a change.
                _hasValue = true;
                return;
            }
            PreviousValue = old;
            Change = now - old;
        }
    }
}
