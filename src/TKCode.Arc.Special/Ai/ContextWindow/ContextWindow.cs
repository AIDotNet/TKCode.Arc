using Avalonia;
using Avalonia.Collections;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Interactivity;
using Avalonia.Metadata;
using AvaloniaButton = Avalonia.Controls.Button;

namespace TKCode.Arc;

/// <summary>
/// A model's context window as a fixed budget of 200 cells (20 × 10) that fills in reading order: the system prompt, tool
/// definitions, files and memory, the summary and the conversation, then free space, with the output reserve held as
/// outlined cells at the end. A dashed line marks the compaction threshold. The header reads the tokens used, the window,
/// the percentage and the free tokens, with a pill to switch models; a legend lists every part with its tokens and share;
/// Send message, Attach file and Compact now change the window, and a status line says what happened.
/// <para>
/// Crossing the threshold after a send or an attachment, or switching to a model the window overfills, folds every
/// message but the newest two into one summary (about 6% of them, between 4K and 24K), and the space comes back. The card
/// rewrites <see cref="Items"/> as it goes and raises <see cref="ItemsChanged"/>, <see cref="Compacted"/> and
/// <see cref="ModelChanged"/>. Given no data at all it shows an invented sample session with three sample models
/// (<see cref="ContextWindowSamples"/>).
/// </para>
/// </summary>
[TemplatePart(ReadoutPart, typeof(ContextWindowReadout))]
[TemplatePart(ModelPillPart, typeof(ContextWindowModelPill))]
[TemplatePart(GridPart, typeof(ContextWindowGrid))]
[TemplatePart(BodyPart, typeof(ContextWindowBodyPanel))]
[TemplatePart(LegendPart, typeof(Avalonia.Controls.Panel))]
[TemplatePart(SendButtonPart, typeof(AvaloniaButton))]
[TemplatePart(AttachButtonPart, typeof(AvaloniaButton))]
[TemplatePart(CompactButtonPart, typeof(AvaloniaButton))]
[TemplatePart(StatusPart, typeof(ContextWindowStatusLine))]
[PseudoClasses(SummarizingPseudoClass, WarningPseudoClass, OverPseudoClass, StackedPseudoClass, CompactHeaderPseudoClass)]
public partial class ContextWindow : TemplatedControl
{
    internal const string ReadoutPart = "PART_Readout";
    internal const string ModelPillPart = "PART_ModelPill";
    internal const string GridPart = "PART_Grid";
    internal const string BodyPart = "PART_Body";
    internal const string LegendPart = "PART_Legend";
    internal const string CellCaptionPart = "PART_CellCaption";
    internal const string ThresholdCaptionPart = "PART_ThresholdCaption";
    internal const string SendButtonPart = "PART_SendButton";
    internal const string AttachButtonPart = "PART_AttachButton";
    internal const string CompactButtonPart = "PART_CompactButton";
    internal const string SendTextPart = "PART_SendText";
    internal const string AttachTextPart = "PART_AttachText";
    internal const string CompactTextPart = "PART_CompactText";
    internal const string StatusPart = "PART_Status";
    internal const string AnnouncerPart = "PART_Announcer";
    internal const string FocusHolderPart = "PART_FocusHolder";

    /// <summary>Set while older messages fold into the summary.</summary>
    public const string SummarizingPseudoClass = ":summarizing";

    /// <summary>Set from 85% of the threshold on.</summary>
    public const string WarningPseudoClass = ":warning";

    /// <summary>Set at or past the threshold.</summary>
    public const string OverPseudoClass = ":over";

    /// <summary>Set when the content width is 640 or less: the legend moves under the grid and the cells tighten.</summary>
    public const string StackedPseudoClass = ":stacked";

    /// <summary>Set when the content width is 420 or less: the model pill moves under the readout.</summary>
    public const string CompactHeaderPseudoClass = ":compact-header";

    /// <summary>The content width (the card's width less its 24px padding on each side) at or below which the legend stacks.</summary>
    public const double StackWidth = 640;

    /// <summary>The content width at or below which the header stacks.</summary>
    public const double HeaderStackWidth = 420;

    /// <summary>The wait after a send or an attachment that crossed the threshold before folding starts.</summary>
    public static readonly TimeSpan ThresholdCompactionDelay = TimeSpan.FromMilliseconds(540);

    /// <summary>The wait after a switch to a model the window overfills before folding starts.</summary>
    public static readonly TimeSpan ModelCompactionDelay = TimeSpan.FromMilliseconds(600);

    /// <summary>How long the summarizing phase lasts.</summary>
    public static readonly TimeSpan SummarizingDuration = TimeSpan.FromMilliseconds(700);

    #region Properties

    /// <summary>Defines the <see cref="Models"/> property.</summary>
    public static readonly StyledProperty<AvaloniaList<ContextWindowModel>> ModelsProperty =
        AvaloniaProperty.Register<ContextWindow, AvaloniaList<ContextWindowModel>>(nameof(Models));

    /// <summary>Defines the <see cref="SelectedModel"/> property.</summary>
    public static readonly StyledProperty<ContextWindowModel?> SelectedModelProperty =
        AvaloniaProperty.Register<ContextWindow, ContextWindowModel?>(nameof(SelectedModel), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>Defines the <see cref="Items"/> property.</summary>
    public static readonly StyledProperty<AvaloniaList<ContextWindowItem>> ItemsProperty =
        AvaloniaProperty.Register<ContextWindow, AvaloniaList<ContextWindowItem>>(nameof(Items));

    /// <summary>Defines the <see cref="Turns"/> property.</summary>
    public static readonly StyledProperty<AvaloniaList<ContextWindowTurn>> TurnsProperty =
        AvaloniaProperty.Register<ContextWindow, AvaloniaList<ContextWindowTurn>>(nameof(Turns));

    /// <summary>Defines the <see cref="Files"/> property.</summary>
    public static readonly StyledProperty<AvaloniaList<ContextWindowTurn>> FilesProperty =
        AvaloniaProperty.Register<ContextWindow, AvaloniaList<ContextWindowTurn>>(nameof(Files));

    /// <summary>Defines the <see cref="Threshold"/> property.</summary>
    public static readonly StyledProperty<double> ThresholdProperty =
        AvaloniaProperty.Register<ContextWindow, double>(nameof(Threshold), 0.8, coerce: CoerceThreshold);

    /// <summary>Defines the <see cref="Summarizer"/> property.</summary>
    public static readonly StyledProperty<Func<IReadOnlyList<ContextWindowItem>, long>?> SummarizerProperty =
        AvaloniaProperty.Register<ContextWindow, Func<IReadOnlyList<ContextWindowItem>, long>?>(nameof(Summarizer));

    /// <summary>Defines the <see cref="KeepRecentMessages"/> property.</summary>
    public static readonly StyledProperty<int> KeepRecentMessagesProperty =
        AvaloniaProperty.Register<ContextWindow, int>(nameof(KeepRecentMessages), 2, coerce: (_, value) => Math.Max(0, value));

    /// <summary>Defines the <see cref="AutoCompact"/> property.</summary>
    public static readonly StyledProperty<bool> AutoCompactProperty =
        AvaloniaProperty.Register<ContextWindow, bool>(nameof(AutoCompact), true);

    /// <summary>Defines the <see cref="Labels"/> property.</summary>
    public static readonly StyledProperty<ContextWindowLabels> LabelsProperty =
        AvaloniaProperty.Register<ContextWindow, ContextWindowLabels>(nameof(Labels), new ContextWindowLabels());

    /// <summary>Defines the <see cref="Phase"/> property.</summary>
    public static readonly DirectProperty<ContextWindow, ContextWindowPhase> PhaseProperty =
        AvaloniaProperty.RegisterDirect<ContextWindow, ContextWindowPhase>(nameof(Phase), o => o.Phase);

    /// <summary>Defines the <see cref="UsedTokens"/> property.</summary>
    public static readonly DirectProperty<ContextWindow, long> UsedTokensProperty =
        AvaloniaProperty.RegisterDirect<ContextWindow, long>(nameof(UsedTokens), o => o.UsedTokens);

    /// <summary>Defines the <see cref="FreeTokens"/> property.</summary>
    public static readonly DirectProperty<ContextWindow, long> FreeTokensProperty =
        AvaloniaProperty.RegisterDirect<ContextWindow, long>(nameof(FreeTokens), o => o.FreeTokens);

    /// <summary>Defines the <see cref="CanCompact"/> property.</summary>
    public static readonly DirectProperty<ContextWindow, bool> CanCompactProperty =
        AvaloniaProperty.RegisterDirect<ContextWindow, bool>(nameof(CanCompact), o => o.CanCompact);

    /// <summary>Defines the <see cref="ModelChanged"/> routed event.</summary>
    public static readonly RoutedEvent<ContextWindowModelChangedEventArgs> ModelChangedEvent =
        RoutedEvent.Register<ContextWindow, ContextWindowModelChangedEventArgs>(nameof(ModelChanged), RoutingStrategies.Bubble);

    /// <summary>Defines the <see cref="Compacted"/> routed event.</summary>
    public static readonly RoutedEvent<ContextWindowCompactedEventArgs> CompactedEvent =
        RoutedEvent.Register<ContextWindow, ContextWindowCompactedEventArgs>(nameof(Compacted), RoutingStrategies.Bubble);

    /// <summary>Defines the <see cref="ItemsChanged"/> routed event.</summary>
    public static readonly RoutedEvent<ContextWindowItemsChangedEventArgs> ItemsChangedEvent =
        RoutedEvent.Register<ContextWindow, ContextWindowItemsChangedEventArgs>(nameof(ItemsChanged), RoutingStrategies.Bubble);

    /// <summary>
    /// The models the pill switches between, in menu order, each with its window and output reserve. Empty uses the sample
    /// models (or only <see cref="SelectedModel"/> when it is set).
    /// </summary>
    public AvaloniaList<ContextWindowModel> Models { get => GetValue(ModelsProperty); set => SetValue(ModelsProperty, value); }

    /// <summary>The model shown (two-way): the pill and the menu write it. Null shows the first model.</summary>
    public ContextWindowModel? SelectedModel { get => GetValue(SelectedModelProperty); set => SetValue(SelectedModelProperty, value); }

    /// <summary>
    /// What is in the window. Items are grouped by category in the grid, so their order matters only within a category.
    /// Send message, Attach file, <see cref="AddItem"/> and compaction rewrite this list. When it, <see cref="Models"/>,
    /// <see cref="Turns"/> and <see cref="Files"/> are all empty the card shows the sample session.
    /// </summary>
    [Content]
    public AvaloniaList<ContextWindowItem> Items { get => GetValue(ItemsProperty); set => SetValue(ItemsProperty, value); }

    /// <summary>The turns Send message adds, in order, starting over after the last. Empty hides Send message (outside the sample).</summary>
    public AvaloniaList<ContextWindowTurn> Turns { get => GetValue(TurnsProperty); set => SetValue(TurnsProperty, value); }

    /// <summary>The files Attach file adds, in order, starting over after the last. Empty hides Attach file (outside the sample).</summary>
    public AvaloniaList<ContextWindowTurn> Files { get => GetValue(FilesProperty); set => SetValue(FilesProperty, value); }

    /// <summary>The share of the window at which compaction runs on its own (default 0.8). The dashed line sits on the nearest row boundary.</summary>
    public double Threshold { get => GetValue(ThresholdProperty); set => SetValue(ThresholdProperty, value); }

    /// <summary>
    /// Returns the summary's size in tokens for the messages being folded. Null uses about 6% of them, at least 4K and at
    /// most 24K.
    /// </summary>
    public Func<IReadOnlyList<ContextWindowItem>, long>? Summarizer { get => GetValue(SummarizerProperty); set => SetValue(SummarizerProperty, value); }

    /// <summary>How many of the newest messages a compaction keeps (default 2). Compact now is available while there are more.</summary>
    public int KeepRecentMessages { get => GetValue(KeepRecentMessagesProperty); set => SetValue(KeepRecentMessagesProperty, value); }

    /// <summary>
    /// Whether the card folds on its own after a send, an attachment, an added item or a model switch leaves it at or past
    /// the threshold (default true). Off, the status still says the threshold was passed and Compact now stays available.
    /// </summary>
    public bool AutoCompact { get => GetValue(AutoCompactProperty); set => SetValue(AutoCompactProperty, value); }

    /// <summary>Every word the card shows or announces.</summary>
    public ContextWindowLabels Labels { get => GetValue(LabelsProperty); set => SetValue(LabelsProperty, value); }

    /// <summary>Idle, or Summarizing while older messages fold (about 700ms; every action and the model menu are off).</summary>
    public ContextWindowPhase Phase { get => _phase; private set => SetAndRaise(PhaseProperty, ref _phase, value); }

    /// <summary>Tokens used by every item; it can pass the window.</summary>
    public long UsedTokens { get => _usedTokens; private set => SetAndRaise(UsedTokensProperty, ref _usedTokens, value); }

    /// <summary>Tokens left before the output reserve (the window less the used tokens and the reserve), never below 0.</summary>
    public long FreeTokens { get => _freeTokens; private set => SetAndRaise(FreeTokensProperty, ref _freeTokens, value); }

    /// <summary>True while Compact now is available: idle, with more messages than <see cref="KeepRecentMessages"/>.</summary>
    public bool CanCompact { get => _canCompact; private set => SetAndRaise(CanCompactProperty, ref _canCompact, value); }

    /// <summary>Raised after the model changed (from the menu or through <see cref="SelectedModel"/>).</summary>
    public event EventHandler<ContextWindowModelChangedEventArgs>? ModelChanged
    {
        add => AddHandler(ModelChangedEvent, value);
        remove => RemoveHandler(ModelChangedEvent, value);
    }

    /// <summary>Raised when a compaction lands, with what it folded and the summary it became.</summary>
    public event EventHandler<ContextWindowCompactedEventArgs>? Compacted
    {
        add => AddHandler(CompactedEvent, value);
        remove => RemoveHandler(CompactedEvent, value);
    }

    /// <summary>Raised after a send, an attachment, an added item or a compaction changed the items.</summary>
    public event EventHandler<ContextWindowItemsChangedEventArgs>? ItemsChanged
    {
        add => AddHandler(ItemsChangedEvent, value);
        remove => RemoveHandler(ItemsChangedEvent, value);
    }

    #endregion

    private ContextWindowPhase _phase;
    private long _usedTokens;
    private long _freeTokens;
    private bool _canCompact;

    /// <summary>Creates the card; with no data it shows the sample session.</summary>
    public ContextWindow()
    {
        SetCurrentValue(ModelsProperty, new AvaloniaList<ContextWindowModel>());
        SetCurrentValue(ItemsProperty, new AvaloniaList<ContextWindowItem>());
        SetCurrentValue(TurnsProperty, new AvaloniaList<ContextWindowTurn>());
        SetCurrentValue(FilesProperty, new AvaloniaList<ContextWindowTurn>());
        InitializeBehavior();
    }

    private static double CoerceThreshold(AvaloniaObject owner, double value) =>
        double.IsFinite(value) ? Math.Clamp(value, 0.01, 1) : 0.8;
}
