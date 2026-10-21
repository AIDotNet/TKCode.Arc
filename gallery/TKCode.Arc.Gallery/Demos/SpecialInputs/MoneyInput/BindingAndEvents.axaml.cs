using System.Globalization;
using Avalonia.Controls;
using Avalonia.Interactivity;

namespace TKCode.Arc.Gallery.Demos;

public partial class MoneyInputBindingAndEventsDemo : UserControl
{
    public MoneyInputBindingAndEventsDemo()
    {
        InitializeComponent();
        ShowAmount();
        // Value and Currency are two-way; read the amount back whenever either changes.
        Amount.PropertyChanged += (_, e) =>
        {
            if (e.Property == MoneyInput.ValueProperty || e.Property == MoneyInput.CurrencyProperty)
            {
                ShowAmount();
            }
        };
    }

    private void OnValueChanged(object? sender, MoneyInputValueChangedEventArgs e)
        => EventLine.Text = $"ValueChanged ({e.ChangeSource}): {Show(e.OldValue)} → {Show(e.NewValue)}, {e.Currency}, "
                            + $"minor {e.MinorUnits?.ToString(CultureInfo.InvariantCulture) ?? "null"}, \"{e.Formatted}\"";

    private void OnSetClick(object? sender, RoutedEventArgs e) => Amount.Value = 99.99m;

    private void OnClearClick(object? sender, RoutedEventArgs e) => Amount.Value = null;

    private void ShowAmount()
        => Readout.Text = $"Value = {Show(Amount.Value)}   "
                          + $"MinorUnits = {Amount.MinorUnits?.ToString(CultureInfo.InvariantCulture) ?? "null"}   "
                          + $"Formatted = \"{Amount.FormattedValue}\"   Currency = {Amount.Currency}";

    private static string Show(decimal? value) => value?.ToString(CultureInfo.InvariantCulture) ?? "null";
}
