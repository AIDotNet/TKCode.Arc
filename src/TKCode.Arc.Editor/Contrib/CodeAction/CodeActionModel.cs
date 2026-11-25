// Ported from vscode (src/vs/editor/contrib/codeAction/browser/codeActionModel.ts, commit b57d5c9e), MIT license.
// Differences from upstream:
//  - the editor is an ICodeActionEditor (model, selection, read-only, the lightbulb option and the events the model
//    listens to), so that the model is pure .NET; the control adapts CodeEditor to it;
//  - timers go through the editor's IdleScheduler (RunAfter): the 250 ms auto trigger and the 500 ms delay of a manual
//    to automatic transition, so that tests can drive time;
//  - a cancelable promise is a Task with a CancellationTokenSource; Triggered.Actions completes with the empty set when
//    the request was cancelled, as upstream;
//  - the editor.codeActionWidget.includeNearbyQuickFixes setting is ICodeActionEditor.IncludeNearbyQuickFixes;
//  - the progress service is not ported.

using TKCode.Arc.Editor.Languages;
using TKCode.Arc.Editor.Model;
using TKCode.Arc.Editor.Platform;
using TKCode.Arc.Editor.Platform.Markers;

namespace TKCode.Arc.Editor.Contrib.CodeAction;

/// <summary>When the lightbulb shows (upstream's ShowLightbulbIconMode).</summary>
internal enum ShowLightbulbIconMode
{
    Off,
    OnCode,
    On,
}

/// <summary>What the code action model needs from an editor.</summary>
internal interface ICodeActionEditor
{
    /// <summary>The model, or null when the editor has none.</summary>
    TextModel? Model { get; }

    /// <summary>The primary selection, or null without a model.</summary>
    TextSelection? GetSelection();

    bool IsReadOnly { get; }

    /// <summary>Upstream's lightbulb.enabled option.</summary>
    ShowLightbulbIconMode Lightbulb { get; }

    /// <summary>Upstream's editor.codeActionWidget.includeNearbyQuickFixes setting.</summary>
    bool IncludeNearbyQuickFixes { get; }

    /// <summary>Runs timers on the editor's thread.</summary>
    IdleScheduler Scheduler { get; }

    event Action? DidChangeCursorPosition;

    event Action? DidChangeModel;

    event Action? DidChangeModelLanguage;

    /// <summary>The lightbulb or read-only option changed.</summary>
    event Action? DidChangeConfiguration;
}

/// <summary>A selection and the trigger that asks for its code actions.</summary>
internal sealed record TriggeredCodeAction(TextSelection Selection, CodeActionTrigger Trigger);

/// <summary>Triggers code action requests when the cursor or the markers change (upstream's CodeActionOracle).</summary>
internal sealed class CodeActionOracle : IDisposable
{
    private readonly ICodeActionEditor _editor;
    private readonly MarkerService _markerService;
    private readonly Action<TriggeredCodeAction?> _signalChange;
    private readonly TimeSpan _delay;
    private IDisposable? _autoTriggerTimer;
    private bool _disposed;

    public CodeActionOracle(ICodeActionEditor editor, MarkerService markerService, Action<TriggeredCodeAction?> signalChange, int delay = 250)
    {
        _editor = editor;
        _markerService = markerService;
        _signalChange = signalChange;
        _delay = TimeSpan.FromMilliseconds(delay);
        _markerService.MarkerChanged += OnMarkerChanges;
        _editor.DidChangeCursorPosition += TryAutoTrigger;
    }

    public bool IgnoreLightbulbOff { get; set; }

    public void Trigger(CodeActionTrigger trigger)
    {
        var selection = GetRangeOfSelectionUnlessWhitespaceEnclosed(trigger);
        _signalChange(selection is { } s ? new TriggeredCodeAction(s, trigger) : null);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _autoTriggerTimer?.Dispose();
        _markerService.MarkerChanged -= OnMarkerChanges;
        _editor.DidChangeCursorPosition -= TryAutoTrigger;
    }

    private void OnMarkerChanges(IReadOnlyList<Uri> resources)
    {
        if (_editor.Model is { } model && resources.Any(resource => resource == model.Uri))
        {
            TryAutoTrigger();
        }
    }

    private void TryAutoTrigger()
    {
        _autoTriggerTimer?.Dispose();
        _autoTriggerTimer = _editor.Scheduler.RunAfter(_delay, () =>
        {
            _autoTriggerTimer = null;
            if (!_disposed)
            {
                Trigger(new CodeActionTrigger(CodeActionTriggerType.Auto, CodeActionTriggerSource.Default));
            }
        });
    }

    private TextSelection? GetRangeOfSelectionUnlessWhitespaceEnclosed(CodeActionTrigger trigger)
    {
        if (_editor.Model is not { } model || _editor.GetSelection() is not { } selection)
        {
            return null;
        }

        if (trigger.Type == CodeActionTriggerType.Invoke)
        {
            return selection;
        }

        var enabled = _editor.Lightbulb;
        if (enabled == ShowLightbulbIconMode.Off && !IgnoreLightbulbOff)
        {
            return null;
        }

        if (enabled is ShowLightbulbIconMode.Off or ShowLightbulbIconMode.On)
        {
            return selection;
        }

        // OnCode
        if (!selection.IsEmpty)
        {
            return selection;
        }

        var (lineNumber, column) = selection.Position;
        var line = model.GetLineContent(lineNumber);
        if (line.Length == 0)
        {
            // empty line
            return null;
        }

        if (column == 1)
        {
            // look only right
            if (char.IsWhiteSpace(line[0]))
            {
                return null;
            }
        }
        else if (column == model.GetLineMaxColumn(lineNumber))
        {
            // look only left
            if (char.IsWhiteSpace(line[^1]))
            {
                return null;
            }
        }
        else if (char.IsWhiteSpace(line[column - 2]) && char.IsWhiteSpace(line[column - 1]))
        {
            // look left and right
            return null;
        }

        return selection;
    }
}

/// <summary>The state of a code action model (upstream's CodeActionsState).</summary>
internal abstract class CodeActionsState
{
    private CodeActionsState()
    {
    }

    /// <summary>No request (upstream's CodeActionsState.Empty).</summary>
    public static CodeActionsState Empty { get; } = new EmptyState();

    /// <summary>A request is running or done (upstream's CodeActionsState.Triggered).</summary>
    internal sealed class Triggered : CodeActionsState
    {
        private readonly CancellationTokenSource _cts;

        public Triggered(CodeActionTrigger trigger, TextPosition position, Task<CodeActionSet> actions, CancellationTokenSource cts)
        {
            Trigger = trigger;
            Position = position;
            _cts = cts;
            Actions = CatchCancellation(actions);
        }

        public CodeActionTrigger Trigger { get; }

        public TextPosition Position { get; }

        /// <summary>The actions; the empty set when the request was cancelled.</summary>
        public Task<CodeActionSet> Actions { get; }

        public void Cancel()
        {
            try
            {
                _cts.Cancel();
            }
            catch (ObjectDisposedException)
            {
                // finished
            }
        }

        private static async Task<CodeActionSet> CatchCancellation(Task<CodeActionSet> actions)
        {
            try
            {
                return await actions.ConfigureAwait(true);
            }
            catch (OperationCanceledException)
            {
                return CodeActionSet.Empty;
            }
        }
    }

    private sealed class EmptyState : CodeActionsState
    {
    }
}

/// <summary>
/// Asks for the code actions at the cursor when it moves or the markers change, and when asked to
/// (upstream's CodeActionModel). Owns the actions of the current request until a caller takes them.
/// </summary>
internal sealed class CodeActionModel : IDisposable
{
    /// <summary>The kinds the providers of the model say they provide, space separated (upstream's SUPPORTED_CODE_ACTIONS).</summary>
    public static readonly RawContextKey<string> SupportedCodeActions = new("supportedCodeAction", string.Empty);

    public const string ApplyFixAllCommandId = "_typescript.applyFixAllCodeAction";

    private readonly ICodeActionEditor _editor;
    private readonly LanguageFeatureRegistry<ICodeActionProvider> _registry;
    private readonly MarkerService _markerService;
    private readonly IContextKey<string>? _supportedCodeActions;
    private readonly Emitter<CodeActionsState> _onDidChangeState = new();
    private CodeActionOracle? _codeActionOracle;
    private CodeActionsState _state = CodeActionsState.Empty;
    private CodeActionSet? _codeActions;
    private IDisposable? _deferredStateTimer;
    private bool _disposed;
    private bool _ignoreLightbulbOff;

    public CodeActionModel(
        ICodeActionEditor editor,
        LanguageFeatureRegistry<ICodeActionProvider> registry,
        MarkerService markerService,
        IContextKey<string>? supportedCodeActions)
    {
        _editor = editor;
        _registry = registry;
        _markerService = markerService;
        _supportedCodeActions = supportedCodeActions;

        _editor.DidChangeModel += Update;
        _editor.DidChangeModelLanguage += Update;
        _editor.DidChangeConfiguration += Update;
        _registry.DidChange += OnRegistryChanged;
        Update();
    }

    /// <summary>Raised when a request starts (Triggered) or there is none (Empty).</summary>
    public event Action<CodeActionsState>? DidChangeState
    {
        add => _onDidChangeState.Add(value!);
        remove => _onDidChangeState.Remove(value);
    }

    /// <summary>The current state.</summary>
    public CodeActionsState State => _state;

    /// <summary>Auto triggers also with the lightbulb off (while something shows the lightbulb's state).</summary>
    public bool IgnoreLightbulbOff
    {
        get => _ignoreLightbulbOff;
        set
        {
            if (_ignoreLightbulbOff == value)
            {
                return;
            }

            _ignoreLightbulbOff = value;
            if (_codeActionOracle is { } oracle)
            {
                oracle.IgnoreLightbulbOff = value;
                if (value)
                {
                    oracle.Trigger(new CodeActionTrigger(CodeActionTriggerType.Auto, CodeActionTriggerSource.Default));
                }
            }
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _editor.DidChangeModel -= Update;
        _editor.DidChangeModelLanguage -= Update;
        _editor.DidChangeConfiguration -= Update;
        _registry.DidChange -= OnRegistryChanged;
        _deferredStateTimer?.Dispose();
        _codeActionOracle?.Dispose();
        _codeActionOracle = null;
        ClearCodeActions();
        SetState(CodeActionsState.Empty, skipNotify: true);
    }

    /// <summary>Asks for code actions now (upstream's trigger).</summary>
    public void Trigger(CodeActionTrigger trigger)
    {
        // upstream clears after triggering, which in JavaScript is before the request can set new actions (it awaits at
        // least one microtask); a request here can finish synchronously, so the old actions go first
        ClearCodeActions();
        _codeActionOracle?.Trigger(trigger);
    }

    /// <summary>Hands the current actions to the caller, who disposes them; null when <paramref name="actions"/> are not the current ones.</summary>
    public CodeActionSet? TakeCodeActions(CodeActionSet actions)
    {
        if (!ReferenceEquals(_codeActions, actions))
        {
            return null;
        }

        _codeActions = null;
        return actions;
    }

    private void OnRegistryChanged(int count) => Update();

    private void Update()
    {
        if (_disposed)
        {
            return;
        }

        _codeActionOracle?.Dispose();
        _codeActionOracle = null;
        SetState(CodeActionsState.Empty);
        ClearCodeActions();

        if (_editor.Model is { } model && _registry.Has(model) && !_editor.IsReadOnly)
        {
            var supportedActions = _registry.All(model).SelectMany(provider => provider.ProvidedCodeActionKinds ?? []);
            _supportedCodeActions?.Set(string.Join(' ', supportedActions));

            var oracle = new CodeActionOracle(_editor, _markerService, trigger => OnTrigger(model, trigger))
            {
                IgnoreLightbulbOff = _ignoreLightbulbOff,
            };
            _codeActionOracle = oracle;
            oracle.Trigger(new CodeActionTrigger(CodeActionTriggerType.Auto, CodeActionTriggerSource.Default));
        }
        else
        {
            _supportedCodeActions?.Reset();
        }
    }

    private void OnTrigger(TextModel model, TriggeredCodeAction? trigger)
    {
        if (trigger is null)
        {
            SetState(CodeActionsState.Empty);
            return;
        }

        var startPosition = trigger.Selection.Range.Start;
        var cts = new CancellationTokenSource();
        var actions = ComputeActions(model, trigger, cts.Token);
        var newState = new CodeActionsState.Triggered(trigger.Trigger, startPosition, actions, cts);

        // a manual request is not replaced by an automatic one at another position until 500 ms later
        var isManualToAutoTransition = _state is CodeActionsState.Triggered current
            && current.Trigger.Type == CodeActionTriggerType.Invoke
            && newState.Trigger.Type == CodeActionTriggerType.Auto
            && current.Position != newState.Position;

        if (!isManualToAutoTransition)
        {
            SetState(newState);
        }
        else
        {
            _deferredStateTimer?.Dispose();
            _deferredStateTimer = _editor.Scheduler.RunAfter(TimeSpan.FromMilliseconds(500), () => SetState(newState));
        }
    }

    private async Task<CodeActionSet> ComputeActions(TextModel model, TriggeredCodeAction trigger, CancellationToken token)
    {
        if (_editor.IncludeNearbyQuickFixes
            && trigger.Trigger.Type == CodeActionTriggerType.Invoke
            && (trigger.Trigger.TriggerAction == CodeActionTriggerSource.QuickFix
                || trigger.Trigger.Filter?.Include?.Contains(CodeActionKind.QuickFix) == true))
        {
            if (await ComputeNearbyQuickFixes(model, trigger, token).ConfigureAwait(true) is { } nearby)
            {
                return nearby;
            }
        }

        // manual triggers (source actions and refactors) and automatic ones
        var codeActions = await CodeActionService.GetCodeActions(_registry, model, trigger.Selection.Range, trigger.Trigger, token).ConfigureAwait(true);
        if (token.IsCancellationRequested)
        {
            codeActions.Dispose();
            return CodeActionSet.Empty;
        }

        return SetCodeActions(codeActions);
    }

    // The quick fixes at the cursor, and when there are none, those of the markers on the cursor's line, nearest first.
    // Null: fall back to the plain request.
    private async Task<CodeActionSet?> ComputeNearbyQuickFixes(TextModel model, TriggeredCodeAction trigger, CancellationToken token)
    {
        var codeActionSets = new List<CodeActionSet>();
        var ownedByResult = false;
        try
        {
            var codeActionSet = await CodeActionService.GetCodeActions(_registry, model, trigger.Selection.Range, trigger.Trigger, token).ConfigureAwait(true);
            codeActionSets.Add(codeActionSet);
            var allCodeActions = new List<CodeActionItem>(codeActionSet.AllActions);
            if (token.IsCancellationRequested)
            {
                return CodeActionSet.Empty;
            }

            // non-AI quick fixes in the set: if AI actions are all there is, look for the diagnostics on the line
            var foundQuickfix = codeActionSet.ValidActions.Any(action =>
                action.Action.Kind is { } kind && CodeActionKind.QuickFix.Contains(new HierarchicalKind(kind)) && !action.Action.IsAI);
            var allMarkers = _markerService.Read(new MarkerReadOptions { Resource = model.Uri });
            if (foundQuickfix)
            {
                var result = SetCodeActions(new CodeActionSet(
                    WithFixAllDiagnostics(codeActionSet.ValidActions, allMarkers), allCodeActions, codeActionSet.Documentation,
                    () => DisposeAll(codeActionSets)));
                ownedByResult = true;
                return result;
            }

            if (allMarkers.Count == 0)
            {
                return null;
            }

            var currPosition = trigger.Selection.Position;
            var distance = int.MaxValue;
            var currentActions = new List<CodeActionItem>(codeActionSet.ValidActions);
            foreach (var marker in allMarkers)
            {
                var col = marker.EndColumn;
                var row = marker.EndLineNumber;
                var startRow = marker.StartLineNumber;

                // a quick fix on the same line: check the distance to the other markers
                if (row != currPosition.LineNumber && startRow != currPosition.LineNumber)
                {
                    continue;
                }

                var trackedPosition = new TextPosition(row, col);
                var newTrigger = new CodeActionTrigger(trigger.Trigger.Type, trigger.Trigger.TriggerAction)
                {
                    Filter = new CodeActionFilter { Include = trigger.Trigger.Filter?.Include ?? CodeActionKind.QuickFix },
                    AutoApply = trigger.Trigger.AutoApply,
                    Context = new CodeActionTriggerContext(trigger.Trigger.Context?.NotAvailableMessage ?? string.Empty, trackedPosition),
                };

                var selectionAsPosition = new TextRange(trackedPosition);
                var actionsAtMarker = await CodeActionService.GetCodeActions(_registry, model, selectionAsPosition, newTrigger, token).ConfigureAwait(true);
                codeActionSets.Add(actionsAtMarker);
                if (token.IsCancellationRequested)
                {
                    return CodeActionSet.Empty;
                }

                if (actionsAtMarker.ValidActions.Count != 0)
                {
                    var atMarker = WithFixAllDiagnostics(actionsAtMarker.ValidActions, allMarkers);
                    if (codeActionSet.AllActions.Count == 0)
                    {
                        allCodeActions.AddRange(actionsAtMarker.AllActions);
                    }

                    // already only quick fixes
                    if (Math.Abs(currPosition.Column - col) < distance)
                    {
                        currentActions.InsertRange(0, atMarker);
                    }
                    else
                    {
                        currentActions.AddRange(atMarker);
                    }
                }

                distance = Math.Abs(currPosition.Column - col);
            }

            var filteredActions = currentActions
                .Where((action, index) => currentActions.FindIndex(a => a.Action.Title == action.Action.Title) == index)
                .Order(Comparer<CodeActionItem>.Create(static (a, b) =>
                {
                    if (a.Action.IsPreferred && !b.Action.IsPreferred)
                    {
                        return -1;
                    }

                    if (!a.Action.IsPreferred && b.Action.IsPreferred)
                    {
                        return 1;
                    }

                    if (a.Action.IsAI && !b.Action.IsAI)
                    {
                        return 1;
                    }

                    if (!a.Action.IsAI && b.Action.IsAI)
                    {
                        return -1;
                    }

                    return 0;
                }))
                .ToList();

            // only replaces the actions when a quick fix was found on the cursor's line
            var nearby = SetCodeActions(new CodeActionSet(filteredActions, allCodeActions, codeActionSet.Documentation, () => DisposeAll(codeActionSets)));
            ownedByResult = true;
            return nearby;
        }
        finally
        {
            if (!ownedByResult)
            {
                DisposeAll(codeActionSets);
            }
        }
    }

    // TypeScript's fix-all action carries the diagnostics with related information
    private static IReadOnlyList<CodeActionItem> WithFixAllDiagnostics(IReadOnlyList<CodeActionItem> actions, IReadOnlyList<Marker> allMarkers)
    {
        foreach (var action in actions)
        {
            if (action.Action.Command?.Arguments?.Any(arg => arg is string s && s.Contains(ApplyFixAllCommandId, StringComparison.Ordinal)) == true)
            {
                var diagnostics = allMarkers.Where(marker => marker.RelatedInformation is not null).Select(ToMarkerData).ToList();
                action.ReplaceAction(action.Action with { Diagnostics = diagnostics });
            }
        }

        return actions;
    }

    private static MarkerData ToMarkerData(Marker marker) => new()
    {
        Code = marker.Code,
        Severity = marker.Severity,
        Message = marker.Message,
        Source = marker.Source,
        StartLineNumber = marker.StartLineNumber,
        StartColumn = marker.StartColumn,
        EndLineNumber = marker.EndLineNumber,
        EndColumn = marker.EndColumn,
        RelatedInformation = marker.RelatedInformation,
        Tags = marker.Tags,
    };

    private static void DisposeAll(List<CodeActionSet> sets)
    {
        foreach (var set in sets)
        {
            set.Dispose();
        }
    }

    private CodeActionSet SetCodeActions(CodeActionSet actions)
    {
        if (_disposed)
        {
            // nobody owns them any more
            actions.Dispose();
            return actions;
        }

        if (!ReferenceEquals(_codeActions, actions))
        {
            _codeActions?.Dispose();
        }

        _codeActions = actions;
        return actions;
    }

    private void ClearCodeActions()
    {
        _codeActions?.Dispose();
        _codeActions = null;
    }

    private void SetState(CodeActionsState newState, bool skipNotify = false)
    {
        if (ReferenceEquals(newState, _state))
        {
            return;
        }

        // cancel the old request
        if (_state is CodeActionsState.Triggered triggered)
        {
            triggered.Cancel();
        }

        _state = newState;
        if (!skipNotify && !_disposed)
        {
            _onDidChangeState.Fire(newState);
        }
    }
}
