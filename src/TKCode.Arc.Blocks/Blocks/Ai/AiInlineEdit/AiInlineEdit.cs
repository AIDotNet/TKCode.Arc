using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Avalonia.Threading;
using TKCode.Arc.DesignTokens;
using TKCode.Arc.Primitives;

namespace TKCode.Arc;

/// <summary>
/// Editing a passage of text with AI in place: select words in <see cref="Text"/>, press Edit with AI (or Ctrl+K), say
/// what to change, and the rewrite shows inside the paragraph as word-level changes: removed words struck through,
/// added words tinted, each change with its own accept and reject buttons. Once every change is decided (or Accept all /
/// Reject all is pressed) the text updates and <see cref="EditApplied"/> reports it. The rewrite comes from
/// <see cref="RewriteHandler"/>; without one a simple built-in stand-in rewrites the sample text.
/// </summary>
/// <example><code>
/// &lt;arc:AiInlineEdit Text="{Binding Draft}" /&gt;
/// </code></example>
[TemplatePart(SourcePart, typeof(SelectableTextBlock))]
[TemplatePart(DocumentPart, typeof(TextBlock))]
[TemplatePart(EditPart, typeof(Button))]
[TemplatePart(InstructionPart, typeof(TextBox))]
[TemplatePart(SendPart, typeof(Button))]
[TemplatePart(CancelPart, typeof(Button))]
[TemplatePart(SuggestionsPart, typeof(Panel))]
[TemplatePart(AcceptAllPart, typeof(Button))]
[TemplatePart(RejectAllPart, typeof(Button))]
[TemplatePart(AnnouncerPart, typeof(AiAnnouncer))]
[PseudoClasses(IdlePseudoClass, InstructingPseudoClass, ThinkingPseudoClass, ReviewingPseudoClass, SelectionPseudoClass, ErrorPseudoClass)]
public class AiInlineEdit : TemplatedControl
{
    public const string SourcePart = "PART_Source";
    public const string DocumentPart = "PART_Document";
    public const string EditPart = "PART_Edit";
    public const string InstructionPart = "PART_Instruction";
    public const string SendPart = "PART_Send";
    public const string CancelPart = "PART_Cancel";
    public const string SuggestionsPart = "PART_Suggestions";
    public const string AcceptAllPart = "PART_AcceptAll";
    public const string RejectAllPart = "PART_RejectAll";
    public const string AnnouncerPart = "PART_Announcer";

    /// <summary>Set while nothing is being edited.</summary>
    public const string IdlePseudoClass = ":idle";

    /// <summary>Set while the instruction field is open.</summary>
    public const string InstructingPseudoClass = ":instructing";

    /// <summary>Set while the rewrite is written.</summary>
    public const string ThinkingPseudoClass = ":thinking";

    /// <summary>Set while changes are reviewed.</summary>
    public const string ReviewingPseudoClass = ":reviewing";

    /// <summary>Set while text is selected.</summary>
    public const string SelectionPseudoClass = ":has-selection";

    /// <summary>Set while the last rewrite failed.</summary>
    public const string ErrorPseudoClass = ":error";

    #region Properties

    /// <summary>The text (two-way: an applied edit writes the new text). Default: the sample paragraph.</summary>
    public static readonly StyledProperty<string> TextProperty =
        AvaloniaProperty.Register<AiInlineEdit, string>(nameof(Text), AiInlineEditSamples.Text, defaultBindingMode: BindingMode.TwoWay);

    /// <summary>Where the selected passage starts (two-way: selecting text with the pointer or the keyboard writes it).</summary>
    public static readonly StyledProperty<int> SelectionStartProperty =
        AvaloniaProperty.Register<AiInlineEdit, int>(nameof(SelectionStart), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>The length of the selected passage; zero means no selection (two-way).</summary>
    public static readonly StyledProperty<int> SelectionLengthProperty =
        AvaloniaProperty.Register<AiInlineEdit, int>(nameof(SelectionLength), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>The quick instructions offered under the field. Default: shorter, friendlier, fix grammar, more formal.</summary>
    public static readonly StyledProperty<IReadOnlyList<string>?> SuggestionsProperty =
        AvaloniaProperty.Register<AiInlineEdit, IReadOnlyList<string>?>(nameof(Suggestions), AiInlineEditSamples.Suggestions());

    /// <summary>Every word the editor shows or reads out, with English defaults.</summary>
    public static readonly StyledProperty<AiInlineEditLabels> LabelsProperty =
        AvaloniaProperty.Register<AiInlineEdit, AiInlineEditLabels>(nameof(Labels), new AiInlineEditLabels());

    /// <summary>Where the edit stands.</summary>
    public static readonly DirectProperty<AiInlineEdit, AiInlineEditPhase> PhaseProperty =
        AvaloniaProperty.RegisterDirect<AiInlineEdit, AiInlineEditPhase>(nameof(Phase), o => o.Phase);

    /// <summary>The number of changes under review (zero outside a review).</summary>
    public static readonly DirectProperty<AiInlineEdit, int> ChangeCountProperty =
        AvaloniaProperty.RegisterDirect<AiInlineEdit, int>(nameof(ChangeCount), o => o.ChangeCount);

    /// <summary>The message of the last failed rewrite; null after a success or a new request.</summary>
    public static readonly DirectProperty<AiInlineEdit, string?> ErrorMessageProperty =
        AvaloniaProperty.RegisterDirect<AiInlineEdit, string?>(nameof(ErrorMessage), o => o.ErrorMessage);

    /// <summary>Raised when a reviewed rewrite is applied to <see cref="Text"/>.</summary>
    public static readonly RoutedEvent<AiInlineEditAppliedEventArgs> EditAppliedEvent =
        RoutedEvent.Register<AiInlineEdit, AiInlineEditAppliedEventArgs>(nameof(EditApplied), RoutingStrategies.Bubble);

    /// <inheritdoc cref="TextProperty"/>
    public string Text { get => GetValue(TextProperty); set => SetValue(TextProperty, value); }

    /// <inheritdoc cref="SelectionStartProperty"/>
    public int SelectionStart { get => GetValue(SelectionStartProperty); set => SetValue(SelectionStartProperty, value); }

    /// <inheritdoc cref="SelectionLengthProperty"/>
    public int SelectionLength { get => GetValue(SelectionLengthProperty); set => SetValue(SelectionLengthProperty, value); }

    /// <inheritdoc cref="SuggestionsProperty"/>
    public IReadOnlyList<string>? Suggestions { get => GetValue(SuggestionsProperty); set => SetValue(SuggestionsProperty, value); }

    /// <inheritdoc cref="LabelsProperty"/>
    public AiInlineEditLabels Labels { get => GetValue(LabelsProperty); set => SetValue(LabelsProperty, value); }

    /// <inheritdoc cref="PhaseProperty"/>
    public AiInlineEditPhase Phase { get => _phase; private set => SetAndRaise(PhaseProperty, ref _phase, value); }

    /// <inheritdoc cref="ChangeCountProperty"/>
    public int ChangeCount { get => _changeCount; private set => SetAndRaise(ChangeCountProperty, ref _changeCount, value); }

    /// <inheritdoc cref="ErrorMessageProperty"/>
    public string? ErrorMessage { get => _error; private set => SetAndRaise(ErrorMessageProperty, ref _error, value); }

    /// <summary>
    /// Writes the rewrite of a request through your model: return the new passage (only the selection, not the whole text);
    /// throw to show the exception's message. The token is cancelled by Cancel, Escape or leaving the window. Null (the
    /// default) uses <see cref="AiInlineEditSamples.RewriteAsync"/>.
    /// </summary>
    public Func<AiInlineEditRequest, CancellationToken, Task<string>>? RewriteHandler { get; set; }

    /// <inheritdoc cref="EditAppliedEvent"/>
    public event EventHandler<AiInlineEditAppliedEventArgs>? EditApplied
    {
        add => AddHandler(EditAppliedEvent, value);
        remove => RemoveHandler(EditAppliedEvent, value);
    }

    #endregion

    private AiInlineEditPhase _phase;
    private int _changeCount;
    private string? _error;
    private SelectableTextBlock? _source;
    private TextBlock? _document;
    private Button? _edit;
    private TextBox? _instruction;
    private Button? _send;
    private Button? _cancel;
    private Panel? _suggestions;
    private Button? _acceptAll;
    private Button? _rejectAll;
    private AiAnnouncer? _announcer;
    private TextBlock? _statusLine;
    private readonly List<AiInlineEditPiece> _pieces = new();
    private readonly List<AiInlineEditHunk> _hunks = new();
    private CancellationTokenSource? _request;
    private int _editStart;
    private int _editLength;
    private string _editInstruction = string.Empty;
    private bool _syncingSelection;

    static AiInlineEdit()
    {
        AutomationProperties.LandmarkTypeProperty.OverrideDefaultValue<AiInlineEdit>(AutomationLandmarkType.Region);
    }

    public AiInlineEdit()
    {
        UpdatePseudoClasses();
    }

    internal IReadOnlyList<AiInlineEditHunk> Hunks => _hunks;

    internal AiAnnouncer? Announcer => _announcer;

    internal TextBlock? Document => _document;

    internal TextBox? InstructionBox => _instruction;

    #region Public actions

    /// <summary>Opens the instruction field for the current selection. Does nothing without a selection or while busy.</summary>
    public void BeginEdit()
    {
        if (Phase is AiInlineEditPhase.Thinking or AiInlineEditPhase.Reviewing || !TryGetSelection(out var start, out var length))
        {
            return;
        }
        _editStart = start;
        _editLength = length;
        ErrorMessage = null;
        SetPhase(AiInlineEditPhase.Instructing);
        if (_instruction is not null)
        {
            _instruction.Text = string.Empty;
            Dispatcher.UIThread.Post(() => _instruction?.Focus(NavigationMethod.Tab), DispatcherPriority.Input);
        }
    }

    /// <summary>Sends <paramref name="instruction"/> for the selected passage and shows the rewrite for review.</summary>
    public async Task SubmitAsync(string instruction)
    {
        if (string.IsNullOrWhiteSpace(instruction))
        {
            return;
        }
        if (Phase == AiInlineEditPhase.Idle)
        {
            if (!TryGetSelection(out _editStart, out _editLength))
            {
                return;
            }
        }
        else if (Phase != AiInlineEditPhase.Instructing)
        {
            return;
        }
        _editInstruction = instruction.Trim();
        var text = Text ?? string.Empty;
        var request = new AiInlineEditRequest(text, _editStart, _editLength, text.Substring(_editStart, _editLength), _editInstruction);
        _request?.Cancel();
        var cancellation = _request = new CancellationTokenSource();
        ErrorMessage = null;
        SetPhase(AiInlineEditPhase.Thinking);
        string rewrite;
        try
        {
            var handler = RewriteHandler ?? AiInlineEditSamples.RewriteAsync;
            rewrite = await handler(request, cancellation.Token);
        }
        catch (OperationCanceledException)
        {
            return;
        }
        catch (Exception exception)
        {
            if (!ReferenceEquals(cancellation, _request))
            {
                return;
            }
            ErrorMessage = exception.Message;
            SetPhase(AiInlineEditPhase.Instructing);
            _announcer?.Announce(Labels.Failed(exception.Message));
            return;
        }
        if (!ReferenceEquals(cancellation, _request) || cancellation.IsCancellationRequested || Phase != AiInlineEditPhase.Thinking)
        {
            return;
        }
        _request = null;
        StartReview(request.Selection, rewrite ?? string.Empty);
    }

    /// <summary>Accepts the change at <paramref name="index"/>; the edit applies once every change is decided.</summary>
    public void AcceptChange(int index) => Decide(index, true);

    /// <summary>Rejects the change at <paramref name="index"/>; the edit applies once every change is decided.</summary>
    public void RejectChange(int index) => Decide(index, false);

    /// <summary>Accepts every change not decided yet and applies the edit.</summary>
    public void AcceptAll() => DecideRest(true);

    /// <summary>Rejects every change not decided yet and applies the edit (the passage keeps what was accepted).</summary>
    public void RejectAll() => DecideRest(false);

    /// <summary>Closes the instruction field or drops a running request; in a review it rejects what is left.</summary>
    public void Cancel()
    {
        switch (Phase)
        {
            case AiInlineEditPhase.Reviewing:
                RejectAll();
                break;
            case AiInlineEditPhase.Thinking:
            case AiInlineEditPhase.Instructing:
                _request?.Cancel();
                _request = null;
                ErrorMessage = null;
                SetPhase(AiInlineEditPhase.Idle);
                _source?.Focus(NavigationMethod.Tab);
                break;
        }
    }

    #endregion

    #region Review

    private void StartReview(string before, string after)
    {
        _pieces.Clear();
        _hunks.Clear();
        var diff = WordDiff.Compute(before, after);
        string? deleted = null;
        foreach (var segment in diff.Segments)
        {
            switch (segment.Kind)
            {
                case WordDiffKind.Equal:
                    FlushDelete(ref deleted);
                    _pieces.Add(new AiInlineEditPiece(segment.Text, null));
                    break;
                case WordDiffKind.Delete:
                    deleted = segment.Text;
                    break;
                default:
                    var hunk = new AiInlineEditHunk(deleted ?? string.Empty, segment.Text);
                    deleted = null;
                    _hunks.Add(hunk);
                    _pieces.Add(new AiInlineEditPiece(null, hunk));
                    break;
            }
        }
        FlushDelete(ref deleted);
        ChangeCount = _hunks.Count;
        if (_hunks.Count == 0)
        {
            ErrorMessage = null;
            SetPhase(AiInlineEditPhase.Instructing);
            _announcer?.Announce(Labels.NoChanges);
            UpdateStatus(Labels.NoChanges);
            return;
        }
        SetPhase(AiInlineEditPhase.Reviewing);
        _announcer?.Announce(Labels.Ready(_hunks.Count));
        Dispatcher.UIThread.Post(FocusFirstOpenChange, DispatcherPriority.Input);
    }

    private void FlushDelete(ref string? deleted)
    {
        if (deleted is null)
        {
            return;
        }
        var hunk = new AiInlineEditHunk(deleted, string.Empty);
        _hunks.Add(hunk);
        _pieces.Add(new AiInlineEditPiece(null, hunk));
        deleted = null;
    }

    private void Decide(int index, bool accept)
    {
        if (Phase != AiInlineEditPhase.Reviewing || index < 0 || index >= _hunks.Count || _hunks[index].Decision is not null)
        {
            return;
        }
        _hunks[index].Decision = accept;
        if (_hunks.TrueForAll(h => h.Decision is not null))
        {
            Apply();
            return;
        }
        RenderDocument();
        UpdateTexts();
        Dispatcher.UIThread.Post(FocusFirstOpenChange, DispatcherPriority.Input);
    }

    private void DecideRest(bool accept)
    {
        if (Phase != AiInlineEditPhase.Reviewing)
        {
            return;
        }
        foreach (var hunk in _hunks)
        {
            hunk.Decision ??= accept;
        }
        Apply();
    }

    /// <summary>The passage with each change's decision (undecided changes keep the old words).</summary>
    internal string MergedPassage()
    {
        var builder = new System.Text.StringBuilder();
        foreach (var piece in _pieces)
        {
            if (piece.Hunk is { } hunk)
            {
                builder.Append(hunk.Decision == true ? hunk.Inserted : hunk.Deleted);
            }
            else
            {
                builder.Append(piece.Text);
            }
        }
        return builder.ToString();
    }

    private void Apply()
    {
        var old = Text ?? string.Empty;
        var passage = MergedPassage();
        var updated = string.Concat(old.AsSpan(0, _editStart), passage, old.AsSpan(_editStart + _editLength));
        var accepted = _hunks.Count(h => h.Decision == true);
        var rejected = _hunks.Count - accepted;
        _pieces.Clear();
        _hunks.Clear();
        ChangeCount = 0;
        SetPhase(AiInlineEditPhase.Idle);
        if (updated != old)
        {
            SetCurrentValue(TextProperty, updated);
        }
        // The edited passage stays selected, so the next instruction can refine it.
        SetSelection(_editStart, passage.Length);
        _announcer?.Announce(Labels.Applied(accepted, rejected));
        RaiseEvent(new AiInlineEditAppliedEventArgs(EditAppliedEvent, old, updated, _editInstruction, accepted, rejected));
        _source?.Focus(NavigationMethod.Tab);
    }

    private void FocusFirstOpenChange()
    {
        if (_document?.Inlines is null)
        {
            return;
        }
        foreach (var inline in _document.Inlines)
        {
            if (inline is InlineUIContainer { Child: AiInlineEditChangeActions actions } && actions.Hunk?.Decision is null)
            {
                actions.FocusAccept();
                return;
            }
        }
    }

    #endregion

    #region Document

    /// <summary>Draws the text into the document block: the selection tinted while instructing, the changes while reviewing.</summary>
    private void RenderDocument()
    {
        if (_document is null)
        {
            return;
        }
        var text = Text ?? string.Empty;
        var inlines = new InlineCollection();
        var start = Math.Clamp(_editStart, 0, text.Length);
        var end = Math.Clamp(_editStart + _editLength, start, text.Length);
        if (start > 0)
        {
            inlines.Add(new Run(text[..start]));
        }
        if (Phase == AiInlineEditPhase.Reviewing)
        {
            var labels = Labels;
            for (var i = 0; i < _pieces.Count; i++)
            {
                var piece = _pieces[i];
                if (piece.Hunk is not { } hunk)
                {
                    inlines.Add(new Run(piece.Text));
                    continue;
                }
                if (hunk.Decision is { } decision)
                {
                    var kept = decision ? hunk.Inserted : hunk.Deleted;
                    if (kept.Length > 0)
                    {
                        inlines.Add(new Run(kept) { TextDecorations = decision ? TextDecorations.Underline : null });
                    }
                    continue;
                }
                if (hunk.Deleted.Length > 0)
                {
                    inlines.Add(new Run(hunk.Deleted)
                    {
                        TextDecorations = TextDecorations.Strikethrough,
                        Foreground = Brush(ArcDesignTokenKind.Danger),
                        Background = Brush(ArcDesignTokenKind.DangerSubtle)
                    });
                }
                if (hunk.Inserted.Length > 0)
                {
                    inlines.Add(new Run(hunk.Inserted) { Background = Brush(ArcDesignTokenKind.SuccessSubtle) });
                }
                var actions = new AiInlineEditChangeActions { Hunk = hunk, Owner = this, Index = _hunks.IndexOf(hunk) };
                actions.SetNames(labels.AcceptChange(hunk.Deleted, hunk.Inserted), labels.RejectChange(hunk.Deleted, hunk.Inserted));
                inlines.Add(new InlineUIContainer(actions) { BaselineAlignment = BaselineAlignment.Center });
            }
        }
        else
        {
            inlines.Add(new Run(text[start..end]) { Background = Brush(ArcDesignTokenKind.AccentSubtle) });
        }
        if (end < text.Length)
        {
            inlines.Add(new Run(text[end..]));
        }
        _document.Inlines = inlines;
    }

    private IBrush Brush(ArcDesignTokenKind kind) => new ImmutableSolidColorBrush(ArcTokens.GetColor(this, kind));

    #endregion

    #region Template

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        if (_source is not null)
        {
            _source.PropertyChanged -= OnSourcePropertyChanged;
        }
        Unhook(_edit, OnEditClick);
        Unhook(_send, OnSendClick);
        Unhook(_cancel, OnCancelClick);
        Unhook(_acceptAll, OnAcceptAllClick);
        Unhook(_rejectAll, OnRejectAllClick);
        if (_instruction is not null)
        {
            _instruction.KeyDown -= OnInstructionKeyDown;
        }
        base.OnApplyTemplate(e);
        var scope = e.NameScope;
        _source = scope.Find<SelectableTextBlock>(SourcePart);
        _document = scope.Find<TextBlock>(DocumentPart);
        _edit = scope.Find<Button>(EditPart);
        _instruction = scope.Find<TextBox>(InstructionPart);
        _send = scope.Find<Button>(SendPart);
        _cancel = scope.Find<Button>(CancelPart);
        _suggestions = scope.Find<Panel>(SuggestionsPart);
        _acceptAll = scope.Find<Button>(AcceptAllPart);
        _rejectAll = scope.Find<Button>(RejectAllPart);
        _announcer = scope.Find<AiAnnouncer>(AnnouncerPart);
        _statusLine = scope.Find<TextBlock>("PART_Status");
        if (_source is not null)
        {
            _source.PropertyChanged += OnSourcePropertyChanged;
        }
        Hook(_edit, OnEditClick);
        Hook(_send, OnSendClick);
        Hook(_cancel, OnCancelClick);
        Hook(_acceptAll, OnAcceptAllClick);
        Hook(_rejectAll, OnRejectAllClick);
        if (_instruction is not null)
        {
            _instruction.KeyDown += OnInstructionKeyDown;
        }
        BuildSuggestions();
        PushSelectionToSource();
        UpdateTexts();
        RenderDocument();
    }

    private static void Hook(Button? button, EventHandler<RoutedEventArgs> handler)
    {
        if (button is not null)
        {
            button.Click += handler;
        }
    }

    private static void Unhook(Button? button, EventHandler<RoutedEventArgs> handler)
    {
        if (button is not null)
        {
            button.Click -= handler;
        }
    }

    private void OnEditClick(object? sender, RoutedEventArgs e) => BeginEdit();

    private void OnSendClick(object? sender, RoutedEventArgs e) => _ = SubmitAsync(_instruction?.Text ?? string.Empty);

    private void OnCancelClick(object? sender, RoutedEventArgs e) => Cancel();

    private void OnAcceptAllClick(object? sender, RoutedEventArgs e) => AcceptAll();

    private void OnRejectAllClick(object? sender, RoutedEventArgs e) => RejectAll();

    private void OnInstructionKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && e.KeyModifiers == KeyModifiers.None)
        {
            e.Handled = true;
            _ = SubmitAsync(_instruction?.Text ?? string.Empty);
        }
    }

    private void BuildSuggestions()
    {
        if (_suggestions is null)
        {
            return;
        }
        _suggestions.Children.Clear();
        foreach (var suggestion in Suggestions ?? [])
        {
            var chip = new Button { Content = suggestion, Variant = ButtonVariant.Secondary, Size = ControlSize.Small };
            chip.Classes.Add("suggestion");
            chip.Click += (_, _) => _ = SubmitAsync(suggestion);
            _suggestions.Children.Add(chip);
        }
    }

    /// <summary>Ctrl+K (Cmd+K) opens the field for the selection; Escape cancels or rejects what is left.</summary>
    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Handled)
        {
            return;
        }
        if (e.Key == Key.K && (e.KeyModifiers & (KeyModifiers.Control | KeyModifiers.Meta)) != 0 && Phase == AiInlineEditPhase.Idle)
        {
            e.Handled = true;
            BeginEdit();
        }
        else if (e.Key == Key.Escape && Phase != AiInlineEditPhase.Idle)
        {
            e.Handled = true;
            Cancel();
        }
    }

    #endregion

    #region Selection and state

    private bool TryGetSelection(out int start, out int length)
    {
        var text = Text ?? string.Empty;
        start = Math.Clamp(SelectionStart, 0, text.Length);
        length = Math.Clamp(SelectionLength, 0, text.Length - start);
        // Trim the spaces at the ends of a selection: a rewrite of "word " should not eat the space.
        while (length > 0 && char.IsWhiteSpace(text[start]))
        {
            start++;
            length--;
        }
        while (length > 0 && char.IsWhiteSpace(text[start + length - 1]))
        {
            length--;
        }
        return length > 0;
    }

    private void SetSelection(int start, int length)
    {
        SetCurrentValue(SelectionStartProperty, start);
        SetCurrentValue(SelectionLengthProperty, length);
    }

    private void OnSourcePropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (_syncingSelection || _source is null ||
            (e.Property != SelectableTextBlock.SelectionStartProperty && e.Property != SelectableTextBlock.SelectionEndProperty))
        {
            return;
        }
        var a = _source.SelectionStart;
        var b = _source.SelectionEnd;
        _syncingSelection = true;
        try
        {
            SetSelection(Math.Min(a, b), Math.Abs(b - a));
        }
        finally
        {
            _syncingSelection = false;
        }
    }

    private void PushSelectionToSource()
    {
        if (_source is null || _syncingSelection)
        {
            return;
        }
        _syncingSelection = true;
        try
        {
            var length = (Text ?? string.Empty).Length;
            var start = Math.Clamp(SelectionStart, 0, length);
            _source.SelectionStart = start;
            _source.SelectionEnd = Math.Clamp(start + SelectionLength, start, length);
        }
        finally
        {
            _syncingSelection = false;
        }
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == SelectionStartProperty || change.Property == SelectionLengthProperty)
        {
            PushSelectionToSource();
            UpdatePseudoClasses();
            UpdateTexts();
        }
        else if (change.Property == TextProperty)
        {
            if (Phase != AiInlineEditPhase.Idle)
            {
                // The text changed under the edit: the request no longer fits it.
                _request?.Cancel();
                _request = null;
                _pieces.Clear();
                _hunks.Clear();
                ChangeCount = 0;
                SetPhase(AiInlineEditPhase.Idle);
            }
            var length = (Text ?? string.Empty).Length;
            if (SelectionStart + SelectionLength > length)
            {
                SetSelection(Math.Min(SelectionStart, length), 0);
            }
            RenderDocument();
            UpdateTexts();
        }
        else if (change.Property == SuggestionsProperty)
        {
            BuildSuggestions();
        }
        else if (change.Property == LabelsProperty)
        {
            UpdateTexts();
            RenderDocument();
        }
        else if (change.Property == ErrorMessageProperty)
        {
            UpdatePseudoClasses();
            UpdateTexts();
        }
    }

    private void SetPhase(AiInlineEditPhase phase)
    {
        Phase = phase;
        UpdatePseudoClasses();
        RenderDocument();
        UpdateTexts();
    }

    private void UpdatePseudoClasses()
    {
        PseudoClasses.Set(IdlePseudoClass, _phase == AiInlineEditPhase.Idle);
        PseudoClasses.Set(InstructingPseudoClass, _phase == AiInlineEditPhase.Instructing);
        PseudoClasses.Set(ThinkingPseudoClass, _phase == AiInlineEditPhase.Thinking);
        PseudoClasses.Set(ReviewingPseudoClass, _phase == AiInlineEditPhase.Reviewing);
        PseudoClasses.Set(SelectionPseudoClass, SelectionLength > 0);
        PseudoClasses.Set(ErrorPseudoClass, _error is not null);
    }

    private void UpdateTexts()
    {
        var labels = Labels;
        string status;
        switch (Phase)
        {
            case AiInlineEditPhase.Reviewing:
                status = labels.Review(_hunks.Count, _hunks.Count(h => h.Decision == true), _hunks.Count(h => h.Decision == false));
                break;
            case AiInlineEditPhase.Thinking:
                status = labels.Thinking;
                break;
            case AiInlineEditPhase.Instructing:
                status = _error is not null ? labels.Failed(_error) : labels.Selected(CountWords(_editStart, _editLength));
                break;
            default:
                status = TryGetSelection(out var start, out var length) ? labels.Selected(CountWords(start, length)) : labels.Hint;
                break;
        }
        UpdateStatus(status);
        SetButton(_edit, labels.Edit);
        SetButton(_send, labels.Send);
        SetButton(_cancel, labels.Cancel);
        SetButton(_acceptAll, labels.AcceptAll);
        SetButton(_rejectAll, labels.RejectAll);
        if (_edit is not null)
        {
            _edit.IsEnabled = Phase == AiInlineEditPhase.Idle && SelectionLength > 0;
        }
        if (_send is not null)
        {
            _send.IsLoading = Phase == AiInlineEditPhase.Thinking;
        }
        if (_instruction is not null)
        {
            if (_instruction is FieldTextBox field)
            {
                field.Placeholder = labels.Placeholder;
            }
            AutomationProperties.SetName(_instruction, labels.Placeholder);
            _instruction.IsReadOnly = Phase == AiInlineEditPhase.Thinking;
        }
    }

    private void UpdateStatus(string text)
    {
        if (_statusLine is not null)
        {
            _statusLine.Text = text;
        }
    }

    private static void SetButton(Button? button, string text)
    {
        if (button is not null)
        {
            button.Content = text;
            AutomationProperties.SetName(button, text);
        }
    }

    private int CountWords(int start, int length)
    {
        var text = Text ?? string.Empty;
        var words = 0;
        var inWord = false;
        for (var i = start; i < start + length && i < text.Length; i++)
        {
            var letter = !char.IsWhiteSpace(text[i]);
            if (letter && !inWord)
            {
                words++;
            }
            inWord = letter;
        }
        return words;
    }

    #endregion

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        if (Phase == AiInlineEditPhase.Thinking)
        {
            Cancel();
        }
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new AiInlineEditAutomationPeer(this);

    private sealed class AiInlineEditAutomationPeer(AiInlineEdit owner) : ControlAutomationPeer(owner)
    {
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Group;

        protected override string? GetNameCore() =>
            base.GetNameCore() is { Length: > 0 } name ? name : ((AiInlineEdit)Owner).Labels.Region;
    }
}

/// <summary>The accept and reject buttons that follow a change inside the paragraph.</summary>
internal sealed class AiInlineEditChangeActions : StackPanel
{
    private readonly AiInlineEditChangeButton _accept = new() { Glyph = ArcIcons.Check, IsAccept = true };
    private readonly AiInlineEditChangeButton _reject = new() { Glyph = ArcIcons.X };

    public AiInlineEditChangeActions()
    {
        Orientation = Orientation.Horizontal;
        Spacing = 2;
        Margin = new Thickness(3, 0, 2, 0);
        Children.Add(_accept);
        Children.Add(_reject);
        _accept.Click += (_, _) => Owner?.AcceptChange(Index);
        _reject.Click += (_, _) => Owner?.RejectChange(Index);
    }

    public AiInlineEditHunk? Hunk { get; set; }

    public AiInlineEdit? Owner { get; set; }

    public int Index { get; set; }

    internal AiInlineEditChangeButton AcceptButton => _accept;

    internal AiInlineEditChangeButton RejectButton => _reject;

    public void SetNames(string accept, string reject)
    {
        AutomationProperties.SetName(_accept, accept);
        AutomationProperties.SetName(_reject, reject);
        Tooltip.SetTip(_accept, accept);
        Tooltip.SetTip(_reject, reject);
    }

    public void FocusAccept() => _accept.Focus(NavigationMethod.Tab);
}

/// <summary>A small round accept (check) or reject (cross) button of a change.</summary>
[PseudoClasses(AcceptPseudoClass)]
internal sealed class AiInlineEditChangeButton : AiAgentButton
{
    public const string AcceptPseudoClass = ":accept";

    public static readonly StyledProperty<Geometry?> GlyphProperty =
        AvaloniaProperty.Register<AiInlineEditChangeButton, Geometry?>(nameof(Glyph));

    public Geometry? Glyph { get => GetValue(GlyphProperty); set => SetValue(GlyphProperty, value); }

    public bool IsAccept
    {
        get => PseudoClasses.Contains(AcceptPseudoClass);
        init => PseudoClasses.Set(AcceptPseudoClass, value);
    }
}
