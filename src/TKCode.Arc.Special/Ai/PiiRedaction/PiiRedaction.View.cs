using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;
using AvaloniaButton = Avalonia.Controls.Button;

namespace TKCode.Arc;

/// <summary>The template parts of <see cref="PiiRedaction"/> and how each one follows the state.</summary>
public partial class PiiRedaction
{
    /// <summary>A layer fading out takes 100ms; a layer fading in takes 160ms (the swap crossfade).</summary>
    internal static readonly TimeSpan LayerOut = TimeSpan.FromMilliseconds(100);

    internal static readonly TimeSpan LayerIn = ArcMotionPresets.Fast;

    private PiiRedactionEditor? _editor;
    private DecoratedTextBlock? _original;
    private DecoratedTextBlock? _redacted;
    private ToolCallText? _messageLabel;
    private Switch? _showOriginalSwitch;
    private PiiRedactionCellsPanel? _cellsPanel;
    private readonly PiiRedactionTypeCell?[] _cells = new PiiRedactionTypeCell?[4];
    private PiiRedactionFlowNode? _flowYou;
    private PiiRedactionFlowNode? _flowGuard;
    private PiiRedactionFlowNode? _flowModel;
    private PiiRedactionConnector? _flowOut;
    private PiiRedactionConnector? _flowIn;
    private TextBlock? _replyPlaceholder;
    private TextBlock? _replyReserve;
    private ArcGlyph? _replyMark;
    private PiiRedactionStreamText? _stream;
    private DecoratedTextBlock? _restored;
    private ArcGlyph? _statusGlyph;
    private ToolCallText? _status;
    private AvaloniaButton? _reset;
    private AvaloniaButton? _primary;
    private ExplainerAnnouncer? _announcer;

    private PiiRedactionFader? _editorFade;
    private PiiRedactionFader? _originalFade;
    private PiiRedactionFader? _redactedFade;
    private PiiRedactionFader? _placeholderFade;
    private PiiRedactionFader? _streamFade;
    private PiiRedactionFader? _restoredFade;

    private readonly List<TextRangeDecoration> _originalMarks = [];
    private readonly List<TextRangeDecoration> _redactedMarks = [];
    private readonly List<TextRangeDecoration> _restoredMarks = [];

    /// <summary>The per-type state of the cells: what each shows, whether it shimmers and whether it found something.</summary>
    private readonly PiiRedactionCellState[] _cellStates = new PiiRedactionCellState[4];

    private LiveLoop? _tintLoop;
    private double _tintClock;
    private readonly List<(TextRangeDecoration Mark, double At)> _tinting = [];
    private bool _syncingSwitch;

    internal PiiRedactionEditor? EditorControl => _editor;

    internal DecoratedTextBlock? OriginalControl => _original;

    internal DecoratedTextBlock? RedactedControl => _redacted;

    internal Switch? ShowOriginalSwitch => _showOriginalSwitch;

    internal PiiRedactionTypeCell? Cell(PiiType type) => _cells[(int)type];

    internal PiiRedactionCellsPanel? CellsPanel => _cellsPanel;

    internal PiiRedactionFlowNode? FlowModel => _flowModel;

    internal PiiRedactionFlowNode? FlowGuard => _flowGuard;

    internal PiiRedactionConnector? FlowOut => _flowOut;

    internal PiiRedactionStreamText? StreamControl => _stream;

    internal DecoratedTextBlock? RestoredControl => _restored;

    internal AvaloniaButton? PrimaryButton => _primary;

    internal AvaloniaButton? ResetButton => _reset;

    internal ExplainerAnnouncer? Announcer => _announcer;

    internal IReadOnlyList<TextRangeDecoration> OriginalMarks => _originalMarks;

    internal IReadOnlyList<TextRangeDecoration> RestoredMarks => _restoredMarks;

    /// <summary>The status line's words.</summary>
    internal string StatusText => _status?.Text ?? string.Empty;

    /// <summary>The primary action's label.</summary>
    internal string PrimaryText => _primary?.Content as string ?? string.Empty;

    /// <inheritdoc />
    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        Unhook();
        var scope = e.NameScope;
        _editor = scope.Find<PiiRedactionEditor>(EditorPart);
        _original = scope.Find<DecoratedTextBlock>(OriginalPart);
        _redacted = scope.Find<DecoratedTextBlock>(RedactedPart);
        _messageLabel = scope.Find<ToolCallText>(MessageLabelPart);
        _showOriginalSwitch = scope.Find<Switch>(ShowOriginalPart);
        _cellsPanel = scope.Find<PiiRedactionCellsPanel>(CellsPart);
        _cells[(int)PiiType.Name] = scope.Find<PiiRedactionTypeCell>("PART_NameCell");
        _cells[(int)PiiType.Email] = scope.Find<PiiRedactionTypeCell>("PART_EmailCell");
        _cells[(int)PiiType.Phone] = scope.Find<PiiRedactionTypeCell>("PART_PhoneCell");
        _cells[(int)PiiType.Card] = scope.Find<PiiRedactionTypeCell>("PART_CardCell");
        _flowYou = scope.Find<PiiRedactionFlowNode>(FlowYouPart);
        _flowGuard = scope.Find<PiiRedactionFlowNode>(FlowGuardPart);
        _flowModel = scope.Find<PiiRedactionFlowNode>(FlowModelPart);
        _flowOut = scope.Find<PiiRedactionConnector>(FlowOutPart);
        _flowIn = scope.Find<PiiRedactionConnector>(FlowInPart);
        _replyPlaceholder = scope.Find<TextBlock>(ReplyPlaceholderPart);
        _replyReserve = scope.Find<TextBlock>("PART_ReplyReserve");
        _replyMark = scope.Find<ArcGlyph>("PART_ReplyMark");
        UpdateReplyReserve();
        _stream = scope.Find<PiiRedactionStreamText>(StreamPart);
        _restored = scope.Find<DecoratedTextBlock>(RestoredPart);
        _statusGlyph = scope.Find<ArcGlyph>(StatusGlyphPart);
        _status = scope.Find<ToolCallText>(StatusPart);
        _reset = scope.Find<AvaloniaButton>(ResetPart);
        _primary = scope.Find<AvaloniaButton>(PrimaryPart);
        _announcer = scope.Find<ExplainerAnnouncer>(AnnouncerPart);

        if (_editor is not null)
        {
            _editor.ScanRequested += OnEditorScanRequested;
            _editor.Edited += OnEditorEdited;
            _editorFade = new PiiRedactionFader(_editor, 1);
        }
        if (_showOriginalSwitch is not null)
        {
            _showOriginalSwitch.Click += OnShowOriginalClick;
        }
        if (_reset is not null)
        {
            _reset.Click += OnResetClick;
        }
        if (_primary is not null)
        {
            _primary.Click += OnPrimaryClick;
        }
        _originalFade = _original is null ? null : new PiiRedactionFader(_original, 0);
        _redactedFade = _redacted is null ? null : new PiiRedactionFader(_redacted, 0);
        _placeholderFade = _replyPlaceholder is null ? null : new PiiRedactionFader(_replyPlaceholder, 1);
        _streamFade = _stream is null ? null : new PiiRedactionFader(_stream, 0);
        _restoredFade = _restored is null ? null : new PiiRedactionFader(_restored, 0);

        RebuildMessageLayers();
        RebuildReplyLayers();
        Refresh(instant: true);
    }

    private void Unhook()
    {
        if (_editor is not null)
        {
            _editor.ScanRequested -= OnEditorScanRequested;
            _editor.Edited -= OnEditorEdited;
        }
        if (_showOriginalSwitch is not null)
        {
            _showOriginalSwitch.Click -= OnShowOriginalClick;
        }
        if (_reset is not null)
        {
            _reset.Click -= OnResetClick;
        }
        if (_primary is not null)
        {
            _primary.Click -= OnPrimaryClick;
        }
    }

    /// <inheritdoc />
    protected override void OnSizeChanged(SizeChangedEventArgs e)
    {
        base.OnSizeChanged(e);
        var width = e.NewSize.Width;
        if (width > 0)
        {
            // Breakpoints follow the card's own width, never the window's.
            PseudoClasses.Set(NarrowPseudoClass, width < NarrowWidth);
            PseudoClasses.Set(CompactPseudoClass, width < CompactWidth);
        }
    }

    /// <summary>The type's color from the theme (or the app).</summary>
    internal IBrush? TypeBrush(PiiType type) => type switch
    {
        PiiType.Name => NameBrush,
        PiiType.Email => EmailBrush,
        PiiType.Phone => PhoneBrush,
        _ => CardBrush,
    };

    internal string TypeLabel(PiiType type) => type switch
    {
        PiiType.Name => Labels.Name,
        PiiType.Email => Labels.Email,
        PiiType.Phone => Labels.Phone,
        _ => Labels.Card,
    };

    private static IBrush? Tint(IBrush? brush, double alpha) =>
        brush is ISolidColorBrush solid ? new ImmutableSolidColorBrush(solid.Color, Math.Clamp(alpha * solid.Opacity, 0, 1)) : null;

    private TextRangeDecoration NewMark(PiiType type, int start, int length, double opacity) => new(start, length)
    {
        Background = Tint(TypeBrush(type), MarkTintOpacity),
        CornerRadius = MarkCornerRadius.TopLeft,
        Outset = MarkOutset,
        Opacity = opacity,
        Tag = type,
    };

    /// <summary>
    /// Fills the original and redacted layers from the last redaction: the original text with a mark under each value
    /// (hidden until the scan reaches it), and the safe prompt with each placeholder in its type's color over its mark.
    /// </summary>
    private void RebuildMessageLayers()
    {
        var message = EffectiveMessage;
        if (_editor is not null && _editor.Text != message)
        {
            _editor.IsSyncing = true;
            _editor.Text = message;
            _editor.IsSyncing = false;
        }
        _originalMarks.Clear();
        _redactedMarks.Clear();
        _tinting.Clear();
        if (_original is not null)
        {
            _original.Decorations.Clear();
            _original.Text = message;
        }
        if (_redacted is not null)
        {
            _redacted.Decorations.Clear();
            _redacted.Inlines?.Clear();
            _redacted.Text = null;
        }
        if (_result is not { } result)
        {
            UpdateMessageNames();
            return;
        }
        var marksShown = _phase != PiiRedactionPhase.Scanning;
        foreach (var entity in result.Entities)
        {
            _originalMarks.Add(NewMark(entity.Type, entity.Start, entity.End - entity.Start, marksShown ? 1 : 0));
            _redactedMarks.Add(NewMark(entity.Type, entity.PromptStart, entity.PromptEnd - entity.PromptStart, 1));
        }
        _original?.Decorations.AddRange(_originalMarks);
        if (_redacted is not null)
        {
            var inlines = new InlineCollection();
            var copied = 0;
            var prompt = result.Prompt;
            foreach (var entity in result.Entities)
            {
                if (entity.PromptStart > copied)
                {
                    inlines.Add(new Run(prompt[copied..entity.PromptStart]));
                }
                inlines.Add(new Run(entity.Placeholder)
                {
                    Foreground = TypeBrush(entity.Type),
                    FontWeight = FontWeight.Medium,
                });
                copied = entity.PromptEnd;
            }
            if (copied < prompt.Length)
            {
                inlines.Add(new Run(prompt[copied..]));
            }
            _redacted.Inlines = inlines;
            _redacted.Decorations.AddRange(_redactedMarks);
        }
        UpdateMessageNames();
    }

    /// <summary>The accessible names of the text layers: the text, and for the original every mark ("Email, redacted as EMAIL_1").</summary>
    private void UpdateMessageNames()
    {
        if (_original is not null)
        {
            AutomationProperties.SetName(_original, EffectiveMessage);
            var marks = _result?.Entities.Select(e => e.Value + ": " + Labels.MarkName(TypeLabel(e.Type), e.Token));
            AutomationProperties.SetHelpText(_original, marks is null ? null : string.Join("; ", marks));
        }
        if (_redacted is not null)
        {
            AutomationProperties.SetName(_redacted, _result?.Prompt);
        }
    }

    /// <summary>Fills the reply layers: the raw reply for streaming and the restored one with a mark under each value put back.</summary>
    private void RebuildReplyLayers()
    {
        _restoredMarks.Clear();
        if (_restored is not null)
        {
            _restored.Decorations.Clear();
            _restored.Text = _restoredText;
            foreach (var range in _restoredRanges)
            {
                _restoredMarks.Add(NewMark(range.Type, range.Start, range.Length, 1));
            }
            _restored.Decorations.AddRange(_restoredMarks);
            AutomationProperties.SetName(_restored, _restoredText);
        }
        if (_stream is not null && _stream.Text != (_raw ?? string.Empty))
        {
            _stream.Reserve(_raw);
        }
    }

    /// <summary>Repaints the marks after a type color or the tint changed (theme switch).</summary>
    private void RefreshMarkBrushes()
    {
        foreach (var list in new[] { _originalMarks, _redactedMarks, _restoredMarks })
        {
            foreach (var mark in list)
            {
                if (mark.Tag is PiiType type)
                {
                    mark.Background = Tint(TypeBrush(type), MarkTintOpacity);
                    mark.CornerRadius = MarkCornerRadius.TopLeft;
                    mark.Outset = MarkOutset;
                }
            }
        }
        if (_result is not null && _redacted?.Inlines is { } inlines)
        {
            var index = 0;
            foreach (var inline in inlines)
            {
                if (inline is Run run && run.FontWeight == FontWeight.Medium && index < _result.Entities.Count)
                {
                    run.Foreground = TypeBrush(_result.Entities[index++].Type);
                }
            }
        }
        _original?.InvalidateDecorations();
        _redacted?.InvalidateDecorations();
        _restored?.InvalidateDecorations();
        RefreshCells();
    }

    /// <summary>Starts a mark's 160ms tint now (the scan reached it). Reduced motion and Off: the mark shows at once.</summary>
    private void StartTint(TextRangeDecoration mark)
    {
        if (!ArcMotion.IsFull(this))
        {
            mark.Opacity = 1;
            _original?.InvalidateDecorations();
            return;
        }
        _tinting.Add((mark, _tintClock));
        _tintLoop ??= new LiveLoop(this, StepTints);
        _tintLoop.Start();
    }

    private bool StepTints(double dt)
    {
        _tintClock += dt;
        var fade = LayerIn.TotalSeconds;
        for (var i = _tinting.Count - 1; i >= 0; i--)
        {
            var (mark, at) = _tinting[i];
            var t = Math.Clamp((_tintClock - at) / fade, 0, 1);
            mark.Opacity = t;
            if (t >= 1)
            {
                _tinting.RemoveAt(i);
            }
        }
        _original?.InvalidateDecorations();
        return _tinting.Count > 0;
    }

    /// <summary>Shows every mark that is still on its way (a stop, a reset or reduced motion).</summary>
    private void FinishTints()
    {
        foreach (var (mark, _) in _tinting)
        {
            mark.Opacity = 1;
        }
        _tinting.Clear();
        _tintLoop?.Stop();
        _original?.InvalidateDecorations();
    }

    private void RefreshCells()
    {
        for (var i = 0; i < 4; i++)
        {
            var cell = _cells[i];
            if (cell is null)
            {
                continue;
            }
            var type = (PiiType)i;
            var state = _cellStates[i];
            cell.TypeName = TypeLabel(type);
            cell.TypeBrush = TypeBrush(type);
            cell.TintBrush = Tint(TypeBrush(type), CellTintOpacity);
            cell.Value = state.Kind switch
            {
                PiiRedactionCellKind.Checking => Labels.Checking,
                PiiRedactionCellKind.Found => Labels.Tokens(state.Tokens),
                PiiRedactionCellKind.None => Labels.NoneFound,
                _ => Labels.NotChecked,
            };
            cell.IsChecking = state.Kind == PiiRedactionCellKind.Checking;
            cell.IsFound = state.Kind == PiiRedactionCellKind.Found;
        }
    }

    private bool HasSafePrompt =>
        _result is not null && _phase is not (PiiRedactionPhase.Idle or PiiRedactionPhase.Scanning)
                            && !(_phase == PiiRedactionPhase.Error && _failed == PiiRedactionStep.Scan);

    private bool ShowsEditor => _phase == PiiRedactionPhase.Idle || _phase == PiiRedactionPhase.Error && _failed == PiiRedactionStep.Scan;

    /// <summary>Brings every part in line with the state; a part that already shows the value is left alone.</summary>
    private void Refresh(bool instant)
    {
        ShownModelName = EffectiveModelName;
        if (_primary is null)
        {
            return;
        }
        var labels = Labels;
        var phase = _phase;
        var instantSwap = instant || ArcMotion.GetEffectiveLevel(this) == ArcMotionLevel.Off;

        // The message: the field before a scan, the marked original while scanning, then the safe prompt or the original.
        var editor = ShowsEditor;
        var showRedacted = HasSafePrompt && !ShowOriginal;
        var showOriginal = !editor && !showRedacted;
        _editorFade?.To(editor ? 1 : 0, editor ? LayerIn : LayerOut, instant: true);
        if (_editor is not null)
        {
            _editor.IsHitTestVisible = editor;
            _editor.IsTabStop = editor;
            _editor.IsReadOnly = !IsEditable;
            _editor.PlaceholderText = labels.Placeholder;
            AutomationProperties.SetAccessibilityView(_editor, editor ? AccessibilityView.Default : AccessibilityView.Raw);
        }
        // The field and the marked original hold the same text in the same place, so that change is instant; the swap to
        // and from the safe prompt is the crossfade.
        _originalFade?.To(showOriginal ? 1 : 0, showOriginal ? LayerIn : LayerOut, instant: instantSwap || editor || phase == PiiRedactionPhase.Scanning);
        _redactedFade?.To(showRedacted ? 1 : 0, showRedacted ? LayerIn : LayerOut, instant: instantSwap);
        SetLayerAccess(_original, showOriginal);
        SetLayerAccess(_redacted, showRedacted);
        _messageLabel?.SetCurrentValue(ToolCallText.TextProperty,
            !HasSafePrompt ? labels.Message : ShowOriginal ? labels.Original : labels.SafePrompt);

        if (_showOriginalSwitch is not null)
        {
            _showOriginalSwitch.IsEnabled = HasSafePrompt;
            _syncingSwitch = true;
            _showOriginalSwitch.IsChecked = ShowOriginal;
            _syncingSwitch = false;
            AutomationProperties.SetName(_showOriginalSwitch, labels.ShowOriginal);
        }

        RefreshCells();

        // The flow: the model's node carries its name and mark.
        if (_flowYou is not null)
        {
            _flowYou.Label = labels.You;
        }
        if (_flowGuard is not null)
        {
            _flowGuard.Label = labels.Title;
        }
        if (_flowModel is not null)
        {
            _flowModel.Label = EffectiveModelName.Length > 0 ? EffectiveModelName : labels.Model;
            _flowModel.Glyph = ModelMark ?? ArcIcons.Sparkles;
            _flowModel.GlyphBrush = ModelMarkBrush;
        }
        if (_replyMark is not null)
        {
            if (ModelMarkBrush is { } markBrush)
            {
                _replyMark.Foreground = markBrush;
            }
            else
            {
                _replyMark.ClearValue(ArcGlyph.ForegroundProperty);
            }
        }

        // The reply: the placeholder until a reply streams, the raw reply while it streams, then the restored text.
        var streaming = phase is PiiRedactionPhase.Streaming;
        var restored = _restoredText is not null && phase is PiiRedactionPhase.Restoring or PiiRedactionPhase.Done;
        var raw = _raw is not null && streaming;
        var placeholder = !raw && !restored;
        _placeholderFade?.To(placeholder ? 1 : 0, placeholder ? LayerIn : LayerOut, instantSwap);
        _streamFade?.To(raw ? 1 : 0, raw ? LayerIn : LayerOut, instantSwap || raw);
        _restoredFade?.To(restored ? 1 : 0, restored ? LayerIn : LayerOut, instantSwap);
        if (_replyPlaceholder is not null)
        {
            _replyPlaceholder.Text = labels.ReplyPlaceholder;
        }
        SetLayerAccess(_replyPlaceholder, placeholder);
        SetLayerAccess(_stream, raw);
        SetLayerAccess(_restored, restored);

        // The footer: the status with its icon, Reset when there is something to reset, and the one primary action.
        var status = StatusFor(phase);
        if (_status is not null && _status.Text != status)
        {
            _status.Text = status;
        }
        if (_status is not null)
        {
            _status.IsShimmering = IsWorking(phase);
        }
        if (_statusGlyph is not null)
        {
            _statusGlyph.Data = GlyphFor(phase);
        }
        if (_reset is not null)
        {
            _reset.Content = labels.Reset;
            var resettable = phase != PiiRedactionPhase.Idle;
            _reset.IsTabStop = resettable;
            _reset.IsHitTestVisible = resettable;
            AutomationProperties.SetAccessibilityView(_reset, resettable ? AccessibilityView.Default : AccessibilityView.Raw);
        }
        _primary.Content = PrimaryLabel(phase);
        _primary.IsEnabled = phase != PiiRedactionPhase.Idle || EffectiveMessage.Trim().Length > 0;

        PseudoClasses.Set(ResettablePseudoClass, phase != PiiRedactionPhase.Idle);
        PseudoClasses.Set(SimulatedPseudoClass, IsSimulated);
        PseudoClasses.Set(WorkingPseudoClass, IsWorking(phase));
        PseudoClasses.Set(ErrorPseudoClass, phase == PiiRedactionPhase.Error);
        PseudoClasses.Set(DonePseudoClass, phase == PiiRedactionPhase.Done || phase == PiiRedactionPhase.Redacted);
        AutomationProperties.SetName(this, labels.CardName);
    }

    private static void SetLayerAccess(Control? layer, bool shown)
    {
        if (layer is null)
        {
            return;
        }
        layer.IsHitTestVisible = shown;
        AutomationProperties.SetAccessibilityView(layer, shown ? AccessibilityView.Default : AccessibilityView.Raw);
    }

    internal static bool IsWorking(PiiRedactionPhase phase) => phase is PiiRedactionPhase.Scanning or PiiRedactionPhase.Sending
        or PiiRedactionPhase.Thinking or PiiRedactionPhase.Streaming or PiiRedactionPhase.Restoring;

    /// <summary>The label of the one primary action: Scan, Stop, Send, Run again or Retry.</summary>
    internal string PrimaryLabel(PiiRedactionPhase phase) => phase switch
    {
        PiiRedactionPhase.Idle => Labels.Scan,
        PiiRedactionPhase.Redacted => Labels.Send,
        PiiRedactionPhase.Done => Labels.RunAgain,
        PiiRedactionPhase.Error => Labels.Retry,
        _ => Labels.Stop,
    };

    /// <summary>The status line for a phase ("4 items redacted", "Waiting for" and the model).</summary>
    internal string StatusFor(PiiRedactionPhase phase)
    {
        var labels = Labels;
        return phase switch
        {
            PiiRedactionPhase.Idle => _stopped ? labels.Stopped : labels.Ready,
            PiiRedactionPhase.Scanning => labels.Scanning,
            PiiRedactionPhase.Redacted when _stopped => labels.Stopped,
            PiiRedactionPhase.Redacted => Entities.Count == 0 ? labels.NothingFound : labels.Redacted(Entities.Count),
            PiiRedactionPhase.Sending => labels.Sending,
            PiiRedactionPhase.Thinking => labels.Thinking(EffectiveModelName),
            PiiRedactionPhase.Streaming => labels.Streaming(EffectiveModelName),
            PiiRedactionPhase.Restoring => labels.Restoring,
            PiiRedactionPhase.Done => labels.Done,
            _ => string.IsNullOrWhiteSpace(_error) ? labels.Failed : _error!,
        };
    }

    private Geometry GlyphFor(PiiRedactionPhase phase) => phase switch
    {
        PiiRedactionPhase.Idle => _stopped ? ArcIcons.Square : ArcIcons.Shield,
        PiiRedactionPhase.Scanning => ArcIcons.ScanText,
        PiiRedactionPhase.Redacted => _stopped ? ArcIcons.Square : ArcIcons.ShieldCheck,
        PiiRedactionPhase.Sending => ArcIcons.Send,
        PiiRedactionPhase.Thinking => ArcIcons.Sparkles,
        PiiRedactionPhase.Streaming => ArcIcons.MessageSquare,
        PiiRedactionPhase.Restoring => ArcIcons.Undo2,
        PiiRedactionPhase.Done => ArcIcons.CircleCheck,
        _ => ArcIcons.CircleAlert,
    };

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == MessageProperty)
        {
            if (_phase != PiiRedactionPhase.Idle && !_editingFromField)
            {
                // A new message from the app starts over: the old marks and reply belong to the old text.
                ResetCore(focus: false);
            }
            else if (!_editingFromField)
            {
                RebuildMessageLayers();
            }
            Refresh(instant: false);
        }
        else if (change.Property == SimulationProperty)
        {
            // A new simulation (or none) starts over with its message, names and reply.
            if (_phase != PiiRedactionPhase.Idle)
            {
                ResetCore(focus: false);
            }
            else
            {
                RebuildMessageLayers();
            }
            UpdateReplyReserve();
            Refresh(instant: false);
            UpdateAutoPlay();
        }
        else if (change.Property == ShowOriginalProperty)
        {
            Refresh(instant: false);
        }
        else if (change.Property == NameBrushProperty || change.Property == EmailBrushProperty || change.Property == PhoneBrushProperty
                 || change.Property == CardBrushProperty || change.Property == MarkTintOpacityProperty || change.Property == CellTintOpacityProperty
                 || change.Property == MarkCornerRadiusProperty || change.Property == MarkOutsetProperty)
        {
            RefreshMarkBrushes();
        }
        else if (change.Property == LabelsProperty)
        {
            UpdateMessageNames();
            Refresh(instant: false);
        }
        else if (change.Property == IsEditableProperty || change.Property == ModelNameProperty || change.Property == ModelMarkProperty
                 || change.Property == ModelMarkBrushProperty || change.Property == SenderProperty)
        {
            Refresh(instant: false);
        }
        else if (change.Property == AutoPlayProperty)
        {
            UpdateAutoPlay();
        }
    }

    /// <summary>
    /// The reply area reserves the height of the simulation's restored reply, so nothing below it jumps as that reply
    /// streams in; without one it grows with the reply.
    /// </summary>
    private void UpdateReplyReserve()
    {
        if (_replyReserve is not null)
        {
            _replyReserve.Text = Simulation?.RestoredReply() ?? string.Empty;
        }
    }

    private void OnShowOriginalClick(object? sender, RoutedEventArgs e)
    {
        if (_syncingSwitch || _showOriginalSwitch is null)
        {
            return;
        }
        TakeOver();
        SetCurrentValue(ShowOriginalProperty, _showOriginalSwitch.IsChecked == true);
    }
}

/// <summary>What a type cell of <see cref="PiiRedaction"/> shows.</summary>
internal enum PiiRedactionCellKind
{
    NotChecked,
    Checking,
    Found,
    None
}

/// <summary>A type cell's state: what it shows and, once found, the tokens of its type in order.</summary>
internal readonly record struct PiiRedactionCellState(PiiRedactionCellKind Kind, IReadOnlyList<string> Tokens)
{
    public static readonly PiiRedactionCellState NotChecked = new(PiiRedactionCellKind.NotChecked, []);
}

/// <summary>The step a failure happened in, which Retry runs again.</summary>
internal enum PiiRedactionStep
{
    None,
    Scan,
    Send
}
