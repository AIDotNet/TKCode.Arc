using Avalonia;
using Avalonia.Interactivity;
using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>How a <see cref="TrialBanner"/> is drawn.</summary>
public enum TrialBannerVariant
{
    /// <summary>A slim row for the top of an app: the ring, two lines of copy and the actions.</summary>
    Banner,

    /// <summary>A sidebar card (up to 340px wide) with the list of what the workspace loses.</summary>
    Card,

    /// <summary>A compact header pill that opens the card in a popover.</summary>
    Pill
}

/// <summary>Where a trial is, as a <see cref="TrialBanner"/> shows it.</summary>
public enum TrialStatus
{
    /// <summary>Days left before the warning starts: the accent ring and the days in the copy.</summary>
    Active,

    /// <summary>The last days (<see cref="TrialBanner.WarnAt"/>): the warning tone, and the copy names the end day.</summary>
    Warning,

    /// <summary>The trial has ended: a lock in place of the ring, and the copy says what the workspace lost.</summary>
    Expired,

    /// <summary>A payment method is on file: a check in a full ring and a Manage billing action.</summary>
    Upgraded
}

/// <summary>What a <see cref="TrialBanner"/> becomes after it is dismissed or snoozed.</summary>
public enum TrialBannerCollapse
{
    /// <summary>It folds into the trial pill, which opens the banner again.</summary>
    Pill,

    /// <summary>It renders nothing.</summary>
    None
}

/// <summary>The snooze choices of a <see cref="TrialBanner"/>.</summary>
public enum TrialSnoozeOption
{
    /// <summary>Until the start of tomorrow.</summary>
    Tomorrow,

    /// <summary>Until the start of the day three days from today (offered while more than three days are left).</summary>
    ThreeDays,

    /// <summary>Until the first day of the warning (offered while the warning has not started).</summary>
    UntilWarning
}

/// <summary>One thing the workspace loses without paying, listed in the card of a <see cref="TrialBanner"/>.</summary>
public sealed class TrialFeature : AvaloniaObject
{
    /// <summary>A stable id for the row.</summary>
    public static readonly StyledProperty<string?> IdProperty =
        AvaloniaProperty.Register<TrialFeature, string?>(nameof(Id));

    /// <summary>The feature's name ("SAML single sign-on").</summary>
    public static readonly StyledProperty<string?> LabelProperty =
        AvaloniaProperty.Register<TrialFeature, string?>(nameof(Label));

    /// <summary>A short muted detail after the name ("12 seats"), or null.</summary>
    public static readonly StyledProperty<string?> DetailProperty =
        AvaloniaProperty.Register<TrialFeature, string?>(nameof(Detail));

    /// <summary>The 14px mark before the name (a 24 × 24 stroked glyph); a check when null.</summary>
    public static readonly StyledProperty<Geometry?> IconProperty =
        AvaloniaProperty.Register<TrialFeature, Geometry?>(nameof(Icon));

    /// <summary>Creates an empty feature.</summary>
    public TrialFeature()
    {
    }

    /// <summary>Creates a feature with its name, and optionally a detail and a mark.</summary>
    public TrialFeature(string id, string label, string? detail = null, Geometry? icon = null)
    {
        Id = id;
        Label = label;
        Detail = detail;
        Icon = icon;
    }

    /// <inheritdoc cref="IdProperty"/>
    public string? Id { get => GetValue(IdProperty); set => SetValue(IdProperty, value); }

    /// <inheritdoc cref="LabelProperty"/>
    public string? Label { get => GetValue(LabelProperty); set => SetValue(LabelProperty, value); }

    /// <inheritdoc cref="DetailProperty"/>
    public string? Detail { get => GetValue(DetailProperty); set => SetValue(DetailProperty, value); }

    /// <inheritdoc cref="IconProperty"/>
    public Geometry? Icon { get => GetValue(IconProperty); set => SetValue(IconProperty, value); }
}

/// <summary>The arguments of <see cref="TrialBanner.Snoozed"/>: the chosen option and when the snooze ends.</summary>
public sealed class TrialSnoozedEventArgs : RoutedEventArgs
{
    /// <summary>Creates the arguments.</summary>
    public TrialSnoozedEventArgs(RoutedEvent routedEvent, TrialSnoozeOption option, DateTimeOffset until)
        : base(routedEvent)
    {
        Option = option;
        Until = until;
    }

    /// <summary>The snooze the person chose.</summary>
    public TrialSnoozeOption Option { get; }

    /// <summary>When the snooze ends (the start of that local day). Persist it and stop rendering the banner until then.</summary>
    public DateTimeOffset Until { get; }
}

/// <summary>Where the add-payment action is.</summary>
internal enum TrialBillingPhase
{
    Idle,
    Busy,
    Failed
}
