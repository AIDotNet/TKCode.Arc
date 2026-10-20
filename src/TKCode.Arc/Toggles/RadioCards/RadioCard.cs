using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;

namespace TKCode.Arc;

/// <summary>One card of <see cref="RadioCards"/>: indicator, label with icon, description and meta.</summary>
[PseudoClasses(ListPseudoClass)]
internal sealed class RadioCard : TogglesItem
{
    public const string ListPseudoClass = ":list";

    public static readonly StyledProperty<string?> DescriptionProperty =
        AvaloniaProperty.Register<RadioCard, string?>(nameof(Description));

    public static readonly StyledProperty<string?> DisabledReasonProperty =
        AvaloniaProperty.Register<RadioCard, string?>(nameof(DisabledReason));

    public static readonly StyledProperty<object?> MetaProperty =
        AvaloniaProperty.Register<RadioCard, object?>(nameof(Meta));

    public static readonly StyledProperty<object?> IconProperty =
        AvaloniaProperty.Register<RadioCard, object?>(nameof(Icon));

    public static readonly DirectProperty<RadioCard, string?> ShownDescriptionProperty =
        AvaloniaProperty.RegisterDirect<RadioCard, string?>(nameof(ShownDescription), o => o.ShownDescription);

    public string? Description { get => GetValue(DescriptionProperty); set => SetValue(DescriptionProperty, value); }
    public string? DisabledReason { get => GetValue(DisabledReasonProperty); set => SetValue(DisabledReasonProperty, value); }
    public object? Meta { get => GetValue(MetaProperty); set => SetValue(MetaProperty, value); }
    public object? Icon { get => GetValue(IconProperty); set => SetValue(IconProperty, value); }

    private string? _shownDescription;

    /// <summary>The disabled reason while the card is disabled (when given), otherwise the description.</summary>
    public string? ShownDescription
    {
        get => _shownDescription;
        private set => SetAndRaise(ShownDescriptionProperty, ref _shownDescription, value);
    }

    public bool IsListLayout
    {
        set => PseudoClasses.Set(ListPseudoClass, value);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == DescriptionProperty || change.Property == DisabledReasonProperty || change.Property == IsEffectivelyEnabledProperty)
        {
            ShownDescription = !IsEffectivelyEnabled && !string.IsNullOrEmpty(DisabledReason) ? DisabledReason : Description;
            AutomationProperties.SetHelpText(this, ShownDescription);
        }
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new TogglesRadioAutomationPeer(this);
}
