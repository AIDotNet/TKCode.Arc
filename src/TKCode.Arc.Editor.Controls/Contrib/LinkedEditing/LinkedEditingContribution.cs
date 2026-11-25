// Ported from vscode (src/vs/editor/contrib/linkedEditing/browser/linkedEditing.ts: LinkedEditingContribution,
// LinkedEditingAction, the cancelLinkedEditingInput command and editor.linkedEditingBackground; commit b57d5c9e), MIT
// license.
// Differences:
//  - the option is the CodeEditor property LinkedEditing (upstream also reads the deprecated renameOnType);
//  - the decorations are model decorations this contribution keeps the ids of (upstream: a decorations collection);
//    the decoration's background is the theme color LinkedEditing (Arc's DangerSubtle);
//  - the Delayers are dispatcher timers (DispatcherDelayer); the range update waits upstream's debounce maximum (200 ms,
//    upstream learns it from how long the providers take) unless SetDebounceDuration changed it; the sync runs at the
//    next dispatcher turn (upstream's Delayer(0));
//  - the edits that mirror the reference range are computed by the engine (LinkedEditingSupport.ComputeSyncEdits);
//  - LinkedEditingAction's run with a uri and a position (opening another editor) is not ported.

using Avalonia;
using Avalonia.Threading;
using TKCode.Arc.Editor;
using TKCode.Arc.Editor.Contrib.LinkedEditing;
using TKCode.Arc.Editor.Languages;
using TKCode.Arc.Editor.Model;
using TKCode.Arc.Editor.Platform;
using TKCode.Arc.Editor.ViewModel;

namespace TKCode.Arc;

/// <summary>Mirrors edits of one range into the ranges the language links to it (upstream's LinkedEditingContribution).</summary>
internal sealed class LinkedEditingContribution : ICodeEditorContribution
{
    public const string Id = "editor.contrib.linkedEditing";

    /// <summary>The class name of the linked ranges' decoration (upstream's linked-editing-decoration).</summary>
    public const string DecorationClassName = "linked-editing-decoration";

    /// <summary>Whether linked ranges are shown (upstream's CONTEXT_ONTYPE_RENAME_INPUT_VISIBLE).</summary>
    public static readonly RawContextKey<bool> InputVisible = new("LinkedEditingInputVisible", false);

    public static readonly EditorContributionDescription Description =
        new(Id, editor => new LinkedEditingContribution(editor), EditorContributionInstantiation.AfterFirstRender);

    /// <summary>Starts linked editing at the cursor (Ctrl+Shift+F2).</summary>
    public static readonly EditorAction LinkedEditingAction = EditorAction.Create(
        "editor.action.linkedEditing",
        "Start Linked Editing",
        ContextKeyExpr.And(EditorContextKeys.Writable, EditorContextKeys.HasRenameProvider),
        static (editor, _) => Get(editor)?.UpdateRanges(force: true) ?? Task.CompletedTask,
        new EditorKeybinding(KeyMod.CtrlCmd | KeyMod.Shift | (int)KeyCode.F2) { When = EditorContextKeys.EditorTextFocus });

    /// <summary>Stops linked editing (Escape, Shift+Escape).</summary>
    public static readonly EditorCommand CancelLinkedEditingInputCommand = EditorCommand.BindToContribution<LinkedEditingContribution>(
        Id,
        "cancelLinkedEditingInput",
        InputVisible,
        static (controller, _) => controller.ClearRanges(),
        new EditorKeybinding((int)KeyCode.Escape)
        {
            Secondary = [KeyMod.Shift | (int)KeyCode.Escape],
            Weight = KeybindingWeight.EditorContrib + 99,
            When = EditorContextKeys.EditorTextFocus,
        });

    private static readonly ModelDecorationOptions Decoration = new("linked-editing")
    {
        Stickiness = TrackedRangeStickiness.AlwaysGrowsWhenTypingAtEdges,
        ClassName = DecorationClassName,
    };

    private const int DefaultDebounceMs = 200;

    private readonly CodeEditor _editor;
    private readonly IContextKey<bool> _visibleContextKey;
    private readonly IDisposable _style;
    private readonly DispatcherDelayer _rangeUpdateScheduler = new();
    private readonly DispatcherDelayer _rangeSyncScheduler = new();
    private string[] _currentDecorations = []; // The one at index 0 is the reference one
    private int? _debounceDuration;
    private bool _enabled;
    private bool _listening;
    private Task? _rangeUpdateTriggerPromise;
    private Task? _rangeSyncTriggerPromise;
    private CancellationTokenSource? _currentRequestCts;
    private TextPosition? _currentRequestPosition;
    private int? _currentRequestModelVersion;
    private int _syncRangesToken;
    private System.Text.RegularExpressions.Regex? _languageWordPattern;
    private System.Text.RegularExpressions.Regex? _currentWordPattern;
    private bool _ignoreChangeEvent;

    private LinkedEditingContribution(CodeEditor editor)
    {
        _editor = editor;
        _visibleContextKey = editor.CreateContextKey(InputVisible);
        _style = editor.DecorationStyles.Register(DecorationClassName, new CodeEditorDecorationStyle { Background = CodeEditorThemeColor.LinkedEditing });
        editor.PropertyChanged += OnEditorPropertyChanged;
        editor.LanguageFeatures.LinkedEditingRangeProvider.DidChange += OnProvidersChanged;
        editor.DidChangeModelLanguage += OnLanguageChanged;
        editor.LanguageConfigurationService.DidChange += OnLanguageConfigurationChanged;
        Reinitialize(forceRefresh: true);
    }

    public static LinkedEditingContribution? Get(CodeEditor editor) => editor.GetContribution<LinkedEditingContribution>(Id);

    /// <summary>The ranges linked now, the reference first (for tests).</summary>
    internal IReadOnlyList<TextRange> CurrentRanges =>
        [.. _currentDecorations.Select(id => _editor.Model.GetDecorationRange(id)).OfType<TextRange>()];

    /// <summary>The pending range update (upstream's currentUpdateTriggerPromise).</summary>
    internal Task CurrentUpdateTriggerPromise => _rangeUpdateTriggerPromise ?? Task.CompletedTask;

    /// <summary>The pending sync (upstream's currentSyncTriggerPromise).</summary>
    internal Task CurrentSyncTriggerPromise => _rangeSyncTriggerPromise ?? Task.CompletedTask;

    /// <summary>For tests: the delay before the ranges are asked for again (upstream's setDebounceDuration).</summary>
    internal void SetDebounceDuration(int timeInMs) => _debounceDuration = timeInMs;

    public void Dispose()
    {
        ClearRanges();
        StopListening();
        _rangeUpdateScheduler.Dispose();
        _rangeSyncScheduler.Dispose();
        _style.Dispose();
        _editor.PropertyChanged -= OnEditorPropertyChanged;
        _editor.LanguageFeatures.LinkedEditingRangeProvider.DidChange -= OnProvidersChanged;
        _editor.DidChangeModelLanguage -= OnLanguageChanged;
        _editor.LanguageConfigurationService.DidChange -= OnLanguageConfigurationChanged;
    }

    private void OnEditorPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == CodeEditor.LinkedEditingProperty)
        {
            Reinitialize(forceRefresh: false);
        }
    }

    private void OnProvidersChanged(int count) => Reinitialize(forceRefresh: false);

    private void OnLanguageChanged(ModelLanguageChangedEvent e) => Reinitialize(forceRefresh: true);

    private void OnLanguageConfigurationChanged(LanguageConfigurationServiceChangeEvent e) => UpdateLanguageWordPattern();

    private void UpdateLanguageWordPattern() =>
        _languageWordPattern = _editor.LanguageConfigurationService.GetLanguageConfiguration(_editor.Model.GetLanguageId()).GetWordDefinition();

    private void Reinitialize(bool forceRefresh)
    {
        var model = _editor.Model;
        var isEnabled = _editor.LinkedEditing && _editor.LanguageFeatures.LinkedEditingRangeProvider.Has(model);
        if (isEnabled == _enabled && !forceRefresh)
        {
            return;
        }

        _enabled = isEnabled;
        ClearRanges();
        StopListening();
        if (!isEnabled)
        {
            return;
        }

        UpdateLanguageWordPattern();
        _editor.DidChangeCursorSelection += OnCursorChanged;
        _editor.DidChangeModelContent += OnContentChanged;
        _listening = true;
        _ = UpdateRanges();
    }

    private void StopListening()
    {
        if (_listening)
        {
            _editor.DidChangeCursorSelection -= OnCursorChanged;
            _editor.DidChangeModelContent -= OnContentChanged;
            _listening = false;
        }

        _rangeUpdateScheduler.Cancel();
        _rangeSyncScheduler.Cancel();
    }

    private void TriggerRangeUpdate() =>
        _rangeUpdateTriggerPromise = _rangeUpdateScheduler.Trigger(() => UpdateRanges(), _debounceDuration ?? DefaultDebounceMs);

    private void TriggerRangeSync(int token) =>
        _rangeSyncTriggerPromise = _rangeSyncScheduler.Trigger(() =>
        {
            SyncRanges(token);
            return Task.CompletedTask;
        }, 0);

    private void OnCursorChanged(CursorStateChangedEvent e) => TriggerRangeUpdate();

    private void OnContentChanged(ModelContentChangedEvent e)
    {
        if (!_ignoreChangeEvent && _currentDecorations.Length > 0)
        {
            var referenceRange = _editor.Model.GetDecorationRange(_currentDecorations[0]);
            if (referenceRange is { } reference && e.Changes.All(c => reference.IntersectRanges(c.Range) is not null))
            {
                _currentRequestModelVersion = _editor.Model.GetVersionId();
                TriggerRangeSync(_syncRangesToken);
                return;
            }
        }

        TriggerRangeUpdate();
    }

    private void SyncRanges(int token)
    {
        // delayed invocation, make sure we're still on
        if (token != _syncRangesToken || _currentDecorations.Length == 0)
        {
            // nothing to do
            return;
        }

        var model = _editor.Model;
        if (model.GetDecorationRange(_currentDecorations[0]) is not { } referenceRange)
        {
            return;
        }

        var mirrors = _currentDecorations.Skip(1).Select(id => model.GetDecorationRange(id)).ToList();
        var edits = LinkedEditingSupport.ComputeSyncEdits(model, referenceRange, mirrors, _currentWordPattern);
        if (edits is null)
        {
            ClearRanges();
            return;
        }

        if (edits.Count == 0)
        {
            return;
        }

        try
        {
            // the mirrored edits join the undo step of the edit that triggered them
            model.PopStackElement();
            _ignoreChangeEvent = true;
            var viewModel = _editor.ViewModel;
            var prevEditOperationType = viewModel?.GetPrevEditOperationType();
            _editor.ExecuteEdits("linkedEditing", edits);
            if (viewModel is not null && prevEditOperationType is { } type)
            {
                viewModel.SetPrevEditOperationType(type);
            }
        }
        finally
        {
            _ignoreChangeEvent = false;
        }
    }

    /// <summary>Removes the linked ranges (upstream's clearRanges).</summary>
    public void ClearRanges()
    {
        _visibleContextKey.Set(false);
        if (_currentDecorations.Length > 0 && !_editor.Model.IsDisposed())
        {
            _currentDecorations = _editor.Model.DeltaDecorations(_currentDecorations, []);
        }

        _currentDecorations = [];
        if (_currentRequestCts is not null)
        {
            _currentRequestCts.Cancel();
            _currentRequestCts = null;
            _currentRequestPosition = null;
        }
    }

    /// <summary>Asks the providers for the ranges at the cursor (upstream's updateRanges).</summary>
    public async Task UpdateRanges(bool force = false)
    {
        if (_editor.GetPosition() is not { } position)
        {
            ClearRanges();
            return;
        }

        if ((!_enabled && !force) || _editor.GetSelections().Count > 1)
        {
            // disabled or multicursor
            ClearRanges();
            return;
        }

        var model = _editor.Model;
        var modelVersionId = model.GetVersionId();
        if (_currentRequestPosition is { } requestPosition && _currentRequestModelVersion == modelVersionId)
        {
            if (position == requestPosition)
            {
                return; // same position
            }

            if (_currentDecorations.Length > 0 && model.GetDecorationRange(_currentDecorations[0]) is { } range && range.ContainsPosition(position))
            {
                return; // just moving inside the existing primary range
            }
        }

        if (_currentRequestPosition != position)
        {
            // If there is no current range or the current range does not contain the new position, clear the ranges
            var currentRange = _currentDecorations.Length > 0 ? model.GetDecorationRange(_currentDecorations[0]) : null;
            if (currentRange is not { } current || !current.ContainsPosition(position))
            {
                // Clear existing decorations while we compute new ones
                ClearRanges();
            }
        }

        _currentRequestPosition = position;
        _currentRequestModelVersion = modelVersionId;
        var currentRequestCts = _currentRequestCts = new CancellationTokenSource();
        try
        {
            var response = await LinkedEditingSupport.GetLinkedEditingRanges(_editor.LanguageFeatures.LinkedEditingRangeProvider, model, position, currentRequestCts.Token).ConfigureAwait(true);
            if (currentRequestCts != _currentRequestCts)
            {
                return;
            }

            _currentRequestCts = null;
            if (modelVersionId != model.GetVersionId())
            {
                return;
            }

            var ranges = response?.Ranges.ToList() ?? [];
            _currentWordPattern = response?.WordPattern ?? _languageWordPattern;

            var foundReferenceRange = false;
            for (var i = 0; i < ranges.Count; i++)
            {
                if (ranges[i].ContainsPosition(position))
                {
                    foundReferenceRange = true;
                    if (i != 0)
                    {
                        var referenceRange = ranges[i];
                        ranges.RemoveAt(i);
                        ranges.Insert(0, referenceRange);
                    }

                    break;
                }
            }

            if (!foundReferenceRange)
            {
                // Cannot do linked editing if the ranges are not where the cursor is...
                ClearRanges();
                return;
            }

            _visibleContextKey.Set(true);
            _currentDecorations = model.DeltaDecorations(_currentDecorations, [.. ranges.Select(static range => new ModelDeltaDecoration(range, Decoration))]);
            _syncRangesToken++; // cancel any pending syncRanges call
        }
        catch (Exception e)
        {
            if (e is not OperationCanceledException)
            {
                UnexpectedErrors.Report(e);
            }

            if (_currentRequestCts == currentRequestCts || _currentRequestCts is null)
            {
                // stop if we are still the latest request
                ClearRanges();
            }
        }
    }
}

/// <summary>
/// Runs a task after a delay on the UI thread; triggering again restarts the delay and the earlier callers get the
/// result of the run that happens (upstream's Delayer).
/// </summary>
internal sealed class DispatcherDelayer : IDisposable
{
    private DispatcherTimer? _timer;
    private Func<Task>? _task;
    private TaskCompletionSource? _completion;
    private bool _postPending;

    public Task Trigger(Func<Task> task, int delayMs)
    {
        _task = task;
        _completion ??= new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _timer?.Stop();
        if (delayMs <= 0)
        {
            if (!_postPending)
            {
                _postPending = true;
                Dispatcher.UIThread.Post(Run, DispatcherPriority.Background);
            }
        }
        else
        {
            _timer ??= new DispatcherTimer(DispatcherPriority.Background);
            _timer.Interval = TimeSpan.FromMilliseconds(delayMs);
            _timer.Tick -= OnTick;
            _timer.Tick += OnTick;
            _timer.Start();
        }

        return _completion.Task;
    }

    public void Cancel()
    {
        _timer?.Stop();
        _task = null;
        _completion?.TrySetCanceled();
        _completion = null;
    }

    public void Dispose() => Cancel();

    private void OnTick(object? sender, EventArgs e)
    {
        _timer?.Stop();
        Run();
    }

    private async void Run()
    {
        _postPending = false;
        var task = _task;
        var completion = _completion;
        _task = null;
        _completion = null;
        if (task is null || completion is null)
        {
            return;
        }

        try
        {
            await task().ConfigureAwait(true);
            completion.TrySetResult();
        }
        catch (Exception e)
        {
            completion.TrySetException(e);
        }
    }
}
