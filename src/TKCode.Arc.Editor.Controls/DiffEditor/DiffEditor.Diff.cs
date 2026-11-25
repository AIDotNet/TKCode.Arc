// The diff computation of the diff editor (upstream's DiffEditorViewModel in
// src/vs/editor/browser/widget/diffEditor/diffEditorViewModel.ts, commit b57d5c9e; MIT license): the diff is computed
// on the thread pool from snapshots of the two models (upstream: in a web worker), after a 200 ms pause in typing, and a
// newer computation cancels an older one.
// Differences: no observables; a result is dropped when a model changed while it was computed (a newer computation is
// scheduled then); upstream's applyOriginalEdits / applyModifiedEdits are stubs there too.

using Avalonia.Threading;
using TKCode.Arc.Editor.Diff;
using TKCode.Arc.Editor.Model;

namespace TKCode.Arc;

public sealed partial class DiffEditor
{
    /// <summary>The pause after an edit before the diff is computed again (upstream's debouncer).</summary>
    internal static readonly TimeSpan RecomputeDelay = TimeSpan.FromMilliseconds(200);

    /// <summary>The time a diff may take (upstream's maxComputationTime default).</summary>
    internal const int MaxComputationTimeMs = 5000;

    private DocumentDiff? _diff;
    private CancellationTokenSource? _diffCancellation;
    private DispatcherTimer? _debounceTimer;
    private bool _immediatePending;
    private int _diffGeneration;

    /// <summary>The diff shown now, or null before the first one.</summary>
    internal DocumentDiff? Diff => _diff;

    /// <summary>Whether the diff shown is the diff of the current texts (upstream's isDiffUpToDate).</summary>
    internal bool IsDiffUpToDate { get; private set; }

    /// <summary>Completes when the next diff was shown (for tests and programmatic callers).</summary>
    private TaskCompletionSource? _diffShown;

    /// <summary>Waits for the diff of the current texts (upstream's waitForDiff).</summary>
    internal Task WaitForDiffAsync()
    {
        if (IsDiffUpToDate)
        {
            return Task.CompletedTask;
        }

        _diffShown ??= new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        return _diffShown.Task;
    }

    private void ScheduleDiff(bool immediately)
    {
        if (_originalEditor is null || _modifiedEditor is null)
        {
            return;
        }

        IsDiffUpToDate = false;
        if (immediately)
        {
            _debounceTimer?.Stop();
            if (_immediatePending)
            {
                return;
            }

            // coalesce the property changes of one dispatcher turn (text, language, options set together)
            _immediatePending = true;
            Dispatcher.UIThread.Post(() =>
            {
                _immediatePending = false;
                StartDiff();
            }, DispatcherPriority.Background);
            return;
        }

        _debounceTimer ??= new DispatcherTimer(RecomputeDelay, DispatcherPriority.Background, (_, _) =>
        {
            _debounceTimer!.Stop();
            StartDiff();
        });
        _debounceTimer.Stop();
        _debounceTimer.Start();
    }

    private void CancelDiff()
    {
        _debounceTimer?.Stop();
        _diffCancellation?.Cancel();
        _diffCancellation?.Dispose();
        _diffCancellation = null;
    }

    private DocumentDiffProviderOptions CreateDiffOptions() => new()
    {
        IgnoreTrimWhitespace = IgnoreTrimWhitespace,
        MaxComputationTimeMs = MaxComputationTimeMs,
        ComputeMoves = true,
    };

    // Takes snapshots of both models and computes their diff on the thread pool.
    private async void StartDiff()
    {
        if (_originalEditor is not { } original || _modifiedEditor is not { } modified)
        {
            return;
        }

        CancelDiff();
        var cancellation = new CancellationTokenSource();
        _diffCancellation = cancellation;
        var generation = ++_diffGeneration;
        var originalModel = original.Model;
        var modifiedModel = modified.Model;
        var originalVersion = originalModel.GetVersionId();
        var modifiedVersion = modifiedModel.GetVersionId();
        var originalSnapshot = originalModel.CreateTextSnapshot();
        var modifiedSnapshot = modifiedModel.CreateTextSnapshot();

        DocumentDiff result;
        try
        {
            result = await DocumentDiffComputer.ComputeDiffAsync(originalSnapshot, modifiedSnapshot, CreateDiffOptions(), cancellation.Token);
        }
        catch (OperationCanceledException)
        {
            return;
        }
        catch (Exception error)
        {
            // a failed diff leaves the last one shown (upstream: onUnexpectedError)
            Editor.UnexpectedErrors.Report(error);
            return;
        }

        if (generation != _diffGeneration || cancellation.IsCancellationRequested
            || !ReferenceEquals(original, _originalEditor) || !ReferenceEquals(modified, _modifiedEditor)
            || originalModel.GetVersionId() != originalVersion || modifiedModel.GetVersionId() != modifiedVersion)
        {
            // the texts changed meanwhile; a newer computation follows
            return;
        }

        ShowDiff(result);
    }

    /// <summary>Computes the diff of the current texts on the calling thread and shows it (tests use it).</summary>
    internal void ComputeDiffNow()
    {
        if (_originalEditor is not { } original || _modifiedEditor is not { } modified)
        {
            return;
        }

        CancelDiff();
        _immediatePending = false;
        _diffGeneration++;
        var originalLines = DocumentDiffComputer.ReadLines(original.Model.CreateTextSnapshot());
        var modifiedLines = DocumentDiffComputer.ReadLines(modified.Model.CreateTextSnapshot());
        ShowDiff(DocumentDiffComputer.ComputeDiff(originalLines, modifiedLines, CreateDiffOptions()));
    }

    private void ShowDiff(DocumentDiff diff)
    {
        _diff = diff;
        IsDiffUpToDate = true;
        UpdateUnchangedRegions(diff);
        ApplyDecorations();
        UpdateViewZones();
        _revertColumn?.Invalidate();
        _movedBlocks?.InvalidateVisual();
        _overviewRuler?.InvalidateVisual();

        var inserted = 0;
        var removed = 0;
        foreach (var change in diff.Changes)
        {
            inserted += change.Modified.Length;
            removed += change.Original.Length;
        }

        DiffComputed?.Invoke(this, new DiffComputedEventArgs(diff.Changes.Count, inserted, removed, diff.Moves.Count, diff.Identical, diff.QuitEarly));

        var shown = _diffShown;
        _diffShown = null;
        shown?.TrySetResult();
    }

    private void ClearDiffPresentation()
    {
        ClearDecorations();
        ClearViewZones();
        _unchangedRegions = [];
    }
}
