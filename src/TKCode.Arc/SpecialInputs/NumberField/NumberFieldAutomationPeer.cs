using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;

namespace TKCode.Arc;

/// <summary>
/// Spin button peer of a <see cref="NumberField"/> (Arc <c>role="spinbutton"</c>): <see cref="IRangeValueProvider"/>
/// with the field's limits, step and large step, and a value text that carries the prefix and suffix
/// (<c>aria-valuetext</c>).
/// </summary>
internal sealed class NumberFieldAutomationPeer : TextBoxAutomationPeer, IRangeValueProvider, IValueProvider
{
    public NumberFieldAutomationPeer(NumberField owner)
        : base(owner)
    {
    }

    private NumberField Field => (NumberField)Owner;

    protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Spinner;

    public double Minimum => (double)Field.Minimum;

    public double Maximum => (double)Field.Maximum;

    double IRangeValueProvider.Value => (double)Field.Value;

    public double SmallChange => (double)Field.EffectiveStep;

    public double LargeChange => (double)Field.EffectiveLargeStep;

    void IRangeValueProvider.SetValue(double value) => Field.SetValueFromAutomation((decimal)value);

    string? IValueProvider.Value => Field.ValueText;

    void IValueProvider.SetValue(string? value) => Field.SetTextFromAutomation(value);

    protected override void OwnerPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        base.OwnerPropertyChanged(sender, e);
        if (e.Property == NumberField.ValueProperty)
        {
            RaisePropertyChangedEvent(RangeValuePatternIdentifiers.ValueProperty,
                (double)e.GetOldValue<decimal>(), (double)e.GetNewValue<decimal>());
        }
        else if (e.Property == NumberField.MinimumProperty)
        {
            RaisePropertyChangedEvent(RangeValuePatternIdentifiers.MinimumProperty,
                (double)e.GetOldValue<decimal>(), (double)e.GetNewValue<decimal>());
        }
        else if (e.Property == NumberField.MaximumProperty)
        {
            RaisePropertyChangedEvent(RangeValuePatternIdentifiers.MaximumProperty,
                (double)e.GetOldValue<decimal>(), (double)e.GetNewValue<decimal>());
        }
    }
}
