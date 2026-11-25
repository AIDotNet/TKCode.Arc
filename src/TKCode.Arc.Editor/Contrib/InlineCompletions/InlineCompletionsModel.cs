// Ported from vscode (commit b57d5c9e), MIT license, as a reduced port of
//  - src/vs/editor/contrib/inlineCompletions/browser/model/inlineCompletionsModel.ts (trigger, triggerExplicitly,
//    stop, next / previous, the ghost text state, accept, acceptNextWord, acceptNextLine);
//  - src/vs/editor/contrib/inlineCompletions/browser/model/inlineCompletionsSource.ts (the debounced, cancellable
//    fetch) and provideInlineCompletions.ts (asking the providers, yieldsToGroupIds, the default range);
//  - inlineSuggestionItem.ts (an item's range follows the edits: here a model decoration that grows when typing at
//    its edges, upstream's withEdit);
//  - src/vs/editor/contrib/inlineCompletions/browser/utils.ts (getEndPositionsAfterApplying,
//    removeTextReplacementCommonSuffixPrefix).
//
// Differences: no observables: the state is recomputed when the items, the selection, the text or the cursors change,
// and DidChangeState fires when it differs. Left out: inline edits (next edit suggestions, isInlineEdit items are
// skipped), the suggest widget integration (preview of the selected suggestion, augmentation), snippets
// (insertText.snippet items are skipped) and completeBracketPairs, forward stability, telemetry, the debounce
// service (a fixed 50 ms), jumping to other documents, typing-speed heuristics. Edits and the cursor go through an
// IInlineCompletionsEditor (the CodeEditor in the controls; a test editor in the tests).

using System.Text.RegularExpressions;
using TKCode.Arc.Editor.Languages;
using TKCode.Arc.Editor.Model;

namespace TKCode.Arc.Editor.Contrib.InlineSuggestions;

/// <summary>What the inline completions model needs from an editor.</summary>
internal interface IInlineCompletionsEditor
{
    TextModel Model { get; }

    IReadOnlyList<TextSelection> GetSelections();

    void PushUndoStop();

    /// <summary>Applies the edits as one undo step and leaves the cursors at <paramref name="endCursorState"/>.</summary>
    bool ExecuteEdits(string source, IReadOnlyList<SingleEditOperation> edits, IReadOnlyList<TextSelection>? endCursorState);

    /// <summary>The word definition of the language at a position (acceptNextWord).</summary>
    Regex GetWordDefinition(TextPosition position);

    /// <summary>Runs a completion's command after it was accepted.</summary>
    void ExecuteCommand(LanguageCommand command);
}

/// <summary>An inline completion of one provider's list, with its range tracked in the model.</summary>
internal sealed class InlineCompletionItem
{
    internal InlineCompletionItem(IInlineCompletionsProvider provider, InlineCompletions list, InlineCompletion completion, TextReplacement edit, string decorationId)
    {
        Provider = provider;
        List = list;
        SourceCompletion = completion;
        OriginalEdit = edit;
        DecorationId = decorationId;
    }

    public IInlineCompletionsProvider Provider { get; }

    public InlineCompletions List { get; }

    public InlineCompletion SourceCompletion { get; }

    /// <summary>The replacement as the provider gave it.</summary>
    public TextReplacement OriginalEdit { get; }

    internal string DecorationId { get; }

    /// <summary>Upstream's semanticId: items with the same text and range are the same suggestion.</summary>
    public string SemanticId => $"{OriginalEdit.Range}:{OriginalEdit.Text}";

    /// <summary>The replacement now: the range follows the edits made since the item came.</summary>
    public TextReplacement GetSingleTextEdit(TextModel model) =>
        model.GetDecorationRange(DecorationId) is { } range ? OriginalEdit with { Range = range } : OriginalEdit;
}

/// <summary>The ghost text state of the model (upstream's state of kind 'ghostText').</summary>
internal sealed record InlineCompletionsState(
    IReadOnlyList<TextReplacement> Edits,
    GhostText PrimaryGhostText,
    IReadOnlyList<GhostText> GhostTexts,
    InlineCompletionItem InlineSuggestion);

/// <summary>Asks the inline completion providers and keeps the ghost text of an editor (upstream's InlineCompletionsModel).</summary>
internal sealed class InlineCompletionsModel : IDisposable
{
    /// <summary>The delay of an automatic request (upstream's debounce minimum and maximum, 50 ms).</summary>
    public const int DebounceDelay = 50;

    private static readonly ModelDecorationOptions s_rangeOptions = new("inline-completion-range")
    {
        Stickiness = TrackedRangeStickiness.AlwaysGrowsWhenTypingAtEdges,
    };

    private readonly IInlineCompletionsEditor _editor;
    private readonly LanguageFeatureRegistry<IInlineCompletionsProvider> _registry;
    private readonly IdleScheduler _scheduler;
    private readonly List<(IInlineCompletionsProvider Provider, Action Handler)> _providerListeners = new();
    private List<InlineCompletionItem> _items = new();
    private List<(IInlineCompletionsProvider Provider, InlineCompletions List)> _lists = new();
    private string? _selectedId;
    private bool _isActive;
    private InlineCompletionsState? _state;
    private IDisposable? _debounceTimer;
    private CancellationTokenSource? _requestCts;
    private (int VersionId, TextPosition Position, bool Explicit)? _lastRequest;
    private bool _isAcceptingPartially;
    private bool _disposed;

    public InlineCompletionsModel(IInlineCompletionsEditor editor, LanguageFeatureRegistry<IInlineCompletionsProvider> registry, IdleScheduler scheduler)
    {
        _editor = editor;
        _registry = registry;
        _scheduler = scheduler;
        TextModel.DidChangeContent += OnDidChangeContent;
        _registry.DidChange += OnRegistryChanged;
        BindProviderListeners();
    }

    public TextModel TextModel => _editor.Model;

    /// <summary>Upstream's inlineSuggest.enabled: whether typing asks for inline completions.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>Upstream's inlineSuggest.mode.</summary>
    public InlineSuggestMode Mode { get; set; } = InlineSuggestMode.SubwordSmart;

    /// <summary>The current ghost text state, or null when no ghost text is shown.</summary>
    public InlineCompletionsState? State => _state;

    /// <summary>The ghost text at the primary cursor.</summary>
    public GhostText? PrimaryGhostText => _state?.PrimaryGhostText;

    /// <summary>The items the providers returned, in order.</summary>
    public IReadOnlyList<InlineCompletionItem> Items => _items;

    /// <summary>The index of the selected item among the visible ones (upstream's selectedInlineCompletionIndex).</summary>
    public int SelectedInlineCompletionIndex
    {
        get
        {
            var visible = GetVisibleItems();
            var index = visible.FindIndex(i => i.SemanticId == _selectedId);
            return index < 0 ? 0 : index;
        }
    }

    /// <summary>How many items are visible at the cursor.</summary>
    public int VisibleItemCount => GetVisibleItems().Count;

    /// <summary>Whether a request is running.</summary>
    public bool IsLoading => _requestCts is not null;

    /// <summary>Raised when the ghost text state changed.</summary>
    public event Action? DidChangeState;

    /// <summary>Raised after an inline completion was accepted (fully).</summary>
    public event Action? DidAccept;

    // ---- triggering

    /// <summary>Asks the providers (automatic kind) after the debounce delay, or now.</summary>
    public Task Trigger(bool noDelay = false) => TriggerCore(explicitRequest: false, noDelay);

    /// <summary>Asks the providers now with the explicit trigger kind (Alt+\, cycling).</summary>
    public Task TriggerExplicitly() => TriggerCore(explicitRequest: true, noDelay: true);

    private Task TriggerCore(bool explicitRequest, bool noDelay)
    {
        _isActive = true;
        if (IsUpToDate(explicitRequest))
        {
            // the items are those of this text and position already
            UpdateState();
            return Task.CompletedTask;
        }

        if (noDelay)
        {
            // a pending automatic request still runs when its delay is over (and then wins, as upstream's does)
            return FetchNow(explicitRequest);
        }

        CancelDebounce();
        var tcs = new TaskCompletionSource();
        _debounceTimer = _scheduler.RunAfter(TimeSpan.FromMilliseconds(DebounceDelay), () =>
        {
            _debounceTimer = null;
            if (IsUpToDate(explicitRequest))
            {
                tcs.TrySetResult();
                return;
            }

            FetchNow(explicitRequest).ContinueWith(_ => tcs.TrySetResult(), TaskScheduler.Current);
        });
        return tcs.Task;
    }

    // whether the last answered request was for this text and position (and explicit, when an explicit one is asked for)
    private bool IsUpToDate(bool explicitRequest) =>
        _requestCts is null
        && _lastRequest is { } last
        && last.VersionId == TextModel.GetVersionId()
        && last.Position == GetPrimaryPosition()
        && (last.Explicit || !explicitRequest);

    /// <summary>Hides the ghost text and forgets the items (upstream's stop).</summary>
    public void Stop(bool explicitCancel = false)
    {
        if (explicitCancel && _state is { } state)
        {
            state.InlineSuggestion.Provider.HandleRejection(state.InlineSuggestion.List, state.InlineSuggestion.SourceCompletion);
        }

        _isActive = false;
        CancelDebounce();
        CancelRequest();
        _lastRequest = null;
        SetItems([], [], InlineCompletionsDisposeReasonKind.Other);
        _selectedId = null;
        UpdateState();
    }

    public async Task Next() => await DeltaSelectedInlineCompletionIndex(1).ConfigureAwait(true);

    public async Task Previous() => await DeltaSelectedInlineCompletionIndex(-1).ConfigureAwait(true);

    private async Task DeltaSelectedInlineCompletionIndex(int delta)
    {
        await TriggerExplicitly().ConfigureAwait(true);

        var completions = GetVisibleItems();
        if (completions.Count > 0)
        {
            var newIdx = (SelectedInlineCompletionIndex + delta + completions.Count) % completions.Count;
            _selectedId = completions[newIdx].SemanticId;
        }
        else
        {
            _selectedId = null;
        }

        UpdateState();
    }

    private void CancelDebounce()
    {
        _debounceTimer?.Dispose();
        _debounceTimer = null;
    }

    private void CancelRequest()
    {
        _requestCts?.Cancel();
        _requestCts = null;
    }

    private async Task FetchNow(bool explicitRequest)
    {
        if (_disposed)
        {
            return;
        }

        CancelRequest();
        var cts = new CancellationTokenSource();
        _requestCts = cts;
        var model = TextModel;
        var position = GetPrimaryPosition();
        var versionId = model.GetVersionId();
        var context = new InlineCompletionContext(explicitRequest ? InlineCompletionTriggerKind.Explicit : InlineCompletionTriggerKind.Automatic);

        List<(IInlineCompletionsProvider Provider, InlineCompletions List)> results;
        try
        {
            results = await ProvideInlineCompletions(model, position, context, cts.Token).ConfigureAwait(true);
        }
        catch (Exception err)
        {
            if (err is not OperationCanceledException)
            {
                UnexpectedErrors.Report(err);
            }

            if (ReferenceEquals(_requestCts, cts))
            {
                _requestCts = null;
            }

            return;
        }

        if (cts.IsCancellationRequested || _disposed || model.GetVersionId() != versionId)
        {
            foreach (var (provider, list) in results)
            {
                provider.DisposeInlineCompletions(list, InlineCompletionsDisposeReasonKind.LostRace);
            }

            return;
        }

        _requestCts = null;
        _lastRequest = (versionId, position, explicitRequest);
        var items = new List<(IInlineCompletionsProvider, InlineCompletions, InlineCompletion, TextReplacement)>();
        var defaultRange = GetDefaultRange(position, model);
        foreach (var (provider, list) in results)
        {
            foreach (var completion in list.Items)
            {
                if (completion.InsertText is not { } text || completion.IsSnippet || completion.IsInlineEdit)
                {
                    continue;
                }

                items.Add((provider, list, completion, new TextReplacement(completion.Range ?? defaultRange, text)));
            }
        }

        SetItems(items, results, InlineCompletionsDisposeReasonKind.NotTaken);
        UpdateState();
    }

    private async Task<List<(IInlineCompletionsProvider Provider, InlineCompletions List)>> ProvideInlineCompletions(
        TextModel model, TextPosition position, InlineCompletionContext context, CancellationToken token)
    {
        var providers = _registry.All(model);
        var results = new List<(IInlineCompletionsProvider, InlineCompletions)>();
        var answeredGroups = new HashSet<string>(StringComparer.Ordinal);

        // providers that yield to others are asked after those (upstream resolves the yieldsTo graph)
        var ordered = providers.OrderBy(p => p.YieldsToGroupIds is { Count: > 0 } ? 1 : 0).ToList();
        foreach (var provider in ordered)
        {
            if (provider.YieldsToGroupIds is { } yieldsTo && yieldsTo.Any(answeredGroups.Contains))
            {
                continue;
            }

            InlineCompletions? list;
            try
            {
                list = await provider.ProvideInlineCompletions(model, position, context, token).ConfigureAwait(true);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception err)
            {
                UnexpectedErrors.Report(err);
                continue;
            }

            token.ThrowIfCancellationRequested();
            if (list is null)
            {
                continue;
            }

            results.Add((provider, list));
            if (list.Items.Count > 0 && provider.GroupId is { } groupId)
            {
                answeredGroups.Add(groupId);
            }
        }

        return results;
    }

    private static TextRange GetDefaultRange(TextPosition position, TextModel model)
    {
        var maxColumn = model.GetLineMaxColumn(position.LineNumber);
        var word = WordHelper.GetWordAtText(position.Column, WordHelper.DefaultWordRegExp, model.GetLineContent(position.LineNumber), 0);
        // By default, always replace up until the end of the current line. This default might be subject to change!
        return word is { } w
            ? new TextRange(position.LineNumber, w.StartColumn, position.LineNumber, maxColumn)
            : new TextRange(position, position with { Column = maxColumn });
    }

    private void SetItems(
        IReadOnlyList<(IInlineCompletionsProvider Provider, InlineCompletions List, InlineCompletion Completion, TextReplacement Edit)> items,
        List<(IInlineCompletionsProvider Provider, InlineCompletions List)> lists,
        InlineCompletionsDisposeReasonKind reason)
    {
        var oldIds = _items.Select(static i => i.DecorationId).ToArray();
        var newIds = TextModel.DeltaDecorations(oldIds, items.Select(i => new ModelDeltaDecoration(i.Edit.Range, s_rangeOptions)).ToArray());
        var oldLists = _lists;
        _items = items.Select((item, index) => new InlineCompletionItem(item.Provider, item.List, item.Completion, item.Edit, newIds[index])).ToList();
        _lists = lists;
        foreach (var (provider, list) in oldLists)
        {
            provider.DisposeInlineCompletions(list, reason);
        }
    }

    // ---- state

    private TextPosition GetPrimaryPosition() => _editor.GetSelections() is { Count: > 0 } selections ? selections[0].Position : new TextPosition(1, 1);

    private List<InlineCompletionItem> GetVisibleItems()
    {
        var model = TextModel;
        var cursor = GetPrimaryPosition();
        var result = new List<InlineCompletionItem>();
        foreach (var item in _items)
        {
            if (GhostTextComputer.InlineCompletionIsVisible(item.GetSingleTextEdit(model), item.OriginalEdit.Range, model, cursor)
                && result.All(r => r.SemanticId != item.SemanticId))
            {
                result.Add(item);
            }
        }

        return result;
    }

    private InlineCompletionItem? GetSelectedInlineCompletion()
    {
        var visible = GetVisibleItems();
        if (visible.Count == 0)
        {
            return null;
        }

        return visible.FirstOrDefault(i => i.SemanticId == _selectedId) ?? visible[0];
    }

    private InlineCompletionsState? ComputeState()
    {
        if (!_isActive)
        {
            return null;
        }

        var inlineSuggestion = GetSelectedInlineCompletion();
        if (inlineSuggestion is null)
        {
            return null;
        }

        var model = TextModel;
        var replacement = inlineSuggestion.GetSingleTextEdit(model);
        var positions = _editor.GetSelections().Select(static s => s.Position).ToList();
        if (positions.Count == 0)
        {
            return null;
        }

        var allPotentialEdits = new List<TextReplacement> { replacement };
        allPotentialEdits.AddRange(GhostTextComputer.GetSecondaryEdits(model, positions, replacement));
        var edits = new List<TextReplacement>();
        var ghostTexts = new List<GhostText>();
        for (var idx = 0; idx < allPotentialEdits.Count && idx < positions.Count; idx++)
        {
            var edit = allPotentialEdits[idx];
            if (GhostTextComputer.ComputeGhostText(edit, model, Mode, positions[idx]) is { } ghostText)
            {
                edits.Add(edit);
                ghostTexts.Add(ghostText);
            }
        }

        if (ghostTexts.Count == 0)
        {
            return null;
        }

        return new InlineCompletionsState(edits, ghostTexts[0], ghostTexts, inlineSuggestion);
    }

    /// <summary>Recomputes the state; raises <see cref="DidChangeState"/> when it changed.</summary>
    public void UpdateState()
    {
        var state = ComputeState();
        var previous = _state;
        _state = state;
        if (!StatesEqual(previous, state))
        {
            DidChangeState?.Invoke();
        }
    }

    private static bool StatesEqual(InlineCompletionsState? a, InlineCompletionsState? b)
    {
        if (a is null || b is null)
        {
            return a is null && b is null;
        }

        return ReferenceEquals(a.InlineSuggestion, b.InlineSuggestion)
            && a.GhostTexts.Count == b.GhostTexts.Count
            && a.GhostTexts.Zip(b.GhostTexts).All(static p => p.First.Equals(p.Second));
    }

    private void OnDidChangeContent(ModelContentChangedEvent e)
    {
        if (_isAcceptingPartially)
        {
            return;
        }

        if (Enabled)
        {
            // upstream's automatic trigger on typing
            _ = Trigger();
        }
        else
        {
            CancelRequest();
        }
    }

    /// <summary>The cursors moved (the editor tells the model, as upstream's model reads the positions).</summary>
    public void HandleCursorChanged() => UpdateState();

    private void OnRegistryChanged(int _)
    {
        BindProviderListeners();
    }

    private void BindProviderListeners()
    {
        foreach (var (provider, handler) in _providerListeners)
        {
            provider.DidChangeInlineCompletions -= handler;
        }

        _providerListeners.Clear();
        foreach (var provider in _registry.AllNoModel())
        {
            Action handler = () =>
            {
                if (_isActive)
                {
                    _lastRequest = null;
                    _ = Trigger(noDelay: false);
                }
            };
            provider.DidChangeInlineCompletions += handler;
            _providerListeners.Add((provider, handler));
        }
    }

    // ---- accepting

    /// <summary>Inserts the selected inline completion (Tab).</summary>
    public void Accept()
    {
        var state = _state;
        if (state is null || state.PrimaryGhostText.IsEmpty())
        {
            return;
        }

        var completion = state.InlineSuggestion;
        var model = TextModel;
        _editor.PushUndoStop();

        // The cursor should move to the end of the edit, not the end of the range provided by the extension
        var minimalEdits = RemoveTextReplacementCommonSuffixPrefix(state.Edits, model);
        var selections = GetEndPositionsAfterApplying(minimalEdits).Select(static p => new TextSelection(p.LineNumber, p.Column, p.LineNumber, p.Column)).ToList();

        var operations = state.Edits.Select(static e => e.ToSingleEditOperation()).ToList();
        if (completion.SourceCompletion.AdditionalTextEdits is { } additional)
        {
            operations.AddRange(additional);
        }

        _editor.ExecuteEdits("inlineCompletionAccept", operations, selections);
        DidAccept?.Invoke();

        // Reset before invoking the command, as the command might cause a follow up trigger (which we don't want to reset).
        Stop();

        if (completion.SourceCompletion.Command is { } command)
        {
            try
            {
                _editor.ExecuteCommand(command);
            }
            catch (Exception err)
            {
                UnexpectedErrors.Report(err);
            }
        }
    }

    /// <summary>Accepts the next word of the ghost text (Ctrl+Right).</summary>
    public void AcceptNextWord() => AcceptNext(PartialAcceptTriggerKind.Word, (pos, text) =>
    {
        var wordRegExp = _editor.GetWordDefinition(pos);
        var m1 = wordRegExp.Match(text);
        int acceptUntilIndexExclusive;
        if (m1.Success)
        {
            acceptUntilIndexExclusive = m1.Index == 0 ? m1.Length : m1.Index;
        }
        else
        {
            acceptUntilIndexExclusive = text.Length;
        }

        var m2 = Regex.Match(text, @"\s+");
        if (m2.Success && m2.Index + m2.Length < acceptUntilIndexExclusive)
        {
            acceptUntilIndexExclusive = m2.Index + m2.Length;
        }

        return acceptUntilIndexExclusive;
    });

    /// <summary>Accepts the ghost text up to and including the next line break.</summary>
    public void AcceptNextLine() => AcceptNext(PartialAcceptTriggerKind.Line, static (_, text) =>
    {
        var index = text.IndexOf('\n', StringComparison.Ordinal);
        return index >= 0 ? index + 1 : text.Length;
    });

    private void AcceptNext(PartialAcceptTriggerKind kind, Func<TextPosition, string, int> getAcceptUntilIndex)
    {
        var state = _state;
        if (state is null || state.PrimaryGhostText.IsEmpty())
        {
            return;
        }

        var ghostText = state.PrimaryGhostText;
        var completion = state.InlineSuggestion;
        var firstPart = ghostText.Parts[0];
        var ghostTextPos = new TextPosition(ghostText.LineNumber, firstPart.Column);
        var ghostTextVal = firstPart.Text;
        var acceptUntilIndexExclusive = getAcceptUntilIndex(ghostTextPos, ghostTextVal);
        if (acceptUntilIndexExclusive == ghostTextVal.Length && ghostText.Parts.Count == 1)
        {
            Accept();
            return;
        }

        var partialGhostTextVal = ghostTextVal[..acceptUntilIndexExclusive];
        var model = TextModel;
        var positions = _editor.GetSelections().Select(static s => s.Position).ToList();
        var cursorPosition = positions[0];

        _isAcceptingPartially = true;
        try
        {
            _editor.PushUndoStop();
            var replaceRange = new TextRange(cursorPosition, ghostTextPos);
            var newText = model.GetValueInRange(replaceRange) + partialGhostTextVal;
            var primaryEdit = new TextReplacement(replaceRange, newText);
            var edits = new List<TextReplacement> { primaryEdit };
            edits.AddRange(GhostTextComputer.GetSecondaryEdits(model, positions, primaryEdit));
            var selections = GetEndPositionsAfterApplying(edits).Select(static p => new TextSelection(p.LineNumber, p.Column, p.LineNumber, p.Column)).ToList();
            _editor.ExecuteEdits("inlineCompletionPartialAccept", edits.Select(static e => e.ToSingleEditOperation()).ToList(), selections);
        }
        finally
        {
            _isAcceptingPartially = false;
        }

        var acceptedEnd = TextHelpers.AddTextToPosition(partialGhostTextVal, ghostTextPos);
        var acceptedRange = new TextRange(completion.OriginalEdit.Range.Start, acceptedEnd);
        var acceptedLength = model.GetValueInRange(acceptedRange, EndOfLinePreference.LF).Length;
        completion.Provider.HandlePartialAccept(completion.List, completion.SourceCompletion, acceptedLength, new PartialAcceptInfo(kind, acceptedLength));
        UpdateState();
    }

    /// <summary>Upstream's removeTextReplacementCommonSuffixPrefix (utils.ts).</summary>
    internal static List<TextReplacement> RemoveTextReplacementCommonSuffixPrefix(IReadOnlyList<TextReplacement> edits, TextModel textModel) =>
        edits.Select(e => e.RemoveCommonSuffix(textModel).RemoveCommonPrefix(textModel)).ToList();

    /// <summary>Upstream's getEndPositionsAfterApplying (utils.ts): where each replacement ends once all are applied.</summary>
    internal static List<TextPosition> GetEndPositionsAfterApplying(IReadOnlyList<TextReplacement> edits)
    {
        var order = Enumerable.Range(0, edits.Count).OrderBy(i => edits[i].Range.Start).ToList();
        var result = new TextPosition[edits.Count];
        var lineDelta = 0;
        var lastOriginalEndLine = -1;
        var lastOriginalEndColumn = 0;
        var lastNewEnd = new TextPosition(1, 1);
        foreach (var i in order)
        {
            var range = edits[i].Range;
            var startLine = range.StartLineNumber + lineDelta;
            var startColumn = range.StartLineNumber == lastOriginalEndLine
                ? lastNewEnd.Column + (range.StartColumn - lastOriginalEndColumn)
                : range.StartColumn;
            var newStart = new TextPosition(startLine, startColumn);
            var newEnd = TextHelpers.AddTextToPosition(edits[i].Text, newStart);
            result[i] = newEnd;

            lineDelta += (newEnd.LineNumber - newStart.LineNumber) - (range.EndLineNumber - range.StartLineNumber);
            lastOriginalEndLine = range.EndLineNumber;
            lastOriginalEndColumn = range.EndColumn;
            lastNewEnd = newEnd;
        }

        return [.. result];
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        Stop();
        _disposed = true;
        TextModel.DidChangeContent -= OnDidChangeContent;
        _registry.DidChange -= OnRegistryChanged;
        foreach (var (provider, handler) in _providerListeners)
        {
            provider.DidChangeInlineCompletions -= handler;
        }

        _providerListeners.Clear();
    }
}
