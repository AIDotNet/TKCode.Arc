// Ported from vscode (src/vs/editor/contrib/parameterHints/browser/parameterHintsModel.ts and provideSignatureHelp.ts,
// commit b57d5c9e), MIT license.
// Differences from upstream:
//  - the editor is the small IParameterHintsEditor (its model, position, options and the events the model listens to)
//    instead of ICodeEditor; the timers run on an IdleScheduler;
//  - a pending request is a task with a CancellationTokenSource (upstream's CancelablePromise): cancelling it ends the
//    await at once with OperationCanceledException, whatever the provider does with the token;
//  - the trigger and retrigger characters are sets of UTF-16 code units (upstream's CharacterSet);
//  - the _executeSignatureHelpProvider command is not registered (there is no process-wide command registry);
//  - provider errors go to UnexpectedErrors.Report; cancellation is not one.

using TKCode.Arc.Editor.Contrib.Hover;
using TKCode.Arc.Editor.Languages;
using TKCode.Arc.Editor.Model;

namespace TKCode.Arc.Editor.Contrib.ParameterHints;

/// <summary>Upstream's <c>editor.parameterHints</c> options.</summary>
internal sealed record ParameterHintsOptions
{
    public static ParameterHintsOptions Default { get; } = new();

    /// <summary>Whether typing a trigger character shows parameter hints.</summary>
    public bool Enabled { get; init; } = true;

    /// <summary>Whether next / previous wrap around the signatures.</summary>
    public bool Cycle { get; init; } = true;
}

/// <summary>What a parameter hints model listens to (upstream's ICodeEditor, reduced).</summary>
internal interface IParameterHintsEditor
{
    /// <summary>The model, or null.</summary>
    TextModel? Model { get; }

    /// <summary>The primary cursor's position, or null without a model.</summary>
    TextPosition? GetPosition();

    ParameterHintsOptions ParameterHintsOptions { get; }

    /// <summary>The signature help providers.</summary>
    LanguageFeatureRegistry<ISignatureHelpProvider> SignatureHelpProviders { get; }

    /// <summary>The editor lost focus (upstream's onDidBlurEditorWidget).</summary>
    event Action? DidBlurEditorWidget;

    /// <summary>The options changed (upstream's onDidChangeConfiguration).</summary>
    event Action? DidChangeConfiguration;

    /// <summary>The model or its language changed (upstream's onDidChangeModel and onDidChangeModelLanguage).</summary>
    event Action? DidChangeModel;

    /// <summary>The cursor moved; the argument is the source ("mouse", "keyboard", ...).</summary>
    event Action<string>? DidChangeCursorSelection;

    /// <summary>The text changed.</summary>
    event Action? DidChangeModelContent;

    /// <summary>Text was typed (upstream's onDidType).</summary>
    event Action<string>? DidType;
}

/// <summary>What triggered parameter hints (upstream's TriggerContext).</summary>
internal sealed record ParameterHintsTriggerContext(SignatureHelpTriggerKind TriggerKind, string? TriggerCharacter = null);

/// <summary>Asks the signature help providers, best first, for the first result (upstream's provideSignatureHelp).</summary>
internal static class ProvideSignatureHelpCommand
{
    public static async Task<SignatureHelpResult?> ProvideSignatureHelp(
        LanguageFeatureRegistry<ISignatureHelpProvider> registry,
        TextModel model,
        TextPosition position,
        SignatureHelpContext context,
        CancellationToken token)
    {
        foreach (var support in registry.Ordered(model))
        {
            try
            {
                var result = await support.ProvideSignatureHelp(model, position, token, context).ConfigureAwait(true);
                if (result is not null)
                {
                    return result;
                }
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception e)
            {
                UnexpectedErrors.Report(e);
            }
        }

        return null;
    }
}

/// <summary>
/// The state of parameter hints: triggers on trigger characters, retriggers on retrigger characters, cursor moves and
/// edits while active, and cycles the signatures (upstream's ParameterHintsModel).
/// </summary>
internal sealed class ParameterHintsModel : IDisposable
{
    public const int DefaultDelay = 120; // ms

    private readonly IParameterHintsEditor _editor;
    private readonly LanguageFeatureRegistry<ISignatureHelpProvider> _providers;
    private readonly Delayer<bool> _throttledDelayer;
    private readonly HashSet<char> _triggerChars = [];
    private readonly HashSet<char> _retriggerChars = [];
    private readonly List<ParameterHintsTriggerContext> _pendingTriggers = [];

    private bool _triggerOnType;
    private State _state = State.Default;
    private SignatureHelpResult? _lastSignatureHelpResult;
    private int _triggerId;
    private bool _disposed;

    public ParameterHintsModel(IParameterHintsEditor editor, IdleScheduler scheduler, int delay = DefaultDelay)
    {
        _editor = editor;
        _providers = editor.SignatureHelpProviders;
        _throttledDelayer = new Delayer<bool>(scheduler, delay);

        _editor.DidBlurEditorWidget += OnBlur;
        _editor.DidChangeConfiguration += OnEditorConfigurationChange;
        _editor.DidChangeModel += OnModelChanged;
        _editor.DidChangeCursorSelection += OnCursorChange;
        _editor.DidChangeModelContent += OnModelContentChange;
        _editor.DidType += OnDidType;
        _providers.DidChange += OnProvidersChanged;

        OnEditorConfigurationChange();
        OnModelChanged();
    }

    /// <summary>The hints to show, or null to hide them (upstream's onChangedHints).</summary>
    public event Action<SignatureHelp?>? OnChangedHints;

    /// <summary>The hints shown, or null.</summary>
    public SignatureHelp? ActiveHints => _state is ActiveState active ? active.Hints : null;

    private State CurrentState
    {
        get => _state;
        set
        {
            if (_state is PendingState pending)
            {
                pending.Cancel();
            }

            _state = value;
        }
    }

    /// <summary>Hides the hints and drops a pending request (upstream's cancel).</summary>
    public void Cancel(bool silent = false)
    {
        CurrentState = State.Default;
        _throttledDelayer.Cancel();
        if (!silent)
        {
            OnChangedHints?.Invoke(null);
        }
    }

    /// <summary>Asks the providers after <paramref name="delay"/> (the model's delay when null) (upstream's trigger).</summary>
    public void Trigger(ParameterHintsTriggerContext context, int? delay = null)
    {
        var model = _editor.Model;
        if (model is null || !_providers.Has(model))
        {
            return;
        }

        var triggerId = ++_triggerId;
        _pendingTriggers.Add(context);
        _ = TriggerCore(triggerId, delay);
    }

    private async Task TriggerCore(int triggerId, int? delay)
    {
        try
        {
            await _throttledDelayer.Trigger(() => DoTrigger(triggerId), delay).ConfigureAwait(true);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception e)
        {
            UnexpectedErrors.Report(e);
        }
    }

    /// <summary>Shows the next signature (upstream's next).</summary>
    public void Next()
    {
        if (_state is not ActiveState active)
        {
            return;
        }

        var length = active.Hints.Signatures.Count;
        var activeSignature = active.Hints.ActiveSignature;
        var last = length > 0 && (activeSignature % length) == (length - 1);
        var cycle = _editor.ParameterHintsOptions.Cycle;

        // if there is only one signature, or we're on last signature of list
        if ((length < 2 || last) && !cycle)
        {
            Cancel();
            return;
        }

        UpdateActiveSignature(last && cycle ? 0 : activeSignature + 1);
    }

    /// <summary>Shows the previous signature (upstream's previous).</summary>
    public void Previous()
    {
        if (_state is not ActiveState active)
        {
            return;
        }

        var length = active.Hints.Signatures.Count;
        var activeSignature = active.Hints.ActiveSignature;
        var first = activeSignature == 0;
        var cycle = _editor.ParameterHintsOptions.Cycle;

        // if there is only one signature, or we're on first signature of list
        if ((length < 2 || first) && !cycle)
        {
            Cancel();
            return;
        }

        UpdateActiveSignature(first && cycle ? length - 1 : activeSignature - 1);
    }

    private void UpdateActiveSignature(int activeSignature)
    {
        if (_state is not ActiveState active)
        {
            return;
        }

        var hints = active.Hints with { ActiveSignature = activeSignature };
        CurrentState = new ActiveState(hints);
        OnChangedHints?.Invoke(hints);
    }

    private async Task<bool> DoTrigger(int triggerId)
    {
        var isRetrigger = _state is ActiveState or PendingState;
        var activeSignatureHelp = GetLastActiveHints();
        Cancel(true);

        if (_pendingTriggers.Count == 0)
        {
            return false;
        }

        var context = _pendingTriggers.Aggregate(MergeTriggerContexts);
        _pendingTriggers.Clear();

        var triggerContext = new SignatureHelpContext(context.TriggerKind, isRetrigger)
        {
            TriggerCharacter = context.TriggerCharacter,
            ActiveSignatureHelp = activeSignatureHelp,
        };

        var model = _editor.Model;
        var position = _editor.GetPosition();
        if (model is null || position is not { } pos)
        {
            return false;
        }

        // as upstream, the request starts (and calls the providers) before the state becomes pending: a provider that
        // cancels and triggers again meanwhile finds the default state
        var pending = new PendingState(activeSignatureHelp);
        pending.Start(token => ProvideSignatureHelpCommand.ProvideSignatureHelp(_providers, model, pos, triggerContext, token));
        CurrentState = pending;

        try
        {
            var result = await pending.Request.ConfigureAwait(true);

            if (triggerId != _triggerId || _disposed)
            {
                result?.Dispose?.Invoke();
                return false;
            }

            if (result is null || result.Value.Signatures.Count == 0)
            {
                result?.Dispose?.Invoke();
                SetLastSignatureHelpResult(null);
                Cancel();
                return false;
            }

            CurrentState = new ActiveState(result.Value);
            SetLastSignatureHelpResult(result);
            OnChangedHints?.Invoke(result.Value);
            return true;
        }
        catch (Exception error)
        {
            if (triggerId == _triggerId)
            {
                CurrentState = State.Default;
            }

            if (error is not OperationCanceledException)
            {
                UnexpectedErrors.Report(error);
            }

            return false;
        }
    }

    private void SetLastSignatureHelpResult(SignatureHelpResult? result)
    {
        if (!ReferenceEquals(_lastSignatureHelpResult, result))
        {
            _lastSignatureHelpResult?.Dispose?.Invoke();
        }

        _lastSignatureHelpResult = result;
    }

    private SignatureHelp? GetLastActiveHints() => _state switch
    {
        ActiveState active => active.Hints,
        PendingState pending => pending.PreviouslyActiveHints,
        _ => null,
    };

    /// <summary>Whether hints are shown, requested or about to be requested (upstream's isTriggered).</summary>
    public bool IsTriggered => _state is ActiveState or PendingState || _throttledDelayer.IsTriggered();

    private void OnBlur() => Cancel();

    private void OnProvidersChanged(int _) => OnModelChanged();

    private void OnModelChanged()
    {
        Cancel();

        _triggerChars.Clear();
        _retriggerChars.Clear();

        var model = _editor.Model;
        if (model is null)
        {
            return;
        }

        foreach (var support in _providers.Ordered(model))
        {
            foreach (var ch in support.SignatureHelpTriggerCharacters ?? [])
            {
                if (ch.Length > 0)
                {
                    _triggerChars.Add(ch[0]);
                    _retriggerChars.Add(ch[0]);
                }
            }

            foreach (var ch in support.SignatureHelpRetriggerCharacters ?? [])
            {
                if (ch.Length > 0)
                {
                    _retriggerChars.Add(ch[0]);
                }
            }
        }
    }

    private void OnDidType(string text)
    {
        if (!_triggerOnType || text.Length == 0)
        {
            return;
        }

        var lastCharIndex = text.Length - 1;
        var triggerCharCode = text[lastCharIndex];

        if (_triggerChars.Contains(triggerCharCode) || (IsTriggered && _retriggerChars.Contains(triggerCharCode)))
        {
            Trigger(new ParameterHintsTriggerContext(SignatureHelpTriggerKind.TriggerCharacter, text[lastCharIndex].ToString()));
        }
    }

    private void OnCursorChange(string source)
    {
        if (source == "mouse")
        {
            Cancel();
        }
        else if (IsTriggered)
        {
            Trigger(new ParameterHintsTriggerContext(SignatureHelpTriggerKind.ContentChange));
        }
    }

    private void OnModelContentChange()
    {
        if (IsTriggered)
        {
            Trigger(new ParameterHintsTriggerContext(SignatureHelpTriggerKind.ContentChange));
        }
    }

    private void OnEditorConfigurationChange()
    {
        _triggerOnType = _editor.ParameterHintsOptions.Enabled;

        if (!_triggerOnType)
        {
            Cancel();
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        Cancel(true);
        _disposed = true;
        SetLastSignatureHelpResult(null);
        _throttledDelayer.Dispose();
        _editor.DidBlurEditorWidget -= OnBlur;
        _editor.DidChangeConfiguration -= OnEditorConfigurationChange;
        _editor.DidChangeModel -= OnModelChanged;
        _editor.DidChangeCursorSelection -= OnCursorChange;
        _editor.DidChangeModelContent -= OnModelContentChange;
        _editor.DidType -= OnDidType;
        _providers.DidChange -= OnProvidersChanged;
        OnChangedHints = null;
    }

    private static ParameterHintsTriggerContext MergeTriggerContexts(ParameterHintsTriggerContext previous, ParameterHintsTriggerContext current) =>
        current.TriggerKind switch
        {
            SignatureHelpTriggerKind.Invoke => current,
            // ignore retriggers if we already have a better trigger context
            SignatureHelpTriggerKind.ContentChange => previous,
            _ => current,
        };

    // upstream's ParameterHintState: Default, Pending (a request and the hints shown before it), Active (the hints)
    private abstract class State
    {
        public static readonly State Default = new DefaultState();
    }

    private sealed class DefaultState : State;

    private sealed class ActiveState(SignatureHelp hints) : State
    {
        public SignatureHelp Hints { get; } = hints;
    }

    private sealed class PendingState(SignatureHelp? previouslyActiveHints) : State
    {
        private readonly CancellationTokenSource _tokenSource = new();
        private readonly TaskCompletionSource<SignatureHelpResult?> _completion = new();
        private bool _settled;

        public SignatureHelp? PreviouslyActiveHints { get; } = previouslyActiveHints;

        public Task<SignatureHelpResult?> Request => _completion.Task;

        public async void Start(Func<CancellationToken, Task<SignatureHelpResult?>> request)
        {
            // upstream's createCancelablePromise disposes its token source once the request settled: cancelling the
            // request afterwards does not reach the provider's token any more (settled is set before the result is
            // handed on, since the continuation may run inline and replace this state)
            try
            {
                var result = await request(_tokenSource.Token).ConfigureAwait(true);
                _settled = true;
                if (!_completion.TrySetResult(result))
                {
                    // cancelled meanwhile: nobody takes the result
                    result?.Dispose?.Invoke();
                }
            }
            catch (OperationCanceledException)
            {
                _settled = true;
                _completion.TrySetCanceled();
            }
            catch (Exception e)
            {
                _settled = true;
                _completion.TrySetException(e);
            }
        }

        public void Cancel()
        {
            if (!_settled)
            {
                _tokenSource.Cancel();
            }

            _completion.TrySetCanceled();
        }
    }
}
