using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>
/// One option of a <see cref="BillingToggle"/>: the label and an optional savings badge. Every badge text the option can
/// show reserves the badge's width, so swapping "Save 20%" for "You save $48" never moves anything around it.
/// </summary>
[PseudoClasses(BadgePseudoClass, LargePseudoClass)]
internal sealed class BillingToggleItem : TogglesItem
{
    public const string BadgePseudoClass = ":badge";
    public const string LargePseudoClass = ":large";

    public static readonly StyledProperty<string?> BadgeProperty =
        AvaloniaProperty.Register<BillingToggleItem, string?>(nameof(Badge));

    public static readonly StyledProperty<string?> ActiveBadgeProperty =
        AvaloniaProperty.Register<BillingToggleItem, string?>(nameof(ActiveBadge));

    public static readonly DirectProperty<BillingToggleItem, string?> CurrentBadgeProperty =
        AvaloniaProperty.RegisterDirect<BillingToggleItem, string?>(nameof(CurrentBadge), o => o.CurrentBadge);

    public static readonly StyledProperty<IBrush?> ActiveBadgeBackgroundProperty =
        AvaloniaProperty.Register<BillingToggleItem, IBrush?>(nameof(ActiveBadgeBackground));

    public static readonly StyledProperty<IBrush?> ActiveBadgeForegroundProperty =
        AvaloniaProperty.Register<BillingToggleItem, IBrush?>(nameof(ActiveBadgeForeground));

    public string? Badge { get => GetValue(BadgeProperty); set => SetValue(BadgeProperty, value); }
    public string? ActiveBadge { get => GetValue(ActiveBadgeProperty); set => SetValue(ActiveBadgeProperty, value); }
    public IBrush? ActiveBadgeBackground { get => GetValue(ActiveBadgeBackgroundProperty); set => SetValue(ActiveBadgeBackgroundProperty, value); }
    public IBrush? ActiveBadgeForeground { get => GetValue(ActiveBadgeForegroundProperty); set => SetValue(ActiveBadgeForegroundProperty, value); }

    private string? _currentBadge;

    /// <summary>The badge text shown now: the active text while selected (when given), otherwise the badge.</summary>
    public string? CurrentBadge
    {
        get => _currentBadge;
        private set => SetAndRaise(CurrentBadgeProperty, ref _currentBadge, value);
    }

    public bool IsLarge
    {
        set => PseudoClasses.Set(LargePseudoClass, value);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == BadgeProperty || change.Property == ActiveBadgeProperty || change.Property == ContentProperty)
        {
            Refresh();
        }
    }

    protected override void OnIsSelectedChanged() => Refresh();

    private void Refresh()
    {
        CurrentBadge = IsSelected ? ActiveBadge ?? Badge : Badge;
        PseudoClasses.Set(BadgePseudoClass, !string.IsNullOrEmpty(Badge) || !string.IsNullOrEmpty(ActiveBadge));
        // The badge is part of the option's text, so the savings are read with its name.
        var label = Content?.ToString();
        AutomationProperties.SetName(this, string.IsNullOrEmpty(CurrentBadge) ? label : $"{label} {CurrentBadge}");
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new TogglesRadioAutomationPeer(this);
}
