using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Interactivity;
using Avalonia.Media;
using TKCode.Arc.Primitives;
using AvaloniaButton = Avalonia.Controls.Button;

namespace TKCode.Arc;

/// <summary>
/// A guardrail card that shows personal data being redacted before a prompt reaches the model (Arc PII redaction,
/// design reference). Scan marks each name, email, phone number and card number in the message with its type, then swaps
/// each one for a numbered placeholder such as <c>[EMAIL_1]</c> in a single crossfade. Send carries the safe prompt along
/// a small flow (you, the PII guard, the model), the reply streams back in quick chunks with its placeholders, and the
/// guard puts the original values back for the person. Show original flips the message between the safe prompt and what
/// was typed, and the message can be edited before a scan.
/// <para>
/// Detection comes from <see cref="Detector"/> (your DLP or NER service) or the built-in <see cref="PiiRedactor"/>; the
/// reply from <see cref="Sender"/> or a simulation, which the card says. Without a <see cref="Message"/> it plays a
/// <see cref="Simulation"/> (a conversation you pass) on a loop while at least a quarter of it is on screen, until
/// someone interacts; with neither it shows an empty message field.
/// </para>
/// </summary>
[TemplatePart(EditorPart, typeof(PiiRedactionEditor))]
[TemplatePart(OriginalPart, typeof(DecoratedTextBlock))]
[TemplatePart(RedactedPart, typeof(DecoratedTextBlock))]
[TemplatePart(MessageLabelPart, typeof(ToolCallText))]
[TemplatePart(ShowOriginalPart, typeof(Switch))]
[TemplatePart(CellsPart, typeof(PiiRedactionCellsPanel))]
[TemplatePart(FlowYouPart, typeof(PiiRedactionFlowNode))]
[TemplatePart(FlowGuardPart, typeof(PiiRedactionFlowNode))]
[TemplatePart(FlowModelPart, typeof(PiiRedactionFlowNode))]
[TemplatePart(FlowOutPart, typeof(PiiRedactionConnector))]
[TemplatePart(FlowInPart, typeof(PiiRedactionConnector))]
[TemplatePart(ReplyPlaceholderPart, typeof(TextBlock))]
[TemplatePart(StreamPart, typeof(PiiRedactionStreamText))]
[TemplatePart(RestoredPart, typeof(DecoratedTextBlock))]
[TemplatePart(StatusGlyphPart, typeof(ArcGlyph))]
[TemplatePart(StatusPart, typeof(ToolCallText))]
[TemplatePart(ResetPart, typeof(AvaloniaButton))]
[TemplatePart(PrimaryPart, typeof(AvaloniaButton))]
[TemplatePart(AnnouncerPart, typeof(ExplainerAnnouncer))]
[PseudoClasses(NarrowPseudoClass, CompactPseudoClass, ResettablePseudoClass, SimulatedPseudoClass, WorkingPseudoClass,
    ErrorPseudoClass, DonePseudoClass)]
public partial class PiiRedaction : TemplatedControl
{
    internal const string EditorPart = "PART_Editor";
    internal const string OriginalPart = "PART_Original";
    internal const string RedactedPart = "PART_Redacted";
    internal const string MessageLabelPart = "PART_MessageLabel";
    internal const string ShowOriginalPart = "PART_ShowOriginal";
    internal const string CellsPart = "PART_Cells";
    internal const string FlowYouPart = "PART_FlowYou";
    internal const string FlowGuardPart = "PART_FlowGuard";
    internal const string FlowModelPart = "PART_FlowModel";
    internal const string FlowOutPart = "PART_FlowOut";
    internal const string FlowInPart = "PART_FlowIn";
    internal const string ReplyPlaceholderPart = "PART_ReplyPlaceholder";
    internal const string StreamPart = "PART_Stream";
    internal const string RestoredPart = "PART_Restored";
    internal const string StatusGlyphPart = "PART_StatusGlyph";
    internal const string StatusPart = "PART_Status";
    internal const string ResetPart = "PART_Reset";
    internal const string PrimaryPart = "PART_Primary";
    internal const string AnnouncerPart = "PART_Announcer";

    internal const string NarrowPseudoClass = ":narrow";
    internal const string CompactPseudoClass = ":compact";
    internal const string ResettablePseudoClass = ":resettable";
    internal const string SimulatedPseudoClass = ":simulated";
    internal const string WorkingPseudoClass = ":working";
    internal const string ErrorPseudoClass = ":error";
    internal const string DonePseudoClass = ":done";

    /// <summary>Below this width the four type cells fall into two rows of two.</summary>
    internal const double NarrowWidth = 520;

    /// <summary>Below this width the card tightens its gaps and hides the Simulated tag.</summary>
    internal const double CompactWidth = 400;

    /// <summary>The message to check (two-way). Null shows the <see cref="Simulation"/>'s message, or a blank field without one.</summary>
    public static readonly StyledProperty<string?> MessageProperty =
        AvaloniaProperty.Register<PiiRedaction, string?>(nameof(Message), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>Whether people can edit the message before a scan. Cmd or Ctrl plus Enter scans from the field.</summary>
    public static readonly StyledProperty<bool> IsEditableProperty =
        AvaloniaProperty.Register<PiiRedaction, bool>(nameof(IsEditable), true);

    /// <summary>
    /// Names the built-in detector always flags, on top of introductions like "I'm", "this is" and "my name is". Pass the
    /// signed-in person's name and their contacts. Null uses the <see cref="Simulation"/>'s names, or none.
    /// </summary>
    public static readonly StyledProperty<IReadOnlyList<string>?> NamesProperty =
        AvaloniaProperty.Register<PiiRedaction, IReadOnlyList<string>?>(nameof(Names));

    /// <summary>
    /// Replaces the built-in detector, for example with your DLP or NER service. Return spans with an exclusive end; the
    /// token is cancelled when the person presses Stop or Reset. A thrown exception shows its message with Retry.
    /// </summary>
    public static readonly StyledProperty<Func<string, CancellationToken, Task<IReadOnlyList<PiiMatch>>>?> DetectorProperty =
        AvaloniaProperty.Register<PiiRedaction, Func<string, CancellationToken, Task<IReadOnlyList<PiiMatch>>>?>(nameof(Detector));

    /// <summary>
    /// Sends the safe prompt to your model and returns its reply, placeholders included; the card restores them. Without
    /// it the reply is simulated (the <see cref="Simulation"/>'s reply, or a short stand-in) and the card says so. A thrown
    /// exception shows its message with Retry.
    /// </summary>
    public static readonly StyledProperty<Func<string, CancellationToken, Task<string>>?> SenderProperty =
        AvaloniaProperty.Register<PiiRedaction, Func<string, CancellationToken, Task<string>>?>(nameof(Sender));

    /// <summary>The model the safe prompt goes to, on the flow and above the reply; null names the <see cref="Simulation"/>'s model, or none.</summary>
    public static readonly StyledProperty<string?> ModelNameProperty =
        AvaloniaProperty.Register<PiiRedaction, string?>(nameof(ModelName));

    /// <summary>The model's mark, a 24 × 24 stroked glyph drawn at 16px; the sparkle by default.</summary>
    public static readonly StyledProperty<Geometry?> ModelMarkProperty =
        AvaloniaProperty.Register<PiiRedaction, Geometry?>(nameof(ModelMark), ArcIcons.Sparkles);

    /// <summary>The mark's color; the accent by default.</summary>
    public static readonly StyledProperty<IBrush?> ModelMarkBrushProperty =
        AvaloniaProperty.Register<PiiRedaction, IBrush?>(nameof(ModelMarkBrush));

    /// <summary>
    /// Plays scan, send and restore on a loop while at least a quarter of the card is on screen. Stops for good as soon as
    /// someone edits, toggles or presses a button. Off with reduced motion.
    /// </summary>
    public static readonly StyledProperty<bool> AutoPlayProperty =
        AvaloniaProperty.Register<PiiRedaction, bool>(nameof(AutoPlay), true);

    /// <summary>
    /// A conversation to play: its message (while the card has no <see cref="Message"/>), the names to flag, the model and
    /// its written reply. Your own values win over the simulation's; null stops it and shows the empty field.
    /// </summary>
    public static readonly StyledProperty<PiiRedactionSimulation?> SimulationProperty =
        AvaloniaProperty.Register<PiiRedaction, PiiRedactionSimulation?>(nameof(Simulation));

    /// <summary>Shows the original message instead of the safe prompt once it is redacted (two-way).</summary>
    public static readonly StyledProperty<bool> ShowOriginalProperty =
        AvaloniaProperty.Register<PiiRedaction, bool>(nameof(ShowOriginal), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>The color of names: their marks, placeholders and cell (theme: Series1).</summary>
    public static readonly StyledProperty<IBrush?> NameBrushProperty =
        AvaloniaProperty.Register<PiiRedaction, IBrush?>(nameof(NameBrush));

    /// <summary>The color of emails (theme: Series2).</summary>
    public static readonly StyledProperty<IBrush?> EmailBrushProperty =
        AvaloniaProperty.Register<PiiRedaction, IBrush?>(nameof(EmailBrush));

    /// <summary>The color of phone numbers (theme: Series3).</summary>
    public static readonly StyledProperty<IBrush?> PhoneBrushProperty =
        AvaloniaProperty.Register<PiiRedaction, IBrush?>(nameof(PhoneBrush));

    /// <summary>The color of card numbers (theme: Series4).</summary>
    public static readonly StyledProperty<IBrush?> CardBrushProperty =
        AvaloniaProperty.Register<PiiRedaction, IBrush?>(nameof(CardBrush));

    /// <summary>Every word the card shows or announces.</summary>
    public static readonly StyledProperty<PiiRedactionLabels> LabelsProperty =
        AvaloniaProperty.Register<PiiRedaction, PiiRedactionLabels>(nameof(Labels), PiiRedactionLabels.Default);

    /// <summary>Where the card is in its flow (read only).</summary>
    public static readonly DirectProperty<PiiRedaction, PiiRedactionPhase> PhaseProperty =
        AvaloniaProperty.RegisterDirect<PiiRedaction, PiiRedactionPhase>(nameof(Phase), o => o.Phase);

    /// <summary>The safe prompt once the message is redacted, otherwise null (read only).</summary>
    public static readonly DirectProperty<PiiRedaction, string?> SafePromptProperty =
        AvaloniaProperty.RegisterDirect<PiiRedaction, string?>(nameof(SafePrompt), o => o.SafePrompt);

    /// <summary>The restored reply once it is back, otherwise null (read only).</summary>
    public static readonly DirectProperty<PiiRedaction, string?> ReplyProperty =
        AvaloniaProperty.RegisterDirect<PiiRedaction, string?>(nameof(Reply), o => o.Reply);

    /// <summary>Raised once the message is redacted, with the safe prompt and every entity. Bubbles.</summary>
    public static readonly RoutedEvent<PiiRedactedEventArgs> RedactedEvent =
        RoutedEvent.Register<PiiRedaction, PiiRedactedEventArgs>(nameof(Redacted), RoutingStrategies.Bubble);

    /// <summary>Raised when the reply is restored, with the model's raw reply and the text the person sees. Bubbles.</summary>
    public static readonly RoutedEvent<PiiReplyRestoredEventArgs> ReplyRestoredEvent =
        RoutedEvent.Register<PiiRedaction, PiiReplyRestoredEventArgs>(nameof(ReplyRestored), RoutingStrategies.Bubble);

    /// <summary>Raised on every phase change. Bubbles.</summary>
    public static readonly RoutedEvent<PiiRedactionPhaseChangedEventArgs> PhaseChangedEvent =
        RoutedEvent.Register<PiiRedaction, PiiRedactionPhaseChangedEventArgs>(nameof(PhaseChanged), RoutingStrategies.Bubble);

    internal static readonly StyledProperty<CornerRadius> MarkCornerRadiusProperty =
        AvaloniaProperty.Register<PiiRedaction, CornerRadius>(nameof(MarkCornerRadius), new CornerRadius(5));

    internal static readonly StyledProperty<Thickness> MarkOutsetProperty =
        AvaloniaProperty.Register<PiiRedaction, Thickness>(nameof(MarkOutset), new Thickness(2, 1));

    internal static readonly StyledProperty<double> MarkTintOpacityProperty =
        AvaloniaProperty.Register<PiiRedaction, double>(nameof(MarkTintOpacity), 0.18);

    internal static readonly StyledProperty<double> CellTintOpacityProperty =
        AvaloniaProperty.Register<PiiRedaction, double>(nameof(CellTintOpacity), 0.1);

    private PiiRedactionPhase _phase;
    private string? _safePrompt;
    private string? _reply;

    /// <inheritdoc cref="MessageProperty"/>
    public string? Message { get => GetValue(MessageProperty); set => SetValue(MessageProperty, value); }

    /// <inheritdoc cref="IsEditableProperty"/>
    public bool IsEditable { get => GetValue(IsEditableProperty); set => SetValue(IsEditableProperty, value); }

    /// <inheritdoc cref="NamesProperty"/>
    public IReadOnlyList<string>? Names { get => GetValue(NamesProperty); set => SetValue(NamesProperty, value); }

    /// <inheritdoc cref="DetectorProperty"/>
    public Func<string, CancellationToken, Task<IReadOnlyList<PiiMatch>>>? Detector
    {
        get => GetValue(DetectorProperty);
        set => SetValue(DetectorProperty, value);
    }

    /// <inheritdoc cref="SenderProperty"/>
    public Func<string, CancellationToken, Task<string>>? Sender { get => GetValue(SenderProperty); set => SetValue(SenderProperty, value); }

    /// <inheritdoc cref="ModelNameProperty"/>
    public string? ModelName { get => GetValue(ModelNameProperty); set => SetValue(ModelNameProperty, value); }

    /// <inheritdoc cref="ModelMarkProperty"/>
    public Geometry? ModelMark { get => GetValue(ModelMarkProperty); set => SetValue(ModelMarkProperty, value); }

    /// <inheritdoc cref="ModelMarkBrushProperty"/>
    public IBrush? ModelMarkBrush { get => GetValue(ModelMarkBrushProperty); set => SetValue(ModelMarkBrushProperty, value); }

    /// <inheritdoc cref="AutoPlayProperty"/>
    public bool AutoPlay { get => GetValue(AutoPlayProperty); set => SetValue(AutoPlayProperty, value); }

    /// <inheritdoc cref="SimulationProperty"/>
    public PiiRedactionSimulation? Simulation { get => GetValue(SimulationProperty); set => SetValue(SimulationProperty, value); }

    /// <inheritdoc cref="ShowOriginalProperty"/>
    public bool ShowOriginal { get => GetValue(ShowOriginalProperty); set => SetValue(ShowOriginalProperty, value); }

    /// <inheritdoc cref="NameBrushProperty"/>
    public IBrush? NameBrush { get => GetValue(NameBrushProperty); set => SetValue(NameBrushProperty, value); }

    /// <inheritdoc cref="EmailBrushProperty"/>
    public IBrush? EmailBrush { get => GetValue(EmailBrushProperty); set => SetValue(EmailBrushProperty, value); }

    /// <inheritdoc cref="PhoneBrushProperty"/>
    public IBrush? PhoneBrush { get => GetValue(PhoneBrushProperty); set => SetValue(PhoneBrushProperty, value); }

    /// <inheritdoc cref="CardBrushProperty"/>
    public IBrush? CardBrush { get => GetValue(CardBrushProperty); set => SetValue(CardBrushProperty, value); }

    /// <inheritdoc cref="LabelsProperty"/>
    public PiiRedactionLabels Labels { get => GetValue(LabelsProperty); set => SetValue(LabelsProperty, value); }

    /// <inheritdoc cref="PhaseProperty"/>
    public PiiRedactionPhase Phase
    {
        get => _phase;
        private set
        {
            var old = _phase;
            if (SetAndRaise(PhaseProperty, ref _phase, value))
            {
                OnPhaseChanged(old, value);
            }
        }
    }

    /// <inheritdoc cref="SafePromptProperty"/>
    public string? SafePrompt { get => _safePrompt; private set => SetAndRaise(SafePromptProperty, ref _safePrompt, value); }

    /// <inheritdoc cref="ReplyProperty"/>
    public string? Reply { get => _reply; private set => SetAndRaise(ReplyProperty, ref _reply, value); }

    /// <summary>The entities of the last redaction, in the order they appear in the message (empty before a scan).</summary>
    public IReadOnlyList<PiiEntity> Entities => _result?.Entities ?? [];

    /// <summary>Whether the reply is simulated (no <see cref="Sender"/>): the card shows a Simulated tag and says so.</summary>
    public bool IsSimulated => Sender is null;

    /// <inheritdoc cref="RedactedEvent"/>
    public event EventHandler<PiiRedactedEventArgs>? Redacted
    {
        add => AddHandler(RedactedEvent, value);
        remove => RemoveHandler(RedactedEvent, value);
    }

    /// <inheritdoc cref="ReplyRestoredEvent"/>
    public event EventHandler<PiiReplyRestoredEventArgs>? ReplyRestored
    {
        add => AddHandler(ReplyRestoredEvent, value);
        remove => RemoveHandler(ReplyRestoredEvent, value);
    }

    /// <inheritdoc cref="PhaseChangedEvent"/>
    public event EventHandler<PiiRedactionPhaseChangedEventArgs>? PhaseChanged
    {
        add => AddHandler(PhaseChangedEvent, value);
        remove => RemoveHandler(PhaseChangedEvent, value);
    }

    internal CornerRadius MarkCornerRadius { get => GetValue(MarkCornerRadiusProperty); set => SetValue(MarkCornerRadiusProperty, value); }

    internal Thickness MarkOutset { get => GetValue(MarkOutsetProperty); set => SetValue(MarkOutsetProperty, value); }

    internal double MarkTintOpacity { get => GetValue(MarkTintOpacityProperty); set => SetValue(MarkTintOpacityProperty, value); }

    internal double CellTintOpacity { get => GetValue(CellTintOpacityProperty); set => SetValue(CellTintOpacityProperty, value); }

    /// <summary>The model name above the reply: <see cref="EffectiveModelName"/> (theme).</summary>
    internal static readonly DirectProperty<PiiRedaction, string> ShownModelNameProperty =
        AvaloniaProperty.RegisterDirect<PiiRedaction, string>(nameof(ShownModelName), o => o.ShownModelName);

    private string _shownModelName = string.Empty;

    /// <inheritdoc cref="ShownModelNameProperty"/>
    internal string ShownModelName { get => _shownModelName; private set => SetAndRaise(ShownModelNameProperty, ref _shownModelName, value); }

    /// <summary>The message on screen: <see cref="Message"/>, the simulation's, or an empty field.</summary>
    internal string EffectiveMessage => Message ?? Simulation?.Message ?? string.Empty;

    /// <summary>The names the built-in detector flags: <see cref="Names"/>, the simulation's, or none.</summary>
    internal IReadOnlyList<string> EffectiveNames => Names ?? Simulation?.Names ?? [];

    /// <summary>The model the safe prompt goes to: <see cref="ModelName"/>, the simulation's, or none (empty).</summary>
    internal string EffectiveModelName => ModelName ?? Simulation?.ModelName ?? string.Empty;

    /// <summary>The simulated reply: the simulation's written one for its own message, else a short stand-in.</summary>
    internal string SimulatedReply(IReadOnlyList<PiiEntity> entities) =>
        Simulation is { Reply: { Length: > 0 } reply } simulation && EffectiveMessage == simulation.Message
            ? reply
            : PiiRedactionSimulation.StandInReply(entities);
}
