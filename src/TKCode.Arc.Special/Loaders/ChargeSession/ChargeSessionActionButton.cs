using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using AvaloniaButton = Avalonia.Controls.Button;

namespace TKCode.Arc;

/// <summary>
/// The action button of <see cref="ChargeSession"/> (Arc Charge session, design reference): Stop charging (outline, square
/// icon) while charging, Resume charging (solid, zap) when stopped, Start new session (solid, rotate-ccw) when complete.
/// The three labels share one cell, so the button keeps the widest label's width; the inactive ones are transparent and
/// the switch is a 160 ms crossfade. Its accessible name is the active label only.
/// </summary>
[TemplatePart(FocusRingPart, typeof(Border))]
[PseudoClasses(StoppedPseudoClass, CompletePseudoClass, SolidPseudoClass)]
internal sealed class ChargeSessionActionButton : AvaloniaButton
{
    public const string FocusRingPart = "PART_FocusRing";
    public const string StoppedPseudoClass = ":stopped";
    public const string CompletePseudoClass = ":complete";
    public const string SolidPseudoClass = ":solid";

    public static readonly StyledProperty<ChargeSessionState> StateProperty =
        AvaloniaProperty.Register<ChargeSessionActionButton, ChargeSessionState>(nameof(State));

    public static readonly StyledProperty<string?> StopTextProperty =
        AvaloniaProperty.Register<ChargeSessionActionButton, string?>(nameof(StopText));

    public static readonly StyledProperty<string?> ResumeTextProperty =
        AvaloniaProperty.Register<ChargeSessionActionButton, string?>(nameof(ResumeText));

    public static readonly StyledProperty<string?> NewSessionTextProperty =
        AvaloniaProperty.Register<ChargeSessionActionButton, string?>(nameof(NewSessionText));

    static ChargeSessionActionButton()
    {
        ClipToBoundsProperty.OverrideDefaultValue<ChargeSessionActionButton>(false);
    }

    public ChargeSessionActionButton()
    {
        UpdateState();
    }

    public ChargeSessionState State { get => GetValue(StateProperty); set => SetValue(StateProperty, value); }

    public string? StopText { get => GetValue(StopTextProperty); set => SetValue(StopTextProperty, value); }

    public string? ResumeText { get => GetValue(ResumeTextProperty); set => SetValue(ResumeTextProperty, value); }

    public string? NewSessionText { get => GetValue(NewSessionTextProperty); set => SetValue(NewSessionTextProperty, value); }

    /// <summary>The label that is showing (and read).</summary>
    public string? ActiveText => State switch
    {
        ChargeSessionState.Stopped => ResumeText,
        ChargeSessionState.Complete => NewSessionText,
        _ => StopText,
    };

    protected override Type StyleKeyOverride => typeof(ChargeSessionActionButton);

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == StateProperty || change.Property == StopTextProperty || change.Property == ResumeTextProperty ||
            change.Property == NewSessionTextProperty)
        {
            UpdateState();
        }
    }

    private void UpdateState()
    {
        PseudoClasses.Set(StoppedPseudoClass, State == ChargeSessionState.Stopped);
        PseudoClasses.Set(CompletePseudoClass, State == ChargeSessionState.Complete);
        PseudoClasses.Set(SolidPseudoClass, State != ChargeSessionState.Charging);
        AutomationProperties.SetName(this, ActiveText ?? string.Empty);
    }
}
