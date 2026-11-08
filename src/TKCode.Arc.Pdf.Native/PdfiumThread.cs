namespace TKCode.Arc;

/// <summary>What a call to <see cref="PdfiumThread"/> competes with; lower runs first, the same priority in order of arrival.</summary>
internal enum PdfiumPriority
{
    /// <summary>Starting the library and closing documents: frees memory and stops the next work from touching a closed document.</summary>
    Lifetime,

    /// <summary>Opening a document.</summary>
    Interactive,

    /// <summary>Page bitmaps.</summary>
    Render,

    /// <summary>Text, links, outline and page labels: search walks every page, which must not hold up what is on screen.</summary>
    Background
}

/// <summary>
/// The one thread of the process that calls PDFium, which is not thread-safe, with a priority queue. Rendering a page cannot be
/// interrupted, so cancelling ends the caller's wait at once and the work, if it has not started, is skipped when it is dequeued.
/// A call whose work already started finishes on the thread and its result is dropped.
/// </summary>
internal sealed class PdfiumThread
{
    private readonly object _gate = new();
    private readonly PriorityQueue<WorkItem, (int Priority, long Order)> _queue = new();
    private readonly Thread _thread;
    private long _order;

    private PdfiumThread()
    {
        _thread = new Thread(Loop)
        {
            Name = "TKCode.Arc.Pdfium",
            IsBackground = true
        };
        _thread.Start();
    }

    /// <summary>The thread of the process.</summary>
    public static PdfiumThread Instance { get; } = new();

    /// <summary>Whether the caller is the PDFium thread itself, which must not wait for its own queue.</summary>
    public bool IsCurrent => Thread.CurrentThread == _thread;

    /// <summary>Runs <paramref name="work"/> on the thread. Continuations never run on the thread.</summary>
    /// <param name="work">The work.</param>
    /// <param name="priority">Its priority.</param>
    /// <param name="cancellationToken">Ends the wait and skips the work if it has not started.</param>
    /// <param name="abandoned">Runs on the thread with a result nobody takes because the call was cancelled meanwhile: frees what the result holds.</param>
    public Task<T> RunAsync<T>(Func<T> work, PdfiumPriority priority, CancellationToken cancellationToken, Action<T>? abandoned = null)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return Task.FromCanceled<T>(cancellationToken);
        }
        var item = new WorkItem<T>(work, abandoned, cancellationToken);
        lock (_gate)
        {
            _queue.Enqueue(item, ((int)priority, _order++));
            Monitor.Pulse(_gate);
        }
        return item.Task;
    }

    private void Loop()
    {
        while (true)
        {
            WorkItem item;
            lock (_gate)
            {
                while (_queue.Count == 0)
                {
                    Monitor.Wait(_gate);
                }
                item = _queue.Dequeue();
            }
            item.Run();
        }
    }

    private abstract class WorkItem
    {
        public abstract void Run();
    }

    private sealed class WorkItem<T> : WorkItem
    {
        private readonly TaskCompletionSource<T> _completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly Func<T> _work;
        private readonly Action<T>? _abandoned;
        private readonly CancellationToken _token;
        private readonly CancellationTokenRegistration _registration;

        public WorkItem(Func<T> work, Action<T>? abandoned, CancellationToken token)
        {
            _work = work;
            _abandoned = abandoned;
            _token = token;
            _registration = token.Register(static state =>
            {
                var item = (WorkItem<T>)state!;
                item._completion.TrySetCanceled(item._token);
            }, this);
        }

        public Task<T> Task => _completion.Task;

        public override void Run()
        {
            try
            {
                if (_token.IsCancellationRequested)
                {
                    _completion.TrySetCanceled(_token);
                    return;
                }
                var result = _work();
                if (!_completion.TrySetResult(result))
                {
                    _abandoned?.Invoke(result);
                }
            }
            catch (OperationCanceledException)
            {
                _completion.TrySetCanceled(_token.IsCancellationRequested ? _token : new CancellationToken(true));
            }
            catch (Exception exception)
            {
                _completion.TrySetException(exception);
            }
            finally
            {
                _registration.Dispose();
            }
        }
    }
}
