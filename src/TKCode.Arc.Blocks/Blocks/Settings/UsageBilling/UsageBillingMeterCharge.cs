using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;

namespace TKCode.Arc;

/// <summary>
/// The right side of a <see cref="UsageBilling"/> meter row: what the overage costs (or "Included" while usage stays
/// within the plan) and the rate under it.
/// </summary>
[PseudoClasses(IncludedPseudoClass)]
internal sealed class UsageBillingMeterCharge : TemplatedControl
{
    public const string IncludedPseudoClass = ":included";

    public static readonly StyledProperty<string?> AmountProperty =
        AvaloniaProperty.Register<UsageBillingMeterCharge, string?>(nameof(Amount));

    public static readonly StyledProperty<string?> RateProperty =
        AvaloniaProperty.Register<UsageBillingMeterCharge, string?>(nameof(Rate));

    public static readonly StyledProperty<bool> IsIncludedProperty =
        AvaloniaProperty.Register<UsageBillingMeterCharge, bool>(nameof(IsIncluded));

    public string? Amount
    {
        get => GetValue(AmountProperty);
        set => SetValue(AmountProperty, value);
    }

    public string? Rate
    {
        get => GetValue(RateProperty);
        set => SetValue(RateProperty, value);
    }

    public bool IsIncluded
    {
        get => GetValue(IsIncludedProperty);
        set => SetValue(IsIncludedProperty, value);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IsIncludedProperty)
        {
            PseudoClasses.Set(IncludedPseudoClass, IsIncluded);
        }
    }
}
