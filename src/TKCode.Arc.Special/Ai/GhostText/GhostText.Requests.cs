using Avalonia.Threading;

namespace TKCode.Arc;

public partial class GhostText
{
    private DispatcherTimer? _debounce;
    private CancellationTokenSource? _cancel;
    private int _requestId;
    private bool _firstChunk;

    /// <summary>The function asked for suggestions: <see cref="Suggest"/>, or the simulation's while it plays or once it was taken over.</summary>
    internal GhostTextSuggest? EffectiveSuggest => Suggest ?? (IsTakenOver || IsSampling ? _simulation?.Answer : null);

    /// <summary>True when a pause would ask for a suggestion: enabled, focused (or played by the sample), caret at the end, long enough.</summary>
    private bool CanAskAfterPause =>
        _editor is { } editor && IsEffectivelyEnabled && (editor.IsFocused || _simulation?.IsRunning == true) &&
        EffectiveSuggest is not null && editor.CaretAtEnd && (editor.Text ?? string.Empty).Trim().Length >= Math.Max(0, MinLength);

    /// <summary>Restarts the pause: one debounced request at a time; every keystroke clears the timer.</summary>
    private void ScheduleRequest()
    {
        _debounce?.Stop();
        if (!CanAskAfterPause || _simulation?.IsRunning == true)
        {
            return;
        }
        var delay = Delay < TimeSpan.Zero ? TimeSpan.Zero : Delay;
        if (_debounce is null)
        {
            _debounce = new DispatcherTimer(DispatcherPriority.Normal);
            _debounce.Tick += (_, _) =>
            {
                _debounce!.Stop();
                if (CanAskAfterPause)
                {
                    StartRequest();
                }
            };
        }
        _debounce.Interval = delay;
        _debounce.Start();
    }

    /// <summary>True while the pause timer runs (tests).</summary>
    internal bool IsWaitingForPause => _debounce?.IsEnabled == true;

    /// <summary>Fires the pause timer now (tests).</summary>
    internal bool FlushPause()
    {
        if (_debounce?.IsEnabled != true)
        {
            return false;
        }
        _debounce.Stop();
        return CanAskAfterPause && StartRequest();
    }

    /// <summary>Aborts the request in flight; its late chunks are ignored.</summary>
    private void CancelRequest()
    {
        _requestId++;
        if (_cancel is not null)
        {
            _cancel.Cancel();
            _cancel.Dispose();
            _cancel = null;
        }
        IsStreaming = false;
        if (Status == GhostTextStatus.Loading)
        {
            Status = GhostTextStatus.Idle;
        }
    }

    private bool StartRequest()
    {
        CancelRequest();
        if (_editor is not { } editor || EffectiveSuggest is not { } suggest || !IsEffectivelyEnabled)
        {
            return false;
        }
        var text = editor.Text ?? string.Empty;
        if (text.Trim().Length < Math.Max(0, MinLength) || !editor.CaretAtEnd)
        {
            return false;
        }
        ClearSuggestion(fade: false);
        var cancel = new CancellationTokenSource();
        _cancel = cancel;
        var id = _requestId;
        GhostTextResponse? response;
        try
        {
            response = suggest(text, cancel.Token);
        }
        catch (OperationCanceledException)
        {
            return false;
        }
        catch (Exception)
        {
            Fail(id);
            return true;
        }
        if (response is null)
        {
            // Skipped synchronously: no Thinking.
            Status = GhostTextStatus.Idle;
            return false;
        }
        _firstChunk = true;
        if (response.Text is { } whole)
        {
            Receive(id, whole);
            Complete(id);
            return true;
        }
        Status = GhostTextStatus.Loading;
        if (response.Task is { } task)
        {
            _ = AwaitTask(id, task, cancel.Token);
        }
        else if (response.Stream is { } stream)
        {
            _ = AwaitStream(id, stream, cancel.Token);
        }
        return true;
    }

    private async Task AwaitTask(int id, Task<string?> task, CancellationToken token)
    {
        string? result;
        try
        {
            result = await task;
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested || id != _requestId)
        {
            return;
        }
        catch (Exception)
        {
            Fail(id);
            return;
        }
        if (id != _requestId)
        {
            return;
        }
        Receive(id, result ?? string.Empty);
        Complete(id);
    }

    private async Task AwaitStream(int id, IAsyncEnumerable<string> stream, CancellationToken token)
    {
        try
        {
            await foreach (var chunk in stream.WithCancellation(token))
            {
                if (id != _requestId)
                {
                    return;
                }
                Receive(id, chunk);
            }
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested || id != _requestId)
        {
            return;
        }
        catch (Exception)
        {
            Fail(id);
            return;
        }
        Complete(id);
    }

    /// <summary>A chunk arrived: it joins the ghost and fades in on its own.</summary>
    private void Receive(int id, string chunk)
    {
        if (id != _requestId || _editor is not { } editor || string.IsNullOrEmpty(chunk))
        {
            return;
        }
        if (!IsMultiline)
        {
            chunk = SingleLine(chunk);
        }
        if (editor.Ghost.Length == 0 || editor.IsGhostLeaving)
        {
            // The component strips a leading space when the text already ends with whitespace.
            var text = editor.Text ?? string.Empty;
            if (text.Length > 0 && char.IsWhiteSpace(text[^1]))
            {
                chunk = chunk.TrimStart();
            }
            if (chunk.Length == 0)
            {
                return;
            }
        }
        editor.AppendGhost(chunk, fade: true);
        Suggestion = editor.Ghost;
        if (_firstChunk)
        {
            _firstChunk = false;
            IsStreaming = true;
            Status = GhostTextStatus.Ready;
        }
    }

    /// <summary>The suggestion is complete: announce it, or go back to idle when nothing came.</summary>
    private void Complete(int id)
    {
        if (id != _requestId)
        {
            return;
        }
        IsStreaming = false;
        _cancel?.Dispose();
        _cancel = null;
        if (_editor is not { } editor || editor.Ghost.Length == 0)
        {
            if (Status != GhostTextStatus.Error)
            {
                Status = GhostTextStatus.Idle;
            }
            return;
        }
        _announcer?.Announce(Labels.SuggestionAnnouncement(editor.Ghost.Trim()));
    }

    private void Fail(int id)
    {
        if (id != _requestId)
        {
            return;
        }
        _cancel?.Dispose();
        _cancel = null;
        _editor?.ClearGhost();
        Suggestion = string.Empty;
        IsStreaming = false;
        Status = GhostTextStatus.Error;
        _announcer?.Announce(Labels.ErrorAnnouncement);
    }
}
