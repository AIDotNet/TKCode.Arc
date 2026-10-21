using System.Globalization;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls.Primitives;

namespace TKCode.Arc;

/// <summary>
/// The price beside a <see cref="BillingToggle"/> (Arc <c>BillingPrice</c>): the amount rolls digit by digit to its new
/// value (<see cref="AnimatedCounter"/>, P14), a struck-through previous price fades in from the left while its width
/// springs open, and the period text swaps in place while its width eases, so nothing beside them jumps.
/// </summary>
public class BillingPrice : TemplatedControl
{
    #region 公共属性定义

    /// <summary>The price shown, formatted with Currency and Decimals.</summary>
    public static readonly StyledProperty<decimal> AmountProperty =
        AvaloniaProperty.Register<BillingPrice, decimal>(nameof(Amount));

    /// <summary>Sign or text placed before the amount (default "$").</summary>
    public static readonly StyledProperty<string> CurrencyProperty =
        AvaloniaProperty.Register<BillingPrice, string>(nameof(Currency), "$");

    /// <summary>Digits after the decimal point; negative values count as 0.</summary>
    public static readonly StyledProperty<int> DecimalsProperty =
        AvaloniaProperty.Register<BillingPrice, int>(nameof(Decimals));

    /// <summary>Previous price, shown struck through when it is higher than <see cref="Amount"/>.</summary>
    public static readonly StyledProperty<decimal?> WasAmountProperty =
        AvaloniaProperty.Register<BillingPrice, decimal?>(nameof(WasAmount));

    /// <summary>Period after the price, e.g. "per month". Swaps in place when it changes.</summary>
    public static readonly StyledProperty<string?> PeriodProperty =
        AvaloniaProperty.Register<BillingPrice, string?>(nameof(Period));

    /// <inheritdoc cref="AmountProperty"/>
    public decimal Amount { get => GetValue(AmountProperty); set => SetValue(AmountProperty, value); }
    /// <inheritdoc cref="CurrencyProperty"/>
    public string Currency { get => GetValue(CurrencyProperty); set => SetValue(CurrencyProperty, value); }
    /// <inheritdoc cref="DecimalsProperty"/>
    public int Decimals { get => GetValue(DecimalsProperty); set => SetValue(DecimalsProperty, value); }
    public decimal? WasAmount { get => GetValue(WasAmountProperty); set => SetValue(WasAmountProperty, value); }
    public string? Period { get => GetValue(PeriodProperty); set => SetValue(PeriodProperty, value); }

    #endregion

    #region 内部属性定义

    internal static readonly DirectProperty<BillingPrice, double> CounterValueProperty =
        AvaloniaProperty.RegisterDirect<BillingPrice, double>(nameof(CounterValue), o => o.CounterValue);

    internal static readonly DirectProperty<BillingPrice, bool> ShowsWasProperty =
        AvaloniaProperty.RegisterDirect<BillingPrice, bool>(nameof(ShowsWas), o => o.ShowsWas);

    internal static readonly DirectProperty<BillingPrice, string?> WasTextProperty =
        AvaloniaProperty.RegisterDirect<BillingPrice, string?>(nameof(WasText), o => o.WasText);

    private double _counterValue;
    private bool _showsWas;
    private string? _wasText;

    internal double CounterValue { get => _counterValue; private set => SetAndRaise(CounterValueProperty, ref _counterValue, value); }
    internal bool ShowsWas { get => _showsWas; private set => SetAndRaise(ShowsWasProperty, ref _showsWas, value); }
    internal string? WasText { get => _wasText; private set => SetAndRaise(WasTextProperty, ref _wasText, value); }

    #endregion

    public BillingPrice()
    {
        Refresh();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == AmountProperty || change.Property == WasAmountProperty || change.Property == CurrencyProperty ||
            change.Property == DecimalsProperty || change.Property == PeriodProperty)
        {
            Refresh();
        }
    }

    private void Refresh()
    {
        var culture = CultureInfo.GetCultureInfo("en-US");
        var decimals = Math.Max(0, Decimals);
        CounterValue = (double)Amount;
        ShowsWas = WasAmount is { } was && was > Amount;
        // Keep the last text while the old price leaves, so it fades out with its value.
        if (WasAmount is { } previous)
        {
            WasText = Currency + previous.ToString("F" + decimals, culture);
        }
        var text = Currency + Amount.ToString("N" + decimals, culture);
        if (ShowsWas && WasText is { } wasText)
        {
            text += $", was {wasText}";
        }
        if (!string.IsNullOrEmpty(Period))
        {
            text += " " + Period;
        }
        AutomationProperties.SetName(this, text);
    }
}
