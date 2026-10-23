using System.Windows.Input;
using Avalonia;
using Avalonia.Collections;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Interactivity;

namespace TKCode.Arc;

/// <summary>
/// An assistant beside a page the person reads: the page on the left, the assistant on the right (or a bottom sheet in a
/// narrow container). The assistant reads the whole page, answers questions about it or about a selection, and when the
/// page should change it answers with a card that shows the change as a word diff. The person applies it (the block on the
/// page sweeps with a tint and an Undo pill appears), discards it, or undoes it.
/// <code>
/// &lt;arc:AiSidePanel Page="{Binding Page}" Responder="{Binding Responder}" AssistantName="Orbit" /&gt;
/// </code>
/// </summary>
public partial class AiSidePanel : TemplatedControl
{
    #region Properties

    /// <summary>The page to read and change. Replacing the object resets the thread and the undo.</summary>
    public static readonly StyledProperty<AiPanelPage?> PageProperty =
        AvaloniaProperty.Register<AiSidePanel, AiPanelPage?>(nameof(Page));

    /// <summary>Answers the questions; null lets the box send nothing.</summary>
    public static readonly StyledProperty<IAiPanelResponder?> ResponderProperty =
        AvaloniaProperty.Register<AiSidePanel, IAiPanelResponder?>(nameof(Responder));

    /// <summary>Whether the assistant is open (two-way): docked beside the page, or a bottom sheet in a narrow container.</summary>
    public static readonly StyledProperty<bool> IsOpenProperty =
        AvaloniaProperty.Register<AiSidePanel, bool>(nameof(IsOpen), true, defaultBindingMode: BindingMode.TwoWay);

    /// <summary>The assistant's name, used in the header, the reopen button and the status texts.</summary>
    public static readonly StyledProperty<string> AssistantNameProperty =
        AvaloniaProperty.Register<AiSidePanel, string>(nameof(AssistantName), "Assistant");

    /// <summary>A second line in the header, such as the model's name.</summary>
    public static readonly StyledProperty<string?> ModelNameProperty =
        AvaloniaProperty.Register<AiSidePanel, string?>(nameof(ModelName));

    /// <summary>The assistant's mark (a geometry in a 24 × 24 box or an image); null shows a spark icon.</summary>
    public static readonly StyledProperty<object?> AssistantMarkProperty =
        AvaloniaProperty.Register<AiSidePanel, object?>(nameof(AssistantMark));

    /// <summary>The width of the docked assistant.</summary>
    public static readonly StyledProperty<double> PanelWidthProperty =
        AvaloniaProperty.Register<AiSidePanel, double>(nameof(PanelWidth), 360);

    /// <summary>The widest the page column grows.</summary>
    public static readonly StyledProperty<double> PageMaxWidthProperty =
        AvaloniaProperty.Register<AiSidePanel, double>(nameof(PageMaxWidth), 680);

    /// <summary>Below this container width the assistant is a bottom sheet over the page.</summary>
    public static readonly StyledProperty<double> SheetBreakpointProperty =
        AvaloniaProperty.Register<AiSidePanel, double>(nameof(SheetBreakpoint), 640);

    /// <summary>The height of the bottom sheet as a share of the container's height.</summary>
    public static readonly StyledProperty<double> SheetHeightRatioProperty =
        AvaloniaProperty.Register<AiSidePanel, double>(nameof(SheetHeightRatio), 0.74);

    /// <summary>How often streamed text is released (50 ms): each step shows a few whole words.</summary>
    public static readonly StyledProperty<TimeSpan> StreamIntervalProperty =
        AvaloniaProperty.Register<AiSidePanel, TimeSpan>(nameof(StreamInterval), TimeSpan.FromMilliseconds(50));

    /// <summary>The fewest whole words a step shows.</summary>
    public static readonly StyledProperty<int> MinWordsPerStepProperty =
        AvaloniaProperty.Register<AiSidePanel, int>(nameof(MinWordsPerStep), 4);

    /// <summary>The most whole words a step shows; a long backlog is released at this rate to catch up.</summary>
    public static readonly StyledProperty<int> MaxWordsPerStepProperty =
        AvaloniaProperty.Register<AiSidePanel, int>(nameof(MaxWordsPerStep), 7);

    /// <summary>Whether the assistant is a bottom sheet because the container is narrow (read-only).</summary>
    public static readonly DirectProperty<AiSidePanel, bool> IsSheetModeProperty =
        AvaloniaProperty.RegisterDirect<AiSidePanel, bool>(nameof(IsSheetMode), o => o.IsSheetMode);

    /// <summary>Whether the assistant is reading the page or writing (read-only).</summary>
    public static readonly DirectProperty<AiSidePanel, bool> IsBusyProperty =
        AvaloniaProperty.RegisterDirect<AiSidePanel, bool>(nameof(IsBusy), o => o.IsBusy);

    /// <summary>The selection the next question is about (read-only); <c>ClearSelection</c> removes it.</summary>
    public static readonly DirectProperty<AiSidePanel, AiPanelSelection?> SelectionProperty =
        AvaloniaProperty.RegisterDirect<AiSidePanel, AiPanelSelection?>(nameof(Selection), o => o.Selection);

    /// <summary>The change the Undo pill would undo (read-only).</summary>
    public static readonly DirectProperty<AiSidePanel, AiPanelChange?> LastAppliedProperty =
        AvaloniaProperty.RegisterDirect<AiSidePanel, AiPanelChange?>(nameof(LastApplied), o => o.LastApplied);

    /// <summary>Run when the person applies a change, with the <see cref="AiPanelChange"/> as the parameter.</summary>
    public static readonly StyledProperty<ICommand?> ApplyCommandProperty =
        AvaloniaProperty.Register<AiSidePanel, ICommand?>(nameof(ApplyCommand));

    /// <summary>Run when the person undoes a change.</summary>
    public static readonly StyledProperty<ICommand?> UndoCommandProperty =
        AvaloniaProperty.Register<AiSidePanel, ICommand?>(nameof(UndoCommand));

    /// <summary>Run when the person discards a change.</summary>
    public static readonly StyledProperty<ICommand?> DiscardCommandProperty =
        AvaloniaProperty.Register<AiSidePanel, ICommand?>(nameof(DiscardCommand));

    /// <summary>Every word the block shows or announces.</summary>
    public static readonly StyledProperty<AiSidePanelLabels> LabelsProperty =
        AvaloniaProperty.Register<AiSidePanel, AiSidePanelLabels>(nameof(Labels), new AiSidePanelLabels());

    #endregion

    #region Events

    /// <summary>Raised after a change is applied and the page data is updated; save the page here.</summary>
    public static readonly RoutedEvent<AiPanelChangeEventArgs> ChangeAppliedEvent =
        RoutedEvent.Register<AiSidePanel, AiPanelChangeEventArgs>(nameof(ChangeApplied), RoutingStrategies.Bubble);

    /// <summary>Raised after a change is undone.</summary>
    public static readonly RoutedEvent<AiPanelChangeEventArgs> ChangeUndoneEvent =
        RoutedEvent.Register<AiSidePanel, AiPanelChangeEventArgs>(nameof(ChangeUndone), RoutingStrategies.Bubble);

    /// <summary>Raised after a change is discarded.</summary>
    public static readonly RoutedEvent<AiPanelChangeEventArgs> ChangeDiscardedEvent =
        RoutedEvent.Register<AiSidePanel, AiPanelChangeEventArgs>(nameof(ChangeDiscarded), RoutingStrategies.Bubble);

    /// <summary>Raised when a selection is added as the context of the next question.</summary>
    public static readonly RoutedEvent<AiPanelSelectionEventArgs> SelectionAskedEvent =
        RoutedEvent.Register<AiSidePanel, AiPanelSelectionEventArgs>(nameof(SelectionAsked), RoutingStrategies.Bubble);

    /// <summary>Raised after a change is applied and the page data is updated.</summary>
    public event EventHandler<AiPanelChangeEventArgs>? ChangeApplied { add => AddHandler(ChangeAppliedEvent, value); remove => RemoveHandler(ChangeAppliedEvent, value); }

    /// <summary>Raised after a change is undone.</summary>
    public event EventHandler<AiPanelChangeEventArgs>? ChangeUndone { add => AddHandler(ChangeUndoneEvent, value); remove => RemoveHandler(ChangeUndoneEvent, value); }

    /// <summary>Raised after a change is discarded.</summary>
    public event EventHandler<AiPanelChangeEventArgs>? ChangeDiscarded { add => AddHandler(ChangeDiscardedEvent, value); remove => RemoveHandler(ChangeDiscardedEvent, value); }

    /// <summary>Raised when a selection is added as context.</summary>
    public event EventHandler<AiPanelSelectionEventArgs>? SelectionAsked { add => AddHandler(SelectionAskedEvent, value); remove => RemoveHandler(SelectionAskedEvent, value); }

    #endregion

    private bool _isSheetMode;
    private bool _isBusy;
    private AiPanelSelection? _selection;
    private AiPanelChange? _lastApplied;

    /// <summary>The prompts of the empty thread; the ones not used yet follow each answer.</summary>
    public AvaloniaList<string> Suggestions { get; } = new();

    /// <summary>The exchanges of the thread, oldest first (the panel adds to it); save and restore it as you like.</summary>
    public AvaloniaList<AiPanelTurn> Turns { get; } = new();

    /// <inheritdoc cref="PageProperty"/>
    public AiPanelPage? Page { get => GetValue(PageProperty); set => SetValue(PageProperty, value); }

    /// <inheritdoc cref="ResponderProperty"/>
    public IAiPanelResponder? Responder { get => GetValue(ResponderProperty); set => SetValue(ResponderProperty, value); }

    /// <inheritdoc cref="IsOpenProperty"/>
    public bool IsOpen { get => GetValue(IsOpenProperty); set => SetValue(IsOpenProperty, value); }

    /// <inheritdoc cref="AssistantNameProperty"/>
    public string AssistantName { get => GetValue(AssistantNameProperty); set => SetValue(AssistantNameProperty, value); }

    /// <inheritdoc cref="ModelNameProperty"/>
    public string? ModelName { get => GetValue(ModelNameProperty); set => SetValue(ModelNameProperty, value); }

    /// <inheritdoc cref="AssistantMarkProperty"/>
    public object? AssistantMark { get => GetValue(AssistantMarkProperty); set => SetValue(AssistantMarkProperty, value); }

    /// <inheritdoc cref="PanelWidthProperty"/>
    public double PanelWidth { get => GetValue(PanelWidthProperty); set => SetValue(PanelWidthProperty, value); }

    /// <inheritdoc cref="PageMaxWidthProperty"/>
    public double PageMaxWidth { get => GetValue(PageMaxWidthProperty); set => SetValue(PageMaxWidthProperty, value); }

    /// <inheritdoc cref="SheetBreakpointProperty"/>
    public double SheetBreakpoint { get => GetValue(SheetBreakpointProperty); set => SetValue(SheetBreakpointProperty, value); }

    /// <inheritdoc cref="SheetHeightRatioProperty"/>
    public double SheetHeightRatio { get => GetValue(SheetHeightRatioProperty); set => SetValue(SheetHeightRatioProperty, value); }

    /// <inheritdoc cref="StreamIntervalProperty"/>
    public TimeSpan StreamInterval { get => GetValue(StreamIntervalProperty); set => SetValue(StreamIntervalProperty, value); }

    /// <inheritdoc cref="MinWordsPerStepProperty"/>
    public int MinWordsPerStep { get => GetValue(MinWordsPerStepProperty); set => SetValue(MinWordsPerStepProperty, value); }

    /// <inheritdoc cref="MaxWordsPerStepProperty"/>
    public int MaxWordsPerStep { get => GetValue(MaxWordsPerStepProperty); set => SetValue(MaxWordsPerStepProperty, value); }

    /// <inheritdoc cref="IsSheetModeProperty"/>
    public bool IsSheetMode { get => _isSheetMode; private set => SetAndRaise(IsSheetModeProperty, ref _isSheetMode, value); }

    /// <inheritdoc cref="IsBusyProperty"/>
    public bool IsBusy { get => _isBusy; private set => SetAndRaise(IsBusyProperty, ref _isBusy, value); }

    /// <inheritdoc cref="SelectionProperty"/>
    public AiPanelSelection? Selection { get => _selection; private set => SetAndRaise(SelectionProperty, ref _selection, value); }

    /// <inheritdoc cref="LastAppliedProperty"/>
    public AiPanelChange? LastApplied { get => _lastApplied; private set => SetAndRaise(LastAppliedProperty, ref _lastApplied, value); }

    /// <inheritdoc cref="ApplyCommandProperty"/>
    public ICommand? ApplyCommand { get => GetValue(ApplyCommandProperty); set => SetValue(ApplyCommandProperty, value); }

    /// <inheritdoc cref="UndoCommandProperty"/>
    public ICommand? UndoCommand { get => GetValue(UndoCommandProperty); set => SetValue(UndoCommandProperty, value); }

    /// <inheritdoc cref="DiscardCommandProperty"/>
    public ICommand? DiscardCommand { get => GetValue(DiscardCommandProperty); set => SetValue(DiscardCommandProperty, value); }

    /// <inheritdoc cref="LabelsProperty"/>
    public AiSidePanelLabels Labels { get => GetValue(LabelsProperty); set => SetValue(LabelsProperty, value); }
}
