using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using AvaloniaButton = Avalonia.Controls.Button;

namespace TKCode.Arc;

/// <summary>
/// The pay button of <see cref="CheckoutSummary"/> (a candidate for a shared Pricing helper: cart drawer checkout,
/// live checkout, credit top-up). It shows one of four faces for <see cref="State"/>: idle (lock + <see cref="IdleLabel"/>),
/// processing (spinner + <see cref="ProcessingLabel"/>), paid (check + <see cref="PaidLabel"/> on green) and error (lock +
/// <see cref="RetryLabel"/>); every face or label change swaps upward (old 160 ms out, new 240 ms in). It is disabled while
/// processing and once paid. The state machine itself (who moves the state) belongs to the owner.
/// </summary>
internal sealed class CheckoutSummaryPayButton : AvaloniaButton
{
    public static readonly StyledProperty<CheckoutSummaryPayState> StateProperty =
        AvaloniaProperty.Register<CheckoutSummaryPayButton, CheckoutSummaryPayState>(nameof(State));

    public static readonly StyledProperty<string?> IdleLabelProperty =
        AvaloniaProperty.Register<CheckoutSummaryPayButton, string?>(nameof(IdleLabel));

    public static readonly StyledProperty<string?> ProcessingLabelProperty =
        AvaloniaProperty.Register<CheckoutSummaryPayButton, string?>(nameof(ProcessingLabel), "Processing");

    public static readonly StyledProperty<string?> PaidLabelProperty =
        AvaloniaProperty.Register<CheckoutSummaryPayButton, string?>(nameof(PaidLabel), "Paid");

    public static readonly StyledProperty<string?> RetryLabelProperty =
        AvaloniaProperty.Register<CheckoutSummaryPayButton, string?>(nameof(RetryLabel), "Try again");

    public static readonly StyledProperty<bool> IsPayEnabledProperty =
        AvaloniaProperty.Register<CheckoutSummaryPayButton, bool>(nameof(IsPayEnabled), true);

    public static readonly StyledProperty<double> GapProperty =
        AvaloniaProperty.Register<CheckoutSummaryPayButton, double>(nameof(Gap), 8);

    public const string ProcessingPseudoClass = ":processing";
    public const string PaidPseudoClass = ":paid";
    public const string ErrorPseudoClass = ":pay-error";
    public const string UnavailablePseudoClass = ":unavailable";

    private readonly CheckoutSummaryLabelSwap _swap = new();
    private CheckoutSummaryPayState _shownState;
    private string? _shownLabel;
    private bool _built;

    public CheckoutSummaryPayButton()
    {
        Content = _swap;
        AutomationProperties.SetLiveSetting(this, AutomationLiveSetting.Polite);
    }

    protected override Type StyleKeyOverride => typeof(CheckoutSummaryPayButton);

    /// <summary>The face shown.</summary>
    public CheckoutSummaryPayState State { get => GetValue(StateProperty); set => SetValue(StateProperty, value); }

    /// <summary>Idle label, such as "Pay $96.75".</summary>
    public string? IdleLabel { get => GetValue(IdleLabelProperty); set => SetValue(IdleLabelProperty, value); }

    /// <summary>Label while processing.</summary>
    public string? ProcessingLabel { get => GetValue(ProcessingLabelProperty); set => SetValue(ProcessingLabelProperty, value); }

    /// <summary>Label once paid.</summary>
    public string? PaidLabel { get => GetValue(PaidLabelProperty); set => SetValue(PaidLabelProperty, value); }

    /// <summary>Label after a failure.</summary>
    public string? RetryLabel { get => GetValue(RetryLabelProperty); set => SetValue(RetryLabelProperty, value); }

    /// <summary>False shows the button but refuses to pay (the reference's <c>disabled</c>).</summary>
    public bool IsPayEnabled { get => GetValue(IsPayEnabledProperty); set => SetValue(IsPayEnabledProperty, value); }

    /// <summary>Gap between glyph and label.</summary>
    public double Gap { get => GetValue(GapProperty); set => SetValue(GapProperty, value); }

    /// <summary>The label of the current face (what the button reads out).</summary>
    public string? CurrentLabel => LabelFor(State);

    /// <summary>The swap host (tests: which face is shown).</summary>
    internal CheckoutSummaryLabelSwap Swap => _swap;

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == StateProperty)
        {
            var state = change.GetNewValue<CheckoutSummaryPayState>();
            PseudoClasses.Set(ProcessingPseudoClass, state == CheckoutSummaryPayState.Processing);
            PseudoClasses.Set(PaidPseudoClass, state == CheckoutSummaryPayState.Paid);
            PseudoClasses.Set(ErrorPseudoClass, state == CheckoutSummaryPayState.Error);
            UpdateEnabled();
            Refresh(animate: true);
        }
        else if (change.Property == IsPayEnabledProperty)
        {
            PseudoClasses.Set(UnavailablePseudoClass, !change.GetNewValue<bool>());
            UpdateEnabled();
        }
        else if (change.Property == IdleLabelProperty || change.Property == ProcessingLabelProperty
                 || change.Property == PaidLabelProperty || change.Property == RetryLabelProperty
                 || change.Property == GapProperty)
        {
            Refresh(animate: change.Property != GapProperty);
        }
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        if (!_built)
        {
            _built = true;
            UpdateEnabled();
            Refresh(animate: false);
        }
    }

    private void UpdateEnabled()
        => IsEnabled = IsPayEnabled && State is CheckoutSummaryPayState.Idle or CheckoutSummaryPayState.Error;

    private string? LabelFor(CheckoutSummaryPayState state) => state switch
    {
        CheckoutSummaryPayState.Processing => ProcessingLabel,
        CheckoutSummaryPayState.Paid => PaidLabel,
        CheckoutSummaryPayState.Error => RetryLabel,
        _ => IdleLabel
    };

    private void Refresh(bool animate)
    {
        var state = State;
        var label = LabelFor(state);
        AutomationProperties.SetName(this, label ?? string.Empty);
        if (_swap.Current is not null && state == _shownState && label == _shownLabel)
        {
            return;
        }
        _shownState = state;
        _shownLabel = label;
        _swap.Swap(BuildFace(state, label), animate && _built);
    }

    private Control BuildFace(CheckoutSummaryPayState state, string? label)
    {
        Control glyph = state switch
        {
            CheckoutSummaryPayState.Processing => new CheckoutSummarySpinner(18),
            CheckoutSummaryPayState.Paid => new ArcGlyph { Data = ArcIcons.Check, Size = 18, StrokeThickness = 2 },
            _ => new ArcGlyph { Data = CheckoutSummaryGlyphs.LockKeyhole, Size = 16, StrokeThickness = 1.75 }
        };
        glyph.VerticalAlignment = VerticalAlignment.Center;
        var text = new TextBlock
        {
            Text = label,
            VerticalAlignment = VerticalAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis
        };
        var row = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = Gap,
            Children = { glyph, text }
        };
        return row;
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new ButtonAutomationPeer(this);
}

/// <summary>Lucide glyphs the block needs that Core does not ship.</summary>
internal static class CheckoutSummaryGlyphs
{
    /// <summary>Lucide <c>lock-keyhole</c>.</summary>
    public static Geometry LockKeyhole => ArcIcons.Parse(
        "M11 16a1 1 0 1 0 2 0a1 1 0 1 0 -2 0z M5 10h14a2 2 0 0 1 2 2v8a2 2 0 0 1 -2 2h-14a2 2 0 0 1 -2 -2v-8a2 2 0 0 1 2 -2z M7 10V7a5 5 0 0 1 10 0v3");

    /// <summary>Lucide <c>ticket-percent</c>.</summary>
    public static Geometry TicketPercent => ArcIcons.Parse(
        "M2 9a3 3 0 1 1 0 6v2a2 2 0 0 0 2 2h16a2 2 0 0 0 2-2v-2a3 3 0 1 1 0-6V7a2 2 0 0 0-2-2H4a2 2 0 0 0-2 2Z M9 9h.01 M15 9l-6 6 M15 15h.01");

    /// <summary>Lucide <c>pen-tool</c> (sample media).</summary>
    public static Geometry PenTool => ArcIcons.Parse(
        "M18 13l-.654-4.736a4 4 0 00-2.756-3.266l-9.163-2.9a2 2 0 00-2.017.493l-.82.819a2 2 0 00-.492 2.017l2.9 9.163a4 4 0 003.266 2.756L13 18 M18.648 12.352a1.205 1.205 0 011.704 0l1.296 1.296a1.205 1.205 0 010 1.704l-6.296 6.296a1.205 1.205 0 01-1.704 0l-1.296-1.296a1.205 1.205 0 010-1.704z M3 3l6.586 6.586 M9 11a2 2 0 1 0 4 0a2 2 0 1 0 -4 0z");
}
