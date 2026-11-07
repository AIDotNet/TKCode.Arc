namespace TKCode.Arc;

/// <summary>Runs the black hole's transitions one after another: a release asked for during a swallow waits its turn.</summary>
internal sealed class BlackHoleQueue
{
    private Task _tail = Task.CompletedTask;
    private int _pending;

    /// <summary>Transitions queued or running.</summary>
    public int Pending => _pending;

    /// <summary>Queues <paramref name="work"/> after everything queued before it; the task completes when it has run.</summary>
    public Task Enqueue(Func<Task> work)
    {
        _pending++;
        var next = RunAfter(_tail, work);
        _tail = next;
        return next;
    }

    private async Task RunAfter(Task previous, Func<Task> work)
    {
        try
        {
            await previous;
        }
        catch (Exception)
        {
            // The earlier call already reported its own failure to its caller; the queue moves on.
        }
        try
        {
            await work();
        }
        finally
        {
            _pending--;
        }
    }
}
