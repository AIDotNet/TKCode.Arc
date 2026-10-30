using System.Collections.Specialized;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Metadata;
using Avalonia.VisualTree;
using TKCode.Arc.Primitives;

namespace TKCode.Arc;

/// <summary>
/// An AI code edit suggestion in a small editor panel, like an inline edit in an AI code editor. The header shows the
/// model's mark, the instruction and the model; the panel shows a file tab with its language mark, a running +/− count,
/// and the code with each proposed change as an inline diff hunk: removed lines tinted red with a minus, new lines tinted
/// green with a plus. New lines stream in, then every hunk gets Reject and Accept under it, and the footer offers Reject
/// all and Accept all (Cmd or Ctrl + Enter). Accepting a hunk folds its removed lines away and settles the new lines into
/// the file; rejecting folds the new lines away and restores the original.
/// <para>
/// Give it the file in <see cref="Code"/>, the changes in <see cref="Hunks"/> and set <see cref="Status"/>; write the file in
/// <see cref="Resolved"/> (or progressively in <see cref="DecisionMade"/>): the control never writes anything itself. With
/// no code and no hunks it shows an empty file, or plays a <see cref="Simulation"/> you pass in, which reviews itself and
/// replays until someone touches it.
/// </para>
/// </summary>
[TemplatePart(MarkPart, typeof(ArcGlyph))]
[TemplatePart(TitlePart, typeof(TextBlock))]
[TemplatePart(ModelPart, typeof(TextBlock))]
[TemplatePart(FileMarkPart, typeof(ContentPresenter))]
[TemplatePart(MonogramPart, typeof(CodeSuggestionFileMark))]
[TemplatePart(FileNamePart, typeof(TextBlock))]
[TemplatePart(FolderPart, typeof(TextBlock))]
[TemplatePart(AddedCountPart, typeof(TextBlock))]
[TemplatePart(RemovedCountPart, typeof(TextBlock))]
[TemplatePart(CountsPart, typeof(Control))]
[TemplatePart(CodeAreaPart, typeof(CodeSuggestionCodeArea))]
[TemplatePart(ScrollPart, typeof(ScrollViewer))]
[TemplatePart(ViewPart, typeof(CodeSuggestionView))]
[TemplatePart(StatusIconPart, typeof(ArcContentSwap))]
[TemplatePart(StatusLabelPart, typeof(ArcContentSwap))]
[TemplatePart(ActionsPart, typeof(ArcContentSwap))]
[TemplatePart(AnnouncerPart, typeof(ExplainerAnnouncer))]
[PseudoClasses(CompactPseudoClass, NarrowPseudoClass, ErrorPseudoClass)]
public partial class CodeSuggestion : TemplatedControl
{
    internal const string MarkPart = "PART_Mark";
    internal const string TitlePart = "PART_Title";
    internal const string ModelPart = "PART_Model";
    internal const string FileMarkPart = "PART_FileMark";
    internal const string MonogramPart = "PART_Monogram";
    internal const string FileNamePart = "PART_FileName";
    internal const string FolderPart = "PART_Folder";
    internal const string AddedCountPart = "PART_AddedCount";
    internal const string RemovedCountPart = "PART_RemovedCount";
    internal const string CountsPart = "PART_Counts";
    internal const string CodeAreaPart = "PART_CodeArea";
    internal const string ScrollPart = "PART_Scroll";
    internal const string ViewPart = "PART_View";
    internal const string StatusIconPart = "PART_StatusIcon";
    internal const string StatusLabelPart = "PART_StatusLabel";
    internal const string ActionsPart = "PART_Actions";
    internal const string AnnouncerPart = "PART_Announcer";

    /// <summary>Below this card width the folder after the file name hides.</summary>
    internal const double CompactWidth = 520;

    /// <summary>Below this card width the gutter narrows, code drops to 12px, summaries hide and the footer stacks.</summary>
    internal const double NarrowWidth = 440;

    internal const string CompactPseudoClass = ":compact";
    internal const string NarrowPseudoClass = ":narrow";
    internal const string ErrorPseudoClass = ":error";

    /// <summary>The original file contents, lines split on <c>\n</c>. Leave it and <see cref="Hunks"/> unset to play the <see cref="Simulation"/>.</summary>
    public static readonly StyledProperty<string?> CodeProperty =
        AvaloniaProperty.Register<CodeSuggestion, string?>(nameof(Code));

    /// <summary>
    /// Where the suggestion is: thinking shows a shimmering label over the plain file, streaming shows hunks as they grow,
    /// ready shows Accept and Reject, error shows the message and Retry. Going back to thinking clears earlier decisions.
    /// </summary>
    public static readonly StyledProperty<CodeSuggestionStatus> StatusProperty =
        AvaloniaProperty.Register<CodeSuggestion, CodeSuggestionStatus>(nameof(Status), CodeSuggestionStatus.Ready);

    /// <summary>What the person asked for, shown as the header title (empty by default).</summary>
    public static readonly StyledProperty<string> InstructionProperty =
        AvaloniaProperty.Register<CodeSuggestion, string>(nameof(Instruction), string.Empty);

    /// <summary>
    /// The model that wrote the suggestion: its name under the title and its mark (a 24 × 24 stroked glyph, in
    /// <see cref="AgentModel.MarkBrush"/> or the text color) in the header. Null (the default) hides the model line.
    /// </summary>
    public static readonly StyledProperty<AgentModel?> ModelProperty =
        AvaloniaProperty.Register<CodeSuggestion, AgentModel?>(nameof(Model));

    /// <summary>The file name in the tab (empty by default); its extension picks the highlighting language and the language mark.</summary>
    public static readonly StyledProperty<string> FileNameProperty =
        AvaloniaProperty.Register<CodeSuggestion, string>(nameof(FileName), string.Empty);

    /// <summary>The folder shown after the file name from a 520px card, such as "src/billing".</summary>
    public static readonly StyledProperty<string?> FilePathProperty =
        AvaloniaProperty.Register<CodeSuggestion, string?>(nameof(FilePath));

    /// <summary>Your own language mark for the tab (any content); null shows two letters from the file's extension.</summary>
    public static readonly StyledProperty<object?> FileMarkProperty =
        AvaloniaProperty.Register<CodeSuggestion, object?>(nameof(FileMark));

    /// <summary>Whether the tab shows a language mark at all.</summary>
    public static readonly StyledProperty<bool> ShowsFileMarkProperty =
        AvaloniaProperty.Register<CodeSuggestion, bool>(nameof(ShowsFileMark), true);

    /// <summary>The message of the error status.</summary>
    public static readonly StyledProperty<string> ErrorTextProperty =
        AvaloniaProperty.Register<CodeSuggestion, string>(nameof(ErrorText), "The model stopped responding. Nothing was changed.");

    /// <summary>Shows Stop while thinking or streaming; it raises <see cref="StopRequested"/>. A simulation always has it.</summary>
    public static readonly StyledProperty<bool> AllowsStopProperty =
        AvaloniaProperty.Register<CodeSuggestion, bool>(nameof(AllowsStop));

    /// <summary>
    /// Shows Regenerate once the review is done (or there were no changes) and Retry on error; both raise
    /// <see cref="RegenerateRequested"/>. A simulation always has them and replays on its own.
    /// </summary>
    public static readonly StyledProperty<bool> AllowsRegenerateProperty =
        AvaloniaProperty.Register<CodeSuggestion, bool>(nameof(AllowsRegenerate));

    /// <summary>
    /// Simulation only: after streaming, it accepts and rejects hunks on its own (one by one and Accept all on alternate runs)
    /// and replays about 2.6s after it finishes. Any pointer, key or focus inside the card hands control to the person for
    /// that run. Off under reduced motion.
    /// </summary>
    public static readonly StyledProperty<bool> AutoPlayProperty =
        AvaloniaProperty.Register<CodeSuggestion, bool>(nameof(AutoPlay), true);

    /// <summary>
    /// A simulated suggestion to play while the card has no <see cref="Code"/> and no <see cref="Hunks"/> (demos and
    /// prototypes): it streams its hunks over its file and, with <see cref="AutoPlay"/>, reviews itself and replays. Null
    /// shows the card's own state.
    /// </summary>
    public static readonly StyledProperty<CodeSuggestionSimulation?> SimulationProperty =
        AvaloniaProperty.Register<CodeSuggestion, CodeSuggestionSimulation?>(nameof(Simulation));

    /// <summary>
    /// Cmd or Ctrl + Enter accepts all and Cmd or Ctrl + Backspace rejects all while focus is inside the card; Accept all
    /// shows the hint.
    /// </summary>
    public static readonly StyledProperty<bool> AreShortcutsEnabledProperty =
        AvaloniaProperty.Register<CodeSuggestion, bool>(nameof(AreShortcutsEnabled), true);

    /// <summary>Caps the code area's height; longer files scroll inside it. Infinity (the default) never scrolls.</summary>
    public static readonly StyledProperty<double> MaxCodeHeightProperty =
        AvaloniaProperty.Register<CodeSuggestion, double>(nameof(MaxCodeHeight), double.PositiveInfinity);

    /// <summary>Every word the card shows or says.</summary>
    public static readonly StyledProperty<CodeSuggestionLabels> LabelsProperty =
        AvaloniaProperty.Register<CodeSuggestion, CodeSuggestionLabels>(nameof(Labels), CodeSuggestionLabels.Default);

    /// <summary>How many hunks still wait for a decision.</summary>
    public static readonly DirectProperty<CodeSuggestion, int> PendingCountProperty =
        AvaloniaProperty.RegisterDirect<CodeSuggestion, int>(nameof(PendingCount), o => o.PendingCount);

    /// <summary>
    /// Raised for every hunk accepted or rejected (Accept all raises it once per hunk), with the file as it now stands.
    /// Bubbles.
    /// </summary>
    public static readonly RoutedEvent<CodeSuggestionDecisionEventArgs> DecisionMadeEvent =
        RoutedEvent.Register<CodeSuggestion, CodeSuggestionDecisionEventArgs>(nameof(DecisionMade), RoutingStrategies.Bubble);

    /// <summary>Raised once every hunk has a decision, with the final code and the hunk ids. Write the file here. Bubbles.</summary>
    public static readonly RoutedEvent<CodeSuggestionResolvedEventArgs> ResolvedEvent =
        RoutedEvent.Register<CodeSuggestion, CodeSuggestionResolvedEventArgs>(nameof(Resolved), RoutingStrategies.Bubble);

    /// <summary>Raised when the person presses Stop. Abort your request, then set <see cref="Status"/>. Bubbles.</summary>
    public static readonly RoutedEvent<RoutedEventArgs> StopRequestedEvent =
        RoutedEvent.Register<CodeSuggestion, RoutedEventArgs>(nameof(StopRequested), RoutingStrategies.Bubble);

    /// <summary>Raised when the person presses Regenerate or Retry. Bubbles.</summary>
    public static readonly RoutedEvent<RoutedEventArgs> RegenerateRequestedEvent =
        RoutedEvent.Register<CodeSuggestion, RoutedEventArgs>(nameof(RegenerateRequested), RoutingStrategies.Bubble);

    private readonly AvaloniaList<CodeSuggestionHunk> _hunks = [];
    private int _pendingCount;

    /// <summary>Creates the card.</summary>
    public CodeSuggestion()
    {
        _hunks.CollectionChanged += OnHunksChanged;
        AddHandler(KeyDownEvent, OnKeyDownTunnel, RoutingStrategies.Tunnel, handledEventsToo: true);
        AddHandler(PointerPressedEvent, OnPointerTunnel, RoutingStrategies.Tunnel, handledEventsToo: true);
        AddHandler(PointerWheelChangedEvent, OnPointerTunnel, RoutingStrategies.Tunnel, handledEventsToo: true);
        AddHandler(GotFocusEvent, OnGotFocus, RoutingStrategies.Bubble, handledEventsToo: true);
        InitSample();
    }

    /// <inheritdoc cref="CodeProperty"/>
    public string? Code { get => GetValue(CodeProperty); set => SetValue(CodeProperty, value); }

    /// <summary>
    /// The proposed changes. Each replaces <see cref="CodeSuggestionHunk.Remove"/> lines from the zero based line
    /// <see cref="CodeSuggestionHunk.Start"/> with its <see cref="CodeSuggestionHunk.Insert"/> lines. Overlapping hunks after
    /// the first are ignored. Leave it empty (and <see cref="Code"/> unset) to play the built-in sample.
    /// </summary>
    [Content]
    public AvaloniaList<CodeSuggestionHunk> Hunks => _hunks;

    /// <inheritdoc cref="StatusProperty"/>
    public CodeSuggestionStatus Status { get => GetValue(StatusProperty); set => SetValue(StatusProperty, value); }

    /// <inheritdoc cref="InstructionProperty"/>
    public string Instruction { get => GetValue(InstructionProperty); set => SetValue(InstructionProperty, value); }

    /// <inheritdoc cref="ModelProperty"/>
    public AgentModel? Model { get => GetValue(ModelProperty); set => SetValue(ModelProperty, value); }

    /// <inheritdoc cref="FileNameProperty"/>
    public string FileName { get => GetValue(FileNameProperty); set => SetValue(FileNameProperty, value); }

    /// <inheritdoc cref="FilePathProperty"/>
    public string? FilePath { get => GetValue(FilePathProperty); set => SetValue(FilePathProperty, value); }

    /// <inheritdoc cref="FileMarkProperty"/>
    public object? FileMark { get => GetValue(FileMarkProperty); set => SetValue(FileMarkProperty, value); }

    /// <inheritdoc cref="ShowsFileMarkProperty"/>
    public bool ShowsFileMark { get => GetValue(ShowsFileMarkProperty); set => SetValue(ShowsFileMarkProperty, value); }

    /// <inheritdoc cref="ErrorTextProperty"/>
    public string ErrorText { get => GetValue(ErrorTextProperty); set => SetValue(ErrorTextProperty, value); }

    /// <inheritdoc cref="AllowsStopProperty"/>
    public bool AllowsStop { get => GetValue(AllowsStopProperty); set => SetValue(AllowsStopProperty, value); }

    /// <inheritdoc cref="AllowsRegenerateProperty"/>
    public bool AllowsRegenerate { get => GetValue(AllowsRegenerateProperty); set => SetValue(AllowsRegenerateProperty, value); }

    /// <inheritdoc cref="AutoPlayProperty"/>
    public bool AutoPlay { get => GetValue(AutoPlayProperty); set => SetValue(AutoPlayProperty, value); }

    /// <inheritdoc cref="SimulationProperty"/>
    public CodeSuggestionSimulation? Simulation { get => GetValue(SimulationProperty); set => SetValue(SimulationProperty, value); }

    /// <inheritdoc cref="AreShortcutsEnabledProperty"/>
    public bool AreShortcutsEnabled { get => GetValue(AreShortcutsEnabledProperty); set => SetValue(AreShortcutsEnabledProperty, value); }

    /// <inheritdoc cref="MaxCodeHeightProperty"/>
    public double MaxCodeHeight { get => GetValue(MaxCodeHeightProperty); set => SetValue(MaxCodeHeightProperty, value); }

    /// <inheritdoc cref="LabelsProperty"/>
    public CodeSuggestionLabels Labels { get => GetValue(LabelsProperty); set => SetValue(LabelsProperty, value); }

    /// <inheritdoc cref="PendingCountProperty"/>
    public int PendingCount { get => _pendingCount; private set => SetAndRaise(PendingCountProperty, ref _pendingCount, value); }

    /// <inheritdoc cref="DecisionMadeEvent"/>
    public event EventHandler<CodeSuggestionDecisionEventArgs>? DecisionMade
    {
        add => AddHandler(DecisionMadeEvent, value);
        remove => RemoveHandler(DecisionMadeEvent, value);
    }

    /// <inheritdoc cref="ResolvedEvent"/>
    public event EventHandler<CodeSuggestionResolvedEventArgs>? Resolved
    {
        add => AddHandler(ResolvedEvent, value);
        remove => RemoveHandler(ResolvedEvent, value);
    }

    /// <inheritdoc cref="StopRequestedEvent"/>
    public event EventHandler<RoutedEventArgs>? StopRequested
    {
        add => AddHandler(StopRequestedEvent, value);
        remove => RemoveHandler(StopRequestedEvent, value);
    }

    /// <inheritdoc cref="RegenerateRequestedEvent"/>
    public event EventHandler<RoutedEventArgs>? RegenerateRequested
    {
        add => AddHandler(RegenerateRequestedEvent, value);
        remove => RemoveHandler(RegenerateRequestedEvent, value);
    }

    /// <summary>Whether the <see cref="Simulation"/> is on screen: one is set and no code and no hunks were given.</summary>
    internal bool UsesSample => Code is null && _hunks.Count == 0 && Simulation is not null;

    /// <summary>The file shown: the given code, or the simulation's.</summary>
    internal string EffectiveCode => (UsesSample ? Simulation!.Code : Code) ?? string.Empty;

    /// <summary>The status shown: the given one, or the simulation's.</summary>
    internal CodeSuggestionStatus EffectiveStatus => UsesSample ? _sampleStatus : Status;

    /// <summary>The folder shown after the file name.</summary>
    internal string? EffectiveFilePath => FilePath;

    private IEnumerable<CodeSuggestionHunk> EffectiveHunks => UsesSample ? _sampleHunks : _hunks;

    private void OnHunksChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.OldItems is not null)
        {
            foreach (CodeSuggestionHunk hunk in e.OldItems)
            {
                hunk.PropertyChanged -= OnHunkChanged;
            }
        }
        if (e.NewItems is not null)
        {
            foreach (CodeSuggestionHunk hunk in e.NewItems)
            {
                hunk.PropertyChanged += OnHunkChanged;
            }
        }
        if (e.Action == NotifyCollectionChangedAction.Reset)
        {
            // Reset gives no old items: resubscribe the hunks that are left.
            foreach (var hunk in _hunks)
            {
                hunk.PropertyChanged -= OnHunkChanged;
                hunk.PropertyChanged += OnHunkChanged;
            }
        }
        OnSourceChanged();
    }

    private void OnHunkChanged(object? sender, AvaloniaPropertyChangedEventArgs e) => Sync(animate: true);

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        var property = change.Property;
        if (property == CodeProperty)
        {
            OnSourceChanged();
        }
        else if (property == SimulationProperty)
        {
            if (change.OldValue is CodeSuggestionSimulation old)
            {
                old.RestartRequested -= OnSimulationRestart;
            }
            if (change.NewValue is CodeSuggestionSimulation added)
            {
                added.RestartRequested += OnSimulationRestart;
            }
            // Another simulation starts a run of its own; none stops the one playing.
            if (_wasSample)
            {
                StopSimulation();
                _wasSample = false;
                ClearDecisions();
            }
            OnSourceChanged();
            UpdateTexts();
        }
        else if (property == StatusProperty)
        {
            if (Status == CodeSuggestionStatus.Thinking)
            {
                ClearDecisions();
            }
            Sync(animate: true);
            AnnounceStatus();
        }
        else if (property == FileNameProperty)
        {
            Sync(animate: false);
            UpdateTexts();
        }
        else if (property == InstructionProperty || property == ModelProperty || property == FilePathProperty
                 || property == FileMarkProperty || property == ShowsFileMarkProperty || property == LabelsProperty
                 || property == ErrorTextProperty)
        {
            UpdateTexts();
            if (property == LabelsProperty)
            {
                UpdateBars();
            }
            UpdateFooter();
        }
        else if (property == AllowsStopProperty || property == AllowsRegenerateProperty || property == AreShortcutsEnabledProperty)
        {
            UpdateFooter();
        }
        else if (property == BoundsProperty)
        {
            UpdateResponsive();
        }
    }

    /// <summary>The code or the hunk list changed: the sample may have started or stopped.</summary>
    private void OnSourceChanged()
    {
        var sample = UsesSample;
        if (sample != _wasSample)
        {
            _wasSample = sample;
            ClearDecisions();
            if (sample)
            {
                InitSample();
                if (this.IsAttachedToVisualTree())
                {
                    PrepareSample();
                    _simStarted = false;
                    if (_inView)
                    {
                        StartRun();
                    }
                }
            }
            else
            {
                StopSimulation();
            }
            UpdateTexts();
        }
        Sync(animate: true);
    }

    private void UpdateResponsive()
    {
        var width = Bounds.Width;
        var compact = width > 0 && width < CompactWidth;
        var narrow = width > 0 && width < NarrowWidth;
        if (PseudoClasses.Contains(CompactPseudoClass) == compact && PseudoClasses.Contains(NarrowPseudoClass) == narrow)
        {
            return;
        }
        PseudoClasses.Set(CompactPseudoClass, compact);
        PseudoClasses.Set(NarrowPseudoClass, narrow);
        UpdateTexts();
        UpdateBars();
        if (_actions?.Content is CodeSuggestionActions actions)
        {
            actions.IsStacked = narrow;
        }
    }

    internal bool IsNarrow => PseudoClasses.Contains(NarrowPseudoClass);

    internal bool IsCompact => PseudoClasses.Contains(CompactPseudoClass);

    // Input ---------------------------------------------------------------------------------------------------------------

    /// <summary>The last press came from the keyboard: focus that moves afterwards shows the ring.</summary>
    private bool _keyboardInput;

    private void OnKeyDownTunnel(object? sender, KeyEventArgs e)
    {
        _keyboardInput = true;
        HandOver();
        if (e.Handled || !AreShortcutsEnabled)
        {
            return;
        }
        var command = (e.KeyModifiers & (KeyModifiers.Control | KeyModifiers.Meta)) != 0;
        if (!command || (e.KeyModifiers & (KeyModifiers.Alt | KeyModifiers.Shift)) != 0)
        {
            return;
        }
        if (e.Key == Key.Enter)
        {
            e.Handled = AcceptAll() > 0 || EffectiveStatus == CodeSuggestionStatus.Ready;
        }
        else if (e.Key == Key.Back)
        {
            e.Handled = RejectAll() > 0 || EffectiveStatus == CodeSuggestionStatus.Ready;
        }
    }

    private void OnPointerTunnel(object? sender, RoutedEventArgs e)
    {
        _keyboardInput = false;
        HandOver();
    }

    /// <inheritdoc />
    protected override void OnPointerEntered(PointerEventArgs e)
    {
        base.OnPointerEntered(e);
        HandOver();
    }

    private void OnGotFocus(object? sender, RoutedEventArgs e) => HandOver();

    private void OnCodeAreaKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Handled || _scroll is null || !ReferenceEquals(e.Source, _codeArea) || e.KeyModifiers != KeyModifiers.None)
        {
            return;
        }
        var offset = _scroll.Offset;
        var step = 40.0;
        switch (e.Key)
        {
            case Key.Left:
                _scroll.Offset = offset.WithX(Math.Max(0, offset.X - step));
                e.Handled = true;
                break;
            case Key.Right:
                _scroll.Offset = offset.WithX(offset.X + step);
                e.Handled = true;
                break;
            case Key.Up:
                _scroll.Offset = offset.WithY(Math.Max(0, offset.Y - step / 2));
                e.Handled = _scroll.Extent.Height > _scroll.Viewport.Height;
                break;
            case Key.Down:
                _scroll.Offset = offset.WithY(offset.Y + step / 2);
                e.Handled = _scroll.Extent.Height > _scroll.Viewport.Height;
                break;
        }
    }

    /// <summary>Focuses <paramref name="target"/> the way the last press came in (a keyboard press shows the ring).</summary>
    private void MoveFocus(IInputElement? target)
    {
        target?.Focus(_keyboardInput ? NavigationMethod.Tab : NavigationMethod.Pointer);
    }
}
