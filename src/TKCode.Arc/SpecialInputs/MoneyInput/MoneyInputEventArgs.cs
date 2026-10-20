using Avalonia.Interactivity;

namespace TKCode.Arc;

/// <summary>Where a <see cref="MoneyInput"/> amount change came from (<see cref="MoneyInputValueChangedEventArgs.ChangeSource"/>).</summary>
public enum MoneyInputChangeSource
{
    /// <summary><see cref="MoneyInput.Value"/> was set by code or a binding.</summary>
    Programmatic,

    /// <summary>Typing or deleting in the editor (Backspace, Delete, cut, text written to the editor).</summary>
    Type,

    /// <summary>A paste into the editor.</summary>
    Paste,

    /// <summary>ArrowUp / ArrowDown / PageUp / PageDown.</summary>
    Key,

    /// <summary>A quick add chip.</summary>
    Chip,

    /// <summary>The user chose another currency and the amount was carried across (Arc <c>convertDraft</c>).</summary>
    Currency,

    /// <summary>An assistive technology set the value (value or range-value pattern).</summary>
    Automation
}

/// <summary>Where a <see cref="MoneyInput"/> currency change came from (<see cref="MoneyInputCurrencyChangedEventArgs.ChangeSource"/>).</summary>
public enum MoneyInputCurrencyChangeSource
{
    /// <summary><see cref="MoneyInput.Currency"/> was set by code or a binding.</summary>
    Programmatic,

    /// <summary>The user chose a currency in the menu.</summary>
    Menu
}

/// <summary>
/// Arguments of <see cref="MoneyInput.ValueChanged"/>: the old and new amount in major units, Arc's <c>details</c>
/// (currency, minor units and the full currency string) and where the change came from.
/// </summary>
public sealed class MoneyInputValueChangedEventArgs : RoutedEventArgs
{
    public MoneyInputValueChangedEventArgs(RoutedEvent routedEvent, object? source, decimal? oldValue, decimal? newValue,
                                           string currency, long? minorUnits, string formatted, MoneyInputChangeSource changeSource)
        : base(routedEvent, source)
    {
        ChangeSource = changeSource;
        OldValue = oldValue;
        NewValue = newValue;
        Currency = currency;
        MinorUnits = minorUnits;
        Formatted = formatted;
    }

    /// <summary>The previous amount in major units (null was empty).</summary>
    public decimal? OldValue { get; }

    /// <summary>The new amount in major units, such as 12.5 for $12.50 (null is empty).</summary>
    public decimal? NewValue { get; }

    /// <summary>ISO 4217 code of the amount.</summary>
    public string Currency { get; }

    /// <summary>The new amount in minor units (cents for USD, yen for JPY), or null when empty.</summary>
    public long? MinorUnits { get; }

    /// <summary>The new amount as a full currency string, such as "$12.50"; empty when there is no amount.</summary>
    public string Formatted { get; }

    /// <summary>Where the change came from; <see cref="MoneyInputChangeSource.Programmatic"/> for code and bindings.</summary>
    public MoneyInputChangeSource ChangeSource { get; }
}

/// <summary>Arguments of <see cref="MoneyInput.CurrencyChanged"/> (Arc's <c>onCurrencyChange</c>, plus programmatic changes).</summary>
public sealed class MoneyInputCurrencyChangedEventArgs : RoutedEventArgs
{
    public MoneyInputCurrencyChangedEventArgs(RoutedEvent routedEvent, object? source, string oldCurrency, string newCurrency,
                                              MoneyInputCurrencyChangeSource changeSource)
        : base(routedEvent, source)
    {
        OldCurrency = oldCurrency;
        NewCurrency = newCurrency;
        ChangeSource = changeSource;
    }

    /// <summary>The ISO 4217 code before the choice.</summary>
    public string OldCurrency { get; }

    /// <summary>The new ISO 4217 code.</summary>
    public string NewCurrency { get; }

    /// <summary>Whether the user chose it in the menu or code set it.</summary>
    public MoneyInputCurrencyChangeSource ChangeSource { get; }
}
