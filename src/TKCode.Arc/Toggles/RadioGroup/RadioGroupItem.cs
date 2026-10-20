using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;

namespace TKCode.Arc;

/// <summary>One row of a <see cref="RadioGroup"/>: a ring with a springing dot, the label and an optional description.</summary>
internal sealed class RadioGroupItem : TogglesItem
{
    public static readonly StyledProperty<string?> DescriptionProperty =
        AvaloniaProperty.Register<RadioGroupItem, string?>(nameof(Description));

    public string? Description
    {
        get => GetValue(DescriptionProperty);
        set => SetValue(DescriptionProperty, value);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == DescriptionProperty)
        {
            AutomationProperties.SetHelpText(this, Description);
        }
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new TogglesRadioAutomationPeer(this);
}
