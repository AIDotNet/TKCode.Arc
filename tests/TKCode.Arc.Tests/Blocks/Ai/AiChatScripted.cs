using Avalonia.Threading;

namespace TKCode.Arc.Tests.Blocks.Ai;

/// <summary>
/// A responder whose replies the test writes: every request makes a <see cref="ScriptedCall"/>, and the test sends its
/// events (<see cref="ScriptedCall.Push"/>), ends it, fails it or throws. Events sent on the UI thread reach the chat at
/// once (the awaiting code continues inline on the same synchronization context).
/// </summary>
internal sealed class ScriptedResponder : IAiChatResponder
{
    public List<ScriptedCall> Calls { get; } = new();

    public ScriptedCall Last => Calls[^1];

    public IAsyncEnumerable<AiChatStreamEvent> RespondAsync(AiChatRequest request, CancellationToken cancellationToken)
    {
        var call = new ScriptedCall(request, cancellationToken);
        Calls.Add(call);
        return call;
    }
}

/// <summary>One reply of a <see cref="ScriptedResponder"/>: an async stream the test feeds.</summary>
internal sealed class ScriptedCall : IAsyncEnumerable<AiChatStreamEvent>, IAsyncEnumerator<AiChatStreamEvent>
{
    private readonly Queue<AiChatStreamEvent> _queue = new();
    private TaskCompletionSource<bool>? _waiting;
    private CancellationToken _token;
    private bool _ended;
    private Exception? _error;

    public ScriptedCall(AiChatRequest request, CancellationToken token)
    {
        Request = request;
        _token = token;
    }

    public AiChatRequest Request { get; }

    /// <summary>Whether the chat cancelled the reply.</summary>
    public bool IsCancelled => _token.IsCancellationRequested;

    public AiChatStreamEvent Current { get; private set; } = null!;

    public IAsyncEnumerator<AiChatStreamEvent> GetAsyncEnumerator(CancellationToken cancellationToken = default)
    {
        if (cancellationToken.CanBeCanceled)
        {
            _token = cancellationToken;
        }
        _token.Register(() => _waiting?.TrySetCanceled(_token));
        return this;
    }

    public ValueTask<bool> MoveNextAsync()
    {
        _token.ThrowIfCancellationRequested();
        if (_queue.Count > 0)
        {
            Current = _queue.Dequeue();
            return new ValueTask<bool>(true);
        }
        if (_error is { } error)
        {
            _error = null;
            return ValueTask.FromException<bool>(error);
        }
        if (_ended)
        {
            return new ValueTask<bool>(false);
        }
        _waiting = new TaskCompletionSource<bool>();
        return new ValueTask<bool>(_waiting.Task);
    }

    /// <summary>Sends events (each one reaches the chat before the next is sent).</summary>
    public ScriptedCall Push(params AiChatStreamEvent[] events)
    {
        foreach (var item in events)
        {
            if (_waiting is { } waiting)
            {
                _waiting = null;
                Current = item;
                waiting.TrySetResult(true);
            }
            else
            {
                _queue.Enqueue(item);
            }
            Dispatcher.UIThread.RunJobs();
        }
        return this;
    }

    /// <summary>Sends text.</summary>
    public ScriptedCall Text(string text) => Push(new AiChatTextDelta(text));

    /// <summary>Ends the stream.</summary>
    public void End()
    {
        _ended = true;
        if (_waiting is { } waiting)
        {
            _waiting = null;
            waiting.TrySetResult(false);
        }
        Dispatcher.UIThread.RunJobs();
    }

    /// <summary>Throws <paramref name="error"/> from the stream.</summary>
    public void Throw(Exception error)
    {
        if (_waiting is { } waiting)
        {
            _waiting = null;
            waiting.TrySetException(error);
        }
        else
        {
            _error = error;
        }
        Dispatcher.UIThread.RunJobs();
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}

/// <summary>A clock the test moves.</summary>
internal sealed class ManualTime : TimeProvider
{
    public DateTimeOffset Now { get; set; } = new(2026, 10, 23, 9, 0, 0, TimeSpan.Zero);

    public override DateTimeOffset GetUtcNow() => Now;

    public void Advance(TimeSpan by) => Now += by;
}
