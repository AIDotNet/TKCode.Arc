using Avalonia;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using AvaloniaButton = Avalonia.Controls.Button;

namespace TKCode.Arc;

/// <summary>
/// What an <see cref="AiRunStatusLine"/> shows for one kind and text: the row (icon cell, text, count) and the detail line.
/// The line makes a new face for every kind, text or detail, so the old one fades out in its own colors while the new one
/// comes in, and a new running face starts its sweep from the beginning; the count and the shimmer change in place.
/// </summary>
[PseudoClasses(IdlePseudoClass, RunningPseudoClass, NeedsCheckPseudoClass, DonePseudoClass, AcceptedPseudoClass,
    StoppedPseudoClass, FailedPseudoClass, CountPseudoClass, DetailPseudoClass)]
internal sealed class AiRunStatusFace : TemplatedControl
{
    public const string IdlePseudoClass = ":idle";
    public const string RunningPseudoClass = ":running";
    public const string NeedsCheckPseudoClass = ":needs-check";
    public const string DonePseudoClass = ":done";
    public const string AcceptedPseudoClass = ":accepted";
    public const string StoppedPseudoClass = ":stopped";
    public const string FailedPseudoClass = ":failed";

    /// <summary>Set while a count shows.</summary>
    public const string CountPseudoClass = ":count";

    /// <summary>Set while a detail line shows.</summary>
    public const string DetailPseudoClass = ":detail";

    /// <summary>The kind the face shows; fixed for the face's life in a line.</summary>
    public static readonly StyledProperty<AiRunStatusKind> KindProperty =
        AvaloniaProperty.Register<AiRunStatusFace, AiRunStatusKind>(nameof(Kind));

    /// <summary>The main text.</summary>
    public static readonly StyledProperty<string?> TextProperty =
        AvaloniaProperty.Register<AiRunStatusFace, string?>(nameof(Text));

    /// <summary>The count after the running text.</summary>
    public static readonly StyledProperty<string?> CountTextProperty =
        AvaloniaProperty.Register<AiRunStatusFace, string?>(nameof(CountText));

    /// <summary>The second line (stopped and failed).</summary>
    public static readonly StyledProperty<string?> DetailTextProperty =
        AvaloniaProperty.Register<AiRunStatusFace, string?>(nameof(DetailText));

    /// <summary>Whether the running text sweeps.</summary>
    public static readonly StyledProperty<bool> IsShimmeringProperty =
        AvaloniaProperty.Register<AiRunStatusFace, bool>(nameof(IsShimmering), true);

    /// <summary>Whether the shimmer sweeps now: running and <see cref="IsShimmering"/> (template use).</summary>
    internal static readonly DirectProperty<AiRunStatusFace, bool> SweepsProperty =
        AvaloniaProperty.RegisterDirect<AiRunStatusFace, bool>(nameof(Sweeps), o => o.Sweeps);

    private bool _sweeps;

    static AiRunStatusFace()
    {
        FocusableProperty.OverrideDefaultValue<AiRunStatusFace>(false);
        IsTabStopProperty.OverrideDefaultValue<AiRunStatusFace>(false);
    }

    public AiRunStatusFace()
    {
        UpdatePseudoClasses();
    }

    /// <inheritdoc cref="KindProperty"/>
    public AiRunStatusKind Kind { get => GetValue(KindProperty); set => SetValue(KindProperty, value); }

    /// <inheritdoc cref="TextProperty"/>
    public string? Text { get => GetValue(TextProperty); set => SetValue(TextProperty, value); }

    /// <inheritdoc cref="CountTextProperty"/>
    public string? CountText { get => GetValue(CountTextProperty); set => SetValue(CountTextProperty, value); }

    /// <inheritdoc cref="DetailTextProperty"/>
    public string? DetailText { get => GetValue(DetailTextProperty); set => SetValue(DetailTextProperty, value); }

    /// <inheritdoc cref="IsShimmeringProperty"/>
    public bool IsShimmering { get => GetValue(IsShimmeringProperty); set => SetValue(IsShimmeringProperty, value); }

    internal bool Sweeps { get => _sweeps; private set => SetAndRaise(SweepsProperty, ref _sweeps, value); }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == KindProperty || change.Property == CountTextProperty || change.Property == DetailTextProperty
            || change.Property == IsShimmeringProperty)
        {
            UpdatePseudoClasses();
        }
    }

    private void UpdatePseudoClasses()
    {
        var kind = Kind;
        PseudoClasses.Set(IdlePseudoClass, kind == AiRunStatusKind.Idle);
        PseudoClasses.Set(RunningPseudoClass, kind == AiRunStatusKind.Running);
        PseudoClasses.Set(NeedsCheckPseudoClass, kind == AiRunStatusKind.NeedsCheck);
        PseudoClasses.Set(DonePseudoClass, kind == AiRunStatusKind.Done);
        PseudoClasses.Set(AcceptedPseudoClass, kind == AiRunStatusKind.Accepted);
        PseudoClasses.Set(StoppedPseudoClass, kind == AiRunStatusKind.Stopped);
        PseudoClasses.Set(FailedPseudoClass, kind == AiRunStatusKind.Failed);
        PseudoClasses.Set(CountPseudoClass, kind == AiRunStatusKind.Running && !string.IsNullOrEmpty(CountText));
        PseudoClasses.Set(DetailPseudoClass, !string.IsNullOrEmpty(DetailText));
        Sweeps = kind == AiRunStatusKind.Running && IsShimmering;
    }
}

/// <summary>
/// The action pill of an <see cref="AiRunStatusLine"/> ("Review"): 28 high, padding 0 11, pill radius, 1px <c>Border</c> on
/// the surface, 12 / 16.8 medium in Foreground; hover (mouse and pen) turns the fill <c>SurfaceMuted</c> and the edge
/// <c>BorderStrong</c> in 160ms. Keyboard focus draws the focus ring 4px out.
/// </summary>
[PseudoClasses(HoverPseudoClass)]
internal sealed class AiRunStatusAction : AvaloniaButton
{
    /// <summary>Mouse or pen over the pill.</summary>
    public const string HoverPseudoClass = ":hover";

    protected override Type StyleKeyOverride => typeof(AiRunStatusAction);

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IsEffectivelyEnabledProperty && !change.GetNewValue<bool>())
        {
            PseudoClasses.Set(HoverPseudoClass, false);
        }
    }

    protected override void OnPointerEntered(PointerEventArgs e)
    {
        base.OnPointerEntered(e);
        // Arc's hover is (hover: hover) and (pointer: fine): touch never leaves a hover look behind.
        PseudoClasses.Set(HoverPseudoClass, e.Pointer.Type != PointerType.Touch && IsEffectivelyEnabled);
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        PseudoClasses.Set(HoverPseudoClass, false);
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new ActionPeer(this);

    /// <summary>A button named by its word.</summary>
    private sealed class ActionPeer(AiRunStatusAction owner) : ButtonAutomationPeer(owner)
    {
        protected override IReadOnlyList<AutomationPeer>? GetChildrenCore() => null;
    }
}
